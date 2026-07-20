using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sounder_APP.Models;
using Sounder_APP.Services;

namespace Sounder_APP.ViewModels
{
    public partial class SubmissionsViewModel : ViewModelBase, ISearchViewModel
    {
        private const int PageSize = 10;

        private readonly ApiService _api;

        IAsyncRelayCommand ISearchViewModel.SearchCommand => SearchCommand;

        // ===== 标签页状态 =====

        [ObservableProperty]
        private int _activeTabIndex;

        /// <summary>当前激活标签: "inProgress" / "waiting" / "completed"</summary>
        [ObservableProperty]
        private string _activeTab = "inProgress";

        // ===== 加载状态 =====

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isLoadingMore;

        [ObservableProperty]
        private bool _hasMore;

        /// <summary>是否显示加载更多（加载中隐藏）</summary>
        public bool ShowLoadMore => !IsLoading && HasMore;

        /// <summary>进行中数量</summary>
        [ObservableProperty]
        private int _inProgressCount;

        /// <summary>待审核数量</summary>
        [ObservableProperty]
        private int _waitingCount;

        /// <summary>已完成数量</summary>
        [ObservableProperty]
        private int _completedCount;

        // ===== 搜索 =====

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        // ===== 数据 =====

        [ObservableProperty]
        private ObservableCollection<Submission> _submissions = new();

        /// <summary>按日期分组的投稿（时间线视图用）</summary>
        [ObservableProperty]
        private ObservableCollection<SubmissionDateGroup> _groupedSubmissions = new();

        /// <summary>是否有投稿数据（加载中隐藏）</summary>
        public bool HasSubmissions => !IsLoading && Submissions.Count > 0;

        /// <summary>没有投稿数据（加载中隐藏）</summary>
        public bool NoSubmissions => !IsLoading && Submissions.Count == 0;

        /// <summary>是否显示时间线视图（进行中/已完成标签下显示）</summary>
        public bool IsTimelineView => ActiveTab != "waiting";

        /// <summary>时间线颜色（进行中=蓝色, 已完成=绿色）</summary>
        public string TimelineColor => ActiveTab switch
        {
            "completed" => "#10B981",
            _ => "#5DA3E8"
        };

        // ===== 表单抽屉 =====

        [ObservableProperty]
        private bool _isFormDrawerOpen;

        [ObservableProperty]
        private bool _isFormSubmitting;

        /// <summary>表单 ViewModel</summary>
        public SubmissionFormViewModel FormViewModel { get; }

        // ===== 分页 =====
        private int _currentPage = 1;
        private int _totalCount;

