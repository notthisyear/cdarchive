using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace CdArchiveBackend.Common
{
    internal static class Utilities
    {
        public static bool CompareNullableStrings(string? a, string? b)
        {
            if (string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b))
                return false;
            else if (!string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
                return false;
            else if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
                return true;
            else
                return a!.Equals(b, StringComparison.Ordinal);
        }

        public static bool TryExtractUserId(this ClaimsPrincipal user, out int userId)
            => int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty, NumberStyles.Integer, CultureInfo.InvariantCulture, out userId);

        public static TimeSpan? GetRetryDelay(this HttpResponseMessage response)
        {
            var header = response.Headers.RetryAfter;
            // Note: The format is either Retry-After: 5 (delta) or  "Retry-After: <http-date>" (date)
            return header?.Delta ?? (header?.Date is DateTimeOffset date ? (date - DateTimeOffset.UtcNow) : null);
        }

        public enum HttpGetResult
        {
            NotSet,
            NotFound,
            RateLimitedOrServiceUnavailable,
            Success
        }

        public static async Task<(T? data, HttpGetResult result)> TryGet<T>(this HttpClient httpClient,
                                                                            string url,
                                                                            RateLimiter limiter,
                                                                            int maxNumberOfAttempts,
                                                                            CancellationToken ct,
                                                                            string step = "",
                                                                            ChannelProgress? channelProgress = null) where T : class
        {
            T? body = null;
            HttpGetResult result = HttpGetResult.NotSet;
            var numberOfAttempts = 0;
            while (!ct.IsCancellationRequested && numberOfAttempts < maxNumberOfAttempts)
            {
                (result, body) = await httpClient.GetJsonAsync<T>(
                    url,
                    limiter,
                    step,
                    numberOfAttempts,
                    maxNumberOfAttempts,
                    channelProgress,
                    ct).ConfigureAwait(false);
                
                if (result == HttpGetResult.Success)
                    break;

                if (result == HttpGetResult.NotFound)
                    return (null, result);

                numberOfAttempts++;
                channelProgress?.Report(new("retrying", Attempt: numberOfAttempts, Of: maxNumberOfAttempts));
            }

            return (body, result);
        }

        private static async Task<(HttpGetResult searchResult, T? data)> GetJsonAsync<T>(this HttpClient httpClient,
                                                                                         string url,
                                                                                         RateLimiter limiter,
                                                                                         string step,
                                                                                         int numberOfAttempts,
                                                                                         int maxNumberOfAttempts,
                                                                                         ChannelProgress? channelProgress,
                                                                                         CancellationToken ct) where T : class
        {
            channelProgress?.Report(new("queued"));
            await limiter.WaitAsync(ct).ConfigureAwait(false);
            channelProgress?.Report(new("requesting", Step: step, Attempt: numberOfAttempts, Of: maxNumberOfAttempts));

            using var response = await httpClient.GetAsync(url, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return (HttpGetResult.NotFound, null);

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                response.StatusCode == HttpStatusCode.TooManyRequests ||
                response.StatusCode == HttpStatusCode.InternalServerError)
            {
                var retryDelay = response.GetRetryDelay();
                if (retryDelay != null && retryDelay.Value.TotalMilliseconds > 0)
                    limiter.BackOff((TimeSpan)retryDelay);

                return (HttpGetResult.RateLimitedOrServiceUnavailable, null);
            }

            response.EnsureSuccessStatusCode();
            return (HttpGetResult.Success, await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct).ConfigureAwait(false));
        }

        public static string? EnsureHttpsAndValidateHost(string? url, Func<string, bool> isAllowedHost)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            // Protocol-relative URLs ("//upload.wikimedia.org/...") show up in some API output
            if (url.StartsWith("//", StringComparison.Ordinal))
                url = "https:" + url;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;

            // Rules out ftp:, javascript:, data: and the like
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return null;

            if (!isAllowedHost(uri.Host))
                return null;

            return new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri.AbsoluteUri;
        }
    }
}
