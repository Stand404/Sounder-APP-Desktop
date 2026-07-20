using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sounder_APP.Models;
using Sounder_APP.Services;
using Sounder_APP.Views;

namespace Sounder_APP.ViewModels
{
    /// <summary>
    /// 音频播放状态管理基类 — 封装了播放列表跟踪、资源级播放计数、停止按钮显隐、
    /// 播放模式（叠加/替换/循环）等公共逻辑。
    /// ShopViewModel 和 PersonalResourceViewModel 均继承此类。
    /// </summary>
    public abstract partial class AudioPlaybackViewModelBase : ViewModelBase
    {
        protected readonly AudioPlaybackService _audioPlayer;
        private readonly PlaybackSettingsService _settingsService;
        private bool _isLoadingSettings;

        // ===== 独立音频项状态（参考 Android DetailUiState 的 Set<Int> 方案） =====

        private readonly ObservableSet _loadingAudioIds = new();
        public ObservableSet LoadingAudioIds => _loadingAudioIds;

        private readonly ObservableSet _playingAudioIds = new();
        public ObservableSet PlayingAudioIds => _playingAudioIds;

        private int _loadingVersion;
        private int _playingVersion;
        public int LoadingVersion => _loadingVersion;
        public int PlayingVersion => _playingVersion;

        [ObservableProperty]
        private string _currentAudioName = string.Empty;

        /// <summary>每个资源当前的活跃播放器数量（用于判断停止按钮显隐）</summary>
        private readonly Dictionary<string, int> _resourcePlayerCount = new();

        /// <summary>audioId → resourceId 映射（用于播放停止时查找所属资源）</summary>
        private readonly Dictionary<string, string> _audioToResource = new();

        [ObservableProperty]
        private bool _isAudioActive;

        [ObservableProperty]
        private PlayMode _playMode = PlayMode.Overlay;

        [ObservableProperty]
        private LoopMode _loopMode = LoopMode.SingleLoop;

        [ObservableProperty]
        private OrderMode _orderMode = OrderMode.Order;

        /// <summary>当前选中资源（由子类读写，用于计算 IsAudioActive 和资源计数）</summary>
        [ObservableProperty]
        private Resource? _selectedResource;

        // ===== 列表循环状态 =====

        /// <summary>是否正在进行列表循环播放（用于自动切歌）</summary>
        private bool _isListLoopSession;

        /// <summary>列表循环中当前播放的音频索引</summary>
        private int _currentListLoopIndex;

        protected AudioPlaybackViewModelBase()
        {
            _settingsService = new PlaybackSettingsService();
            _audioPlayer = new AudioPlaybackService();
            _audioPlayer.PlaybackStarted += OnPlaybackStarted;
            _audioPlayer.PlaybackStopped += OnPlaybackStopped;
        }

        // ===== 选中资源变更 =====

        partial void OnSelectedResourceChanged(Resource? value)
        {
            // 切换资源时停止所有当前播放
            StopAllPlayback();
            // 加载新资源的播放设置
            if (value != null)
                LoadPlaybackSettings(value.Id);
            UpdateIsAudioActive();
            OnSelectedResourceChangedCore(value);
        }

        /// <summary>子类可重写以在选中资源变更时执行额外逻辑（如加载详情）</summary>
        protected virtual void OnSelectedResourceChangedCore(Resource? value) { }

        // ===== 播放设置持久化 =====

        /// <summary>从持久化存储加载指定资源的播放设置</summary>
        private void LoadPlaybackSettings(string resourceId)
        {
            _isLoadingSettings = true;
            var settings = _settingsService.GetSettings(resourceId);
            PlayMode = settings.PlayMode;
            LoopMode = settings.LoopMode;
            OrderMode = settings.OrderMode;
            _isLoadingSettings = false;
        }

        /// <summary>保存当前资源的播放设置到持久化存储</summary>
        private void SaveCurrentPlaybackSettings()
        {
            if (_isLoadingSettings || SelectedResource == null) return;
            _settingsService.SaveSettings(SelectedResource.Id, new PlaybackSettings
            {
                PlayMode = PlayMode,
                LoopMode = LoopMode,
                OrderMode = OrderMode
            });
        }

        partial void OnPlayModeChanged(PlayMode value) => SaveCurrentPlaybackSettings();

        partial void OnLoopModeChanged(LoopMode value) => SaveCurrentPlaybackSettings();

