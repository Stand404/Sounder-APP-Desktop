using System;
using System.Diagnostics;
using System.IO;

namespace Sounder_APP.Services.Mac
{
    /// <summary>
    /// macOS 单实例锁：基于文件锁 (FileMode.CreateNew + FileShare.None + DeleteOnClose)。
    /// macOS 是 Unix 系统，使用与 Linux 相同的文件锁机制。
    /// </summary>
    public class MacInstanceLockBackend : IInstanceLockBackend
    {
        private static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Sounder-APP");
        private static readonly string LockFilePath = Path.Combine(DataDir, "instance.lock");
        private FileStream? _lockFile;

        public bool TryAcquire()
        {
            try
            {
                Directory.CreateDirectory(DataDir);

                _lockFile = TryCreateLockFile();
                if (_lockFile == null)
                {
                    if (IsStaleLock())
                    {
                        Debug.WriteLine("[SingleInstance] 清理死锁文件");
                        try { File.Delete(LockFilePath); } catch { }
                        _lockFile = TryCreateLockFile();
                    }
                }

                if (_lockFile == null)
                {
                    Debug.WriteLine("[SingleInstance] 已有实例 (File Lock)");
                    return false;
                }

                var pidBytes = System.Text.Encoding.UTF8.GetBytes(Environment.ProcessId.ToString());
                _lockFile.Write(pidBytes, 0, pidBytes.Length);
                _lockFile.Flush();

                Debug.WriteLine("[SingleInstance] 主实例 (File Lock)");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SingleInstance] 文件锁失败: {ex.Message}");
                return false;
            }
        }

        private static FileStream? TryCreateLockFile()
        {
            try
            {
                return new FileStream(LockFilePath, FileMode.CreateNew,
                    FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (IOException)
            {
                return null;
            }
        }

        private static bool IsStaleLock()
        {
            try
            {
                if (!File.Exists(LockFilePath)) return false;
                var content = File.ReadAllText(LockFilePath).Trim();
                if (int.TryParse(content, out int pid))
                {
                    var proc = Process.GetProcessById(pid);
                    return !proc.ProcessName.Contains("Sounder", StringComparison.OrdinalIgnoreCase) &&
                           !proc.ProcessName.Contains("发声", StringComparison.OrdinalIgnoreCase);
                }
                return true;
            }
            catch (ArgumentException) { return true; }
            catch (Exception) { return false; }
        }

        public void Release()
        {
            _lockFile?.Dispose();
            _lockFile = null;
            try { if (File.Exists(LockFilePath)) File.Delete(LockFilePath); } catch { }
        }

        public void Dispose() => Release();
    }
}
