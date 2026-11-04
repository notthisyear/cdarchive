using CdArchiveBackend.Data.Database;
using CdArchiveBackend.Data.DTO;
using Xunit;

namespace CdArchiveTests
{
    public class NewCreatorRequestTests
    {
        private const string Mbid = "e1f1e33e-2e4c-4d43-b91b-7064068d3283";

        private static NewCreatorRequest Request(string name = "Kiss",
                                                 string? country = "US",
                                                 string? musicBrainzId = null,
                                                 string? spotifyUrl = null,
                                                 NewImage? image = null)
            => new(name, null, CreatorType.Group, country, musicBrainzId, spotifyUrl, image);

        [Fact]
        public void MinimalRequestIsValid()
            => Assert.True(Request().Validate(fileCount: 0));

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void EmptyNameIsInvalid(string name)
            => Assert.False(Request(name: name).Validate(0));

        [Fact]
        public void TooLongNameIsInvalid()
            => Assert.False(Request(name: new string('a', 301)).Validate(0));

        [Theory]
        [InlineData("USA")]
        [InlineData("U")]
        public void CountryThatIsntTwoLettersIsInvalid(string country)
            => Assert.False(Request(country: country).Validate(0));

        [Fact]
        public void MusicBrainzIdThatIsntAGuidIsInvalid()
            => Assert.False(Request(musicBrainzId: "kiss/../../admin").Validate(0));

        [Fact]
        public void MusicBrainzIdIsValid()
            => Assert.True(Request(musicBrainzId: Mbid).Validate(0));

        #region Spotify
        [Theory]
        [InlineData("https://open.spotify.com/artist/07XSN3sPlIlB2L2XNcTwJw")]
        [InlineData("https://open.spotify.com/artist/07XSN3sPlIlB2L2XNcTwJw?si=abc123")]
        [InlineData("https://open.spotify.com/intl-de/artist/07XSN3sPlIlB2L2XNcTwJw")]
        [InlineData("https://open.spotify.com/intl-pt-br/artist/07XSN3sPlIlB2L2XNcTwJw/")]
        [InlineData("  https://open.spotify.com/artist/07XSN3sPlIlB2L2XNcTwJw  ")]
        public void SpotifyArtistUrlGivesItsId(string url)
        {
            var request = Request(spotifyUrl: url);
            Assert.True(request.Validate(0));
            Assert.Equal("07XSN3sPlIlB2L2XNcTwJw", request.GetSpotifyId());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NoSpotifyUrlIsValidWithoutId(string? url)
        {
            var request = Request(spotifyUrl: url);
            Assert.True(request.Validate(0));
            Assert.Null(request.GetSpotifyId());
        }

        [Theory]
        [InlineData("https://open.spotify.com/album/07XSN3sPlIlB2L2XNcTwJw")]   // not an artist
        [InlineData("http://open.spotify.com/artist/07XSN3sPlIlB2L2XNcTwJw")]   // not https
        [InlineData("https://evil.example/artist/07XSN3sPlIlB2L2XNcTwJw")]
        [InlineData("https://open.spotify.com/artist/tooshort")]
        [InlineData("spotify:artist:07XSN3sPlIlB2L2XNcTwJw")]
        public void OtherSpotifyUrlsAreInvalid(string url)
            => Assert.False(Request(spotifyUrl: url).Validate(0));
        #endregion

        #region Image
        [Fact]
        public void UploadedImageMustReferToASentFile()
        {
            var image = new NewImage(MediaSource.Upload, null, 0);
            Assert.True(Request(image: image).Validate(fileCount: 1));
            Assert.False(Request(image: image).Validate(fileCount: 0));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(1)]
        public void UploadedImageWithIndexOutsideTheFilesIsInvalid(int fileIndex)
            => Assert.False(Request(image: new NewImage(MediaSource.Upload, null, fileIndex)).Validate(fileCount: 1));

        [Fact]
        public void UploadedImageWithExternalIdIsInvalid()
            => Assert.False(Request(image: new NewImage(MediaSource.Upload, Mbid, 0)).Validate(fileCount: 1));

        [Theory]
        [InlineData(MediaSource.Wikimedia)]
        [InlineData(MediaSource.CoverArtArchive)]
        public void LookedUpImageNeedsAMusicBrainzId(MediaSource source)
        {
            Assert.True(Request(image: new NewImage(source, Mbid, null)).Validate(0));
            Assert.False(Request(image: new NewImage(source, null, null)).Validate(0));
            Assert.False(Request(image: new NewImage(source, "https://evil.example/image.jpg", null)).Validate(0));
        }

        [Fact]
        public void LookedUpImageWithFileIndexIsInvalid()
            => Assert.False(Request(image: new NewImage(MediaSource.Wikimedia, Mbid, 0)).Validate(fileCount: 1));

        [Theory]
        [InlineData(MediaSource.Spotify)]
        [InlineData(MediaSource.Other)]
        public void ImagesFromOtherSourcesAreInvalid(MediaSource source)
            => Assert.False(Request(image: new NewImage(source, Mbid, null)).Validate(0));
        #endregion
    }
}
