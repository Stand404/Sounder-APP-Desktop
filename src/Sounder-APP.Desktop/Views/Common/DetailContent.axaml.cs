using Avalonia;
using Avalonia.Controls;

namespace Sounder_APP.Views
{
    public partial class DetailContent : UserControl
    {
        public static readonly StyledProperty<object?> ViewModelProperty =
            AvaloniaProperty.Register<DetailContent, object?>(nameof(ViewModel));

        public object? ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public DetailContent()
        {
            InitializeComponent();
        }

    }
}
