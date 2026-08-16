using System;
using System.Text.Json.Serialization;

namespace PbRecoil.Models
{
    public class WeaponPreset : ICloneable
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        
        public string Name { get; set; } = "New Weapon";
        
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public WeaponCategory Category { get; set; } = WeaponCategory.AssaultRifle;
        
        /// <summary>
        /// Kekuatan tarikan vertikal ke bawah (Pixel delta Y per tick)
        /// </summary>
        public int VerticalRecoil { get; set; } = 4;
        
        /// <summary>
        /// Kompensasi horizontal (Pixel delta X per tick, minus = kiri, plus = kanan)
        /// </summary>
        public int HorizontalRecoil { get; set; } = 0;
        
        /// <summary>
        /// Interval waktu antar drag dalam milidetik (ms)
        /// </summary>
        public int DelayMs { get; set; } = 15;
        
        /// <summary>
        /// Pembagian langkah penghalusan gerakan (Smoothing Steps)
        /// </summary>
        public int SmoothStep { get; set; } = 2;
        
        /// <summary>
        /// Tingkat variasi acak (Humanizer Jitter) agar tarikan tidak kaku
        /// </summary>
        public int Jitter { get; set; } = 1;
        
        /// <summary>
        /// Jika true, recoil hanya aktif saat Klik Kanan (Scope/Aim) juga ditekan
        /// </summary>
        public bool ScopeOnly { get; set; } = false;
        
        /// <summary>
        /// Catatan/Deskripsi senjata
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Penanda preset bawaan sistem
        /// </summary>
        public bool IsDefault { get; set; } = false;

        public object Clone()
        {
            return new WeaponPreset
            {
                Id = this.Id,
                Name = this.Name,
                Category = this.Category,
                VerticalRecoil = this.VerticalRecoil,
                HorizontalRecoil = this.HorizontalRecoil,
                DelayMs = this.DelayMs,
                SmoothStep = this.SmoothStep,
                Jitter = this.Jitter,
                ScopeOnly = this.ScopeOnly,
                Description = this.Description,
                IsDefault = this.IsDefault
            };
        }
    }
}