        partial void OnOrderModeChanged(OrderMode value) => SaveCurrentPlaybackSettings();

        // ===== 集合操作辅助方法 =====

        private void AddLoading(string id)
        {
            _loadingAudioIds.Add(id);
            _loadingVersion++;
            Debug.WriteLine($"[AudioState] AddLoading: {id}, LoadingIds.Count={_loadingAudioIds.Count}, PlayingIds.Count={_playingAudioIds.Count}");
            OnPropertyChanged(nameof(LoadingAudioIds));
            OnPropertyChanged(nameof(LoadingVersion));
            UpdateIsAudioActive();
        }

        private void RemoveLoading(string id)
        {
            _loadingAudioIds.Remove(id);
            _loadingVersion++;
            Debug.WriteLine($"[AudioState] RemoveLoading: {id}, LoadingIds.Count={_loadingAudioIds.Count}, PlayingIds.Count={_playingAudioIds.Count}");
            OnPropertyChanged(nameof(LoadingAudioIds));
            OnPropertyChanged(nameof(LoadingVersion));
            UpdateIsAudioActive();
        }

        private void ClearLoading()
        {
            _loadingAudioIds.Clear();
            _loadingVersion++;
            Debug.WriteLine($"[AudioState] ClearLoading, LoadingIds.Count={_loadingAudioIds.Count}, PlayingIds.Count={_playingAudioIds.Count}");
            OnPropertyChanged(nameof(LoadingAudioIds));
            OnPropertyChanged(nameof(LoadingVersion));
            UpdateIsAudioActive();
        }

        private void AddPlaying(string id)
        {
            _playingAudioIds.Add(id);
            _playingVersion++;
            Debug.WriteLine($"[AudioState] AddPlaying: {id}, LoadingIds.Count={_loadingAudioIds.Count}, PlayingIds.Count={_playingAudioIds.Count}");
            OnPropertyChanged(nameof(PlayingAudioIds));
            OnPropertyChanged(nameof(PlayingVersion));
            UpdateIsAudioActive();
        }

        private void RemovePlaying(string id)
        {
            _playingAudioIds.Remove(id);
            _playingVersion++;
            Debug.WriteLine($"[AudioState] RemovePlaying: {id}, LoadingIds.Count={_loadingAudioIds.Count}, PlayingIds.Count={_playingAudioIds.Count}");
            OnPropertyChanged(nameof(PlayingAudioIds));
            OnPropertyChanged(nameof(PlayingVersion));
            UpdateIsAudioActive();
        }

        private void ClearPlaying()
        {
            _playingAudioIds.Clear();
            _playingVersion++;
            Debug.WriteLine($"[AudioState] ClearPlaying, LoadingIds.Count={_loadingAudioIds.Count}, PlayingIds.Count={_playingAudioIds.Count}");
            OnPropertyChanged(nameof(PlayingAudioIds));
            OnPropertyChanged(nameof(PlayingVersion));
            UpdateIsAudioActive();
        }

        private void ClearAllStates()
        {
            ClearLoading();
            ClearPlaying();
            CurrentAudioName = string.Empty;
        }

        // ===== 资源级播放计数管理 =====

        private void UpdateIsAudioActive()
        {
            var currentId = SelectedResource?.Id ?? "";
            IsAudioActive = _resourcePlayerCount.TryGetValue(currentId, out var count) && count > 0;
        }

        /// <summary>原子化替换所有现有播放状态为单一的新的加载项。</summary>
        private void ResetToSingleState(string newId, string newName)
        {
            _loadingAudioIds.Clear();
            _playingAudioIds.Clear();
            _loadingVersion++;
            _playingVersion++;
            OnPropertyChanged(nameof(LoadingAudioIds));
            OnPropertyChanged(nameof(LoadingVersion));
            OnPropertyChanged(nameof(PlayingAudioIds));
            OnPropertyChanged(nameof(PlayingVersion));

            _loadingAudioIds.Add(newId);
            _loadingVersion++;
            OnPropertyChanged(nameof(LoadingAudioIds));
            OnPropertyChanged(nameof(LoadingVersion));

            CurrentAudioName = newName;

            var resourceId = SelectedResource?.Id ?? "";
            _resourcePlayerCount.Clear();
            _audioToResource.Clear();
            _resourcePlayerCount[resourceId] = 1;
            _audioToResource[newId] = resourceId;

            IsAudioActive = true;
        }

