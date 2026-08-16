using System;
using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;
using PbRecoil.ViewModels;

namespace PbRecoil.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private OverlayWindow? _overlayWindow;
        private Forms.NotifyIcon? _notifyIcon;

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

            InitializeSystemTray();

            KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Up)
                {
                    _viewModel.SelectPreviousSetting();
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.Down)
                {
                    _viewModel.SelectNextSetting();
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.Left)
                {
                    _viewModel.DecreaseCurrentSetting();
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.Right)
                {
                    _viewModel.IncreaseCurrentSetting();
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.F3)
                {
                    _viewModel.ToggleSettingsVisibility();
                    e.Handled = true;
                }
            };

            Loaded += (s, e) =>
            {
                _viewModel.Initialize();
                if (_viewModel.IsOverlayActive) _overlayWindow.Show();
            };

            Closed += (s, e) =>
            {
                _overlayWindow?.Close();
                _notifyIcon?.Dispose();
                _viewModel.Dispose();
            };
        }

        private void InitializeSystemTray()
        {
            var contextMenu = new Forms.ContextMenuStrip();

            var openItem = new Forms.ToolStripMenuItem("Buka PB Recoil", null, (s, e) => RestoreFromTray())
            {
                Font = new Font(Forms.Control.DefaultFont, System.Drawing.FontStyle.Bold)
            };
            contextMenu.Items.Add(openItem);

            var toggleEngineItem = new Forms.ToolStripMenuItem("Toggle Engine (F1)", null, (s, e) =>
            {
                _viewModel.IsEngineActive = !_viewModel.IsEngineActive;
            });
            contextMenu.Items.Add(toggleEngineItem);

            var toggleOverlayItem = new Forms.ToolStripMenuItem("Toggle HUD Overlay (F2)", null, (s, e) =>
            {
                _viewModel.ToggleOverlay();
            });
            contextMenu.Items.Add(toggleOverlayItem);

            var toggleSettingsItem = new Forms.ToolStripMenuItem("Toggle Menu Pengaturan (F3)", null, (s, e) =>
            {
                _viewModel.ToggleSettingsVisibility();
            });
            contextMenu.Items.Add(toggleSettingsItem);

            contextMenu.Items.Add(new Forms.ToolStripSeparator());

            var exitItem = new Forms.ToolStripMenuItem("Keluar", null, (s, e) =>
            {
                _notifyIcon?.Dispose();
                _notifyIcon = null;
                System.Windows.Application.Current.Shutdown();
            });
            contextMenu.Items.Add(exitItem);

            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = SystemIcons.Shield,
                Text = "PB Auto-Tap & Anti-Recoil",
                Visible = true,
                ContextMenuStrip = contextMenu
            };

            _notifyIcon.DoubleClick += (s, e) => RestoreFromTray();
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            _notifyIcon?.ShowBalloonTip(
                1200,
                "PB Auto-Tap",
                "Aplikasi diminimalkan ke System Tray. Klik ganda ikon untuk membuka kembali.",
                Forms.ToolTipIcon.Info
            );
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
