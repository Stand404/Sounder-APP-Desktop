using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NAudio.Wave;
using SkiaSharp;
using Sortable.Avalonia;
using Sounder_APP.Models;
using Sounder_APP.Services;

namespace Sounder_APP.ViewModels
{
    /// <summary>
    /// 个人资源编辑页 ViewModel
    /// </summary>
    public partial class PersonalResourceEditViewModel : ViewModelBase
    {
        private readonly ResourceService _resourceService;
        private readonly DownloadManager _downloadManager;
        private readonly AudioPlaybackService _audioPlayer;
        private readonly Resource _originalResource;
        private readonly Action _onLeave;

        // ===== 编辑数据 =====

        [ObservableProperty]
        private string _editDisplayName = string.Empty;

        [ObservableProperty]
        private string _editName = string.Empty;

        [ObservableProperty]
        private string _editDescription = string.Empty;

        [ObservableProperty]
        private string _editIcon = string.Empty;

        [ObservableProperty]
        private ObservableCollection<EditAudioItem> _editAudioItems = new();

        [ObservableProperty]
        private Bitmap? _editIconBitmap;

        [ObservableProperty]
        private string _editSize = string.Empty;

        /// <summary>编辑中的资源包总大小（字节）</summary>
        private long _editSizeBytes;

        // 计算属性
        public bool HasAudioItems => EditAudioItems.Count > 0;
        public bool HasNoAudioItems => EditAudioItems.Count == 0;
        public bool HasMultipleAudioItems => EditAudioItems.Count > 1;
        public bool CanDelete => SelectedAudioCount > 0;

        /// <summary>选中音频数量（基于 IsSelected 遍历）</summary>
        public int SelectedAudioCount => EditAudioItems.Count(i => i.IsSelected);

        /// <summary>是否全选</summary>
        public bool IsAudioAllSelected =>
            EditAudioItems.Count > 0 && EditAudioItems.All(i => i.IsSelected);

        /// <summary>当前正在编辑的资源 ID</summary>
        public string ResourceId => _originalResource.Id;

        /// <summary>图标来源标签</summary>
        public string IconSourceLabel { get; private set; } = string.Empty;

        // ===== 音频选择器弹窗状态 =====

        [ObservableProperty]
        private bool _isAudioPickerVisible;

        [ObservableProperty]
        private ObservableCollection<PickerAudioItem> _pickerAudioItems = new();

        [ObservableProperty]
        private string _pickerSearchText = string.Empty;

        [ObservableProperty]
        private int _pickerSelectedCount;

        /// <summary>所有拾取项（未过滤前）</summary>
        private List<PickerAudioItem> _allPickerItems = new();

        /// <summary>音频抽屉确认按钮文本</summary>
        public string ConfirmAudioText => "添加";

        /// <summary>音频抽屉底部状态文本 "已选择 x 个"</summary>
        public string PickerStatusText => PickerSelectedCount > 0
            ? $"已选择 {PickerSelectedCount} 个" : string.Empty;

        partial void OnPickerSearchTextChanged(string value)
        {
            FilterPickerItems(value);
        }

        partial void OnPickerSelectedCountChanged(int value)
        {
            OnPropertyChanged(nameof(PickerStatusText));
        }

