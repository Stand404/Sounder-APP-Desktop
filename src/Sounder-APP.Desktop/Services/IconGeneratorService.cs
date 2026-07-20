using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Sounder_APP.Models;

namespace Sounder_APP.Services
{
    /// <summary>
    /// 图标生成服务：将资源图片转为平台的圆角图标文件。
    /// 平台特定格式（Windows .ico / Linux .png）委托给 <see cref="IIconGeneratorBackend"/>。
    /// </summary>
    public static class IconGeneratorService
    {
        private static readonly IIconGeneratorBackend _backend = BackendFactory.CreateIconGeneratorBackend();

        /// <summary>
        /// 为资源生成平台适配的圆角图标（已存在则跳过）。
        /// </summary>
        public static async Task<string?> GeneratePlatformIconAsync(Resource resource)
        {
            var destPath = GetIconPath(resource);
            if (File.Exists(destPath))
            {
                Debug.WriteLine($"[IconGen] 已存在: {destPath}");
                return destPath;
            }

            var sourcePath = GetSourceImagePath(resource);
            if (sourcePath == null)
            {
                Debug.WriteLine("[IconGen] 无可用图标源");
                return null;
            }

            return await Task.Run(() => _backend.GenerateIcon(sourcePath, destPath));
        }

        /// <summary>获取平台适配的图标路径</summary>
        public static string GetIconPath(Resource resource)
        {
            var dir = ResourceService.GetResourceInstallDir(resource.Id);
            return Path.Combine(dir, _backend.GetIconFileName());
        }

        /// <summary>检查平台适配的图标是否已生成</summary>
        public static bool IconExists(Resource resource)
            => File.Exists(GetIconPath(resource));

        private static string? GetSourceImagePath(Resource resource)
        {
            if (!string.IsNullOrEmpty(resource.Icon) && !resource.Icon.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(resource.Icon))
                    return resource.Icon;
            }
            var installedDir = ResourceService.GetResourceInstallDir(resource.Id);
            foreach (var icon in new[] { "icon.jpg", "icon.png", "icon.jpeg" })
            {
                var path = Path.Combine(installedDir, icon);
                if (File.Exists(path)) return path;
            }
            return null;
        }
    }
}
