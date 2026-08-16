using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PbRecoil.Core;
using PbRecoil.Models;
using PbRecoil.Services;

namespace PbRecoil.ViewModels
{
    public class MainViewModel : BaseViewModel, IDisposable
    {
        private readonly IPresetService _presetService;
        private readonly MouseInputEngine _engine;
        private readonly GlobalHotkeyManager _hotkeyManager;

        private WeaponPreset? _selectedPreset;
        private WeaponCategory? _selectedCategoryFilter;
        private string _searchQuery = string.Empty;
        private bool _isEngineActive;
        private bool _isOverlayActive = true;
        private string _statusMessage = "Siap digunakan. Tekan [F6] untuk Toggle Recoil.";

        public ObservableCollection<WeaponPreset> FilteredPresets { get; } = new();
        public ObservableCollection<WeaponCategoryItem> CategoryFilterList { get; } = new();

        public WeaponPreset? SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (SetField(ref _selectedPreset, value))
                {
                    _engine.ActivePreset = value;
                    OnPropertyChanged(nameof(HasSelectedPreset));
                    OnPropertyChanged(nameof(CanDeleteSelected));
                }
            }
        }

        public bool HasSelectedPreset => SelectedPreset != null;
        public bool CanDeleteSelected => SelectedPreset != null && !SelectedPreset.IsDefault;

        public WeaponCategory? SelectedCategoryFilter
        {
            get => _selectedCategoryFilter;
            set
            {
                if (SetField(ref _selectedCategoryFilter, value))
                {
                    ApplyFilter();
                }
            }
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetField(ref _searchQuery, value))
                {
                    ApplyFilter();
                }
            }
        }

        public bool IsEngineActive
        {
            get => _isEngineActive;
            set
            {
                if (SetField(ref _isEngineActive, value))
                {
                    _engine.IsEnabled = value;
                    StatusMessage = value
                        ? $"RECOIL AKTIF [ON] - Preset: {SelectedPreset?.Name ?? "Tidak Ada"}"
                        : "RECOIL NON-AKTIF [OFF] - Tekan [F6] untuk mengaktifkan.";
                    OnPropertyChanged(nameof(StatusHeader));
                }
            }
        }

        public string StatusHeader => IsEngineActive ? "AKTIF" : "STANDBY";

        public bool IsOverlayActive
        {
            get => _isOverlayActive;
            set => SetField(ref _isOverlayActive, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        public ICommand ToggleEngineCommand { get; }
        public ICommand ToggleOverlayCommand { get; }
        public ICommand AddPresetCommand { get; }
        public ICommand DeletePresetCommand { get; }
        public ICommand SaveChangesCommand { get; }
        public ICommand ResetDefaultsCommand { get; }
        public ICommand CycleNextPresetCommand { get; }

        public event Action<bool>? RequestOverlayVisibility;
        public event Func<WeaponPreset, Task<WeaponPreset?>>? RequestPresetEditor;

        public MainViewModel(IPresetService presetService)
        {
            _presetService = presetService;
            _engine = new MouseInputEngine();
            _hotkeyManager = new GlobalHotkeyManager();

            // Binding Commands
            ToggleEngineCommand = new RelayCommand(() => IsEngineActive = !IsEngineActive);
            ToggleOverlayCommand = new RelayCommand(ToggleOverlay);
            AddPresetCommand = new RelayCommand(async () => await CreateNewPresetAsync());
            DeletePresetCommand = new RelayCommand(async () => await DeleteCurrentPresetAsync(), () => CanDeleteSelected);
            SaveChangesCommand = new RelayCommand(async () => await SaveAllPresetsAsync());
            ResetDefaultsCommand = new RelayCommand(async () => await ResetToDefaultsAsync());
            CycleNextPresetCommand = new RelayCommand(CycleNextPreset);

            // Inisialisasi Kategori Filter
            PopulateCategories();

            // Event Listeners Engine & Hotkey
            _engine.OnStateChanged += state =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _isEngineActive = state;
                    OnPropertyChanged(nameof(IsEngineActive));
                    OnPropertyChanged(nameof(StatusHeader));
                    StatusMessage = state
                        ? $"RECOIL AKTIF [ON] - Preset: {SelectedPreset?.Name ?? "None"}"
                        : "RECOIL NON-AKTIF [OFF] - Tekan [F6] untuk mengaktifkan.";
                });
            };

            _hotkeyManager.OnToggleEngine += () =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsEngineActive = !IsEngineActive;
                });
            };

            _hotkeyManager.OnCyclePreset += () =>
            {
                Application.Current.Dispatcher.Invoke(CycleNextPreset);
            };

            _hotkeyManager.OnToggleOverlay += () =>
            {
                Application.Current.Dispatcher.Invoke(ToggleOverlay);
            };
        }

        public async Task InitializeAsync()
        {
            await _presetService.InitializeAsync();
            ApplyFilter();

            if (FilteredPresets.Count > 0)
            {
                SelectedPreset = FilteredPresets.First();
            }

            _engine.Start();
            _hotkeyManager.Start();
        }

        private void PopulateCategories()
        {
            CategoryFilterList.Clear();
            CategoryFilterList.Add(new WeaponCategoryItem { Category = null, DisplayName = "Semua Senjata" });
            
            foreach (WeaponCategory cat in Enum.GetValues(typeof(WeaponCategory)))
            {
                CategoryFilterList.Add(new WeaponCategoryItem
                {
                    Category = cat,
                    DisplayName = cat.ToDisplayString()
                });
            }

            _selectedCategoryFilter = null;
        }

        public void ApplyFilter()
        {
            var query = _presetService.Presets.AsEnumerable();

            if (_selectedCategoryFilter.HasValue)
            {
                query = query.Where(p => p.Category == _selectedCategoryFilter.Value);
            }

            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                query = query.Where(p => p.Name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                                         p.Description.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase));
            }

            var list = query.ToList();
            FilteredPresets.Clear();
            foreach (var item in list)
            {
                FilteredPresets.Add(item);
            }

            if (SelectedPreset == null || !FilteredPresets.Contains(SelectedPreset))
            {
                SelectedPreset = FilteredPresets.FirstOrDefault();
            }
        }

        private async Task CreateNewPresetAsync()
        {
            var newPreset = new WeaponPreset
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = "Custom Weapon " + (FilteredPresets.Count + 1),
                Category = SelectedCategoryFilter ?? WeaponCategory.Custom,
                VerticalRecoil = 5,
                HorizontalRecoil = 0,
                DelayMs = 12,
                SmoothStep = 2,
                Jitter = 1,
                ScopeOnly = false,
                Description = "Custom recoil profile.",
                IsDefault = false
            };

            if (RequestPresetEditor != null)
            {
                var result = await RequestPresetEditor(newPreset);
                if (result != null)
                {
                    await _presetService.AddPresetAsync(result);
                    ApplyFilter();
                    SelectedPreset = FilteredPresets.FirstOrDefault(p => p.Id == result.Id);
                    StatusMessage = $"Preset '{result.Name}' berhasil dibuat dan disimpan.";
                }
            }
            else
            {
                await _presetService.AddPresetAsync(newPreset);
                ApplyFilter();
                SelectedPreset = FilteredPresets.FirstOrDefault(p => p.Id == newPreset.Id);
                StatusMessage = $"Preset baru '{newPreset.Name}' berhasil ditambahkan.";
            }
        }

        private async Task DeleteCurrentPresetAsync()
        {
            if (SelectedPreset == null || SelectedPreset.IsDefault) return;

            var confirm = MessageBox.Show(
                $"Apakah Anda yakin ingin menghapus preset '{SelectedPreset.Name}'?",
                "Konfirmasi Hapus Preset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                var name = SelectedPreset.Name;
                await _presetService.DeletePresetAsync(SelectedPreset.Id);
                ApplyFilter();
                StatusMessage = $"Preset '{name}' berhasil dihapus.";
            }
        }

        public async Task SaveAllPresetsAsync()
        {
            await _presetService.SaveAsync();
            StatusMessage = "Seluruh perubahan preset berhasil disimpan ke presets.json.";
            MessageBox.Show("Pengaturan dan preset berhasil disimpan!", "Sukses Simpan", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task ResetToDefaultsAsync()
        {
            var confirm = MessageBox.Show(
                "Kembalikan seluruh preset ke pengaturan bawaan Point Blank?",
                "Reset Default",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                await _presetService.ResetToDefaultsAsync();
                ApplyFilter();
                StatusMessage = "Preset berhasil di-reset ke nilai default PB.";
            }
        }

        public void CycleNextPreset()
        {
            if (FilteredPresets.Count <= 1) return;

            var currentIndex = SelectedPreset != null ? FilteredPresets.IndexOf(SelectedPreset) : -1;
            var nextIndex = (currentIndex + 1) % FilteredPresets.Count;
            SelectedPreset = FilteredPresets[nextIndex];
            StatusMessage = $"Beralih ke preset: [{SelectedPreset.Name}] ({SelectedPreset.Category.ToDisplayString()})";
        }

        private void ToggleOverlay()
        {
            IsOverlayActive = !IsOverlayActive;
            RequestOverlayVisibility?.Invoke(IsOverlayActive);
            StatusMessage = IsOverlayActive ? "In-Game HUD Overlay: DITAMPILKAN" : "In-Game HUD Overlay: DISEMBUNYIKAN";
        }

        public void Dispose()
        {
            _engine.Dispose();
            _hotkeyManager.Dispose();
        }
    }

    public class WeaponCategoryItem
    {
        public WeaponCategory? Category { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}
