using CdArchiveBackend.Common;
using Xunit;

namespace CdArchiveTests
{
    public class NameSimilarityTests
    {
        #region StringDistance
        [Theory]
        [InlineData("", "", 0)]
        [InlineData("abc", "abc", 0)]
        [InlineData("abc", "", 3)]
        [InlineData("", "abc", 3)]
        [InlineData("kitten", "sitting", 3)]    // two substitutions and an insertion
        [InlineData("flaw", "lawn", 2)]         // a deletion and an insertion
        [InlineData("abc", "abd", 1)]           // substitution
        [InlineData("abc", "abcd", 1)]          // insertion
        [InlineData("abcd", "abc", 1)]          // deletion
        public void StringDistance_CountsInsertionsDeletionsAndSubstitutions(string a, string b, int expected)
            => Assert.Equal(expected, NameSimilarity.StringDistance(a, b));

        [Theory]
        [InlineData("ca", "ac", 1)]
        [InlineData("abc", "acb", 1)]
        [InlineData("beatles", "baetles", 1)]
        public void StringDistance_CountsAdjacentTranspositionAsOneEdit(string a, string b, int expected)
            => Assert.Equal(expected, NameSimilarity.StringDistance(a, b));

        [Fact]
        public void StringDistance_DoesNotEditATransposedPairAgain()
        {
            // Optimal string alignment, not full Damerau-Levenshtein: "ca" -> "ac" -> "abc" would
            // be 2, but a transposed pair can't be edited further, so it's 3
            Assert.Equal(3, NameSimilarity.StringDistance("ca", "abc"));
        }

        [Theory]
        [InlineData("kitten", "sitting")]
        [InlineData("ca", "abc")]
        [InlineData("", "abc")]
        public void StringDistance_IsSymmetric(string a, string b)
            => Assert.Equal(NameSimilarity.StringDistance(a, b), NameSimilarity.StringDistance(b, a));

        [Fact]
        public void StringDistance_HandlesStringsTooLongForTheStack()
        {
            // Rows of more than ~85 chars come from the array pool instead of the stack
            var a = new string('a', 200);
            var b = new string('a', 100) + "b" + new string('a', 99);

            Assert.Equal(1, NameSimilarity.StringDistance(a, b));
            Assert.Equal(200, NameSimilarity.StringDistance(a, new string('b', 200)));
        }
        #endregion

        #region Normalize
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Normalize_EmptyOrWhitespace_IsEmpty(string? name)
            => Assert.Equal("", NameSimilarity.Normalize(name));

        [Theory]
        [InlineData("Björk", "bjork")]
        [InlineData("Mötley Crüe", "motley crue")]
        [InlineData("Sigur Rós", "sigur ros")]
        public void Normalize_LowercasesAndStripsDiacritics(string name, string expected)
            => Assert.Equal(expected, NameSimilarity.Normalize(name));

        [Theory]
        [InlineData("AC/DC", "ac dc")]
        [InlineData("Guns N' Roses", "guns n roses")]
        [InlineData("  The   Beatles!! ", "the beatles")]
        [InlineData("Crosby, Stills & Nash", "crosby stills nash")]
        public void Normalize_TurnsPunctuationAndWhitespaceIntoSingleSpaces(string name, string expected)
            => Assert.Equal(expected, NameSimilarity.Normalize(name));
        #endregion

        #region Score
        [Theory]
        [InlineData("Kiss", "Kiss")]
        [InlineData("KISS", "kiss")]
        [InlineData("Björk", "Bjork")]
        [InlineData("AC/DC", "AC-DC")]
        public void Score_SameNameWrittenDifferently_IsOne(string expected, string candidate)
            => Assert.Equal(1.0, NameSimilarity.Score(expected, candidate));

        [Theory]
        [InlineData("John Lennon", "Lennon, John")]
        [InlineData("Simon Garfunkel", "Garfunkel Simon")]
        public void Score_ReorderedNames_IsOne(string expected, string candidate)
            => Assert.Equal(1.0, NameSimilarity.Score(expected, candidate));

        [Fact]
        public void Score_Typo_IsHigh()
            => Assert.True(NameSimilarity.Score("Metallica", "Metalica") > 0.8);

        [Theory]
        [InlineData("Kiss", "Abba")]
        [InlineData("The Beatles", "Pink Floyd")]
        public void Score_DifferentNames_IsLow(string expected, string candidate)
            => Assert.True(NameSimilarity.Score(expected, candidate) < 0.5);

        [Theory]
        [InlineData("!!!", "")]
        [InlineData("", "!!!")]
        [InlineData("!!!", "   ")]
        [InlineData("   ", "!!!")]
        public void Score_PunctuationOnlyNameAgainstEmpty_IsNotOne(string expected, string candidate)
        {
            // Both normalize to "", but the band "!!!" isn't a match for a missing name
            Assert.True(NameSimilarity.Score(expected, candidate) < 1.0);
        }

        [Theory]
        [InlineData("!!!", "!!!", 1.0)]
        [InlineData("!!!", " !!! ", 1.0)]
        [InlineData("!!!", "???", 0.0)]
        public void Score_PunctuationOnlyNames_AreComparedAsWritten(string expected, string candidate, double score)
            => Assert.Equal(score, NameSimilarity.Score(expected, candidate), precision: 10);

        [Theory]
        [InlineData("Metallica", "Metalica")]
        [InlineData("John Lennon", "Lennon, John")]
        [InlineData("Kiss", "Abba")]
        public void Score_IsSymmetric(string a, string b)
            => Assert.Equal(NameSimilarity.Score(a, b), NameSimilarity.Score(b, a));
        #endregion
    }
}
