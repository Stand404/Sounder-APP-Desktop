using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Sounder_APP.Models;
using Sounder_APP.Services;

namespace Sounder_APP.Services
{
    public partial class PlaybackTaskInfo : ObservableObject
    {
        [ObservableProperty] private string _resourceId = string.Empty;
        [ObservableProperty] private string _displayName = string.Empty;     // 完整名称
        [ObservableProperty] private string _shortName = string.Empty;       // 简称
        [ObservableProperty] private Bitmap? _iconBitmap;                    // 资源图标位图
        [ObservableProperty] private string _currentAudioName = string.Empty;
        [ObservableProperty] private string _playModeText = string.Empty;    // 叠加/替换/单曲循环/列表循环
        [ObservableProperty] private int _activePlayerCount;
        [ObservableProperty] private bool _isActive;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class BackgroundPlaybackManager
    {
        /// <summary>循环状态（单曲/列表，顺序/随机）</summary>
        private class LoopState
        {
            public LoopMode Mode;
            public OrderMode OrderMode;
            public List<AudioItem> AudioList = new();
            public AudioItem? CurrentAudio; // 单曲循环时锁定当前音频
        }

        private readonly AudioPlaybackService _audioPlayer = new();
        private readonly PlaybackSettingsService _settingsService = new();

        public ObservableCollection<PlaybackTaskInfo> ActiveTasks { get; } = new();
        public event Action? AllTasksCompleted;
        public event Action? TaskStateChanged;

        private readonly Dictionary<string, PlaybackTaskInfo> _taskMap = new();
        private readonly Dictionary<string, List<string>> _playerMap = new();
        private readonly Dictionary<string, string> _audioToResource = new();
        private readonly Dictionary<string, string> _instanceToResource = new();
        private readonly Dictionary<string, List<AudioItem>> _listLoopPool = new();
        private readonly Dictionary<string, LoopState> _loopStates = new();
        private readonly Dictionary<string, PlayMode> _activePlayModes = new(); // resourceId → 当前模式

        private readonly object _lock = new();

        public BackgroundPlaybackManager()
        {
            _audioPlayer.PlaybackStarted += OnPlaybackStarted;
            _audioPlayer.PlaybackStopped += OnPlaybackStopped;
        }

        public bool HasActiveTasks { get { lock (_lock) return _taskMap.Count > 0; } }

        // ==================== 播放入口 ====================

        public async Task PlayResourceAsync(string resourceId)
        {
            try
            {
                Debug.WriteLine($"[BgPlayback] 播放请求: resourceId={resourceId}");

                var resource = ResourceService.LoadResourceJson(resourceId);
                if (resource == null) { Debug.WriteLine("[BgPlayback] 资源不存在"); return; }

                // 从磁盘重新加载（ViewModel 可能已修改设置）
                _settingsService.Reload();
                var settings = _settingsService.GetSettings(resourceId);
                var audioList = resource.AudioItems.Where(a => !string.IsNullOrEmpty(a.Src))
                    .OrderBy(a => a.OrderIndex).ToList();
                if (audioList.Count == 0) { Debug.WriteLine("[BgPlayback] 资源无可用音频"); return; }

                Debug.WriteLine($"[BgPlayback] {resource.DisplayName}, 模式={settings.PlayMode}, 顺序={settings.OrderMode}, 音频数={audioList.Count}");

                // ===== 模式切换 =====
                var modeChanged = false;
                if (HasResource(resourceId))
                {
                    var prevMode = _activePlayModes.GetValueOrDefault(resourceId);
                    modeChanged = prevMode != settings.PlayMode;

                    if (!modeChanged)
                    {
                        // 模式相同 + 循环：切换停止
                        if (settings.PlayMode == PlayMode.Loop)
                        {
                            Debug.WriteLine("[BgPlayback] 已在循环，切换为停止");
                            StopResource(resourceId);
                            return;
                        }
                        // 模式相同 + 非循环：不停止，叠加新音频
                    }
                    else
                    {
                        // 模式变更：清旧启新
                        Debug.WriteLine($"[BgPlayback] 模式变更 {prevMode}→{settings.PlayMode}，清理旧任务");
                        StopResource(resourceId);
                    }
                }
                switch (settings.PlayMode)
                {
                    case PlayMode.Loop:
                        StartLoop(resourceId, resource, settings, audioList);
                        lock (_lock) _activePlayModes[resourceId] = PlayMode.Loop;
                        break;
                    case PlayMode.Replace:
                        StopResourceInternal(resourceId);
                        {
                            lock (_lock) _activePlayModes[resourceId] = PlayMode.Replace;
                            var taskInfo = GetOrCreateTask(resource);
                            var orderLabel = GetOrderLabel(settings.OrderMode);
                            taskInfo.PlayModeText = $"{LocalizationService.Instance.Get("replace_mode")} · {orderLabel}";
                            var audio = SelectAudio(resourceId, audioList, settings.OrderMode);
                            taskInfo.CurrentAudioName = audio.Name;
                            RegisterAudioMapping(audio.Id, resourceId);
                            _ = PlayReplaceSafeAsync(audio);
                        }
                        break;
                    default: // Overlay
                        {
                            lock (_lock) _activePlayModes[resourceId] = PlayMode.Overlay;
                            var taskInfo = GetOrCreateTask(resource);
                            var orderLabel = GetOrderLabel(settings.OrderMode);
                            taskInfo.PlayModeText = $"{LocalizationService.Instance.Get("overlay_mode")} · {orderLabel}";
                            var audio = SelectAudio(resourceId, audioList, settings.OrderMode);
                            taskInfo.CurrentAudioName = audio.Name;
                            RegisterAudioMapping(audio.Id, resourceId);
                            await _audioPlayer.PlayAsync(audio.Src, audio.Id, PlayMode.Overlay);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BgPlayback] 异常: {ex.Message}");
            }
        }

        // ==================== 循环模式 ====================

        private void StartLoop(string resourceId, Resource resource, PlaybackSettings settings, List<AudioItem> audioList)
        {
            StopResourceInternal(resourceId);

            var loopState = new LoopState
            {
                Mode = settings.LoopMode,
                OrderMode = settings.OrderMode,
                AudioList = audioList
            };
            _loopStates[resourceId] = loopState;

            var taskInfo = GetOrCreateTask(resource);
            var orderLabel = GetOrderLabel(settings.OrderMode);

            if (settings.LoopMode == LoopMode.SingleLoop)
            {
                taskInfo.PlayModeText = $"{LocalizationService.Instance.Get("play_mode_single_loop")} · {orderLabel}";
                var audio = SelectAudio(resourceId, audioList, settings.OrderMode);
                loopState.CurrentAudio = audio;
                taskInfo.CurrentAudioName = audio.Name;
                RegisterAudioMapping(audio.Id, resourceId);
                _ = _audioPlayer.PlayAsync(audio.Src, audio.Id, PlayMode.Overlay);
            }
            else // ListLoop
            {
                taskInfo.PlayModeText = $"{LocalizationService.Instance.Get("play_mode_list_loop")} · {orderLabel}";
                AudioItem first;
                if (settings.OrderMode == OrderMode.Random)
                {
                    var pool = new List<AudioItem>(audioList);
                    _listLoopPool[resourceId] = pool;
                    var pickIdx = Random.Shared.Next(pool.Count);
                    first = pool[pickIdx];
                    pool.RemoveAt(pickIdx);
                }
                else
                {
                    first = audioList[settings.CurrentAudioIndex % audioList.Count];
                    settings.CurrentAudioIndex = (settings.CurrentAudioIndex + 1) % audioList.Count;
                    _settingsService.SaveSettings(resourceId, settings);
                }
                taskInfo.CurrentAudioName = first.Name;
                RegisterAudioMapping(first.Id, resourceId);
                _ = _audioPlayer.PlayAsync(first.Src, first.Id, PlayMode.Overlay);
            }
        }

        // ==================== 音频选择 ====================

        private AudioItem SelectAudio(string resourceId, List<AudioItem> audioList, OrderMode orderMode)
        {
            if (orderMode == OrderMode.Random)
                return audioList[Random.Shared.Next(audioList.Count)];

            // 顺序模式：从持久化设置读索引，更新后保存（跨重启衔接）
            var settings = _settingsService.GetSettings(resourceId);
            int index = settings.CurrentAudioIndex % audioList.Count;
            settings.CurrentAudioIndex = (index + 1) % audioList.Count;
            _settingsService.SaveSettings(resourceId, settings);
            return audioList[index];
        }

        // ==================== 播放辅助 ====================

        private async Task PlayReplaceSafeAsync(AudioItem audio)
        {
            try { await _audioPlayer.PlayAsync(audio.Src, audio.Id, PlayMode.Overlay); }
            catch (Exception ex) { Debug.WriteLine($"[BgPlayback] 替换播放异常: {ex.Message}"); }
        }

        /// <summary>单曲循环重启（不使用 PlayLoopAsync，避免对象池事件残留）</summary>
        private async Task RestartSingleLoopAsync(string resourceId)
        {
            LoopState? state;
            PlaybackTaskInfo? taskInfo;
            lock (_lock)
            {
                if (!_loopStates.TryGetValue(resourceId, out state) || state.CurrentAudio == null) return;
                if (!_taskMap.TryGetValue(resourceId, out taskInfo)) return;
            }

            // 清理残留实例 ID（StopAll 不会触发 OnPlaybackStopped）
            lock (_lock) _playerMap.Remove(resourceId);

            taskInfo.CurrentAudioName = state.CurrentAudio.Name;
            RegisterAudioMapping(state.CurrentAudio.Id, resourceId);
            try
            {
                await _audioPlayer.PlayAsync(state.CurrentAudio.Src, state.CurrentAudio.Id, PlayMode.Overlay);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BgPlayback] 单曲循环重启异常: {ex.Message}");
            }
        }

        /// <summary>列表循环自动切歌（在 OnPlaybackStopped 中触发）</summary>
        private async Task AdvanceListLoopAsync(string resourceId)
        {
            LoopState? state;
            PlaybackTaskInfo? taskInfo;
            lock (_lock)
            {
                if (!_loopStates.TryGetValue(resourceId, out state)) return;
                if (!_taskMap.TryGetValue(resourceId, out taskInfo)) return;
            }

            // 清理残留实例 ID（StopAll 不会触发 OnPlaybackStopped）
            lock (_lock) _playerMap.Remove(resourceId);

            AudioItem next;
            if (state.OrderMode == OrderMode.Random)
            {
                List<AudioItem> pool;
                lock (_lock)
                {
                    if (!_listLoopPool.TryGetValue(resourceId, out pool!) || pool.Count == 0)
                    {
                        pool = new List<AudioItem>(state.AudioList);
                        _listLoopPool[resourceId] = pool;
                    }
                }
                var pickIdx = Random.Shared.Next(pool.Count);
                next = pool[pickIdx];
                pool.RemoveAt(pickIdx);
                Debug.WriteLine($"[BgPlayback] 随机池切歌: {next.Name}, 剩余 {pool.Count}/{state.AudioList.Count}");
            }
            else
            {
                var settings = _settingsService.GetSettings(resourceId);
                int index = settings.CurrentAudioIndex % state.AudioList.Count;
                settings.CurrentAudioIndex = (index + 1) % state.AudioList.Count;
                _settingsService.SaveSettings(resourceId, settings);
                next = state.AudioList[index];
            }

            try
            {
                taskInfo.CurrentAudioName = next.Name;
                RegisterAudioMapping(next.Id, resourceId);
                await _audioPlayer.PlayAsync(next.Src, next.Id, PlayMode.Overlay);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BgPlayback] 列表循环切歌异常: {ex.Message}");
            }
        }

