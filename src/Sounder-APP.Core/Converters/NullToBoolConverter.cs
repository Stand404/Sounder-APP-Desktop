using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Sounder_APP.Converters
{
    /// <summary>
    /// 非空值 -> true, null -> false
    /// </summary>
    public class NullToBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is not null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
