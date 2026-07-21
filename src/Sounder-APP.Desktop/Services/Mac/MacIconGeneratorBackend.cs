using System;
using System.IO;
using SkiaSharp;

namespace Sounder_APP.Services.Mac
{
    /// <summary>
    /// macOS 图标后端：生成 512x512 圆角 PNG（用于 .command 快捷方式图标）。
    /// </summary>
    public class MacIconGeneratorBackend : IIconGeneratorBackend
    {
        // 512px 确保 Retina 屏幕下图标清晰
        private const int IconSize = 512;

        public string GetIconFileName() => "icon.png";

        public string? GenerateIcon(string sourcePath, string destPath)
        {
            try
            {
                using var inputStream = File.OpenRead(sourcePath);
                using var skBitmap = SKBitmap.Decode(inputStream);
                if (skBitmap == null) return null;

                // macOS 桌面图标圆角约 12.5%
                var rounded = IconGeneratorUtils.ProcessIcon(skBitmap, IconSize, cornerRadius: IconSize / 8);
                if (rounded == null) return null;

                using var image = SKImage.FromBitmap(rounded);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.OpenWrite(destPath);
                data.SaveTo(stream);

                return destPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IconGen] 生成 PNG 失败: {ex.Message}");
                return null;
            }
        }
    }
}
