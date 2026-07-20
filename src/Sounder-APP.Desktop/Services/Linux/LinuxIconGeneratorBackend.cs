using System;
using System.IO;
using SkiaSharp;

namespace Sounder_APP.Services.Linux
{
    /// <summary>
    /// Linux 图标后端：生成 .png 文件（Linux .desktop 快捷方式需要）。
    /// </summary>
    public class LinuxIconGeneratorBackend : IIconGeneratorBackend
    {
        private const int IconSize = 48;

        public string GetIconFileName() => "icon.png";

        public string? GenerateIcon(string sourcePath, string destPath)
        {
            try
            {
                using var inputStream = File.OpenRead(sourcePath);
                using var skBitmap = SKBitmap.Decode(inputStream);
                if (skBitmap == null) return null;

                using var rounded = IconGeneratorUtils.ProcessIcon(skBitmap, IconSize);
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
