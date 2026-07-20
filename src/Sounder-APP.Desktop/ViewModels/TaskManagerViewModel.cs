using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sounder_APP.Models;
using Sounder_APP.Services;

namespace Sounder_APP.ViewModels
{
    /// <summary>
    /// 任务管理 ViewModel — 展示所有通过桌面快捷方式触发的高活跃后台播放任务。
    /// 对应 Node.js mine.vue 中 itemStates 的可视化管理。
    /// </summary>
    public partial class TaskManagerViewModel : ViewModelBase, ISearchViewModel
    {
        private readonly BackgroundPlaybackManager _playbackManager;

        [ObservableProperty]
        private ObservableCollection<PlaybackTaskInfo> _activeTasks;

        [ObservableProperty]
        private bool _hasTasks;

        [ObservableProperty]
        private int _taskCount;

        [ObservableProperty]
        private string _statusText = ""; // 构造函数中 RefreshState() 立即覆盖为本地化值

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isSearchActive;

        /// <summary>搜索模式下无匹配结果（区别于空列表）</summary>
        public bool HasNoMatch => IsSearchActive && TaskCount == 0 && !HasTasks;

        /// <summary>列表为空且不在搜索模式（初始空状态）</summary>
        public bool IsEmptyList => !HasTasks && !IsSearchActive;

        private List<PlaybackTaskInfo> _allTasks = new();

        public TaskManagerViewModel(BackgroundPlaybackManager playbackManager)
        {
            _playbackManager = playbackManager;
            _activeTasks = playbackManager.ActiveTasks;
            _playbackManager.TaskStateChanged += OnTaskStateChanged;
            RefreshState();
        }

        private void OnTaskStateChanged()
        {
            RefreshState();
        }

        private void RefreshState()
        {
            // 保存所有任务引用
            _allTasks = ActiveTasks.ToList();
            TaskCount = ActiveTasks.Count;
            HasTasks = TaskCount > 0;
            StatusText = HasTasks
                ? LocalizationService.Instance.Get("task_in_progress_fmt", TaskCount)
                : LocalizationService.Instance.Get("no_active_tasks");
            OnPropertyChanged(nameof(HasNoMatch));
            OnPropertyChanged(nameof(IsEmptyList));
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                IsSearchActive = false;
                // 恢复全部任务
                ActiveTasks = new ObservableCollection<PlaybackTaskInfo>(_allTasks);
                TaskCount = ActiveTasks.Count;
                HasTasks = TaskCount > 0;
                StatusText = HasTasks
                    ? LocalizationService.Instance.Get("task_in_progress_fmt", TaskCount)
                    : LocalizationService.Instance.Get("no_active_tasks");
            }
            else
            {
                IsSearchActive = true;
                var query = SearchQuery.Trim().ToLower();
                var filtered = _allTasks
                    .Where(t => t.DisplayName.Contains(query, System.StringComparison.OrdinalIgnoreCase)
                             || t.ShortName.Contains(query, System.StringComparison.OrdinalIgnoreCase)
                             || t.CurrentAudioName.Contains(query, System.StringComparison.OrdinalIgnoreCase))
                    .ToList();
                ActiveTasks = new ObservableCollection<PlaybackTaskInfo>(filtered);
                TaskCount = ActiveTasks.Count;
                HasTasks = TaskCount > 0;
                StatusText = HasTasks
                    ? LocalizationService.Instance.Get("found_tasks_fmt", TaskCount)
                    : LocalizationService.Instance.Get("no_match_task");
            }
            OnPropertyChanged(nameof(HasNoMatch));
            OnPropertyChanged(nameof(IsEmptyList));

            await System.Threading.Tasks.Task.CompletedTask;
        }

        /// <summary>停止单个资源的后台播放</summary>
        [RelayCommand]
        private void StopTask(PlaybackTaskInfo? taskInfo)
        {
            if (taskInfo == null) return;
            Debug.WriteLine($"[TaskManager] 停止任务: {taskInfo.DisplayName}");
            _playbackManager.StopResource(taskInfo.ResourceId);
        }

        /// <summary>停止全部后台播放</summary>
        [RelayCommand]
        private void StopAllTasks()
        {
            Debug.WriteLine("[TaskManager] 停止全部任务");
            _playbackManager.StopAll();
        }
    }
}
