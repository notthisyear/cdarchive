using CdArchiveBackend.Common;
using CdArchiveBackend.Data.External;
using CdArchiveBackend.Data.External.CoverArtArchive;
using CdArchiveBackend.Endpoints.Artwork;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static CdArchiveBackend.Common.Utilities;

namespace CdArchiveBackend.Services.External
{
    using Options = Data.External.CoverArtArchive.Options;

    internal sealed class CoverArtService(HttpClient httpClient,
                                          CoverArtArchiveRateLimiter limiter,
                                          IOptions<Options> options,
                                          IMemoryCache cache)
    {
        private static readonly TimeSpan FoundTtl = TimeSpan.FromDays(7);
        private static readonly TimeSpan MissingTtl = TimeSpan.FromHours(12);

        private readonly HttpClient _httpClient = httpClient;
        private readonly IMemoryCache _cache = cache;
        private readonly RateLimiter _limiter = limiter;
        private readonly int _maxNumberOfAttempts = options.Value.MaxNumberOfAttempts;

        public async Task<ArtworkResult> GetFrontCoverAsync(string mbid,
                                                            ChannelProgress channelProgress,
                                                            CancellationToken ct)
        {
            if (string.IsNullOrEmpty(mbid))
                return new(HttpGetResult.NotSet, null);

            HttpGetResult getResult = HttpGetResult.NotSet;
            var cacheKey = $"front-cover-photo:{mbid}";
            var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                CoverArtArchiveResponseDto? body;
                (body, getResult) = await _httpClient.TryGet<CoverArtArchiveResponseDto>(
                    $"release/{mbid}",
                    _limiter,
                    _maxNumberOfAttempts,
                    ct,
                    "Getting album artwork",
                    channelProgress).ConfigureAwait(false);

                if (getResult == HttpGetResult.NotFound)
                {
                    entry.AbsoluteExpirationRelativeToNow = MissingTtl;
                    return null;
                }

                var image = body?.Images?.FirstOrDefault(i => i.Front && i.Approved);
                var url = PickUrl(image);

                if (url is null)
                {
                    entry.AbsoluteExpirationRelativeToNow = MissingTtl;
                    return null;
                }

                entry.AbsoluteExpirationRelativeToNow = FoundTtl;
                return new ArtworkData(
                    Title: "frontcover",
                    Url: url,
                    OriginalUrl: EnsureHttpsAndValidateHost(image!.Image, HostnameIsValid),
                    Source: "coverartarchive",
                    Author: null,
                    License: null,
                    LicenseUrl: null,
                    SourcePageUrl: body?.ReleasePageUrl,
                    AttributionRequired: false);
            }).ConfigureAwait(false);

            // If we failed due to the the CoverArtArchive server being unhelpful, let's not cache it.
            if (getResult == HttpGetResult.RateLimitedOrServiceUnavailable)
                _cache.Remove(cacheKey);

            return new(getResult, result);
        }

        private static string? PickUrl(CoverArtArchiveImageDto? image)
        {
            if (image is null)
                return null;

            string? url = null;
            if (image.Thumbnails is Dictionary<string, string> t)
                url = t.GetValueOrDefault("500") ?? t.GetValueOrDefault("large") ?? t.GetValueOrDefault("250");

            return EnsureHttpsAndValidateHost(url ?? image.Image, HostnameIsValid);
        }

        private static bool HostnameIsValid(string hostname)
            => hostname.Equals("coverartarchive.org", StringComparison.Ordinal) ||
            hostname.Equals("archive.org", StringComparison.Ordinal) ||
            hostname.EndsWith(".archive.org", StringComparison.Ordinal);
    }
}
