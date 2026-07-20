using Avalonia.Data.Converters;
using Sounder_APP.Models;
using System;
using System.Globalization;

namespace Sounder_APP.Converters;

/// <summary>
/// 将 PlayMode 枚举转换为 PillToggle 的 SelectedIndex (0=Overlay, 1=Replace, 2=Loop)
/// </summary>
public class PlayModeToIndexConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is PlayMode mode)
        {
            return mode switch
            {
                PlayMode.Overlay => 0,
                PlayMode.Replace => 1,
                PlayMode.Loop => 2,
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
                0 => PlayMode.Overlay,
                1 => PlayMode.Replace,
                2 => PlayMode.Loop,
                _ => PlayMode.Overlay
            };
        }
        return PlayMode.Overlay;
    }
}
