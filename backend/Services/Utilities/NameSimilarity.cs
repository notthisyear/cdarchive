using System;
using System.Buffers;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CdArchiveBackend.Services.Utilities
{
    public static class NameSimilarity
    {
        // Note: This number is the same that they have in the docs, see https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/stackalloc
        //       We stackallocate up to 1 KiB -> rows of up to ~85 chars. Should be sufficient for most all media titles.
        private const int MaxStackAllocInts = 256; 

        /// <summary>Returns 1.0 for identical names, 0.0 for completely different ones.</summary>
        public static double Score(string expected, string candidate)
        {
            var a = Normalize(expected);
            var b = Normalize(candidate);

            var direct = StringSimilarity(a, b);
            if (direct == 1.0)
                return 1.0;

            // Ensure reordered names are caught
            var tokenSorted = StringSimilarity(SortTokens(a), SortTokens(b));

            return Math.Max(direct, tokenSorted);
        }

        /// <summary>
        /// Calculates the Levensthtein distance (also known as optimal string alignment).
        /// Insertions, deletions, substitutions and adjacent transpositions all have a
        /// cost of 1.
        /// </summary>
        /// <param name="a">The first string.</param>
        /// <param name="b">The second string.</param>
        /// <returns>The distance between a and b.</returns>
        public static int StringDistance(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
        {
            // Strip common prefix and suffix, (cheap and often removes most of the work)
            int prefix = 0;
            while (prefix < a.Length &&
                   prefix < b.Length &&
                   a[prefix] == b[prefix])
            {
                prefix++;
            }

            a = a[prefix..];
            b = b[prefix..];

            int suffix = 0;
            while (suffix < a.Length &&
                   suffix < b.Length &&
                   a[a.Length - 1 - suffix] == b[b.Length - 1 - suffix])
            {
                suffix++;
            }

            a = a[..^suffix];
            b = b[..^suffix];

            // Make b the shorter string so the rows are as small as possible
            if (a.Length < b.Length)
            {
                var t = a;
                a = b;
                b = t;
            }

            if (b.Length == 0)
                return a.Length;

            var n = b.Length + 1;
            var needed = n * 3;

            int[]? rented = null;
            Span<int> buffer = needed <= MaxStackAllocInts ? stackalloc int[MaxStackAllocInts] : (rented = ArrayPool<int>.Shared.Rent(needed));
            
            // The rows of the table
            Span<int> prev2 = buffer[..n];
            Span<int> prev = buffer.Slice(n, n);
            Span<int> curr = buffer.Slice(2 * n, n);

            try
            {
                for (int j = 0; j < n; j++)
                    prev[j] = j;

                for (int i = 1; i <= a.Length; i++)
                {
                    curr[0] = i;
                    var aChar = a[i - 1];

                    for (int j = 1; j < n; j++)
                    {
                        var bChar = b[j - 1];
                        var cost = aChar == bChar ? 0 : 1;

                        var v = Math.Min(
                            Math.Min(prev[j] + 1,      // deletion
                                     curr[j - 1] + 1), // insertion
                            prev[j - 1] + cost);       // substitution

                        if (i > 1 &&
                            j > 1 &&
                            aChar == b[j - 2] &&
                            a[i - 2] == bChar)
                        {
                            v = Math.Min(v, prev2[j - 2] + 1); // transposition
                        }

                        curr[j] = v;
                    }

                    // Rotate the rows without allocating
                    var tmp = prev2;
                    prev2 = prev;
                    prev = curr;
                    curr = tmp;
                }

                return prev[n - 1];
            }
            finally
            {
                if (rented is not null)
                    ArrayPool<int>.Shared.Return(rented);
            }
        }

        /// <summary>Lowercase, strip diacritics, drop punctuation, collapse whitespace.</summary>
        public static string Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return string.Empty;

            var decomposed = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            var lastWasSpace = true;

            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue; // é -> e, å -> a

                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                    lastWasSpace = false;
                }
                else if (!lastWasSpace) // whitespace, hyphens, commas etc. become a single space
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
            }

            if (sb.Length > 0 && sb[^1] == ' ')
                sb.Length--;

            return sb.ToString();
        }

        private static double StringSimilarity(string a, string b)
        {
            int maxLen = Math.Max(a.Length, b.Length);
            if (maxLen == 0)
                return 1.0;

            return 1.0 - (double)StringDistance(a, b) / maxLen;
        }

        private static string SortTokens(string s) =>
            string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries).OrderBy(t => t, StringComparer.Ordinal));
    }
}
