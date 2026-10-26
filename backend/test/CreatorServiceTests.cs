using CdArchiveBackend;
using CdArchiveBackend.Data.Database;
using CdArchiveBackend.Data.DTO;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static CdArchiveTests.ExternalServiceStubs;

namespace CdArchiveTests
{
    using AddCreatorError = CreatorService.AddCreatorError;

    // CreatorService against the in-memory database. It doesn't enforce unique indexes or foreign
    // keys, so these tests check what the service writes, not what the database would refuse.
    // SearchCreatorsAsync uses ILike, which only Postgres has, so it isn't tested here.
    public sealed class CreatorServiceTests : IDisposable
    {
        private const long UserId = 7;
        private const long OtherUserId = 8;
        private const string SpotifyId = "07XSN3sPlIlB2L2XNcTwJw";
        private const string SpotifyUrl = $"https://open.spotify.com/artist/{SpotifyId}?si=abc";

        private readonly ServiceHarness _harness = new();

        public void Dispose() => _harness.Dispose();

        private static NewCreatorRequest Request(string name = "Kiss",
                                                 string? sortName = null,
                                                 CreatorType type = CreatorType.Group,
                                                 string? country = "us",
                                                 string? musicBrainzId = null,
                                                 string? spotifyUrl = null,
                                                 NewImage? image = null)
            => new(name, sortName, type, country, musicBrainzId, spotifyUrl, image);

        private static readonly NewImage LookedUpPhoto = new(MediaSource.Wikimedia, ArtistMbid, null);
        private static readonly NewImage Upload = new(MediaSource.Upload, null, 0);

        // One request, as the endpoint would make it
        private Task<CreatorService.AddCreatorResult> AddAsync(NewCreatorRequest request, IFormFileCollection? files = null, long userId = UserId)
            => _harness.NewRequest().Creators.AddCreatorAsync(userId, request, files ?? ServiceHarness.Files(), CancellationToken.None);

        private Task<CreatorSummary?> GetAsync(long creatorId, long userId = UserId)
            => _harness.NewRequest().Creators.GetCreatorAsync(userId, creatorId, CancellationToken.None);

        // A creator already in someone's collection, as it would be in the database
        private async Task<Creator> SeedCreatorAsync(long userId = OtherUserId,
                                                     string name = "Kiss",
                                                     string? musicBrainzId = null,
                                                     string? spotifyId = null,
                                                     MediaFile[]? images = null,
                                                     MediaFile? preferredImage = null)
        {
            using var db = _harness.CreateDbContext();
            var creator = new Creator
            {
                UserId = userId,
                Name = name,
                SortName = name,
                Type = CreatorType.Group,
                Country = "US"
            };

            if (musicBrainzId is not null)
                creator.ExternalIds.Add(new() { Source = ExternalSource.MusicBrainz, ExternalId = musicBrainzId });
            if (spotifyId is not null)
                creator.ExternalIds.Add(new() { Source = ExternalSource.Spotify, ExternalId = spotifyId });

            // In the order given
            for (var i = 0; i < (images?.Length ?? 0); i++)
                creator.Images.Add(new() { MediaFile = images![i], SortOrder = i });

            creator.PreferredImage = preferredImage;
            db.Creators.Add(creator);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return creator;
        }

        private static MediaFile SharedImage(byte[] content) => new()
        {
            FileName = $"{Guid.NewGuid()}.jpg",
            ContentType = "image/jpeg",
            ByteCount = content.Length,
            Sha1 = Convert.ToHexStringLower(SHA1.HashData(content)),
            Source = MediaSource.Wikimedia
        };

        // What's in the database now, read outside of any request
        private DatabaseContext Database() => _harness.CreateDbContext();

        private async Task<List<Creator>> CreatorsOfAsync(long userId)
            => await Database().Creators
                .Include(x => x.ExternalIds)
                .Include(x => x.Images.OrderBy(i => i.SortOrder)).ThenInclude(x => x.MediaFile)
                .Where(x => x.UserId == userId)
                .ToListAsync(TestContext.Current.CancellationToken);

        private async Task<Creator> SingleCreatorOfAsync(long userId = UserId)
            => Assert.Single(await CreatorsOfAsync(userId));

