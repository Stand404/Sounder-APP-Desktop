using Avalonia.Controls;
using Sounder_APP.ViewModels;

namespace Sounder_APP.Views;

public partial class SubmissionsView : UserControl
{
    public SubmissionsView()
    {
        InitializeComponent();

        // 订阅 Drawer 事件，传递到 ViewModel
        FormDrawer.Confirmed += (_, _) =>
        {
            if (DataContext is SubmissionsViewModel vm)
                vm.FormViewModel.SubmitCommand.Execute(null);
        };

        FormDrawer.Cancelled += (_, _) =>
        {
            if (DataContext is SubmissionsViewModel vm)
                vm.FormViewModel.CancelCommand.Execute(null);
        };

        FormDrawer.Closed += (_, _) =>
        {
            if (DataContext is SubmissionsViewModel vm)
                vm.CloseFormDrawerCommand.Execute(null);
        };
    }
}
