using CdArchiveBackend;
using CdArchiveBackend.Data.Database;
using CdArchiveBackend.Data.DTO;
using CdArchiveBackend.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static CdArchiveTests.ExternalServiceStubs;

namespace CdArchiveTests
{
    using ImageError = MediaFileService.ImageError;

    public sealed class MediaFileServiceTests : IDisposable
    {
        private const string SharedFolder = ServiceHarness.SharedFolder;
        private const string UserFolder = ServiceHarness.UserFolder;
        private const long UserId = 7;

        private readonly ServiceHarness _harness = new();
        private readonly FakeFileSystem _fileSystem;
        private readonly FakeHttpHandler _http;
        private readonly DatabaseContext _db;
        private readonly MediaFileService _service;

        public MediaFileServiceTests()
        {
            _fileSystem = _harness.FileSystem;
            _http = _harness.Http;

            var request = _harness.NewRequest();
            _db = request.Db;
            _service = request.MediaFiles;
        }

        public void Dispose() => _harness.Dispose();

        // A service as in another request. Tests that need a second request create one.
        private MediaFileService CreateService() => _harness.NewRequest().MediaFiles;

        private static Microsoft.AspNetCore.Http.FormFileCollection Files(params byte[][] contents) => ServiceHarness.Files(contents);

        private Task<MediaFileService.ImageResult> UploadAsync(byte[] content, long userId = UserId, MediaFileService? service = null)
            => (service ?? _service).AddAsync(userId, new NewImage(MediaSource.Upload, null, 0), Files(content), CancellationToken.None);

        private Task<MediaFileService.ImageResult> AddCoverAsync()
            => _service.AddAsync(UserId, new NewImage(MediaSource.CoverArtArchive, ReleaseMbid, null), Files(), CancellationToken.None);

        private Task<MediaFileService.ImageResult> AddArtistPhotoAsync()
            => _service.AddAsync(UserId, new NewImage(MediaSource.Wikimedia, ArtistMbid, null), Files(), CancellationToken.None);

        private void CoverArtArchiveHasTheCover() => ExternalServiceStubs.CoverArtArchiveHasTheCover(_http);

        private void MusicBrainzKnowsTheWikidataEntity() => ExternalServiceStubs.MusicBrainzKnowsTheWikidataEntity(_http);

        private void WikidataNamesTheCommonsFile() => ExternalServiceStubs.WikidataNamesTheCommonsFile(_http);

        private void CommonsHasTheImageInfo(string thumbUrl = PhotoUrl) => ExternalServiceStubs.CommonsHasTheImageInfo(_http, thumbUrl);

        private void WikimediaHasThePhoto() => ExternalServiceStubs.WikimediaHasThePhoto(_http);

        #region Uploads
        [Fact]
        public async Task Upload_IsStoredInTheUsersFolder()
        {
            var result = await UploadAsync(Jpeg);

            Assert.Equal(ImageError.None, result.Error);
            var file = result.File!;
            Assert.Equal(UserId, file.OwnerUserId);
            Assert.Equal(MediaSource.Upload, file.Source);
            Assert.Equal(Jpeg.Length, file.ByteCount);
            Assert.Equal(Convert.ToHexStringLower(SHA1.HashData(Jpeg)), file.Sha1);
            Assert.Null(file.OriginalUrl);

            var userFolder = Path.Combine(UserFolder, UserId.ToString());
            Assert.Contains(userFolder, _fileSystem.Directories);
            Assert.Equal(Jpeg, _fileSystem.Files[Path.Combine(userFolder, file.FileName)]);
        }

        [Fact]
        public async Task Upload_IsAddedToTheContextButNotSaved()
        {
            var result = await UploadAsync(Jpeg);

            // The caller saves it together with whatever uses it
            Assert.Equal(EntityState.Added, _db.Entry(result.File!).State);
        }

        [Theory]
        [InlineData("jpeg", "image/jpeg", ".jpg")]
        [InlineData("png", "image/png", ".png")]
        [InlineData("webp", "image/webp", ".webp")]
        public async Task Upload_TypeComesFromTheContent(string format, string contentType, string extension)
        {
            var content = format switch { "jpeg" => Jpeg, "png" => Png, _ => Webp };

            var file = (await UploadAsync(content)).File!;

            Assert.Equal(contentType, file.ContentType);
            Assert.EndsWith(extension, file.FileName);
        }

        [Fact]
        public async Task Upload_ThatIsntAnImageWeTake_IsRejected()
        {
            // A GIF, and text pretending to be an image
            foreach (var content in new[] { "GIF89a....."u8.ToArray(), "<script>alert(1)</script>"u8.ToArray() })
            {
                var result = await UploadAsync(content);

                Assert.Equal(ImageError.Invalid, result.Error);
                Assert.Null(result.File);
            }

            Assert.Empty(_fileSystem.Files);
            Assert.Empty(_db.MediaFiles.Local);
        }

