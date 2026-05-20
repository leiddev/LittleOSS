using System.Collections.Concurrent;

namespace LittleOSS.Services;

/// <summary>
/// 区域锁服务实现，使用 SemaphoreSlim 为每个区域提供线程安全的互斥锁
/// </summary>
public class RegionLockService : IRegionLockService
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _regionLocks = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<IAsyncDisposable> AcquireRegionLockAsync(string region, CancellationToken ct = default)
    {
        var semaphore = _regionLocks.GetOrAdd(region, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(ct);

        return new RegionLockReleaser(semaphore);
    }

    /// <summary>
    /// 区域锁释放器，用于 <see cref="IRegionLockService.AcquireRegionLockAsync"/> 返回值的 Dispose 逻辑
    /// </summary>
    private sealed class RegionLockReleaser : IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        /// <summary>构造函数</summary>
        /// <param name="semaphore">关联的信号量实例</param>
        public RegionLockReleaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        /// <inheritdoc />
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
