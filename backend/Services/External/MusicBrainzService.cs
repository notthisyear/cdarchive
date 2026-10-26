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

        // Search results are ordered by score, so the album we look for is near the top if it's there at all
        private const int ReleaseGroupPageLimit = 100;
        private const int MaxReleaseGroups = ReleaseGroupPageLimit;

        // Usually just one, but a title may match several groups equally well (e.g. an album and a
        // live album with the same name). Each costs at least one request, so keep it small.
        private const int MaxReleaseGroupsToFetch = 3;

        // 100 is the most MusicBrainz returns at once. Popular albums have hundreds of releases.
        private const int ReleasesInGroupPageLimit = 100;
        private const int MaxReleasesInGroup = ReleasesInGroupPageLimit * 3;

        private const int ArtistCandidateLimit = 10;

        private const int MinimumNumberOfReleasePoints = 75;
        #endregion

        public readonly record struct ReleaseCandidateResult(HttpGetResult GetResult, IReadOnlyList<MusicBrainzReleaseCandidate> Candidates);
        public readonly record struct TrackListResult(HttpGetResult GetResult, IReadOnlyList<MusicBrainzTrack> Tracks);
        public readonly record struct ArtistCandidateResult(HttpGetResult GetResult, IReadOnlyList<MusicBrainzArtist> Artists);
        public readonly record struct WikidataEntityIdResult(HttpGetResult GetResult, string? EntityId);

        #region Public methods

        // The MusicBrainz releases that may be the album we look for, best first:
        //  1. Search for release groups (albums) with this title and artists, and pick the ones whose
        //     title, first release date and type fit
        //  2. Get all official releases in those groups and rank them by track count, date etc.
        // If no release group fits, we search for the releases directly instead, which finds more
        // but also a lot more noise.
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
            var escapedTitle = EscapePhrase(CollapseWhitespace(title));

            // 1. Release groups
            var (groups, getResult) = await GetAllPagesAsync<ReleaseGroupSearchDto, ReleaseGroupDto>(
                $"release-group/?query={Uri.EscapeDataString($"releasegroup:\"{escapedTitle}\"{who}")}&fmt=json",
                ReleaseGroupPageLimit,
                MaxReleaseGroups,
                body => (body.ReleaseGroups, body.Count),
                "Getting matching albums",
                channelProgress,
                ct).ConfigureAwait(false);

            if (getResult == HttpGetResult.RateLimitedOrServiceUnavailable)
                return new(getResult, []);

            // 2. Their releases
            List<ReleaseCandidateDto> releases = [];
            foreach (var group in groups.PickReleaseGroups(title, releaseDate, releaseType, MaxReleaseGroupsToFetch))
            {
                List<ReleaseCandidateDto> groupReleases;
                (groupReleases, getResult) = await GetAllPagesAsync<ReleaseBrowseDto, ReleaseCandidateDto>(
                    $"release?release-group={Uri.EscapeDataString(group.Id)}&status=official&inc=media+artist-credits+release-groups&fmt=json",
                    ReleasesInGroupPageLimit,
                    MaxReleasesInGroup,
                    body => (body.Releases, body.Count),
                    "Getting the album's releases",
                    channelProgress,
                    ct).ConfigureAwait(false);

                if (getResult == HttpGetResult.RateLimitedOrServiceUnavailable)
                    return new(getResult, []);

                releases.AddRange(groupReleases);
            }

            List<ReleaseCandidateDto> ranked;
            if (releases.Count > 0)
            {
                // They're all releases of the right album, so rank them without leaving any out
                ranked = releases
                    .Select(WithTrackCount)
                    .RankAndFilterReleases(title, numberOfTracks, releaseDate, artistNames, releaseType);
            }
            else
            {
                // Fallback: search the releases themselves
                (releases, getResult) = await GetAllPagesAsync<ReleaseCandidateSearchDto, ReleaseCandidateDto>(
                    $"release/?query={Uri.EscapeDataString($"release:\"{escapedTitle}\"{who} AND status:official")}&fmt=json",
                    ReleaseCandidatePageLimit,
                    MaxReleaseCandidates,
                    body => (body.Releases, body.Count),
                    "Getting matching releases",
                    channelProgress,
                    ct).ConfigureAwait(false);

                if (getResult == HttpGetResult.RateLimitedOrServiceUnavailable)
                    return new(getResult, []);

                ranked = releases
                    .Where(x => x.ReleaseGroup?.Id is not null)
                    .RankAndFilterReleases(title, numberOfTracks, releaseDate, artistNames, releaseType, MinimumNumberOfReleasePoints);
            }

            return new(HttpGetResult.Success, [.. ranked.Select(ToCandidate)]);
        }

        // The releases an mbid the user gave us stands for: the release itself, or if it's a release
        // group's, all of the group's releases, by date and then country (there's nothing to rank
        // them by). MusicBrainz ids are unique across entity types, so trying it as a release first
        // tells which it is. No releases if it's neither.
        public async Task<ReleaseCandidateResult> LookUpReleaseCandidatesAsync(string mbid, ChannelProgress? channelProgress, CancellationToken ct)
        {
            // 1. A release?
            var (release, getResult) = await GetCachedAsync<ReleaseCandidateDto>(
                $"release/{Uri.EscapeDataString(mbid)}?inc=artist-credits+release-groups+media&fmt=json",
                "Looking up the release",
                channelProgress,
                ct).ConfigureAwait(false);

            if (getResult == HttpGetResult.RateLimitedOrServiceUnavailable)
                return new(getResult, []);

            if (release is not null)
                return new(HttpGetResult.Success, [ToCandidate(WithTrackCount(release))]);

            // 2. A release group? Not only the official releases: the user asked for this group.
            List<ReleaseCandidateDto> releases;
            (releases, getResult) = await GetAllPagesAsync<ReleaseBrowseDto, ReleaseCandidateDto>(
                $"release?release-group={Uri.EscapeDataString(mbid)}&inc=media+artist-credits+release-groups&fmt=json",
                ReleasesInGroupPageLimit,
                MaxReleasesInGroup,
                body => (body.Releases, body.Count),
                "Getting the album's releases",
                channelProgress,
                ct).ConfigureAwait(false);

            if (getResult == HttpGetResult.RateLimitedOrServiceUnavailable)
                return new(getResult, []);

            return new(releases.Count > 0 ? HttpGetResult.Success : HttpGetResult.NotFound, [.. releases
                .Select(WithTrackCount)
                // Undated last
                .OrderBy(x => string.IsNullOrEmpty(x.Date))
                .ThenBy(x => x.Date, StringComparer.Ordinal)
                .ThenBy(x => x.Country, StringComparer.Ordinal)
                .Select(ToCandidate)]);
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

        // EntityId is null both when the artist has no Wikidata entity and when MusicBrainz was
        // unavailable; GetResult tells them apart
        public async Task<WikidataEntityIdResult> GetWikidataEntityIdAsync(string artistMbid, ChannelProgress? channelProgress, CancellationToken ct)
        {
            var cacheKey = $"mb:wikidata:{artistMbid}";
            HttpGetResult getResult = HttpGetResult.NotSet;
            var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                RelationsDto? body;
                (body, getResult) = await _httpClient.TryGet<RelationsDto>(
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

            return new(getResult, result);
        }

        public async Task<ArtistCandidateResult> GetArtistAsync(string artistMbid, ChannelProgress? channelProgress, CancellationToken ct)
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

        // Gets one thing (e.g. a lookup by id), cached. Null if MusicBrainz doesn't know it.
        private async Task<(TBody? Body, HttpGetResult Result)> GetCachedAsync<TBody>(string url,
                                                                                    string step,
                                                                                    ChannelProgress? channelProgress,
                                                                                    CancellationToken ct) where TBody : class
        {
            HttpGetResult getResult = HttpGetResult.NotSet;
            var cacheKey = $"mb:get:{url}";
            var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                TBody? body;
                (body, getResult) = await _httpClient.TryGet<TBody>(
                    url,
                    _limiter,
                    _maxNumberOfAttempts,
                    ct,
                    step,
                    channelProgress).ConfigureAwait(false);
                return body;
            }).ConfigureAwait(false);

            // If we failed due to the the MusicBrainz server being unhelpful, let's not cache it
            if (getResult == HttpGetResult.RateLimitedOrServiceUnavailable)
            {
                _cache.Remove(cacheKey);
                return (null, getResult);
            }

            return (result, getResult);
        }

        // Browsed and looked-up releases only have a track count per medium
        private static ReleaseCandidateDto WithTrackCount(ReleaseCandidateDto release)
            => release with { TrackCount = release.TrackCount ?? release.Media?.Sum(m => m.TrackCount ?? 0) };

        private static MusicBrainzReleaseCandidate ToCandidate(ReleaseCandidateDto r)
            => new(
                r.Id,
                r.ReleaseGroup?.Id ?? string.Empty,
                r.Title ?? string.Empty,
                // Each name followed by its own join phrase: "A, B & C" is A + ", " + B + " & " + C + ""
                r.ArtistCredit != null ? string.Concat(r.ArtistCredit.Select(x => x.Name + (x.JoinPhrase ?? string.Empty))) : string.Empty,
                r.ArtistCredit != null ? [.. r.ArtistCredit.Select(x => x.Name)] : [],
                r.ArtistCredit != null ? [.. r.ArtistCredit.Select(x => x.ArtistDto?.Id ?? string.Empty)] : [],
                r.ArtistCredit != null ? [.. r.ArtistCredit.Select(x => x.JoinPhrase ?? string.Empty)] : [],
                // Only known for releases found through or with their release group
                r.ReleaseGroup?.FirstReleaseDate,
                r.Date,
                r.ReleaseGroup?.PrimaryType,
                // Note: I haven't seen more than one media format, so let's just take the first one
                r.Media?.FirstOrDefault()?.Format ?? string.Empty,
                r.Country,
                r.Barcode,
                r.Disambiguation,
                r.TrackCount ?? 0);

        // Gets every page of a search or browse (up to maxItems), cached as a whole. url is
        // everything but the offset and limit. unpack gives the items on a page and the total count.
        private async Task<(List<TItem> Items, HttpGetResult Result)> GetAllPagesAsync<TBody, TItem>(string url,
                                                                                                     int pageLimit,
                                                                                                     int maxItems,
                                                                                                     Func<TBody, (List<TItem>? Items, int Count)> unpack,
                                                                                                     string step,
                                                                                                     ChannelProgress? channelProgress,
                                                                                                     CancellationToken ct) where TBody : class
        {
            HttpGetResult getResult = HttpGetResult.NotSet;
            var cacheKey = $"mb:pages:{url}";
            var result = await _cache.GetOrCreateAsync<List<TItem>>(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                List<TItem> items = [];
                while (true)
                {
                    TBody? body;
                    (body, getResult) = await _httpClient.TryGet<TBody>(
                        $"{url}&offset={items.Count}&limit={pageLimit}",
                        _limiter,
                        _maxNumberOfAttempts,
                        ct,
                        step,
                        channelProgress).ConfigureAwait(false);

                    if (body == null)
                        break;

                    var (page, count) = unpack(body);
                    if (page == null || page.Count == 0)
                        break;

                    items.AddRange(page);
                    if (items.Count >= count || items.Count >= maxItems)
                        break;
                }

                return items;
            }).ConfigureAwait(false);

            // If we failed due to the the MusicBrainz server being unhelpful, let's not cache it,
            // nor use what we got before it failed
            if (getResult == HttpGetResult.RateLimitedOrServiceUnavailable)
            {
                _cache.Remove(cacheKey);
                return ([], getResult);
            }

            return (result ?? [], getResult);
        }

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
            CollapseWhitespace(s).ToLowerInvariant();

        // Not lowercased, unlike Normalize: Lucene only takes AND in capitals as an operator
        private static string CollapseWhitespace(string s) =>
            NormalizeStringRegex().Replace(s.Trim(), " ");

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