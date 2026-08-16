using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using PbRecoil.Models;

namespace PbRecoil.Services
{
    public class JsonStorageService : IStorageService
    {
        private readonly string _filePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public JsonStorageService()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            _filePath = Path.Combine(appDir, "presets.json");
            
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
        }

        public string GetStorageFilePath() => _filePath;

        public async Task<List<WeaponPreset>> LoadPresetsAsync()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    using var stream = File.OpenRead(_filePath);
                    var list = await JsonSerializer.DeserializeAsync<List<WeaponPreset>>(stream, _jsonOptions);
                    if (list != null && list.Count > 0)
                    {
                        return list;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JsonStorageService] Gagal membaca preset: {ex.Message}");
            }

            // Kembalikan preset default Point Blank jika file belum ada atau kosong
            var defaults = GetDefaultPointBlankPresets();
            await SavePresetsAsync(defaults);
            return defaults;
        }

        public async Task SavePresetsAsync(IEnumerable<WeaponPreset> presets)
        {
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var stream = File.Create(_filePath);
                await JsonSerializer.SerializeAsync(stream, presets, _jsonOptions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JsonStorageService] Gagal menyimpan preset: {ex.Message}");
                throw;
            }
        }

        private static List<WeaponPreset> GetDefaultPointBlankPresets()
        {
            return new List<WeaponPreset>
            {
                // ASSAULT RIFLE
                new()
                {
                    Id = "ar-aug-a3",
                    Name = "AUG A3",
                    Category = WeaponCategory.AssaultRifle,
                    VerticalRecoil = 5,
                    HorizontalRecoil = 1,
                    DelayMs = 12,
                    SmoothStep = 2,
                    Jitter = 1,
                    ScopeOnly = false,
                    Description = "Assault Rifle standar kompetisi PB. Tarikan vertikal kuat dengan sedikit deviasi kanan.",
                    IsDefault = true
                },
                new()
                {
                    Id = "ar-sc-2010",
                    Name = "SC-2010",
                    Category = WeaponCategory.AssaultRifle,
                    VerticalRecoil = 4,
                    HorizontalRecoil = 0,
                    DelayMs = 14,
                    SmoothStep = 2,
                    Jitter = 1,
                    ScopeOnly = false,
                    Description = "Akurasi tinggi dan damage stabil. Tarikan vertikal sedang.",
                    IsDefault = true
                },
                new()
                {
                    Id = "ar-m4a1",
                    Name = "M4A1 Ext.",
                    Category = WeaponCategory.AssaultRifle,
                    VerticalRecoil = 4,
                    HorizontalRecoil = -1,
                    DelayMs = 13,
                    SmoothStep = 2,
                    Jitter = 1,
                    ScopeOnly = false,
                    Description = "Recoil seimbang, tarikan konsisten untuk pertempuran jarak menengah.",
                    IsDefault = true
                },

                // SUBMACHINE GUN
                new()
                {
                    Id = "smg-kriss-sv",
                    Name = "Kriss S.V / Dual",
                    Category = WeaponCategory.SubMachineGun,
                    VerticalRecoil = 6,
                    HorizontalRecoil = 0,
                    DelayMs = 9,
                    SmoothStep = 3,
                    Jitter = 2,
                    ScopeOnly = false,
                    Description = "Fire rate sangat tinggi. Membutuhkan tarikan vertikal cepat beruntun.",
                    IsDefault = true
                },
                new()
                {
                    Id = "smg-oa-93",
                    Name = "OA-93 Dual",
                    Category = WeaponCategory.SubMachineGun,
                    VerticalRecoil = 8,
                    HorizontalRecoil = 0,
                    DelayMs = 7,
                    SmoothStep = 3,
                    Jitter = 2,
                    ScopeOnly = false,
                    Description = "Semburan peluru instan. Tarikan awal vertikal sangat agresif.",
                    IsDefault = true
                },
                new()
                {
                    Id = "smg-p90-ext",
                    Name = "P90 Ext. / M.C",
                    Category = WeaponCategory.SubMachineGun,
                    VerticalRecoil = 4,
                    HorizontalRecoil = 1,
                    DelayMs = 11,
                    SmoothStep = 2,
                    Jitter = 1,
                    ScopeOnly = false,
                    Description = "Kapasitas 50 peluru dengan recoil stabil merata.",
                    IsDefault = true
                },

                // SHOTGUN
                new()
                {
                    Id = "sg-m1887",
                    Name = "M1887 / Putar",
                    Category = WeaponCategory.Shotgun,
                    VerticalRecoil = 2,
                    HorizontalRecoil = 0,
                    DelayMs = 30,
                    SmoothStep = 1,
                    Jitter = 0,
                    ScopeOnly = false,
                    Description = "Shotgun manual pump. Kompensasi hentakan tembakan tunggal.",
                    IsDefault = true
                },
                new()
                {
                    Id = "sg-spas-15",
                    Name = "SPAS-15",
                    Category = WeaponCategory.Shotgun,
                    VerticalRecoil = 7,
                    HorizontalRecoil = 0,
                    DelayMs = 25,
                    SmoothStep = 2,
                    Jitter = 1,
                    ScopeOnly = false,
                    Description = "Semi-otomatis shotgun dengan hentakan vertikal kuat per tembakan.",
                    IsDefault = true
                },

                // SNIPER RIFLE
                new()
                {
                    Id = "sr-cheytac-m200",
                    Name = "CheyTac M200 / L115A1",
                    Category = WeaponCategory.SniperRifle,
                    VerticalRecoil = 1,
                    HorizontalRecoil = 0,
                    DelayMs = 50,
                    SmoothStep = 1,
                    Jitter = 0,
                    ScopeOnly = true,
                    Description = "Sniper satu tembakan (Scope Only). Stabilisasi crosshair mikro.",
                    IsDefault = true
                },

                // MACHINE GUN
                new()
                {
                    Id = "mg-pkm",
                    Name = "PKM / Gatling",
                    Category = WeaponCategory.MachineGun,
                    VerticalRecoil = 7,
                    HorizontalRecoil = 2,
                    DelayMs = 10,
                    SmoothStep = 3,
                    Jitter = 2,
                    ScopeOnly = false,
                    Description = "Senjata berat dengan tembakan beruntun panjang dan getaran tinggi.",
                    IsDefault = true
                }
            };
        }
    }
}
