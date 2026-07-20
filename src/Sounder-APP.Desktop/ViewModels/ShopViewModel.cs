using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sounder_APP.Models;
using Sounder_APP.Services;

namespace Sounder_APP.ViewModels
{
    public partial class ShopViewModel : AudioPlaybackViewModelBase, ISearchViewModel
    {
        private readonly ResourceService _resourceService;
        private readonly DownloadManager _downloadManager;
        private bool _isUpdatingDetail;
        private int _detailLoadVersion;

        [ObservableProperty]
        private ObservableCollection<Resource> _resources = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isDetailLoading;

        [ObservableProperty]
        private bool _isLoadingMore;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isEmpty;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private string _statusText = LocalizationService.Instance.Get("loading");

        [ObservableProperty]
        private bool _hasMore = true;

        // ===== 下载/安装进度管理（支持多资源同时安装） =====

        private readonly ConcurrentDictionary<string, DownloadState> _downloadStates = new();

        private int _currentPage = 1;
        private bool _isSearchMode;

        public ShopViewModel(ResourceService resourceService, DownloadManager downloadManager)
        {
            _resourceService = resourceService;
            _downloadManager = downloadManager;
            _downloadManager.StateChanged += OnDownloadStateChanged;
            ResourceService.ResourceUninstalled += OnResourceUninstalled;
        }

