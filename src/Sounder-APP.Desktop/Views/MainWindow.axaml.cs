using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.ComponentModel;

namespace Sounder_APP.Views
{
    public partial class MainWindow : Window
    {
        /// <summary>是否真正退出（托盘菜单退出时设为 true）</summary>
        private bool _isReallyClosing;

        public MainWindow()
        {
            InitializeComponent();
            Closing += OnWindowClosing;
        }

        private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        }

        private void OnMinimizeClick(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            // 点击关闭按钮 → 隐藏到托盘（不退出）
            Hide();
        }

        /// <summary>窗口关闭事件：Alt+F4 或系统关闭时隐藏到托盘</summary>
        private void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            if (!_isReallyClosing)
            {
                e.Cancel = true;
                Hide();
            }
        }

        /// <summary>真正关闭窗口（由托盘菜单调用）</summary>
        public void ReallyClose()
        {
            _isReallyClosing = true;
            Close();
        }

        /// <summary>窗口关闭时清理</summary>
        protected override void OnClosed(EventArgs e)
        {
            Closing -= OnWindowClosing;
            base.OnClosed(e);
        }
    }
}
