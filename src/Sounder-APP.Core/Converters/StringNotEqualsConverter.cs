using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Sounder_APP.Converters
{
    /// <summary>
    /// 字符串不相等比较转换器：value.ToString() != parameter.ToString() 返回 true
    /// </summary>
    public class StringNotEqualsConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.ToString() != parameter?.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
