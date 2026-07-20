using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Sounder_APP.Services;

namespace Sounder_APP.Models
{
    public enum ResourceSource
    {
        Cloud,
        Local
    }

    /// <summary>
    /// 领域模型 - 资源包
    /// </summary>
    public class Resource : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string name = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        private string _icon = string.Empty;
        public string Icon
        {
            get => _icon;
            set
            {
                if (_icon == value) return;
                _icon = value;
                _iconBitmap = null;
                IconLoadFailed = false;
                LoadIconAsync();
            }
        }

        private Bitmap? _iconBitmap;
        /// <summary>从 Icon URL 加载的 Bitmap，异步下载完成后通知 UI 更新</summary>
        public Bitmap? IconBitmap => _iconBitmap;

        private bool _iconLoadFailed;
        /// <summary>图标加载是否失败（用于显示失败图标）</summary>
        public bool IconLoadFailed
        {
            get => _iconLoadFailed;
            private set
            {
                if (_iconLoadFailed == value) return;
                _iconLoadFailed = value;
                Notify(nameof(IconLoadFailed));
            }
        }

        /// <summary>API 返回的原始尺寸字符串，如 "24.40KB"</summary>
        public string Size { get; set; } = "0";

        public string PublishDate { get; set; } = string.Empty;
        public int AudioCount => AudioItems.Count;
        private bool _isInstalled;
        public bool IsInstalled
        {
            get => _isInstalled;
            set
            {
                if (_isInstalled == value) return;
                _isInstalled = value;
                Notify();
            }
        }
        public ResourceSource Source { get; set; }
        public List<AudioItem> AudioItems { get; set; } = new();

        // ===== 每资源独立下载状态（支持多资源同时安装） =====

        private float _downloadProgress;
        public float DownloadProgress
        {
            get => _downloadProgress;
            set { if (_downloadProgress == value) return; _downloadProgress = value; Notify(); }
        }

        private string _downloadStatusText = string.Empty;
        public string DownloadStatusText
        {
            get => _downloadStatusText;
            set { if (_downloadStatusText == value) return; _downloadStatusText = value; Notify(); }
        }

        private bool _isDownloadActive;
        /// <summary>资源是否正在下载中（用于进度条显示切换）</summary>
        public bool IsDownloadActive
        {
            get => _isDownloadActive;
            set
            {
                if (_isDownloadActive == value) return;
                _isDownloadActive = value;
                Notify(nameof(IsDownloadActive));
                Notify(nameof(ActiveInstallingId));
            }
        }

        private bool _isDownloadPaused;
        /// <summary>资源下载是否已暂停（按钮显示"继续"）</summary>
        public bool IsDownloadPaused
        {
            get => _isDownloadPaused;
            set
            {
                if (_isDownloadPaused == value) return;
                _isDownloadPaused = value;
                Notify();
            }
        }

        /// <summary>
        /// 下载中时返回资源 Id，空闲时返回空字符串。
        /// 供按钮的 InstallingResourceId vs ResourceId 比较逻辑使用。
        /// </summary>
        public string ActiveInstallingId => IsDownloadActive ? Id : string.Empty;

        public string SizeFormatted
        {
            get
            {
                if (string.IsNullOrEmpty(Size)) return "0 B";
                // API 已返回带单位的格式化字符串，直接使用
                if (Size.Any(c => !char.IsDigit(c) && c != '.'))
                    return Size;
                // 纯数字字符串则解释为字节
                if (long.TryParse(Size, out var bytes))
                {
                    if (bytes < 1024) return $"{bytes} B";
                    if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
                    if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
                    return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
                }
                return Size;
            }
        }

        public string AudioCountDisplay => $"{AudioCount} 个";

        public string AudioCountListDisplay => $"{AudioCount} 个音频";

