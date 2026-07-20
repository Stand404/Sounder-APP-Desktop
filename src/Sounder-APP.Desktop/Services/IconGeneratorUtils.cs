using System;
using SkiaSharp;

namespace Sounder_APP.Services
{
    /// <summary>
    /// 图标处理共享工具：裁剪正方形、缩放、圆角处理。
    /// Windows/Linux 图标后端共用。
    /// </summary>
    internal static class IconGeneratorUtils
    {
        /// <summary>加载源图 → 裁切正方形 → 缩放 → 圆角</summary>
        public static SKBitmap? ProcessIcon(SKBitmap source, int targetSize)
        {
            var size = Math.Min(source.Width, source.Height);
            var srcRect = new SKRectI(
                (source.Width - size) / 2, (source.Height - size) / 2,
                (source.Width - size) / 2 + size, (source.Height - size) / 2 + size);
            using var square = new SKBitmap(size, size);
            source.ExtractSubset(square, srcRect);

            using var resized = square.Resize(new SKImageInfo(targetSize, targetSize),
                new SKSamplingOptions(SKFilterMode.Linear));
            if (resized == null) return null;

            return ApplyRoundedCorners(resized, 12);
        }

        private static SKBitmap ApplyRoundedCorners(SKBitmap source, int radius)
        {
            var w = source.Width;
            var h = source.Height;
            var result = new SKBitmap(w, h);
            using var canvas = new SKCanvas(result);
            canvas.Clear(SKColors.Transparent);
            using var path = new SKPath();
            path.AddRoundRect(new SKRect(0, 0, w, h), radius, radius);
            canvas.ClipPath(path);
            canvas.DrawBitmap(source, 0, 0);
            return result;
        }
    }
}
