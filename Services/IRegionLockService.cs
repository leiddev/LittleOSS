namespace LittleOSS.Services;

/// <summary>
/// 区域锁服务接口，提供区域内并发访问的互斥协调
/// </summary>
public interface IRegionLockService
{
    /// <summary>
    /// 获取指定区域的异步锁，锁定期间该区域内的上传/删除操作将互斥执行
    /// </summary>
    /// <param name="region">区域名称</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>
    /// 一个可释放的异步句柄；Dispose 时释放锁。
    /// 若锁已被占用，则等待直到获取成功或被取消。
    /// </returns>
    Task<IAsyncDisposable> AcquireRegionLockAsync(string region, CancellationToken ct = default);

    /// <summary>
    /// 获取指定区域的异步锁，带超时限制
    /// </summary>
    /// <param name="region">区域名称</param>
    /// <param name="timeout">获取锁的最大等待时间</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>
    /// 一个可释放的异步句柄（获取成功时）；获取失败时抛出 <see cref="TimeoutException"/>。
    /// </returns>
    Task<IAsyncDisposable> AcquireRegionLockAsync(string region, TimeSpan timeout, CancellationToken ct = default);
}
