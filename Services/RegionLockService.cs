using System.Collections.Concurrent;

namespace LittleOSS.Services;

public class RegionLockService : IRegionLockService
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _regionLocks = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IAsyncDisposable> AcquireRegionLockAsync(string region, CancellationToken ct = default)
    {
        var semaphore = _regionLocks.GetOrAdd(region, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(ct);

        return new RegionLockReleaser(semaphore);
    }

    private sealed class RegionLockReleaser : IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        public RegionLockReleaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                _semaphore.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
