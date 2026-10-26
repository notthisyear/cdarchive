using CdArchiveBackend.Common;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CdArchiveTests
{
    // MusicBrainzService.LookUpReleaseCandidatesAsync: the releases an id the user pasted stands for
    public sealed class MusicBrainzLookupTests : IDisposable
    {
        private const string ReleaseId = "4fef8a2e-e3e5-46b2-bdfb-2803b361f1f0";
        private const string ReleaseGroupId = "541eb339-d0c6-3740-b665-8ad6c09da331";

        private static readonly string ReleaseUrl = $"https://musicbrainz.org/ws/2/release/{ReleaseId}?inc=artist-credits+release-groups+media&fmt=json";
        private static readonly string GroupAsReleaseUrl = $"https://musicbrainz.org/ws/2/release/{ReleaseGroupId}?inc=artist-credits+release-groups+media&fmt=json";
        private static readonly string GroupBrowseUrl = $"https://musicbrainz.org/ws/2/release?release-group={ReleaseGroupId}&inc=media+artist-credits+release-groups&fmt=json&offset=0&limit=100";

        private readonly ServiceHarness _harness = new();

        public void Dispose() => _harness.Dispose();

        private Task<CdArchiveBackend.Services.External.MusicBrainzService.ReleaseCandidateResult> LookUpAsync(string mbid)
            => _harness.NewRequest().MusicBrainz.LookUpReleaseCandidatesAsync(mbid, null, CancellationToken.None);

        // A release as MusicBrainz gives it, looked up or browsed
        private static string Release(string id, string? date, string? country, int[] trackCounts) => $$"""
            {
                "id": "{{id}}",
                "title": "Destroyer",
                "date": {{(date is null ? "null" : $"\"{date}\"")}},
                "country": {{(country is null ? "null" : $"\"{country}\"")}},
                "barcode": "",
                "disambiguation": "",
                "release-group": { "id": "{{ReleaseGroupId}}", "primary-type": "Album", "first-release-date": "1976-03-15" },
                "artist-credit": [
                    { "name": "Simon", "joinphrase": " & ", "artist": { "id": "a1", "name": "Simon", "score": 0 } },
                    { "name": "Garfunkel", "joinphrase": "", "artist": { "id": "a2", "name": "Garfunkel", "score": 0 } }
                ],
                "media": [{{string.Join(", ", trackCounts.Select(n => $$"""{ "format": "CD", "track-count": {{n}} }"""))}}]
            }
            """;

        [Fact]
        public async Task ReleaseId_GivesThatRelease()
        {
            _harness.Http.Respond(ReleaseUrl, HttpStatusCode.OK, Release(ReleaseId, "1997", "US", [10, 3]));

            var result = await LookUpAsync(ReleaseId);

            Assert.Equal(Utilities.HttpGetResult.Success, result.GetResult);
            var release = Assert.Single(result.Candidates);
            Assert.Equal(ReleaseId, release.Id);
            Assert.Equal(ReleaseGroupId, release.ReleaseGroupId);
            Assert.Equal("Simon & Garfunkel", release.ArtistCombined);
            Assert.Equal([" & ", ""], release.JoinPhrases);
            Assert.Equal("1976-03-15", release.FirstReleaseDate);
            Assert.Equal("CD", release.MediaType);

            // A looked-up release only has track counts per medium
            Assert.Equal(13, release.TrackCount);

            // Known as a release, so no need to try it as a release group
            Assert.False(_harness.Http.WasRequested(GroupBrowseUrl.Replace(ReleaseGroupId, ReleaseId)));
        }

        [Fact]
        public async Task ReleaseGroupId_GivesItsReleases_ByDateThenCountry_UndatedLast()
        {
            _harness.Http.Respond(GroupAsReleaseUrl, HttpStatusCode.NotFound);
            _harness.Http.Respond(GroupBrowseUrl, HttpStatusCode.OK, $$"""
                {
                    "release-count": 4,
                    "releases": [
                        {{Release("00000000-0000-0000-0000-000000000001", null, "US", [9])}},
                        {{Release("00000000-0000-0000-0000-000000000002", "1997", "US", [9])}},
                        {{Release("00000000-0000-0000-0000-000000000003", "1976-03-15", "US", [9])}},
                        {{Release("00000000-0000-0000-0000-000000000004", "1976-03-15", "GB", [9])}}
                    ]
                }
                """);

            var result = await LookUpAsync(ReleaseGroupId);

            Assert.Equal(Utilities.HttpGetResult.Success, result.GetResult);
            Assert.Equal(
                ["00000000-0000-0000-0000-000000000004", "00000000-0000-0000-0000-000000000003", "00000000-0000-0000-0000-000000000002", "00000000-0000-0000-0000-000000000001"],
                result.Candidates.Select(x => x.Id));
        }

        [Fact]
        public async Task IdOfNeither_GivesNothing()
        {
            _harness.Http.Respond(GroupAsReleaseUrl, HttpStatusCode.NotFound);
            _harness.Http.Respond(GroupBrowseUrl, HttpStatusCode.NotFound);

            var result = await LookUpAsync(ReleaseGroupId);

            Assert.Equal(Utilities.HttpGetResult.NotFound, result.GetResult);
            Assert.Empty(result.Candidates);
        }

        [Fact]
        public async Task WhenMusicBrainzIsDown_SaysSo()
        {
            _harness.Http.Respond(ReleaseUrl, HttpStatusCode.ServiceUnavailable);

            var result = await LookUpAsync(ReleaseId);

            Assert.Equal(Utilities.HttpGetResult.RateLimitedOrServiceUnavailable, result.GetResult);
            Assert.Empty(result.Candidates);
        }

        [Fact]
        public async Task WhenMusicBrainzWasDown_IsLookedUpAgain()
        {
            _harness.Http.Respond(ReleaseUrl, HttpStatusCode.ServiceUnavailable);
            await LookUpAsync(ReleaseId);

            // The failure mustn't be cached as "not a release"
            _harness.Http.Respond(ReleaseUrl, HttpStatusCode.OK, Release(ReleaseId, "1997", "US", [10]));
            Assert.Single((await LookUpAsync(ReleaseId)).Candidates);
        }
    }
}
