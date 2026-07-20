using System;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;

namespace Sounder_APP.Services.Windows
{
    /// <summary>
    /// Windows 图标后端：生成 .ico 文件（Windows 快捷方式需要）。
    /// </summary>
    public class WindowsIconGeneratorBackend : IIconGeneratorBackend
    {
        private const int IconSize = 48;

        public string GetIconFileName() => "icon.ico";

        public string? GenerateIcon(string sourcePath, string destPath)
        {
            try
            {
                using var inputStream = File.OpenRead(sourcePath);
                using var skBitmap = SKBitmap.Decode(inputStream);
                if (skBitmap == null) return null;

                using var rounded = IconGeneratorUtils.ProcessIcon(skBitmap, IconSize);
                if (rounded == null) return null;

                using var pngData = rounded.Encode(SKEncodedImageFormat.Png, 100);
                var pngBytes = pngData.ToArray();
                WriteIcoFile(destPath, pngBytes, IconSize);

                return destPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IconGen] 生成 .ico 失败: {ex.Message}");
                return null;
            }
        }

        private static void WriteIcoFile(string path, byte[] pngData, int size)
        {
            const int headerSize = 6;
            const int entrySize = 16;
            var fileSize = headerSize + entrySize + pngData.Length;
            var buffer = new byte[fileSize];

            buffer[0] = 0; buffer[1] = 0;
            buffer[2] = 1; buffer[3] = 0;
            buffer[4] = 1; buffer[5] = 0;

            int offset = headerSize;
            buffer[offset + 0] = (byte)(size >= 256 ? 0 : size);
            buffer[offset + 1] = (byte)(size >= 256 ? 0 : size);
            buffer[offset + 2] = 0;
            buffer[offset + 3] = 0;
            buffer[offset + 4] = 1; buffer[offset + 5] = 0;
            buffer[offset + 6] = 32; buffer[offset + 7] = 0;
            BitConverter.GetBytes(pngData.Length).CopyTo(buffer, offset + 8);
            BitConverter.GetBytes(headerSize + entrySize).CopyTo(buffer, offset + 12);

            Array.Copy(pngData, 0, buffer, headerSize + entrySize, pngData.Length);
            File.WriteAllBytes(path, buffer);
        }
    }
}