        [Fact]
        public async Task Upload_Empty_IsRejected()
        {
            Assert.Equal(ImageError.Invalid, (await UploadAsync([])).Error);
            Assert.Empty(_fileSystem.Files);
        }

        [Fact]
        public async Task Upload_LargerThanTheLimit_IsRejected()
        {
            var content = new byte[MediaFileService.MaxImageSizeBytes + 1];
            Jpeg.CopyTo(content, 0);

            Assert.Equal(ImageError.Invalid, (await UploadAsync(content)).Error);
            Assert.Empty(_fileSystem.Files);
        }

        [Fact]
        public async Task Upload_AtTheLimit_IsStored()
        {
            var content = new byte[MediaFileService.MaxImageSizeBytes];
            Jpeg.CopyTo(content, 0);

            Assert.Equal(ImageError.None, (await UploadAsync(content)).Error);
        }
        #endregion

        #region Deduplication
        [Fact]
        public async Task SameUploadTwiceInOneRequest_IsStoredOnce()
        {
            var files = Files(Jpeg, Jpeg);

            var first = await _service.AddAsync(UserId, new NewImage(MediaSource.Upload, null, 0), files, CancellationToken.None);
            var second = await _service.AddAsync(UserId, new NewImage(MediaSource.Upload, null, 1), files, CancellationToken.None);

            // One row, or saving would break the unique index on owner and SHA-1 (which the
            // in-memory database doesn't check, so that's tested here instead)
            Assert.Same(first.File, second.File);
            Assert.Single(_db.MediaFiles.Local);
            Assert.Single(_fileSystem.Files);
        }

        [Fact]
        public async Task SameUploadAsAnEarlierRequest_ReusesItsFile()
        {
            var first = (await UploadAsync(Jpeg)).File!;
            await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var second = (await UploadAsync(Jpeg, service: CreateService())).File!;

            Assert.Equal(first.Id, second.Id);
            Assert.Single(_fileSystem.Files);
        }

        [Fact]
        public async Task SameUploadByAnotherUser_IsStoredForThemToo()
        {
            var mine = (await UploadAsync(Jpeg, userId: 7)).File!;
            var theirs = (await UploadAsync(Jpeg, userId: 8)).File!;

            // Uploads are private, so each user has their own copy
            Assert.NotSame(mine, theirs);
            Assert.Equal(8, theirs.OwnerUserId);
            Assert.Equal(2, _fileSystem.Files.Count);
        }
        #endregion

        #region Looked-up images
        [Fact]
        public async Task LookedUpCover_IsDownloadedToTheSharedFolderWithItsCredit()
        {
            CoverArtArchiveHasTheCover();
            _http.Respond(CoverUrl, HttpStatusCode.OK, Jpeg);

            var result = await AddCoverAsync();

            Assert.Equal(ImageError.None, result.Error);
            var file = result.File!;
            Assert.Null(file.OwnerUserId);
            Assert.Equal(MediaSource.CoverArtArchive, file.Source);
            Assert.Equal(OriginalCoverUrl, file.OriginalUrl);
            Assert.Equal(ReleasePageUrl, file.SourcePageUrl);
            Assert.Equal(Jpeg, _fileSystem.Files[Path.Combine(SharedFolder, file.FileName)]);
        }

        [Fact]
        public async Task LookedUpCover_DownloadsTheVersionTheUserSaw()
        {
            CoverArtArchiveHasTheCover();
            _http.Respond(CoverUrl, HttpStatusCode.OK, Jpeg);

            await AddCoverAsync();

            // The ~500px thumbnail, not the original, which can be many megabytes
            Assert.True(_http.WasRequested(CoverUrl));
            Assert.False(_http.WasRequested(OriginalCoverUrl));
        }

        [Fact]
        public async Task LookedUpCover_SameAsAnUpload_IsStillShared()
        {
            CoverArtArchiveHasTheCover();
            _http.Respond(CoverUrl, HttpStatusCode.OK, Jpeg);

            var upload = (await UploadAsync(Jpeg)).File!;
            var cover = (await AddCoverAsync()).File!;

            Assert.NotSame(upload, cover);
            Assert.Null(cover.OwnerUserId);
        }

        [Fact]
        public async Task LookedUpCover_ThatCoverArtArchiveDoesntHave_IsNotFound()
        {
            _http.Respond(CoverArtArchiveReleaseUrl, HttpStatusCode.NotFound);

            Assert.Equal(ImageError.NotFound, (await AddCoverAsync()).Error);
            Assert.Empty(_fileSystem.Files);
        }

