using CdArchiveBackend.Data.Database;
using System;
using System.Text.RegularExpressions;

namespace CdArchiveBackend.Data.DTO
{
    // A creator in the user's collection, e.g. in search results. ImageUrl is their preferred image.
    public sealed record CreatorSummary(
        long Id,
        string Name,
        CreatorType Type,
        string? Country,
        string? ImageUrl,
        string? MusicBrainzId);

    // One name to search the user's creators for
    public sealed record CreatorQuery(string Name);

    // Sent as the "request" part of a multipart form, with an uploaded image (if any) as a file part
    public sealed partial record NewCreatorRequest(
        string Name,
        string? SortName,       // e.g. "Beatles, The". The name is used if there's none.
        CreatorType Type,
        string? Country,
        string? MusicBrainzId,
        string? SpotifyUrl,     // https://open.spotify.com/artist/<id>
        NewImage? Image)
    {
        // fileCount: the number of files sent along with the request
        public bool Validate(int fileCount)
        {
            if (string.IsNullOrWhiteSpace(Name) || Name.Length > 300)
                return false;

            if (SortName?.Length > 300)
                return false;

            if (Country is not null && Country.Length != 2)
                return false;

            // A GUID, which also keeps it safe to put in a URL
            if (MusicBrainzId is not null && !Guid.TryParse(MusicBrainzId, out _))
                return false;

            if (!string.IsNullOrWhiteSpace(SpotifyUrl) && GetSpotifyId() is null)
                return false;

            if (Image is not null && !Image.Validate(fileCount))
                return false;

            return true;
        }

        // The artist id in SpotifyUrl, or null if there's no (valid) URL
        public string? GetSpotifyId()
        {
            var match = SpotifyArtistUrlRegex().Match(SpotifyUrl?.Trim() ?? "");
            return match.Success ? match.Groups[1].Value : null;
        }

        // Also with a locale ("/intl-de/") and a query string ("?si=..."), as the share links have
        [GeneratedRegex(@"^https://open\.spotify\.com/(?:intl-[a-z]{2}(?:-[a-z]{2})?/)?artist/([A-Za-z0-9]{22})(?:[/?#].*)?$", RegexOptions.IgnoreCase)]
        private static partial Regex SpotifyArtistUrlRegex();
    }
}
