using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Sounder_APP.Models;

namespace Sounder_APP.Converters
{
    /// <summary>
    /// 多值集合包含检查转换器：values[0]（音频项 Id）是否存在于 values[1]（ObservableSet）中
    /// </summary>
    public class SetContainsMultiConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count >= 2 && values[0] is string itemId && values[1] is ObservableSet set)
            {
                var result = set.Contains(itemId);
                Debug.WriteLine($"[Converter] SetContains: itemId={itemId}, set.Count={set.Count}, contains={result}");
                return result;
            }
            Debug.WriteLine($"[Converter] SetContains: FAILED - values[0]={values[0]?.GetType().Name ?? "null"}, values[1]={values[1]?.GetType().Name ?? "null"}");
            return false;
        }
    }

    /// <summary>
    /// 多值集合不包含检查转换器：values[0]（音频项 Id）不存在于 values[1]（ObservableSet）中
    /// </summary>
    public class SetNotContainsMultiConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count >= 2 && values[0] is string itemId && values[1] is ObservableSet set)
            {
                var result = !set.Contains(itemId);
                Debug.WriteLine($"[Converter] SetNotContains: itemId={itemId}, set.Count={set.Count}, notContains={result}");
                return result;
            }
            Debug.WriteLine($"[Converter] SetNotContains: FAILED - values[0]={values[0]?.GetType().Name ?? "null"}, values[1]={values[1]?.GetType().Name ?? "null"}");
            return true;
        }
    }

    /// <summary>
    /// 播放状态背景色 — values[0]（itemId）在 values[1]（PlayingAudioIds）中时返回播放高亮底色
    /// 颜色取自 App.axaml 主题字典中的 PlayingHighlightBg
    /// </summary>
    public class PlayingBackgroundSetConverter : IMultiValueConverter
    {
        private static readonly IBrush TransparentBg = Brushes.Transparent;

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count >= 2 && values[0] is string itemId && values[1] is ObservableSet set && set.Contains(itemId))
            {
                Debug.WriteLine($"[Converter] PlayingBackground: itemId={itemId} → HIGHLIGHT");
                return FindResource("PlayingHighlightBg");
            }
            Debug.WriteLine($"[Converter] PlayingBackground: itemId={values[0]?.ToString() ?? "null"} → transparent");
            return TransparentBg;
        }

        private static IBrush? FindResource(string key)
        {
            if (Application.Current?.Resources.TryGetResource(key, null, out var res) == true
                && res is IBrush brush)
                return brush;
            return Brushes.Transparent;
        }
    }
}
