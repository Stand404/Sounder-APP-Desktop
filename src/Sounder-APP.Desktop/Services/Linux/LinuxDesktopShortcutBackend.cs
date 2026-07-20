using System;
using System.IO;
using System.Diagnostics;

namespace Sounder_APP.Services.Linux
{
    /// <summary>
    /// Linux 桌面快捷方式后端：创建 .desktop 快捷方式文件。
    /// </summary>
    public class LinuxDesktopShortcutBackend : IDesktopShortcutBackend
    {
        public string GetShortcutExtension() => ".desktop";

        public bool CreateShortcut(string shortcutPath, string displayName, string resourceId, string iconPath)
        {
            var exePath = Environment.ProcessPath
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounder-APP");

            var content = "[Desktop Entry]\n" +
                          "Type=Application\n" +
                          $"Name={displayName}\n" +
                          $"Exec=\"{exePath}\" sounder://play/?resourceId={resourceId}\n" +
                          $"Icon={iconPath}\n" +
                          "Terminal=false\n" +
                          "Categories=Audio;Education;\n" +
                          $"Comment=发声APP - {displayName}\n";

            File.WriteAllText(shortcutPath, content);
            try { Process.Start("chmod", $"+x \"{shortcutPath}\""); } catch { }

            return true;
        }
    }
}
