using CdArchiveBackend.Data.Database;
using CdArchiveBackend.Data.DTO;
using CdArchiveBackend.Services.External;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CdArchiveBackend.Services
{
    using ImageError = MediaFileService.ImageError;

    // Each user has their own creators, even for the same artist, so they can name and picture
    // them as they like. Only the image files themselves are shared (see MediaFileService).
    internal sealed class CreatorService(DatabaseContext dbContext,
                                         MediaFileService mediaFileService,
                                         MediaUrlService mediaUrlService,
                                         MusicBrainzService musicBrainzService)
    {
        private const int MaxSearchResults = 20;

        public enum AddCreatorError
        {
            None,
            AlreadyInCollection,
            InvalidImage,
            ImageNotFound,
            ImageServiceUnavailable
        }

        // CreatorId is set for None and AlreadyInCollection
        public readonly record struct AddCreatorResult(long CreatorId, AddCreatorError Error);

        private readonly DatabaseContext _dbContext = dbContext;
        private readonly MediaFileService _mediaFileService = mediaFileService;
        private readonly MediaUrlService _mediaUrlService = mediaUrlService;
        private readonly MusicBrainzService _musicBrainzService = musicBrainzService;

        // The user's creators whose name or sort name contains name
        public async Task<List<CreatorSummary>> SearchCreatorsAsync(long userId, string name, CancellationToken ct)
        {
            var pattern = "%" + EscapeLikePattern(name.Trim()) + "%";
            return await ToSummariesAsync(
                _dbContext.Creators
                    .Where(x => x.UserId == userId &&
                                (EF.Functions.ILike(x.Name, pattern, @"\") ||
                                 EF.Functions.ILike(x.SortName, pattern, @"\")))
                    .OrderBy(x => x.SortName)
                    .Take(MaxSearchResults),
                ct).ConfigureAwait(false);
        }

        // Null if the creator isn't in the user's collection
        public async Task<CreatorSummary?> GetCreatorAsync(long userId, long creatorId, CancellationToken ct)
            => (await ToSummariesAsync(
                _dbContext.Creators.Where(x => x.UserId == userId && x.Id == creatorId),
                ct).ConfigureAwait(false)).FirstOrDefault();

        // Adds the creator to the user's collection, unless they already have them (known by the
        // same MusicBrainz or Spotify id).
        // request must have been validated (NewCreatorRequest.Validate) against files.
        public async Task<AddCreatorResult> AddCreatorAsync(long userId, NewCreatorRequest request, IFormFileCollection files, CancellationToken ct)
        {
            var name = request.Name.Trim();
            var musicBrainzId = request.MusicBrainzId?.ToLowerInvariant();
            var spotifyId = request.GetSpotifyId();

            var existingId = await FindInCollectionAsync(userId, ExternalSource.MusicBrainz, musicBrainzId, ct).ConfigureAwait(false) ??
                             await FindInCollectionAsync(userId, ExternalSource.Spotify, spotifyId, ct).ConfigureAwait(false);

            if (existingId is long id)
                return new(id, AddCreatorError.AlreadyInCollection);

            MediaFile? image = null;
            if (request.Image is not null)
            {
                var result = await _mediaFileService.AddAsync(userId, request.Image, files, ct).ConfigureAwait(false);
                if (result.Error != ImageError.None)
                {
                    return new(0, result.Error switch
                    {
                        ImageError.NotFound => AddCreatorError.ImageNotFound,
                        ImageError.Unavailable => AddCreatorError.ImageServiceUnavailable,
                        _ => AddCreatorError.InvalidImage
                    });
                }

                image = result.File;
            }

            var creator = new Creator
            {
                UserId = userId,
                Name = name,
                SortName = request.SortName?.Trim() is { Length: > 0 } sortName ?
                    sortName :
                    await GetMusicBrainzSortNameAsync(musicBrainzId, ct).ConfigureAwait(false) ?? name,
                Type = request.Type,
                Country = request.Country?.ToUpperInvariant()
            };

            if (musicBrainzId is not null)
                creator.ExternalIds.Add(new() { Source = ExternalSource.MusicBrainz, ExternalId = musicBrainzId });
            if (spotifyId is not null)
                creator.ExternalIds.Add(new() { Source = ExternalSource.Spotify, ExternalId = spotifyId });

            if (image is not null)
            {
                creator.Images.Add(new() { MediaFile = image, SortOrder = 0 });
                creator.PreferredImage = image;
            }

            _dbContext.Creators.Add(creator);

            try
            {
                await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                _mediaFileService.DeleteWrittenFiles();
                throw;
            }

            return new(creator.Id, AddCreatorError.None);
        }

        // The id of the user's creator with this external id, if they have one
        private async Task<long?> FindInCollectionAsync(long userId, ExternalSource source, string? externalId, CancellationToken ct)
        {
            if (externalId is null)
                return null;

            return await _dbContext.Creators
                .Where(x => x.UserId == userId && x.ExternalIds.Any(e => e.Source == source && e.ExternalId == externalId))
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }

        // Usually cached from when the form looked the artist up. Null if MusicBrainz doesn't know.
        private async Task<string?> GetMusicBrainzSortNameAsync(string? musicBrainzId, CancellationToken ct)
        {
            if (musicBrainzId is null)
                return null;

            var result = await _musicBrainzService.GetArtistAsync(musicBrainzId, null, ct).ConfigureAwait(false);
            return result.Artists.FirstOrDefault()?.SortName is { Length: > 0 } sortName ? sortName : null;
        }

        private async Task<List<CreatorSummary>> ToSummariesAsync(IQueryable<Creator> creators, CancellationToken ct)
        {
            var rows = await creators
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Type,
                    x.Country,
                    x.PreferredImage,
                    FirstImage = x.Images
                        .OrderBy(i => i.SortOrder)
                        .Select(i => i.MediaFile)
                        .FirstOrDefault(),
                    MusicBrainzId = x.ExternalIds
                        .Where(e => e.Source == ExternalSource.MusicBrainz)
                        .Select(e => e.ExternalId)
                        .FirstOrDefault()
                })
                .AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return [.. rows.Select(x =>
            {
                var image = x.PreferredImage ?? x.FirstImage;
                return new CreatorSummary(
                    x.Id,
                    x.Name,
                    x.Type,
                    x.Country,
                    image is null ? null : _mediaUrlService.GetUrl(image),
                    x.MusicBrainzId);
            })];
        }

        private static string EscapeLikePattern(string s)
            => s.Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_");
    }
}
