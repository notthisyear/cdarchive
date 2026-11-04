using CdArchiveBackend.Common;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static CdArchiveTests.ExternalServiceStubs;

namespace CdArchiveTests
{
    // MusicBrainzService.GetArtistAsync and GetWikidataEntityIdAsync, which share one lookup
    public sealed class MusicBrainzArtistLookupTests : IDisposable
    {
        private readonly ServiceHarness _harness = new();

        public void Dispose() => _harness.Dispose();

        private Task<CdArchiveBackend.Services.External.MusicBrainzService.ArtistCandidateResult> GetArtistAsync()
            => _harness.NewRequest().MusicBrainz.GetArtistAsync(ArtistMbid, null, CancellationToken.None);

        private Task<CdArchiveBackend.Services.External.MusicBrainzService.WikidataEntityIdResult> GetWikidataEntityIdAsync()
            => _harness.NewRequest().MusicBrainz.GetWikidataEntityIdAsync(ArtistMbid, null, CancellationToken.None);

        [Fact]
        public async Task WikidataEntityAfterTheArtistNeedsNoRequest()
        {
            MusicBrainzKnowsTheWikidataEntity(_harness.Http);

            await GetArtistAsync();
            var result = await GetWikidataEntityIdAsync();

            Assert.Equal(WikidataEntityId, result.EntityId);
            Assert.Single(_harness.Http.RequestedUrls);
        }

        [Fact]
        public async Task ArtistAfterTheWikidataEntityNeedsNoRequest()
        {
            MusicBrainzKnowsTheWikidataEntity(_harness.Http);

            await GetWikidataEntityIdAsync();
            var result = await GetArtistAsync();

            Assert.Equal("Kiss", Assert.Single(result.Artists).Name);
            Assert.Single(_harness.Http.RequestedUrls);
        }

        [Fact]
        public async Task ArtistWithoutAWikidataLinkIsKnownToHaveNone()
        {
            MusicBrainzKnowsTheArtist(_harness.Http, "Kiss", "Kiss");

            await GetArtistAsync();
            var result = await GetWikidataEntityIdAsync();

            Assert.Null(result.EntityId);
            Assert.Single(_harness.Http.RequestedUrls);
        }

        [Fact]
        public async Task UnavailableArtistLeavesTheWikidataEntityToBeLookedUp()
        {
            _harness.Http.Respond(MusicBrainzArtistUrl, System.Net.HttpStatusCode.ServiceUnavailable);
            var failed = await GetArtistAsync();

            MusicBrainzKnowsTheWikidataEntity(_harness.Http);
            var result = await GetWikidataEntityIdAsync();

            Assert.Equal(Utilities.HttpGetResult.RateLimitedOrServiceUnavailable, failed.GetResult);
            Assert.Equal(WikidataEntityId, result.EntityId);
        }
    }
}
