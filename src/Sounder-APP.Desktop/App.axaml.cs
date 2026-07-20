using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Sounder_APP.Services;
using Sounder_APP.ViewModels;
using Sounder_APP.Views;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Sounder_APP
{
    public partial class App : Application
    {
        private TrayIcon? _trayIcon;
        private IClassicDesktopStyleApplicationLifetime? _desktop;
        private bool _isHeadlessMode;

        /// <summary>后台播放管理器（全局单例）</summary>
        public static BackgroundPlaybackManager BackgroundPlayback { get; private set; } = null!;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // 预加载 NAudio 核心类型（防止裁剪后运行时异常）
            AudioPlaybackService.Warmup();

            // 初始化全局后台播放管理器
            BackgroundPlayback = new BackgroundPlaybackManager();

            // 从持久化存储恢复主题和语言
            var settings = new AppSettings();
            RequestedThemeVariant = settings.ThemeMode switch
            {
                "Light" => ThemeVariant.Light,
                "Dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };

            // 恢复语言设置
            LocalizationService.Instance.CurrentLanguage = settings.Language;

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                _desktop = desktop;

                // 关闭窗口不退出应用（通过托盘菜单退出）
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };

                // 检查启动参数是否包含 sounder:// URL
                var sounderUrl = FindSounderUrl(desktop.Args ?? Array.Empty<string>());
                if (sounderUrl != null)
                {
                    _isHeadlessMode = true;

                    // 所有后台任务完成后自动退出
                    BackgroundPlayback.AllTasksCompleted += () =>
                    {
                        if (!_isHeadlessMode) return;
                        _isHeadlessMode = false;
                        Dispatcher.UIThread.Post(() =>
                        {
                            _trayIcon?.Dispose();
                            _trayIcon = null;
                            SingleInstanceService.Dispose();
                            _desktop?.Shutdown();
                        });
                    };

                    EventHandler? openedHandler = null;
                    openedHandler = (_, _) =>
                    {
                        desktop.MainWindow.Opened -= openedHandler; // 仅首次触发
                        desktop.MainWindow.Hide();
                        _ = HandleSounderUrlAsync(sounderUrl);
                    };
                    desktop.MainWindow.Opened += openedHandler;
                }

                // 创建系统托盘图标
                CreateTrayIcon(desktop);

                // 退出时清理
                desktop.Exit += (_, _) =>
                {
                    _trayIcon?.Dispose();
                    BackgroundPlayback.StopAll();
                    SingleInstanceService.Dispose();
                };
            }

            base.OnFrameworkInitializationCompleted();

            // 启动 IPC 监听（在 UI 线程初始化完成后）
            SingleInstanceService.MessageReceived += OnIpcMessageReceived;
            SingleInstanceService.StartIpcListener();
        }

        /// <summary>创建系统托盘图标及菜单</summary>
        private void CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                var icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Sounder-APP/Assets/Sounder-APP.ico")));

                void ShowWindow()
                {
                    if (desktop.MainWindow != null)
                    {
                        desktop.MainWindow.Show();
                        desktop.MainWindow.WindowState = WindowState.Normal;
                        desktop.MainWindow.Activate();
                    }
                }

                var trayMenu = new NativeMenu();

                var showItem = new NativeMenuItem(LocalizationService.Instance["show_window"]);
                showItem.Click += (_, _) => ShowWindow();
                trayMenu.Add(showItem);
                
                var exitItem = new NativeMenuItem(LocalizationService.Instance["exit"]);
                exitItem.Click += (_, _) =>
                {
                    _trayIcon?.Dispose();
                    _trayIcon = null;
                    SingleInstanceService.StopIpcListener();
                    BackgroundPlayback.StopAll();

                    if (desktop.MainWindow?.DataContext is MainWindowViewModel vm)
                        vm.StopAllAudio();

                    desktop.Shutdown();
                };
                trayMenu.Add(exitItem);

                _trayIcon = new TrayIcon
                {
                    Icon = icon,
                    ToolTipText = LocalizationService.Instance["tooltip_title"],
                    Menu = trayMenu,
                    IsVisible = true
                };
                _trayIcon.Clicked += (_, _) => ShowWindow();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] 创建托盘图标失败: {ex.Message}");
            }
        }

        /// <summary>从参数列表中提取 sounder:// URL</summary>
        private static string? FindSounderUrl(string[] args)
        {
            foreach (var arg in args)
            {
                if (arg.StartsWith("sounder://", StringComparison.OrdinalIgnoreCase))
                    return arg;
                if (arg.Contains("sounder://", StringComparison.OrdinalIgnoreCase))
                {
                    // 可能被引号包裹或与其他内容混合
                    var idx = arg.IndexOf("sounder://", StringComparison.OrdinalIgnoreCase);
                    var end = arg.IndexOf('"', idx);
                    return end > idx ? arg[idx..end] : arg[idx..];
                }
            }
            return null;
        }

        /// <summary>处理 sounder:// URL（后台播放）</summary>
        private async Task HandleSounderUrlAsync(string url)
        {
            var resourceId = UrlProtocolService.ParsePlayUrl(url);
            if (resourceId == null) return;

            try
            {
                await BackgroundPlayback.PlayResourceAsync(resourceId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] PlayResourceAsync 异常: {ex.Message}");
            }
        }

        /// <summary>IPC 消息回调：主实例收到新参数时处理</summary>
        private void OnIpcMessageReceived(string message)
        {
            if (message == SingleInstanceService.ShowWindowMessage)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_desktop?.MainWindow != null)
                    {
                        _desktop.MainWindow.Show();
                        _desktop.MainWindow.WindowState = WindowState.Normal;
                        _desktop.MainWindow.Activate();
                    }
                });
                return;
            }

            Dispatcher.UIThread.Post(() => _ = HandleSounderUrlAsync(message));
        }
    }
}
