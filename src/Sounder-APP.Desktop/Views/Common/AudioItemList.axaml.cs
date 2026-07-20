using Avalonia;
using Avalonia.Controls;

namespace Sounder_APP.Views
{
    public partial class AudioItemList : UserControl
    {
        public static readonly StyledProperty<object?> ViewModelProperty =
            AvaloniaProperty.Register<AudioItemList, object?>(nameof(ViewModel));

        public object? ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public AudioItemList()
        {
            InitializeComponent();
        }
    }
}
