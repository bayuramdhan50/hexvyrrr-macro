using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using PbRecoil.Core;
using WpfApplication = System.Windows.Application;

namespace PbRecoil.ViewModels
{
    public class MainViewModel : BaseViewModel, IDisposable
    {
        private readonly MouseInputEngine _engine;
        private readonly GlobalHotkeyManager _hotkeyManager;

        // ── Presets Kalibrasi Timing Hexvyrr Macro ──────────────────────────────
        public static readonly int[] HoldPresets    = { 5, 8, 10, 12, 15, 18, 20, 22, 25, 30, 40, 50 }; // ms (20ms = default)
        public static readonly int[] ReleasePresets = { 0, 1, 2, 4, 6, 8, 10, 12, 15, 20 };            // ms (0ms = default)

        private bool _isEngineActive = true;
        private bool _isOverlayActive = true;
        private bool _isCrosshairVisible = false;
        private bool _isFiring;
        private string _statusMessage = "HEXVYRR MACRO AKTIF — Tahan LMB untuk menembak.";

        // ── Parameter Macro Timing ──────────────────────────────────────────────
        private int _holdMs    = 20; // Default 20ms
        private int _releaseMs = 0;  // Default 0ms

        // ── HUD Settings Navigation State ──────────────────────────────────────
        private bool _isSettingsVisible = false;
        // 0: Hold Time, 1: Release Delay, 2: Crosshair Dot
        private int _selectedSettingIndex = 0;

