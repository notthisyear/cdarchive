using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CdArchiveBackend.Common
{
    internal sealed class RequestStore<T> : IAsyncDisposable
    {

        #region Private fields
        private readonly ConcurrentDictionary<ulong, (DateTime expiresAt, T[] entries)> _store = [];
        private readonly int _storeEntryTimeoutMs;
        private readonly Task _clearExpiredCacheEntriesTask;
        private readonly PeriodicTimer _clearExpiredCacheEntriesTimer;
        private readonly CancellationTokenSource _cts = new();
        private ulong _nextId = 0;
        private int _disposed = 0;
        #endregion

        public RequestStore(int storeEntryTimeoutMs, TimeProvider? timeProvider = null)
        {
            _storeEntryTimeoutMs = storeEntryTimeoutMs;
            _clearExpiredCacheEntriesTimer = new(
                TimeSpan.FromMilliseconds(Math.Clamp(storeEntryTimeoutMs / 2, 10_000, 60_000)),
                timeProvider ?? TimeProvider.System);
            _clearExpiredCacheEntriesTask = CheckForExpiredEntries();
        }

        #region Public methods
        public ulong AddCacheEntries(IEnumerable<T> entries)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

            var id = Interlocked.Increment(ref _nextId);
            _ = _store.TryAdd(id, (DateTime.UtcNow.AddMilliseconds(_storeEntryTimeoutMs), entries.ToArray()));

            Console.WriteLine($"Added {id} to cache ({entries.Count()} entries)");
            return id;
        }

        public T[]? TryGetAndClearCacheEntries(ulong id)
        {
            if (!_store.TryRemove(id, out var v))
                return null;

            if (v.expiresAt <= DateTime.UtcNow)
                return null;

            return [.. v.entries];
        }
        #endregion

        private async Task CheckForExpiredEntries()
        {
            try
            {
                while (await _clearExpiredCacheEntriesTimer.WaitForNextTickAsync(_cts.Token).ConfigureAwait(false))
                {
                    var dt = DateTime.UtcNow;
                    List<ulong> idsToDelete = [];
                    foreach (var entry in _store)
                    {
                        if (entry.Value.expiresAt < dt)
                            idsToDelete.Add(entry.Key);
                    }

                    Console.WriteLine($"Found {idsToDelete.Count} entries to clear");

                    foreach (var id in idsToDelete)
                        _ = _store.TryRemove(id, out _);
                }
            }
            catch (OperationCanceledException) { }
        }


        #region Disposal
        public async ValueTask DisposeAsync()
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;

            Interlocked.Exchange(ref _disposed, 1);

            _cts.Cancel();
            try
            {
                await _clearExpiredCacheEntriesTask.ConfigureAwait(false);
            }
            finally
            {
                _clearExpiredCacheEntriesTimer.Dispose();
                _store.Clear();
                _cts.Dispose();
            }

            GC.SuppressFinalize(this);
        }
        #endregion
    }
}