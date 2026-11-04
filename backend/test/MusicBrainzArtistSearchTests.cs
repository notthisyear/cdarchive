using CdArchiveBackend.Common;
using CdArchiveBackend.Data.Database;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CdArchiveTests
{
    // MusicBrainzService.SearchArtistsAsync: the artists with the name the user typed
    public sealed class MusicBrainzArtistSearchTests : IDisposable
    {
        private readonly ServiceHarness _harness = new();

        public void Dispose() => _harness.Dispose();

        private Task<CdArchiveBackend.Services.External.MusicBrainzService.ArtistCandidateResult> SearchAsync(string name)
            => _harness.NewRequest().MusicBrainz.SearchArtistsAsync(name, null, CancellationToken.None);

        // What the service asks for (names without quotes or backslashes, which it would escape)
        private static string ArtistSearchUrl(string name)
            => $"https://musicbrainz.org/ws/2/artist/?query={Uri.EscapeDataString($"artist:\"{name.Trim()}\"")}&limit=10&fmt=json";

        // A search result as MusicBrainz gives it, ids made up from the position
        private static string Artists(params (string Name, int Score)[] artists) => $$"""
            {
                "artists": [
                    {{string.Join(", ", artists.Select((a, i) => $$"""
                        { "id": "artist-{{i}}", "name": "{{a.Name}}", "sort-name": "{{a.Name}}", "type": "Group", "country": "GB", "score": {{a.Score}} }
                        """))}}
                ]
            }
            """;

        [Fact]
        public async Task NameWithoutDiacriticsFindsTheArtistWithThem()
        {
            _harness.Http.Respond(ArtistSearchUrl("Motorhead"), HttpStatusCode.OK, Artists(("Motörhead", 100)));

            var result = await SearchAsync("Motorhead");

            Assert.Equal(Utilities.HttpGetResult.Success, result.GetResult);
            Assert.Equal("Motörhead", Assert.Single(result.Artists).Name);
        }

        [Fact]
        public async Task AsciiApostropheFindsTheTypographicOne()
        {
            _harness.Http.Respond(ArtistSearchUrl("Guns N' Roses"), HttpStatusCode.OK, Artists(("Guns N’ Roses", 100)));

            var result = await SearchAsync("Guns N' Roses");

            Assert.Equal("Guns N’ Roses", Assert.Single(result.Artists).Name);
        }

        [Fact]
        public async Task OtherNamesContainingTheNameAreLeftOut()
        {
            _harness.Http.Respond(ArtistSearchUrl("Low"), HttpStatusCode.OK, Artists(("Low", 100), ("Low Roar", 90), ("The Low Anthem", 85), ("Lou", 60)));

            var result = await SearchAsync("Low");

            Assert.Equal("Low", Assert.Single(result.Artists).Name);
        }

        [Fact]
        public async Task NameAsWrittenComesBeforeHigherScoredVariants()
        {
            _harness.Http.Respond(ArtistSearchUrl("Motorhead"), HttpStatusCode.OK, Artists(("Motörhead", 100), ("MOTORHEAD", 40), ("Motorhead", 50)));

            var result = await SearchAsync("Motorhead");

            Assert.Equal(["Motorhead", "MOTORHEAD", "Motörhead"], result.Artists.Select(a => a.Name));
        }

        [Fact]
        public async Task AllPunctuationNameOnlyFindsThatName()
        {
            _harness.Http.Respond(ArtistSearchUrl("!!!"), HttpStatusCode.OK, Artists(("!!!", 100), ("???", 50)));

            var result = await SearchAsync("!!!");

            Assert.Equal("!!!", Assert.Single(result.Artists).Name);
        }

        [Fact]
        public async Task SameNameInOtherCaseOrSpacingIsAnsweredFromTheCache()
        {
            _harness.Http.Respond(ArtistSearchUrl("ABBA"), HttpStatusCode.OK, Artists(("ABBA", 100)));

            await SearchAsync("ABBA");
            var result = await SearchAsync("  Abba ");

            Assert.Equal("ABBA", Assert.Single(result.Artists).Name);
            Assert.Single(_harness.Http.RequestedUrls);
        }

        [Fact]
        public async Task AllPunctuationNamesDoNotShareACacheEntry()
        {
            _harness.Http.Respond(ArtistSearchUrl("!!!"), HttpStatusCode.OK, Artists(("!!!", 100)));
            _harness.Http.Respond(ArtistSearchUrl("???"), HttpStatusCode.OK, Artists(("???", 100)));

            var first = await SearchAsync("!!!");
            var second = await SearchAsync("???");

            Assert.Equal("!!!", Assert.Single(first.Artists).Name);
            Assert.Equal("???", Assert.Single(second.Artists).Name);
            Assert.Equal(2, _harness.Http.RequestedUrls.Count);
        }

        [Fact]
        public async Task MissingTypeIsNullAndUnknownTypeIsOther()
        {
            _harness.Http.Respond(ArtistSearchUrl("Kiss"), HttpStatusCode.OK, """
                {
                    "artists": [
                        { "id": "artist-0", "name": "Kiss", "score": 100 },
                        { "id": "artist-1", "name": "Kiss", "type": "Character", "score": 90 },
                        { "id": "artist-2", "name": "Kiss", "type": "Orchestra", "score": 80 }
                    ]
                }
                """);

            var result = await SearchAsync("Kiss");

            Assert.Equal([null, CreatorType.Other, CreatorType.Group], result.Artists.Select(a => a.Type));
        }

        [Fact]
        public async Task FoundArtistNeedsNoLookupLater()
        {
            _harness.Http.Respond(ArtistSearchUrl("Low"), HttpStatusCode.OK, Artists(("Low", 100)));
            await SearchAsync("Low");

            // As when the artist is saved and the sort name is needed
            var later = await _harness.NewRequest().MusicBrainz.GetArtistAsync("artist-0", null, CancellationToken.None);

            Assert.Equal("Low", Assert.Single(later.Artists).SortName);
            Assert.Single(_harness.Http.RequestedUrls);
        }

        [Fact]
        public async Task UnavailableIsNotCached()
        {
            _harness.Http.Respond(ArtistSearchUrl("ABBA"), HttpStatusCode.ServiceUnavailable);
            var failed = await SearchAsync("ABBA");

            _harness.Http.Respond(ArtistSearchUrl("ABBA"), HttpStatusCode.OK, Artists(("ABBA", 100)));
            var result = await SearchAsync("ABBA");

            Assert.Equal(Utilities.HttpGetResult.RateLimitedOrServiceUnavailable, failed.GetResult);
            Assert.Empty(failed.Artists);
            Assert.Equal(Utilities.HttpGetResult.Success, result.GetResult);
            Assert.Equal("ABBA", Assert.Single(result.Artists).Name);
        }
    }
}