        public bool IsEngineActive
        {
            get => _isEngineActive;
            set
            {
                if (SetField(ref _isEngineActive, value))
                {
                    _engine.IsEnabled = value;
                    StatusMessage = value
                        ? "HEXVYRR MACRO AKTIF — Tahan LMB untuk menembak."
                        : "ENGINE STANDBY — Tekan [F1] untuk aktifkan.";
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

        public bool IsCrosshairVisible
        {
            get => _isCrosshairVisible;
            set
            {
                if (SetField(ref _isCrosshairVisible, value))
                {
                    RequestCrosshairVisibility?.Invoke(value);
                    OnPropertyChanged(nameof(CrosshairStatusLabel));
                }
            }
        }

        public string CrosshairStatusLabel => _isCrosshairVisible ? "ON" : "OFF";

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

        public int HoldMs
        {
            get => _holdMs;
            set
            {
                if (SetField(ref _holdMs, value))
                {
                    _engine.HoldMs = value;
                }
            }
        }

        public int ReleaseMs
        {
            get => _releaseMs;
            set
            {
                if (SetField(ref _releaseMs, value))
                {
                    _engine.ReleaseMs = value;
                }
            }
        }

        public bool IsSettingsVisible
        {
            get => _isSettingsVisible;
            set
            {
                if (SetField(ref _isSettingsVisible, value))
                {
                    _hotkeyManager.IsSettingsOpen = value;
                }
            }
        }

        public int SelectedSettingIndex
        {
            get => _selectedSettingIndex;
            set => SetField(ref _selectedSettingIndex, value);
        }

        public event Action<bool>? RequestOverlayVisibility;
        public event Action<bool>? RequestCrosshairVisibility;

        // ── Commands ────────────────────────────────────────────────────────────
        public ICommand ToggleEngineCommand { get; }
        public ICommand ToggleOverlayCommand { get; }
        public ICommand ToggleSettingsCommand { get; }
        public ICommand ToggleCrosshairCommand { get; }
        public ICommand SaveConfigCommand { get; }
        public ICommand LoadConfigCommand { get; }
        public ICommand ResetDefaultConfigCommand { get; }

        public MainViewModel()
        {
            _engine        = new MouseInputEngine();
            _hotkeyManager = new GlobalHotkeyManager();

            ToggleEngineCommand        = new RelayCommand(_ => IsEngineActive = !IsEngineActive);
            ToggleOverlayCommand       = new RelayCommand(_ => ToggleOverlay());
            ToggleSettingsCommand      = new RelayCommand(_ => ToggleSettingsVisibility());
            ToggleCrosshairCommand     = new RelayCommand(_ => IsCrosshairVisible = !IsCrosshairVisible);
            SaveConfigCommand          = new RelayCommand(_ => SaveConfig());
            LoadConfigCommand          = new RelayCommand(_ => LoadConfig());
            ResetDefaultConfigCommand  = new RelayCommand(_ => ResetDefaultConfig());

            // Sync state dari engine ke ViewModel
            _engine.OnStateChanged += state =>
            {
                WpfApplication.Current?.Dispatcher.Invoke(() =>
                {
                    _isEngineActive = state;
                    OnPropertyChanged(nameof(IsEngineActive));
                    OnPropertyChanged(nameof(StatusHeader));
                    StatusMessage = state
                        ? "HEXVYRR MACRO AKTIF — Tahan LMB untuk menembak."
                        : "ENGINE STANDBY — Tekan [F1] untuk aktifkan.";
                });
            };

            _engine.OnFiringStateChanged += firing =>
            {
                WpfApplication.Current?.Dispatcher.Invoke(() =>
                {
                    IsFiring = firing;
                });
            };

            // F1 — Toggle Engine ON/OFF
            _hotkeyManager.OnToggleEngine += () =>
            {
                WpfApplication.Current?.Dispatcher.Invoke(() =>
                {
                    IsEngineActive = !IsEngineActive;
                });
            };

            // F2 — Toggle HUD Overlay
            _hotkeyManager.OnToggleOverlay += () =>
            {
                WpfApplication.Current?.Dispatcher.Invoke(ToggleOverlay);
            };

            // F3 — Toggle Menu Pengaturan HUD
            _hotkeyManager.OnToggleSettings += () =>
            {
                WpfApplication.Current?.Dispatcher.Invoke(ToggleSettingsVisibility);
            };

            // Tombol Panah — Navigasi Item (Up / Down)
            _hotkeyManager.OnNavigateUp += () =>
            {
                WpfApplication.Current?.Dispatcher.Invoke(SelectPreviousSetting);
            };

            _hotkeyManager.OnNavigateDown += () =>
            {
                WpfApplication.Current?.Dispatcher.Invoke(SelectNextSetting);
            };

            // Tombol Panah — Ubah Nilai (Left / Right)
            _hotkeyManager.OnValueLeft += () =>
            {
                WpfApplication.Current?.Dispatcher.Invoke(DecreaseCurrentSetting);
            };

            _hotkeyManager.OnValueRight += () =>
            {
                WpfApplication.Current?.Dispatcher.Invoke(IncreaseCurrentSetting);
            };
        }

        public void Initialize()
        {
            // Auto load saved config jika ada
            var savedConfig = ConfigService.LoadConfig();
            ApplyConfigValues(savedConfig);

            _engine.Start();
            _hotkeyManager.Start();
        }

        public void SaveConfig()
        {
            var config = new AppConfig
            {
                HoldMs             = HoldMs,
                ReleaseMs          = ReleaseMs,
                IsCrosshairVisible = IsCrosshairVisible,
                IsOverlayActive    = IsOverlayActive
            };

            bool success = ConfigService.SaveConfig(config);
            StatusMessage = success
                ? "✓ KONFIGURASI TERSIMPAN (hexvyrr_config.json)"
                : "✗ GAGAL MENYIMPAN KONFIGURASI";

            PlayFeedbackTick(success ? 1300 : 400);
        }

        public void LoadConfig()
        {
            var config = ConfigService.LoadConfig();
            ApplyConfigValues(config);
            StatusMessage = "✓ KONFIGURASI BERHASIL DIMUAT!";
            PlayFeedbackTick(1100);
        }

        public void ResetDefaultConfig()
        {
            var defaultConfig = ConfigService.GetDefaultConfig();
            ApplyConfigValues(defaultConfig);
            StatusMessage = "↺ KONFIGURASI DI-RESET KE DEFAULT (20ms/0ms)";
            PlayFeedbackTick(900);
        }

        private void ApplyConfigValues(AppConfig config)
        {
            HoldMs             = config.HoldMs;
            ReleaseMs          = config.ReleaseMs;
            IsCrosshairVisible = config.IsCrosshairVisible;
            IsOverlayActive    = config.IsOverlayActive;

            _engine.HoldMs     = config.HoldMs;
            _engine.ReleaseMs  = config.ReleaseMs;

            RequestCrosshairVisibility?.Invoke(IsCrosshairVisible);
            RequestOverlayVisibility?.Invoke(IsOverlayActive);
        }

        public void ToggleOverlay()
        {
            IsOverlayActive = !IsOverlayActive;
            RequestOverlayVisibility?.Invoke(IsOverlayActive);
            PlayFeedbackTick(IsOverlayActive ? 1000 : 500);
        }

        public void ToggleSettingsVisibility()
        {
            IsSettingsVisible = !IsSettingsVisible;
            PlayFeedbackTick(IsSettingsVisible ? 1100 : 700);
        }

        private static List<int> GetActiveMenuIndices()
        {
            // 0: Hold Time, 1: Release Delay, 2: Crosshair
            return new List<int> { 0, 1, 2 };
        }

        public void SelectNextSetting()
        {
            var activeIndices = GetActiveMenuIndices();
            int currentPos = activeIndices.IndexOf(SelectedSettingIndex);

            if (currentPos < 0)
            {
                SelectedSettingIndex = activeIndices[0];
            }
            else
            {
                int nextPos = (currentPos + 1) % activeIndices.Count;
                SelectedSettingIndex = activeIndices[nextPos];
            }

            PlayFeedbackTick(950);
        }

        public void SelectPreviousSetting()
        {
            var activeIndices = GetActiveMenuIndices();
            int currentPos = activeIndices.IndexOf(SelectedSettingIndex);

            if (currentPos < 0)
            {
                SelectedSettingIndex = activeIndices[0];
            }
            else
            {
                int prevPos = (currentPos + activeIndices.Count - 1) % activeIndices.Count;
                SelectedSettingIndex = activeIndices[prevPos];
            }

            PlayFeedbackTick(950);
        }

        public void IncreaseCurrentSetting()
        {
            switch (SelectedSettingIndex)
            {
                case 0: HoldMs             = StepNext(HoldMs, HoldPresets); break;
                case 1: ReleaseMs          = StepNext(ReleaseMs, ReleasePresets); break;
                case 2: IsCrosshairVisible = !IsCrosshairVisible; break;
            }

            int pitch = (SelectedSettingIndex == 2)
                ? (IsCrosshairVisible ? 1200 : 600)
                : 1200;

            PlayFeedbackTick(pitch);
        }

        public void DecreaseCurrentSetting()
        {
            switch (SelectedSettingIndex)
            {
                case 0: HoldMs             = StepPrevious(HoldMs, HoldPresets); break;
                case 1: ReleaseMs          = StepPrevious(ReleaseMs, ReleasePresets); break;
                case 2: IsCrosshairVisible = !IsCrosshairVisible; break;
            }

            int pitch = (SelectedSettingIndex == 2)
                ? (IsCrosshairVisible ? 1200 : 600)
                : 750;

            PlayFeedbackTick(pitch);
        }

        private static int StepNext(int current, int[] presets)
        {
            int idx = Array.IndexOf(presets, current);
            if (idx >= 0)
            {
                return presets[Math.Min(presets.Length - 1, idx + 1)];
            }
            for (int i = 0; i < presets.Length; i++)
            {
                if (presets[i] > current) return presets[i];
            }
            return presets[^1];
        }

        private static int StepPrevious(int current, int[] presets)
        {
            int idx = Array.IndexOf(presets, current);
            if (idx >= 0)
            {
                return presets[Math.Max(0, idx - 1)];
            }
            for (int i = presets.Length - 1; i >= 0; i--)
            {
                if (presets[i] < current) return presets[i];
            }
            return presets[0];
        }

        private static void PlayFeedbackTick(int pitch)
        {
            Task.Run(() =>
            {
                try
                {
                    Console.Beep(pitch, 25);
                }
                catch { }
            });
        }

        public void Dispose()
        {
            _engine.Dispose();
            _hotkeyManager.Dispose();
        }
    }
}
