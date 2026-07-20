using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Sounder_APP.ViewModels
{
    public partial class SubmissionFormViewModel : ViewModelBase
    {
        // ===== 表单字段 =====

        [ObservableProperty]
        private string _resourceName = string.Empty;

        [ObservableProperty]
        private string _appName = string.Empty;

        [ObservableProperty]
        private string _brief = string.Empty;

        [ObservableProperty]
        private string _platform = string.Empty;

        [ObservableProperty]
        private string _platformId = string.Empty;

        [ObservableProperty]
        private string _nickname = string.Empty;

        [ObservableProperty]
        private string _imageSource = string.Empty;

        [ObservableProperty]
        private string _imageSourceLink = string.Empty;

        [ObservableProperty]
        private string _voiceSource = string.Empty;

        [ObservableProperty]
        private string _voiceSourceLink = string.Empty;

        [ObservableProperty]
        private string _fileLink = string.Empty;

        // ===== 验证错误 =====

        [ObservableProperty]
        private string? _resourceNameError;

        [ObservableProperty]
        private string? _appNameError;

        [ObservableProperty]
        private string? _briefError;

        [ObservableProperty]
        private string? _platformError;

        [ObservableProperty]
        private string? _nicknameError;

        [ObservableProperty]
        private string? _imageSourceError;

        [ObservableProperty]
        private string? _voiceSourceError;

        // ===== 选项列表 =====

        public static List<string> PlatformOptions { get; } = new() { "哔哩", "抖音", "快手", "粉丝群" };

        public static List<string> ImageSourceOptions { get; } = new() { "游戏截图", "动漫截图", "互联网", "视频截图" };

        public static List<string> VoiceSourceOptions { get; } = new() { "游戏录制", "视频链接", "互联网" };

        // ===== 条件显示 =====

        public bool ShowImageSourceLink => !string.IsNullOrEmpty(ImageSource);
        public bool ShowVoiceSourceLink => !string.IsNullOrEmpty(VoiceSource);

        // ===== 事件 =====

        public event EventHandler? Submitted;
        public event EventHandler? Cancelled;

        public SubmissionFormViewModel()
        {
        }

        /// <summary>
        /// 重置表单到初始状态
        /// </summary>
        public void Reset()
        {
            ResourceName = string.Empty;
            AppName = string.Empty;
            Brief = string.Empty;
            Platform = string.Empty;
            PlatformId = string.Empty;
            Nickname = string.Empty;
            ImageSource = string.Empty;
            ImageSourceLink = string.Empty;
            VoiceSource = string.Empty;
            VoiceSourceLink = string.Empty;
            FileLink = string.Empty;
            ClearErrors();
        }

        /// <summary>
        /// 清除所有验证错误
        /// </summary>
        private void ClearErrors()
        {
            ResourceNameError = null;
            AppNameError = null;
            BriefError = null;
            PlatformError = null;
            NicknameError = null;
            ImageSourceError = null;
            VoiceSourceError = null;
        }

        // ===== 属性变更通知 =====

        partial void OnImageSourceChanged(string value)
        {
            OnPropertyChanged(nameof(ShowImageSourceLink));
            if (string.IsNullOrEmpty(value))
                ImageSourceLink = string.Empty;
        }

        partial void OnVoiceSourceChanged(string value)
        {
            OnPropertyChanged(nameof(ShowVoiceSourceLink));
            if (string.IsNullOrEmpty(value))
                VoiceSourceLink = string.Empty;
        }

        // ===== 平台选择 =====

        [RelayCommand]
        private void SelectPlatform(string platform)
        {
            Platform = platform;
            PlatformError = null;
        }

        // ===== 图片来源选择 =====

        [RelayCommand]
        private void SelectImageSource(string source)
        {
            ImageSource = source;
            ImageSourceError = null;
        }

        // ===== 声音来源选择 =====

        [RelayCommand]
        private void SelectVoiceSource(string source)
        {
            VoiceSource = source;
            VoiceSourceError = null;
        }

        // ===== 表单验证 =====

        private bool Validate()
        {
            ClearErrors();
            bool valid = true;

            if (string.IsNullOrWhiteSpace(ResourceName))
            {
                ResourceNameError = "资源包全称不能为空";
                valid = false;
            }
            else if (ResourceName.Length > 10)
            {
                ResourceNameError = "资源包全称不能超过10个字符";
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(AppName))
            {
                AppNameError = "APP简称不能为空";
                valid = false;
            }
            else if (AppName.Length > 10)
            {
                AppNameError = "APP简称不能超过10个字符";
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(Brief))
            {
                BriefError = "简述不能为空";
                valid = false;
            }
            else if (Brief.Length > 50)
            {
                BriefError = "简述不能超过50个字符";
                valid = false;
            }

            if (string.IsNullOrEmpty(Platform))
            {
                PlatformError = "请选择平台";
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(Nickname))
            {
                NicknameError = "昵称不能为空";
                valid = false;
            }
            else if (Nickname.Length > 50)
            {
                NicknameError = "昵称不能超过50个字符";
                valid = false;
            }

            if (string.IsNullOrEmpty(ImageSource))
            {
                ImageSourceError = "请选择图片来源";
                valid = false;
            }

            if (string.IsNullOrEmpty(VoiceSource))
            {
                VoiceSourceError = "请选择声音来源";
                valid = false;
            }

            // 如果没有填写备注，则图片和声音来源链接为必填
            if (string.IsNullOrWhiteSpace(FileLink))
            {
                if (string.IsNullOrWhiteSpace(ImageSourceLink))
                {
                    ImageSourceError = "请填写图片来源链接";
                    valid = false;
                }
                if (string.IsNullOrWhiteSpace(VoiceSourceLink))
                {
                    VoiceSourceError = "请填写声音来源链接";
                    valid = false;
                }
            }

            return valid;
        }

        // ===== 提交 / 取消 =====

        [RelayCommand]
        private void Submit()
        {
            if (!Validate()) return;
            Submitted?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void Cancel()
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
        }
    }
}