        public SubmissionsViewModel(ApiService api)
        {
            _api = api;
            FormViewModel = new SubmissionFormViewModel();
            FormViewModel.Submitted += OnFormSubmitted;
            FormViewModel.Cancelled += OnFormCancelled;

            Submissions.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(HasSubmissions));
                OnPropertyChanged(nameof(NoSubmissions));
            };
        }

        partial void OnIsLoadingChanged(bool value)
        {
            OnPropertyChanged(nameof(HasSubmissions));
            OnPropertyChanged(nameof(NoSubmissions));
            OnPropertyChanged(nameof(ShowLoadMore));
        }

        partial void OnHasMoreChanged(bool value) => OnPropertyChanged(nameof(ShowLoadMore));

        partial void OnActiveTabChanged(string value)
        {
            OnPropertyChanged(nameof(IsTimelineView));
            OnPropertyChanged(nameof(TimelineColor));
        }

        /// <summary>
        /// 激活时加载数据（已有缓存则跳过，和商店行为一致）
        /// </summary>
        [RelayCommand]
        private async Task ActivateAsync()
        {
            if (Submissions.Count == 0 && !IsLoading)
                await LoadSubmissionsAsync(reset: true);
        }

        // ===== 标签切换 =====

        [RelayCommand]
        private async Task SwitchTabAsync(string tab)
        {
            ActiveTab = tab;
            ActiveTabIndex = tab switch
            {
                "inProgress" => 0,
                "waiting" => 1,
                "completed" => 2,
                _ => 0
            };
            await LoadSubmissionsAsync(reset: true);
        }

        // ===== 搜索 =====

        [RelayCommand]
        private async Task SearchAsync()
        {
            await LoadSubmissionsAsync(reset: true);
        }

        // ===== 数据加载 =====

        private async Task LoadSubmissionsAsync(bool reset = true)
        {
            if (reset)
            {
                _currentPage = 1;
                IsLoading = true;
            }

            try
            {
                var status = MapTabToStatus();
                var keyword = string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery.Trim();

                var result = await _api.GetSubmissionsAsync(_currentPage, PageSize, status, keyword);

                if (reset)
                {
                    Submissions.Clear();
                }

                if (result.Success && result.Data != null)
                {
                    _totalCount = result.Data.Total;
                    foreach (var remote in result.Data.Submissions)
                    {
                        Submissions.Add(Submission.FromRemote(remote));
                    }

                    // 更新各状态计数（首次加载时从 statusCounts 获取）
                    if (reset && result.Data.StatusCounts != null)
                    {
                        var sc = result.Data.StatusCounts;
                        InProgressCount = sc.InProgress;
                        WaitingCount = sc.Pending;
                        CompletedCount = sc.Completed;
                    }
                }

                RefreshGroups();

                HasMore = Submissions.Count < _totalCount;
            }
            catch (Exception)
            {
                // 加载失败，保持现有数据
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task LoadMoreAsync()
        {
            IsLoadingMore = true;
            try
            {
                _currentPage++;
                await LoadSubmissionsAsync(reset: false);
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        // ===== 表单抽屉 =====

        [RelayCommand]
        private void OpenFormDrawer()
        {
            FormViewModel.Reset();
            IsFormDrawerOpen = true;
        }

        [RelayCommand]
        private void CloseFormDrawer()
        {
            IsFormDrawerOpen = false;
            IsFormSubmitting = false;
        }

        private async void OnFormSubmitted(object? sender, EventArgs e)
        {
            IsFormSubmitting = true;
            try
            {
                var payload = new
                {
                    resourceName = FormViewModel.ResourceName,
                    appName = FormViewModel.AppName,
                    brief = FormViewModel.Brief,
                    platform = FormViewModel.Platform,
                    platformId = FormViewModel.PlatformId,
                    nickname = FormViewModel.Nickname,
                    imageSource = FormViewModel.ImageSource,
                    imageSourceLink = FormViewModel.ImageSourceLink,
                    voiceSource = FormViewModel.VoiceSource,
                    voiceSourceLink = FormViewModel.VoiceSourceLink,
                    fileLink = FormViewModel.FileLink
                };

                var result = await _api.CreateSubmissionAsync(payload);

                if (result.Success)
                {
                    IsFormDrawerOpen = false;
                    await LoadSubmissionsAsync(reset: true);
                }
            }
            catch (Exception)
            {
                // 提交失败
            }
            finally
            {
                IsFormSubmitting = false;
            }
        }

        private void OnFormCancelled(object? sender, EventArgs e)
        {
            IsFormDrawerOpen = false;
            IsFormSubmitting = false;
        }

        // ===== 工具方法 =====

        private void RefreshGroups()
        {
            GroupedSubmissions.Clear();
            var color = TimelineColor;
            var groups = Submissions
                .GroupBy(s => string.IsNullOrEmpty(s.PlannedDate) ? "未排期" : s.PlannedDate)
                .OrderByDescending(g => g.Key)
                .Select(g => new SubmissionDateGroup
                {
                    Date = g.Key,
                    TimelineColor = color,
                    Items = new ObservableCollection<Submission>(g)
                });

            foreach (var g in groups)
                GroupedSubmissions.Add(g);
        }

        private string MapTabToStatus()
        {
            return ActiveTab switch
            {
                "waiting" => "pending",
                "inProgress" => "in_progress",
                "completed" => "completed",
                _ => "in_progress"
            };
        }
    }

    /// <summary>
    /// 按日期分组的投稿组
    /// </summary>
    public class SubmissionDateGroup
    {
        public string Date { get; set; } = string.Empty;
        public ObservableCollection<Submission> Items { get; set; } = new();
        public string TimelineColor { get; set; } = "#5DA3E8";

        /// <summary>相对日期描述（今天/昨天/前天/N天后/N天前）</summary>
        public string FormattedDate => string.IsNullOrEmpty(Date) ? "" : FormatRelativeDate(Date);

        private static string FormatRelativeDate(string dateStr)
        {
            if (!DateTime.TryParse(dateStr, out var date)) return dateStr;

            var today = DateTime.Today;
            var diffDays = (date - today).Days;

            return diffDays switch
            {
                0 => "今天",
                -1 => "昨天",
                -2 => "前天",
                1 => "明天",
                2 => "后天",
                > 2 => $"{diffDays}天后",
                _ => $"{-diffDays}天前"
            };
        }
    }
}
