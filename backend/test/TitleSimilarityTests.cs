using CdArchiveBackend.Services.Utilities;
using Xunit;

namespace CdArchiveTests
{
    public class TitleSimilarityTests
    {
        // MusicBrainzReleaseScorer treats titles scoring below this as different
        private const double MatchThreshold = MusicBrainzReleaseScorer.TitleSimilarityLowerThreshold;

        private static double Score(string expected, string candidate)
            => TitleSimilarity.Score(TitleSimilarity.Prepare(expected), TitleSimilarity.Prepare(candidate));

        [Theory]
        [InlineData("Destroyer", "Destroyer")]
        [InlineData("Destroyer", "DESTROYER")]
        [InlineData("Destroyer!", "Destroyer")]
        [InlineData("Déjà Vu", "Deja Vu")]
        public void SameTitle_IsOne(string expected, string candidate)
            => Assert.Equal(1.0, Score(expected, candidate));

        [Theory]
        [InlineData("The Wall", "Wall")]
        [InlineData("A Night at the Opera", "Night at the Opera")]
        [InlineData("An Innocent Man", "Innocent Man")]
        public void LeadingArticle_IsIgnored(string expected, string candidate)
            => Assert.Equal(1.0, Score(expected, candidate));

        [Fact]
        public void ArticleAlone_IsKept()
            => Assert.True(Score("The", "Them") < 1.0);

        [Fact]
        public void Ampersand_EqualsAnd()
            => Assert.Equal(1.0, Score("Sticky & Sweet", "Sticky and Sweet"));

        [Theory]
        [InlineData("Sgt. Pepper's Lonely Hearts Club Band", "Sgt Peppers Lonely Hearts Club Band")]
        [InlineData("Sgt. Pepper’s Lonely Hearts Club Band", "Sgt. Pepper's Lonely Hearts Club Band")]
        public void Apostrophes_AreIgnored(string expected, string candidate)
            => Assert.Equal(1.0, Score(expected, candidate));

        [Fact]
        public void WordOrder_IsIgnored()
            => Assert.Equal(1.0, Score("Hits Greatest", "Greatest Hits"));

        [Theory]
        [InlineData("Destroyer (Remastered)")]
        [InlineData("Destroyer (2012 Remaster)")]
        [InlineData("Destroyer - Remastered 2012")]
        [InlineData("Destroyer (Digitally Remastered)")]
        [InlineData("Destroyer (Deluxe Edition)")]
        [InlineData("Destroyer (Expanded Edition)")]
        [InlineData("Destroyer (45th Anniversary Edition)")]
        [InlineData("Destroyer (Super Deluxe Edition)")]
        [InlineData("Destroyer (Bonus Tracks Version)")]
        [InlineData("Destroyer [Explicit]")]
        [InlineData("Destroyer (1976)")]
        [InlineData("Destroyer (Collector's Edition)")]
        [InlineData("Destroyer (Collector’s Edition)")]
        public void EditionNoise_IsIgnored(string candidate)
            => Assert.Equal(1.0, Score("Destroyer", candidate));

        [Theory]
        [InlineData("Blade Runner (Director's Cut)", "Blade Runner")]
        [InlineData("Blade Runner (Theatrical Version)", "Blade Runner")]
        [InlineData("Blade Runner (Unrated Extended Edition)", "Blade Runner")]
        public void FilmEditionNoise_IsIgnored(string expected, string candidate)
            => Assert.Equal(1.0, Score(expected, candidate));

        [Theory]
        [InlineData("Super", "Super")]
        [InlineData("Special", "Special")]
        public void TitleMadeOnlyOfNoiseWords_MatchesItself(string expected, string candidate)
            => Assert.Equal(1.0, Score(expected, candidate));

        [Theory]
        [InlineData("Super", "Deluxe")]
        [InlineData("Special", "Limited")]
        public void TitlesMadeOnlyOfNoiseWords_AreStillCompared(string expected, string candidate)
            => Assert.True(Score(expected, candidate) < MatchThreshold);

