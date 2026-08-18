using System;
using System.IO;
using System.Text.Json;

namespace PbRecoil.Core
{
    public class AppConfig
    {
        public int VerticalPullPixels { get; set; } = 1;
        public int ShotHoldMs { get; set; } = 15;
        public int ReleaseRecoveryMs { get; set; } = 8;
        public int InitialKickBonus { get; set; } = 1;
        public bool IsCrosshairVisible { get; set; } = false;
        public bool IsOverlayActive { get; set; } = true;
    }

    public static class ConfigService
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hexvyrr_config.json");
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public static bool SaveConfig(AppConfig config)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, JsonOptions);
                File.WriteAllText(ConfigPath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static AppConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                    if (cfg != null) return cfg;
                }
            }
            catch { }

            return GetDefaultConfig();
        }

        public static AppConfig GetDefaultConfig()
        {
            return new AppConfig
            {
                VerticalPullPixels = 1,
                ShotHoldMs         = 15,
                ReleaseRecoveryMs  = 8,
                InitialKickBonus   = 1,
                IsCrosshairVisible = false,
                IsOverlayActive    = true
            };
        }
    }
}
