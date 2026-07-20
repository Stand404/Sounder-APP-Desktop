using Avalonia;
using Avalonia.Controls;

namespace Sounder_APP.Views
{
    public partial class ResourceDetailLayout : UserControl
    {
        public static readonly StyledProperty<Control?> LeftContentProperty =
            AvaloniaProperty.Register<ResourceDetailLayout, Control?>(nameof(LeftContent));

        public Control? LeftContent
        {
            get => GetValue(LeftContentProperty);
            set => SetValue(LeftContentProperty, value);
        }

        public static readonly StyledProperty<Control?> RightContentProperty =
            AvaloniaProperty.Register<ResourceDetailLayout, Control?>(nameof(RightContent));

        public Control? RightContent
        {
            get => GetValue(RightContentProperty);
            set => SetValue(RightContentProperty, value);
        }

        public ResourceDetailLayout()
        {
            InitializeComponent();

            // 初始同步属性值到对应 ContentControl
            SyncContent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == LeftContentProperty)
                LeftContainer.Content = change.NewValue as Control;
            else if (change.Property == RightContentProperty)
                RightContainer.Content = change.NewValue as Control;
        }

        private void SyncContent()
        {
            LeftContainer.Content = LeftContent;
            RightContainer.Content = RightContent;
        }
    }
}