        [Theory]
        [InlineData("Rocky II", "Rocky 2")]
        [InlineData("Rocky II", "Rocky Two")]
        [InlineData("Rocky X", "Rocky 10")]
        [InlineData("Greatest Hits Vol. I", "Greatest Hits Vol. 1")]
        [InlineData("Led Zeppelin IV", "Led Zeppelin 4")]
        public void RomanNumeralsAndNumberWords_EqualDigits(string expected, string candidate)
            => Assert.Equal(1.0, Score(expected, candidate));

        [Theory]
        [InlineData("Rocky II", "Rocky III")]
        [InlineData("Greatest Hits Vol. 1", "Greatest Hits Vol. 2")]
        [InlineData("Led Zeppelin II", "Led Zeppelin")]
        public void DifferentNumbers_AreDifferentWorks(string expected, string candidate)
            => Assert.True(Score(expected, candidate) < MatchThreshold);

        [Theory]
        [InlineData("Star Wars: A New Hope", "Star Wars")]
        [InlineData("Star Wars", "Star Wars: A New Hope")]
        [InlineData("Destroyer - Resurrected", "Destroyer")]
        public void SubtitleOnOneSide_MatchesMainTitleSlightlyDiscounted(string expected, string candidate)
            => Assert.Equal(0.9, Score(expected, candidate), precision: 10);

        [Fact]
        public void DifferentSubtitles_AreDifferentWorks()
            => Assert.True(Score("Star Wars: A New Hope", "Star Wars: The Empire Strikes Back") < MatchThreshold);

        [Fact]
        public void LongerTitleStartingTheSame_IsNotAMatch()
            => Assert.True(Score("Destroyer", "Destroyer Demos") < MatchThreshold);

        [Fact]
        public void Live_IsNotNoise()
            => Assert.True(Score("Destroyer", "Destroyer (Live)") < MatchThreshold);

        [Theory]
        [InlineData("Destroyer", "Love Gun")]
        [InlineData("The Wall", "Animals")]
        public void DifferentTitles_AreLow(string expected, string candidate)
            => Assert.True(Score(expected, candidate) < 0.5);

        #region Where the match threshold falls
        [Theory]
        [InlineData("Rumours", "Rumors")]
        [InlineData("Colour by Numbers", "Color by Numbers")]
        [InlineData("Physical Graffiti", "Physical Grafitti")]
        [InlineData("MTV Unplugged in New York", "Unplugged in New York")]
        public void SpellingVariants_AreAMatch(string expected, string candidate)
            => Assert.True(Score(expected, candidate) >= MatchThreshold);

        [Theory]
        [InlineData("Greatest Hits", "Greatest Hits Live")]
        [InlineData("Greatest Hits", "Greatest Hits Remixed")]
        [InlineData("Love Gun", "Love Gun Live")]
        public void OneMoreWord_IsNotAMatch(string expected, string candidate)
            => Assert.True(Score(expected, candidate) < MatchThreshold);

        [Theory]
        [InlineData("Help", "Hell")]
        [InlineData("Love", "Live")]
        [InlineData("Unplugged", "Plugged")]
        public void ShortTitlesOneLetterApart_AreNotAMatch(string expected, string candidate)
            => Assert.True(Score(expected, candidate) < MatchThreshold);

        // Comparing characters can't tell a typo from a different word one letter off. Title
        // similarity alone can't tell these apart; the artist, track count and date have to.
        [Theory]
        [InlineData("Hot Space", "Hot Spice")]
        [InlineData("Animals", "Animal")]
        public void KnownLimitation_DifferentTitlesOneLetterApart_AreAMatch(string expected, string candidate)
            => Assert.True(Score(expected, candidate) >= MatchThreshold);

        // Shortened titles lose too many characters to match without letting noise in
        [Theory]
        [InlineData("Unplugged", "MTV Unplugged")]
        [InlineData("Sgt Pepper", "Sgt. Pepper's Lonely Hearts Club Band")]
        [InlineData("Ziggy Stardust", "The Rise and Fall of Ziggy Stardust and the Spiders From Mars")]
        public void KnownLimitation_ShortenedTitles_AreNotAMatch(string expected, string candidate)
            => Assert.True(Score(expected, candidate) < MatchThreshold);
        #endregion
    }
}
