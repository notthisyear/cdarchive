using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CdArchiveBackend.Services.Utilities
{
    public static partial class TitleSimilarity
    {
        public sealed record Prepared(string Full, string Sorted, string Main, HashSet<string> Numbers);

        private static readonly HashSet<string> Articles = ["the", "a", "an"];

        // Mapped identically on both sides, so "Rocky II" == "Rocky 2" == "Rocky Two"
        private static readonly Dictionary<string, string> NumberMap = new()
        {
            ["ii"] = "2",
            ["iii"] = "3",
            ["iv"] = "4",
            ["v"] = "5",
            ["vi"] = "6",
            ["vii"] = "7",
            ["viii"] = "8",
            ["ix"] = "9",
            ["two"] = "2",
            ["three"] = "3",
            ["four"] = "4",
            ["five"] = "5",
            ["six"] = "6",
            ["seven"] = "7",
            ["eight"] = "8",
            ["nine"] = "9",
            ["ten"] = "10",
        };

        /// <summary>Prepare a title once; reuse the expected title across all candidates.</summary>
        public static Prepared Prepare(string raw)
        {
            var tokens = Tokens(raw);
            var main = Tokens(MainTitle(raw));
            return new Prepared(
                string.Join(' ', tokens),
                string.Join(' ', tokens.OrderBy(t => t, StringComparer.Ordinal)),
                string.Join(' ', main),
                [.. tokens.Where(t => t.All(char.IsDigit))]);
        }

        public static double Score(Prepared expected, Prepared candidate)
        {
            var score = Math.Max(
                Similarity(expected.Full, candidate.Full),
                Similarity(expected.Sorted, candidate.Sorted));

            // Subtitle present on only one side: compare main titles, slightly discounted
            score = Math.Max(score, 0.9 * Similarity(expected.Main, candidate.Main));

            // Different sequel/volume numbers: almost certainly a different work
            if (!expected.Numbers.SetEquals(candidate.Numbers))
                score *= 0.5;

            return score;
        }

        private static string[] Tokens(string raw)
        {
            string s = TitleNoiseRegex().Replace(raw.Replace("&", " and "), " ");
            s = EmptyBracketsRegex().Replace(s, " ");
            s = s.Replace("'", "").Replace("’", ""); // "Philosopher's" -> "philosophers"

            var tokens = NameSimilarity.Normalize(s)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => NumberMap.TryGetValue(t, out var d) ? d : t)
                .ToList();

            if (tokens.Count > 1 && Articles.Contains(tokens[0]))
                tokens.RemoveAt(0);

            return [.. tokens];
        }

        private static string MainTitle(string raw)
        {
            var m = SubtitleSeparatorRegex().Match(raw);
            return m.Success && m.Index > 0 ? raw[..m.Index] : raw;
        }

        private static double Similarity(string a, string b)
        {
            var maxLen = Math.Max(a.Length, b.Length);
            return maxLen == 0 ? 1.0 : 1.0 - (double)NameSimilarity.StringDistance(a, b) / maxLen;
        }

        // Edition/version noise that shouldn't count against a match
        [GeneratedRegex(@"\b(?:\d{4}\s+)?(?:digital(?:ly)?\s+)?remaster(?:ed)?(?:\s+\d{4})?\b|\b\d+(?:st|nd|rd|th)\s+anniversary\b|\b(?:deluxe|expanded|super|special|collector'?s|anniversary|limited|extended|director'?s\s+cut|unrated|theatrical|explicit|clean|bonus\s+tracks?|edition|version)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, "sv-SE")]
        private static partial Regex TitleNoiseRegex();

        // Brackets left empty after noise removal, or containing only a year: "(1982)", "[ ]"
        [GeneratedRegex(@"[\(\[][\s\d,.\-]*[\)\]]", RegexOptions.Compiled)]
        private static partial Regex EmptyBracketsRegex();

        [GeneratedRegex(@"\s*(?::|\s[-–—]\s)", RegexOptions.Compiled)]
        private static partial Regex SubtitleSeparatorRegex();
    }
}