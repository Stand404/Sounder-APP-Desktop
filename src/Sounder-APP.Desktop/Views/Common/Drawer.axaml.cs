using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Sounder_APP.Views.Common;

public partial class Drawer : UserControl
{
    #region Styled Properties

    /// <summary>是否打开抽屉（带动画）</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<Drawer, bool>(nameof(IsOpen), defaultValue: false);

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>抽屉宽度（默认 420）</summary>
    public static readonly StyledProperty<double> DrawerWidthProperty =
        AvaloniaProperty.Register<Drawer, double>(nameof(DrawerWidth), defaultValue: 420.0);

    public double DrawerWidth
    {
        get => GetValue(DrawerWidthProperty);
        set => SetValue(DrawerWidthProperty, value);
    }

    /// <summary>标题文本</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<Drawer, string>(nameof(Title), defaultValue: "标题");

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>抽屉内容</summary>
    public static readonly StyledProperty<object?> DrawerContentProperty =
        AvaloniaProperty.Register<Drawer, object?>(nameof(DrawerContent));

    public object? DrawerContent
    {
        get => GetValue(DrawerContentProperty);
        set => SetValue(DrawerContentProperty, value);
    }

    /// <summary>是否显示底部操作栏</summary>
    public static readonly StyledProperty<bool> ShowFooterProperty =
        AvaloniaProperty.Register<Drawer, bool>(nameof(ShowFooter), defaultValue: true);

    public bool ShowFooter
    {
        get => GetValue(ShowFooterProperty);
        set => SetValue(ShowFooterProperty, value);
    }

    /// <summary>底部左侧提示文本</summary>
    public static readonly StyledProperty<string?> FooterHintProperty =
        AvaloniaProperty.Register<Drawer, string?>(nameof(FooterHint));

    public string? FooterHint
    {
        get => GetValue(FooterHintProperty);
        set => SetValue(FooterHintProperty, value);
    }

    /// <summary>确认按钮文本（默认 "确认"）</summary>
    public static readonly StyledProperty<string> ConfirmTextProperty =
        AvaloniaProperty.Register<Drawer, string>(nameof(ConfirmText), defaultValue: "确认");

    public string ConfirmText
    {
        get => GetValue(ConfirmTextProperty);
        set => SetValue(ConfirmTextProperty, value);
    }

    /// <summary>取消按钮文本（默认 "取消"）</summary>
    public static readonly StyledProperty<string> CancelTextProperty =
        AvaloniaProperty.Register<Drawer, string>(nameof(CancelText), defaultValue: "取消");

    public string CancelText
    {
        get => GetValue(CancelTextProperty);
        set => SetValue(CancelTextProperty, value);
    }

    #endregion

    #region Routed Events

    /// <summary>确认按钮点击</summary>
    public event EventHandler? Confirmed;

    /// <summary>取消按钮点击</summary>
    public event EventHandler? Cancelled;

    /// <summary>关闭（点击遮罩或 X 按钮）</summary>
    public event EventHandler? Closed;

    #endregion

    private TranslateTransform _drawerTransform = new();
    private bool _isAnimating;

    public Drawer()
    {
        InitializeComponent();
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        // 代码创建 RenderTransform，避免 x:Name 生成问题
        PART_DrawerPanel.RenderTransform = _drawerTransform;
        _drawerTransform.X = DrawerWidth;

        // 初始化按钮可见性
        PART_CancelBtn.IsVisible = !string.IsNullOrEmpty(CancelText);
        PART_FooterHint.IsVisible = !string.IsNullOrEmpty(FooterHint);

        // 监听属性变化以更新 UI 可见性
        PropertyChanged += (s, e) =>
        {
            if (e.Property == CancelTextProperty)
            {
                PART_CancelBtn.IsVisible = !string.IsNullOrEmpty(CancelText);
            }
            else if (e.Property == FooterHintProperty)
            {
                PART_FooterHint.IsVisible = !string.IsNullOrEmpty(FooterHint);
            }
        };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsOpenProperty)
        {
            var isOpen = change.GetNewValue<bool>();
            if (isOpen)
                _ = AnimateOpenAsync();
            else
                _ = AnimateCloseAsync();
        }
        else if (change.Property == DrawerWidthProperty)
        {
            var width = change.GetNewValue<double>();
            if (!IsOpen)
                _drawerTransform.X = width;
        }
    }

    #region Animation

    /// <summary>打开抽屉：遮罩淡入 + 面板从右侧滑入</summary>
    private async Task AnimateOpenAsync()
    {
        if (_isAnimating) return;
        _isAnimating = true;

        PART_DrawerPanel.IsVisible = true;
        PART_DrawerPanel.IsHitTestVisible = true;
        PART_Backdrop.IsHitTestVisible = true;

        var width = DrawerWidth;
        var durationMs = 280;
        var steps = 24;
        var delay = durationMs / steps;

        for (int i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            // cubic ease-out
            var eased = 1.0 - Math.Pow(1.0 - t, 3);
            _drawerTransform.X = width * (1.0 - eased);
            PART_Backdrop.Opacity = eased;
            await Task.Delay(delay);
        }

        _drawerTransform.X = 0;
        PART_Backdrop.Opacity = 1;
        _isAnimating = false;
    }

    /// <summary>关闭抽屉：遮罩淡出 + 面板滑出</summary>
    private async Task AnimateCloseAsync()
    {
        if (_isAnimating) return;
        _isAnimating = true;

        var width = DrawerWidth;
        var durationMs = 220;
        var steps = 18;
        var delay = durationMs / steps;

        for (int i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            // cubic ease-in
            var eased = Math.Pow(t, 3);
            _drawerTransform.X = width * eased;
            PART_Backdrop.Opacity = 1.0 - eased;
            await Task.Delay(delay);
        }

        _drawerTransform.X = width;
        PART_Backdrop.Opacity = 0;

        // 动画完成后隐藏面板
        PART_DrawerPanel.IsVisible = false;
        PART_DrawerPanel.IsHitTestVisible = false;
        PART_Backdrop.IsHitTestVisible = false;

        _isAnimating = false;
    }

    #endregion

    #region Event Handlers

    private void OnBackdropTapped(object? sender, TappedEventArgs e)
    {
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void OnCloseTapped(object? sender, TappedEventArgs e)
    {
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void OnCancelTapped(object? sender, TappedEventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void OnConfirmTapped(object? sender, TappedEventArgs e)
    {
        Confirmed?.Invoke(this, EventArgs.Empty);
    }

    #endregion
}
