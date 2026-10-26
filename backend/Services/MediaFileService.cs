using CdArchiveBackend.Data.Database;
using CdArchiveBackend.Data.DTO;
using CdArchiveBackend.Data.External;
using CdArchiveBackend.Interfaces;
using CdArchiveBackend.Services.External;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using static CdArchiveBackend.Common.Utilities;

namespace CdArchiveBackend.Services
{
    using ImageOptions = CdArchiveBackend.Data.ImageStore.Options;

    // Turns a NewImage into a MediaFile: stores the file and adds its row to the context, for the
    // caller to save together with whatever uses it.
    //  - Uploads go to a folder of the user's own, and are only reachable through signed links
    //  - Looked-up images are looked up again (we don't download a URL the client gave us), and
    //    go to the shared folder together with the credit their license asks for
    // The same file is only stored once per owner (or once in the shared folder).
    internal sealed class MediaFileService(HttpClient httpClient,
                                           DatabaseContext dbContext,
                                           IFileSystem fileSystem,
                                           IOptions<ImageOptions> options,
                                           MusicBrainzService musicBrainzService,
                                           CoverArtService coverArtService,
                                           WikimediaService wikimediaService)
    {
        public const int MaxImageSizeBytes = 1 << 23; // 8 MiB

        public enum ImageError
        {
            None,
            Invalid,        // Not an image we take, or too big
            NotFound,       // The lookup no longer finds an image
            Unavailable     // The service we'd get it from is down or rate limiting us
        }

        public readonly record struct ImageResult(MediaFile? File, ImageError Error);

        private readonly record struct ImageType(string ContentType, string Extension);

        private static ReadOnlySpan<byte> JpegMagic => [0xFF, 0xD8, 0xFF];
        private static ReadOnlySpan<byte> PngMagic => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        private readonly HttpClient _httpClient = httpClient;
        private readonly DatabaseContext _dbContext = dbContext;
        private readonly IFileSystem _fileSystem = fileSystem;
        private readonly ImageOptions _options = options.Value;
        private readonly MusicBrainzService _musicBrainzService = musicBrainzService;
        private readonly CoverArtService _coverArtService = coverArtService;
        private readonly WikimediaService _wikimediaService = wikimediaService;

        // Files written in this request, so they can be removed if saving their rows fails
        private readonly List<string> _writtenFiles = [];

        // image must have been validated (NewImage.Validate) against files
        public async Task<ImageResult> AddAsync(long userId, NewImage image, IFormFileCollection files, CancellationToken ct)
            => image.Source switch
            {
                MediaSource.Upload => await AddUploadAsync(userId, files[image.FileIndex!.Value], ct).ConfigureAwait(false),
                MediaSource.CoverArtArchive => await AddLookedUpAsync(
                    await _coverArtService.GetFrontCoverAsync(image.ExternalId!, null, ct).ConfigureAwait(false),
                    MediaSource.CoverArtArchive,
                    ct).ConfigureAwait(false),
                MediaSource.Wikimedia => await AddArtistPhotoAsync(image.ExternalId!, ct).ConfigureAwait(false),
                _ => new(null, ImageError.Invalid)
            };

        // For when saving the rows of the files failed. Best effort: a file left behind is only
        // wasted space.
        public void DeleteWrittenFiles()
        {
            foreach (var path in _writtenFiles)
            {
                try
                {
                    _fileSystem.DeleteFile(path);
                }
                catch (Exception)
                {
                }
            }

            _writtenFiles.Clear();
        }

        private async Task<ImageResult> AddUploadAsync(long userId, IFormFile file, CancellationToken ct)
        {
            if (file.Length == 0 || file.Length > MaxImageSizeBytes)
                return new(null, ImageError.Invalid);

            using var buffer = new MemoryStream((int)file.Length);
            await file.CopyToAsync(buffer, ct).ConfigureAwait(false);
            var bytes = buffer.ToArray();

            // What the file is, not what the browser says it is
            if (DetectImageType(bytes) is not ImageType type)
                return new(null, ImageError.Invalid);

            var mediaFile = await StoreAsync(bytes, type, userId, MediaSource.Upload, credit: null, ct).ConfigureAwait(false);
            return new(mediaFile, ImageError.None);
        }

        // ExternalId is the artist's MBID: MusicBrainz knows their Wikidata entity, which has the photo
        private async Task<ImageResult> AddArtistPhotoAsync(string artistMbid, CancellationToken ct)
        {
            var (getResult, entityId) = await _musicBrainzService.GetWikidataEntityIdAsync(artistMbid, null, ct).ConfigureAwait(false);
            if (getResult == HttpGetResult.RateLimitedOrServiceUnavailable)
                return new(null, ImageError.Unavailable);

            if (string.IsNullOrEmpty(entityId))
                return new(null, ImageError.NotFound);

            return await AddLookedUpAsync(
                await _wikimediaService.GetImageAsync(entityId, null, ct).ConfigureAwait(false),
                MediaSource.Wikimedia,
                ct).ConfigureAwait(false);
        }

        private async Task<ImageResult> AddLookedUpAsync(ArtworkResult lookup, MediaSource source, CancellationToken ct)
        {
            if (lookup.GetResult == HttpGetResult.RateLimitedOrServiceUnavailable)
                return new(null, ImageError.Unavailable);

            // Url is the ~500px version the user saw; the original may be many megabytes. The
            // services only give URLs on hosts they trust.
            var credit = lookup.ArtworkData;
            if (credit is null || string.IsNullOrEmpty(credit.Url))
                return new(null, ImageError.NotFound);

            var (bytes, error) = await DownloadAsync(credit.Url, ct).ConfigureAwait(false);
            if (bytes is null)
                return new(null, error);

            if (DetectImageType(bytes) is not ImageType type)
                return new(null, ImageError.Invalid);

            var mediaFile = await StoreAsync(bytes, type, ownerUserId: null, source, credit, ct).ConfigureAwait(false);
            return new(mediaFile, ImageError.None);
        }

        private async Task<(byte[]? Bytes, ImageError Error)> DownloadAsync(string url, CancellationToken ct)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

                if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
                    return (null, ImageError.Unavailable);

                if (!response.IsSuccessStatusCode)
                    return (null, ImageError.NotFound);

                if (response.Content.Headers.ContentLength > MaxImageSizeBytes)
                    return (null, ImageError.Invalid);

                // Read no more than we take, whatever the length header said
                await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var content = new MemoryStream();
                var chunk = new byte[81920];
                int read;
                while ((read = await stream.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
                {
                    if (content.Length + read > MaxImageSizeBytes)
                        return (null, ImageError.Invalid);

                    content.Write(chunk, 0, read);
                }

                return (content.ToArray(), ImageError.None);
            }
            catch (HttpRequestException)
            {
                return (null, ImageError.Unavailable);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timed out
                return (null, ImageError.Unavailable);
            }
        }

        private async Task<MediaFile> StoreAsync(byte[] bytes,
                                                 ImageType type,
                                                 long? ownerUserId,
                                                 MediaSource source,
                                                 Data.External.ArtworkData? credit,
                                                 CancellationToken ct)
        {
            var sha1 = Convert.ToHexStringLower(SHA1.HashData(bytes));

            // Also look among the files added in this request but not saved yet, or the same
            // image twice in one request would break the unique index
            var existing = _dbContext.MediaFiles.Local.FirstOrDefault(x => x.OwnerUserId == ownerUserId && x.Sha1 == sha1) ??
                await _dbContext.MediaFiles.FirstOrDefaultAsync(x => x.OwnerUserId == ownerUserId && x.Sha1 == sha1, ct).ConfigureAwait(false);

            if (existing is not null)
                return existing;

            var folder = ownerUserId is null ?
                _options.Shared :
                Path.Combine(_options.User, ownerUserId.Value.ToString(CultureInfo.InvariantCulture));

            _fileSystem.CreateDirectory(folder);

            var fileName = $"{Guid.NewGuid()}.{type.Extension}";
            var path = Path.Combine(folder, fileName);
            await _fileSystem.WriteAllBytesAsync(path, bytes, ct).ConfigureAwait(false);
            _writtenFiles.Add(path);

            var mediaFile = new MediaFile
            {
                OwnerUserId = ownerUserId,
                FileName = fileName,
                ContentType = type.ContentType,
                ByteCount = bytes.Length,
                Sha1 = sha1,
                Source = source,
                OriginalUrl = credit?.OriginalUrl ?? credit?.Url,
                SourcePageUrl = credit?.SourcePageUrl,
                Title = credit?.Title,
                Author = credit?.Author,
                License = credit?.License,
                LicenseUrl = credit?.LicenseUrl,
                AttributionRequired = credit?.AttributionRequired ?? false
            };

            _dbContext.MediaFiles.Add(mediaFile);
            return mediaFile;
        }

        private static ImageType? DetectImageType(ReadOnlySpan<byte> bytes)
        {
            if (bytes.StartsWith(JpegMagic))
                return new("image/jpeg", "jpg");

            if (bytes.StartsWith(PngMagic))
                return new("image/png", "png");

            // "RIFF", the size, then "WEBP"
            if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
                return new("image/webp", "webp");

            return null;
        }
    }
}
