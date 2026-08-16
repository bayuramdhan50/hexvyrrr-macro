using System.Collections.Generic;
using System.Threading.Tasks;
using PbRecoil.Models;

namespace PbRecoil.Services
{
    public interface IPresetService
    {
        Task InitializeAsync();
        IReadOnlyList<WeaponPreset> Presets { get; }
        Task<WeaponPreset> AddPresetAsync(WeaponPreset preset);
        Task UpdatePresetAsync(WeaponPreset preset);
        Task<bool> DeletePresetAsync(string id);
        Task ResetToDefaultsAsync();
        Task SaveAsync();
    }
}
