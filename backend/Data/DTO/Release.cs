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
        string Name,
        CreditRole Role,
        string JoinPhrase,
        string? CharacterName);

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
        string? Notes,
        int? Score,
        DateOnly? AcquiredOn,
        DateTimeOffset CreatedAt,
        IReadOnlyList<ReleaseCredit> Credits,
        IReadOnlyList<ReleaseTrack> Tracks,
        IReadOnlyList<ImageInfo> Images,
        IReadOnlyDictionary<ExternalSource, string> ExternalIds);

    // --- Write side ---

    // The credits are in billing order
    public sealed record NewReleaseCredit(long CreatorId, CreditRole Role, string JoinPhrase, string? CharacterName);

    public sealed partial record NewReleaseRequest(
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
        IReadOnlyList<NewImage> Images)     // The first one is the cover, and the preferred image
    {
        public const int MaxImages = 20;

        // fileCount: the number of files sent along with the request
        public bool Validate(int fileCount)
        {
            // Missing lists in the JSON come in as null, and so may their items
            if (Credits is null || Tracks is null || Images is null)
                return false;

            if (Credits.Any(x => x is null) || Tracks.Any(x => x is null) || Images.Any(x => x is null))
                return false;

            if (string.IsNullOrWhiteSpace(Title) || Title.Length > 500)
                return false;

            if (ReleaseYear is < 1 or > 9999 || FirstReleaseYear is < 1 or > 9999)
                return false;

            // A later pressing can't come out before the original
            if (FirstReleaseYear > ReleaseYear)
                return false;

            if (DurationSeconds < 0)
                return false;

            if (Barcode?.Length > 32 || Notes?.Length > 4000)
                return false;

            if (Score is < Release.MinScore or > Release.MaxScore)
                return false;

            // GUIDs, which also keeps them safe to put in a URL
            if ((MusicBrainzReleaseId is not null && !Guid.TryParse(MusicBrainzReleaseId, out _)) ||
                (MusicBrainzReleaseGroupId is not null && !Guid.TryParse(MusicBrainzReleaseGroupId, out _)))
            {
                return false;
            }

            if (SpotifyAlbumId is not null && !SpotifyIdRegex().IsMatch(SpotifyAlbumId))
                return false;

            if (!Credits.Any(x => x.Role == CreditRole.Primary))
                return false;

            foreach (var credit in Credits)
            {
                if (credit.CreatorId <= 0)
                    return false;

                if (credit.JoinPhrase is null || credit.JoinPhrase.Length > 50 || credit.CharacterName?.Length > 200)
                    return false;
            }

            // One credit per creator and role
            if (Credits.DistinctBy(x => (x.CreatorId, x.Role)).Count() != Credits.Count)
                return false;

            foreach (var track in Tracks)
            {
                if (string.IsNullOrWhiteSpace(track.Title) || track.Title.Length > 500)
                    return false;

                if (track.DiscNumber < 1 || track.TrackNumber < 1 || track.DurationSeconds < 0)
                    return false;
            }

            // One track per position
            if (Tracks.DistinctBy(x => (x.DiscNumber, x.TrackNumber)).Count() != Tracks.Count)
                return false;

            if (Images.Count > MaxImages)
                return false;

            return Images.All(x => x.Validate(fileCount));
        }

        // Spotify's ids are 22 base-62 characters
        [GeneratedRegex("^[A-Za-z0-9]{22}$")]
        private static partial Regex SpotifyIdRegex();
    }
}
