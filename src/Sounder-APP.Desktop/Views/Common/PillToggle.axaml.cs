using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Windows.Input;

namespace Sounder_APP.Views.Common;

/// <summary>
/// 固定2项的 Pill Toggle。
/// 指示器使用 TranslateTransform（GPU 合成层动画），匹配 Vue ::before + translateX 方案。
/// </summary>
public class PillToggle2 : TemplatedControl
{
    public static readonly StyledProperty<string> Label0Property =
        AvaloniaProperty.Register<PillToggle2, string>(nameof(Label0), "");
    public static readonly StyledProperty<string> Label1Property =
        AvaloniaProperty.Register<PillToggle2, string>(nameof(Label1), "");
    public static readonly StyledProperty<bool> IsSelected0Property =
        AvaloniaProperty.Register<PillToggle2, bool>(nameof(IsSelected0));
    public static readonly StyledProperty<bool> IsSelected1Property =
        AvaloniaProperty.Register<PillToggle2, bool>(nameof(IsSelected1));
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<PillToggle2, int>(nameof(SelectedIndex), 0, defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PillToggle2, ICommand?>(nameof(Command));
    public static readonly StyledProperty<object?> CommandParameter0Property =
        AvaloniaProperty.Register<PillToggle2, object?>(nameof(CommandParameter0));
    public static readonly StyledProperty<object?> CommandParameter1Property =
        AvaloniaProperty.Register<PillToggle2, object?>(nameof(CommandParameter1));

    public string Label0 { get => GetValue(Label0Property); set => SetValue(Label0Property, value); }
    public string Label1 { get => GetValue(Label1Property); set => SetValue(Label1Property, value); }
    public bool IsSelected0 { get => GetValue(IsSelected0Property); set => SetValue(IsSelected0Property, value); }
    public bool IsSelected1 { get => GetValue(IsSelected1Property); set => SetValue(IsSelected1Property, value); }
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter0 { get => GetValue(CommandParameter0Property); set => SetValue(CommandParameter0Property, value); }
    public object? CommandParameter1 { get => GetValue(CommandParameter1Property); set => SetValue(CommandParameter1Property, value); }

    private Border? _indicator;
    private Grid? _host;
    private TranslateTransform? _slide;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Grid>("PART_Host") ?? this.FindControl<Grid>("PART_Host");
        _indicator = e.NameScope.Find<Border>("PART_Indicator") ?? this.FindControl<Border>("PART_Indicator");

        if (e.NameScope.Find<Button>("PART_Btn0") is Button b0)
            b0.Click += (_, _) => { SelectedIndex = 0; Command?.Execute(CommandParameter0); };
        if (e.NameScope.Find<Button>("PART_Btn1") is Button b1)
            b1.Click += (_, _) => { SelectedIndex = 1; Command?.Execute(CommandParameter1); };

        if (_indicator != null && _slide == null)
        {
            _slide = new TranslateTransform(0, 0);
            _slide.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = TranslateTransform.XProperty,
                    Duration = TimeSpan.FromMilliseconds(300),
                    Easing = new CubicEaseOut()
                }
            };
            _indicator.RenderTransform = _slide;
            _indicator.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = Border.WidthProperty,
                    Duration = TimeSpan.FromMilliseconds(300),
                    Easing = new CubicEaseOut()
                }
            };
        }

        UpdateIndicator();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty || change.Property == IsSelected0Property || change.Property == IsSelected1Property)
            UpdateIndicator();
    }

    private void UpdateIndicator()
    {
        if (_indicator == null || _host == null || _slide == null) return;

        // 从绑定的 IsSelected 属性推导选中索引，避免覆盖绑定值
        int sel = IsSelected0 ? 0 : 1;

        if (_host.Bounds.Width > 0)
        {
            // 匹配 Android: 指示器填满整列，偏移 = 列宽 × 索引
            var colW = _host.Bounds.Width / 2;
            _indicator.Width = colW;
            _slide.X = colW * sel;
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var s = base.ArrangeOverride(finalSize);
        UpdateIndicator();
        return s;
    }
}

