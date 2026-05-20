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
}
