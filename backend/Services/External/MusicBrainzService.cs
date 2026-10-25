using CdArchiveBackend.Common;
using CdArchiveBackend.Data.Database;
using CdArchiveBackend.Data.External.MusicBrainz;
using CdArchiveBackend.Services.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static CdArchiveBackend.Common.Utilities;

namespace CdArchiveBackend.Services.External
{
    using Options = CdArchiveBackend.Data.External.MusicBrainz.Options;

    internal sealed partial class MusicBrainzService(HttpClient httpClient,
                                                     MusicBrainzRateLimiter limiter,
                                                     IOptions<Options> options,
                                                     IMemoryCache cache)
    {
        #region Private fields

        private enum MusicBrainzResult
        {
            NotFound,
            RateLimitedOrTooManyRequests,
            Success
        }

        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

        private readonly HttpClient _httpClient = httpClient;

        private readonly RateLimiter _limiter = limiter;

        private readonly int _maxNumberOfAttempts = options.Value.MaxNumberOfAttempts;

        private readonly IMemoryCache _cache = cache;

        private const int ReleaseCandidatePageLimit = 50;

        // Note: For some searches, MusicBrainz can returns tens of thousands of results. Let's cap it after two full pages.
        private const int MaxReleaseCandidates = ReleaseCandidatePageLimit * 2;

        private const int ArtistCandidateLimit = 10;

        private const int MinimumNumberOfReleasePoints = 75;
        #endregion

        public readonly record struct ReleaseCandidateResult(HttpGetResult GetResult, IReadOnlyList<MusicBrainzReleaseCandidate> Candidates);
        public readonly record struct TrackListResult(HttpGetResult GetResult, IReadOnlyList<MusicBrainzTrack> Tracks);
        public readonly record struct ArtistCandidateResult(HttpGetResult GetResult, IReadOnlyList<MusicBrainzArtist> Artists);

        #region Public methods
        public async Task<ReleaseCandidateResult> SearchReleaseCandidatesAsync(string title,
                                                                               string[] artistNames,
                                                                               DateOnly releaseDate,
                                                                               int numberOfTracks,
                                                                               string releaseType,
                                                                               ChannelProgress channelProgress,
                                                                               CancellationToken ct)
        {
            // Note: We just include the first two artists when searching MusicBrainz as it can
            //       otherwise be a bit fragile.
            var artistClauses = artistNames
                .Where(a => !a.Equals("Various Artists", StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .Select(a => $"artistname:\"{EscapePhrase(a.Trim())}\"")
                .ToList();

            var who = artistClauses.Count > 0 ? " AND " + string.Join(" AND ", artistClauses) : "";
            var luceneSearchString = Normalize($"release:\"{EscapePhrase(title.Trim())}\"{who}");

            HttpGetResult getResult = HttpGetResult.NotSet;
            var cacheKey = $"mb:release-candidates:{luceneSearchString.ToLowerInvariant()}";
            var result = await _cache.GetOrCreateAsync<IReadOnlyList<MusicBrainzReleaseCandidate>>(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                List<ReleaseCandidateDto> bodies = [];
                var currentOffset = 0;
                while (true)
                {
                    ReleaseCandidateSearchDto? body;
                    entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                    (body, getResult) = await _httpClient.TryGet<ReleaseCandidateSearchDto>(
                        $"release/?query={Uri.EscapeDataString(luceneSearchString)}&offset={currentOffset}&limit={ReleaseCandidatePageLimit}&fmt=json&status:official",
                        _limiter,
                        _maxNumberOfAttempts,
                        ct,
                        "Getting matching artists",
                        channelProgress).ConfigureAwait(false);

                    if (body?.Releases == null)
                        return [];

                    bodies.AddRange(body.Releases);

                    if (((currentOffset + body.Releases.Count) < body.Count) && (bodies.Count < MaxReleaseCandidates))
                        currentOffset = ReleaseCandidatePageLimit;
                    else
                        break;
                }

                var releases = bodies.Where(x => x.ReleaseGroup?.Id is not null)
                    .RankAndFilterReleases(title, numberOfTracks, releaseDate, artistNames, releaseType, MinimumNumberOfReleasePoints);

                static string GetArtistName(List<CreditDto>? credits)
                {
                    if (credits == null)
                        return string.Empty;

                    var joinPhrase = credits.FirstOrDefault(x => !string.IsNullOrEmpty(x.JoinPhrase))?.JoinPhrase ?? "&";
                    return string.Join(joinPhrase, credits.Select(x => x.Name));
                }
                        
                return [.. releases
                .Select(r => new MusicBrainzReleaseCandidate(
                    r.Id,
                    r.ReleaseGroup?.Id ?? string.Empty,
                    r.Title ?? string.Empty,
                    GetArtistName(r.ArtistCredit),
                    r.ArtistCredit != null ? [..r.ArtistCredit.Select(x => x.Name)] : [],
                    r.ArtistCredit != null ? [..r.ArtistCredit.Select(x => x.ArtistDto?.Id ?? string.Empty)] : [],
                    r.Date,
                    r.ReleaseGroup?.PrimaryType,
                    // Note: I haven't seen more than one media format, so let's just take the first one
                    r.Media?.First().Format ?? string.Empty,
                    r.Country,
                    r.Disambiguation,
                    r.TrackCount ?? 0){
                })];
            }).ConfigureAwait(false);

            // If we failed due to the the MusicBrainz server being unhelpful, let's not cache it.
            if (getResult == HttpGetResult.RateLimitedOrServiceUnavailable)
                _cache.Remove(cacheKey);

            return new(getResult, result ?? []);
        }

        public async Task<TrackListResult> GetTracklistAsync(string releaseMbid, ChannelProgress channelProgress, CancellationToken ct)
        {
            HttpGetResult getResult = HttpGetResult.NotSet;
            var cacheKey = $"mb:tracklist:{releaseMbid}";
            var result = await _cache.GetOrCreateAsync<IReadOnlyList<MusicBrainzTrack>>(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                TrackListDto? body;
                (body, getResult) = await _httpClient.TryGet<TrackListDto>(
                    $"release/{releaseMbid}?inc=recordings&fmt=json",
                    _limiter,
                    _maxNumberOfAttempts,
                    ct,
                    "Getting tracklist",
                    channelProgress).ConfigureAwait(false);

                return body?.ToTracks() ?? [];

            }).ConfigureAwait(false);

            // If we failed due to the the MusicBrainz server being unhelpful, let's not cache it.
            if (getResult == HttpGetResult.RateLimitedOrServiceUnavailable)
                _cache.Remove(cacheKey);

            return new(getResult, result ?? []);
        }

        public async Task<ArtistCandidateResult> SearchArtistsAsync(string name, ChannelProgress channelProgress, CancellationToken ct)
        {
            var trimmedName = name.Trim();
            var luceneSearchString = $"artist:\"{EscapePhrase(trimmedName)}\"";
            var cacheKey = $"mb:artist:{Normalize(trimmedName)}";

            HttpGetResult getResult = HttpGetResult.NotSet;
            var result = await _cache.GetOrCreateAsync<IReadOnlyList<MusicBrainzArtist>>(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;

                ArtistCandidateDto? body;
                (body, getResult) = await _httpClient.TryGet<ArtistCandidateDto>(
                    $"artist/?query={Uri.EscapeDataString(luceneSearchString)}&limit={ArtistCandidateLimit}&fmt=json",
                    _limiter,
                    _maxNumberOfAttempts,
                    ct,
                    "Getting matching artists",
                    channelProgress).ConfigureAwait(false);

                if (body?.ArtistList == null)
                    return [];

                // Extract the matches that have exactly the same name and order by score
                return [.. body.ArtistList
                    .Where(x => CompareNullableStrings(x.Name, trimmedName))
                    .OrderByDescending(x => x.Score)
                    .Select(x => new MusicBrainzArtist(
                        x.Id,
                        x.Name,
                        x.SortName,
                        MapType(x.Type ?? string.Empty),
                        x.Country ?? string.Empty,
                        x.Disambiguation ?? string.Empty))];
                
            }).ConfigureAwait(false);

            // If we failed due to the the MusicBrainz server being unhelpful, let's not cache it.
            if (getResult == HttpGetResult.RateLimitedOrServiceUnavailable)
                _cache.Remove(cacheKey);

            return new(getResult, result ?? []);
        }

