using Avalonia.Controls;
using Sounder_APP.Models;
using Sounder_APP.ViewModels;

namespace Sounder_APP.Views;

public partial class AudioPickerDrawer : UserControl
{
    public AudioPickerDrawer()
    {
        InitializeComponent();
    }

    private void OnAudioListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox
            && listBox.SelectedItem is PickerAudioItem item
            && DataContext is PersonalResourceEditViewModel vm)
        {
            vm.TogglePickerItem(item);
            listBox.SelectedItem = null;
        }
    }

    private void OnConfirmed(object? sender, System.EventArgs e)
    {
        if (DataContext is PersonalResourceEditViewModel vm)
        {
            vm.ConfirmPickerSelectionCommand.Execute(null);
        }
    }

    private void OnCancelled(object? sender, System.EventArgs e)
    {
        if (DataContext is PersonalResourceEditViewModel vm)
        {
            vm.CancelPickerCommand.Execute(null);
        }
    }
}
