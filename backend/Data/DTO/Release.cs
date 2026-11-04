using CdArchiveBackend.Data.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CdArchiveBackend.Data.DTO
{
    // --- Read side ---

    // One release on the dashboard
    public sealed record ReleaseSummary(
        long Id,
        string Title,
        string ArtistName,      // The primary credits joined by their join phrases
        string? ImageUrl,       // The preferred image (the cover unless the user picked another)
        int? Year,              // The first release year, or this release's if that's unknown
        ReleaseType Type,
        ReleaseStatus Status);

    public sealed record ReleaseCredit(
        long CreatorId,
        string Name,                // The creator's own name
        CreditRole Role,
        string JoinPhrase,
        string? CharacterName,
        string? CreditedName,       // What this release calls them, or null when it's their own name
        string? Country,            // The creator's
        string? MusicBrainzId);     // The creator's MusicBrainz artist, if linked

    public sealed record ReleaseTrack(int DiscNumber, int TrackNumber, string Title, int? DurationSeconds);

    // Everything about one release. Contains its summary so the frontend can update the
    // dashboard from it without another request.
    public sealed record ReleaseDetails(
        ReleaseSummary Summary,
        ReleaseFormat Format,
        int? ReleaseYear,
        int? FirstReleaseYear,
        int? DurationSeconds,
        string? Barcode,
        string? Country,            // Where this release came out (see Release.Country)
        string? Notes,
        int? Score,
        DateOnly? AcquiredOn,
        bool Consumed,              // Listened to, read or watched
        DateTimeOffset CreatedAt,
        IReadOnlyList<ReleaseCredit> Credits,
        IReadOnlyList<ReleaseTrack> Tracks,
        IReadOnlyList<ImageInfo> Images,
        IReadOnlyDictionary<ExternalSource, string> ExternalIds);

    // --- The user's own data about a release ---

    // All of it at once: what isn't given is cleared. Score is Release.MinScore to MaxScore, or null
    // for not rated.
    public sealed record PersonalReleaseData(DateOnly? AcquiredOn, int? Score, bool Consumed, string? Notes)
    {
        public bool Validate()
            => Score is null or (>= Release.MinScore and <= Release.MaxScore) && !(Notes?.Length > 4000);
    }

    // --- Removing ---

    // What's left to decide after removing a release: the creators it credited who are now on none
    // of the user's releases, by sort name, with their images to show whom the user is asked about
    public sealed record RemovedRelease(IReadOnlyList<CreatorSummary> CreatorsWithoutReleases);

    // --- Write side ---

    // The credits are in billing order. CreditedName is the name the release credits the creator as,
    // if it isn't their own (e.g. "John Cougar" for John Mellencamp).
    public sealed record NewReleaseCredit(long CreatorId, CreditRole Role, string JoinPhrase, string? CharacterName, string? CreditedName = null);

    public sealed record NewReleaseRequest(
        string Title,
        ReleaseType Type,
        ReleaseStatus Status,
        ReleaseFormat Format,
        int? ReleaseYear,
        int? FirstReleaseYear,
        int? DurationSeconds,
        string? Barcode,
        string? MusicBrainzReleaseId,
        string? MusicBrainzReleaseGroupId,
        string? SpotifyAlbumId,
        string? Notes,
        int? Score,
        DateOnly? AcquiredOn,
        IReadOnlyList<NewReleaseCredit> Credits,
        IReadOnlyList<ReleaseTrack> Tracks,
        IReadOnlyList<NewImage> Images,     // The first one is the cover, and the preferred image
        string? Country = null)             // Where this release came out (see Release.Country)
    {
        public const int MaxImages = 20;

        // fileCount: the number of files sent along with the request
        public bool Validate(int fileCount)
        {
            // Missing lists in the JSON come in as null, and so may their items
            if (Images is null || Images.Any(x => x is null))
                return false;

            if (!ReleaseRules.AreValidDetails(Title, ReleaseYear, FirstReleaseYear, DurationSeconds, Barcode, Country,
                                              MusicBrainzReleaseId, MusicBrainzReleaseGroupId, SpotifyAlbumId))
            {
                return false;
            }

            if (Notes?.Length > 4000)
                return false;

            if (Score is < Release.MinScore or > Release.MaxScore)
                return false;

            if (!ReleaseRules.AreValidCredits(Credits) || !ReleaseRules.AreValidTracks(Tracks))
                return false;

            if (Images.Count > MaxImages)
                return false;

            return Images.All(x => x.Validate(fileCount));
        }
    }

    // Changes to one of the user's releases, from the "Edit record details" dialog. Sent as JSON,
    // as there's nothing to upload: a cover can only come from the Cover Art Archive here.
    //  - A release linked to MusicBrainz that stays linked to the same release only takes the
    //    Spotify album id: the rest is MusicBrainz's.
    //  - Tracks: null keeps the current ones. Given when filled in from MusicBrainz.
    //  - Cover: null keeps the current images. Otherwise the release's only image from now on.
    public sealed record UpdateReleaseRequest(
        string Title,
        ReleaseFormat Format,
        int? ReleaseYear,
        int? FirstReleaseYear,
        int? DurationSeconds,
        string? Barcode,
        string? Country,
        string? MusicBrainzReleaseId,
        string? MusicBrainzReleaseGroupId,
        string? SpotifyAlbumId,
        IReadOnlyList<NewReleaseCredit> Credits,
        IReadOnlyList<ReleaseTrack>? Tracks = null,
        NewImage? Cover = null)
    {
        public bool Validate()
        {
            if (!ReleaseRules.AreValidDetails(Title, ReleaseYear, FirstReleaseYear, DurationSeconds, Barcode, Country,
                                              MusicBrainzReleaseId, MusicBrainzReleaseGroupId, SpotifyAlbumId))
            {
                return false;
            }

            if (!ReleaseRules.AreValidCredits(Credits))
                return false;

            if (Tracks is not null && !ReleaseRules.AreValidTracks(Tracks))
                return false;

            // Nothing is uploaded with an update
            return Cover is null || (Cover.Source == MediaSource.CoverArtArchive && Cover.Validate(fileCount: 0));
        }
    }

    // The rules a release's details, credits and tracks must follow, whether it's being added or changed
    public static partial class ReleaseRules
    {
        public static bool AreValidDetails(string? title,
                                           int? releaseYear,
                                           int? firstReleaseYear,
                                           int? durationSeconds,
                                           string? barcode,
                                           string? country,
                                           string? musicBrainzReleaseId,
                                           string? musicBrainzReleaseGroupId,
                                           string? spotifyAlbumId)
        {
            if (string.IsNullOrWhiteSpace(title) || title.Length > 500)
                return false;

            if (releaseYear is < 1 or > 9999 || firstReleaseYear is < 1 or > 9999)
                return false;

            // A later pressing can't come out before the original
            if (firstReleaseYear > releaseYear)
                return false;

            if (durationSeconds < 0)
                return false;

            if (barcode?.Length > 32)
                return false;

            // Two letters, as MusicBrainz's country codes are
            if (!string.IsNullOrWhiteSpace(country) && !CountryRegex().IsMatch(country.Trim()))
                return false;

            // GUIDs, which also keeps them safe to put in a URL
            if ((musicBrainzReleaseId is not null && !Guid.TryParse(musicBrainzReleaseId, out _)) ||
                (musicBrainzReleaseGroupId is not null && !Guid.TryParse(musicBrainzReleaseGroupId, out _)))
            {
                return false;
            }

            return spotifyAlbumId is null || SpotifyIdRegex().IsMatch(spotifyAlbumId);
        }

        // At least one primary credit, and one credit per creator and role
        public static bool AreValidCredits(IReadOnlyList<NewReleaseCredit>? credits)
        {
            // Missing lists in the JSON come in as null, and so may their items
            if (credits is null || credits.Any(x => x is null))
                return false;

            if (!credits.Any(x => x.Role == CreditRole.Primary))
                return false;

            foreach (var credit in credits)
            {
                if (credit.CreatorId <= 0)
                    return false;

                if (credit.JoinPhrase is null || credit.JoinPhrase.Length > 50 || credit.CharacterName?.Length > 200)
                    return false;

                // As long as a creator's name may be
                if (credit.CreditedName?.Length > 300)
                    return false;
            }

            return credits.DistinctBy(x => (x.CreatorId, x.Role)).Count() == credits.Count;
        }

        // One track per position
        public static bool AreValidTracks(IReadOnlyList<ReleaseTrack>? tracks)
        {
            if (tracks is null || tracks.Any(x => x is null))
                return false;

            foreach (var track in tracks)
            {
                if (string.IsNullOrWhiteSpace(track.Title) || track.Title.Length > 500)
                    return false;

                if (track.DiscNumber < 1 || track.TrackNumber < 1 || track.DurationSeconds < 0)
                    return false;
            }

            return tracks.DistinctBy(x => (x.DiscNumber, x.TrackNumber)).Count() == tracks.Count;
        }

        // Spotify's ids are 22 base-62 characters
        [GeneratedRegex("^[A-Za-z0-9]{22}$")]
        private static partial Regex SpotifyIdRegex();

        [GeneratedRegex("^[A-Za-z]{2}$")]
        private static partial Regex CountryRegex();
    }
}
