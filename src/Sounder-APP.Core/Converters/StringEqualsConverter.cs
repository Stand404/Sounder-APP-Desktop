using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Sounder_APP.Converters
{
    /// <summary>
    /// 字符串相等比较转换器：value == parameter 返回 true
    /// </summary>
    public class StringEqualsConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.ToString() == parameter?.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// 十六进制颜色字符串 → SolidColorBrush 转换器
    /// </summary>
    public class HexToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrEmpty(hex))
            {
                try { return new SolidColorBrush(Color.Parse(hex)); }
                catch { }
            }
            return Brushes.Gray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
