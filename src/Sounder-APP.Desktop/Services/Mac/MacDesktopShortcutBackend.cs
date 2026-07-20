using System;
using System.IO;

namespace Sounder_APP.Services.Mac
{
    /// <summary>
    /// macOS 桌面快捷方式后端：创建 .webloc 快捷方式文件。
    /// macOS 原生使用 .webloc 格式存储 URL 快捷方式。
    /// </summary>
    public class MacDesktopShortcutBackend : IDesktopShortcutBackend
    {
        public string GetShortcutExtension() => ".webloc";

        public bool CreateShortcut(string shortcutPath, string displayName, string resourceId, string iconPath)
        {
            var sounderUrl = $"sounder://play/?resourceId={resourceId}";

            // macOS .webloc 是 XML plist 格式
            var content = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>URL</key>
    <string>" + sounderUrl + @"</string>
</dict>
</plist>";

            File.WriteAllText(shortcutPath, content);
            return true;
        }
    }
}
