using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Sounder_APP.Converters
{
    /// <summary>
    /// 多值字符串相等比较转换器：values[0] == values[1] 返回 true
    /// 用于逐项匹配 LoadingAudioItemId / PlayingAudioItemId
    /// </summary>
    public class StringEqualMultiConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count >= 2 && values[0] is string s0 && values[1] is string s1)
                return s0 == s1;
            return false;
        }
    }

    /// <summary>
    /// 多值字符串不相等比较转换器：values[0] != values[1] 返回 true
    /// 用于非加载/非播放状态下显示默认图标
    /// </summary>
    public class StringNotEqualMultiConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count >= 2 && values[0] is string s0 && values[1] is string s1)
                return s0 != s1;
            return true;
        }
    }

    /// <summary>
    /// 播放状态背景色转换器：values[0](itemId) == values[1](playingAudioItemId) 时返回播放高亮底色
    /// 颜色取自 App.axaml 主题字典中的 PlayingHighlightBg
    /// </summary>
    public class PlayingBackgroundMultiConverter : IMultiValueConverter
    {
        private static readonly IBrush TransparentBg = Brushes.Transparent;

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count >= 2 && values[0] is string s0 && values[1] is string s1 && s0 == s1)
                return FindResource("PlayingHighlightBg");
            return TransparentBg;
        }

        private static IBrush? FindResource(string key)
        {
            if (Application.Current?.Resources.TryGetResource(key, null, out var res) == true && res is IBrush brush)
                return brush;
            return Brushes.Transparent;
        }
    }

    /// <summary>
    /// 播放状态前景色转换器：匹配播放/加载状态时返回 AccentBlue（活跃色），
    /// 非匹配态根据 ConverterParameter 区分："Primary"→TextPrimary, 其余→TextSecondary
    /// 颜色取自 App.axaml 主题字典
    /// </summary>
    public class PlayingForegroundMultiConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            // 匹配活跃状态（loading 或 playing）→ AccentBlue
            if (values.Count >= 2 && values[0] is string s0)
            {
                for (int i = 1; i < values.Count; i++)
                {
                    if (values[i] is string si && s0 == si)
                        return FindResource("AccentBlue");
                }
            }

            // 非活跃态 → 根据参数选择颜色
            var key = parameter as string;
            if (key == "Primary")
                return FindResource("TextPrimary");
            return FindResource("TextSecondary");
        }

        private static IBrush? FindResource(string key)
        {
            if (Application.Current?.Resources.TryGetResource(key, null, out var res) == true && res is IBrush brush)
                return brush;
            return null;
        }
    }
}
