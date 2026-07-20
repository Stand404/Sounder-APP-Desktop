using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;

namespace Sounder_APP.Views.Submissions;

public partial class SubmissionCardView : UserControl
{
    private bool _isExpanded;

    public SubmissionCardView()
    {
        InitializeComponent();
    }

    private void OnHeaderTapped(object? sender, TappedEventArgs e)
    {
        _isExpanded = !_isExpanded;
        DetailPanel.IsVisible = _isExpanded;

        if (_isExpanded)
        {
            ExpandArrow.RenderTransform = new Avalonia.Media.RotateTransform(180);
        }
        else
        {
            ExpandArrow.RenderTransform = new Avalonia.Media.RotateTransform(0);
        }
    }

    private async void OnCopyTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string text && !string.IsNullOrEmpty(text))
        {
            await CopyToClipboardAsync(text);
        }
    }

    private static async Task CopyToClipboardAsync(string text)
    {
        var topLevel = TopLevel.GetTopLevel(Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow : null);

        if (topLevel?.Clipboard != null)
        {
            await topLevel.Clipboard.SetTextAsync(text ?? string.Empty);
        }
    }
}
