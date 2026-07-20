using CommunityToolkit.Mvvm.Input;

namespace Sounder_APP.Models
{
    public interface ISearchViewModel
    {
        string SearchQuery { get; set; }
        IAsyncRelayCommand SearchCommand { get; }
    }
}
