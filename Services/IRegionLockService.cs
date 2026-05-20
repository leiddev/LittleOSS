namespace LittleOSS.Services;

public interface IRegionLockService
{
    Task<IAsyncDisposable> AcquireRegionLockAsync(string region, CancellationToken ct = default);
}
