using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Sounder_APP.Services;

namespace Sounder_APP.Views
{
    /// <summary>
    /// 脏数据确认弹窗 — 编辑模式下内容有修改时，取消或切换前弹出询问
    /// </summary>
    public enum DirtyConfirmAction
    {
        SaveAndProceed,    // 保存并继续
        DiscardAndProceed, // 放弃并继续
        Cancel             // 取消操作
    }

    public class DirtyConfirmWindow : Window
    {
        private static readonly BoxShadows DialogShadow = new(new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 8,
            Blur = 24,
            Spread = 0,
            Color = Color.FromArgb(38, 0, 0, 0),
        });

        /// <summary>设置入场动画初始状态（Content 赋值前调用）</summary>
        private static void SetupEntranceState(Border card)
        {
            var scaleTransform = new ScaleTransform(0.85, 0.85);
            card.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            card.RenderTransform = scaleTransform;
            card.Opacity = 0;
        }

        /// <summary>为弹窗卡片添加弹性入场动画</summary>
        private static void ApplyEntranceAnimation(Window window, Border card)
        {
            window.Opened += (_, _) =>
            {
                var scale = (ScaleTransform)card.RenderTransform!;
                _ = AnimateEntranceAsync(card, scale);
            };
        }

        private static async Task AnimateEntranceAsync(Border card, ScaleTransform scale)
        {
            card.Opacity = 1;
            // 弹性缩放：400ms，使用 spring 公式
            var scaleSteps = 20;
            for (int i = 1; i <= scaleSteps; i++)
            {
                var t = (double)i / scaleSteps;
                var s = SpringValue(0.85, 1.0, t, 8, 0.75);
                scale.ScaleX = s;
                scale.ScaleY = s;
                await Task.Delay(20);
            }

            scale.ScaleX = 1;
            scale.ScaleY = 1;
        }

        private static double SpringValue(double from, double to, double t, double stiffness, double damping)
        {
            var x = Math.Exp(-damping * t * stiffness);
            var y = Math.Cos(stiffness * Math.Sqrt(1 - damping * damping) * t * Math.PI);
            return to - (to - from) * x * y;
        }

        private DirtyConfirmAction _result = DirtyConfirmAction.Cancel;

        private DirtyConfirmWindow(string title, string message, string primaryText, string dangerText)
        {
            Width = 400;
            Height = 250;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;
            WindowDecorations = Avalonia.Controls.WindowDecorations.None;
            Background = Brushes.Transparent;

            var cardBg = TryGetThemeBrush("Lv3Bg", SolidColorBrush.Parse("#FFFFFF"));
            var textPrimary = TryGetThemeBrush("TextPrimary", SolidColorBrush.Parse("#E6E6E6"));
            var textSecondary = TryGetThemeBrush("TextSecondary", SolidColorBrush.Parse("#8E8E8E"));

            // 卡片容器 — 可拖拽
            var card = new Border
            {
                Width = 360,
                CornerRadius = new CornerRadius(12),
                Background = cardBg,
                BoxShadow = DialogShadow,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            card.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    BeginMoveDrag(e);
            };

            var cardGrid = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
                Margin = new Thickness(24, 20, 24, 20),
                RowSpacing = 12,
            };

