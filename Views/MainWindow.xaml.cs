using System;
using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;
using PbRecoil.Core;
using PbRecoil.ViewModels;

namespace PbRecoil.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly UpdateService _updateService;
        private OverlayWindow? _overlayWindow;
        private CrosshairOverlay? _crosshairOverlay;
        private Forms.NotifyIcon? _notifyIcon;

        public MainWindow()
        {
            InitializeComponent();

            _updateService = new UpdateService();
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

            _crosshairOverlay = new CrosshairOverlay();

            _viewModel.RequestCrosshairVisibility += isVisible =>
            {
                if (isVisible) _crosshairOverlay.Show();
                else           _crosshairOverlay.Hide();
            };

            InitializeSystemTray();

            Loaded += (s, e) =>
            {
                _viewModel.Initialize();
                if (_viewModel.IsOverlayActive) _overlayWindow.Show();

                // Pengecekan pembaruan otomatis di background saat aplikasi dibuka
                CheckForUpdates(isManual: false);
            };

            Closed += (s, e) =>
            {
                _crosshairOverlay?.Close();
                _overlayWindow?.Close();
                _notifyIcon?.Dispose();
                _viewModel.Dispose();
            };
        }

        private void InitializeSystemTray()
        {
            var contextMenu = new Forms.ContextMenuStrip();

            var openItem = new Forms.ToolStripMenuItem("Buka Hexvyrr Macro", null, (s, e) => RestoreFromTray())
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

            var toggleCrosshairItem = new Forms.ToolStripMenuItem("Toggle Crosshair Dot", null, (s, e) =>
            {
                _viewModel.IsCrosshairVisible = !_viewModel.IsCrosshairVisible;
            });
            contextMenu.Items.Add(toggleCrosshairItem);

            var checkUpdateItem = new Forms.ToolStripMenuItem("Cek Pembaruan Versi...", null, (s, e) =>
            {
                CheckForUpdates(isManual: true);
            });
            contextMenu.Items.Add(checkUpdateItem);

            contextMenu.Items.Add(new Forms.ToolStripSeparator());

            var exitItem = new Forms.ToolStripMenuItem("Keluar", null, (s, e) =>
            {
                _notifyIcon?.Dispose();
                _notifyIcon = null;
                System.Windows.Application.Current.Shutdown();
            });
            contextMenu.Items.Add(exitItem);

            Icon trayIcon = SystemIcons.Shield;
            try
            {
                var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app_icon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    trayIcon = new Icon(iconPath);
                }
                else
                {
                    var streamInfo = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Resources/app_icon.ico"));
                    if (streamInfo != null)
                    {
                        trayIcon = new Icon(streamInfo.Stream);
                    }
                }
            }
            catch { }

            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = trayIcon,
                Text = "Hexvyrr Macro",
                Visible = true,
                ContextMenuStrip = contextMenu
            };

            _notifyIcon.DoubleClick += (s, e) => RestoreFromTray();
        }

        public async void CheckForUpdates(bool isManual = false)
        {
            try
            {
                var updateInfo = await _updateService.CheckForUpdatesAsync();
                if (updateInfo.IsUpdateAvailable)
                {
                    var dialog = new UpdateDialog(updateInfo, _updateService)
                    {
                        Owner = this
                    };
                    dialog.ShowDialog();
                }
                else if (isManual)
                {
                    System.Windows.MessageBox.Show(
                        $"Aplikasi Anda sudah versi terbaru (v{updateInfo.CurrentVersion}).",
                        "Hexvyrr Macro — Pembaruan",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
            }
            catch (Exception ex)
            {
                if (isManual)
                {
                    System.Windows.MessageBox.Show(
                        $"Gagal memeriksa pembaruan: {ex.Message}",
                        "Hexvyrr Macro — Pembaruan",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }
            }
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
                "Hexvyrr Macro",
                "Aplikasi diminimalkan ke System Tray. Klik ganda ikon untuk membuka kembali.",
                Forms.ToolTipIcon.Info
            );
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        private void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            CheckForUpdates(isManual: true);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
