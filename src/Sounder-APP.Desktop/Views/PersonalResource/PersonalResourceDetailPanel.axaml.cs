using Avalonia;
using Avalonia.Controls;
using Sounder_APP.ViewModels;

namespace Sounder_APP.Views
{
    public partial class PersonalResourceDetailPanel : UserControl
    {
        public static readonly StyledProperty<PersonalResourceViewModel> ViewModelProperty =
            AvaloniaProperty.Register<PersonalResourceDetailPanel, PersonalResourceViewModel>(nameof(ViewModel));

        public PersonalResourceViewModel ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public PersonalResourceDetailPanel()
        {
            InitializeComponent();
        }
    }
}