        private void FilterPickerItems(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                PickerAudioItems = new ObservableCollection<PickerAudioItem>(_allPickerItems);
            }
            else
            {
                var q = query.Trim().ToLower();
                var filtered = _allPickerItems
                    .Where(i => i.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                             || i.SourceName.Contains(q, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                PickerAudioItems = new ObservableCollection<PickerAudioItem>(filtered);
            }
            PickerSelectedCount = PickerAudioItems.Count(i => i.IsSelected);
        }

        /// <summary>打开音频选择器</summary>
        [RelayCommand]
        private async Task OpenAudioPickerAsync()
        {
            var allResources = await _resourceService.GetLocalResourcesAsync();
            var items = new List<PickerAudioItem>();
            foreach (var r in allResources)
            {
                foreach (var a in r.AudioItems)
                {
                    items.Add(PickerAudioItem.FromAudioItem(a, r.DisplayName));
                }
            }
            _allPickerItems = items;
            PickerSearchText = string.Empty;
            PickerAudioItems = new ObservableCollection<PickerAudioItem>(items);
            PickerSelectedCount = 0;
            IsAudioPickerVisible = true;
        }

        /// <summary>切换拾取器中的选择</summary>
        public void TogglePickerItem(PickerAudioItem item)
        {
            item.IsSelected = !item.IsSelected;
            PickerSelectedCount = PickerAudioItems.Count(i => i.IsSelected);
        }

        /// <summary>确认选择</summary>
        [RelayCommand]
        private void ConfirmPickerSelection()
        {
            var selected = _allPickerItems.Where(i => i.IsSelected).Select(i => i.AudioItem).ToArray();
            IsAudioPickerVisible = false;
            if (selected.Length > 0)
            {
                AddAudioFromInternalConfirm(selected);
            }
        }

        /// <summary>取消选择器</summary>
        [RelayCommand]
        private void CancelPicker()
        {
            IsAudioPickerVisible = false;
        }

        // ===== 图标选择器弹窗状态 =====

        [ObservableProperty]
        private bool _isIconPickerVisible;

        [ObservableProperty]
        private ObservableCollection<PickerIconItem> _pickerIconItems = new();

        [ObservableProperty]
        private int _pickerIconSelectedIndex = -1;

        /// <summary>打开图标选择器（只读取集中图标目录中的图标）</summary>
        [RelayCommand]
        private async Task OpenIconPickerAsync()
        {
            var allResources = await _resourceService.GetLocalResourcesAsync();
            var nameById = allResources.ToDictionary(r => r.Id, r => r.DisplayName);

            var items = new List<PickerIconItem>();
            foreach (var iconPath in ResourceService.GetAllIconFiles())
            {
                // 扁平结构：installed_icons/{id}.{ext}，资源 id 取自文件名（不含扩展名）
                var id = Path.GetFileNameWithoutExtension(iconPath);
                if (string.IsNullOrEmpty(id) || !File.Exists(iconPath)) continue;
                nameById.TryGetValue(id, out var displayName);
                displayName ??= id;

                Bitmap? bitmap = null;
                try { bitmap = new Bitmap(iconPath); } catch { }
                items.Add(new PickerIconItem
                {
                    IconPath = iconPath,
                    IconBitmap = bitmap,
                    SourceName = displayName,
                    DisplayName = displayName
                });
            }
            PickerIconItems = new ObservableCollection<PickerIconItem>(items);
            PickerIconSelectedIndex = -1;
            IsIconPickerVisible = true;
        }

        /// <summary>单选图标项（取消其他选中）</summary>
        public void SelectIconItem(PickerIconItem item)
        {
            var wasSelected = item.IsSelected;

            // 取消所有选中
            foreach (var i in PickerIconItems)
                i.IsSelected = false;

            // 切换当前项（点击已选中则取消）
            item.IsSelected = !wasSelected;
        }

        /// <summary>确认图标选择</summary>
        [RelayCommand]
        private void ConfirmIconPicker()
        {
            var selected = PickerIconItems.FirstOrDefault(i => i.IsSelected);
            if (selected != null)
            {
                PickIconFromInternalConfirm(selected.IconPath);
            }
            IsIconPickerVisible = false;
        }

        /// <summary>取消图标选择</summary>
        [RelayCommand]
        private void CancelIconPicker()
        {
            IsIconPickerVisible = false;
        }

        [RelayCommand]
        private async Task PickIconFromInternalAsync()
        {
            await OpenIconPickerAsync();
        }

        // ===== 音频预览状态 =====

        public PersonalResourceEditViewModel(
            ResourceService resourceService,
            DownloadManager downloadManager,
            AudioPlaybackService audioPlayer,
            Resource resource,
            Action onLeave)
        {
            _resourceService = resourceService;
            _downloadManager = downloadManager;
            _audioPlayer = audioPlayer;
            _originalResource = resource;
            _onLeave = onLeave;

            // 深拷贝到编辑数据
            EditDisplayName = resource.DisplayName;
            EditName = resource.Name;
            EditDescription = resource.Description;
            EditIcon = resource.Icon;
            var initialList = new ObservableCollection<EditAudioItem>(
                resource.AudioItems.Select(a =>
                {
                    var item = EditAudioItem.FromAudioItem(a);
                    item.PropertyChanged += OnAudioItemPropertyChanged;
                    return item;
                }));
            initialList.CollectionChanged += (_, _) => NotifyAudioListChanged();
            EditAudioItems = initialList;
            OnPropertyChanged(nameof(HasAudioItems));
            OnPropertyChanged(nameof(HasNoAudioItems));
            OnPropertyChanged(nameof(HasMultipleAudioItems));

            IconSourceLabel = string.IsNullOrEmpty(resource.Icon) || resource.Icon.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? "网络" : "本地";

            // 加载图标
            _ = LoadIconBitmapAsync(resource.Icon);

            // 初始计算资源包大小
            RecalculateSize();
        }

        // ===== 图标 Bitmap 加载 =====

        private async Task LoadIconBitmapAsync(string iconPath)
        {
            if (string.IsNullOrEmpty(iconPath))
            {
                EditIconBitmap = null;
                return;
            }

            try
            {
                var bitmap = await ImageCache.LoadAsync(iconPath);
                EditIconBitmap = bitmap;
            }
            catch
            {
                EditIconBitmap = null;
            }
        }

        partial void OnEditIconChanged(string value)
        {
            _ = LoadIconBitmapAsync(value);
        }

        // ===== 音频列表变更通知 =====

        private void NotifyAudioListChanged()
        {
            OnPropertyChanged(nameof(HasAudioItems));
            OnPropertyChanged(nameof(HasNoAudioItems));
            OnPropertyChanged(nameof(HasMultipleAudioItems));
            OnPropertyChanged(nameof(IsAudioAllSelected));
            OnPropertyChanged(nameof(SelectedAudioCount));
            OnPropertyChanged(nameof(CanDelete));
            RecalculateSize();
        }

        /// <summary>重新计算资源包总大小（图标 + 所有音频文件）</summary>
        private void RecalculateSize()
        {
            long total = 0;

            // 图标大小
            if (!string.IsNullOrEmpty(EditIcon) && !EditIcon.StartsWith("http", StringComparison.OrdinalIgnoreCase) && File.Exists(EditIcon))
            {
                try { total += new FileInfo(EditIcon).Length; } catch { }
            }

            // 音频文件大小
            foreach (var audio in EditAudioItems)
            {
                if (!string.IsNullOrEmpty(audio.Src) && !audio.Src.StartsWith("http", StringComparison.OrdinalIgnoreCase) && File.Exists(audio.Src))
                {
                    try { total += new FileInfo(audio.Src).Length; } catch { }
                }
            }

            _editSizeBytes = total;
            EditSize = FormatFileSize(total);
        }

        /// <summary>安全获取文件大小，失败返回 0</summary>
        private static long GetFileSizeSafe(string? path)
        {
            if (string.IsNullOrEmpty(path) || path.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return 0;
            try
            {
                return File.Exists(path) ? new FileInfo(path).Length : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes <= 0) return "0 B";
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }

        private void OnAudioItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EditAudioItem.IsSelected))
            {
                OnPropertyChanged(nameof(IsAudioAllSelected));
                OnPropertyChanged(nameof(SelectedAudioCount));
                OnPropertyChanged(nameof(CanDelete));
            }
        }