        [Fact]
        public async Task LookedUpCover_WhenCoverArtArchiveIsDown_IsUnavailable()
        {
            _http.Respond(CoverArtArchiveReleaseUrl, HttpStatusCode.ServiceUnavailable);

            Assert.Equal(ImageError.Unavailable, (await AddCoverAsync()).Error);
        }

        [Theory]
        // The error by name: ImageError is internal, so a public test method can't take one
        [InlineData(HttpStatusCode.ServiceUnavailable, nameof(ImageError.Unavailable))]
        [InlineData(HttpStatusCode.TooManyRequests, nameof(ImageError.Unavailable))]
        [InlineData(HttpStatusCode.NotFound, nameof(ImageError.NotFound))]
        public async Task LookedUpCover_WhenTheDownloadFails_SaysWhy(HttpStatusCode status, string expected)
        {
            CoverArtArchiveHasTheCover();
            _http.Respond(CoverUrl, status);

            Assert.Equal(Enum.Parse<ImageError>(expected), (await AddCoverAsync()).Error);
            Assert.Empty(_fileSystem.Files);
        }

        [Fact]
        public async Task LookedUpCover_ThatIsntAnImage_IsRejected()
        {
            CoverArtArchiveHasTheCover();
            _http.Respond(CoverUrl, HttpStatusCode.OK, "<html>Not found</html>"u8.ToArray());

            Assert.Equal(ImageError.Invalid, (await AddCoverAsync()).Error);
            Assert.Empty(_fileSystem.Files);
        }
        #endregion

        #region Artist photos
        [Fact]
        public async Task ArtistPhoto_IsDownloadedToTheSharedFolderWithItsCreditAndLicense()
        {
            WikimediaHasThePhoto();

            var result = await AddArtistPhotoAsync();

            Assert.Equal(ImageError.None, result.Error);
            var file = result.File!;
            Assert.Null(file.OwnerUserId);
            Assert.Equal(MediaSource.Wikimedia, file.Source);
            Assert.Equal(CommonsFileName, file.Title);
            Assert.Equal(OriginalPhotoUrl, file.OriginalUrl);
            Assert.Equal(PhotoPageUrl, file.SourcePageUrl);
            Assert.Equal("CC BY-SA 4.0", file.License);
            Assert.Equal(LicenseUrl, file.LicenseUrl);
            Assert.True(file.AttributionRequired);
            Assert.Equal(Jpeg, _fileSystem.Files[Path.Combine(SharedFolder, file.FileName)]);
        }

        [Fact]
        public async Task ArtistPhoto_AuthorIsPlainText()
        {
            WikimediaHasThePhoto();

            var file = (await AddArtistPhotoAsync()).File!;

            // Commons gives the author as HTML; only the text is kept, so the frontend can't run any of it
            Assert.Equal("Jane Doe", file.Author);
        }

        [Fact]
        public async Task ArtistPhoto_DownloadsTheVersionTheUserSaw()
        {
            WikimediaHasThePhoto();

            await AddArtistPhotoAsync();

            Assert.True(_http.WasRequested(PhotoUrl));
            Assert.False(_http.WasRequested(OriginalPhotoUrl));
        }

        [Fact]
        public async Task ArtistPhoto_OnAHostWeDontTrust_IsNotDownloaded()
        {
            const string untrustedUrl = "https://evil.example/kiss.jpg";
            MusicBrainzKnowsTheWikidataEntity();
            WikidataNamesTheCommonsFile();
            CommonsHasTheImageInfo(thumbUrl: untrustedUrl);
            _http.Respond(untrustedUrl, HttpStatusCode.OK, Jpeg);

            Assert.Equal(ImageError.NotFound, (await AddArtistPhotoAsync()).Error);
            Assert.False(_http.WasRequested(untrustedUrl));
            Assert.Empty(_fileSystem.Files);
        }

        [Fact]
        public async Task ArtistPhoto_ArtistWithoutAWikidataEntity_IsNotFound()
        {
            _http.Respond(MusicBrainzArtistRelationsUrl, HttpStatusCode.OK, """
                { "relations": [{ "type": "official homepage", "url": { "resource": "https://www.kissonline.com/" } }] }
                """);

            Assert.Equal(ImageError.NotFound, (await AddArtistPhotoAsync()).Error);
            Assert.False(_http.WasRequested(WikidataClaimsUrl));
        }

        [Fact]
        public async Task ArtistPhoto_WikidataEntityWithoutAnImage_IsNotFound()
        {
            MusicBrainzKnowsTheWikidataEntity();
            _http.Respond(WikidataClaimsUrl, HttpStatusCode.OK, """{ "claims": {} }""");

            Assert.Equal(ImageError.NotFound, (await AddArtistPhotoAsync()).Error);
            Assert.False(_http.WasRequested(CommonsImageInfoUrl));
        }

