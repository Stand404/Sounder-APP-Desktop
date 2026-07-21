using System;
using System.Diagnostics;
using System.IO;

namespace Sounder_APP.Services.Mac
{
    /// <summary>
    /// macOS 桌面快捷方式后端：创建最小 .app 包替代 .command，彻底避免 Terminal 弹窗。
    /// .app 内部启动器直接运行 Sounder-APP 二进制（非 open 命令），
    /// 确保 URL 作为命令行参数传递；若已有实例运行，IPC 自动转发。
    /// </summary>
    public class MacDesktopShortcutBackend : IDesktopShortcutBackend
    {
        public string GetShortcutExtension() => ".app";

        public bool CreateShortcut(string shortcutPath, string displayName, string resourceId, string iconPath)
        {
            try
            {
                var sounderUrl = $"sounder://play/?resourceId={resourceId}";

                // 定位当前运行的 .app 包路径
                // Environment.ProcessPath 示例: /Applications/Sounder-APP.app/Contents/MacOS/Sounder-APP
                var exePath = Environment.ProcessPath ?? "";
                var appBundlePath = exePath;
                // 向上回溯找到 .app 目录
                for (int i = 0; i < 3; i++)
                {
                    var parent = Path.GetDirectoryName(appBundlePath);
                    if (parent == null) break;
                    appBundlePath = parent;
                }
                if (!appBundlePath.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                    appBundlePath = "/Applications/Sounder-APP.app"; // 兜底

                // 创建 .app 包结构
                var contentsDir = Path.Combine(shortcutPath, "Contents");
                var macosDir = Path.Combine(contentsDir, "MacOS");
                Directory.CreateDirectory(macosDir);

                // Info.plist — LSUIElement=true 隐藏 dock 图标和菜单栏
                var infoPlist = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
	<key>CFBundleExecutable</key>
	<string>launcher</string>
	<key>CFBundleIdentifier</key>
	<string>com.sounderapp.shortcut</string>
	<key>CFBundlePackageType</key>
	<string>APPL</string>
	<key>LSUIElement</key>
	<true/>
</dict>
</plist>";
                File.WriteAllText(Path.Combine(contentsDir, "Info.plist"), infoPlist);

                // 启动器脚本 — 直接运行 Sounder-APP 二进制，URL 作为 argv 传入
                var launcherPath = Path.Combine(macosDir, "launcher");
                var launcherContent = $"#!/bin/bash\n\"{appBundlePath}/Contents/MacOS/Sounder-APP\" \"{sounderUrl}\" &\n";
                File.WriteAllText(launcherPath, launcherContent);

                // 设为可执行
                if (OperatingSystem.IsMacOS())
                    File.SetUnixFileMode(launcherPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

                // 注入自定义图标
                if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                    InjectIcon(shortcutPath, iconPath);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MacShortcut] 创建 .app 快捷方式失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 使用 osascript (AppKit) 为 .app 包设置自定义图标。
        /// </summary>
        private static void InjectIcon(string shortcutPath, string iconPath)
        {
            try
            {
                var psi = new ProcessStartInfo("osascript")
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };

                var script = $"use framework \"AppKit\"\n" +
                             $"set iconPath to \"{iconPath}\"\n" +
                             $"set filePath to \"{shortcutPath}\"\n" +
                             $"set iconImg to current application's NSImage's alloc()'s initWithContentsOfFile:iconPath\n" +
                             $"current application's NSWorkspace's sharedWorkspace()'s " +
                             $"setIcon:iconImg forFile:filePath options:0";
                psi.ArgumentList.Add("-e");
                psi.ArgumentList.Add(script);

                using var process = Process.Start(psi);
                if (process == null) return;

                process.WaitForExit(5000);

                if (process.ExitCode != 0)
                {
                    var error = process.StandardError.ReadToEnd();
                    Debug.WriteLine($"[MacShortcut] osascript 设置图标失败 (exit={process.ExitCode}): {error}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MacShortcut] 注入图标异常: {ex.Message}");
            }
        }
    }
}
