using Avalonia;
using Avalonia.Controls;
using Sounder_APP.ViewModels;

namespace Sounder_APP.Views
{
    public partial class ResourceListContent : UserControl
    {
        public static readonly StyledProperty<ShopViewModel> ViewModelProperty =
            AvaloniaProperty.Register<ResourceListContent, ShopViewModel>(nameof(ViewModel));

        public ShopViewModel ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public ResourceListContent()
        {
            InitializeComponent();
        }
    }
}