        // ===== 离开编辑（自动保存） =====

        [RelayCommand]
        private async Task LeaveEditAsync()
        {
            await DoSaveAsync();
            _onLeave();
        }

        /// <summary>内部保存逻辑，离开编辑和切换资源时调用</summary>
        private async Task DoSaveAsync()
        {
            try
            {
                var updated = _originalResource.Clone();
                updated.DisplayName = EditDisplayName;
                updated.Name = EditName;
                updated.Description = EditDescription;
                updated.Icon = EditIcon;
                updated.AudioItems = EditAudioItems.Select((a, i) => new AudioItem
                {
                    Id = a.Id,
                    Name = a.Name,
                    Src = a.Src,
                    DurationMs = a.DurationMs,
                    Size = GetFileSizeSafe(a.Src),
                    OrderIndex = i + 1
                }).ToList();

                updated.Size = _editSizeBytes.ToString();

                await _resourceService.SaveResourceEditAsync(_originalResource.Id, updated);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Edit] 保存失败: {ex.Message}");
            }
        }

        // ===== 音频选择（基于 EditAudioItem.IsSelected） =====

        [RelayCommand]
        private void ToggleSelectAudio(EditAudioItem? item)
        {
            if (item == null) return;
            item.IsSelected = !item.IsSelected;
        }

        [RelayCommand]
        private void ToggleSelectAllAudio()
        {
            var allSelected = IsAudioAllSelected;
            foreach (var item in EditAudioItems)
                item.IsSelected = !allSelected;
            OnPropertyChanged(nameof(IsAudioAllSelected));
            OnPropertyChanged(nameof(SelectedAudioCount));
            OnPropertyChanged(nameof(CanDelete));
        }

        // ===== 添加音频 =====

        [RelayCommand]
        private async Task AddAudioFromFileAsync()
        {
            var topLevel = TopLevel.GetTopLevel(
                Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择音频文件",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("音频文件")
                    {
                        Patterns = new[] { "*.mp3", "*.wav", "*.ogg", "*.aac", "*.flac", "*.wma" }
                    }
                }
            });

            foreach (var file in files)
            {
                var srcPath = file.Path.LocalPath;
                var name = Path.GetFileNameWithoutExtension(srcPath);
                var audioId = $"audio_{DateTime.Now.Ticks}_{Guid.NewGuid():N}";

                var audioDir = Path.Combine(ResourceService.GetResourceInstallDir(_originalResource.Id), "audio");
                Directory.CreateDirectory(audioDir);
                var destName = $"{audioId}{Path.GetExtension(srcPath)}";
                var destPath = Path.Combine(audioDir, destName);

                try
                {
                    File.Copy(srcPath, destPath, overwrite: true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Edit] 复制音频失败: {ex.Message}");
                    continue;
                }

                var durationMs = 0L;
                try
                {
                    var ext = Path.GetExtension(destPath)?.ToLowerInvariant() ?? "";
                    using WaveStream reader = ext switch
                    {
                        ".wav" => new WaveFileReader(destPath),
                        ".mp3" => new Mp3FileReader(destPath),
                        ".aiff" or ".aif" => new AiffFileReader(destPath),
                        _ => new MediaFoundationReader(destPath),
                    };
                    durationMs = (long)reader.TotalTime.TotalMilliseconds;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Edit] 读取音频时长失败: {destPath}, {ex.Message}");
                }

                var item = new EditAudioItem
                {
                    Id = audioId,
                    Name = name,
                    Src = destPath,
                    DurationMs = durationMs,
                    OrderIndex = EditAudioItems.Count + 1
                };
                item.PropertyChanged += OnAudioItemPropertyChanged;
                EditAudioItems.Add(item);
            }

            // 处理完成后关闭音频选择抽屉（确保在 UI 线程执行）
            Dispatcher.UIThread.Post(() => IsAudioPickerVisible = false);
        }

        [RelayCommand]
        private async Task AddAudioFromInternalAsync()
        {
            await OpenAudioPickerAsync();
        }

        /// <summary>
        /// 从应用内选择确认回调（由 AudioPickerPopup 调用）
        /// </summary>
        public void AddAudioFromInternalConfirm(AudioItem[] selectedItems)
        {
            foreach (var audio in selectedItems)
            {
                var srcPath = audio.Src;
                var name = audio.Name;
                var audioId = $"audio_{DateTime.Now.Ticks}_{Guid.NewGuid():N}";

                var audioDir = Path.Combine(ResourceService.GetResourceInstallDir(_originalResource.Id), "audio");
                Directory.CreateDirectory(audioDir);
                var ext = Path.GetExtension(srcPath);
                var destName = $"{audioId}{ext}";
                var destPath = Path.Combine(audioDir, destName);

                try
                {
                    File.Copy(srcPath, destPath, overwrite: true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Edit] 复制音频(应用内)失败: {ex.Message}");
                    destPath = srcPath;
                }

                var item = new EditAudioItem
                {
                    Id = audioId,
                    Name = name,
                    Src = destPath,
                    DurationMs = audio.DurationMs,
                    OrderIndex = EditAudioItems.Count + 1
                };
                item.PropertyChanged += OnAudioItemPropertyChanged;
                EditAudioItems.Add(item);
            }
        }

        // ===== 删除音频 =====

        [RelayCommand]
        private async Task DeleteSelectedAudiosAsync()
        {
            var selected = EditAudioItems.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0) return;

            foreach (var item in selected)
            {
                if (!string.IsNullOrEmpty(item.Src) && !item.Src.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(item.Src); }
                    catch { /* 忽略 */ }
                }
                EditAudioItems.Remove(item);
            }
            OnPropertyChanged(nameof(IsAudioAllSelected));
            OnPropertyChanged(nameof(SelectedAudioCount));
            OnPropertyChanged(nameof(CanDelete));
            await Task.CompletedTask;
        }

        // ===== 图标更换 =====

        [RelayCommand]
        private async Task PickIconFromFileAsync()
        {
            var topLevel = TopLevel.GetTopLevel(
                Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择图标图片",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("图片文件")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif" }
                    }
                }
            });

            if (files.Count > 0)
            {
                var srcPath = files[0].Path.LocalPath;
                // 先缩放到临时文件，再保存到集中图标目录（文件名 = 资源 id）
                var tmpPath = Path.Combine(Path.GetTempPath(), $"sounder_icon_{Guid.NewGuid():N}.jpg");

                try
                {
                    // 缩放为 150x150 并保存为 jpg
                    ResizeToJpg(srcPath, tmpPath, 150, 150);

                    // 保存到资源自身文件夹 installed/{id}/icon.{ext}（资源列表用），不污染公共 installed_icons
                    var destPath = ResourceService.SaveIconToResourceFolder(_originalResource.Id, tmpPath);
                    EditIcon = !string.IsNullOrEmpty(destPath) ? destPath : tmpPath;
                    // 强制刷新预览：同一路径第二次写入会被 ImageCache 命中旧图，先失效缓存
                    ImageCache.Invalidate(EditIcon);
                    _ = LoadIconBitmapAsync(EditIcon);
                    IconSourceLabel = "本地";
                    IsIconPickerVisible = false;
                    RecalculateSize();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Edit] 处理图标失败: {ex.Message}");
                }
                finally
                {
                    if (File.Exists(tmpPath))
                    {
                        try { File.Delete(tmpPath); } catch { /* 忽略 */ }
                    }
                }
            }
        }

        private static void ResizeToJpg(string srcPath, string destPath, int width, int height)
        {
            using var input = SKBitmap.Decode(srcPath);
            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;
            canvas.DrawBitmap(input, new SKRect(0, 0, width, height));
            canvas.Flush();
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);
            using var stream = File.OpenWrite(destPath);
            data.SaveTo(stream);
        }

        public void PickIconFromInternalConfirm(string srcPath)
        {
            try
            {
                // 仅保存到资源自身文件夹 installed/{id}/icon.{ext}，不污染公共 installed_icons
                var destPath = ResourceService.SaveIconToResourceFolder(_originalResource.Id, srcPath);
                EditIcon = !string.IsNullOrEmpty(destPath) ? destPath : srcPath;
                // 强制刷新预览：同一路径第二次写入会被 ImageCache 命中旧图，先失效缓存
                ImageCache.Invalidate(EditIcon);
                _ = LoadIconBitmapAsync(EditIcon);
                IconSourceLabel = "本地";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Edit] 复制图标(应用内)失败: {ex.Message}");
                EditIcon = srcPath;
            }
            RecalculateSize();
        }

        // ===== 拖拽排序 =====

        [RelayCommand]
        private void MoveAudio(SortableUpdateEventArgs e)
        {
            if (e.ApplyUpdateMutation())
            {
                OnPropertyChanged(nameof(IsAudioAllSelected));
            }
        }

        // ===== 音频预览 =====

        /// <summary>
        /// 点击试听按钮：播放 / 停止
        /// </summary>
        public async void TogglePreview(EditAudioItem item)
        {
            if (string.IsNullOrEmpty(item.Src)) return;

            // 如果正在播放 → 停止
            if (item.IsPlaying)
            {
                _audioPlayer.StopAudio(item.Id);
                item.IsPlaying = false;
                return;
            }

            // 停止当前正在播放的其他项
            foreach (var other in EditAudioItems)
            {
                if (other != item && other.IsPlaying)
                {
                    _audioPlayer.StopAudio(other.Id);
                    other.IsPlaying = false;
                }
            }

            // 开始播放
            item.IsPlaying = true;
            var instanceId = await _audioPlayer.PlayAsync(item.Src, item.Id, Models.PlayMode.Replace);

            // 播放结束或失败 → 恢复状态
            if (string.IsNullOrEmpty(instanceId))
            {
                item.IsPlaying = false;
            }
            else
            {
                // 监听播放停止事件，一次性的
                void OnStopped(string id)
                {
                    if (id == instanceId || id.StartsWith(item.Id))
                    {
                        item.IsPlaying = false;
                        _audioPlayer.PlaybackStopped -= OnStopped;
                    }
                }
                _audioPlayer.PlaybackStopped += OnStopped;
            }
        }
    }
}
