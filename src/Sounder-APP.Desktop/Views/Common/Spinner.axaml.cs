using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Sounder_APP.Views.Common;

public partial class Spinner : UserControl
{
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<Spinner, double>(nameof(Size), defaultValue: 24.0);

    public static readonly StyledProperty<IBrush?> ColorProperty =
        AvaloniaProperty.Register<Spinner, IBrush?>(nameof(Color));

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public IBrush? Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public Spinner()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SizeProperty)
        {
            var val = change.GetNewValue<double>();
            PART_Icon.Width = val;
            PART_Icon.Height = val;
        }
        else if (change.Property == ColorProperty)
        {
            PART_Icon.Foreground = change.GetNewValue<IBrush?>();
        }
    }
}
