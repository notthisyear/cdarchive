using System;
using System.Threading;
using System.Threading.Tasks;

namespace CdArchiveBackend.Common
{
    internal abstract class RateLimiter(long minIntervalMs)
    {
        private readonly long _minIntervalMs = minIntervalMs;

        private readonly SemaphoreSlim _requestMutex = new(1, 1);

        private long _notBefore;

        public async Task WaitAsync(CancellationToken ct)
        {
            await _requestMutex.WaitAsync(ct);
            try
            {
                // Re-check after every sleep: a BackOff may have moved the deadline while we waited
                long wait;
                while (true)
                {
                    wait = Volatile.Read(ref _notBefore) - Environment.TickCount64;
                    if (wait <= 0)
                        break;

                    await Task.Delay(TimeSpan.FromMilliseconds(wait), ct);
                }
                AdvanceTo(Environment.TickCount64 + _minIntervalMs);
            }
            finally
            {
                _requestMutex.Release();
            }
        }

        public void BackOff(TimeSpan delay)
            => AdvanceTo(Environment.TickCount64 + (long)delay.TotalMilliseconds);

        private void AdvanceTo(long until)
        {
            long current;
            do
            {
                current = Volatile.Read(ref _notBefore);
                if (current >= until)
                    return;
            } while (Interlocked.CompareExchange(ref _notBefore, until, current) != current);
        }

    }

    // See https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting
    internal sealed class MusicBrainzRateLimiter(long minIntervalMs = 1100) : RateLimiter(minIntervalMs);

    // See https://musicbrainz.org/doc/Cover_Art_Archive/API#Rate_limiting_rules
    internal sealed class CoverArtArchiveRateLimiter(long minIntervalMs = 1100) : RateLimiter(minIntervalMs);

    // See https://www.mediawiki.org/wiki/Wikimedia_APIs/Rate_limits
    internal sealed class WikimediaRateLimiter(long minIntervalMs = 750) : RateLimiter(minIntervalMs);
}
