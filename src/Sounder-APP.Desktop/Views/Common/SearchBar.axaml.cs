using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Sounder_APP.Models;

namespace Sounder_APP.Views
{
    public partial class SearchBar : UserControl
    {
        private string? _lastQuery;

        public static readonly StyledProperty<ISearchViewModel?> ViewModelProperty =
            AvaloniaProperty.Register<SearchBar, ISearchViewModel?>(nameof(ViewModel));

        public ISearchViewModel? ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public SearchBar()
        {
            InitializeComponent();
        }

        private void OnSearchTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ExecuteSearch();
                e.Handled = true;
            }
        }

        private void OnSearchButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ExecuteSearch();
        }

        private void ExecuteSearch()
        {
            if (ViewModel == null) return;

            var query = ViewModel.SearchQuery?.Trim() ?? string.Empty;

            _lastQuery = query;

            if (ViewModel.SearchCommand.CanExecute(null))
            {
                ViewModel.SearchCommand.Execute(null);
            }
        }
    }
}