        private async Task AssertNothingWrittenAsync()
        {
            using var db = Database();
            Assert.Empty(await db.Creators.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Empty(await db.MediaFiles.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Empty(_harness.FileSystem.Files);
        }

        #region New creators
        [Fact]
        public async Task NewCreator_IsWrittenForTheUserWithItsExternalIds()
        {
            var result = await AddAsync(Request(
                name: "Kiss",
                sortName: "Kiss",
                type: CreatorType.Group,
                country: "us",
                musicBrainzId: ArtistMbid.ToUpperInvariant(),
                spotifyUrl: SpotifyUrl));

            Assert.Equal(AddCreatorError.None, result.Error);

            var creator = await SingleCreatorOfAsync(UserId);
            Assert.Equal(result.CreatorId, creator.Id);
            Assert.Equal("Kiss", creator.Name);
            Assert.Equal(CreatorType.Group, creator.Type);
            Assert.Equal("US", creator.Country);
            Assert.Empty(creator.Images);

            // The MBID lower-cased, as MusicBrainz writes them; the Spotify id out of the URL
            Assert.Equal(2, creator.ExternalIds.Count);
            Assert.Equal(ArtistMbid, creator.ExternalIds.Single(x => x.Source == ExternalSource.MusicBrainz).ExternalId);
            Assert.Equal(SpotifyId, creator.ExternalIds.Single(x => x.Source == ExternalSource.Spotify).ExternalId);
        }

        [Fact]
        public async Task NewCreator_WithoutExternalIds_HasNone()
        {
            await AddAsync(Request());

            Assert.Empty((await SingleCreatorOfAsync()).ExternalIds);
        }

        [Fact]
        public async Task NewCreator_NameIsTrimmed()
        {
            await AddAsync(Request(name: "  Kiss  "));

            Assert.Equal("Kiss", (await SingleCreatorOfAsync()).Name);
        }

        [Fact]
        public async Task NewCreator_WithTheSameNameAsAnother_IsANewCreator()
        {
            // Without an id in common, a name says nothing: there are several bands called Kiss
            var other = await SeedCreatorAsync(userId: UserId, name: "Kiss");

            var result = await AddAsync(Request(name: "Kiss"));

            Assert.Equal(AddCreatorError.None, result.Error);
            Assert.NotEqual(other.Id, result.CreatorId);
            Assert.Equal(2, (await CreatorsOfAsync(UserId)).Count);
        }
        #endregion

        #region Sort names
        [Fact]
        public async Task SortName_FromTheRequest_IsUsed()
        {
            await AddAsync(Request(name: "The Beatles", sortName: " Beatles, The ", musicBrainzId: ArtistMbid));

            Assert.Equal("Beatles, The", (await SingleCreatorOfAsync()).SortName);
            Assert.False(_harness.Http.WasRequested(MusicBrainzArtistUrl));
        }

        [Fact]
        public async Task SortName_NotInTheRequest_ComesFromMusicBrainz()
        {
            MusicBrainzKnowsTheArtist(_harness.Http, "The Beatles", "Beatles, The");

            await AddAsync(Request(name: "The Beatles", musicBrainzId: ArtistMbid));

            Assert.Equal("Beatles, The", (await SingleCreatorOfAsync()).SortName);
        }

        [Fact]
        public async Task SortName_WithoutAMusicBrainzId_IsTheName()
        {
            await AddAsync(Request(name: "The Beatles"));

            Assert.Equal("The Beatles", (await SingleCreatorOfAsync()).SortName);
            Assert.Empty(_harness.Http.RequestedUrls);
        }

        [Fact]
        public async Task SortName_WhenMusicBrainzIsDown_IsTheName()
        {
            _harness.Http.Respond(MusicBrainzArtistUrl, HttpStatusCode.ServiceUnavailable);

            var result = await AddAsync(Request(name: "The Beatles", musicBrainzId: ArtistMbid));

            // The artist is still added; the sort name just isn't as good
            Assert.Equal(AddCreatorError.None, result.Error);
            Assert.Equal("The Beatles", (await SingleCreatorOfAsync()).SortName);
        }
        #endregion

        #region Other users
        [Fact]
        public async Task SameArtistInAnotherUsersCollection_IsAddedAsTheUsersOwn()
        {
            var theirs = await SeedCreatorAsync(userId: OtherUserId, musicBrainzId: ArtistMbid, spotifyId: SpotifyId);

            var result = await AddAsync(Request(musicBrainzId: ArtistMbid, spotifyUrl: SpotifyUrl));

            Assert.Equal(AddCreatorError.None, result.Error);
            Assert.NotEqual(theirs.Id, result.CreatorId);

            // Both have the ids, as nothing makes them unique across users
            var mine = await SingleCreatorOfAsync(UserId);
            Assert.Equal(2, mine.ExternalIds.Count);
            Assert.Equal(2, (await SingleCreatorOfAsync(OtherUserId)).ExternalIds.Count);
        }

        [Fact]
        public async Task AnotherUsersCreator_IsNotChanged()
        {
            await SeedCreatorAsync(userId: OtherUserId, name: "KISS", musicBrainzId: ArtistMbid);

            await AddAsync(Request(name: "Kiss", type: CreatorType.Person, country: "se", musicBrainzId: ArtistMbid));

            var theirs = await SingleCreatorOfAsync(OtherUserId);
            Assert.Equal("KISS", theirs.Name);
            Assert.Equal(CreatorType.Group, theirs.Type);
            Assert.Equal("US", theirs.Country);
        }
        #endregion

        #region Already in the collection
        [Fact]
        public async Task AlreadyInTheCollection_ByMusicBrainzId_IsReportedWithItsId()
        {
            var mine = await SeedCreatorAsync(userId: UserId, musicBrainzId: ArtistMbid);

            var result = await AddAsync(Request(musicBrainzId: ArtistMbid));

            Assert.Equal(AddCreatorError.AlreadyInCollection, result.Error);
            Assert.Equal(mine.Id, result.CreatorId);
            Assert.Single(await CreatorsOfAsync(UserId));
        }

        [Fact]
        public async Task AlreadyInTheCollection_ByMusicBrainzIdInCapitals_IsReported()
        {
            var mine = await SeedCreatorAsync(userId: UserId, musicBrainzId: ArtistMbid);

            var result = await AddAsync(Request(musicBrainzId: ArtistMbid.ToUpperInvariant()));

            Assert.Equal(AddCreatorError.AlreadyInCollection, result.Error);
            Assert.Equal(mine.Id, result.CreatorId);
        }

        [Fact]
        public async Task AlreadyInTheCollection_BySpotifyId_IsReported()
        {
            var mine = await SeedCreatorAsync(userId: UserId, spotifyId: SpotifyId);

            var result = await AddAsync(Request(spotifyUrl: SpotifyUrl));

            Assert.Equal(AddCreatorError.AlreadyInCollection, result.Error);
            Assert.Equal(mine.Id, result.CreatorId);
        }

        [Fact]
        public async Task AlreadyInTheCollection_StoresNoImage()
        {
            await SeedCreatorAsync(userId: UserId, musicBrainzId: ArtistMbid);
            WikimediaHasThePhoto(_harness.Http);

            await AddAsync(Request(musicBrainzId: ArtistMbid, image: LookedUpPhoto));

            Assert.Empty(await Database().MediaFiles.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Empty(_harness.FileSystem.Files);
            Assert.False(_harness.Http.WasRequested(PhotoUrl));
        }
        #endregion

        #region Images
        [Fact]
        public async Task LookedUpPhoto_IsTheirFirstAndPreferredImage_AndShared()
        {
            WikimediaHasThePhoto(_harness.Http);

            await AddAsync(Request(musicBrainzId: ArtistMbid, image: LookedUpPhoto));

            var creator = await SingleCreatorOfAsync();
            var image = Assert.Single(creator.Images);
            Assert.Equal(0, image.SortOrder);
            Assert.Equal(image.MediaFileId, creator.PreferredImageId);
            Assert.Null(image.MediaFile.OwnerUserId);
        }

        [Fact]
        public async Task Upload_IsTheirFirstAndPreferredImage_AndPrivate()
        {
            await AddAsync(Request(image: Upload), ServiceHarness.Files(Jpeg));

            var creator = await SingleCreatorOfAsync();
            var image = Assert.Single(creator.Images);
            Assert.Equal(0, image.SortOrder);
            Assert.Equal(image.MediaFileId, creator.PreferredImageId);
            Assert.Equal(UserId, image.MediaFile.OwnerUserId);
        }

        [Fact]
        public async Task WithoutAnImage_HasNoPreferredImage()
        {
            await AddAsync(Request());

            Assert.Null((await SingleCreatorOfAsync()).PreferredImageId);
        }

        [Fact]
        public async Task SamePhotoForTwoUsers_IsStoredOnce()
        {
            WikimediaHasThePhoto(_harness.Http);

            await AddAsync(Request(musicBrainzId: ArtistMbid, image: LookedUpPhoto), userId: OtherUserId);
            await AddAsync(Request(musicBrainzId: ArtistMbid, image: LookedUpPhoto), userId: UserId);

            // Each has their own creator, but the photo is one shared file
            var mine = Assert.Single((await SingleCreatorOfAsync(UserId)).Images);
            var theirs = Assert.Single((await SingleCreatorOfAsync(OtherUserId)).Images);
            Assert.Equal(theirs.MediaFileId, mine.MediaFileId);
            Assert.Single(await Database().MediaFiles.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Single(_harness.FileSystem.Files);
        }

        [Fact]
        public async Task ImageThatIsntAnImage_IsInvalid_AndNothingIsWritten()
        {
            var result = await AddAsync(Request(image: Upload), ServiceHarness.Files("not an image"u8.ToArray()));

            Assert.Equal(AddCreatorError.InvalidImage, result.Error);
            await AssertNothingWrittenAsync();
        }

        [Fact]
        public async Task PhotoThatCantBeFound_IsNotFound_AndNothingIsWritten()
        {
            _harness.Http.Respond(MusicBrainzArtistRelationsUrl, HttpStatusCode.OK, """{ "relations": [] }""");

            var result = await AddAsync(Request(musicBrainzId: ArtistMbid, image: LookedUpPhoto));

            Assert.Equal(AddCreatorError.ImageNotFound, result.Error);
            await AssertNothingWrittenAsync();
        }

        [Fact]
        public async Task PhotoWhenTheServiceIsDown_IsUnavailable_AndNothingIsWritten()
        {
            _harness.Http.Respond(MusicBrainzArtistRelationsUrl, HttpStatusCode.ServiceUnavailable);

            var result = await AddAsync(Request(musicBrainzId: ArtistMbid, image: LookedUpPhoto));

            Assert.Equal(AddCreatorError.ImageServiceUnavailable, result.Error);
            await AssertNothingWrittenAsync();
        }
        #endregion

        #region Failures
        [Fact]
        public async Task FailedSave_Throws_AndRemovesTheImageItWrote()
        {
            _harness.FailSaves = true;

            await Assert.ThrowsAsync<DbUpdateException>(() => AddAsync(Request(image: Upload), ServiceHarness.Files(Jpeg)));

            Assert.Empty(_harness.FileSystem.Files);
        }
        #endregion

        #region GetCreatorAsync
        [Fact]
        public async Task Get_GivesTheCreator()
        {
            var image = SharedImage(Jpeg);
            var mine = await SeedCreatorAsync(userId: UserId, name: "Kiss", musicBrainzId: ArtistMbid, images: [image]);

            var summary = await GetAsync(mine.Id);

            Assert.NotNull(summary);
            Assert.Equal(mine.Id, summary.Id);
            Assert.Equal("Kiss", summary.Name);
            Assert.Equal(CreatorType.Group, summary.Type);
            Assert.Equal("US", summary.Country);
            Assert.Equal(ArtistMbid, summary.MusicBrainzId);
            Assert.Equal($"/images/{image.FileName}", summary.ImageUrl);
        }

        [Fact]
        public async Task Get_UploadedImage_IsASignedLink()
        {
            var upload = new MediaFile
            {
                OwnerUserId = UserId,
                FileName = "mine.jpg",
                ContentType = "image/jpeg",
                Sha1 = "a",
                Source = MediaSource.Upload
            };
            var mine = await SeedCreatorAsync(userId: UserId, images: [upload]);

            Assert.StartsWith($"/api/uploads/{upload.Id}?", (await GetAsync(mine.Id))!.ImageUrl);
        }

        [Fact]
        public async Task Get_GivesThePreferredImage_EvenIfItIsntTheFirst()
        {
            var first = SharedImage(Jpeg);
            var second = SharedImage(Png);
            var mine = await SeedCreatorAsync(userId: UserId, images: [first, second], preferredImage: second);

            Assert.Equal($"/images/{second.FileName}", (await GetAsync(mine.Id))!.ImageUrl);
        }

        // E.g. when the preferred image's file has been deleted, which sets it to null
        [Fact]
        public async Task Get_WithoutAPreferredImage_GivesTheFirstBySortOrder()
        {
            var first = SharedImage(Jpeg);
            var second = SharedImage(Png);
            var mine = await SeedCreatorAsync(userId: UserId, images: [first, second]);

            // Swap them, so the first in SortOrder isn't the first in the table
            using (var db = Database())
            {
                var images = await db.CreatorImages.Where(x => x.CreatorId == mine.Id).ToListAsync(TestContext.Current.CancellationToken);
                images.Single(x => x.MediaFileId == first.Id).SortOrder = 1;
                images.Single(x => x.MediaFileId == second.Id).SortOrder = 0;
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            Assert.Equal($"/images/{second.FileName}", (await GetAsync(mine.Id))!.ImageUrl);
        }

        [Fact]
        public async Task Get_WithoutAnyImage_HasNoImageUrl()
        {
            var mine = await SeedCreatorAsync(userId: UserId);

            Assert.Null((await GetAsync(mine.Id))!.ImageUrl);
        }

        [Fact]
        public async Task Get_AnotherUsersCreator_IsNull()
        {
            var theirs = await SeedCreatorAsync(userId: OtherUserId);

            Assert.Null(await GetAsync(theirs.Id));
        }

        [Fact]
        public async Task Get_CreatorThatDoesntExist_IsNull()
            => Assert.Null(await GetAsync(12345));
        #endregion
    }
}
