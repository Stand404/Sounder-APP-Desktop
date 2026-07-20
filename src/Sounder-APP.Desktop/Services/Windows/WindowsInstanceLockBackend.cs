using System;
using System.Diagnostics;
using System.Threading;

namespace Sounder_APP.Services.Windows
{
    /// <summary>
    /// Windows 单实例锁：基于命名 Mutex（全局内核对象）。
    /// </summary>
    public class WindowsInstanceLockBackend : IInstanceLockBackend
    {
        private const string MutexName = @"Global\SounderApp_SingleInstance";
        private Mutex? _mutex;

        public bool TryAcquire()
        {
            try
            {
                _mutex = new Mutex(true, MutexName, out bool createdNew);
                if (createdNew)
                {
                    Debug.WriteLine("[SingleInstance] 主实例 (Windows Mutex)");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SingleInstance] Mutex 失败: {ex.Message}");
                return false;
            }
        }

        public void Release()
        {
            _mutex?.Dispose();
            _mutex = null;
        }

        public void Dispose() => Release();
    }
}
