using System;

namespace Sounder_APP.Services
{
    /// <summary>
    /// 平台特定的单实例锁后端。
    /// </summary>
    public interface IInstanceLockBackend : IDisposable
    {
        /// <summary>尝试获取实例锁。返回 true 表示主实例。</summary>
        bool TryAcquire();
        /// <summary>释放锁资源。</summary>
        void Release();
    }
}