        [Fact]
        public async Task ArtistPhoto_FileMissingOnCommons_IsNotFound()
        {
            MusicBrainzKnowsTheWikidataEntity();
            WikidataNamesTheCommonsFile();
            _http.Respond(CommonsImageInfoUrl, HttpStatusCode.OK, """{ "query": { "pages": [{ "missing": true }] } }""");

            Assert.Equal(ImageError.NotFound, (await AddArtistPhotoAsync()).Error);
            Assert.Empty(_fileSystem.Files);
        }

        [Fact]
        public async Task ArtistPhoto_WhenWikidataIsDown_IsUnavailable()
        {
            MusicBrainzKnowsTheWikidataEntity();
            _http.Respond(WikidataClaimsUrl, HttpStatusCode.ServiceUnavailable);

            Assert.Equal(ImageError.Unavailable, (await AddArtistPhotoAsync()).Error);
        }

        [Fact]
        public async Task ArtistPhoto_WhenCommonsIsDown_IsUnavailable()
        {
            MusicBrainzKnowsTheWikidataEntity();
            WikidataNamesTheCommonsFile();
            _http.Respond(CommonsImageInfoUrl, HttpStatusCode.ServiceUnavailable);

            Assert.Equal(ImageError.Unavailable, (await AddArtistPhotoAsync()).Error);
        }

        [Fact]
        public async Task ArtistPhoto_WhenTheDownloadIsRateLimited_IsUnavailable()
        {
            MusicBrainzKnowsTheWikidataEntity();
            WikidataNamesTheCommonsFile();
            CommonsHasTheImageInfo();
            _http.Respond(PhotoUrl, HttpStatusCode.TooManyRequests);

            Assert.Equal(ImageError.Unavailable, (await AddArtistPhotoAsync()).Error);
            Assert.Empty(_fileSystem.Files);
        }

        [Fact]
        public async Task ArtistPhoto_AfterWikimediaWasDown_IsLookedUpAgain()
        {
            MusicBrainzKnowsTheWikidataEntity();
            _http.Respond(WikidataClaimsUrl, HttpStatusCode.ServiceUnavailable);
            Assert.Equal(ImageError.Unavailable, (await AddArtistPhotoAsync()).Error);

            // The failure mustn't be cached as "no image"
            WikimediaHasThePhoto();
            Assert.Equal(ImageError.None, (await AddArtistPhotoAsync()).Error);
        }

        [Fact]
        public async Task ArtistPhoto_AfterMusicBrainzWasDown_IsLookedUpAgain()
        {
            _http.Respond(MusicBrainzArtistRelationsUrl, HttpStatusCode.ServiceUnavailable);
            await AddArtistPhotoAsync();

            // The failure mustn't be cached as "no Wikidata entity"
            WikimediaHasThePhoto();
            Assert.Equal(ImageError.None, (await AddArtistPhotoAsync()).Error);
        }

        [Fact]
        public async Task ArtistPhoto_WhenMusicBrainzIsDown_IsUnavailable()
        {
            _http.Respond(MusicBrainzArtistRelationsUrl, HttpStatusCode.ServiceUnavailable);

            Assert.Equal(ImageError.Unavailable, (await AddArtistPhotoAsync()).Error);
            Assert.False(_http.WasRequested(WikidataClaimsUrl));
        }
        #endregion

        #region Failures
        [Fact]
        public async Task FailedWrite_Throws_AndAddsNoRow()
        {
            _fileSystem.FailWrites = true;

            await Assert.ThrowsAsync<IOException>(() => UploadAsync(Jpeg));

            Assert.Empty(_db.MediaFiles.Local);
        }

        [Fact]
        public async Task DeleteWrittenFiles_RemovesTheFilesWrittenInTheRequest()
        {
            await UploadAsync(Jpeg);
            await UploadAsync(Png);
            Assert.Equal(2, _fileSystem.Files.Count);

            _service.DeleteWrittenFiles();

            Assert.Empty(_fileSystem.Files);
        }

        [Fact]
        public async Task DeleteWrittenFiles_LeavesReusedFilesAlone()
        {
            await UploadAsync(Jpeg);
            await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

            // A later request reuses the file, then fails to save
            var laterRequest = CreateService();
            await UploadAsync(Jpeg, service: laterRequest);
            laterRequest.DeleteWrittenFiles();

            Assert.Single(_fileSystem.Files);
        }

        [Fact]
        public async Task DeleteWrittenFiles_KeepsGoingWhenADeleteFails()
        {
            await UploadAsync(Jpeg);
            await UploadAsync(Png);
            _fileSystem.FailDeletesOf = _fileSystem.Files.Keys.First();

            _service.DeleteWrittenFiles();

            Assert.Single(_fileSystem.Files);
        }
        #endregion
    }
}
