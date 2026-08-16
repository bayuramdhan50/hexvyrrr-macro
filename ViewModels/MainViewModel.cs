using System;
using System.Windows;
using PbRecoil.Core;

namespace PbRecoil.ViewModels
{
    public class MainViewModel : BaseViewModel, IDisposable
    {
        private readonly MouseInputEngine _engine;
        private readonly GlobalHotkeyManager _hotkeyManager;

        private bool _isEngineActive = true; // Default langsung ON saat aplikasi dibuka
        private bool _isOverlayActive = true;
        private bool _isFiring;
        private string _statusMessage = "AUTO-TAP & RECOIL AKTIF — Tahan LMB untuk menembak.";

        public bool IsEngineActive
        {
            get => _isEngineActive;
            set
            {
                if (SetField(ref _isEngineActive, value))
                {
                    _engine.IsEnabled = value;
                    StatusMessage = value
                        ? "AUTO-TAP & RECOIL AKTIF — Tahan LMB untuk menembak."
                        : "ENGINE NONAKTIF — Tekan [F1] untuk aktifkan.";
                    OnPropertyChanged(nameof(StatusHeader));
                }
            }
        }

        public string StatusHeader => IsEngineActive ? "ON" : "OFF";

        public bool IsOverlayActive
        {
            get => _isOverlayActive;
            set => SetField(ref _isOverlayActive, value);
        }

        public bool IsFiring
        {
            get => _isFiring;
            set => SetField(ref _isFiring, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        public event Action<bool>? RequestOverlayVisibility;

        public MainViewModel()
        {
            _engine        = new MouseInputEngine();
            _hotkeyManager = new GlobalHotkeyManager();

            // Sync state dari engine ke ViewModel
            _engine.OnStateChanged += state =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _isEngineActive = state;
                    OnPropertyChanged(nameof(IsEngineActive));
                    OnPropertyChanged(nameof(StatusHeader));
                    StatusMessage = state
                        ? "AUTO-TAP & RECOIL AKTIF — Tahan LMB untuk menembak."
                        : "ENGINE NONAKTIF — Tekan [F1] untuk aktifkan.";
                });
            };

            _engine.OnFiringStateChanged += firing =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsFiring = firing;
                });
            };

            // F1 — Toggle Engine
            _hotkeyManager.OnToggleEngine += () =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsEngineActive = !IsEngineActive;
                });
            };

            // F2 — Toggle HUD Overlay
            _hotkeyManager.OnToggleOverlay += () =>
            {
                Application.Current.Dispatcher.Invoke(ToggleOverlay);
            };
        }

        public void Initialize()
        {
            _engine.Start();
            _hotkeyManager.Start();
        }

        public void ToggleOverlay()
        {
            IsOverlayActive = !IsOverlayActive;
            RequestOverlayVisibility?.Invoke(IsOverlayActive);
        }

        public void Dispose()
        {
            _engine.Dispose();
            _hotkeyManager.Dispose();
        }
    }
}
