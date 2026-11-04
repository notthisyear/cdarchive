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
        public async Task NewCreatorIsWrittenForTheUserWithItsExternalIds()
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
        public async Task NewCreatorWithoutExternalIdsHasNone()
        {
            await AddAsync(Request());

            Assert.Empty((await SingleCreatorOfAsync()).ExternalIds);
        }

        [Fact]
        public async Task NewCreatorNameIsTrimmed()
        {
            await AddAsync(Request(name: "  Kiss  "));

            Assert.Equal("Kiss", (await SingleCreatorOfAsync()).Name);
        }

        [Fact]
        public async Task NewCreatorWithTheSameNameAsAnotherIsANewCreator()
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
        public async Task SortNameFromTheRequestIsUsed()
        {
            await AddAsync(Request(name: "The Beatles", sortName: " Beatles, The ", musicBrainzId: ArtistMbid));

            Assert.Equal("Beatles, The", (await SingleCreatorOfAsync()).SortName);
            Assert.False(_harness.Http.WasRequested(MusicBrainzArtistUrl));
        }

        [Fact]
        public async Task SortNameNotInTheRequestComesFromMusicBrainz()
        {
            MusicBrainzKnowsTheArtist(_harness.Http, "The Beatles", "Beatles, The");

            await AddAsync(Request(name: "The Beatles", musicBrainzId: ArtistMbid));

            Assert.Equal("Beatles, The", (await SingleCreatorOfAsync()).SortName);
        }

        [Fact]
        public async Task SortNameWithoutAMusicBrainzIdIsTheName()
        {
            await AddAsync(Request(name: "The Beatles"));

            Assert.Equal("The Beatles", (await SingleCreatorOfAsync()).SortName);
            Assert.Empty(_harness.Http.RequestedUrls);
        }

        [Fact]
        public async Task SortNameWhenMusicBrainzIsDownIsTheName()
        {
            _harness.Http.Respond(MusicBrainzArtistUrl, HttpStatusCode.ServiceUnavailable);

            var result = await AddAsync(Request(name: "The Beatles", musicBrainzId: ArtistMbid));

            // The artist is still added; the sort name just isn't as good
            Assert.Equal(AddCreatorError.None, result.Error);
            Assert.Equal("The Beatles", (await SingleCreatorOfAsync()).SortName);
        }

        [Fact]
        public async Task SortNameAndLookedUpPhotoTakeOneMusicBrainzRequest()
        {
            WikimediaHasThePhoto(_harness.Http);

            await AddAsync(Request(name: "Kiss", musicBrainzId: ArtistMbid, image: LookedUpPhoto));

            Assert.Equal("Kiss", (await SingleCreatorOfAsync()).SortName);
            Assert.Single(_harness.Http.RequestedUrls, new Uri(MusicBrainzArtistUrl).AbsoluteUri);
        }
        #endregion

        #region Other users
        [Fact]
        public async Task SameArtistInAnotherUsersCollectionIsAddedAsTheUsersOwn()
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
        public async Task AnotherUsersCreatorIsNotChanged()
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
        public async Task AlreadyInTheCollectionByMusicBrainzIdIsReportedWithItsId()
        {
            var mine = await SeedCreatorAsync(userId: UserId, musicBrainzId: ArtistMbid);

            var result = await AddAsync(Request(musicBrainzId: ArtistMbid));

            Assert.Equal(AddCreatorError.AlreadyInCollection, result.Error);
            Assert.Equal(mine.Id, result.CreatorId);
            Assert.Single(await CreatorsOfAsync(UserId));
        }

        [Fact]
        public async Task AlreadyInTheCollectionByMusicBrainzIdInCapitalsIsReported()
        {
            var mine = await SeedCreatorAsync(userId: UserId, musicBrainzId: ArtistMbid);

            var result = await AddAsync(Request(musicBrainzId: ArtistMbid.ToUpperInvariant()));

            Assert.Equal(AddCreatorError.AlreadyInCollection, result.Error);
            Assert.Equal(mine.Id, result.CreatorId);
        }

        [Fact]
        public async Task AlreadyInTheCollectionBySpotifyIdIsReported()
        {
            var mine = await SeedCreatorAsync(userId: UserId, spotifyId: SpotifyId);

            var result = await AddAsync(Request(spotifyUrl: SpotifyUrl));

            Assert.Equal(AddCreatorError.AlreadyInCollection, result.Error);
            Assert.Equal(mine.Id, result.CreatorId);
        }

        [Fact]
        public async Task AlreadyInTheCollectionStoresNoImage()
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
        public async Task LookedUpPhotoIsTheirFirstAndPreferredImageAndShared()
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
        public async Task UploadIsTheirFirstAndPreferredImageAndPrivate()
        {
            await AddAsync(Request(image: Upload), ServiceHarness.Files(Jpeg));

            var creator = await SingleCreatorOfAsync();
            var image = Assert.Single(creator.Images);
            Assert.Equal(0, image.SortOrder);
            Assert.Equal(image.MediaFileId, creator.PreferredImageId);
            Assert.Equal(UserId, image.MediaFile.OwnerUserId);
        }

        [Fact]
        public async Task WithoutAnImageHasNoPreferredImage()
        {
            await AddAsync(Request());

            Assert.Null((await SingleCreatorOfAsync()).PreferredImageId);
        }

        [Fact]
        public async Task SamePhotoForTwoUsersIsStoredOnce()
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
        public async Task ImageThatIsntAnImageIsInvalidAndNothingIsWritten()
        {
            var result = await AddAsync(Request(image: Upload), ServiceHarness.Files("not an image"u8.ToArray()));

            Assert.Equal(AddCreatorError.InvalidImage, result.Error);
            await AssertNothingWrittenAsync();
        }

        [Fact]
        public async Task PhotoThatCantBeFoundIsNotFoundAndNothingIsWritten()
        {
            _harness.Http.Respond(MusicBrainzArtistUrl, HttpStatusCode.OK, """{ "relations": [] }""");

            var result = await AddAsync(Request(musicBrainzId: ArtistMbid, image: LookedUpPhoto));

            Assert.Equal(AddCreatorError.ImageNotFound, result.Error);
            await AssertNothingWrittenAsync();
        }

        [Fact]
        public async Task PhotoWhenTheServiceIsDownIsUnavailableAndNothingIsWritten()
        {
            _harness.Http.Respond(MusicBrainzArtistUrl, HttpStatusCode.ServiceUnavailable);

            var result = await AddAsync(Request(musicBrainzId: ArtistMbid, image: LookedUpPhoto));

            Assert.Equal(AddCreatorError.ImageServiceUnavailable, result.Error);
            await AssertNothingWrittenAsync();
        }
        #endregion

        #region Failures
        [Fact]
        public async Task FailedSaveThrowsAndRemovesTheImageItWrote()
        {
            _harness.FailSaves = true;

            await Assert.ThrowsAsync<DbUpdateException>(() => AddAsync(Request(image: Upload), ServiceHarness.Files(Jpeg)));

            Assert.Empty(_harness.FileSystem.Files);
        }
        #endregion

        #region GetCreatorAsync
        [Fact]
        public async Task GetGivesTheCreator()
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
        public async Task GetUploadedImageIsASignedLink()
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
        public async Task GetGivesThePreferredImageEvenIfItIsntTheFirst()
        {
            var first = SharedImage(Jpeg);
            var second = SharedImage(Png);
            var mine = await SeedCreatorAsync(userId: UserId, images: [first, second], preferredImage: second);

            Assert.Equal($"/images/{second.FileName}", (await GetAsync(mine.Id))!.ImageUrl);
        }

        // E.g. when the preferred image's file has been deleted, which sets it to null
        [Fact]
        public async Task GetWithoutAPreferredImageGivesTheFirstBySortOrder()
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
        public async Task GetWithoutAnyImageHasNoImageUrl()
        {
            var mine = await SeedCreatorAsync(userId: UserId);

            Assert.Null((await GetAsync(mine.Id))!.ImageUrl);
        }

        [Fact]
        public async Task GetAnotherUsersCreatorIsNull()
        {
            var theirs = await SeedCreatorAsync(userId: OtherUserId);

            Assert.Null(await GetAsync(theirs.Id));
        }

        [Fact]
        public async Task GetCreatorThatDoesntExistIsNull()
            => Assert.Null(await GetAsync(12345));
        #endregion

        #region RemoveCreatorAsync
        private Task<CreatorService.RemoveCreatorResult> RemoveAsync(long creatorId, long userId = UserId)
            => _harness.NewRequest().Creators.RemoveCreatorAsync(userId, creatorId, CancellationToken.None);

        // A release of the user's crediting the creator, as it would be in the database
        private async Task SeedReleaseCreditingAsync(long creatorId)
        {
            using var db = _harness.CreateDbContext();
            var release = new Release { UserId = UserId, Title = "Destroyer", Fingerprint = "fingerprint" };
            release.Credits.Add(new() { CreatorId = creatorId, Role = CreditRole.Primary });
            db.Releases.Add(release);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task RemovedCreatorIsGoneWithTheirIdsAndImages()
        {
            var photo = SharedImage(Jpeg);
            var kiss = await SeedCreatorAsync(userId: UserId, musicBrainzId: ArtistMbid, spotifyId: SpotifyId, images: [photo], preferredImage: photo);

            Assert.Equal(CreatorService.RemoveCreatorResult.Removed, await RemoveAsync(kiss.Id));

            using var db = _harness.CreateDbContext();
            var ct = TestContext.Current.CancellationToken;
            Assert.Empty(await db.Creators.ToListAsync(ct));
            Assert.Empty(await db.Set<CreatorExternalId>().ToListAsync(ct));
            Assert.Empty(await db.Set<CreatorImage>().ToListAsync(ct));

            // A shared file may be anyone's
            Assert.Single(await db.MediaFiles.ToListAsync(ct));
        }

        [Fact]
        public async Task CreatorOnAReleaseIsntRemoved()
        {
            var kiss = await SeedCreatorAsync(userId: UserId);
            await SeedReleaseCreditingAsync(kiss.Id);

            Assert.Equal(CreatorService.RemoveCreatorResult.HasReleases, await RemoveAsync(kiss.Id));
            Assert.Single(await _harness.CreateDbContext().Creators.ToListAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task AnotherUsersCreatorIsntRemoved()
        {
            var theirs = await SeedCreatorAsync(userId: OtherUserId);

            Assert.Equal(CreatorService.RemoveCreatorResult.NotFound, await RemoveAsync(theirs.Id, userId: UserId));
            Assert.Single(await _harness.CreateDbContext().Creators.ToListAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task RemovingACreatorThatDoesntExistIsNotFound()
            => Assert.Equal(CreatorService.RemoveCreatorResult.NotFound, await RemoveAsync(12345));
        #endregion

        #region FindMatchAsync
        private const string OtherArtistMbid = "5b11f4ce-a62d-471e-81fc-a69a8278c7da";

        private Task<long?> FindMatchAsync(string name, string? musicBrainzId = null, long userId = UserId)
            => _harness.NewRequest().Creators.FindMatchAsync(userId, name, musicBrainzId, CancellationToken.None);

        [Fact]
        public async Task CreatorWithTheNameIsAMatch()
        {
            var creator = await SeedCreatorAsync(userId: UserId, name: "Low");

            Assert.Equal(creator.Id, await FindMatchAsync("Low"));
        }

        [Fact]
        public async Task NameIsMatchedIgnoringCaseAndSurroundingSpaces()
        {
            var creator = await SeedCreatorAsync(userId: UserId, name: "ABBA");

            Assert.Equal(creator.Id, await FindMatchAsync("  Abba "));
        }

        [Fact]
        public async Task CreatorWithTheNameAsPartOfTheirsIsNoMatch()
        {
            // The only hit when searching for "Low", but another band
            await SeedCreatorAsync(userId: UserId, name: "Low Roar");

            Assert.Null(await FindMatchAsync("Low"));
        }

        [Fact]
        public async Task TwoCreatorsWithTheNameAreNoMatch()
        {
            await SeedCreatorAsync(userId: UserId, name: "Kiss");
            await SeedCreatorAsync(userId: UserId, name: "Kiss");

            Assert.Null(await FindMatchAsync("Kiss"));
        }

        [Fact]
        public async Task OtherUsersCreatorIsNoMatch()
        {
            await SeedCreatorAsync(userId: OtherUserId, name: "Kiss", musicBrainzId: ArtistMbid);

            Assert.Null(await FindMatchAsync("Kiss", ArtistMbid));
        }

        [Fact]
        public async Task CreatorLinkedToTheCreditedArtistIsAMatchUnderAnotherName()
        {
            // Credited as on the release rather than by the name the user has for them
            var creator = await SeedCreatorAsync(userId: UserId, name: "2Pac", musicBrainzId: ArtistMbid);

            Assert.Equal(creator.Id, await FindMatchAsync("Tupac", ArtistMbid.ToUpperInvariant()));
        }

        [Fact]
        public async Task CreatorLinkedToTheCreditedArtistWinsOverOthersWithTheName()
        {
            await SeedCreatorAsync(userId: UserId, name: "Low");
            var linked = await SeedCreatorAsync(userId: UserId, name: "Low", musicBrainzId: ArtistMbid);

            Assert.Equal(linked.Id, await FindMatchAsync("Low", ArtistMbid));
        }

        [Fact]
        public async Task CreatorLinkedToAnotherArtistIsNoMatchForTheCreditedOne()
        {
            // Another band called Low
            await SeedCreatorAsync(userId: UserId, name: "Low", musicBrainzId: OtherArtistMbid);

            Assert.Null(await FindMatchAsync("Low", ArtistMbid));
        }

        [Fact]
        public async Task UnlinkedCreatorWithTheNameIsAMatchForACreditedArtist()
        {
            var creator = await SeedCreatorAsync(userId: UserId, name: "Low");

            Assert.Equal(creator.Id, await FindMatchAsync("Low", ArtistMbid));
        }

        [Fact]
        public async Task LinkedCreatorIsAMatchForTheNameAlone()
        {
            // Typed by hand, so there's no artist to tell them apart by
            var creator = await SeedCreatorAsync(userId: UserId, name: "Low", musicBrainzId: OtherArtistMbid);

            Assert.Equal(creator.Id, await FindMatchAsync("Low"));
        }
        #endregion
    }
}
