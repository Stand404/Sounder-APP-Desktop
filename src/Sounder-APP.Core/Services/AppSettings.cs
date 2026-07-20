using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sounder_APP.Services
{
    /// <summary>
    /// 应用设置持久化：JSON 文件存储于 LocalApplicationData
    /// </summary>
    public class AppSettings
    {
        private static readonly string FilePath;

        private static readonly AppSettingsJsonContext JsonContext = AppSettingsJsonContext.Default;

        static AppSettings()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Sounder-APP");
            Directory.CreateDirectory(dir);
            FilePath = Path.Combine(dir, "settings.json");
        }

        private AppSettingsModel _data;

        public AppSettings()
        {
            _data = Load();
        }

        public string ThemeMode
        {
            get => _data.ThemeMode;
            set
            {
                if (_data.ThemeMode != value)
                {
                    _data.ThemeMode = value;
                    Save();
                }
            }
        }

        public string Language
        {
            get => _data.Language;
            set
            {
                if (_data.Language != value)
                {
                    _data.Language = value;
                    Save();
                }
            }
        }

        private static AppSettingsModel Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    return JsonSerializer.Deserialize(json, JsonContext.AppSettingsModel)!;
                }
            }
            catch { /* ignore corrupt file */ }
            return new AppSettingsModel();
        }

        private void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_data, JsonContext.AppSettingsModel);
                File.WriteAllText(FilePath, json);
            }
            catch { /* ignore save failures */ }
        }

        internal class AppSettingsModel
        {
            public string ThemeMode { get; set; } = "Auto";
            public string Language { get; set; } = "System";
        }
    }

    /// <summary>裁剪模式下 AppSettingsModel 的 JSON 序列化上下文</summary>
    [JsonSerializable(typeof(AppSettings.AppSettingsModel))]
    internal partial class AppSettingsJsonContext : JsonSerializerContext
    {
    }
}
