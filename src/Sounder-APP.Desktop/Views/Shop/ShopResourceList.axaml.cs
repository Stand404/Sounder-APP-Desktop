using Avalonia;
using Avalonia.Controls;
using Sounder_APP.ViewModels;

namespace Sounder_APP.Views
{
    public partial class ShopResourceList : UserControl
    {
        public static readonly StyledProperty<ShopViewModel> ViewModelProperty =
            AvaloniaProperty.Register<ShopResourceList, ShopViewModel>(nameof(ViewModel));

        public ShopViewModel ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public ShopResourceList()
        {
            InitializeComponent();
        }
    }
}