            // 标题
            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeight.SemiBold,
                Foreground = textPrimary,
            };
            Grid.SetRow(titleBlock, 0);

            // 消息
            var msgBlock = new TextBlock
            {
                Text = message,
                FontSize = 13,
                Foreground = textSecondary,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(msgBlock, 1);

            // 按钮行
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
            };
            Grid.SetRow(btnPanel, 2);

            // 取消按钮（outline）
            var cancelBtn = new Button
            {
                Content = LocalizationService.Instance.Get("cancel"),
                Padding = new Thickness(14, 6),
                FontSize = 13,
                MinWidth = 60,
            };
            cancelBtn.Classes.AddRange(new[] { "btn", "secondary" });
            cancelBtn.Click += (_, _) => { _result = DirtyConfirmAction.Cancel; Close(); };

            // 放弃按钮（danger 填充）
            var discardBtn = new Button
            {
                Content = dangerText,
                Padding = new Thickness(14, 6),
                FontSize = 13,
                MinWidth = 60,
            };
            discardBtn.Classes.AddRange(new[] { "btn", "danger" });
            discardBtn.Click += (_, _) => { _result = DirtyConfirmAction.DiscardAndProceed; Close(); };

            // 保存并继续按钮（primary）
            var saveBtn = new Button
            {
                Content = primaryText,
                Padding = new Thickness(14, 6),
                FontSize = 13,
                MinWidth = 60,
            };
            saveBtn.Classes.AddRange(new[] { "btn", "primary" });
            saveBtn.Click += (_, _) => { _result = DirtyConfirmAction.SaveAndProceed; Close(); };

            btnPanel.Children.Add(discardBtn);
            btnPanel.Children.Add(cancelBtn);
            btnPanel.Children.Add(saveBtn);

            cardGrid.Children.Add(titleBlock);
            cardGrid.Children.Add(msgBlock);
            cardGrid.Children.Add(btnPanel);
            card.Child = cardGrid;

            SetupEntranceState(card);
            Content = card;
            ApplyEntranceAnimation(this, card);
        }

        /// <summary>
        /// 从应用资源中按当前主题解析画刷
        /// </summary>
        private static IBrush TryGetThemeBrush(string key, IBrush fallback)
        {
            var app = Application.Current;
            if (app?.Resources is ResourceDictionary dict)
            {
                var theme = app.ActualThemeVariant;
                if (dict.TryGetResource(key, theme, out var value) && value is IBrush brush)
                    return brush;
            }
            return fallback;
        }

        /// <summary>
        /// 创建取消场景的确认弹窗
        /// </summary>
        public static async Task<DirtyConfirmAction> ShowExitConfirmAsync(Visual parent)
        {
            var L = LocalizationService.Instance;
            var window = new DirtyConfirmWindow(
                L.Get("dirty_confirm_title"),
                L.Get("dirty_exit_message"),
                L.Get("dirty_save_exit"),
                L.Get("dirty_confirm_exit"))
            {
                Topmost = true,
            };
            await window.ShowDialog(parent as Window ?? GetTopWindow()!);
            return window._result;
        }

        /// <summary>
        /// 创建切换场景的确认弹窗
        /// </summary>
        public static async Task<DirtyConfirmAction> ShowSwitchConfirmAsync(Visual parent)
        {
            var L = LocalizationService.Instance;
            var window = new DirtyConfirmWindow(
                L.Get("dirty_confirm_title"),
                L.Get("dirty_switch_message"),
                L.Get("dirty_save_switch"),
                L.Get("dirty_confirm_switch"))
            {
                Topmost = true,
            };
            await window.ShowDialog(parent as Window ?? GetTopWindow()!);
            return window._result;
        }

        /// <summary>
        /// 创建删除资源确认弹窗
        /// </summary>
        public static async Task<bool> ShowDeleteConfirmAsync(Visual parent, string displayName)
        {
            var parentWin = parent as Window ?? GetTopWindow();
            if (parentWin == null) return false;

            var cardBg = TryGetThemeBrush("Lv3Bg", SolidColorBrush.Parse("#FFFFFF"));
            var textPrimary = TryGetThemeBrush("TextPrimary", SolidColorBrush.Parse("#E6E6E6"));
            var textSecondary = TryGetThemeBrush("TextSecondary", SolidColorBrush.Parse("#8E8E8E"));

            var window = new Window
            {
                Width = 360,
                Height = 190,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Topmost = true,
                Background = Brushes.Transparent,
            };
            window.WindowDecorations = Avalonia.Controls.WindowDecorations.None;

            // 卡片容器 — 可拖拽
            var card = new Border
            {
                Width = 320,
                CornerRadius = new CornerRadius(12),
                Background = cardBg,
                BoxShadow = DialogShadow,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            card.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(window).Properties.IsLeftButtonPressed)
                    window.BeginMoveDrag(e);
            };

            var grid = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
                Margin = new Thickness(24, 20),
                RowSpacing = 12,
            };

            var L = LocalizationService.Instance;
            var title = new TextBlock
            {
                Text = L.Get("dirty_delete_title"),
                FontSize = 16,
                FontWeight = FontWeight.SemiBold,
                Foreground = textPrimary,
            };
            Grid.SetRow(title, 0);

            var msg = new TextBlock
            {
                Text = L.Get("dirty_delete_message", displayName),
                FontSize = 13,
                Foreground = textSecondary,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(msg, 1);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
            };
            Grid.SetRow(btnPanel, 2);

            var cancelBtn = new Button
            {
                Content = LocalizationService.Instance.Get("cancel"),
                Padding = new Thickness(14, 6),
                FontSize = 13,
                MinWidth = 60,
            };
            cancelBtn.Classes.AddRange(new[] { "btn", "secondary" });
            var confirmed = false;
            cancelBtn.Click += (_, _) => { window.Close(); };

            var deleteBtn = new Button
            {
                Content = LocalizationService.Instance.Get("dirty_confirm_delete"),
                Padding = new Thickness(14, 6),
                FontSize = 13,
                MinWidth = 80,
            };
            deleteBtn.Classes.AddRange(new[] { "btn", "danger" });
            deleteBtn.Click += (_, _) => { confirmed = true; window.Close(); };

            btnPanel.Children.Add(cancelBtn);
            btnPanel.Children.Add(deleteBtn);

            grid.Children.Add(title);
            grid.Children.Add(msg);
            grid.Children.Add(btnPanel);
            card.Child = grid;
            SetupEntranceState(card);
            window.Content = card;
            ApplyEntranceAnimation(window, card);

            await window.ShowDialog(parentWin);
            return confirmed;
        }

        /// <summary>
        /// 提示弹窗（仅"确定"按钮），用于操作结果反馈
        /// </summary>
        public static async Task ShowToastAsync(string title, string message)
        {
            var parentWin = GetTopWindow();
            if (parentWin == null) return;

            var cardBg = TryGetThemeBrush("Lv3Bg", SolidColorBrush.Parse("#FFFFFF"));
            var textPrimary = TryGetThemeBrush("TextPrimary", SolidColorBrush.Parse("#E6E6E6"));
            var textSecondary = TryGetThemeBrush("TextSecondary", SolidColorBrush.Parse("#8E8E8E"));

            var window = new Window
            {
                Width = 320,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Topmost = true,
                Background = Brushes.Transparent,
            };
            window.WindowDecorations = WindowDecorations.None;

            var card = new Border
            {
                Width = 280,
                CornerRadius = new CornerRadius(12),
                Background = cardBg,
                BoxShadow = DialogShadow,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            card.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(window).Properties.IsLeftButtonPressed)
                    window.BeginMoveDrag(e);
            };

            var grid = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
                Margin = new Thickness(24, 20),
                RowSpacing = 12,
            };

            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeight.SemiBold,
                Foreground = textPrimary,
            };
            Grid.SetRow(titleBlock, 0);

            var msgBlock = new TextBlock
            {
                Text = message,
                FontSize = 13,
                Foreground = textSecondary,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(msgBlock, 1);

            var okBtn = new Button
            {
                Content = LocalizationService.Instance.Get("confirm"),
                Padding = new Thickness(20, 6),
                FontSize = 13,
                MinWidth = 60,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            okBtn.Classes.AddRange(new[] { "btn", "primary" });
            okBtn.Click += (_, _) => window.Close();
            Grid.SetRow(okBtn, 2);

            grid.Children.Add(titleBlock);
            grid.Children.Add(msgBlock);
            grid.Children.Add(okBtn);
            card.Child = grid;
            SetupEntranceState(card);
            window.Content = card;
            ApplyEntranceAnimation(window, card);

            await window.ShowDialog(parentWin);
        }

        private static Window? GetTopWindow()
        {
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow;
            return null;
        }

        /// <summary>
        /// 通用确认弹窗（确认/取消），返回 true 表示用户点击确认
        /// </summary>
        public static async Task<bool> ShowConfirmAsync(string title, string message)
        {
            var parentWin = GetTopWindow();
            if (parentWin == null) return false;

            var cardBg = TryGetThemeBrush("Lv3Bg", SolidColorBrush.Parse("#FFFFFF"));
            var textPrimary = TryGetThemeBrush("TextPrimary", SolidColorBrush.Parse("#E6E6E6"));
            var textSecondary = TryGetThemeBrush("TextSecondary", SolidColorBrush.Parse("#8E8E8E"));

            var window = new Window
            {
                Width = 360,
                Height = 190,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Topmost = true,
                Background = Brushes.Transparent,
            };
            window.WindowDecorations = WindowDecorations.None;

            var card = new Border
            {
                Width = 320,
                CornerRadius = new CornerRadius(12),
                Background = cardBg,
                BoxShadow = DialogShadow,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            card.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(window).Properties.IsLeftButtonPressed)
                    window.BeginMoveDrag(e);
            };

            var grid = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
                Margin = new Thickness(24, 20),
                RowSpacing = 12,
            };

            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeight.SemiBold,
                Foreground = textPrimary,
            };
            Grid.SetRow(titleBlock, 0);

            var msgBlock = new TextBlock
            {
                Text = message,
                FontSize = 13,
                Foreground = textSecondary,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(msgBlock, 1);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
            };
            Grid.SetRow(btnPanel, 2);

            var confirmed = false;

            var cancelBtn = new Button
            {
                Content = LocalizationService.Instance.Get("cancel"),
                Padding = new Thickness(14, 6),
                FontSize = 13,
                MinWidth = 60,
            };
            cancelBtn.Classes.AddRange(new[] { "btn", "secondary" });
            cancelBtn.Click += (_, _) => { window.Close(); };

            var confirmBtn = new Button
            {
                Content = LocalizationService.Instance.Get("dirty_confirm_btn"),
                Padding = new Thickness(14, 6),
                FontSize = 13,
                MinWidth = 60,
            };
            confirmBtn.Classes.AddRange(new[] { "btn", "primary" });
            confirmBtn.Click += (_, _) => { confirmed = true; window.Close(); };

            btnPanel.Children.Add(cancelBtn);
            btnPanel.Children.Add(confirmBtn);

            grid.Children.Add(titleBlock);
            grid.Children.Add(msgBlock);
            grid.Children.Add(btnPanel);
            card.Child = grid;
            SetupEntranceState(card);
            window.Content = card;
            ApplyEntranceAnimation(window, card);

            await window.ShowDialog(parentWin);
            return confirmed;
        }
    }
}
