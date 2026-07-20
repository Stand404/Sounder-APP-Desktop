using System;
using System.Diagnostics;
using System.IO;

namespace Sounder_APP.Services.Linux
{
    /// <summary>
    /// Linux URL 协议后端：通过 .desktop 文件 + xdg-mime 注册 sounder:// 协议。
    /// </summary>
    public class LinuxUrlProtocolBackend : IUrlProtocolBackend
    {
        private const string ProtocolName = "sounder";

        public void RegisterProtocol(string exePath)
        {
            try
            {
                var appsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "..", "local", "share", "applications");
                appsDir = Path.GetFullPath(appsDir);
                Directory.CreateDirectory(appsDir);

                var desktopPath = Path.Combine(appsDir, "sounder-protocol.desktop");
                var content = "[Desktop Entry]\n" +
                              "Type=Application\n" +
                              "Name=Sounder Protocol Handler\n" +
                              $"Exec=\"{exePath}\" %u\n" +
                              "StartupNotify=false\n" +
                              "MimeType=x-scheme-handler/sounder;\n" +
                              "NoDisplay=true\n";

                File.WriteAllText(desktopPath, content);
                try { Process.Start("chmod", $"+x \"{desktopPath}\""); } catch { }
                try { Process.Start("update-desktop-database", $"\"{appsDir}\""); } catch { }

                Debug.WriteLine($"[UrlProtocol] 协议 {ProtocolName}:// 已注册 (Linux)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UrlProtocol] 注册 Linux 协议失败: {ex.Message}");
            }
        }

        public void UnregisterProtocol()
        {
            try
            {
                var appsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "..", "local", "share", "applications");
                appsDir = Path.GetFullPath(appsDir);
                var desktopPath = Path.Combine(appsDir, "sounder-protocol.desktop");

                if (File.Exists(desktopPath))
                    File.Delete(desktopPath);

                try { Process.Start("update-desktop-database", $"\"{appsDir}\""); } catch { }

                Debug.WriteLine($"[UrlProtocol] 协议 {ProtocolName}:// 已注销 (Linux)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UrlProtocol] 注销 Linux 协议失败: {ex.Message}");
            }
        }
    }
}
