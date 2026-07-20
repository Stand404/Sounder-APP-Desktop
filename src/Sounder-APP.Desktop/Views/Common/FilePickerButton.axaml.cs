using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace Sounder_APP.Views.Common;

public partial class FilePickerButton : UserControl
{
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<FilePickerButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<FilePickerButton, string>(nameof(Text), "从文件选择");

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public FilePickerButton()
    {
        InitializeComponent();
    }
}
