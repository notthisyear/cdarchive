using CdArchiveBackend.Data.Database;
using CdArchiveBackend.Data.DTO;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CdArchiveTests
{
    public class NewReleaseRequestTests
    {
        private const string Mbid = "8a5d9f5a-3c0e-4b5e-9a39-1f2d3c4b5a69";

        private static NewReleaseRequest Request(string title = "Destroyer",
                                                 int? releaseYear = 1997,
                                                 int? firstReleaseYear = 1976,
                                                 int? durationSeconds = 2060,
                                                 string? musicBrainzReleaseId = null,
                                                 string? musicBrainzReleaseGroupId = null,
                                                 string? spotifyAlbumId = null,
                                                 int? score = null,
                                                 IReadOnlyList<NewReleaseCredit>? credits = null,
                                                 IReadOnlyList<ReleaseTrack>? tracks = null,
                                                 IReadOnlyList<NewImage>? images = null)
            => new(title, ReleaseType.Record, ReleaseStatus.Owned, ReleaseFormat.Cd, releaseYear, firstReleaseYear, durationSeconds,
                   null, musicBrainzReleaseId, musicBrainzReleaseGroupId, spotifyAlbumId, null, score, null,
                   credits ?? [new(1, CreditRole.Primary, "", null)],
                   tracks ?? [],
                   images ?? []);

        [Fact]
        public void MinimalRequestIsValid()
            => Assert.True(Request().Validate(fileCount: 0));

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void EmptyTitleIsInvalid(string title)
            => Assert.False(Request(title: title).Validate(0));

        [Fact]
        public void FirstReleasedAfterThisReleaseIsInvalid()
            => Assert.False(Request(releaseYear: 1976, firstReleaseYear: 1997).Validate(0));

        [Fact]
        public void NegativeDurationIsInvalid()
            => Assert.False(Request(durationSeconds: -1).Validate(0));

        [Theory]
        [InlineData("SE")]
        [InlineData("xw")]
        [InlineData("")]
        [InlineData(null)]
        public void TwoLetterCountryOrNoneIsValid(string? country)
            => Assert.True((Request() with { Country = country }).Validate(0));

        [Theory]
        [InlineData("SWE")]
        [InlineData("S")]
        [InlineData("S1")]
        public void OtherCountryIsInvalid(string country)
            => Assert.False((Request() with { Country = country }).Validate(0));

        [Theory]
        [InlineData(Release.MinScore)]
        [InlineData(Release.MaxScore)]
        [InlineData(null)]
        public void ScoreInRangeOrNoneIsValid(int? score)
            => Assert.True(Request(score: score).Validate(0));

        [Theory]
        [InlineData(Release.MinScore - 1)]
        [InlineData(Release.MaxScore + 1)]
        public void ScoreOutOfRangeIsInvalid(int score)
            => Assert.False(Request(score: score).Validate(0));

        [Fact]
        public void MusicBrainzIdsMustBeGuids()
        {
            Assert.True(Request(musicBrainzReleaseId: Mbid, musicBrainzReleaseGroupId: Mbid).Validate(0));
            Assert.False(Request(musicBrainzReleaseId: "../release").Validate(0));
            Assert.False(Request(musicBrainzReleaseGroupId: "destroyer").Validate(0));
        }

        [Fact]
        public void SpotifyAlbumIdIsValid()
            => Assert.True(Request(spotifyAlbumId: "0YQ5ZsSIbzeRwFMBiQh0eg").Validate(0));

        [Theory]
        [InlineData("")]
        [InlineData("tooshort")]
        [InlineData("0YQ5ZsSIbzeRwFMBiQh0egX")]                         // 23 characters
        [InlineData("spotify:album:0YQ5ZsSIbzeRwFMBiQh0eg")]
        [InlineData("https://open.spotify.com/album/0YQ5ZsSIbzeRwFMBiQh0eg")]
        [InlineData("0YQ5ZsSIbzeRwFMBiQh0e/")]
        public void SpotifyAlbumIdThatIsntAnIdIsInvalid(string spotifyAlbumId)
            => Assert.False(Request(spotifyAlbumId: spotifyAlbumId).Validate(0));

        [Fact]
        public void WithoutAPrimaryCreditIsInvalid()
            => Assert.False(Request(credits: [new(1, CreditRole.Producer, "", null)]).Validate(0));

        [Theory]
        [InlineData(null)]
        [InlineData(Release.MinScore)]
        [InlineData(Release.MaxScore)]
        public void PersonalDataWithARatingInRangeOrNoneIsValid(int? score)
            => Assert.True(new PersonalReleaseData(null, score, false, null).Validate());

        [Theory]
        [InlineData(Release.MinScore - 1)]
        [InlineData(Release.MaxScore + 1)]
        public void PersonalDataWithARatingOutOfRangeIsInvalid(int score)
            => Assert.False(new PersonalReleaseData(null, score, false, null).Validate());

        [Fact]
        public void PersonalDataWithTooLongNotesIsInvalid()
            => Assert.False(new PersonalReleaseData(null, null, false, new string('a', 4001)).Validate());

        // UpdateReleaseRequest follows the same rules
        private static UpdateReleaseRequest Update(int? releaseYear = 1997, int? firstReleaseYear = 1976, NewImage? cover = null, IReadOnlyList<ReleaseTrack>? tracks = null)
            => new("Destroyer", ReleaseFormat.Cd, releaseYear, firstReleaseYear, 2060, null, null, null, null, null,
                   [new(1, CreditRole.Primary, "", null)], tracks, cover);

        [Fact]
        public void MinimalUpdateIsValid()
            => Assert.True(Update().Validate());

        [Fact]
        public void UpdateFirstReleasedAfterThisReleaseIsInvalid()
            => Assert.False(Update(releaseYear: 1976, firstReleaseYear: 1997).Validate());

        [Fact]
        public void UpdateWithALookedUpCoverIsValid()
            => Assert.True(Update(cover: new(MediaSource.CoverArtArchive, Mbid, null)).Validate());

        // Nothing is uploaded with an update
        [Fact]
        public void UpdateWithAnUploadedCoverIsInvalid()
            => Assert.False(Update(cover: new(MediaSource.Upload, null, 0)).Validate());

        [Fact]
        public void UpdateWithTwoTracksInOnePositionIsInvalid()
            => Assert.False(Update(tracks: [new(1, 1, "Beth", 165), new(1, 1, "Detroit Rock City", 318)]).Validate());

        [Fact]
        public void CreditedNameAsLongAsACreatorsNameIsValid()
            => Assert.True(Request(credits: [new(1, CreditRole.Primary, "", null, new string('a', 300))]).Validate(0));

        [Fact]
        public void CreditedNameLongerThanACreatorsNameIsInvalid()
            => Assert.False(Request(credits: [new(1, CreditRole.Primary, "", null, new string('a', 301))]).Validate(0));

        [Fact]
        public void CreditWithoutAValidCreatorIdIsInvalid()
            => Assert.False(Request(credits: [new(0, CreditRole.Primary, "", null)]).Validate(0));

        [Fact]
        public void SameCreatorInTheSameRoleTwiceIsInvalid()
            => Assert.False(Request(credits: [new(1, CreditRole.Primary, " & ", null), new(1, CreditRole.Primary, "", null)]).Validate(0));

        [Fact]
        public void TwoTracksInTheSamePositionIsInvalid()
            => Assert.False(Request(tracks: [new(1, 1, "A", null), new(1, 1, "B", null)]).Validate(0));

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        public void TrackPositionsStartAtOne(int discNumber, int trackNumber)
            => Assert.False(Request(tracks: [new(discNumber, trackNumber, "A", null)]).Validate(0));

        [Fact]
        public void TooManyImagesIsInvalid()
        {
            var images = Enumerable.Range(0, NewReleaseRequest.MaxImages + 1)
                .Select(i => new NewImage(MediaSource.Upload, null, i))
                .ToList();

            Assert.False(Request(images: images).Validate(fileCount: images.Count));
        }

        [Fact]
        public void MissingListsAreInvalid()
        {
            var request = Request() with { Credits = null!, Tracks = null!, Images = null! };
            Assert.False(request.Validate(0));
        }
    }
}
