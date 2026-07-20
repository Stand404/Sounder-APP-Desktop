using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Sounder_APP.Converters
{
    /// <summary>
    /// 布尔值转偏移量。true → 0（可见位置），false → parameter（隐藏偏移）。
    /// 用于停止按钮底部滑入/滑出动画：true 时 TranslateY=0，false 时 TranslateY=offset（滑出屏幕）。
    /// </summary>
    public class BoolToOffsetConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b && double.TryParse(parameter?.ToString(), out var offset))
                return b ? 0.0 : offset;
            return 0.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
