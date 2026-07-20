using Avalonia;
using Avalonia.Controls;
using Sounder_APP.Models;

namespace Sounder_APP.Views.Common
{
    public partial class PersonalResourceListItem : UserControl
    {
        /// <summary>资源数据（绑定当前 DataContext 中的 Resource）</summary>
        public static readonly StyledProperty<Resource?> ResourceProperty =
            AvaloniaProperty.Register<PersonalResourceListItem, Resource?>(nameof(Resource));

        /// <summary>父级 PersonalResourceViewModel（提供 UninstallResourceCommand）</summary>
        public static readonly StyledProperty<object?> ViewModelProperty =
            AvaloniaProperty.Register<PersonalResourceListItem, object?>(nameof(ViewModel));

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

        public PersonalResourceListItem()
        {
            InitializeComponent();
        }
    }
}
