using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace Sounder_APP.Views.Common
{
    [PseudoClasses(":loading", ":hasMore")]
    public partial class LoadMoreFooter : UserControl
    {
        public static readonly StyledProperty<bool> IsLoadingProperty =
            AvaloniaProperty.Register<LoadMoreFooter, bool>(nameof(IsLoading));

        public static readonly StyledProperty<bool> HasMoreProperty =
            AvaloniaProperty.Register<LoadMoreFooter, bool>(nameof(HasMore));

        public static readonly StyledProperty<ICommand?> LoadMoreCommandProperty =
            AvaloniaProperty.Register<LoadMoreFooter, ICommand?>(nameof(LoadMoreCommand));

        public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }
        public bool HasMore { get => GetValue(HasMoreProperty); set => SetValue(HasMoreProperty, value); }
        public ICommand? LoadMoreCommand { get => GetValue(LoadMoreCommandProperty); set => SetValue(LoadMoreCommandProperty, value); }

        public LoadMoreFooter()
        {
            InitializeComponent();
        }
    }
}
