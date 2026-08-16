using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PbRecoil.Models;

namespace PbRecoil.Services
{
    public class PresetService : IPresetService
    {
        private readonly IStorageService _storageService;
        private readonly List<WeaponPreset> _presets = new();

        public IReadOnlyList<WeaponPreset> Presets => _presets.AsReadOnly();

        public PresetService(IStorageService storageService)
        {
            _storageService = storageService;
        }

        public async Task InitializeAsync()
        {
            _presets.Clear();
            var loaded = await _storageService.LoadPresetsAsync();
            _presets.AddRange(loaded);
        }

        public async Task<WeaponPreset> AddPresetAsync(WeaponPreset preset)
        {
            if (string.IsNullOrWhiteSpace(preset.Id))
            {
                preset.Id = Guid.NewGuid().ToString("N");
            }
            
            _presets.Add(preset);
            await SaveAsync();
            return preset;
        }

        public async Task UpdatePresetAsync(WeaponPreset preset)
        {
            var index = _presets.FindIndex(p => p.Id == preset.Id);
            if (index >= 0)
            {
                _presets[index] = preset;
                await SaveAsync();
            }
        }

        public async Task<bool> DeletePresetAsync(string id)
        {
            var item = _presets.FirstOrDefault(p => p.Id == id);
            if (item != null)
            {
                _presets.Remove(item);
                await SaveAsync();
                return true;
            }
            return false;
        }

        public async Task ResetToDefaultsAsync()
        {
            _presets.Clear();
            var defaults = await _storageService.LoadPresetsAsync();
            _presets.AddRange(defaults);
            await SaveAsync();
        }

        public async Task SaveAsync()
        {
            await _storageService.SavePresetsAsync(_presets);
        }
    }
}
