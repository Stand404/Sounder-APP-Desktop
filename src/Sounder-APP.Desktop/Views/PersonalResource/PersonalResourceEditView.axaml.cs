using Avalonia.Controls;
using Avalonia.Input;
using Sounder_APP.Models;
using Sounder_APP.ViewModels;

namespace Sounder_APP.Views;

public partial class PersonalResourceEditView : UserControl
{
    public PersonalResourceEditView()
    {
        InitializeComponent();
    }

    /// <summary>点击图标区域 → 打开图标选择抽屉</summary>
    private void OnIconTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is PersonalResourceEditViewModel vm)
        {
            vm.PickIconFromInternalCommand.Execute(null);
        }
    }

    /// <summary>点击音频「添加」按钮 → 打开音频选择抽屉</summary>
    private void OnAddAudioTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is PersonalResourceEditViewModel vm)
        {
            vm.AddAudioFromInternalCommand.Execute(null);
        }
    }

    /// <summary>点击试听按钮 → 播放 / 停止</summary>
    private void OnAudioPlayTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is EditAudioItem item)
        {
            if (DataContext is PersonalResourceEditViewModel vm)
            {
                vm.TogglePreview(item);
            }
        }
    }
}
