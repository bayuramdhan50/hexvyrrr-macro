using System.Collections.Generic;
using System.Threading.Tasks;
using PbRecoil.Models;

namespace PbRecoil.Services
{
    public interface IStorageService
    {
        Task<List<WeaponPreset>> LoadPresetsAsync();
        Task SavePresetsAsync(IEnumerable<WeaponPreset> presets);
        string GetStorageFilePath();
    }
}