        public string PublishDateDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(PublishDate)) return string.Empty;
                if (DateTime.TryParse(PublishDate, out var dt))
                    return dt.ToString("yyyy-MM-dd");
                return PublishDate;
            }
        }

        /// <summary>
        /// 异步加载图标（后台线程下载，完成后在主线程通过 INPC 通知 UI）
        /// </summary>
        public async void LoadIconAsync()
        {
            if (string.IsNullOrEmpty(_icon)) return;
            var bitmap = await ImageCache.LoadAsync(_icon);
            Dispatcher.UIThread.Post(() =>
            {
                if (bitmap != null)
                {
                    _iconBitmap = bitmap;
                    Notify(nameof(IconBitmap));
                    IconLoadFailed = false;
                }
                else
                {
                    IconLoadFailed = true;
                }
            });
        }

        /// <summary>
        /// 保留另一个资源已加载好的图标，避免切换时重复加载和闪烁
        /// </summary>
        public void PreserveIcon(Bitmap bitmap)
        {
            _iconBitmap = bitmap;
            Notify(nameof(IconBitmap));
        }

        /// <summary>
        /// 设置图标路径但不触发 LoadIconAsync（安装完成后调用，图标已在本地）
        /// </summary>
        public void SetIconDirect(string localPath)
        {
            if (_icon == localPath) return;
            _icon = localPath;
            _iconBitmap = null;
            IconLoadFailed = false;
            LoadIconAsync(); // 从本地文件加载
        }

        /// <summary>
        /// 深拷贝当前资源（用于编辑模式）
        /// </summary>
        public Resource Clone()
        {
            return new Resource
            {
                Id = Id,
                Name = Name,
                DisplayName = DisplayName,
                Description = Description,
                Icon = Icon,
                Size = Size,
                PublishDate = PublishDate,
                Source = Source,
                IsInstalled = IsInstalled,
                AudioItems = AudioItems.Select(a => new AudioItem
                {
                    Id = a.Id,
                    Name = a.Name,
                    Src = a.Src,
                    DurationMs = a.DurationMs,
                    OrderIndex = a.OrderIndex,
                    Size = a.Size
                }).ToList()
            };
        }

        /// <summary>
        /// 从 API 数据创建领域模型
        /// </summary>
        public static Resource FromRemote(RemoteResource remote)
        {
            return new Resource
            {
                Id = remote.Id,
                Name = remote.Name,
                DisplayName = remote.DisplayName,
                Description = remote.Description,
                Icon = remote.Icon,
                Size = remote.Size,
                PublishDate = remote.PublishDate,
                Source = ResourceSource.Cloud,
                AudioItems = remote.AudioList.Select((a, i) => new AudioItem
                {
                    Id = string.IsNullOrEmpty(a.Id) ? $"{remote.Id}_{i}" : a.Id,
                    Name = a.Name,
                    Src = a.Src,
                    DurationMs = a.Duration,
                    OrderIndex = i + 1
                }).ToList()
            };
        }
    }

    /// <summary>
    /// 领域模型 - 音频项
    /// </summary>
    public class AudioItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Src { get; set; } = string.Empty;
        public long DurationMs { get; set; } // 毫秒
        public int OrderIndex { get; set; }

        public string DurationFormatted
        {
            get
            {
                var seconds = DurationMs / 1000.0;
                if (seconds == Math.Floor(seconds))
                    return $"{(int)seconds}s";
                return $"{seconds:F1}s";
            }
        }

        public long Size { get; set; }

        public string SizeFormatted
        {
            get
            {
                if (Size < 1024) return $"{Size} B";
                if (Size < 1024 * 1024) return $"{Size / 1024.0:F1} KB";
                return $"{Size / (1024.0 * 1024):F1} MB";
            }
        }
    }

    /// <summary>
    /// 拾取器音频项（用于从应用内选择音频弹窗，包含来源资源名称）
    /// </summary>
    public partial class PickerAudioItem : ObservableObject
    {
        public AudioItem AudioItem { get; set; } = new();
        public string SourceName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DurationFormatted { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isSelected;

        /// <summary>
        /// 从 AudioItem 和来源资源名称创建拾取器项
        /// </summary>
        public static PickerAudioItem FromAudioItem(AudioItem item, string sourceName)
        {
            return new PickerAudioItem
            {
                AudioItem = item,
                SourceName = sourceName,
                Name = item.Name,
                DurationFormatted = item.DurationFormatted
            };
        }
    }

    /// <summary>
    /// 拾取器图标项（用于从应用内选择图标弹窗）
    /// </summary>
    public partial class PickerIconItem : ObservableObject
    {
        public string IconPath { get; set; } = string.Empty;
        public Bitmap? IconBitmap { get; set; }
        public string SourceName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isSelected;
    }

    /// <summary>
    /// 可观察的编辑用音频项模型
    /// </summary>
    public partial class EditAudioItem : ObservableObject
    {
        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _src = string.Empty;

        [ObservableProperty]
        private long _durationMs;

        [ObservableProperty]
        private int _orderIndex;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isPlaying;

        /// <summary>
        /// 格式化时长: 以秒为单位, 整数显示"6s", 小数保留一位显示"0.4s"、"2.5s"
        /// </summary>
        public string DurationFormatted
        {
            get
            {
                var seconds = DurationMs / 1000.0;
                if (seconds == Math.Floor(seconds))
                    return $"{(int)seconds}s";
                return $"{seconds:F1}s";
            }
        }

        /// <summary>
        /// 从 AudioItem 创建编辑模型
        /// </summary>
        public static EditAudioItem FromAudioItem(AudioItem item)
        {
            return new EditAudioItem
            {
                Id = item.Id,
                Name = item.Name,
                Src = item.Src,
                DurationMs = item.DurationMs,
                OrderIndex = item.OrderIndex
            };
        }

        /// <summary>
        /// 转回 AudioItem
        /// </summary>
        public AudioItem ToAudioItem()
        {
            return new AudioItem
            {
                Id = Id,
                Name = Name,
                Src = Src,
                DurationMs = DurationMs,
                OrderIndex = OrderIndex
            };
        }
    }
}
