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

    internal sealed class ReleaseService(DatabaseContext dbContext,
                                         MediaFileService mediaFileService,
                                         MediaUrlService mediaUrlService,
                                         CreatorService creatorService)
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

        public enum UpdateReleaseError
        {
            None,
            NotFound,               // Not the user's release (or doesn't exist)
            AlreadyInCollection,    // The change would make it the same as another of the user's releases
            UnknownCreator,         // A credit names a creator that isn't in the user's collection
            InvalidImage,
            ImageNotFound,
            ImageServiceUnavailable
        }

        // Release is the release as it is now, for None. ExistingReleaseId is the other release, for
        // AlreadyInCollection.
        public readonly record struct UpdateReleaseResult(UpdateReleaseError Error, ReleaseDetails? Release = null, long ExistingReleaseId = 0);

        private readonly DatabaseContext _dbContext = dbContext;
        private readonly MediaFileService _mediaFileService = mediaFileService;
        private readonly MediaUrlService _mediaUrlService = mediaUrlService;
        private readonly CreatorService _creatorService = creatorService;

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
                .Include(x => x.Credits).ThenInclude(x => x.Creator).ThenInclude(x => x.ExternalIds)
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
                release.Country,
                release.Notes,
                release.Score,
                release.AcquiredOn,
                release.Consumed,
                release.CreatedAt,
                [.. release.Credits
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new ReleaseCredit(x.CreatorId, x.Creator.Name, x.Role, x.JoinPhrase, x.CharacterName, x.CreditedName, x.Creator.Country,
                        x.Creator.ExternalIds.FirstOrDefault(e => e.Source == ExternalSource.MusicBrainz)?.ExternalId))],
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
                        // As the release credits them ("John Cougar"), though the shelf sorts by who they are
                        .Select(c => new ArtistRow(c.CreditedName ?? c.Creator.Name, c.JoinPhrase))
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
            if (await GetCreatorNamesAsync(userId, request.Credits, ct).ConfigureAwait(false) is not { } creatorNames)
                return new(0, AddReleaseError.UnknownCreator);

            // Checked before any image is downloaded
            var fingerprint = ReleaseFingerprint.Compute(request);
            var musicBrainzReleaseId = request.MusicBrainzReleaseId?.ToLowerInvariant();
            if (await FindInCollectionAsync(userId, musicBrainzReleaseId, fingerprint, exceptReleaseId: null, ct).ConfigureAwait(false) is long existingId)
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
                Country = TrimToNull(request.Country)?.ToUpperInvariant(),
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

            SetCredits(release, request.Credits, creatorNames);
            SetTracks(release, request.Tracks);

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

        // Removes one of the user's releases, with its credits, tracks, images and external ids.
        // Null if it isn't theirs (or doesn't exist). Otherwise the creators it credited who have
        // no releases left, for the user to decide whether to remove them too. Their image files
        // stay: shared ones may be used by others, and the creator's own images aren't touched.
        public async Task<RemovedRelease?> RemoveReleaseAsync(long userId, long releaseId, CancellationToken ct)
        {
            // Loaded with everything that goes with it, so the change tracker removes those too
            // (the database would cascade anyway, but the in-memory one used by the tests doesn't)
            var release = await _dbContext.Releases
                .Include(x => x.Credits)
                .Include(x => x.Tracks)
                .Include(x => x.Images)
                .Include(x => x.ExternalIds)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == releaseId, ct)
                .ConfigureAwait(false);

            if (release is null)
                return null;

            var creatorIds = release.Credits.Select(x => x.CreatorId).Distinct().ToList();

            _dbContext.Releases.Remove(release);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            // With their images, to show whom the user is asked about
            return new RemovedRelease(await _creatorService.GetCreatorsWithoutReleasesAsync(userId, creatorIds, ct).ConfigureAwait(false));
        }

        // Changes one of the user's releases (see UpdateReleaseRequest). One linked to MusicBrainz that
        // stays linked to the same release only takes the Spotify album id: the rest is MusicBrainz's,
        // whatever the request says. Otherwise everything is taken, as when adding a release.
        // request must have been validated (UpdateReleaseRequest.Validate).
        public async Task<UpdateReleaseResult> UpdateReleaseAsync(long userId, long releaseId, UpdateReleaseRequest request, CancellationToken ct)
        {
            if (await LoadForChangeAsync(userId, releaseId, ct).ConfigureAwait(false) is not { } release)
                return new(UpdateReleaseError.NotFound);

            var musicBrainzReleaseId = request.MusicBrainzReleaseId?.ToLowerInvariant();
            var linkedTo = GetExternalId(release, ExternalSource.MusicBrainz);
            if (linkedTo is not null && linkedTo == musicBrainzReleaseId)
            {
                SetExternalId(release, ExternalSource.Spotify, request.SpotifyAlbumId);
                await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                return new(UpdateReleaseError.None, await GetReleaseAsync(userId, releaseId, ct).ConfigureAwait(false));
            }

            if (await GetCreatorNamesAsync(userId, request.Credits, ct).ConfigureAwait(false) is not { } creatorNames)
                return new(UpdateReleaseError.UnknownCreator);

            // What it will be, for telling whether it's now the same as another of the user's releases.
            // Checked before any cover is downloaded.
            var tracks = request.Tracks ?? [.. release.Tracks.Select(x => new ReleaseTrack(x.DiscNumber, x.TrackNumber, x.Title, x.DurationSeconds))];
            var fingerprint = ReleaseFingerprint.Compute(new NewReleaseRequest(
                request.Title, release.Type, release.Status, request.Format, request.ReleaseYear, request.FirstReleaseYear,
                request.DurationSeconds, request.Barcode, musicBrainzReleaseId, request.MusicBrainzReleaseGroupId,
                request.SpotifyAlbumId, release.Notes, release.Score, release.AcquiredOn, request.Credits, tracks, [], request.Country));

            if (await FindInCollectionAsync(userId, musicBrainzReleaseId, fingerprint, exceptReleaseId: release.Id, ct).ConfigureAwait(false) is long existingId)
                return new(UpdateReleaseError.AlreadyInCollection, ExistingReleaseId: existingId);

            MediaFile? cover = null;
            if (request.Cover is not null)
            {
                var result = await _mediaFileService.AddAsync(userId, request.Cover, new FormFileCollection(), ct).ConfigureAwait(false);
                if (result.Error != ImageError.None)
                {
                    return new(result.Error switch
                    {
                        ImageError.NotFound => UpdateReleaseError.ImageNotFound,
                        ImageError.Unavailable => UpdateReleaseError.ImageServiceUnavailable,
                        _ => UpdateReleaseError.InvalidImage
                    });
                }

                cover = result.File;
            }

            release.Title = request.Title.Trim();
            release.Format = request.Format;
            release.ReleaseYear = request.ReleaseYear;
            release.FirstReleaseYear = request.FirstReleaseYear;
            release.DurationSeconds = request.DurationSeconds;
            release.Barcode = TrimToNull(request.Barcode);
            release.Country = TrimToNull(request.Country)?.ToUpperInvariant();
            release.Fingerprint = fingerprint;

            SetExternalId(release, ExternalSource.MusicBrainz, musicBrainzReleaseId);
            SetExternalId(release, ExternalSource.MusicBrainzReleaseGroup, request.MusicBrainzReleaseGroupId?.ToLowerInvariant());
            SetExternalId(release, ExternalSource.Spotify, request.SpotifyAlbumId);

            SetCredits(release, request.Credits, creatorNames);
            if (request.Tracks is not null)
                SetTracks(release, request.Tracks);

            // The new cover is the only image from now on. The old files stay, as when removing a release.
            // A cover the release already has (the same file, reused by its SHA-1) is kept as it is.
            if (cover is not null)
            {
                var kept = cover.Id != 0 ? release.Images.FirstOrDefault(x => x.MediaFileId == cover.Id) : null;
                release.Images.RemoveAll(x => x != kept);
                if (kept is null)
                    release.Images.Add(new() { MediaFile = cover, SortOrder = 0 });
                else
                    kept.SortOrder = 0;
                release.PreferredImage = cover;
            }

            try
            {
                await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                _mediaFileService.DeleteWrittenFiles();
                throw;
            }

            return new(UpdateReleaseError.None, await GetReleaseAsync(userId, releaseId, ct).ConfigureAwait(false));
        }

        // Replaces the track list of one of the user's releases, as edited on its page. Its length
        // becomes the sum of the tracks'. Another track list is another release than the one on
        // MusicBrainz, so a linked release loses its link, though not its release group: it's still
        // the same album. tracks must be valid (ReleaseRules.AreValidTracks).
        public async Task<UpdateReleaseResult> ReplaceTracksAsync(long userId, long releaseId, IReadOnlyList<ReleaseTrack> tracks, CancellationToken ct)
        {
            if (await LoadForChangeAsync(userId, releaseId, ct).ConfigureAwait(false) is not { } release)
                return new(UpdateReleaseError.NotFound);

            int? durationSeconds = tracks.Any(x => x.DurationSeconds is not null) ? tracks.Sum(x => x.DurationSeconds ?? 0) : null;

            // As it is otherwise, for telling whether it's now the same as another of the user's releases
            var credits = release.Credits
                .OrderBy(x => x.SortOrder)
                .Select(x => new NewReleaseCredit(x.CreatorId, x.Role, x.JoinPhrase, x.CharacterName, x.CreditedName))
                .ToList();
            var fingerprint = ReleaseFingerprint.Compute(new NewReleaseRequest(
                release.Title, release.Type, release.Status, release.Format, release.ReleaseYear, release.FirstReleaseYear,
                durationSeconds, release.Barcode, null, GetExternalId(release, ExternalSource.MusicBrainzReleaseGroup),
                GetExternalId(release, ExternalSource.Spotify), release.Notes, release.Score, release.AcquiredOn, credits, tracks, [], release.Country));

            if (await FindInCollectionAsync(userId, musicBrainzReleaseId: null, fingerprint, exceptReleaseId: release.Id, ct).ConfigureAwait(false) is long existingId)
                return new(UpdateReleaseError.AlreadyInCollection, ExistingReleaseId: existingId);

            SetTracks(release, tracks);
            release.DurationSeconds = durationSeconds;
            release.Fingerprint = fingerprint;
            SetExternalId(release, ExternalSource.MusicBrainz, null);

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            return new(UpdateReleaseError.None, await GetReleaseAsync(userId, releaseId, ct).ConfigureAwait(false));
        }

        // Sets the user's own data about one of their releases: when they got it, their rating, whether
        // they've listened to it and their notes. None of it is part of what the release is, so its
        // MusicBrainz link and fingerprint stay. data must be valid (PersonalReleaseData.Validate).
        public async Task<UpdateReleaseResult> SetPersonalDataAsync(long userId, long releaseId, PersonalReleaseData data, CancellationToken ct)
        {
            var release = await _dbContext.Releases
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == releaseId, ct)
                .ConfigureAwait(false);

            if (release is null)
                return new(UpdateReleaseError.NotFound);

            release.AcquiredOn = data.AcquiredOn;
            release.Score = data.Score;
            release.Consumed = data.Consumed;
            release.Notes = TrimToNull(data.Notes);

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            return new(UpdateReleaseError.None, await GetReleaseAsync(userId, releaseId, ct).ConfigureAwait(false));
        }

        // Adds an image the user uploaded (files[0]) to one of their releases, as the one to show. It
        // goes in the user's own folder; the release keeps its other images, which other users' copies
        // of the release may share. The same file uploaded again is stored once.
        public async Task<UpdateReleaseResult> AddUploadedImageAsync(long userId, long releaseId, IFormFileCollection files, CancellationToken ct)
        {
            if (await LoadForChangeAsync(userId, releaseId, ct).ConfigureAwait(false) is not { } release)
                return new(UpdateReleaseError.NotFound);

            var result = await _mediaFileService.AddAsync(userId, new NewImage(MediaSource.Upload, null, 0), files, ct).ConfigureAwait(false);
            if (result.Error != ImageError.None)
                return new(UpdateReleaseError.InvalidImage);

            var image = result.File!;
            if (image.Id == 0 || !release.Images.Any(x => x.MediaFileId == image.Id))
            {
                var sortOrder = release.Images.Count == 0 ? 0 : release.Images.Max(x => x.SortOrder) + 1;
                release.Images.Add(new() { MediaFile = image, SortOrder = sortOrder });
            }
            release.PreferredImage = image;

            try
            {
                await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                _mediaFileService.DeleteWrittenFiles();
                throw;
            }

            return new(UpdateReleaseError.None, await GetReleaseAsync(userId, releaseId, ct).ConfigureAwait(false));
        }

        // One of the user's releases with everything that may change, tracked. Null if it isn't theirs.
        private Task<Release?> LoadForChangeAsync(long userId, long releaseId, CancellationToken ct)
            => _dbContext.Releases
                .Include(x => x.Credits)
                .Include(x => x.Tracks)
                .Include(x => x.Images)
                .Include(x => x.ExternalIds)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == releaseId, ct);

        // The names of the creators the credits name, by id. Null if any isn't the user's: credits may
        // only name the user's own creators. The names tell a credited name that's only the creator's
        // own name from one that isn't.
        private async Task<Dictionary<long, string>?> GetCreatorNamesAsync(long userId, IReadOnlyList<NewReleaseCredit> credits, CancellationToken ct)
        {
            var creatorIds = credits.Select(x => x.CreatorId).Distinct().ToList();
            var creatorNames = await _dbContext.Creators
                .Where(x => x.UserId == userId && creatorIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
                .ConfigureAwait(false);

            return creatorNames.Count == creatorIds.Count ? creatorNames : null;
        }

        // Makes the release's credits these, in billing order. Credits already there (same creator and
        // role) are changed in place, as their key can't be stored twice.
        private static void SetCredits(Release release, IReadOnlyList<NewReleaseCredit> credits, Dictionary<long, string> creatorNames)
        {
            var existing = release.Credits.ToDictionary(x => (x.CreatorId, x.Role));
            release.Credits.RemoveAll(x => !credits.Any(c => c.CreatorId == x.CreatorId && c.Role == x.Role));

            for (var i = 0; i < credits.Count; i++)
            {
                var credit = credits[i];
                if (!existing.TryGetValue((credit.CreatorId, credit.Role), out var stored))
                {
                    stored = new() { CreatorId = credit.CreatorId, Role = credit.Role };
                    release.Credits.Add(stored);
                }

                stored.JoinPhrase = credit.JoinPhrase;
                stored.CharacterName = TrimToNull(credit.CharacterName);
                // Null when it's just their own name, so that's never stored two ways
                stored.CreditedName = TrimToNull(credit.CreditedName) is string creditedName && creditedName != creatorNames[credit.CreatorId] ?
                    creditedName :
                    null;
                stored.SortOrder = i;
            }
        }

        // Makes the release's tracks these. A track already at a position is changed in place, as two
        // tracks can't share a position even for a moment.
        private static void SetTracks(Release release, IReadOnlyList<ReleaseTrack> tracks)
        {
            var existing = release.Tracks.ToDictionary(x => (x.DiscNumber, x.TrackNumber));
            release.Tracks.RemoveAll(x => !tracks.Any(t => t.DiscNumber == x.DiscNumber && t.TrackNumber == x.TrackNumber));

            foreach (var track in tracks)
            {
                if (existing.TryGetValue((track.DiscNumber, track.TrackNumber), out var stored))
                {
                    stored.Title = track.Title.Trim();
                    stored.DurationSeconds = track.DurationSeconds;
                }
                else
                {
                    release.Tracks.Add(new()
                    {
                        DiscNumber = track.DiscNumber,
                        TrackNumber = track.TrackNumber,
                        Title = track.Title.Trim(),
                        DurationSeconds = track.DurationSeconds
                    });
                }
            }
        }

        private static string? GetExternalId(Release release, ExternalSource source)
            => release.ExternalIds.FirstOrDefault(x => x.Source == source)?.ExternalId;

        // At most one id per source: null removes it
        private static void SetExternalId(Release release, ExternalSource source, string? externalId)
        {
            var existing = release.ExternalIds.FirstOrDefault(x => x.Source == source);
            if (externalId is null)
            {
                if (existing is not null)
                    release.ExternalIds.Remove(existing);
            }
            else if (existing is not null)
            {
                existing.ExternalId = externalId;
            }
            else
            {
                release.ExternalIds.Add(new() { Source = source, ExternalId = externalId });
            }
        }

        // The id of the user's release that's the same as the new one, if they have one. With a
        // MusicBrainz id, that's the release with the same id: two MusicBrainz releases may look
        // the same here (e.g. two pressings with no barcode) and still be different. Without
        // one, it's any release with the same fingerprint. exceptReleaseId is a release being
        // changed, which is never the same as itself.
        private async Task<long?> FindInCollectionAsync(long userId, string? musicBrainzReleaseId, string fingerprint, long? exceptReleaseId, CancellationToken ct)
        {
            var releases = _dbContext.Releases.Where(x => x.UserId == userId && x.Id != exceptReleaseId);
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
