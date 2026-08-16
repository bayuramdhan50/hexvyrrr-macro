using System;
using System.Threading.Tasks;
using System.Windows;
using PbRecoil.Models;
using PbRecoil.Services;
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

            // Dependency Injection & Inisialisasi Service
            IStorageService storageService = new JsonStorageService();
            IPresetService presetService = new PresetService(storageService);
            _viewModel = new MainViewModel(presetService);

            DataContext = _viewModel;

            // Inisialisasi Overlay Window
            _overlayWindow = new OverlayWindow
            {
                DataContext = _viewModel
            };

            // Event Listeners dari ViewModel
            _viewModel.RequestOverlayVisibility += isVisible =>
            {
                if (isVisible)
                {
                    _overlayWindow.Show();
                }
                else
                {
                    _overlayWindow.Hide();
                }
            };

            _viewModel.RequestPresetEditor += ShowPresetEditorDialogAsync;

            Loaded += async (s, e) =>
            {
                await _viewModel.InitializeAsync();
                if (_viewModel.IsOverlayActive)
                {
                    _overlayWindow.Show();
                }
            };

            Closed += (s, e) =>
            {
                _overlayWindow?.Close();
                _viewModel.Dispose();
            };
        }

        private Task<WeaponPreset?> ShowPresetEditorDialogAsync(WeaponPreset preset)
        {
            var dialog = new PresetEditorDialog(preset)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                return Task.FromResult<WeaponPreset?>(dialog.Preset);
            }

            return Task.FromResult<WeaponPreset?>(null);
        }
    }
}
