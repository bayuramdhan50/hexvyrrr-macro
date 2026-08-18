using System;

namespace PbRecoil.Core
{
    public class UpdateInfo
    {
        public string CurrentVersion { get; set; } = "1.0.0";
        public string LatestVersion { get; set; } = "1.0.0";
        public bool IsUpdateAvailable { get; set; }
        public string ReleaseNotes { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string ReleasePageUrl { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }

        public string FormattedFileSize
        {
            get
            {
                if (FileSizeBytes <= 0) return "Unknown size";
                double mb = (double)FileSizeBytes / (1024 * 1024);
                return $"{mb:F2} MB";
            }
        }
    }
}