        // ==================== 停止 ====================

        public void StopResource(string resourceId) => StopResourceInternal(resourceId);

        public void StopAll()
        {
            lock (_lock)
            {
                foreach (var id in _taskMap.Keys.ToList())
                    StopResourceInternal(id);
            }
            _audioPlayer.StopAll();
        }

        private void StopResourceInternal(string resourceId)
        {
            lock (_lock)
            {
                _loopStates.Remove(resourceId);
                _listLoopPool.Remove(resourceId);
                _activePlayModes.Remove(resourceId);

                if (!_playerMap.TryGetValue(resourceId, out var instances))
                    return;

                var copy = instances.ToList();
                _playerMap.Remove(resourceId);
                foreach (var instId in copy)
                {
                    _instanceToResource.Remove(instId);
                    _audioPlayer.StopInstance(instId);
                }

                var toRemove = _audioToResource.Where(kv => kv.Value == resourceId)
                    .Select(kv => kv.Key).ToList();
                foreach (var k in toRemove) _audioToResource.Remove(k);
            }
            RemoveTask(resourceId);
        }

        // ==================== 播放器事件 ====================

        private void RegisterAudioMapping(string audioId, string resourceId)
        {
            lock (_lock) _audioToResource[audioId] = resourceId;
        }

        private void OnPlaybackStarted(string instanceId)
        {
            Dispatcher.UIThread.Post(() =>
            {
                lock (_lock)
                {
                    var underscoreIdx = instanceId.LastIndexOf('_');
                    var audioId = underscoreIdx > 0 ? instanceId[..underscoreIdx] : instanceId;

                    if (!_audioToResource.TryGetValue(audioId, out var resourceId))
                        return;

                    _instanceToResource[instanceId] = resourceId;
                    if (!_playerMap.TryGetValue(resourceId, out var list))
                    {
                        list = new List<string>();
                        _playerMap[resourceId] = list;
                    }
                    list.Add(instanceId);
                    UpdateTaskState(resourceId);
                }
            });
        }

