using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Sounder_APP.Services
{
    /// <summary>
    /// 单实例管理服务：
    ///   平台特定锁机制委托给 <see cref="IInstanceLockBackend"/>（Windows: Mutex / Linux: 文件锁）。
    ///   IPC 消息传递通过文件轮询实现，所有平台通用。
    /// </summary>
    public static class SingleInstanceService
    {
        public const string ShowWindowMessage = "__SHOW_WINDOW__";

        private static IInstanceLockBackend? _lockBackend;
        private static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Sounder-APP");
        private static readonly string IpcFilePath = Path.Combine(DataDir, "ipc_msg.txt");
        private static CancellationTokenSource? _ipcCts;

        /// <summary>收到外部 IPC 消息时触发</summary>
        public static event Action<string>? MessageReceived;

        /// <summary>
        /// 尝试申请单实例互斥体。返回 true 表示第一个实例；
        /// false 表示已有实例，参数会自动转发。
        /// </summary>
        public static bool TryAcquire(string[] args)
        {
            _lockBackend = BackendFactory.CreateInstanceLockBackend();

            if (_lockBackend.TryAcquire())
                return true;

            // 兜底：进程名扫描
            if (TryProcessCheck())
                return true;

            // 确认已有实例 → 转发参数并退出
            Debug.WriteLine("[SingleInstance] 检测到已有实例");
            if (args.Length > 0)
            {
                var message = string.Join(" ", args);
                SendToMainInstance(message);
            }
            else
            {
                SendToMainInstance(ShowWindowMessage);
            }
            return false;
        }

        /// <summary>进程名扫描（兜底）</summary>
        private static bool TryProcessCheck()
        {
            try
            {
                var current = Process.GetCurrentProcess();
                var processes = Process.GetProcessesByName(current.ProcessName);
                if (processes.Length > 1)
                    return false;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SingleInstance] 进程检测失败: {ex.Message}");
                return true;
            }
        }

        private static void SendToMainInstance(string message)
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                File.WriteAllText(IpcFilePath, message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SingleInstance] 转发失败: {ex.Message}");
            }
        }

        /// <summary>启动 IPC 监听器</summary>
        public static void StartIpcListener(CancellationToken cancellationToken = default)
        {
            _ipcCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var thread = new Thread(() => ListenLoop(_ipcCts.Token))
            {
                IsBackground = true,
                Name = "SounderIpcListener"
            };
            thread.Start();
        }

        /// <summary>停止 IPC 监听器</summary>
        public static void StopIpcListener() => _ipcCts?.Cancel();

        private static void ListenLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (File.Exists(IpcFilePath))
                    {
                        var content = File.ReadAllText(IpcFilePath).Trim();
                        File.Delete(IpcFilePath);
                        if (!string.IsNullOrEmpty(content))
                        {
                            MessageReceived?.Invoke(content);
                            continue;
                        }
                    }
                    ct.WaitHandle.WaitOne(500);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SingleInstance] IPC 异常: {ex.Message}");
                    try { Thread.Sleep(1000); } catch { break; }
                }
            }
        }

        /// <summary>释放资源</summary>
        public static void Dispose()
        {
            StopIpcListener();
            _lockBackend?.Dispose();
            _lockBackend = null;
            try
            {
                var lockFilePath = Path.Combine(DataDir, "instance.lock");
                if (File.Exists(lockFilePath)) File.Delete(lockFilePath);
            }
            catch { }
        }
    }
}
