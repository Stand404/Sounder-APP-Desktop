using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Sounder_APP.Services;
using System;

namespace Sounder_APP.Converters
{
    /// <summary>
    /// 本地化 MarkupExtension，支持动态切换语言。
    /// XAML 用法：Text="{i18n:Translate settings_title}"
    /// </summary>
    public class TranslateExtension : MarkupExtension
    {
        public string Key { get; set; } = string.Empty;

        public TranslateExtension() { }

        public TranslateExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new Binding
            {
                Source = LocalizationService.Instance,
                Path = $"[{Key}]",
                Mode = BindingMode.OneWay,
            };

            return binding;
        }
    }
}