        private void OnPlaybackStopped(string instanceId)
        {
            Dispatcher.UIThread.Post(() =>
            {
                string? resourceId = null;
                bool shouldAdvance = false;
                bool shouldRestartSingle = false;

                lock (_lock)
                {
                    if (!_instanceToResource.TryGetValue(instanceId, out resourceId))
                        return;

                    _instanceToResource.Remove(instanceId);

                    if (!_playerMap.TryGetValue(resourceId, out var list))
                        return;

                    list.Remove(instanceId);
                    if (list.Count > 0)
                    {
                        UpdateTaskState(resourceId);
                        return;
                    }

                    // 所有实例已停止
                    _playerMap.Remove(resourceId);
                    var toRemove = _audioToResource.Where(kv => kv.Value == resourceId)
                        .Select(kv => kv.Key).ToList();
                    foreach (var k in toRemove) _audioToResource.Remove(k);

                    // 循环模式：任务不消失，自动继续
                    if (_loopStates.TryGetValue(resourceId, out var loopState))
                    {
                        if (loopState.Mode == LoopMode.ListLoop)
                            shouldAdvance = true;
                        else if (loopState.Mode == LoopMode.SingleLoop && loopState.CurrentAudio != null)
                            shouldRestartSingle = true;
                    }
                    else
                    {
                        RemoveTask(resourceId);
                    }
                }

                if (shouldAdvance && resourceId != null)
                    _ = AdvanceListLoopAsync(resourceId);
                else if (shouldRestartSingle && resourceId != null)
                    _ = RestartSingleLoopAsync(resourceId);
            });
        }

