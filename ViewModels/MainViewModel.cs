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
        public static readonly int[] AugPullPresets = { 0, 1, 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 25, 30, 40, 50 }; // px (3px = default sweet spot)
        public static readonly MacroMode[] AvailableModes = (MacroMode[])Enum.GetValues(typeof(MacroMode));
        public static readonly WeaponCategory[] AvailableWeapons = (WeaponCategory[])Enum.GetValues(typeof(WeaponCategory));
        public static readonly QcLevel[] AvailableQcLevels = (QcLevel[])Enum.GetValues(typeof(QcLevel));

        private bool _isEngineActive = false; // Default OFF saat pertama kali dijalankan
        private bool _isOverlayActive = true;
        private bool _isCrosshairVisible = false;
        private bool _isFiring;
        private string _statusMessage = "ENGINE STANDBY — Tekan [F5] untuk aktifkan.";

        // ── Parameter Mode Senjata & Timing ─────────────────────────────────────
        private MacroMode _selectedMode = MacroMode.AssaultNoRecoil;
        private WeaponCategory _selectedWeapon = WeaponCategory.Assault;
        private QcLevel _selectedQcLevel = QcLevel.Normal;
        private int _holdMs    = 20; // Default 20ms
        private int _releaseMs = 0;  // Default 0ms
        private int _augPullDown = 3; // Default 3 px per shot

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
                    SyncWeaponAndQcFromMode(value);
                    OnPropertyChanged(nameof(ModeName));
                    OnPropertyChanged(nameof(ModeShortBadge));
                    OnPropertyChanged(nameof(ModeDescription));
                    OnPropertyChanged(nameof(IsNoRecoilMode));
                    OnPropertyChanged(nameof(IsAugMode));
                    OnPropertyChanged(nameof(IsAllSniperMode));
                    OnPropertyChanged(nameof(IsKarMode));
                    OnPropertyChanged(nameof(IsSgMode));
                    OnPropertyChanged(nameof(IsSniperOrKarMode));
                    OnPropertyChanged(nameof(IsQcWeapon));
                    OnPropertyChanged(nameof(SelectedWeapon));
                    OnPropertyChanged(nameof(SelectedQcLevel));
                    OnPropertyChanged(nameof(WeaponDisplayLabel));
                    OnPropertyChanged(nameof(QcDisplayLabel));
                    UpdateStatusMessage();
                }
            }
        }

        public WeaponCategory SelectedWeapon
        {
            get => _selectedWeapon;
            set
            {
                if (SetField(ref _selectedWeapon, value))
                {
                    SyncModeFromWeaponAndQc();
                    OnPropertyChanged(nameof(WeaponDisplayLabel));
                    OnPropertyChanged(nameof(IsQcWeapon));
                    ValidateSelectedSettingIndex();
                }
            }
        }

        public QcLevel SelectedQcLevel
        {
            get => _selectedQcLevel;
            set
            {
                if (SetField(ref _selectedQcLevel, value))
                {
                    SyncModeFromWeaponAndQc();
                    OnPropertyChanged(nameof(QcDisplayLabel));
                }
            }
        }

        public bool IsQcWeapon => SelectedWeapon is WeaponCategory.AllSniper or WeaponCategory.Kar98k or WeaponCategory.Shotgun;

        public string WeaponDisplayLabel => SelectedWeapon switch
        {
            WeaponCategory.Assault   => "ASSAULT / SMG",
            WeaponCategory.AugA3     => "AUG A3",
            WeaponCategory.AllSniper => "ALL SNIPER",
            WeaponCategory.Kar98k    => "KAR98K",
            WeaponCategory.Shotgun   => "SHOTGUN",
            _                        => "WEAPON"
        };

        public string QcDisplayLabel => SelectedQcLevel switch
        {
            QcLevel.Normal => "NORMAL (NO QC)",
            QcLevel.Qc50   => "QC 50%",
            QcLevel.Qc75   => "QC 75%",
            _              => "NORMAL"
        };

        public string ModeName => IsQcWeapon
            ? $"{WeaponDisplayLabel} — {QcDisplayLabel}"
            : SelectedMode switch
            {
                MacroMode.AssaultNoRecoil => "NO RECOIL (ASSAULT / SMG)",
                MacroMode.AugA3           => "AUG A3 / HBAR (DYNAMIC RECOIL)",
                _                         => "HEXVYRR MACRO"
            };

        public string ModeShortBadge => SelectedMode switch
        {
            MacroMode.AssaultNoRecoil => "NO RECOIL",
            MacroMode.AugA3           => "AUG A3",
            MacroMode.AllSniperNormal => "SNIPER [NORMAL]",
            MacroMode.AllSniperQc50   => "SNIPER [50%]",
            MacroMode.AllSniperQc75   => "SNIPER [75%]",
            MacroMode.KarNormal       => "KAR [NORMAL]",
            MacroMode.KarQc50         => "KAR [50%]",
            MacroMode.KarQc75         => "KAR [75%]",
            MacroMode.SgNormal        => "SG [NORMAL]",
            MacroMode.SgQc50          => "SG [50%]",
            MacroMode.SgQc75          => "SG [75%]",
            _                         => "MACRO"
        };

        public string ModeDescription => SelectedMode switch
        {
            MacroMode.AssaultNoRecoil => "Auto-Tap ultra presisi untuk senjata Assault Rifle dan SMG.",
            MacroMode.AugA3           => $"AUG A3 Logitech Recoil (Burst 8/5 peluru Y:{AugPullStatusLabel} -> Sustained Spray).",
            MacroMode.AllSniperNormal => "Sniper No QC (Scope 82ms -> Fire 80ms -> 3-1 switch -> Recovery 600ms).",
            MacroMode.AllSniperQc50   => "Sniper QC 50% (Scope 40ms -> Fire 65ms -> 3-1 switch 35ms -> Recovery 350ms).",
            MacroMode.AllSniperQc75   => "Sniper QC 75% Scope + Fire + 3-Q-1 ultra cepat (245ms).",
            MacroMode.KarNormal       => "Kar98k No QC (Scope 82ms -> Fire 80ms -> 3-1 switch -> Recovery 600ms).",
            MacroMode.KarQc50         => "Kar98k QC 50% (Scope 40ms -> Fire 65ms -> 3-1 switch 35ms -> Recovery 350ms).",
            MacroMode.KarQc75         => "Kar98k Scope + Fire + 3-Q-1 ultra cepat bawaan GHUB (300ms).",
            MacroMode.SgNormal        => "Shotgun tembak + switch 3-1 interval standar (750ms).",
            MacroMode.SgQc50          => "Shotgun tembak + switch 3-1 dengan timing QC 50% (480ms).",
            MacroMode.SgQc75          => "Shotgun tembak + switch 3-1 ultra cepat (245ms).",
            _                         => ""
        };

        public bool IsNoRecoilMode   => SelectedMode == MacroMode.AssaultNoRecoil;
        public bool IsAugMode        => SelectedMode == MacroMode.AugA3;
        public bool IsAllSniperMode  => SelectedMode is MacroMode.AllSniperNormal or MacroMode.AllSniperQc50 or MacroMode.AllSniperQc75;
        public bool IsKarMode        => SelectedMode is MacroMode.KarNormal or MacroMode.KarQc50 or MacroMode.KarQc75;
        public bool IsSgMode         => SelectedMode is MacroMode.SgNormal or MacroMode.SgQc50 or MacroMode.SgQc75;
        public bool IsSniperOrKarMode => IsAllSniperMode || IsKarMode;

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

        public int AugPullDown
        {
            get => _augPullDown;
            set
            {
                if (SetField(ref _augPullDown, value))
                {
                    _engine.AugPullDown = value;
                    OnPropertyChanged(nameof(AugPullStatusLabel));
                    if (IsAugMode) OnPropertyChanged(nameof(ModeDescription));
                }
            }
        }

        public string AugPullStatusLabel => AugPullDown <= 0 ? "0 px (Off)" : $"{AugPullDown} px (Smooth)";

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
        public ICommand SelectWeaponCommand { get; }
        public ICommand SelectQcCommand { get; }
        public ICommand SaveConfigCommand { get; }
        public ICommand LoadConfigCommand { get; }
        public ICommand ResetDefaultConfigCommand { get; }
        public ICommand SetHoldMsCommand { get; }
        public ICommand SetReleaseMsCommand { get; }
        public ICommand SetAugPullCommand { get; }
        public ICommand IncreaseAugPullCommand { get; }
        public ICommand DecreaseAugPullCommand { get; }

        public MainViewModel()
        {
            _engine        = new MouseInputEngine();
            _hotkeyManager = new GlobalHotkeyManager();

            ToggleEngineCommand        = new RelayCommand(_ => IsEngineActive = !IsEngineActive);
            ToggleOverlayCommand       = new RelayCommand(_ => ToggleOverlay());
            ToggleSettingsCommand      = new RelayCommand(_ => ToggleSettingsVisibility());
            ToggleCrosshairCommand     = new RelayCommand(_ => IsCrosshairVisible = !IsCrosshairVisible);
            SelectModeCommand          = new RelayCommand(param => SetModeFromParam(param));
            SelectWeaponCommand        = new RelayCommand(param => SetWeaponFromParam(param));
            SelectQcCommand            = new RelayCommand(param => SetQcFromParam(param));
            SaveConfigCommand          = new RelayCommand(_ => SaveConfig());
            LoadConfigCommand          = new RelayCommand(_ => LoadConfig());
            ResetDefaultConfigCommand  = new RelayCommand(_ => ResetDefaultConfig());

            SetHoldMsCommand           = new RelayCommand(p => { if (p != null && int.TryParse(p.ToString(), out int v)) HoldMs = v; });
            SetReleaseMsCommand        = new RelayCommand(p => { if (p != null && int.TryParse(p.ToString(), out int v)) ReleaseMs = v; });
            SetAugPullCommand          = new RelayCommand(p =>
            {
                if (p != null && int.TryParse(p.ToString(), out int v))
                {
                    AugPullDown = v;
                    PlayFeedbackTick(1100);
                }
            });
            IncreaseAugPullCommand     = new RelayCommand(_ =>
            {
                AugPullDown = Math.Min(100, AugPullDown + 1);
                PlayFeedbackTick(1200);
            });
            DecreaseAugPullCommand     = new RelayCommand(_ =>
            {
                AugPullDown = Math.Max(0, AugPullDown - 1);
                PlayFeedbackTick(750);
            });

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

            // F5 — Toggle Engine ON/OFF
            _hotkeyManager.OnToggleEngine += () =>
            {
                WpfApplication.Current?.Dispatcher.Invoke(() =>
                {
                    IsEngineActive = !IsEngineActive;
                });
            };

            // F6 — Toggle HUD Overlay
            _hotkeyManager.OnToggleOverlay += () =>
            {
                WpfApplication.Current?.Dispatcher.Invoke(ToggleOverlay);
            };

            // F7 — Toggle Menu Pengaturan HUD
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

        private void SetWeaponFromParam(object? param)
        {
            if (param is WeaponCategory wep)
            {
                SelectedWeapon = wep;
                PlayFeedbackTick(1100);
            }
            else if (param is string str && Enum.TryParse<WeaponCategory>(str, out var parsedWep))
            {
                SelectedWeapon = parsedWep;
                PlayFeedbackTick(1100);
            }
        }

        private void SetQcFromParam(object? param)
        {
            QcLevel? targetQc = null;
            if (param is QcLevel qc)
            {
                targetQc = qc;
            }
            else if (param is string str && Enum.TryParse<QcLevel>(str, out var parsedQc))
            {
                targetQc = parsedQc;
            }

            if (targetQc.HasValue)
            {
                if (!IsQcWeapon)
                {
                    _selectedWeapon = WeaponCategory.AllSniper;
                    OnPropertyChanged(nameof(SelectedWeapon));
                    OnPropertyChanged(nameof(WeaponDisplayLabel));
                    OnPropertyChanged(nameof(IsQcWeapon));
                }
                SelectedQcLevel = targetQc.Value;
                PlayFeedbackTick(1100);
            }
        }

        private void SyncModeFromWeaponAndQc()
        {
            MacroMode targetMode = _selectedWeapon switch
            {
                WeaponCategory.Assault   => MacroMode.AssaultNoRecoil,
                WeaponCategory.AugA3     => MacroMode.AugA3,
                WeaponCategory.AllSniper => _selectedQcLevel switch
                {
                    QcLevel.Normal => MacroMode.AllSniperNormal,
                    QcLevel.Qc50   => MacroMode.AllSniperQc50,
                    QcLevel.Qc75   => MacroMode.AllSniperQc75,
                    _              => MacroMode.AllSniperNormal
                },
                WeaponCategory.Kar98k => _selectedQcLevel switch
                {
                    QcLevel.Normal => MacroMode.KarNormal,
                    QcLevel.Qc50   => MacroMode.KarQc50,
                    QcLevel.Qc75   => MacroMode.KarQc75,
                    _              => MacroMode.KarNormal
                },
                WeaponCategory.Shotgun => _selectedQcLevel switch
                {
                    QcLevel.Normal => MacroMode.SgNormal,
                    QcLevel.Qc50   => MacroMode.SgQc50,
                    QcLevel.Qc75   => MacroMode.SgQc75,
                    _              => MacroMode.SgNormal
                },
                _ => MacroMode.AssaultNoRecoil
            };

            if (SelectedMode != targetMode)
            {
                SelectedMode = targetMode;
            }
        }

        private void SyncWeaponAndQcFromMode(MacroMode mode)
        {
            var (weapon, qc) = mode switch
            {
                MacroMode.AssaultNoRecoil => (WeaponCategory.Assault, QcLevel.Normal),
                MacroMode.AugA3           => (WeaponCategory.AugA3, QcLevel.Normal),
                MacroMode.AllSniperNormal => (WeaponCategory.AllSniper, QcLevel.Normal),
                MacroMode.AllSniperQc50   => (WeaponCategory.AllSniper, QcLevel.Qc50),
                MacroMode.AllSniperQc75   => (WeaponCategory.AllSniper, QcLevel.Qc75),
                MacroMode.KarNormal       => (WeaponCategory.Kar98k, QcLevel.Normal),
                MacroMode.KarQc50         => (WeaponCategory.Kar98k, QcLevel.Qc50),
                MacroMode.KarQc75         => (WeaponCategory.Kar98k, QcLevel.Qc75),
                MacroMode.SgNormal        => (WeaponCategory.Shotgun, QcLevel.Normal),
                MacroMode.SgQc50          => (WeaponCategory.Shotgun, QcLevel.Qc50),
                MacroMode.SgQc75          => (WeaponCategory.Shotgun, QcLevel.Qc75),
                _                         => (WeaponCategory.Assault, QcLevel.Normal)
            };

            if (_selectedWeapon != weapon)
            {
                _selectedWeapon = weapon;
                OnPropertyChanged(nameof(SelectedWeapon));
                OnPropertyChanged(nameof(WeaponDisplayLabel));
                OnPropertyChanged(nameof(IsQcWeapon));
            }

            if (IsQcWeapon && _selectedQcLevel != qc)
            {
                _selectedQcLevel = qc;
                OnPropertyChanged(nameof(SelectedQcLevel));
                OnPropertyChanged(nameof(QcDisplayLabel));
            }
        }

        private void UpdateStatusMessage()
        {
            string label = IsQcWeapon ? $"{WeaponDisplayLabel} [{QcDisplayLabel}]" : ModeShortBadge;
            StatusMessage = IsEngineActive
                ? $"[{label}] AKTIF — Tahan LMB untuk aksi."
                : "ENGINE STANDBY — Tekan [F5] untuk aktifkan.";
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
                AugPullDown        = AugPullDown,
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
            AugPullDown        = config.AugPullDown;
            IsCrosshairVisible = config.IsCrosshairVisible;
            IsOverlayActive    = config.IsOverlayActive;

            _engine.CurrentMode = config.SelectedMode;
            _engine.HoldMs      = config.HoldMs;
            _engine.ReleaseMs   = config.ReleaseMs;
            _engine.AugPullDown = config.AugPullDown;

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

        private List<int> GetActiveMenuIndices()
        {
            // 0: Weapon
            var list = new List<int> { 0 };

            if (IsQcWeapon)
            {
                // 5: QC Mode (Normal / 50% / 75%)
                list.Add(5);
            }
            else if (IsAugMode)
            {
                // 4: Smooth Pull-Down Y
                list.Add(4);
            }
            else if (IsNoRecoilMode)
            {
                // 1: Hold Time, 2: Release Delay
                list.Add(1);
                list.Add(2);
            }

            // 3: Crosshair Dot
            list.Add(3);
            return list;
        }

        private void ValidateSelectedSettingIndex()
        {
            var activeIndices = GetActiveMenuIndices();
            if (!activeIndices.Contains(SelectedSettingIndex))
            {
                SelectedSettingIndex = activeIndices[0];
            }
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
                    int nextWepIdx = ((int)SelectedWeapon + 1) % AvailableWeapons.Length;
                    SelectedWeapon = AvailableWeapons[nextWepIdx];
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
                case 4:
                    AugPullDown = StepNext(AugPullDown, AugPullPresets);
                    break;
                case 5:
                    int nextQcIdx = ((int)SelectedQcLevel + 1) % AvailableQcLevels.Length;
                    SelectedQcLevel = AvailableQcLevels[nextQcIdx];
                    break;
            }

            int pitch = (SelectedSettingIndex == 3)
                ? (IsCrosshairVisible ? 1200 : 600)
                : 1100;

            PlayFeedbackTick(pitch);
        }

        public void DecreaseCurrentSetting()
        {
            switch (SelectedSettingIndex)
            {
                case 0:
                    int prevWepIdx = ((int)SelectedWeapon - 1 + AvailableWeapons.Length) % AvailableWeapons.Length;
                    SelectedWeapon = AvailableWeapons[prevWepIdx];
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
                case 4:
                    AugPullDown = StepPrevious(AugPullDown, AugPullPresets);
                    break;
                case 5:
                    int prevQcIdx = ((int)SelectedQcLevel - 1 + AvailableQcLevels.Length) % AvailableQcLevels.Length;
                    SelectedQcLevel = AvailableQcLevels[prevQcIdx];
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
            // Win32Api.Beep adalah native kernel32 call — lebih ringan dan tidak blocking Console I/O
            Task.Run(() =>
            {
                try { Win32Api.Beep((uint)pitch, 25); } catch { }
            });
        }

        public void Dispose()
        {
            _engine.Dispose();
            _hotkeyManager.Dispose();
        }
    }
}
