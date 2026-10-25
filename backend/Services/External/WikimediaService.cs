using CdArchiveBackend.Common;
using CdArchiveBackend.Data.External;
using CdArchiveBackend.Data.External.Wikimedia;
using CdArchiveBackend.Endpoints.Artwork;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CdArchiveBackend.Services.External
{
    using static CdArchiveBackend.Common.Utilities;
    using Options = Data.External.Wikimedia.Options;

    internal sealed partial class WikimediaService(HttpClient httpClient,
                                           WikimediaRateLimiter limiter,
                                           IOptions<Options> options,
                                           IMemoryCache cache)
    {
        #region Private fields

        private static readonly TimeSpan FoundTtl = TimeSpan.FromDays(7);
        
        private static readonly TimeSpan MissingTtl = TimeSpan.FromHours(12);

        private readonly HttpClient _httpClient = httpClient;

        private readonly RateLimiter _limiter = limiter;

        private readonly int _maxNumberOfAttempts = options.Value.MaxNumberOfAttempts;

        private readonly string _wikimediaApiUrl = options.Value.WikidataApiUrl;
        
        private readonly string _commonApiUrl = options.Value.CommonsApiUrl;

        private readonly IMemoryCache _cache = cache;
        #endregion

        public async Task<ArtworkResult> GetImageAsync(string wikidataEntityId, ChannelProgress channelProgress, CancellationToken ct)
        {
            if (!ValidateWikidataEntityIdRegex().IsMatch(wikidataEntityId))
                return new (HttpGetResult.NotSet, null);

            var cacheKey = $"wiki:image:{wikidataEntityId}";
            HttpGetResult getResult = HttpGetResult.NotSet;
            var image = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                ArtworkData? imageData;
                (getResult, imageData) = await FetchAsync(wikidataEntityId!, channelProgress, ct).ConfigureAwait(false);
                entry.AbsoluteExpirationRelativeToNow = imageData is null ? MissingTtl : FoundTtl;
                return imageData;
            }).ConfigureAwait(false);

            // If we failed due to the the Wikimedia server being unhelpful, let's not cache it.
            if (getResult == HttpGetResult.RateLimitedOrServiceUnavailable)
                _cache.Remove(cacheKey);

            return new(getResult, image);
        }

        private async Task<(HttpGetResult getResult, ArtworkData? data)> FetchAsync(string wikidataEntityId, ChannelProgress channelProgress, CancellationToken ct)
        {
            var (getResult, imageName) = await GetImageFileNameAsync(wikidataEntityId, channelProgress, ct);
            if (string.IsNullOrEmpty(imageName))
                return (getResult, null);

            // File info + license/author for a 500px rendition
            var url = $"{_commonApiUrl}api.php?action=query&format=json&formatversion=2"
                    + "&prop=imageinfo&iiprop=url%7Cextmetadata&iiurlwidth=500"
                    + "&titles=" + Uri.EscapeDataString("File:" + imageName);

            var result = await _httpClient.TryGet<CommonsQueryResultDto>(
                url,
                _limiter,
                _maxNumberOfAttempts,
                ct,
                "Fetching image",
                channelProgress).ConfigureAwait(false); 

            if (result.data == null ||
                result.data.Query == null ||
                result.data.Query.Pages == null ||
                result.data.Query.Pages.Count != 1 ||
                result.data.Query.Pages[0] == null)
            {
                return (result.result, null);
            }

            var p = result.data.Query.Pages[0]!;
            if (p.Missing ?? false)
                return (result.result, null);

            if (p.ImageInfo == null ||
                p.ImageInfo.Count != 1 ||
                p.ImageInfo[0] == null)
            {
                return (result.result, null);
            }

            var imageInfo = p.ImageInfo[0]!;

            return (result.result, new ArtworkData(
                Title: imageName,
                Url: EnsureHttpsAndValidateHost(imageInfo.ThumbUrl, ValidateHostname) ?? string.Empty,
                OriginalUrl: EnsureHttpsAndValidateHost(imageInfo.Url, ValidateHostname),
                Source: "wikimedia",
                Author: StripHtml(imageInfo.ExtMetadata?.Artist?.Value),
                License: imageInfo.ExtMetadata?.LicenseShortName?.Value,
                LicenseUrl: EnsureHttpsAndValidateHost(imageInfo.ExtMetadata?.LicenseUrl?.Value, ValidateHostname),
                SourcePageUrl: EnsureHttpsAndValidateHost(imageInfo.DescriptionUrl, ValidateHostname),
                AttributionRequired: CompareNullableStrings(imageInfo.ExtMetadata?.AttributionRequired?.Value, "true")));
        }

        private async Task<(HttpGetResult getResult, string imageName)> GetImageFileNameAsync(string wikidataEntityId, ChannelProgress channelProgress, CancellationToken ct)
        {
            var result = await _httpClient.TryGet<WbGetClaimsDto>(
                $"{_wikimediaApiUrl}api.php?action=wbgetclaims&entity={wikidataEntityId}&property=P18&format=json",
                _limiter,
                _maxNumberOfAttempts,
                ct,
                "Fetching image name",
                channelProgress).ConfigureAwait(false);

            if (result.data == null || result.data.Claim == null || result.data.Claim.P18Claims == null)
                return (result.result, string.Empty);

            string? fallback = null;
            foreach (var claim in result.data.Claim.P18Claims)
            {
                if (CompareNullableStrings(claim.Rank, "deprecated"))
                    continue;

                var v = claim.Snak?.DataValue?.Value ?? string.Empty;
                if (string.IsNullOrEmpty(v))
                    continue;

                if (CompareNullableStrings(claim.Rank, "preferred"))
                    return (result.result, v);

                fallback ??= v;
            }

            return (result.result, fallback ?? string.Empty);
        }

        // Note: The value in the "Artist" field of the extmetadata section that we get from Wikidata can contain all kinds of stuff.
        //       To not pass along any HTML (that the frontend could possibly run by accident), we try to ensure that we only
        //       send back plain text.
        private static string? StripHtml(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return null;

            // Line breaks and block ends separate separate authors; other tags just disappear
            var withBreaks = HtmlLineAndBlockBreaksRegex().Replace(html, "; ");

            var text = WebUtility.HtmlDecode(HtmlStartAndEndTagsRegex().Replace(withBreaks, ""));
            text = CollapseWhitespaceRegex().Replace(text, " ");
            text = FindSemicolonRegex().Replace(text, "; ").Trim(' ', ';');

            return text.Length == 0 ? null : text;
        }

        private static bool ValidateHostname(string hostname)
            => hostname.Equals("upload.wikimedia.org", StringComparison.Ordinal) ||
            hostname.Equals("thumb.wikimedia.org", StringComparison.Ordinal) ||
            hostname.Equals("commons.wikimedia.org", StringComparison.Ordinal) ||
            hostname.Equals("creativecommons.org", StringComparison.Ordinal);

        [GeneratedRegex(@"^Q\d+$")]
        private static partial Regex ValidateWikidataEntityIdRegex();
        
        [GeneratedRegex(@"<\s*br\s*/?\s*>|</\s*(?:p|div|li|tr)\s*>", RegexOptions.IgnoreCase, "sv-SE")]
        private static partial Regex HtmlLineAndBlockBreaksRegex();
        
        [GeneratedRegex("<[^>]+>")]
        private static partial Regex HtmlStartAndEndTagsRegex();
        
        [GeneratedRegex(@"\s+")]
        private static partial Regex CollapseWhitespaceRegex();

        [GeneratedRegex(@"(\s*;\s*)+")]
        private static partial Regex FindSemicolonRegex();
    }
}
