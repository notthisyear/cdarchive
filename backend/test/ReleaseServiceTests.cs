using CdArchiveBackend;
using CdArchiveBackend.Data.Database;
using CdArchiveBackend.Data.DTO;
using CdArchiveBackend.Services;
using CdArchiveBackend.Services.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static CdArchiveTests.ExternalServiceStubs;

namespace CdArchiveTests
{
    using AddReleaseError = ReleaseService.AddReleaseError;

    // ReleaseService against the in-memory database. It doesn't enforce unique indexes, foreign
    // keys or check constraints, so these tests check what the service writes.
    public sealed class ReleaseServiceTests : IDisposable
    {
        private const long UserId = 7;
        private const long OtherUserId = 8;
        private const string ReleaseGroupMbid = "541eb339-d0c6-3740-b665-8ad6c09da331";

        private readonly ServiceHarness _harness = new();

        public void Dispose() => _harness.Dispose();

        private static readonly NewImage Cover = new(MediaSource.CoverArtArchive, ReleaseMbid, null);

        private static NewImage Upload(int fileIndex) => new(MediaSource.Upload, null, fileIndex);

        private static NewReleaseRequest Request(IReadOnlyList<NewReleaseCredit> credits,
                                                 IReadOnlyList<ReleaseTrack>? tracks = null,
                                                 IReadOnlyList<NewImage>? images = null,
                                                 string title = "Destroyer",
                                                 string? musicBrainzReleaseId = null,
                                                 string? musicBrainzReleaseGroupId = null,
                                                 string? spotifyAlbumId = null,
                                                 string? barcode = null,
                                                 int? score = null)
            => new(
                Title: title,
                Type: ReleaseType.Record,
                Status: ReleaseStatus.Owned,
                Format: ReleaseFormat.Cd,
                ReleaseYear: 1997,
                FirstReleaseYear: 1976,
                DurationSeconds: 2060,
                Barcode: barcode,
                MusicBrainzReleaseId: musicBrainzReleaseId,
                MusicBrainzReleaseGroupId: musicBrainzReleaseGroupId,
                SpotifyAlbumId: spotifyAlbumId,
                Notes: null,
                Score: score,
                AcquiredOn: null,
                Credits: credits,
                Tracks: tracks ?? [],
                Images: images ?? []);

        private static NewReleaseCredit Primary(long creatorId, string joinPhrase = "") => new(creatorId, CreditRole.Primary, joinPhrase, null);

        // One request, as the endpoint would make it
        private Task<ReleaseService.AddReleaseResult> AddAsync(NewReleaseRequest request, IFormFileCollection? files = null, long userId = UserId)
            => _harness.NewRequest().Releases.AddReleaseAsync(userId, request, files ?? ServiceHarness.Files(), CancellationToken.None);

        private async Task<long> SeedCreatorAsync(long userId = UserId, string name = "Kiss")
        {
            using var db = _harness.CreateDbContext();
            var creator = new Creator { UserId = userId, Name = name, SortName = name, Type = CreatorType.Group };
            db.Creators.Add(creator);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return creator.Id;
        }

        private async Task<Release> SingleReleaseAsync()
            => await _harness.CreateDbContext().Releases
                .Include(x => x.Credits)
                .Include(x => x.Tracks)
                .Include(x => x.ExternalIds)
                .Include(x => x.Images).ThenInclude(x => x.MediaFile)
                .SingleAsync(TestContext.Current.CancellationToken);

