using CdArchiveBackend.Common;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CdArchiveTests
{
    // MusicBrainzService.FindArtistsBySpotifyIdAsync: the artists that link to a Spotify artist's page
    public sealed class MusicBrainzSpotifyArtistTests : IDisposable
    {
        private const string SpotifyId = "1DFr97A9HnbV3SKTJFu62M";
        private const string ArtistMbid = "57961a97-3796-4bf7-9f02-a985a8979ae9";
        private const string OtherArtistMbid = "5b11f4ce-a62d-471e-81fc-a69a8278c7da";

        private static readonly string UrlLookupUrl =
            $"https://musicbrainz.org/ws/2/url?resource={Uri.EscapeDataString($"https://open.spotify.com/artist/{SpotifyId}")}&inc=artist-rels&fmt=json";

        private readonly ServiceHarness _harness = new();

        public void Dispose() => _harness.Dispose();

        private Task<CdArchiveBackend.Services.External.MusicBrainzService.ArtistCandidateResult> FindAsync()
            => _harness.NewRequest().MusicBrainz.FindArtistsBySpotifyIdAsync(SpotifyId, null, CancellationToken.None);

        // The Spotify page as MusicBrainz gives it, with what links to it. A linked artist comes whole.
        private static string UrlRelations(params (string TargetType, string Mbid)[] relations) => $$"""
            {
                "id": "url-0",
                "resource": "https://open.spotify.com/artist/{{SpotifyId}}",
                "relations": [
                    {{string.Join(", ", Array.ConvertAll(relations, r => $$"""
                        {
                            "type": "free streaming",
                            "target-type": "{{r.TargetType}}",
                            "{{r.TargetType}}": { "id": "{{r.Mbid}}", "name": "Low", "sort-name": "Low", "type": "Group", "country": "US", "disambiguation": "Duluth band" }
                        }
                        """))}}
                ]
            }
            """;

        [Fact]
        public async Task ArtistLinkingToThePageIsFoundWithAllTheyAre()
        {
            _harness.Http.Respond(UrlLookupUrl, HttpStatusCode.OK, UrlRelations(("artist", ArtistMbid)));

            var result = await FindAsync();

            Assert.Equal(Utilities.HttpGetResult.Success, result.GetResult);
            var artist = Assert.Single(result.Artists);
            Assert.Equal(ArtistMbid, artist.Id);
            Assert.Equal("Low", artist.SortName);
            Assert.Equal(CdArchiveBackend.Data.Database.CreatorType.Group, artist.Type);
            Assert.Equal("US", artist.Country);
            Assert.Equal("Duluth band", artist.Disambiguation);

            // All from the one request
            Assert.Single(_harness.Http.RequestedUrls);
        }

        [Fact]
        public async Task FoundArtistNeedsNoLookupLater()
        {
            _harness.Http.Respond(UrlLookupUrl, HttpStatusCode.OK, UrlRelations(("artist", ArtistMbid)));
            await FindAsync();

            // As when the artist is saved and the sort name is needed
            var later = await _harness.NewRequest().MusicBrainz.GetArtistAsync(ArtistMbid, null, CancellationToken.None);

            Assert.Equal("Low", Assert.Single(later.Artists).SortName);
            Assert.Single(_harness.Http.RequestedUrls);
        }

        [Fact]
        public async Task OtherThingsLinkingToThePageAreLeftOut()
        {
            _harness.Http.Respond(UrlLookupUrl, HttpStatusCode.OK, UrlRelations(("label", OtherArtistMbid), ("artist", ArtistMbid)));

            var result = await FindAsync();

            Assert.Equal(ArtistMbid, Assert.Single(result.Artists).Id);
        }

        [Fact]
        public async Task PageNoArtistLinksToGivesNone()
        {
            // MusicBrainz doesn't know the URL at all, which the fake answers for anything not set up

            var result = await FindAsync();

            Assert.Empty(result.Artists);
            Assert.NotEqual(Utilities.HttpGetResult.RateLimitedOrServiceUnavailable, result.GetResult);
        }

        [Fact]
        public async Task UnavailableIsNotCached()
        {
            _harness.Http.Respond(UrlLookupUrl, HttpStatusCode.ServiceUnavailable);
            var failed = await FindAsync();

            _harness.Http.Respond(UrlLookupUrl, HttpStatusCode.OK, UrlRelations(("artist", ArtistMbid)));
            var result = await FindAsync();

            Assert.Equal(Utilities.HttpGetResult.RateLimitedOrServiceUnavailable, failed.GetResult);
            Assert.Empty(failed.Artists);
            Assert.Equal(ArtistMbid, Assert.Single(result.Artists).Id);
        }
    }
}
