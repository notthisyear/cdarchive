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
        public void MinimalRequest_IsValid()
            => Assert.True(Request().Validate(fileCount: 0));

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void EmptyTitle_IsInvalid(string title)
            => Assert.False(Request(title: title).Validate(0));

        [Fact]
        public void FirstReleasedAfterThisRelease_IsInvalid()
            => Assert.False(Request(releaseYear: 1976, firstReleaseYear: 1997).Validate(0));

        [Fact]
        public void NegativeDuration_IsInvalid()
            => Assert.False(Request(durationSeconds: -1).Validate(0));

        [Theory]
        [InlineData(Release.MinScore)]
        [InlineData(Release.MaxScore)]
        [InlineData(null)]
        public void ScoreInRangeOrNone_IsValid(int? score)
            => Assert.True(Request(score: score).Validate(0));

        [Theory]
        [InlineData(Release.MinScore - 1)]
        [InlineData(Release.MaxScore + 1)]
        public void ScoreOutOfRange_IsInvalid(int score)
            => Assert.False(Request(score: score).Validate(0));

        [Fact]
        public void MusicBrainzIds_MustBeGuids()
        {
            Assert.True(Request(musicBrainzReleaseId: Mbid, musicBrainzReleaseGroupId: Mbid).Validate(0));
            Assert.False(Request(musicBrainzReleaseId: "../release").Validate(0));
            Assert.False(Request(musicBrainzReleaseGroupId: "destroyer").Validate(0));
        }

        [Fact]
        public void SpotifyAlbumId_IsValid()
            => Assert.True(Request(spotifyAlbumId: "0YQ5ZsSIbzeRwFMBiQh0eg").Validate(0));

        [Theory]
        [InlineData("")]
        [InlineData("tooshort")]
        [InlineData("0YQ5ZsSIbzeRwFMBiQh0egX")]                         // 23 characters
        [InlineData("spotify:album:0YQ5ZsSIbzeRwFMBiQh0eg")]
        [InlineData("https://open.spotify.com/album/0YQ5ZsSIbzeRwFMBiQh0eg")]
        [InlineData("0YQ5ZsSIbzeRwFMBiQh0e/")]
        public void SpotifyAlbumId_ThatIsntAnId_IsInvalid(string spotifyAlbumId)
            => Assert.False(Request(spotifyAlbumId: spotifyAlbumId).Validate(0));

        [Fact]
        public void WithoutAPrimaryCredit_IsInvalid()
            => Assert.False(Request(credits: [new(1, CreditRole.Producer, "", null)]).Validate(0));

        [Fact]
        public void CreditWithoutAValidCreatorId_IsInvalid()
            => Assert.False(Request(credits: [new(0, CreditRole.Primary, "", null)]).Validate(0));

        [Fact]
        public void SameCreatorInTheSameRoleTwice_IsInvalid()
            => Assert.False(Request(credits: [new(1, CreditRole.Primary, " & ", null), new(1, CreditRole.Primary, "", null)]).Validate(0));

        [Fact]
        public void TwoTracksInTheSamePosition_IsInvalid()
            => Assert.False(Request(tracks: [new(1, 1, "A", null), new(1, 1, "B", null)]).Validate(0));

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        public void TrackPositionsStartAtOne(int discNumber, int trackNumber)
            => Assert.False(Request(tracks: [new(discNumber, trackNumber, "A", null)]).Validate(0));

        [Fact]
        public void TooManyImages_IsInvalid()
        {
            var images = Enumerable.Range(0, NewReleaseRequest.MaxImages + 1)
                .Select(i => new NewImage(MediaSource.Upload, null, i))
                .ToList();

            Assert.False(Request(images: images).Validate(fileCount: images.Count));
        }

        [Fact]
        public void MissingLists_AreInvalid()
        {
            var request = Request() with { Credits = null!, Tracks = null!, Images = null! };
            Assert.False(request.Validate(0));
        }
    }
}
