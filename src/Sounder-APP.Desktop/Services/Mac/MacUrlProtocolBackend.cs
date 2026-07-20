using System;
using System.Diagnostics;
using System.IO;

namespace Sounder_APP.Services.Mac
{
    /// <summary>
    /// macOS URL 协议后端：通过 plist + URL 类型注册 sounder:// 协议。
    /// </summary>
    public class MacUrlProtocolBackend : IUrlProtocolBackend
    {
        private const string ProtocolName = "sounder";
        private static readonly string LaunchServicesPlist = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "..", "Library", "Preferences", "com.sounder-app.plist");
        private static readonly string PlistBuddy = "/usr/libexec/PlistBuddy";

        public void RegisterProtocol(string exePath)
        {
            try
            {
                // 使用 PlistBuddy 注册 URL scheme（macOS 原生方式）
                RunPlistBuddy($"Add :CFBundleURLTypes array");
                RunPlistBuddy($"Add :CFBundleURLTypes:0 dict");
                RunPlistBuddy($"Add :CFBundleURLTypes:0:CFBundleURLName string {ProtocolName}");
                RunPlistBuddy($"Add :CFBundleURLTypes:0:CFBundleURLSchemes array");
                RunPlistBuddy($"Add :CFBundleURLTypes:0:CFBundleURLSchemes:0 string {ProtocolName}");

                // 创建辅助 .app 注册（用于 Launch Services）
                var appsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "..", "Library", "Application Support", "Sounder-APP");
                appsDir = Path.GetFullPath(appsDir);
                Directory.CreateDirectory(appsDir);

                var plistPath = Path.Combine(appsDir, "sounder-protocol.plist");
                var plist = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>CFBundleIdentifier</key>
    <string>com.sounder-app.protocol</string>
    <key>CFBundleName</key>
    <string>Sounder Protocol Handler</string>
    <key>CFBundleExecutable</key>
    <string>Sounder-APP</string>
    <key>CFBundleURLTypes</key>
    <array>
        <dict>
            <key>CFBundleURLName</key>
            <string>Sounder Protocol</string>
            <key>CFBundleURLSchemes</key>
            <array>
                <string>sounder</string>
            </array>
        </dict>
    </array>
</dict>
</plist>";
                File.WriteAllText(plistPath, plist);

                // 刷新 Launch Services
                try { Process.Start("open", "-a sounds-er-protocol"); } catch { }
                try { Process.Start("/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister", $"-f \"{plistPath}\""); } catch { }

                Debug.WriteLine($"[UrlProtocol] 协议 {ProtocolName}:// 已注册 (macOS)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UrlProtocol] 注册 macOS 协议失败: {ex.Message}");
            }
        }

        public void UnregisterProtocol()
        {
            try
            {
                var appsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "..", "Library", "Application Support", "Sounder-APP");
                appsDir = Path.GetFullPath(appsDir);
                var plistPath = Path.Combine(appsDir, "sounder-protocol.plist");

                if (File.Exists(plistPath))
                    File.Delete(plistPath);

                try
                {
                    Process.Start("/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister", $"-u \"{plistPath}\"");
                }
                catch { }

                Debug.WriteLine($"[UrlProtocol] 协议 {ProtocolName}:// 已注销 (macOS)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UrlProtocol] 注销 macOS 协议失败: {ex.Message}");
            }
        }

        private static void RunPlistBuddy(string command)
        {
            try
            {
                var psi = new ProcessStartInfo(PlistBuddy)
                {
                    ArgumentList = { "-c", command, LaunchServicesPlist },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(2000);
            }
            catch { }
        }
    }
}
