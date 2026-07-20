using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Sounder_APP.Services
{
    /// <summary>
    /// 本地化服务：直接从程序集嵌入的 .resources 流读取翻译数据，
    /// 无需依赖 ResourceManager / CultureInfo / ICU，彻底消除 Linux 上因缺少 ICU 导致的崩溃。
    /// 
    /// 支持语言：zh-CN（默认/回退）、zh-TW、en、ja、ru
    /// XAML 绑定：{i18n:Translate key} 或 {Binding L[key]}
    /// </summary>
    public class LocalizationService : INotifyPropertyChanged
    {
        // ====================================================================
        //  单例
        // ====================================================================
        private static readonly Lazy<LocalizationService> _instance =
            new(() => new LocalizationService());
        public static LocalizationService Instance => _instance.Value;

        // ====================================================================
        //  内部存储：languageCode → (key → value)
        //  languageCode 是嵌入资源后缀名：""(中性/zh-CN)、"en"、"ja"、"ru"、"zh-TW"
        // ====================================================================
        private readonly Dictionary<string, Dictionary<string, string>> _resources = new();

        // ====================================================================
        //  支持的语言代码（UserSetting → InternalCode）
        // ====================================================================
        /// <summary>External language code → internal resource code</summary>
        private static readonly Dictionary<string, string> ExternalToInternal = new()
        {
            { "zh-CN", "" },      // 中性资源 = 简体中文
            { "zh-TW", "zh-TW" },
            { "en",    "en" },
            { "ja",    "ja" },
            { "ru",    "ru" },
        };

        /// <summary>所有有效的外置代码（含 "System"）</summary>
        public static readonly string[] SupportedLanguages =
            { "System", "zh-CN", "zh-TW", "en", "ja", "ru" };

        // ====================================================================
        //  系统语言检测（启动时快照，避免后续被 ApplyCulture 覆盖）
        // ====================================================================
        private static readonly bool _isInvariant;
        private static readonly string _systemCultureName;

        static LocalizationService()
        {
            try
            {
                _systemCultureName = CultureInfo.CurrentUICulture.Name.ToLowerInvariant();
                _isInvariant = string.IsNullOrEmpty(_systemCultureName);
            }
            catch
            {
                // 无 ICU 环境：取环境变量 LANG
                _systemCultureName = DetectSystemLanguageFromEnv();
                _isInvariant = string.IsNullOrEmpty(_systemCultureName);
            }
        }

        private static string DetectSystemLanguageFromEnv()
        {
            try
            {
                var lang = Environment.GetEnvironmentVariable("LANG") ?? "";
                lang = lang.ToLowerInvariant();
                if (lang.StartsWith("zh_cn") || lang.StartsWith("zh-cn") || lang.StartsWith("zh-sg"))
                    return "zh-cn";
                if (lang.StartsWith("zh_tw") || lang.StartsWith("zh_hk") || lang.StartsWith("zh_mo"))
                    return "zh-tw";
                if (lang.StartsWith("zh"))
                    return "zh-cn";
                if (lang.StartsWith("ja"))
                    return "ja";
                if (lang.StartsWith("ru"))
                    return "ru";
                if (lang.StartsWith("en"))
                    return "en";
            }
            catch { }
            return "en";
        }

        // ====================================================================
        //  当前语言状态
        // ====================================================================
        private string _currentLanguage = "System";

        /// <summary>当前选择的语言代码（"System", "zh-CN", "zh-TW", "en", "ja", "ru"）</summary>
        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    ApplyCultureSafe();
                    OnPropertyChanged();
                    OnPropertyChanged("Item");
                    OnPropertyChanged("Item[]");
                }
            }
        }

        /// <summary>当前实际生效的内部资源代码（""、 "en"、 "ja"、 "ru"、 "zh-TW"）</summary>
        public string EffectiveLanguageCode
        {
            get
            {
                var ext = _currentLanguage == "System" ? DetectSystemLanguage() : _currentLanguage;
                return ExternalToInternal.TryGetValue(ext, out var code) ? code : "";
            }
        }

        // ====================================================================
        //  索引器：XAML 绑定 {Binding L[key]} 或 {i18n:Translate key}
        // ====================================================================
        public string this[string key]
        {
            get
            {
                if (string.IsNullOrEmpty(key)) return string.Empty;
                var code = EffectiveLanguageCode;
                if (_resources.TryGetValue(code, out var dict) && dict.TryGetValue(key, out var val))
                    return val;
                // 如果目标语言没有找到，回退到中性资源
                if (code != "" && _resources.TryGetValue("", out var neutral) && neutral.TryGetValue(key, out var fallback))
                    return fallback;
                return $"[{key}]"; // 调试：显示未翻译的 key
            }
        }

        /// <summary>获取指定 key 的本地化字符串</summary>
        public string Get(string key) => this[key];

        /// <summary>获取指定 key 的本地化字符串（带参数格式化）</summary>
        public string Get(string key, params object[] args)
        {
            try { return string.Format(this[key], args); }
            catch { return this[key]; }
        }

        // ====================================================================
        //  设置语言
        // ====================================================================
        public void SetLanguage(string languageCode)
        {
            if (Array.IndexOf(SupportedLanguages, languageCode) >= 0)
                CurrentLanguage = languageCode;
        }

        // ====================================================================
        //  系统语言检测
        // ====================================================================
        public string DetectSystemLanguage()
        {
            if (!_isInvariant)
            {
                var name = _systemCultureName;
                // 繁体
                if (name == "zh-tw" || name == "zh-hk" || name == "zh-mo" || name.StartsWith("zh-hant"))
                    return "zh-TW";
                // 简体
                if (name.StartsWith("zh"))
                    return "zh-CN";
                if (name.StartsWith("ja"))
                    return "ja";
                if (name.StartsWith("ru"))
                    return "ru";
                if (name.StartsWith("en"))
                    return "en";
                return "en";
            }
            return "en"; // 无 ICU 环境默认英文
        }

        // ====================================================================
        //  加载资源（构造函数中调用）
        // ====================================================================
        private LocalizationService()
        {
            LoadResources();
        }

        private void LoadResources()
        {
            var assembly = typeof(LocalizationService).Assembly;
            var cultures = new[] { "", "en", "ja", "ru", "zh-TW" };

            // 枚举所有嵌入资源，按后缀名模糊匹配（兼容不同项目根命名空间）
            var allResourceNames = assembly.GetManifestResourceNames();

            foreach (var culture in cultures)
            {
                var dict = new Dictionary<string, string>(StringComparer.Ordinal);
                var suffix = string.IsNullOrEmpty(culture)
                    ? "Resource.resources"
                    : $"Resource.{culture}.resources";

                var resName = allResourceNames.FirstOrDefault(n =>
                    n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

                if (resName == null)
                {
                    // 回退：硬编码路径
                    resName = string.IsNullOrEmpty(culture)
                        ? "Sounder_APP.Properties.Resource.resources"
                        : $"Sounder_APP.Properties.Resource.{culture}.resources";
                }

                try
                {
                    using var stream = assembly.GetManifestResourceStream(resName);
                    if (stream != null)
                    {
                        using var reader = new ResourceReader(stream);
                        foreach (System.Collections.DictionaryEntry entry in reader)
                        {
                            var k = entry.Key?.ToString();
                            var v = entry.Value?.ToString();
                            if (k != null)
                                dict[k] = v ?? string.Empty;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Localization] 加载 {resName} 失败: {ex.Message}");
                }

                _resources[culture] = dict;
            }
        }

        // ====================================================================
        //  INotifyPropertyChanged
        // ====================================================================
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ====================================================================
        //  ApplyCulture（安全模式：仅尝试，不依赖 ICU）
        // ====================================================================
        private void ApplyCultureSafe()
        {
            try
            {
                var ext = _currentLanguage == "System" ? DetectSystemLanguage() : _currentLanguage;
                // 只设置已知的简单文化名称，避免在无 ICU 环境崩溃
                var cultureName = ext switch
                {
                    "zh-CN" => "zh-CN",
                    "zh-TW" => "zh-TW",
                    "en" => "en",
                    "ja" => "ja",
                    "ru" => "ru",
                    _ => "en"
                };
                var ci = CultureInfo.GetCultureInfo(cultureName);
                Thread.CurrentThread.CurrentCulture = ci;
                Thread.CurrentThread.CurrentUICulture = ci;
                CultureInfo.DefaultThreadCurrentCulture = ci;
                CultureInfo.DefaultThreadCurrentUICulture = ci;
            }
            catch
            {
                // 无 ICU 环境：忽略 CultureInfo 设置，不影响翻译功能
            }
        }
    }
}