        private void OnResourceUninstalled(string resourceId)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var resource = Resources.FirstOrDefault(r => r.Id == resourceId);
                if (resource != null)
                {
                    resource.IsInstalled = false;
                    Debug.WriteLine($"[ShopVM] 资源已标记为未安装: {resource.DisplayName}");
                }
            });
        }

        protected override void OnSelectedResourceChangedCore(Resource? value)
        {
            if (_isUpdatingDetail || value == null) return;
            Interlocked.Increment(ref _detailLoadVersion);
            _ = LoadDetailAsync(value);
        }

        public bool ShowDetailPanel => !IsLoading && !IsEmpty && !IsDetailLoading;

        partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(ShowDetailPanel));
        partial void OnIsEmptyChanged(bool value) => OnPropertyChanged(nameof(ShowDetailPanel));
        partial void OnIsDetailLoadingChanged(bool value) => OnPropertyChanged(nameof(ShowDetailPanel));

        public async Task ActivateAsync()
        {
            if (Resources.Count == 0 && !IsLoading && !HasError)
            {
                await LoadResourcesAsync();
            }

            // 恢复未完成下载的状态显示
            RestorePendingDownloadStates();
        }

        /// <summary>扫描 .download 文件，恢复未完成下载的资源在列表中的状态</summary>
        private void RestorePendingDownloadStates()
        {
            var pendingIds = DownloadManager.GetPendingDownloads();
            if (pendingIds.Count == 0) return;

            foreach (var resource in Resources)
            {
                if (pendingIds.Contains(resource.Id) && !resource.IsInstalled)
                {
                    resource.IsDownloadActive = false;
                    resource.IsDownloadPaused = true;
                    Debug.WriteLine($"[ShopVM] 恢复未完成下载状态: {resource.DisplayName} (id={resource.Id})");
                }
            }
        }

        [RelayCommand]
        private async Task LoadResourcesAsync()
        {
            try
            {
                IsLoading = true;
                HasError = false;
                _currentPage = 1;
                _isSearchMode = false;
                StatusText = LocalizationService.Instance.Get("loading_shop");

                var items = await _resourceService.GetCloudResourcesAsync(_currentPage);
                Resources = new ObservableCollection<Resource>(items);
                IsEmpty = Resources.Count == 0;
                HasMore = items.Count >= 10;

                if (Resources.Count > 0 && SelectedResource == null)
                {
                    _isUpdatingDetail = true;
                    SelectedResource = Resources[0];
                    _isUpdatingDetail = false;
                }
                else if (Resources.Count == 0)
                    SelectedResource = null;

                StatusText = IsEmpty
                    ? LocalizationService.Instance.Get("no_data")
                    : LocalizationService.Instance.Get("resource_count_fmt", Resources.Count);
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = LocalizationService.Instance.Get("load_error_fmt", ex.Message);
                StatusText = LocalizationService.Instance.Get("load_failed");
            }
            finally
            {
                IsLoading = false;
            }

            if (Resources.Count > 0 && SelectedResource != null)
                _ = LoadDetailAsync(SelectedResource);
        }

        [RelayCommand]
        private async Task LoadMoreAsync()
        {
            if (IsLoadingMore || !HasMore || _isSearchMode) return;
            try
            {
                IsLoadingMore = true;
                _currentPage++;
                var items = await _resourceService.GetCloudResourcesAsync(_currentPage);
                HasMore = items.Count >= 10;
                foreach (var item in items) Resources.Add(item);
                StatusText = LocalizationService.Instance.Get("resource_count_fmt", Resources.Count);
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                _isSearchMode = false;
                await LoadResourcesAsync();
                return;
            }

            IsLoading = true;
            _isSearchMode = true;
            HasError = false;
            try
            {
                var results = await _resourceService.SearchCloudResourcesAsync(SearchQuery);
                Resources = new ObservableCollection<Resource>(results);
                IsEmpty = Resources.Count == 0;
                if (Resources.Count > 0)
                {
                    _isUpdatingDetail = true;
                    SelectedResource = Resources[0];
                    _isUpdatingDetail = false;
                }
                else
                {
                    SelectedResource = null;
                }
                HasMore = false;
                StatusText = IsEmpty
                    ? LocalizationService.Instance.Get("no_match_resource")
                    : LocalizationService.Instance.Get("found_resource_count_fmt", Resources.Count);
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = LocalizationService.Instance.Get("search_error_fmt", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }

            if (Resources.Count > 0 && SelectedResource != null)
                _ = LoadDetailAsync(SelectedResource);
        }

        [RelayCommand]
        private async Task RetryAsync()
        {
            HasError = false;
            await LoadResourcesAsync();
        }

        // ===== 下载状态变更处理（参照 Android StateChanged Flow） =====

        private void OnDownloadStateChanged(DownloadState state)
        {
            _downloadStates[state.ResourceId] = state;

            Dispatcher.UIThread.Post(() =>
            {
                // 查找对应的资源并更新其独立下载状态
                var resource = Resources.FirstOrDefault(r => r.Id == state.ResourceId);
                if (resource == null) return;

                switch (state.Status)
                {
                    case DownloadStatus.Downloading:
                        resource.IsDownloadActive = true;
                        resource.IsDownloadPaused = false;
                        resource.DownloadProgress = state.Progress;
                        break;
                    case DownloadStatus.Paused:
                        resource.IsDownloadActive = true;
                        resource.IsDownloadPaused = true;
                        resource.DownloadProgress = state.Progress;
                        break;
                    case DownloadStatus.Failed:
                        resource.IsDownloadActive = false;
                        resource.IsDownloadPaused = false;
                        break;
                    case DownloadStatus.Completed:
                        resource.IsInstalled = true;
                        resource.IsDownloadActive = false;
                        resource.IsDownloadPaused = false;
                        resource.DownloadProgress = 1f;
                        break;
                }
            });
        }

        // ===== 安装 / 暂停 / 取消 =====

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task InstallResourceAsync(Resource? resource)
        {
            if (resource == null || resource.IsInstalled) return;

            // 暂停状态 → 恢复下载
            if (_downloadManager.IsDownloadPaused(resource.Id))
            {
                resource.IsDownloadPaused = false;
                resource.IsDownloadActive = true;
                _downloadManager.Cleanup(resource.Id);

                try
                {
                    var success = await _downloadManager.InstallResourceByIdAsync(resource.Id);
                    if (success)
                    {
                        var idx = Resources.IndexOf(resource);
                        if (idx >= 0)
                        {
                            resource.IsInstalled = true;
                            resource.IsDownloadActive = false;
                            resource.IsDownloadPaused = false;
                            Resources[idx] = resource;
                        }
                    }
                }
                catch (Exception)
                {
                    resource.IsDownloadActive = false;
                    resource.IsDownloadPaused = false;
                }
                finally
                {
                    _downloadStates.TryRemove(resource.Id, out _);
                }
                return;
            }

            // 下载中 → 暂停
            if (_downloadManager.IsDownloadActive(resource.Id))
            {
                _downloadManager.PauseDownload(resource.Id);
                return;
            }

            // 空闲 → 开始下载
            resource.IsDownloadActive = true;
            resource.IsDownloadPaused = false;
            resource.DownloadProgress = 0f;

            try
            {
                var success = await _downloadManager.InstallResourceByIdAsync(resource.Id);
                if (success)
                {
                    var idx = Resources.IndexOf(resource);
                    if (idx >= 0)
                    {
                        resource.IsInstalled = true;
                        resource.IsDownloadActive = false;
                        resource.IsDownloadPaused = false;
                        Resources[idx] = resource;
                    }
                }
            }
            catch (Exception)
            {
                resource.IsDownloadActive = false;
                resource.IsDownloadPaused = false;
            }
            finally
            {
                _downloadStates.TryRemove(resource.Id, out _);
            }
        }

        [RelayCommand]
        private void CancelInstall()
        {
            foreach (var id in _downloadManager.GetActiveDownloadIds())
            {
                _downloadManager.PauseDownload(id);
            }
        }

        private async Task LoadDetailAsync(Resource resource, bool showSkeleton = true)
        {
            var version = showSkeleton ? Interlocked.Increment(ref _detailLoadVersion) : 0;
            if (showSkeleton)
                IsDetailLoading = true;
            try
            {
                var detail = await _resourceService.GetResourceDetailAsync(resource.Id);
                if (detail != null && (!showSkeleton || version == _detailLoadVersion))
                {
                    _isUpdatingDetail = true;
                    var index = Resources.IndexOf(resource);
                    if (index >= 0)
                    {
                        var old = Resources[index];
                        if (old.Icon == detail.Icon && old.IconBitmap != null)
                            detail.PreserveIcon(old.IconBitmap);
                        detail.IsInstalled = old.IsInstalled;
                        Resources[index] = detail;
                        SelectedResource = detail;
                    }
                    else
                    {
                        SelectedResource = detail;
                    }
                    _isUpdatingDetail = false;
                }
            }
            finally
            {
                if (showSkeleton && version == _detailLoadVersion)
                    IsDetailLoading = false;
            }
        }
    }
}
