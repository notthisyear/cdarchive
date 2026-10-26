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
        public void MinimalRequest_IsValid()
            => Assert.True(Request().Validate(fileCount: 0));

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void EmptyName_IsInvalid(string name)
            => Assert.False(Request(name: name).Validate(0));

        [Fact]
        public void TooLongName_IsInvalid()
            => Assert.False(Request(name: new string('a', 301)).Validate(0));

        [Theory]
        [InlineData("USA")]
        [InlineData("U")]
        public void CountryThatIsntTwoLetters_IsInvalid(string country)
            => Assert.False(Request(country: country).Validate(0));

        [Fact]
        public void MusicBrainzIdThatIsntAGuid_IsInvalid()
            => Assert.False(Request(musicBrainzId: "kiss/../../admin").Validate(0));

        [Fact]
        public void MusicBrainzId_IsValid()
            => Assert.True(Request(musicBrainzId: Mbid).Validate(0));

        #region Spotify
        [Theory]
        [InlineData("https://open.spotify.com/artist/07XSN3sPlIlB2L2XNcTwJw")]
        [InlineData("https://open.spotify.com/artist/07XSN3sPlIlB2L2XNcTwJw?si=abc123")]
        [InlineData("https://open.spotify.com/intl-de/artist/07XSN3sPlIlB2L2XNcTwJw")]
        [InlineData("https://open.spotify.com/intl-pt-br/artist/07XSN3sPlIlB2L2XNcTwJw/")]
        [InlineData("  https://open.spotify.com/artist/07XSN3sPlIlB2L2XNcTwJw  ")]
        public void SpotifyArtistUrl_GivesItsId(string url)
        {
            var request = Request(spotifyUrl: url);
            Assert.True(request.Validate(0));
            Assert.Equal("07XSN3sPlIlB2L2XNcTwJw", request.GetSpotifyId());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NoSpotifyUrl_IsValidWithoutId(string? url)
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
        public void OtherSpotifyUrls_AreInvalid(string url)
            => Assert.False(Request(spotifyUrl: url).Validate(0));
        #endregion

        #region Image
        [Fact]
        public void UploadedImage_MustReferToASentFile()
        {
            var image = new NewImage(MediaSource.Upload, null, 0);
            Assert.True(Request(image: image).Validate(fileCount: 1));
            Assert.False(Request(image: image).Validate(fileCount: 0));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(1)]
        public void UploadedImage_WithIndexOutsideTheFiles_IsInvalid(int fileIndex)
            => Assert.False(Request(image: new NewImage(MediaSource.Upload, null, fileIndex)).Validate(fileCount: 1));

        [Fact]
        public void UploadedImage_WithExternalId_IsInvalid()
            => Assert.False(Request(image: new NewImage(MediaSource.Upload, Mbid, 0)).Validate(fileCount: 1));

        [Theory]
        [InlineData(MediaSource.Wikimedia)]
        [InlineData(MediaSource.CoverArtArchive)]
        public void LookedUpImage_NeedsAMusicBrainzId(MediaSource source)
        {
            Assert.True(Request(image: new NewImage(source, Mbid, null)).Validate(0));
            Assert.False(Request(image: new NewImage(source, null, null)).Validate(0));
            Assert.False(Request(image: new NewImage(source, "https://evil.example/image.jpg", null)).Validate(0));
        }

        [Fact]
        public void LookedUpImage_WithFileIndex_IsInvalid()
            => Assert.False(Request(image: new NewImage(MediaSource.Wikimedia, Mbid, 0)).Validate(fileCount: 1));

        [Theory]
        [InlineData(MediaSource.Spotify)]
        [InlineData(MediaSource.Other)]
        public void ImagesFromOtherSources_AreInvalid(MediaSource source)
            => Assert.False(Request(image: new NewImage(source, Mbid, null)).Validate(0));
        #endregion
    }
}