        private async Task AssertNothingWrittenAsync()
        {
            using var db = _harness.CreateDbContext();
            Assert.Empty(await db.Releases.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Empty(await db.MediaFiles.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Empty(_harness.FileSystem.Files);
        }

        #region The release itself
        [Fact]
        public async Task NewRelease_IsWrittenForTheUser()
        {
            var kiss = await SeedCreatorAsync();

            var result = await AddAsync(Request(
                credits: [Primary(kiss)],
                title: "  Destroyer  ",
                barcode: " 731453238428 ",
                score: 8));

            Assert.Equal(AddReleaseError.None, result.Error);

            var release = await SingleReleaseAsync();
            Assert.Equal(result.ReleaseId, release.Id);
            Assert.Equal(UserId, release.UserId);
            Assert.Equal("Destroyer", release.Title);
            Assert.Equal(ReleaseType.Record, release.Type);
            Assert.Equal(ReleaseStatus.Owned, release.Status);
            Assert.Equal(ReleaseFormat.Cd, release.Format);
            Assert.Equal(1997, release.ReleaseYear);
            Assert.Equal(1976, release.FirstReleaseYear);
            Assert.Equal(2060, release.DurationSeconds);
            Assert.Equal("731453238428", release.Barcode);
            Assert.Equal(8, release.Score);
        }

        [Fact]
        public async Task EmptyBarcode_IsStoredAsNone()
        {
            var kiss = await SeedCreatorAsync();

            await AddAsync(Request(credits: [Primary(kiss)], barcode: "   "));

            Assert.Null((await SingleReleaseAsync()).Barcode);
        }

        [Fact]
        public async Task MusicBrainzIds_AreStoredLowerCased()
        {
            var kiss = await SeedCreatorAsync();

            await AddAsync(Request(
                credits: [Primary(kiss)],
                musicBrainzReleaseId: ReleaseMbid.ToUpperInvariant(),
                musicBrainzReleaseGroupId: ReleaseGroupMbid.ToUpperInvariant()));

            var ids = (await SingleReleaseAsync()).ExternalIds;
            Assert.Equal(2, ids.Count);
            Assert.Equal(ReleaseMbid, ids.Single(x => x.Source == ExternalSource.MusicBrainz).ExternalId);
            Assert.Equal(ReleaseGroupMbid, ids.Single(x => x.Source == ExternalSource.MusicBrainzReleaseGroup).ExternalId);
        }

        [Fact]
        public async Task SpotifyAlbumId_IsStoredAsGiven()
        {
            var kiss = await SeedCreatorAsync();

            await AddAsync(Request(credits: [Primary(kiss)], spotifyAlbumId: "0YQ5ZsSIbzeRwFMBiQh0eg"));

            // Spotify's ids are case-sensitive
            var id = Assert.Single((await SingleReleaseAsync()).ExternalIds);
            Assert.Equal(ExternalSource.Spotify, id.Source);
            Assert.Equal("0YQ5ZsSIbzeRwFMBiQh0eg", id.ExternalId);
        }

        [Fact]
        public async Task Details_IncludeTheSpotifyAlbumId()
        {
            var kiss = await SeedCreatorAsync();
            var added = await AddAsync(Request(credits: [Primary(kiss)], spotifyAlbumId: "0YQ5ZsSIbzeRwFMBiQh0eg"));

            var details = await _harness.NewRequest().Releases.GetReleaseAsync(UserId, added.ReleaseId, CancellationToken.None);

            Assert.Equal("0YQ5ZsSIbzeRwFMBiQh0eg", details!.ExternalIds[ExternalSource.Spotify]);
        }

        [Fact]
        public async Task WithoutMusicBrainzIds_HasNoExternalIds()
        {
            var kiss = await SeedCreatorAsync();

            await AddAsync(Request(credits: [Primary(kiss)]));

            Assert.Empty((await SingleReleaseAsync()).ExternalIds);
        }
        #endregion

        #region Duplicates
        private async Task<int> ReleaseCountAsync()
            => await _harness.CreateDbContext().Releases.CountAsync(TestContext.Current.CancellationToken);

        [Fact]
        public async Task NewRelease_StoresItsFingerprint()
        {
            var kiss = await SeedCreatorAsync();
            var request = Request(credits: [Primary(kiss)]);

            await AddAsync(request);

            Assert.Equal(ReleaseFingerprint.Compute(request), (await SingleReleaseAsync()).Fingerprint);
        }

        [Fact]
        public async Task SameMusicBrainzRelease_IsAlreadyInTheCollection()
        {
            var kiss = await SeedCreatorAsync();
            var first = await AddAsync(Request(credits: [Primary(kiss)], musicBrainzReleaseId: ReleaseMbid));

            // Even written differently and with other details: the id says it's the same release
            var result = await AddAsync(Request(credits: [Primary(kiss)], title: "Destroyer (Remastered)", musicBrainzReleaseId: ReleaseMbid.ToUpperInvariant()));

            Assert.Equal(AddReleaseError.AlreadyInCollection, result.Error);
            Assert.Equal(first.ReleaseId, result.ReleaseId);
            Assert.Equal(1, await ReleaseCountAsync());
        }

        [Fact]
        public async Task OtherMusicBrainzRelease_ThatLooksTheSame_IsAdded()
        {
            // E.g. two pressings without barcodes, which nothing else here tells apart
            var kiss = await SeedCreatorAsync();
            await AddAsync(Request(credits: [Primary(kiss)], musicBrainzReleaseId: ReleaseMbid));

            var result = await AddAsync(Request(credits: [Primary(kiss)], musicBrainzReleaseId: "0d7d2b4e-6a7c-4c1e-9b0a-2f1f3c5d7e9a"));

            Assert.Equal(AddReleaseError.None, result.Error);
            Assert.Equal(2, await ReleaseCountAsync());
        }

        [Fact]
        public async Task WithoutMusicBrainzId_SameContent_IsAlreadyInTheCollection()
        {
            var kiss = await SeedCreatorAsync();
            var first = await AddAsync(Request(credits: [Primary(kiss)], tracks: [new(1, 1, "Detroit Rock City", 318)]));

            var result = await AddAsync(Request(credits: [Primary(kiss)], title: " destroyer ", tracks: [new(1, 1, "Detroit Rock City", 318)]));

            Assert.Equal(AddReleaseError.AlreadyInCollection, result.Error);
            Assert.Equal(first.ReleaseId, result.ReleaseId);
            Assert.Equal(1, await ReleaseCountAsync());
        }

        [Fact]
        public async Task WithoutMusicBrainzId_SameContentAsOneWithAnId_IsAlreadyInTheCollection()
        {
            var kiss = await SeedCreatorAsync();
            var first = await AddAsync(Request(credits: [Primary(kiss)], musicBrainzReleaseId: ReleaseMbid));

            var result = await AddAsync(Request(credits: [Primary(kiss)]));

            Assert.Equal(AddReleaseError.AlreadyInCollection, result.Error);
            Assert.Equal(first.ReleaseId, result.ReleaseId);
        }

        [Fact]
        public async Task WithoutMusicBrainzId_DifferentContent_IsAdded()
        {
            var kiss = await SeedCreatorAsync();
            await AddAsync(Request(credits: [Primary(kiss)], tracks: [new(1, 1, "Detroit Rock City", 318)]));

            var result = await AddAsync(Request(credits: [Primary(kiss)], tracks: [new(1, 1, "Detroit Rock City", 319)]));

            Assert.Equal(AddReleaseError.None, result.Error);
            Assert.Equal(2, await ReleaseCountAsync());
        }

        [Fact]
        public async Task SameReleaseInAnotherUsersCollection_IsAdded()
        {
            var mine = await SeedCreatorAsync(userId: UserId);
            var theirs = await SeedCreatorAsync(userId: OtherUserId);
            await AddAsync(Request(credits: [Primary(theirs)], musicBrainzReleaseId: ReleaseMbid), userId: OtherUserId);

            var result = await AddAsync(Request(credits: [Primary(mine)], musicBrainzReleaseId: ReleaseMbid), userId: UserId);

            Assert.Equal(AddReleaseError.None, result.Error);
            Assert.Equal(2, await ReleaseCountAsync());
        }

        [Fact]
        public async Task AlreadyInTheCollection_StoresNoImage()
        {
            var kiss = await SeedCreatorAsync();
            await AddAsync(Request(credits: [Primary(kiss)], musicBrainzReleaseId: ReleaseMbid));
            CoverArtArchiveHasTheCover(_harness.Http);
            _harness.Http.Respond(CoverUrl, HttpStatusCode.OK, Jpeg);

            await AddAsync(Request(credits: [Primary(kiss)], musicBrainzReleaseId: ReleaseMbid, images: [Cover]));

            Assert.False(_harness.Http.WasRequested(CoverUrl));
            Assert.Empty(_harness.FileSystem.Files);
        }
        #endregion

        #region Credits
        [Fact]
        public async Task Credits_AreStoredInBillingOrder_WithTheirJoinPhrases()
        {
            var simon = await SeedCreatorAsync(name: "Simon");
            var garfunkel = await SeedCreatorAsync(name: "Garfunkel");

            await AddAsync(Request(credits: [Primary(simon, " & "), Primary(garfunkel)]));

            var credits = (await SingleReleaseAsync()).Credits.OrderBy(x => x.SortOrder).ToList();
            Assert.Equal(2, credits.Count);
            Assert.Equal((simon, 0, " & "), (credits[0].CreatorId, credits[0].SortOrder!.Value, credits[0].JoinPhrase));
            Assert.Equal((garfunkel, 1, ""), (credits[1].CreatorId, credits[1].SortOrder!.Value, credits[1].JoinPhrase));
        }

        [Fact]
        public async Task OneCreatorInTwoRoles_IsTwoCredits()
        {
            var prince = await SeedCreatorAsync(name: "Prince");

            await AddAsync(Request(credits: [Primary(prince), new(prince, CreditRole.Producer, "", null)]));

            Assert.Equal(2, (await SingleReleaseAsync()).Credits.Count);
        }

        [Fact]
        public async Task CreditForAnotherUsersCreator_IsUnknown_AndNothingIsWritten()
        {
            var theirs = await SeedCreatorAsync(userId: OtherUserId);

            var result = await AddAsync(Request(credits: [Primary(theirs)]));

            Assert.Equal(AddReleaseError.UnknownCreator, result.Error);
            await AssertNothingWrittenAsync();
        }

        [Fact]
        public async Task CreditForACreatorThatDoesntExist_IsUnknown()
        {
            var kiss = await SeedCreatorAsync();

            var result = await AddAsync(Request(credits: [Primary(kiss, " & "), Primary(12345)]));

            Assert.Equal(AddReleaseError.UnknownCreator, result.Error);
            await AssertNothingWrittenAsync();
        }

        [Fact]
        public async Task CreditForAnUnknownCreator_StoresNoImage()
        {
            CoverArtArchiveHasTheCover(_harness.Http);
            _harness.Http.Respond(CoverUrl, HttpStatusCode.OK, Jpeg);

            await AddAsync(Request(credits: [Primary(12345)], images: [Cover]));

            // Checked before anything is downloaded
            Assert.False(_harness.Http.WasRequested(CoverUrl));
            await AssertNothingWrittenAsync();
        }
        #endregion

        #region Tracks
        [Fact]
        public async Task Tracks_AreStored()
        {
            var kiss = await SeedCreatorAsync();

            await AddAsync(Request(
                credits: [Primary(kiss)],
                tracks: [new(1, 1, " Detroit Rock City ", 318), new(1, 2, "King of the Night Time World", null), new(2, 1, "Bonus", 200)]));

            var tracks = (await SingleReleaseAsync()).Tracks.OrderBy(x => x.DiscNumber).ThenBy(x => x.TrackNumber).ToList();
            Assert.Equal(3, tracks.Count);
            Assert.Equal((1, 1, "Detroit Rock City", (int?)318), (tracks[0].DiscNumber, tracks[0].TrackNumber, tracks[0].Title, tracks[0].DurationSeconds));
            Assert.Null(tracks[1].DurationSeconds);
            Assert.Equal(2, tracks[2].DiscNumber);
        }
        #endregion

        #region Images
        [Fact]
        public async Task LookedUpCover_IsTheFirstAndPreferredImage()
        {
            var kiss = await SeedCreatorAsync();
            CoverArtArchiveHasTheCover(_harness.Http);
            _harness.Http.Respond(CoverUrl, HttpStatusCode.OK, Jpeg);

            await AddAsync(Request(credits: [Primary(kiss)], images: [Cover]));

            var release = await SingleReleaseAsync();
            var image = Assert.Single(release.Images);
            Assert.Equal(0, image.SortOrder);
            Assert.Null(image.MediaFile.OwnerUserId);
            Assert.Equal(image.MediaFileId, release.PreferredImageId);
        }

        [Fact]
        public async Task SeveralImages_KeepTheirOrder_AndTheFirstIsPreferred()
        {
            var kiss = await SeedCreatorAsync();

            await AddAsync(Request(credits: [Primary(kiss)], images: [Upload(0), Upload(1)]), ServiceHarness.Files(Png, Jpeg));

            var release = await SingleReleaseAsync();
            var images = release.Images.OrderBy(x => x.SortOrder).ToList();
            Assert.Equal(2, images.Count);
            Assert.Equal("image/png", images[0].MediaFile.ContentType);
            Assert.Equal("image/jpeg", images[1].MediaFile.ContentType);
            Assert.Equal(images[0].MediaFileId, release.PreferredImageId);
            Assert.All(images, x => Assert.Equal(UserId, x.MediaFile.OwnerUserId));
        }

        [Fact]
        public async Task SameImageTwice_IsUsedOnce()
        {
            var kiss = await SeedCreatorAsync();

            await AddAsync(Request(credits: [Primary(kiss)], images: [Upload(0), Upload(1)]), ServiceHarness.Files(Jpeg, Jpeg));

            Assert.Single((await SingleReleaseAsync()).Images);
            Assert.Single(_harness.FileSystem.Files);
        }

        [Fact]
        public async Task WithoutImages_HasNoPreferredImage()
        {
            var kiss = await SeedCreatorAsync();

            await AddAsync(Request(credits: [Primary(kiss)]));

            var release = await SingleReleaseAsync();
            Assert.Empty(release.Images);
            Assert.Null(release.PreferredImageId);
        }

        [Fact]
        public async Task ImageThatIsntAnImage_IsInvalid_AndNothingIsWritten()
        {
            var kiss = await SeedCreatorAsync();

            var result = await AddAsync(Request(credits: [Primary(kiss)], images: [Upload(0)]), ServiceHarness.Files("not an image"u8.ToArray()));

            Assert.Equal(AddReleaseError.InvalidImage, result.Error);
            await AssertNothingWrittenAsync();
        }

        [Fact]
        public async Task CoverThatCantBeFound_IsNotFound()
        {
            var kiss = await SeedCreatorAsync();
            _harness.Http.Respond(CoverArtArchiveReleaseUrl, HttpStatusCode.NotFound);

            var result = await AddAsync(Request(credits: [Primary(kiss)], images: [Cover]));

            Assert.Equal(AddReleaseError.ImageNotFound, result.Error);
            await AssertNothingWrittenAsync();
        }

        [Fact]
        public async Task CoverWhenTheServiceIsDown_IsUnavailable()
        {
            var kiss = await SeedCreatorAsync();
            _harness.Http.Respond(CoverArtArchiveReleaseUrl, HttpStatusCode.ServiceUnavailable);

            var result = await AddAsync(Request(credits: [Primary(kiss)], images: [Cover]));

            Assert.Equal(AddReleaseError.ImageServiceUnavailable, result.Error);
            await AssertNothingWrittenAsync();
        }

        [Fact]
        public async Task ImageFailingAfterAnother_RemovesTheOneAlreadyWritten()
        {
            var kiss = await SeedCreatorAsync();
            _harness.Http.Respond(CoverArtArchiveReleaseUrl, HttpStatusCode.NotFound);

            var result = await AddAsync(Request(credits: [Primary(kiss)], images: [Upload(0), Cover]), ServiceHarness.Files(Jpeg));

            Assert.Equal(AddReleaseError.ImageNotFound, result.Error);
            await AssertNothingWrittenAsync();
        }
        #endregion

        #region GetReleasesForUserAsync
        private Task<List<ReleaseSummary>> GetPageAsync(int offset = 0, int limit = ReleaseService.MaxPageSize, long userId = UserId)
            => _harness.NewRequest().Releases.GetReleasesForUserAsync(userId, offset, limit, CancellationToken.None);

        private async Task<long> AddReleaseAsync(long creatorId,
                                                 string title,
                                                 int? firstReleaseYear = 1976,
                                                 int? releaseYear = 1997,
                                                 ReleaseStatus status = ReleaseStatus.Owned,
                                                 long userId = UserId)
        {
            var request = Request(credits: [Primary(creatorId)], title: title) with
            {
                FirstReleaseYear = firstReleaseYear,
                ReleaseYear = releaseYear,
                Status = status
            };
            var result = await AddAsync(request, userId: userId);
            Assert.Equal(AddReleaseError.None, result.Error);
            return result.ReleaseId;
        }

        [Fact]
        public async Task Summaries_DescribeTheRelease()
        {
            var simon = await SeedCreatorAsync(name: "Simon");
            var garfunkel = await SeedCreatorAsync(name: "Garfunkel");
            var added = await AddAsync(Request(credits: [Primary(simon, " & "), Primary(garfunkel)], title: "Bookends"));

            var summary = Assert.Single(await GetPageAsync());

            Assert.Equal(added.ReleaseId, summary.Id);
            Assert.Equal("Bookends", summary.Title);
            Assert.Equal("Simon & Garfunkel", summary.ArtistName);
            Assert.Equal(1976, summary.Year);
            Assert.Equal(ReleaseType.Record, summary.Type);
            Assert.Equal(ReleaseStatus.Owned, summary.Status);
            Assert.Null(summary.ImageUrl);
        }

        [Fact]
        public async Task Summaries_ArtistName_IsOnlyThePrimaryCredits()
        {
            var kiss = await SeedCreatorAsync(name: "Kiss");
            var ezrin = await SeedCreatorAsync(name: "Bob Ezrin");
            await AddAsync(Request(credits: [Primary(kiss), new(ezrin, CreditRole.Producer, "", null)]));

            Assert.Equal("Kiss", Assert.Single(await GetPageAsync()).ArtistName);
        }

        [Fact]
        public async Task Summaries_Year_IsTheFirstReleaseYear_OrThisReleasesIfUnknown()
        {
            var kiss = await SeedCreatorAsync();
            await AddReleaseAsync(kiss, "Destroyer", firstReleaseYear: 1976, releaseYear: 1997);
            await AddReleaseAsync(kiss, "Love Gun", firstReleaseYear: null, releaseYear: 1997);

            var years = (await GetPageAsync()).ToDictionary(x => x.Title, x => x.Year);
            Assert.Equal(1976, years["Destroyer"]);
            Assert.Equal(1997, years["Love Gun"]);
        }

        [Fact]
        public async Task Summaries_Image_IsThePreferredOne()
        {
            var kiss = await SeedCreatorAsync();
            await AddAsync(Request(credits: [Primary(kiss)], images: [Upload(0), Upload(1)]), ServiceHarness.Files(Png, Jpeg));

            // Make the second image the preferred one
            using (var db = _harness.CreateDbContext())
            {
                var release = await db.Releases.Include(x => x.Images).SingleAsync(TestContext.Current.CancellationToken);
                release.PreferredImageId = release.Images.Single(x => x.SortOrder == 1).MediaFileId;
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var preferred = await _harness.CreateDbContext().MediaFiles.SingleAsync(x => x.ContentType == "image/jpeg", TestContext.Current.CancellationToken);
            Assert.StartsWith($"/api/uploads/{preferred.Id}?", Assert.Single(await GetPageAsync()).ImageUrl);
        }

        [Fact]
        public async Task Summaries_WithoutAPreferredImage_ShowTheFirst()
        {
            var kiss = await SeedCreatorAsync();
            CoverArtArchiveHasTheCover(_harness.Http);
            _harness.Http.Respond(CoverUrl, HttpStatusCode.OK, Jpeg);
            await AddAsync(Request(credits: [Primary(kiss)], images: [Cover]));

            using (var db = _harness.CreateDbContext())
            {
                var release = await db.Releases.SingleAsync(TestContext.Current.CancellationToken);
                release.PreferredImageId = null;
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var file = await _harness.CreateDbContext().MediaFiles.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal($"/images/{file.FileName}", Assert.Single(await GetPageAsync()).ImageUrl);
        }

        [Fact]
        public async Task Summaries_AreOnlyTheUsersOwnedReleases()
        {
            var mine = await SeedCreatorAsync(userId: UserId);
            var theirs = await SeedCreatorAsync(userId: OtherUserId);
            await AddReleaseAsync(mine, "Destroyer");
            await AddReleaseAsync(mine, "Love Gun", status: ReleaseStatus.Wanted);
            await AddReleaseAsync(mine, "Unmasked", status: ReleaseStatus.NotOwned);
            await AddReleaseAsync(theirs, "Alive!", userId: OtherUserId);

            Assert.Equal("Destroyer", Assert.Single(await GetPageAsync()).Title);
        }

        [Fact]
        public async Task Summaries_AreSortedByArtistThenYearThenTitle()
        {
            // By sort name: "Beatles, The" before "Kiss"
            var kiss = await SeedCreatorAsync(name: "Kiss");
            var beatles = await SeedCreatorAsync(name: "Beatles, The");
            await AddReleaseAsync(kiss, "Love Gun", firstReleaseYear: 1977);
            await AddReleaseAsync(kiss, "Destroyer", firstReleaseYear: 1976);
            await AddReleaseAsync(kiss, "Alive!", firstReleaseYear: 1975);
            await AddReleaseAsync(kiss, "Dressed to Kill", firstReleaseYear: 1975);
            await AddReleaseAsync(beatles, "Abbey Road", firstReleaseYear: 1969);

            var titles = (await GetPageAsync()).Select(x => x.Title);

            Assert.Equal(["Abbey Road", "Alive!", "Dressed to Kill", "Destroyer", "Love Gun"], titles);
        }

        [Fact]
        public async Task Summaries_ComeInPages()
        {
            var kiss = await SeedCreatorAsync();
            foreach (var year in new[] { 1974, 1975, 1976, 1977, 1978 })
                await AddReleaseAsync(kiss, $"Album {year}", firstReleaseYear: year);

            Assert.Equal(["Album 1974", "Album 1975"], (await GetPageAsync(offset: 0, limit: 2)).Select(x => x.Title));
            Assert.Equal(["Album 1976", "Album 1977"], (await GetPageAsync(offset: 2, limit: 2)).Select(x => x.Title));
            Assert.Equal(["Album 1978"], (await GetPageAsync(offset: 4, limit: 2)).Select(x => x.Title));
            Assert.Empty(await GetPageAsync(offset: 6, limit: 2));
        }
        #endregion

        #region GetReleaseAsync
        private Task<ReleaseDetails?> GetReleaseAsync(long releaseId, long userId = UserId)
            => _harness.NewRequest().Releases.GetReleaseAsync(userId, releaseId, CancellationToken.None);

        [Fact]
        public async Task Details_DescribeEverything()
        {
            var simon = await SeedCreatorAsync(name: "Simon");
            var garfunkel = await SeedCreatorAsync(name: "Garfunkel");
            CoverArtArchiveHasTheCover(_harness.Http);
            _harness.Http.Respond(CoverUrl, HttpStatusCode.OK, Jpeg);

            var request = Request(
                credits: [Primary(simon, " & "), Primary(garfunkel)],
                tracks: [new(1, 2, "Save the Life of My Child", 169), new(1, 1, "Bookends Theme", 32)],
                images: [Cover],
                title: "Bookends",
                musicBrainzReleaseId: ReleaseMbid,
                musicBrainzReleaseGroupId: ReleaseGroupMbid,
                barcode: "074640905529",
                score: 9);
            var added = await AddAsync(request);

            var details = await GetReleaseAsync(added.ReleaseId);

            Assert.NotNull(details);
            Assert.Equal(added.ReleaseId, details.Summary.Id);
            Assert.Equal("Bookends", details.Summary.Title);
            Assert.Equal("Simon & Garfunkel", details.Summary.ArtistName);
            Assert.Equal(ReleaseFormat.Cd, details.Format);
            Assert.Equal(1997, details.ReleaseYear);
            Assert.Equal(1976, details.FirstReleaseYear);
            Assert.Equal(2060, details.DurationSeconds);
            Assert.Equal("074640905529", details.Barcode);
            Assert.Equal(9, details.Score);

            Assert.Equal(
                [new(simon, "Simon", CreditRole.Primary, " & ", null), new(garfunkel, "Garfunkel", CreditRole.Primary, "", null)],
                details.Credits);

            // In position order, not the order they were sent in
            Assert.Equal(["Bookends Theme", "Save the Life of My Child"], details.Tracks.Select(x => x.Title));

            var image = Assert.Single(details.Images);
            Assert.StartsWith("/images/", image.Url);
            Assert.Equal(ReleasePageUrl, image.SourcePageUrl);
            Assert.Equal(image.Url, details.Summary.ImageUrl);

            Assert.Equal(ReleaseMbid, details.ExternalIds[ExternalSource.MusicBrainz]);
            Assert.Equal(ReleaseGroupMbid, details.ExternalIds[ExternalSource.MusicBrainzReleaseGroup]);
        }

        [Fact]
        public async Task Details_ImagesAreInTheirOrder()
        {
            var kiss = await SeedCreatorAsync();
            var added = await AddAsync(Request(credits: [Primary(kiss)], images: [Upload(0), Upload(1)]), ServiceHarness.Files(Png, Jpeg));

            var details = await GetReleaseAsync(added.ReleaseId);

            var files = await _harness.CreateDbContext().MediaFiles.ToListAsync(TestContext.Current.CancellationToken);
            var png = files.Single(x => x.ContentType == "image/png");
            var jpeg = files.Single(x => x.ContentType == "image/jpeg");
            Assert.Equal(2, details!.Images.Count);
            Assert.StartsWith($"/api/uploads/{png.Id}?", details.Images[0].Url);
            Assert.StartsWith($"/api/uploads/{jpeg.Id}?", details.Images[1].Url);
        }

        [Fact]
        public async Task Details_OfAnotherUsersRelease_AreNull()
        {
            var theirs = await SeedCreatorAsync(userId: OtherUserId);
            var releaseId = await AddReleaseAsync(theirs, "Destroyer", userId: OtherUserId);

            Assert.Null(await GetReleaseAsync(releaseId, userId: UserId));
        }

        [Fact]
        public async Task Details_OfAReleaseThatDoesntExist_AreNull()
            => Assert.Null(await GetReleaseAsync(12345));
        #endregion

        #region Failures
        [Fact]
        public async Task FailedSave_Throws_AndRemovesTheImagesItWrote()
        {
            var kiss = await SeedCreatorAsync();
            _harness.FailSaves = true;

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                AddAsync(Request(credits: [Primary(kiss)], images: [Upload(0)]), ServiceHarness.Files(Jpeg)));

            Assert.Empty(_harness.FileSystem.Files);
        }
        #endregion
    }
}
