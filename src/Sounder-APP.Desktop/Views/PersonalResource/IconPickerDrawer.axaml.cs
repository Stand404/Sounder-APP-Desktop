using Avalonia.Controls;
using Avalonia.Input;
using Sounder_APP.Models;
using Sounder_APP.ViewModels;

namespace Sounder_APP.Views;

public partial class IconPickerDrawer : UserControl
{
    public IconPickerDrawer()
    {
        InitializeComponent();
    }

    private void OnIconItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.DataContext is PickerIconItem item)
        {
            if (DataContext is PersonalResourceEditViewModel vm)
            {
                vm.SelectIconItem(item);
            }
        }
    }

    private void OnConfirmed(object? sender, System.EventArgs e)
    {
        if (DataContext is PersonalResourceEditViewModel vm)
        {
            vm.ConfirmIconPickerCommand.Execute(null);
        }
    }

    private void OnCancelled(object? sender, System.EventArgs e)
    {
        if (DataContext is PersonalResourceEditViewModel vm)
        {
            vm.CancelIconPickerCommand.Execute(null);
        }
    }
}