/// <summary>
/// 固定3项的 Pill Toggle。
/// 指示器使用 TranslateTransform（GPU 合成层动画），匹配 Vue ::before + translateX 方案。
/// </summary>
public class PillToggle3 : TemplatedControl
{
    public static readonly StyledProperty<string> Label0Property =
        AvaloniaProperty.Register<PillToggle3, string>(nameof(Label0), "");
    public static readonly StyledProperty<string> Label1Property =
        AvaloniaProperty.Register<PillToggle3, string>(nameof(Label1), "");
    public static readonly StyledProperty<string> Label2Property =
        AvaloniaProperty.Register<PillToggle3, string>(nameof(Label2), "");
    public static readonly StyledProperty<bool> IsSelected0Property =
        AvaloniaProperty.Register<PillToggle3, bool>(nameof(IsSelected0));
    public static readonly StyledProperty<bool> IsSelected1Property =
        AvaloniaProperty.Register<PillToggle3, bool>(nameof(IsSelected1));
    public static readonly StyledProperty<bool> IsSelected2Property =
        AvaloniaProperty.Register<PillToggle3, bool>(nameof(IsSelected2));
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<PillToggle3, int>(nameof(SelectedIndex), 0, defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PillToggle3, ICommand?>(nameof(Command));
    public static readonly StyledProperty<object?> CommandParameter0Property =
        AvaloniaProperty.Register<PillToggle3, object?>(nameof(CommandParameter0));
    public static readonly StyledProperty<object?> CommandParameter1Property =
        AvaloniaProperty.Register<PillToggle3, object?>(nameof(CommandParameter1));
    public static readonly StyledProperty<object?> CommandParameter2Property =
        AvaloniaProperty.Register<PillToggle3, object?>(nameof(CommandParameter2));

    public static readonly StyledProperty<IBrush?> IndicatorColor0Property =
        AvaloniaProperty.Register<PillToggle3, IBrush?>(nameof(IndicatorColor0));
    public static readonly StyledProperty<IBrush?> IndicatorColor1Property =
        AvaloniaProperty.Register<PillToggle3, IBrush?>(nameof(IndicatorColor1));
    public static readonly StyledProperty<IBrush?> IndicatorColor2Property =
        AvaloniaProperty.Register<PillToggle3, IBrush?>(nameof(IndicatorColor2));

    public string Label0 { get => GetValue(Label0Property); set => SetValue(Label0Property, value); }
    public string Label1 { get => GetValue(Label1Property); set => SetValue(Label1Property, value); }
    public string Label2 { get => GetValue(Label2Property); set => SetValue(Label2Property, value); }
    public bool IsSelected0 { get => GetValue(IsSelected0Property); set => SetValue(IsSelected0Property, value); }
    public bool IsSelected1 { get => GetValue(IsSelected1Property); set => SetValue(IsSelected1Property, value); }
    public bool IsSelected2 { get => GetValue(IsSelected2Property); set => SetValue(IsSelected2Property, value); }
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter0 { get => GetValue(CommandParameter0Property); set => SetValue(CommandParameter0Property, value); }
    public object? CommandParameter1 { get => GetValue(CommandParameter1Property); set => SetValue(CommandParameter1Property, value); }
    public object? CommandParameter2 { get => GetValue(CommandParameter2Property); set => SetValue(CommandParameter2Property, value); }
    public IBrush? IndicatorColor0 { get => GetValue(IndicatorColor0Property); set => SetValue(IndicatorColor0Property, value); }
    public IBrush? IndicatorColor1 { get => GetValue(IndicatorColor1Property); set => SetValue(IndicatorColor1Property, value); }
    public IBrush? IndicatorColor2 { get => GetValue(IndicatorColor2Property); set => SetValue(IndicatorColor2Property, value); }

    private Border? _indicator;
    private Grid? _host;
    private TranslateTransform? _slide;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Grid>("PART_Host") ?? this.FindControl<Grid>("PART_Host");
        _indicator = e.NameScope.Find<Border>("PART_Indicator") ?? this.FindControl<Border>("PART_Indicator");

        if (e.NameScope.Find<Button>("PART_Btn0") is Button b0)
            b0.Click += (_, _) => { SelectedIndex = 0; Command?.Execute(CommandParameter0); };
        if (e.NameScope.Find<Button>("PART_Btn1") is Button b1)
            b1.Click += (_, _) => { SelectedIndex = 1; Command?.Execute(CommandParameter1); };
        if (e.NameScope.Find<Button>("PART_Btn2") is Button b2)
            b2.Click += (_, _) => { SelectedIndex = 2; Command?.Execute(CommandParameter2); };

        if (_indicator != null && _slide == null)
        {
            _slide = new TranslateTransform(0, 0);
            _slide.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = TranslateTransform.XProperty,
                    Duration = TimeSpan.FromMilliseconds(300),
                    Easing = new CubicEaseOut()
                }
            };
            _indicator.RenderTransform = _slide;
            _indicator.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = Border.WidthProperty,
                    Duration = TimeSpan.FromMilliseconds(300),
                    Easing = new CubicEaseOut()
                }
            };
        }

        UpdateIndicator();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty ||
            change.Property == IsSelected0Property ||
            change.Property == IsSelected1Property ||
            change.Property == IsSelected2Property ||
            change.Property == IndicatorColor0Property ||
            change.Property == IndicatorColor1Property ||
            change.Property == IndicatorColor2Property)
            UpdateIndicator();
    }

    private void UpdateIndicator()
    {
        if (_indicator == null || _host == null || _slide == null) return;

        // 从绑定的 IsSelected 属性推导选中索引，避免覆盖绑定值
        int sel = IsSelected0 ? 0 : (IsSelected1 ? 1 : 2);

        // 根据选中列更新指示器颜色（null 时回退到默认 AccentBlue）
        var color = sel switch
        {
            0 => IndicatorColor0,
            1 => IndicatorColor1,
            2 => IndicatorColor2,
            _ => null
        };
        if (color != null)
        {
            _indicator.Background = color;
        }

        if (_host.Bounds.Width > 0)
        {
            // 匹配 Android: 指示器填满整列，偏移 = 列宽 × 索引
            var colW = _host.Bounds.Width / 3;
            _indicator.Width = colW;
            _slide.X = colW * sel;
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var s = base.ArrangeOverride(finalSize);
        UpdateIndicator();
        return s;
    }
}



