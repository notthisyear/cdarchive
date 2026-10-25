using CdArchiveBackend.Data.External.MusicBrainz;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CdArchiveBackend.Services.Utilities
{
    internal static partial class MusicBrainzReleaseScorer
    {
        private const int TrackCountMatchPoints = 100;
        private const int TrackCountMissPenaltyPerTrack = 5;
        private const int ExactDatePoints = 50;
        private const int ArtistMatchPoints = 20;
        private const int NoArtistMatchPenalty = 50;
        private const int TypeMatchPoints = 10;
        private const int TypeMismatchPenalty = 10;
        private const int TitleMatchPoints = 100;
        private const double TitleSimilarityLowerThreshold = 0.7;
        private const double ArtistSimilarityLowerThreshold = 0.8;
        private enum DatePrecision
        {
            Year = 1,
            Month = 2,
            Day = 3
        }

        [GeneratedRegex(@"^(\d{4})(?:-(\d{2})(?:-(\d{2}))?)?$")]
        private static partial Regex PartialDateParseRegex();

        public static List<ReleaseCandidateDto> RankAndFilterReleases(this IEnumerable<ReleaseCandidateDto> candidates,
                                                                      string expectedTitle,
                                                                      int trackCount,
                                                                      DateOnly actualReleaseDate,
                                                                      string[] artists,
                                                                      string type,
                                                                      int minimumTotalScore = int.MinValue)
        {
            var wantedArtists = artists.Select(TrimAndRemoveLeadingThe).Where(x => x.Length > 0);
            var expected = TitleSimilarity.Prepare(expectedTitle);
            return  [..candidates
                .Select(x => (score: Score(x, expected, trackCount, actualReleaseDate, wantedArtists, type), release: x))
                .Where(x => x.score >= minimumTotalScore)
                .OrderByDescending(x => x.score)
                .Select(x => x.release)];
        }

        private static int Score(ReleaseCandidateDto c,
                                 TitleSimilarity.Prepared expectedTitle,
                                 int trackCount,
                                 DateOnly actualReleaseDate,
                                 IEnumerable<string> wantedArtists,
                                 string type)
        {
            var s = 0;
            s += c.TrackCount == trackCount ? TrackCountMatchPoints : 
                -TrackCountMissPenaltyPerTrack * Math.Abs((c.TrackCount ?? 0) - trackCount);

            s += DateScore(c.Date, actualReleaseDate);
            if (wantedArtists.Any())
            {
                var matches = 0;
                foreach (var artistCredit in c.ArtistCredit ?? [])
                {
                    matches += wantedArtists
                        .Select(x => NameSimilarity.Score(x, TrimAndRemoveLeadingThe(artistCredit.Name)))
                        .Any(x => x > ArtistSimilarityLowerThreshold) ? 1 : 0;
                }
                s += matches > 0 ? matches * ArtistMatchPoints : -NoArtistMatchPenalty;
            }

            s += TypeScore(type, c.ReleaseGroup?.PrimaryType);

            var titleScore = TitleSimilarity.Score(expectedTitle, TitleSimilarity.Prepare(c.Title));
            s += (int)((titleScore < TitleSimilarityLowerThreshold) ? ((1 - titleScore) * -TitleMatchPoints) : (titleScore * TitleMatchPoints));

            return s;
        }

        private static string TrimAndRemoveLeadingThe(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";

            var s = name.Trim();
            if (s.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
                s = s[4..];

            return s;
        }

        private static int DateScore(string? mbDate, DateOnly actualReleaseDate)
        {
            if (!TryParsePartialDate(mbDate, out var year, out var month, out var day))
                return 0;

            var candidateLevel = day is not null ? DatePrecision.Day
                               : month is not null ? DatePrecision.Month
                               : DatePrecision.Year;

            var level = (DatePrecision)Math.Min((int)candidateLevel, (int)DatePrecision.Day);

            var yearsApart = Math.Abs(year - actualReleaseDate.Year);
            if (level == DatePrecision.Day)
            {
                // Compare actual calendar days, which also handles Dec 31 vs Jan 1
                var days = Math.Abs(new DateOnly(year, month!.Value, day!.Value).DayNumber - actualReleaseDate.DayNumber);
                if (days == 0)
                    return 50;

                if (days <= 7)
                    return 40;       // digital and physical release dates are often a few days apart
            }

            if (level >= DatePrecision.Month && year == actualReleaseDate.Year && month == actualReleaseDate.Month)
                return 25;

            if (year == actualReleaseDate.Year)
                return 15;

            return yearsApart == 1 ? 5 : 0;
        }

        private static int TypeScore(string? type, string? primaryType)
        {
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(primaryType))
                return 0;

            bool Is(string name) => string.Equals(primaryType.Trim(), name, StringComparison.OrdinalIgnoreCase);

            // Only Album, EP and Single say anything about the release
            if (!Is("Album") && !Is("EP") && !Is("Single"))
                return 0;

            bool? matches = type.Trim().ToLowerInvariant() switch
            {
                "album" or "compilation" => Is("Album") || Is("EP"),
                "single" => Is("Single") || Is("EP"),
                _ => null
            };

            return matches switch
            {
                true => TypeMatchPoints,
                false => -TypeMismatchPenalty,
                null => 0
            };
        }

        private static bool TryParsePartialDate(string? s, out int year, out int? month, out int? day)
        {
            year = 0; month = null; day = null;

            var m = PartialDateParseRegex().Match(s ?? "");
            if (!m.Success)
                return false;

            year = int.Parse(m.Groups[1].Value);
            if (year < 1)
                return false;

            if (m.Groups[2].Success)
            {
                var mo = int.Parse(m.Groups[2].Value);
                if (mo is < 1 or > 12)
                    return false;
                month = mo;

                if (m.Groups[3].Success)
                {
                    var d = int.Parse(m.Groups[3].Value);
                    if (d < 1 || d > DateTime.DaysInMonth(year, mo))
                        return false;
                    day = d;
                }
            }
            return true;

        }
    }
}