        public async Task<string?> GetWikidataEntityIdAsync(string artistMbid, ChannelProgress channelProgress, CancellationToken ct)
        {
            var cacheKey = $"mb:wikidata:{artistMbid}";
            HttpGetResult getResult = HttpGetResult.NotSet;
            var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                var (body, _) = await _httpClient.TryGet<RelationsDto>(
                    $"artist/{artistMbid}?inc=url-rels&fmt=json",
                    _limiter,
                    _maxNumberOfAttempts,
                    ct,
                    "Fetching Wikidata ID",
                    channelProgress);

                var resource = body?.Relations?.FirstOrDefault(r => CompareNullableStrings(r.Type, "wikidata"))?.Url?.Resource;
                if (resource is null)
                    return null;

                var match = ExtractItemEntityIdRegex().Match(resource);
                return match.Success ? match.Groups[1].Value : null;
            });

            // If we failed due to the the MusicBrainz server being unhelpful, let's not cache it.
            if (getResult == HttpGetResult.RateLimitedOrServiceUnavailable)
                _cache.Remove(cacheKey);

            return result;
        }

        public async Task<ArtistCandidateResult> GetArtistAsync(string artistMbid, ChannelProgress channelProgress, CancellationToken ct)
        {
            var cacheKey = $"mb:artist:{artistMbid}";
            HttpGetResult getResult = HttpGetResult.NotSet;
            var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                ArtistDto? body;
                (body, getResult) = await _httpClient.TryGet<ArtistDto>(
                    $"artist/{artistMbid}?fmt=json",
                    _limiter,
                    _maxNumberOfAttempts,
                    ct,
                    "Fetching artist",
                    channelProgress);

                if (body == null)
                    return null;

                return new MusicBrainzArtist(
                        body.Id,
                        body.Name,
                        body.SortName,
                        MapType(body.Type ?? string.Empty),
                        body.Country ?? string.Empty,
                        body.Disambiguation ?? string.Empty);
            });

            // If we failed due to the the MusicBrainz server being unhelpful, let's not cache it.
            if (getResult == HttpGetResult.RateLimitedOrServiceUnavailable)
                _cache.Remove(cacheKey);

            return new(getResult, result != null ? [result] : []);
        }
        #endregion

        // See https://musicbrainz.org/doc/Artist
        private static CreatorType MapType(string musicBrainzType)
            => musicBrainzType switch
            {
                "Person" => CreatorType.Person,
                "Group" or "Orchestra" or "Choir" => CreatorType.Group,
                _ => CreatorType.Other,
            };

        private static string EscapePhrase(string s)
            => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static string Normalize(string s) =>
            NormalizeStringRegex().Replace(s.Trim(), " ").ToLowerInvariant();

        // See https://www.wikidata.org/wiki/Wikidata:Identifiers
        [GeneratedRegex(@"/(Q\d+)$")]
        private static partial Regex ExtractItemEntityIdRegex();

        [GeneratedRegex(@"\s+")]
        private static partial Regex NormalizeStringRegex();

        //[GeneratedRegex(@"^\d{8,14}$")]
        //private static partial Regex BarcodeRegex();




        //public async Task<MusicBrainzArtist?> LookupArtistAsync(string name, CancellationToken ct)
        //{
        //    var candidates = await SearchArtistsAsync(name, 10, ct);
        //    var wanted = Normalize(name);

        //    var exact = candidates
        //        .Where(c => Normalize(c.Name) == wanted)
        //        .OrderByDescending(c => c.Score)
        //        .ToList();

        //    return exact.Count == 0 ? null : exact[0] with { Ambiguous = exact.Count > 1 };
        //}

        //public async Task<IReadOnlyList<MusicBrainzReleaseRef>> SearchReleasesByBarcodeAsync(
        //    string barcode, int limit, CancellationToken ct)
        //{
        //    barcode = barcode?.Trim() ?? "";
        //    if (!Regex.IsMatch(barcode, @"^\d{8,14}$"))      // also keeps the query free of Lucene syntax
        //        return Array.Empty<MusicBrainzReleaseRef>();

        //    limit = Math.Clamp(limit, 1, 10);
        //    var key = $"mb:barcode:{limit}:{barcode}";

        //    var result = await _cache.GetOrCreateAsync<IReadOnlyList<MusicBrainzReleaseRef>>(key, async entry =>
        //    {
        //        entry.AbsoluteExpirationRelativeToNow = CacheTtl;

        //        // A 12-digit UPC is the same code as a 13-digit EAN with a leading zero; sources differ on which they store
        //        var lucene = barcode.Length == 12
        //            ? $"barcode:{barcode} OR barcode:0{barcode}"
        //            : $"barcode:{barcode}";

        //        var body = await GetJsonAsync<ReleaseSearchDto>(
        //            $"release/?query={Uri.EscapeDataString(lucene)}&fmt=json&limit={limit}", ct);

        //        return (body?.Releases ?? new())
        //            .Where(r => r.ReleaseGroup?.Id is not null)
        //            .Select(r => new MusicBrainzReleaseRef(
        //                r.Id, r.ReleaseGroup!.Id!, r.Title ?? "", r.ArtistCredit?.FirstOrDefault()?.Name, r.Score))
        //            .ToList();
        //    });

        //    return result ?? Array.Empty<MusicBrainzReleaseRef>();
        //}


        //private async Task<IReadOnlyList<MusicBrainzArtist>> FetchAsync(string name, int limit, CancellationToken ct)
        //{
        //    // Quoted phrase query: inside quotes only backslash and double quote need escaping
        //    var escaped = name.Replace("\\", "\\\\").Replace("\"", "\\\"");
        //    var query = Uri.EscapeDataString($"artist:\"{escaped}\"");

        //    await _limiter.WaitAsync(ct);

        //    using var response = await _http.GetAsync($"artist/?query={query}&fmt=json&limit={limit}", ct);

        //    if (response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests)
        //        throw new HttpRequestException("MusicBrainz is rate limiting us", null, response.StatusCode);

        //    response.EnsureSuccessStatusCode();

        //    var body = await response.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken: ct);

        //    return (body?.Artists ?? new())
        //        .Select(a => new MusicBrainzArtist(
        //            a.Id, a.Name, a.SortName, a.Type, MapType(a.Type),
        //            a.Disambiguation, a.Country, a.Score))
        //        .ToList();
        //}



    }
}