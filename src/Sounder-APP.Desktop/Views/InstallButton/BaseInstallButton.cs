using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace Sounder_APP.Views.InstallButton
{
    /// <summary>
    /// 安装按钮基类，提供通用属性、暂停/继续支持和进度动画。
    /// 具体布局由 <see cref="CompactInstallButton"/>（紧凑小按钮）
    /// 和 <see cref="WideInstallButton"/>（宽屏大按钮）分别实现。
    /// </summary>
    public class BaseInstallButton : UserControl
    {
        /// <summary>安装命令（绑定到 ViewModel.InstallResourceCommand）</summary>
        public static readonly StyledProperty<ICommand?> CommandProperty =
            AvaloniaProperty.Register<BaseInstallButton, ICommand?>(nameof(Command));

        /// <summary>命令参数</summary>
        public static readonly StyledProperty<object?> CommandParameterProperty =
            AvaloniaProperty.Register<BaseInstallButton, object?>(nameof(CommandParameter));

        /// <summary>资源是否已安装（控制按钮可见性，由父级结合 IsInstalled 决定）</summary>
        public static readonly StyledProperty<bool> IsInstalledProperty =
            AvaloniaProperty.Register<BaseInstallButton, bool>(nameof(IsInstalled));

        /// <summary>当前正在安装的资源 ID（来自 ViewModel.InstallingResourceId）</summary>
        public static readonly StyledProperty<string> InstallingResourceIdProperty =
            AvaloniaProperty.Register<BaseInstallButton, string>(nameof(InstallingResourceId), string.Empty);

        /// <summary>此按钮对应的资源 ID</summary>
        public static readonly StyledProperty<string?> ResourceIdProperty =
            AvaloniaProperty.Register<BaseInstallButton, string?>(nameof(ResourceId));

        /// <summary>安装进度 0~1</summary>
        public static readonly StyledProperty<float> InstallProgressProperty =
            AvaloniaProperty.Register<BaseInstallButton, float>(nameof(InstallProgress));

        /// <summary>安装状态文本（如"下载中"、"已暂停"、"失败"等）</summary>
        public static readonly StyledProperty<string> InstallStatusTextProperty =
            AvaloniaProperty.Register<BaseInstallButton, string>(nameof(InstallStatusText), string.Empty);

        /// <summary>是否已暂停</summary>
        public static readonly StyledProperty<bool> IsPausedProperty =
            AvaloniaProperty.Register<BaseInstallButton, bool>(nameof(IsPaused));

        /// <summary>暂停/继续命令（绑定到 ViewModel.PauseResumeCommand）</summary>
        public static readonly StyledProperty<ICommand?> PauseResumeCommandProperty =
            AvaloniaProperty.Register<BaseInstallButton, ICommand?>(nameof(PauseResumeCommand));

        public ICommand? Command
        {
            get => GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public object? CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        public bool IsInstalled
        {
            get => GetValue(IsInstalledProperty);
            set => SetValue(IsInstalledProperty, value);
        }

        public string InstallingResourceId
        {
            get => GetValue(InstallingResourceIdProperty);
            set => SetValue(InstallingResourceIdProperty, value);
        }

        public string? ResourceId
        {
            get => GetValue(ResourceIdProperty);
            set => SetValue(ResourceIdProperty, value);
        }

        public float InstallProgress
        {
            get => GetValue(InstallProgressProperty);
            set => SetValue(InstallProgressProperty, value);
        }

        public string InstallStatusText
        {
            get => GetValue(InstallStatusTextProperty);
            set => SetValue(InstallStatusTextProperty, value);
        }

        public bool IsPaused
        {
            get => GetValue(IsPausedProperty);
            set => SetValue(IsPausedProperty, value);
        }

        public ICommand? PauseResumeCommand
        {
            get => GetValue(PauseResumeCommandProperty);
            set => SetValue(PauseResumeCommandProperty, value);
        }
    }
}
