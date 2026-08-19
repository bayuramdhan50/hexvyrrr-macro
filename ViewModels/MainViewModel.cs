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
        public static readonly MacroMode[] AvailableModes = (MacroMode[])Enum.GetValues(typeof(MacroMode));

        private bool _isEngineActive = true;
        private bool _isOverlayActive = true;
        private bool _isCrosshairVisible = false;
        private bool _isFiring;
        private string _statusMessage = "HEXVYRR MACRO AKTIF — Tahan LMB untuk menembak.";

        // ── Parameter Mode Senjata & Timing ─────────────────────────────────────
        private MacroMode _selectedMode = MacroMode.AssaultNoRecoil;
        private int _holdMs    = 20; // Default 20ms
        private int _releaseMs = 0;  // Default 0ms

        // ── HUD Settings Navigation State ──────────────────────────────────────
        private bool _isSettingsVisible = false;
        // 0: Mode, 1: Hold Time, 2: Release Delay, 3: Crosshair Dot
        private int _selectedSettingIndex = 0;

        public bool IsEngineActive
        {
            get => _isEngineActive;
            set
            {
                if (SetField(ref _isEngineActive, value))
                {
                    _engine.IsEnabled = value;
                    UpdateStatusMessage();
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

        public MacroMode SelectedMode
        {
            get => _selectedMode;
            set
            {
                if (SetField(ref _selectedMode, value))
                {
                    _engine.CurrentMode = value;
                    OnPropertyChanged(nameof(ModeName));
                    OnPropertyChanged(nameof(ModeShortBadge));
                    OnPropertyChanged(nameof(ModeDescription));
                    OnPropertyChanged(nameof(IsAssaultMode));
                    OnPropertyChanged(nameof(IsAwpMode));
                    OnPropertyChanged(nameof(IsSgMode));
                    UpdateStatusMessage();
                }
            }
        }

        public string ModeName => SelectedMode switch
        {
            MacroMode.AssaultNoRecoil => "ASSAULT / SMG (NO RECOIL)",
            MacroMode.AwpNormal       => "AWP TAHAN (NO QC — 890ms)",
            MacroMode.AwpQc50         => "AWP TAHAN (QC 50% — 590ms)",
            MacroMode.AwpQc75         => "AWP TAHAN (QC 75% — 300ms)",
            MacroMode.SgNormal        => "SG TAHAN (NO QC — 750ms)",
            MacroMode.SgQc50          => "SG TAHAN (QC 50% — 480ms)",
            MacroMode.SgQc75          => "SG TAHAN (QC 75% — 245ms)",
            _                         => "HEXVYRR MACRO"
        };

        public string ModeShortBadge => SelectedMode switch
        {
            MacroMode.AssaultNoRecoil => "AR RECOIL",
            MacroMode.AwpNormal       => "AWP NORMAL",
            MacroMode.AwpQc50         => "AWP QC 50%",
            MacroMode.AwpQc75         => "AWP QC 75%",
            MacroMode.SgNormal        => "SG NORMAL",
            MacroMode.SgQc50          => "SG QC 50%",
            MacroMode.SgQc75          => "SG QC 75%",
            _                         => "MACRO"
        };

        public string ModeDescription => SelectedMode switch
        {
            MacroMode.AssaultNoRecoil => "Auto-Tap ultra presisi untuk senjata Assault Rifle dan SMG.",
            MacroMode.AwpNormal       => "Sniper AWP scope + tembak + switch 3-Q-1 otomatis tanpa QC delay.",
            MacroMode.AwpQc50         => "Sniper AWP scope + tembak + switch 3-Q-1 dengan timing QC 50%.",
            MacroMode.AwpQc75         => "Sniper AWP scope + tembak + switch 3-Q-1 ultra cepat (QC 75%).",
            MacroMode.SgNormal        => "Shotgun tembak + switch 3-1 otomatis interval standar.",
            MacroMode.SgQc50          => "Shotgun tembak + switch 3-1 otomatis dengan timing QC 50%.",
            MacroMode.SgQc75          => "Shotgun tembak + switch 3-1 ultra cepat (QC 75%).",
            _                         => ""
        };

        public bool IsAssaultMode => SelectedMode == MacroMode.AssaultNoRecoil;
        public bool IsAwpMode     => SelectedMode is MacroMode.AwpNormal or MacroMode.AwpQc50 or MacroMode.AwpQc75;
        public bool IsSgMode      => SelectedMode is MacroMode.SgNormal or MacroMode.SgQc50 or MacroMode.SgQc75;

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
        public ICommand SelectModeCommand { get; }
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
            SelectModeCommand          = new RelayCommand(param => SetModeFromParam(param));
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
                    UpdateStatusMessage();
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

        private void SetModeFromParam(object? param)
        {
            if (param is MacroMode mode)
            {
                SelectedMode = mode;
                PlayFeedbackTick(1100);
            }
            else if (param is string str && Enum.TryParse<MacroMode>(str, out var parsedMode))
            {
                SelectedMode = parsedMode;
                PlayFeedbackTick(1100);
            }
        }

        private void UpdateStatusMessage()
        {
            StatusMessage = IsEngineActive
                ? $"[{ModeShortBadge}] AKTIF — Tahan LMB untuk aksi."
                : "ENGINE STANDBY — Tekan [F1] untuk aktifkan.";
        }

        public void Initialize()
        {
            var savedConfig = ConfigService.LoadConfig();
            ApplyConfigValues(savedConfig);

            _engine.Start();
            _hotkeyManager.Start();
        }

        public void SaveConfig()
        {
            var config = new AppConfig
            {
                SelectedMode       = SelectedMode,
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
            StatusMessage = "↺ KONFIGURASI DI-RESET KE DEFAULT";
            PlayFeedbackTick(900);
        }

        private void ApplyConfigValues(AppConfig config)
        {
            SelectedMode       = config.SelectedMode;
            HoldMs             = config.HoldMs;
            ReleaseMs          = config.ReleaseMs;
            IsCrosshairVisible = config.IsCrosshairVisible;
            IsOverlayActive    = config.IsOverlayActive;

            _engine.CurrentMode = config.SelectedMode;
            _engine.HoldMs      = config.HoldMs;
            _engine.ReleaseMs   = config.ReleaseMs;

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
            // 0: Mode Senjata, 1: Hold Time, 2: Release Delay, 3: Crosshair
            return new List<int> { 0, 1, 2, 3 };
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
                case 0:
                    int nextModeIdx = ((int)SelectedMode + 1) % AvailableModes.Length;
                    SelectedMode = AvailableModes[nextModeIdx];
                    break;
                case 1:
                    HoldMs = StepNext(HoldMs, HoldPresets);
                    break;
                case 2:
                    ReleaseMs = StepNext(ReleaseMs, ReleasePresets);
                    break;
                case 3:
                    IsCrosshairVisible = !IsCrosshairVisible;
                    break;
            }

            int pitch = (SelectedSettingIndex == 3)
                ? (IsCrosshairVisible ? 1200 : 600)
                : 1200;

            PlayFeedbackTick(pitch);
        }

        public void DecreaseCurrentSetting()
        {
            switch (SelectedSettingIndex)
            {
                case 0:
                    int prevModeIdx = ((int)SelectedMode - 1 + AvailableModes.Length) % AvailableModes.Length;
                    SelectedMode = AvailableModes[prevModeIdx];
                    break;
                case 1:
                    HoldMs = StepPrevious(HoldMs, HoldPresets);
                    break;
                case 2:
                    ReleaseMs = StepPrevious(ReleaseMs, ReleasePresets);
                    break;
                case 3:
                    IsCrosshairVisible = !IsCrosshairVisible;
                    break;
            }

            int pitch = (SelectedSettingIndex == 3)
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