        /// <summary>递增当前选中资源的播放计数。</summary>
        private void IncrementResourceCount(string audioId)
        {
            var resourceId = SelectedResource?.Id ?? "";
            _audioToResource[audioId] = resourceId;
            _resourcePlayerCount.TryGetValue(resourceId, out var count);
            _resourcePlayerCount[resourceId] = count + 1;
            UpdateIsAudioActive();
        }

        /// <summary>递减指定音频所属资源的播放计数。</summary>
        private void DecrementResourceCount(string audioId)
        {
            if (_audioToResource.TryGetValue(audioId, out var resourceId))
            {
                if (_resourcePlayerCount.TryGetValue(resourceId, out var count))
                {
                    count--;
                    if (count <= 0)
                        _resourcePlayerCount.Remove(resourceId);
                    else
                        _resourcePlayerCount[resourceId] = count;
                }
            }
            UpdateIsAudioActive();
        }

        // ===== 播放生命周期回调 =====

        private void OnPlaybackStarted(string instanceId)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var audioId = instanceId;
                var underscoreIdx = instanceId.LastIndexOf('_');
                if (underscoreIdx > 0)
                    audioId = instanceId[..underscoreIdx];

                RemoveLoading(audioId);
                AddPlaying(audioId);
            });
        }

        private void OnPlaybackStopped(string instanceId)
        {
            var isError = instanceId.StartsWith("error_");

            Dispatcher.UIThread.Post(async () =>
            {
                // 提取原始 audioId：instancdId 格式为 "{audioId}_{counter}"，
                // 但错误路径会发送 "error_{audioId}"，此时保留完整 audioId
                var audioId = instanceId;
                var underscoreIdx = instanceId.LastIndexOf('_');
                if (isError)
                {
                    // "error_{audioId}" → 直接取 "error_" 之后的部分
                    audioId = instanceId.Length > 6 ? instanceId[6..] : instanceId;
                }
                else if (underscoreIdx > 0)
                {
                    // "{audioId}_{counter}" → 去掉后缀
                    audioId = instanceId[..underscoreIdx];
                }

                // 播放出错时向用户展示通知
                if (isError)
                {
                    var L = LocalizationService.Instance;
                    await DirtyConfirmWindow.ShowToastAsync(L.Get("error"), L.Get("play_failed"));
                }

                RemovePlaying(audioId);

                if (_audioToResource.TryGetValue(audioId, out var resourceId))
                {
                    if (_resourcePlayerCount.TryGetValue(resourceId, out var count))
                    {
                        count--;
                        if (count <= 0)
                            _resourcePlayerCount.Remove(resourceId);
                        else
                            _resourcePlayerCount[resourceId] = count;
                    }
                }

                if (_playingAudioIds.IsEmpty && _loadingAudioIds.IsEmpty)
                {
                    CurrentAudioName = string.Empty;
                }
                UpdateIsAudioActive();

                // 列表循环：当前音频播放完毕后自动播放下一首
                if (_isListLoopSession)
                {
                    _ = PlayNextInListAsync();
                }
            });
        }

        /// <summary>列表循环模式下自动播放下一首音频</summary>
        private async Task PlayNextInListAsync()
        {
            var audioList = SelectedResource?.AudioItems
                ?.Where(a => !string.IsNullOrEmpty(a.Src))
                ?.OrderBy(a => a.OrderIndex)
                ?.ToList();

            if (audioList == null || audioList.Count == 0)
            {
                _isListLoopSession = false;
                return;
            }

            // 计算下一首索引
            int nextIndex;
            if (OrderMode == OrderMode.Random)
            {
                nextIndex = Random.Shared.Next(audioList.Count);
            }
            else
            {
                nextIndex = (_currentListLoopIndex + 1) % audioList.Count;
            }

            _currentListLoopIndex = nextIndex;
            var nextItem = audioList[nextIndex];

            Debug.WriteLine($"[ListLoop] 自动切歌: {nextItem.Name} (index={nextIndex})");

            _loadingAudioIds.Clear();
            _playingAudioIds.Clear();
            _loadingVersion++;
            _playingVersion++;
            OnPropertyChanged(nameof(LoadingAudioIds));
            OnPropertyChanged(nameof(LoadingVersion));
            OnPropertyChanged(nameof(PlayingAudioIds));
            OnPropertyChanged(nameof(PlayingVersion));

            AddLoading(nextItem.Id);
            CurrentAudioName = nextItem.Name;

            var resourceId = SelectedResource?.Id ?? "";
            _resourcePlayerCount.Clear();
            _audioToResource.Clear();
            _resourcePlayerCount[resourceId] = 1;
            _audioToResource[nextItem.Id] = resourceId;
            IsAudioActive = true;

            var instanceId = await _audioPlayer.PlayAsync(nextItem.Src, nextItem.Id, PlayMode.Replace);
            if (instanceId == null)
            {
                RemoveLoading(nextItem.Id);
                DecrementResourceCount(nextItem.Id);
                // 播放失败也尝试继续下一首
                if (_isListLoopSession)
                    _ = PlayNextInListAsync();
            }
        }

        // ===== 公开命令 =====

        /// <summary>
        /// 播放/暂停。根据当前 PlayMode 决定行为：
        /// - Overlay：每次点击创建独立实例，新老音频同时播放
        /// - Replace：停止所有后播放当前
        /// - Loop + SingleLoop：停止所有后单曲循环
        /// - Loop + ListLoop：停止所有后从当前音频开始列表循环
        /// 注意：试听模式（未安装资源）只开放 Overlay/Replace 两种模式，不开放 Loop。
        /// </summary>
        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task TogglePlayAudio(AudioItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.Src)) return;

            if (PlayMode == PlayMode.Overlay)
            {
                // 叠加模式：不停止现有播放，创建独立的新实例
                _isListLoopSession = false;
                var isNewClick = !_loadingAudioIds.Contains(item.Id) && !_playingAudioIds.Contains(item.Id);
                if (isNewClick)
                    AddLoading(item.Id);
                CurrentAudioName = item.Name;
                IncrementResourceCount(item.Id);

                var instanceId = await _audioPlayer.PlayAsync(item.Src, item.Id, PlayMode.Overlay);
                if (instanceId == null)
                {
                    if (isNewClick)
                        RemoveLoading(item.Id);
                    DecrementResourceCount(item.Id);
                }
            }
            else if (PlayMode == PlayMode.Replace)
            {
                // 替换模式：停止所有后播放当前
                _isListLoopSession = false;
                _audioPlayer.StopAll();
                ResetToSingleState(item.Id, item.Name);

                var instanceId = await _audioPlayer.PlayAsync(item.Src, item.Id, PlayMode.Replace);
                if (instanceId == null)
                {
                    RemoveLoading(item.Id);
                    DecrementResourceCount(item.Id);
                }
            }
            else if (PlayMode == PlayMode.Loop && LoopMode == LoopMode.SingleLoop)
            {
                // 单曲循环
                _isListLoopSession = false;
                _audioPlayer.StopAll();
                ResetToSingleState(item.Id, item.Name);

                var instanceId = await _audioPlayer.PlayLoopAsync(item.Src, item.Id);
                if (instanceId == null)
                {
                    RemoveLoading(item.Id);
                    DecrementResourceCount(item.Id);
                }
            }
            else if (PlayMode == PlayMode.Loop && LoopMode == LoopMode.ListLoop)
            {
                // 列表循环：从当前音频开始，播放完毕后自动切歌
                _audioPlayer.StopAll();
                ResetToSingleState(item.Id, item.Name);
                _currentListLoopIndex = 0;
                _isListLoopSession = true;

                var instanceId = await _audioPlayer.PlayAsync(item.Src, item.Id, PlayMode.Replace);
                if (instanceId == null)
                {
                    _isListLoopSession = false;
                    RemoveLoading(item.Id);
                    DecrementResourceCount(item.Id);
                }
            }
        }

        [RelayCommand]
        private void SetPlayMode(PlayMode mode) => PlayMode = mode;

        [RelayCommand]
        private void SetLoopMode(LoopMode mode) => LoopMode = mode;

        [RelayCommand]
        private void SetOrderMode() => SetOrderModeTo(OrderMode == OrderMode.Order ? OrderMode.Random : OrderMode.Order);

        [RelayCommand]
        private void SetOrderModeTo(OrderMode mode) => OrderMode = mode;

        /// <summary>停止所有音频播放（公开方法，供外部调用）</summary>
        public void StopAllPlayback()
        {
            _isListLoopSession = false;
            _audioPlayer.StopAll();
            _resourcePlayerCount.Clear();
            _audioToResource.Clear();
            ClearAllStates();
        }

        /// <summary>停止所有音频播放</summary>
        [RelayCommand]
        private void StopAudio()
        {
            StopAllPlayback();
        }
    }
}
