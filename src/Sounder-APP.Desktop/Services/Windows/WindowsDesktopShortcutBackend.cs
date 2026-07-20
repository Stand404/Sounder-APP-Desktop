using System.IO;

namespace Sounder_APP.Services.Windows
{
    /// <summary>
    /// Windows 桌面快捷方式后端：创建 .url 快捷方式文件。
    /// </summary>
    public class WindowsDesktopShortcutBackend : IDesktopShortcutBackend
    {
        public string GetShortcutExtension() => ".url";

        public bool CreateShortcut(string shortcutPath, string displayName, string resourceId, string iconPath)
        {
            var sounderUrl = $"sounder://play/?resourceId={resourceId}";
            var content = "[InternetShortcut]\r\n" +
                          $"URL={sounderUrl}\r\n" +
                          $"IconFile={iconPath}\r\n" +
                          "IconIndex=0\r\n";
            File.WriteAllText(shortcutPath, content);
            return true;
        }
    }
}
