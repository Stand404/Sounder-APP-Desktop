using Avalonia.Data.Converters;
using Sounder_APP.Models;
using System;
using System.Globalization;

namespace Sounder_APP.Converters;

/// <summary>
/// 将 OrderMode 枚举转换为 PillToggle 的 SelectedIndex (0=Order, 1=Random)
/// </summary>
public class OrderModeToIndexConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is OrderMode mode)
        {
            return mode switch
            {
                OrderMode.Order => 0,
                OrderMode.Random => 1,
                _ => 0
            };
        }
        return 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index)
        {
            return index switch
            {
                0 => OrderMode.Order,
                1 => OrderMode.Random,
                _ => OrderMode.Order
            };
        }
        return OrderMode.Order;
    }
}
