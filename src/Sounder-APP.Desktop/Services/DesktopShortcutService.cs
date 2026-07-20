using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Sounder_APP.Models;

namespace Sounder_APP.Services
{
    /// <summary>
    /// 桌面快捷方式服务：在用户桌面创建快捷方式。
    /// 平台特定实现（Windows .url / Linux .desktop）委托给 <see cref="IDesktopShortcutBackend"/>。
    /// </summary>
    public static class DesktopShortcutService
    {
        /// <summary>获取用户桌面路径</summary>
        private static string DesktopPath =>
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        private static readonly IDesktopShortcutBackend _backend = BackendFactory.CreateDesktopShortcutBackend();
        private static readonly IIconGeneratorBackend _iconBackend = BackendFactory.CreateIconGeneratorBackend();

        /// <summary>
        /// 为指定资源创建桌面快捷方式。
        /// </summary>
        public static async Task<bool> CreateShortcutAsync(Resource resource)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    // 生成图标（已存在则跳过）
                    var iconPath = await IconGeneratorService.GeneratePlatformIconAsync(resource);
                    if (iconPath == null) return false;

                    var safeName = SanitizeFileName(resource.Name);
                    var resourceId = Uri.EscapeDataString(resource.Id);
                    var shortcutPath = Path.Combine(DesktopPath, $"{safeName}{_backend.GetShortcutExtension()}");

                    return _backend.CreateShortcut(shortcutPath, safeName, resourceId, iconPath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Shortcut] 创建失败: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>过滤文件名非法字符</summary>
        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new char[name.Length];
            for (int i = 0; i < name.Length; i++)
                sanitized[i] = invalid.Contains(name[i]) ? '_' : name[i];
            var result = new string(sanitized);
            return string.IsNullOrWhiteSpace(result) ? "resource" : result;
        }
    }
}
