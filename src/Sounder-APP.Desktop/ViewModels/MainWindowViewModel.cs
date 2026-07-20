using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sounder_APP.Services;

namespace Sounder_APP.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ViewModelBase? _activeViewModel;

        [ObservableProperty]
        private bool _isPersonalSelected = true;

        [ObservableProperty]
        private bool _isShopSelected;

        [ObservableProperty]
        private bool _isSettingsSelected;

        [ObservableProperty]
        private bool _isSubmissionsSelected;

        [ObservableProperty]
        private bool _isTaskManagerSelected;

        private readonly ResourceService _resourceService;
        private readonly DownloadManager _downloadManager;
        private ShopViewModel _shopViewModel;
        private PersonalResourceViewModel _personalResourceViewModel;
        private SettingsViewModel _settingsViewModel;
        private SubmissionsViewModel _submissionsViewModel;
        private TaskManagerViewModel _taskManagerViewModel;
        public MainWindowViewModel()
        {
            _resourceService = new ResourceService();
            _downloadManager = new DownloadManager(_resourceService);
            _shopViewModel = new ShopViewModel(_resourceService, _downloadManager);
            _personalResourceViewModel = new PersonalResourceViewModel(_resourceService, _downloadManager);
            _settingsViewModel = new SettingsViewModel(new AppSettings());
            _submissionsViewModel = new SubmissionsViewModel(new ApiService());
            _taskManagerViewModel = new TaskManagerViewModel(App.BackgroundPlayback!);

            ActiveViewModel = _personalResourceViewModel;
        }

        /// <summary>导航前停止所有音频播放</summary>
        private void StopAllAudioPlayback()
        {
            _personalResourceViewModel.StopAllPlayback();
            _shopViewModel.StopAllPlayback();
        }

        [RelayCommand]
        private async Task NavigateToShop()
        {
            StopAllAudioPlayback();
            IsShopSelected = true;
            IsPersonalSelected = false;
            IsSettingsSelected = false;
            IsSubmissionsSelected = false;
            IsTaskManagerSelected = false;
            ActiveViewModel = _shopViewModel;
            await _shopViewModel.ActivateAsync();
        }

        [RelayCommand]
        private async Task NavigateToPersonal()
        {
            StopAllAudioPlayback();
            IsShopSelected = false;
            IsPersonalSelected = true;
            IsSettingsSelected = false;
            IsSubmissionsSelected = false;
            IsTaskManagerSelected = false;
            ActiveViewModel = _personalResourceViewModel;
            await _personalResourceViewModel.LoadResourcesCommand.ExecuteAsync(null);
        }

        [RelayCommand]
        private void NavigateToSettings()
        {
            StopAllAudioPlayback();
            IsShopSelected = false;
            IsPersonalSelected = false;
            IsSettingsSelected = true;
            IsSubmissionsSelected = false;
            IsTaskManagerSelected = false;
            ActiveViewModel = _settingsViewModel;
        }

        [RelayCommand]
        private async Task NavigateToSubmissions()
        {
            StopAllAudioPlayback();
            IsShopSelected = false;
            IsPersonalSelected = false;
            IsSettingsSelected = false;
            IsSubmissionsSelected = true;
            IsTaskManagerSelected = false;
            ActiveViewModel = _submissionsViewModel;
            await _submissionsViewModel.ActivateCommand.ExecuteAsync(null);
        }

        [RelayCommand]
        private void NavigateToTaskManager()
        {
            StopAllAudioPlayback();
            IsShopSelected = false;
            IsPersonalSelected = false;
            IsSettingsSelected = false;
            IsSubmissionsSelected = false;
            IsTaskManagerSelected = true;
            ActiveViewModel = _taskManagerViewModel;
        }

        /// <summary>停止所有音频播放（供外部调用，如托盘退出时）</summary>
        public void StopAllAudio()
        {
            StopAllAudioPlayback();
        }

        public string GetTypeName() => GetType().FullName ?? string.Empty;
    }
}
