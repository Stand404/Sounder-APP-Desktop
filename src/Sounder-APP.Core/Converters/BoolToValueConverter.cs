using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Sounder_APP.Converters
{
    /// <summary>
    /// 布尔值转两个值的转换器。ConverterParameter 格式: "true值;false值"
    /// </summary>
    public class BoolToValueConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b && parameter is string param)
            {
                var parts = param.Split(';');
                return b ? parts[0] : (parts.Length > 1 ? parts[1] : null);
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// 布尔值取反
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }
    }
}
