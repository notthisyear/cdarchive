using CdArchiveBackend.Data.Database;
using CdArchiveBackend.Data.DTO;
using CdArchiveBackend.Services.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CdArchiveBackend.Services
{
    using ImageError = MediaFileService.ImageError;

    internal sealed class ReleaseService(DatabaseContext dbContext, MediaFileService mediaFileService, MediaUrlService mediaUrlService)
    {
        public const int MaxPageSize = 100;

        public enum AddReleaseError
        {
            None,
            AlreadyInCollection,    // The user already has this release
            UnknownCreator,         // A credit names a creator that isn't in the user's collection
            InvalidImage,
            ImageNotFound,
            ImageServiceUnavailable
        }

        // ReleaseId is set for None and AlreadyInCollection
        public readonly record struct AddReleaseResult(long ReleaseId, AddReleaseError Error);

        private readonly DatabaseContext _dbContext = dbContext;
        private readonly MediaFileService _mediaFileService = mediaFileService;
        private readonly MediaUrlService _mediaUrlService = mediaUrlService;

        // What a summary is built from, read in one query
        private sealed record SummaryRow(long Id,
                                         string Title,
                                         ReleaseType Type,
                                         ReleaseStatus Status,
                                         int? Year,
                                         MediaFile? PreferredImage,
                                         MediaFile? FirstImage,
                                         List<ArtistRow> Artists);

        private sealed record ArtistRow(string Name, string JoinPhrase);

        public async Task<int> GetTotalNumberOfReleasesForUser(int userId)
            => await _dbContext.Releases.CountAsync(x => x.UserId == userId && x.Status == ReleaseStatus.Owned).ConfigureAwait(false);

        // One page of the releases the user owns, sorted as on a shelf: by artist, then by when
        // the album first came out, then by title. limit must be 1 to MaxPageSize.
        public async Task<List<ReleaseSummary>> GetReleasesForUserAsync(long userId, int offset, int limit, CancellationToken ct)
        {
            var rows = await ToSummaryRows(_dbContext.Releases
                    .Where(x => x.UserId == userId && x.Status == ReleaseStatus.Owned)
                    .OrderBy(x => x.Credits
                        .Where(c => c.Role == CreditRole.Primary)
                        .OrderBy(c => c.SortOrder)
                        .Select(c => c.Creator.SortName)
                        .FirstOrDefault())
                    .ThenBy(x => x.FirstReleaseYear ?? x.ReleaseYear)
                    .ThenBy(x => x.Title)
                    .ThenBy(x => x.Id)
                    .Skip(offset)
                    .Take(limit))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return [.. rows.Select(ToSummary)];
        }

        // Everything about one of the user's releases. Null if it isn't theirs (or doesn't exist).
        public async Task<ReleaseDetails?> GetReleaseAsync(long userId, long releaseId, CancellationToken ct)
        {
            var mine = _dbContext.Releases.Where(x => x.UserId == userId && x.Id == releaseId);

            var summaryRow = await ToSummaryRows(mine).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (summaryRow is null)
                return null;

            var release = await mine
                .Include(x => x.Credits).ThenInclude(x => x.Creator)
                .Include(x => x.Tracks)
                .Include(x => x.Images).ThenInclude(x => x.MediaFile)
                .Include(x => x.ExternalIds)
                .AsSplitQuery()
                .AsNoTracking()
                .SingleAsync(ct)
                .ConfigureAwait(false);

            return new ReleaseDetails(
                ToSummary(summaryRow),
                release.Format,
                release.ReleaseYear,
                release.FirstReleaseYear,
                release.DurationSeconds,
                release.Barcode,
                release.Notes,
                release.Score,
                release.AcquiredOn,
                release.CreatedAt,
                [.. release.Credits
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new ReleaseCredit(x.CreatorId, x.Creator.Name, x.Role, x.JoinPhrase, x.CharacterName))],
                [.. release.Tracks
                    .OrderBy(x => x.DiscNumber)
                    .ThenBy(x => x.TrackNumber)
                    .Select(x => new ReleaseTrack(x.DiscNumber, x.TrackNumber, x.Title, x.DurationSeconds))],
                [.. release.Images
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new ImageInfo(
                        _mediaUrlService.GetUrl(x.MediaFile),
                        x.MediaFile.Author,
                        x.MediaFile.License,
                        x.MediaFile.LicenseUrl,
                        x.MediaFile.SourcePageUrl,
                        x.MediaFile.AttributionRequired))],
                release.ExternalIds.ToDictionary(x => x.Source, x => x.ExternalId));
        }

        private static IQueryable<SummaryRow> ToSummaryRows(IQueryable<Release> releases)
            => releases
                .Select(x => new SummaryRow(
                    x.Id,
                    x.Title,
                    x.Type,
                    x.Status,
                    x.FirstReleaseYear ?? x.ReleaseYear,
                    x.PreferredImage,
                    x.Images.OrderBy(i => i.SortOrder).Select(i => i.MediaFile).FirstOrDefault(),
                    x.Credits
                        .Where(c => c.Role == CreditRole.Primary)
                        .OrderBy(c => c.SortOrder)
                        .Select(c => new ArtistRow(c.Creator.Name, c.JoinPhrase))
                        .ToList()))
                .AsNoTracking();

        private ReleaseSummary ToSummary(SummaryRow row)
        {
            var image = row.PreferredImage ?? row.FirstImage;
            return new ReleaseSummary(
                row.Id,
                row.Title,
                // Each name followed by its join phrase: "Simon" + " & " + "Garfunkel" + ""
                string.Concat(row.Artists.Select(x => x.Name + x.JoinPhrase)).Trim(),
                image is null ? null : _mediaUrlService.GetUrl(image),
                row.Year,
                row.Type,
                row.Status);
        }

        // Adds a release to the user's collection, with its credits, tracks, images and external
        // ids. The first image becomes the preferred one.
        // request must have been validated (NewReleaseRequest.Validate) against files.
        public async Task<AddReleaseResult> AddReleaseAsync(long userId, NewReleaseRequest request, IFormFileCollection files, CancellationToken ct)
        {
            // Credits may only name the user's own creators
            var creatorIds = request.Credits.Select(x => x.CreatorId).Distinct().ToList();
            var ownCreatorCount = await _dbContext.Creators
                .CountAsync(x => x.UserId == userId && creatorIds.Contains(x.Id), ct)
                .ConfigureAwait(false);

            if (ownCreatorCount != creatorIds.Count)
                return new(0, AddReleaseError.UnknownCreator);

            // Checked before any image is downloaded
            var fingerprint = ReleaseFingerprint.Compute(request);
            var musicBrainzReleaseId = request.MusicBrainzReleaseId?.ToLowerInvariant();
            if (await FindInCollectionAsync(userId, musicBrainzReleaseId, fingerprint, ct).ConfigureAwait(false) is long existingId)
                return new(existingId, AddReleaseError.AlreadyInCollection);

            // In the order given. The same file twice (e.g. one upload sent twice) is only used once.
            List<MediaFile> images = [];
            foreach (var newImage in request.Images)
            {
                var result = await _mediaFileService.AddAsync(userId, newImage, files, ct).ConfigureAwait(false);
                if (result.Error != ImageError.None)
                {
                    // Images stored before this one won't be used now
                    _mediaFileService.DeleteWrittenFiles();
                    return new(0, result.Error switch
                    {
                        ImageError.NotFound => AddReleaseError.ImageNotFound,
                        ImageError.Unavailable => AddReleaseError.ImageServiceUnavailable,
                        _ => AddReleaseError.InvalidImage
                    });
                }

                if (!images.Contains(result.File!))
                    images.Add(result.File!);
            }

            var release = new Release
            {
                UserId = userId,
                Status = request.Status,
                Title = request.Title.Trim(),
                Type = request.Type,
                Format = request.Format,
                ReleaseYear = request.ReleaseYear,
                FirstReleaseYear = request.FirstReleaseYear,
                DurationSeconds = request.DurationSeconds,
                Barcode = TrimToNull(request.Barcode),
                Fingerprint = fingerprint,
                Notes = TrimToNull(request.Notes),
                Score = request.Score,
                AcquiredOn = request.AcquiredOn,
                PreferredImage = images.FirstOrDefault()
            };

            if (musicBrainzReleaseId is not null)
                release.ExternalIds.Add(new() { Source = ExternalSource.MusicBrainz, ExternalId = musicBrainzReleaseId });
            if (request.MusicBrainzReleaseGroupId is not null)
                release.ExternalIds.Add(new() { Source = ExternalSource.MusicBrainzReleaseGroup, ExternalId = request.MusicBrainzReleaseGroupId.ToLowerInvariant() });

            // Spotify's ids are case-sensitive, so stored as given
            if (request.SpotifyAlbumId is not null)
                release.ExternalIds.Add(new() { Source = ExternalSource.Spotify, ExternalId = request.SpotifyAlbumId });

            // In billing order
            for (var i = 0; i < request.Credits.Count; i++)
            {
                var credit = request.Credits[i];
                release.Credits.Add(new()
                {
                    CreatorId = credit.CreatorId,
                    Role = credit.Role,
                    JoinPhrase = credit.JoinPhrase,
                    CharacterName = TrimToNull(credit.CharacterName),
                    SortOrder = i
                });
            }

            foreach (var track in request.Tracks)
            {
                release.Tracks.Add(new()
                {
                    DiscNumber = track.DiscNumber,
                    TrackNumber = track.TrackNumber,
                    Title = track.Title.Trim(),
                    DurationSeconds = track.DurationSeconds
                });
            }

            for (var i = 0; i < images.Count; i++)
                release.Images.Add(new() { MediaFile = images[i], SortOrder = i });

            _dbContext.Releases.Add(release);

            try
            {
                await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                _mediaFileService.DeleteWrittenFiles();
                throw;
            }

            return new(release.Id, AddReleaseError.None);
        }

        // The id of the user's release that's the same as the new one, if they have one. With a
        // MusicBrainz id, that's the release with the same id: two MusicBrainz releases may look
        // the same here (e.g. two pressings with no barcode) and still be different. Without
        // one, it's any release with the same fingerprint.
        private async Task<long?> FindInCollectionAsync(long userId, string? musicBrainzReleaseId, string fingerprint, CancellationToken ct)
        {
            var releases = _dbContext.Releases.Where(x => x.UserId == userId);
            releases = musicBrainzReleaseId is not null ?
                releases.Where(x => x.ExternalIds.Any(e => e.Source == ExternalSource.MusicBrainz && e.ExternalId == musicBrainzReleaseId)) :
                releases.Where(x => x.Fingerprint == fingerprint);

            return await releases
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }

        private static string? TrimToNull(string? s)
            => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
