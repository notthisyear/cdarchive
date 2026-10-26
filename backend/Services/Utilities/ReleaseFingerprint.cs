using CdArchiveBackend.Data.DTO;
using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CdArchiveBackend.Services.Utilities
{
    // A hash of what makes a release the release it is, for telling whether the user already has
    // it when there's no MusicBrainz id to go by. Leaves out what's the user's own (status, notes,
    // score, when they got it) and the images (the same album with another photo of its cover is
    // still the same album). Text counts the same whatever its case and surrounding whitespace.
    internal static partial class ReleaseFingerprint
    {
        // Part of what's hashed, so changing what goes in can't make old fingerprints match new ones
        private const int Version = 1;

        // Hex SHA-256, 64 characters
        public static string Compute(NewReleaseRequest request)
        {
            var canonical = new
            {
                v = Version,
                type = request.Type.ToString(),
                format = request.Format.ToString(),
                title = Normalize(request.Title),
                releaseYear = request.ReleaseYear,
                firstReleaseYear = request.FirstReleaseYear,
                durationSeconds = request.DurationSeconds,
                barcode = Normalize(request.Barcode),

                // In billing order, which is part of the credit
                credits = request.Credits.Select(x => new
                {
                    creatorId = x.CreatorId,
                    role = x.Role.ToString(),
                    joinPhrase = x.JoinPhrase,
                    characterName = Normalize(x.CharacterName)
                }),

                // In position order, whatever order they were sent in
                tracks = request.Tracks
                    .OrderBy(x => x.DiscNumber)
                    .ThenBy(x => x.TrackNumber)
                    .Select(x => new
                    {
                        disc = x.DiscNumber,
                        track = x.TrackNumber,
                        title = Normalize(x.Title),
                        durationSeconds = x.DurationSeconds
                    })
            };

            var json = JsonSerializer.SerializeToUtf8Bytes(canonical);
            return Convert.ToHexStringLower(SHA256.HashData(json));
        }

        private static string Normalize(string? s)
            => string.IsNullOrWhiteSpace(s) ? "" : WhitespaceRegex().Replace(s.Trim(), " ").ToLower(CultureInfo.InvariantCulture);

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();
    }
}
