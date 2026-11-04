using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Xunit;

namespace CdArchiveTests
{
    // MusicBrainzService.SearchReleaseCandidatesAsync: what it asks MusicBrainz for
    public sealed class MusicBrainzReleaseSearchTests : IDisposable
    {
        private readonly ServiceHarness _harness = new();

        public void Dispose() => _harness.Dispose();

        // MusicBrainz knows nothing here (the fake answers 404), so the search asks for the release
        // groups and then falls back to the releases
        private Task SearchAsync(string title, params string[] artists)
            => _harness.NewRequest().MusicBrainz.SearchReleaseCandidatesAsync(
                title, artists, new DateOnly(2011, 8, 8), 12, "Album", null, CancellationToken.None);

        // The Lucene query of the first request to this MusicBrainz resource ("release-group" or "release")
        private string QueryTo(string resource)
        {
            var url = new Uri(_harness.Http.RequestedUrls.First(x => x.StartsWith($"https://musicbrainz.org/ws/2/{resource}/?", StringComparison.Ordinal)));
            return HttpUtility.ParseQueryString(url.Query)["query"]!;
        }

        // Watch the Throne credits "Kanye West", whose own name on MusicBrainz is now "Ye", and
        // "Jay‐Z", whose own name is now "JAŸ-Z". Either name must find the album.
        [Fact]
        public async Task ArtistsAreFoundByTheirOwnNameOrTheCreditedOne()
        {
            await SearchAsync("Watch the Throne", "JAŸ-Z", "Kanye West");

            const string artists = """(artistname:"JAŸ-Z" OR creditname:"JAŸ-Z") AND (artistname:"Kanye West" OR creditname:"Kanye West")""";
            Assert.Equal($"""releasegroup:"Watch the Throne" AND {artists}""", QueryTo("release-group"));
            Assert.Equal($"""release:"Watch the Throne" AND {artists} AND status:official""", QueryTo("release"));
        }

        [Fact]
        public async Task OnlyTheFirstTwoArtistsAreSearchedFor()
        {
            await SearchAsync("Crosby, Stills, Nash & Young", "Crosby", "Stills", "Nash");

            Assert.DoesNotContain("Nash\"", QueryTo("release-group"));
        }

        [Fact]
        public async Task VariousArtistsIsNotSearchedFor()
        {
            await SearchAsync("Now That's What I Call Music!", "Various Artists");

            Assert.Equal("releasegroup:\"Now That's What I Call Music!\"", QueryTo("release-group"));
        }

        [Fact]
        public async Task QuotesInAnArtistsNameAreEscaped()
        {
            await SearchAsync("Album", "The \"Artist\"");

            Assert.Equal("""releasegroup:"Album" AND (artistname:"The \"Artist\"" OR creditname:"The \"Artist\"")""", QueryTo("release-group"));
        }
    }
}
