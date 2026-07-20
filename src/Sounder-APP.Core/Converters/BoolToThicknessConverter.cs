using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Sounder_APP.Converters
{
    /// <summary>
    /// 布尔值转 Thickness。ConverterParameter 格式："activeBottom|inactiveBottom"
    /// 示例：ConverterParameter="76|-100"
    ///   true → 底部 margin=76（正常位置）
    ///   false → 底部 margin=-100（偏移到视图外）
    /// </summary>
    public class BoolToThicknessConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b && parameter is string s)
            {
                var parts = s.Split('|');
                if (parts.Length == 2
                    && double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var active)
                    && double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var inactive))
                {
                    var bottom = b ? active : inactive;
                    return new Avalonia.Thickness(0, 0, 0, bottom);
                }
            }
            return new Avalonia.Thickness(0);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
