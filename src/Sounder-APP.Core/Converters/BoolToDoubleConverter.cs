using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Sounder_APP.Converters
{
    /// <summary>
    /// 布尔值转 double。ConverterParameter 指定 true 时的值，false 时返回 0。
    /// 示例：ConverterParameter="48" → true=48, false=0
    /// </summary>
    public class BoolToDoubleConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b && double.TryParse(parameter?.ToString(), out var trueVal))
                return b ? trueVal : 0.0;
            return 0.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
