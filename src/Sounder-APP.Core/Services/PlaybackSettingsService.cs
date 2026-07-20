using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Sounder_APP.Models;

namespace Sounder_APP.Services
{
    /// <summary>
    /// 每个资源的播放设置持久化服务。
    /// 存储在 %LocalAppData%\Sounder-APP\playback_settings.json。
    /// </summary>
    public class PlaybackSettingsService
    {
        private static readonly string SettingsPath;

        private static readonly AppJsonContext JsonContext = AppJsonContext.Default;

        private Dictionary<string, PlaybackSettings> _settings = new();

        static PlaybackSettingsService()
        {
            var appDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Sounder-APP");
            Directory.CreateDirectory(appDir);
            SettingsPath = Path.Combine(appDir, "playback_settings.json");
        }

        public PlaybackSettingsService()
        {
            Reload();
        }

        /// <summary>从磁盘重新加载设置（解决多实例缓存不同步问题）</summary>
        public void Reload()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                var json = File.ReadAllText(SettingsPath);
                var data = JsonSerializer.Deserialize(json, JsonContext.DictionaryStringPlaybackSettings);
                if (data != null)
                    _settings = data;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaybackSettings] 重新加载失败: {ex.Message}");
            }
        }

        /// <summary>获取指定资源的播放设置，不存在则返回默认值</summary>
        public PlaybackSettings GetSettings(string resourceId)
        {
            if (_settings.TryGetValue(resourceId, out var s))
                return s;
            return PlaybackSettings.Default;
        }

        /// <summary>保存指定资源的播放设置</summary>
        public void SaveSettings(string resourceId, PlaybackSettings settings)
        {
            _settings[resourceId] = settings;
            Save();
        }

        private void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, JsonContext.DictionaryStringPlaybackSettings);
                var tmpPath = SettingsPath + ".tmp";
                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, SettingsPath, overwrite: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaybackSettings] 保存失败: {ex.Message}");
            }
        }
    }
}
