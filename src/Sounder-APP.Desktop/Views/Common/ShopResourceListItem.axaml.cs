using Avalonia;
using Avalonia.Controls;
using Sounder_APP.Models;

namespace Sounder_APP.Views.Common
{
    public partial class ShopResourceListItem : UserControl
    {
        /// <summary>资源数据（绑定当前 DataContext 中的 Resource）</summary>
        public static readonly StyledProperty<Resource?> ResourceProperty =
            AvaloniaProperty.Register<ShopResourceListItem, Resource?>(nameof(Resource));

        /// <summary>父级 ShopViewModel（提供 InstallCommand / InstallingResourceId / InstallProgress 等）</summary>
        public static readonly StyledProperty<object?> ViewModelProperty =
            AvaloniaProperty.Register<ShopResourceListItem, object?>(nameof(ViewModel));

        public Resource? Resource
        {
            get => GetValue(ResourceProperty);
            set => SetValue(ResourceProperty, value);
        }

        public object? ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public ShopResourceListItem()
        {
            InitializeComponent();
        }
    }
}
