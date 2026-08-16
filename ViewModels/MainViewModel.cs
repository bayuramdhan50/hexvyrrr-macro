using System;
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

        // ── Presets ─────────────────────────────────────────────────────────────
        public static readonly int[] DelayPresets  = { 1, 5, 10, 20, 50, 100 }; // ms (1ms = default)
        public static readonly int[] PullPresets   = { 0, 1, 2, 3, 4, 5, 6, 8, 10, 15, 20 }; // px
        public static readonly int[] SmoothPresets = { 1, 2, 3, 4, 5, 8, 10 }; // steps
        public static readonly int[] JitterPresets = { 0, 1, 2, 3 }; // px

        private bool _isEngineActive = true;
        private bool _isOverlayActive = true;
        private bool _isFiring;
        private string _statusMessage = "AUTO-TAP & RECOIL AKTIF — Tahan LMB untuk menembak.";

        // ── Pengaturan Recoil & Delay (Default Original) ────────────────────────
        private int _shotHoldMs         = 1;
        private int _releaseRecoveryMs  = 1;
        private int _verticalPullPixels = 0;
        private int _smoothSteps        = 2;
        private int _jitterRange        = 1;

        // ── HUD Settings Navigation State ──────────────────────────────────────
        private bool _isSettingsVisible = false;
        private int _selectedSettingIndex = 0; // 0: Hold, 1: Release, 2: Pull, 3: Smooth, 4: Jitter

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

        public int ShotHoldMs
        {
            get => _shotHoldMs;
            set
            {
                if (SetField(ref _shotHoldMs, value))
                {
                    _engine.ShotHoldMs = value;
                }
            }
        }

        public int ReleaseRecoveryMs
        {
            get => _releaseRecoveryMs;
            set
            {
                if (SetField(ref _releaseRecoveryMs, value))
                {
                    _engine.ReleaseRecoveryMs = value;
                }
            }
        }

        public int VerticalPullPixels
        {
            get => _verticalPullPixels;
            set
            {
                if (SetField(ref _verticalPullPixels, value))
                {
                    _engine.VerticalPullPixels = value;
                }
            }
        }

        public int SmoothSteps
        {
            get => _smoothSteps;
            set
            {
                if (SetField(ref _smoothSteps, value))
                {
                    _engine.SmoothSteps = value;
                }
            }
        }

        public int JitterRange
        {
            get => _jitterRange;
            set
            {
                if (SetField(ref _jitterRange, value))
                {
                    _engine.JitterRange = value;
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

        // ── Commands ────────────────────────────────────────────────────────────
        public ICommand ToggleSettingsCommand { get; }
        public ICommand SetShotHoldCommand { get; }
        public ICommand SetReleaseRecoveryCommand { get; }
        public ICommand ChangePullCommand { get; }
        public ICommand ChangeSmoothCommand { get; }
        public ICommand ChangeJitterCommand { get; }

        public MainViewModel()
        {
            _engine        = new MouseInputEngine();
            _hotkeyManager = new GlobalHotkeyManager();

            // Inisialisasi commands
            ToggleSettingsCommand = new RelayCommand(_ => ToggleSettingsVisibility());
            SetShotHoldCommand = new RelayCommand(p =>
            {
                if (p != null && int.TryParse(p.ToString(), out int val))
                {
                    ShotHoldMs = val;
                    PlayFeedbackTick(900);
                }
            });
            SetReleaseRecoveryCommand = new RelayCommand(p =>
            {
                if (p != null && int.TryParse(p.ToString(), out int val))
                {
                    ReleaseRecoveryMs = val;
                    PlayFeedbackTick(900);
                }
            });
            ChangePullCommand = new RelayCommand(p =>
            {
                if (p != null && int.TryParse(p.ToString(), out int delta))
                {
                    VerticalPullPixels = Math.Clamp(VerticalPullPixels + delta, 0, 30);
                    PlayFeedbackTick(850);
                }
            });
            ChangeSmoothCommand = new RelayCommand(p =>
            {
                if (p != null && int.TryParse(p.ToString(), out int delta))
                {
                    SmoothSteps = Math.Clamp(SmoothSteps + delta, 1, 10);
                    PlayFeedbackTick(850);
                }
            });
            ChangeJitterCommand = new RelayCommand(p =>
            {
                if (p != null && int.TryParse(p.ToString(), out int delta))
                {
                    JitterRange = Math.Clamp(JitterRange + delta, 0, 5);
                    PlayFeedbackTick(850);
                }
            });

            // Sync state dari engine ke ViewModel
            _engine.OnStateChanged += state =>
            {
                WpfApplication.Current?.Dispatcher.Invoke(() =>
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
                WpfApplication.Current?.Dispatcher.Invoke(() =>
                {
                    IsFiring = firing;
                });
            };

            // F1 — Toggle Engine
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
            _engine.ShotHoldMs         = _shotHoldMs;
            _engine.ReleaseRecoveryMs  = _releaseRecoveryMs;
            _engine.VerticalPullPixels = _verticalPullPixels;
            _engine.SmoothSteps        = _smoothSteps;
            _engine.JitterRange        = _jitterRange;

            _engine.Start();
            _hotkeyManager.Start();
        }

        public void ToggleOverlay()
        {
            IsOverlayActive = !IsOverlayActive;
            RequestOverlayVisibility?.Invoke(IsOverlayActive);
        }

        public void ToggleSettingsVisibility()
        {
            IsSettingsVisible = !IsSettingsVisible;
            PlayFeedbackTick(IsSettingsVisible ? 1100 : 700);
        }

        public void SelectNextSetting()
        {
            SelectedSettingIndex = (SelectedSettingIndex + 1) % 5;
            PlayFeedbackTick(950);
        }

        public void SelectPreviousSetting()
        {
            SelectedSettingIndex = (SelectedSettingIndex + 4) % 5;
            PlayFeedbackTick(950);
        }

        public void IncreaseCurrentSetting()
        {
            switch (SelectedSettingIndex)
            {
                case 0: // Shot Hold
                    ShotHoldMs = StepNext(ShotHoldMs, DelayPresets);
                    break;
                case 1: // Release Recovery
                    ReleaseRecoveryMs = StepNext(ReleaseRecoveryMs, DelayPresets);
                    break;
                case 2: // Vertical Pull
                    VerticalPullPixels = StepNext(VerticalPullPixels, PullPresets);
                    break;
                case 3: // Smooth Steps
                    SmoothSteps = StepNext(SmoothSteps, SmoothPresets);
                    break;
                case 4: // Jitter Range
                    JitterRange = StepNext(JitterRange, JitterPresets);
                    break;
            }
            PlayFeedbackTick(1200);
        }

        public void DecreaseCurrentSetting()
        {
            switch (SelectedSettingIndex)
            {
                case 0: // Shot Hold
                    ShotHoldMs = StepPrevious(ShotHoldMs, DelayPresets);
                    break;
                case 1: // Release Recovery
                    ReleaseRecoveryMs = StepPrevious(ReleaseRecoveryMs, DelayPresets);
                    break;
                case 2: // Vertical Pull
                    VerticalPullPixels = StepPrevious(VerticalPullPixels, PullPresets);
                    break;
                case 3: // Smooth Steps
                    SmoothSteps = StepPrevious(SmoothSteps, SmoothPresets);
                    break;
                case 4: // Jitter Range
                    JitterRange = StepPrevious(JitterRange, JitterPresets);
                    break;
            }
            PlayFeedbackTick(750);
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
