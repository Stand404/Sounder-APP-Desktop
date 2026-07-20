using Avalonia;
using Avalonia.Controls;
using Sounder_APP.ViewModels;

namespace Sounder_APP.Views
{
    public partial class ShopDetailPanel : UserControl
    {
        public static readonly StyledProperty<ShopViewModel> ViewModelProperty =
            AvaloniaProperty.Register<ShopDetailPanel, ShopViewModel>(nameof(ViewModel));

        public ShopViewModel ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public ShopDetailPanel()
        {
            InitializeComponent();
        }
    }
}
