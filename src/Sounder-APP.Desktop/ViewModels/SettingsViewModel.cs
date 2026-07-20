using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sounder_APP.Services;
using Sounder_APP.Views;

namespace Sounder_APP.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly AppSettings _appSettings;

        [ObservableProperty]
        private string _themeMode = "Auto";

        [ObservableProperty]
        private string _cacheSize = "计算中...";

        [ObservableProperty]
        private LanguageOption? _selectedLanguage;

        public string AuthorName => "Stand404";
        public string WebsiteUrl => "https://stand.homes";
        public string PackageUrl => "https://stand.homes/apps/a4afacef-96c4-40dc-a5b4-be1e55d73df1";
        public string AudioFilesUrl => "https://stand.homes/apps/2b6f4e18-bafe-46bf-b162-056fa277c82f";
        public string VersionText => "发声APP · 造化版";

        /// <summary>本地化服务引用</summary>
        public LocalizationService L => LocalizationService.Instance;

        /// <summary>语言选项列表</summary>
        public ObservableCollection<LanguageOption> LanguageOptions { get; }

        private long _cacheBytes;
        private bool _initialized;

        public SettingsViewModel(AppSettings appSettings)
        {
            _appSettings = appSettings;

            // 先恢复语言以便 L 索引器返回正确的文本
            L.CurrentLanguage = appSettings.Language;

            // 初始化语言选项列表
            LanguageOptions = new ObservableCollection<LanguageOption>
            {
                new LanguageOption("System", L["language_system"]),
                new LanguageOption("zh-CN",  L["language_zh"]),
                new LanguageOption("zh-TW",  L["language_zh_tw"]),
                new LanguageOption("en",     L["language_en"]),
                new LanguageOption("ja",     L["language_ja"]),
                new LanguageOption("ru",     L["language_ru"]),
            };

            // 从持久化存储恢复主题模式
            ThemeMode = appSettings.ThemeMode;

            // 恢复语言选中项
            SelectedLanguage = LanguageOptions.FirstOrDefault(o => o.Code == L.CurrentLanguage)
                               ?? LanguageOptions[0];

            _initialized = true;
            RefreshCacheSize();
        }

        partial void OnSelectedLanguageChanged(LanguageOption? value)
        {
            if (value == null || !_initialized) return;
            L.SetLanguage(value.Code);
            _appSettings.Language = value.Code;

            // 刷新语言选项的显示文本
            foreach (var opt in LanguageOptions)
                opt.RefreshLabel(L);
        }

        partial void OnThemeModeChanged(string value)
        {
            Application.Current!.RequestedThemeVariant = value switch
            {
                "Light" => ThemeVariant.Light,
                "Dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };

            // 持久化保存（跳过构造函数中的初始化触发）
            if (_initialized)
                _appSettings.ThemeMode = value;
        }

        [RelayCommand]
        private void SetThemeMode(string mode)
        {
            ThemeMode = mode;
        }

        private void RefreshCacheSize()
        {
            // 图片和音频共用统一 cache 目录，只需计算一次
            _cacheBytes = ImageCache.GetCacheSize();
            CacheSize = FormatSize(_cacheBytes);
        }

        [RelayCommand]
        private async Task ClearCache()
        {
            if (_cacheBytes == 0) return;

            var confirmed = await DirtyConfirmWindow.ShowConfirmAsync(
                L["clear_cache_confirm_title"],
                L["clear_cache_confirm_msg"]);
            if (!confirmed) return;

            // 统一 cache 目录只需清理一次，同时清理旧目录
            ImageCache.ClearAll();
            RefreshCacheSize();
        }

        [RelayCommand]
        private void OpenWebsite()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = WebsiteUrl,
                UseShellExecute = true
            });
        }

        [RelayCommand]
        private void OpenPackage()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = PackageUrl,
                UseShellExecute = true
            });
        }

        [RelayCommand]
        private void OpenAudioFiles()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AudioFilesUrl,
                UseShellExecute = true
            });
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }
    }

    /// <summary>语言选项数据模型</summary>
    public partial class LanguageOption : ObservableObject
    {
        public string Code { get; }
        [ObservableProperty]
        private string _label;

        public LanguageOption(string code, string label)
        {
            Code = code;
            _label = label;
        }

        public void RefreshLabel(LocalizationService l)
        {
            Label = Code switch
            {
                "System" => l["language_system"],
                "zh-CN" => l["language_zh"],
                "zh-TW" => l["language_zh_tw"],
                "en" => l["language_en"],
                "ja" => l["language_ja"],
                "ru" => l["language_ru"],
                _ => Code
            };
        }
    }
}
