using System;
using System.Windows;
using PbRecoil.ViewModels;

namespace PbRecoil.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private OverlayWindow? _overlayWindow;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            _overlayWindow = new OverlayWindow
            {
                DataContext = _viewModel
            };

            _viewModel.RequestOverlayVisibility += isVisible =>
            {
                if (isVisible) _overlayWindow.Show();
                else           _overlayWindow.Hide();
            };

            Loaded += (s, e) =>
            {
                _viewModel.Initialize();
                if (_viewModel.IsOverlayActive) _overlayWindow.Show();
            };

            Closed += (s, e) =>
            {
                _overlayWindow?.Close();
                _viewModel.Dispose();
            };
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