        // ==================== 任务 UI ====================

        private PlaybackTaskInfo GetOrCreateTask(Resource resource)
        {
            lock (_lock)
            {
                if (_taskMap.TryGetValue(resource.Id, out var existing))
                {
                    existing.CreatedAt = DateTime.Now;
                    return existing;
                }
                var taskInfo = new PlaybackTaskInfo
                {
                    ResourceId = resource.Id,
                    DisplayName = resource.DisplayName,
                    ShortName = resource.Name,
                    IconBitmap = LoadIconBitmap(resource.Icon),
                    CurrentAudioName = "准备中...",
                    IsActive = true,
                    ActivePlayerCount = 0,
                    CreatedAt = DateTime.Now
                };
                _taskMap[resource.Id] = taskInfo;
                _playerMap[resource.Id] = new List<string>();
                ActiveTasks.Insert(0, taskInfo);
                TaskStateChanged?.Invoke();
                return taskInfo;
            }
        }

        private void UpdateTaskState(string resourceId)
        {
            if (_taskMap.TryGetValue(resourceId, out var taskInfo))
            {
                var count = _playerMap.TryGetValue(resourceId, out var list) ? list.Count : 0;
                taskInfo.ActivePlayerCount = count;
                taskInfo.IsActive = count > 0;
                TaskStateChanged?.Invoke();
            }
        }

        private void RemoveTask(string resourceId)
        {
            lock (_lock)
            {
                if (_taskMap.Remove(resourceId, out var taskInfo))
                {
                    taskInfo.IsActive = false;
                    taskInfo.ActivePlayerCount = 0;
                    ActiveTasks.Remove(taskInfo);
                }
            }
            TaskStateChanged?.Invoke();
            lock (_lock)
            {
                if (_taskMap.Count == 0)
                    AllTasksCompleted?.Invoke();
            }
        }

        private bool HasResource(string resourceId)
        {
            lock (_lock) return _taskMap.ContainsKey(resourceId);
        }

        /// <summary>获取本地化后的顺序/随机标签</summary>
        private static string GetOrderLabel(OrderMode mode)
            => LocalizationService.Instance.Get(mode == OrderMode.Random ? "random_play" : "order_play");

        /// <summary>从本地文件路径加载图标 Bitmap，失败返回 null</summary>
        private static Bitmap? LoadIconBitmap(string iconPath)
        {
            if (string.IsNullOrEmpty(iconPath) || !File.Exists(iconPath))
                return null;
            try { return new Bitmap(iconPath); }
            catch { return null; }
        }
    }
}
