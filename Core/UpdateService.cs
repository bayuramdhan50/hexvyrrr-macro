using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PbRecoil.Core
{
    public class UpdateService
    {
        // Ganti dengan repository publik Anda di GitHub
        private const string DefaultRepoOwner = "bayuramdhan50";
        private const string DefaultRepoName = "hexvyrrr-macro";

        private static readonly HttpClient _httpClient = new HttpClient();

        static UpdateService()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("HexvyrrMacro-Updater", "1.0")
            );
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        /// <summary>
        /// Mendapatkan versi aplikasi yang sedang berjalan.
        /// </summary>
        public static string GetCurrentVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var ver = assembly.GetName().Version;
            return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.0.0";
        }

        /// <summary>
        /// Membersihkan string tag dan mem-parsing versi semantik secara presisi.
        /// Menangani awalan 'v'/'V', sufiks build/prerelease ('-beta', '+build'), dsb.
        /// </summary>
        public static Version? ParseCleanVersion(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string s = raw.Trim().TrimStart('v', 'V').Trim();
            int extraIdx = s.IndexOfAny(new[] { '-', '+' });
            if (extraIdx >= 0) s = s.Substring(0, extraIdx);

            var parts = s.Split('.');
            var numParts = new System.Collections.Generic.List<int>();
            foreach (var p in parts)
            {
                string digits = "";
                foreach (char c in p)
                {
                    if (char.IsDigit(c)) digits += c;
                    else break;
                }
                if (int.TryParse(digits, out int val))
                {
                    numParts.Add(val);
                }
                else
                {
                    break;
                }
            }

            if (numParts.Count == 0) return null;
            if (numParts.Count == 1) return new Version(numParts[0], 0);
            if (numParts.Count == 2) return new Version(numParts[0], numParts[1]);
            if (numParts.Count == 3) return new Version(numParts[0], numParts[1], numParts[2]);
            return new Version(numParts[0], numParts[1], numParts[2], numParts[3]);
        }

        /// <summary>
        /// Memeriksa pembaruan versi dari GitHub Releases API secara akurat tanpa terpengaruh cache CDN.
        /// </summary>
        public async Task<UpdateInfo> CheckForUpdatesAsync(string repoOwner = DefaultRepoOwner, string repoName = DefaultRepoName)
        {
            string currentVerStr = GetCurrentVersion();
            var updateInfo = new UpdateInfo
            {
                CurrentVersion = currentVerStr,
                LatestVersion = currentVerStr,
                IsUpdateAvailable = false,
                CheckFailed = false
            };

            try
            {
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                // Query 1: Mengambil daftar rilis terbaru (menghindari jeda cache CDN pada endpoint /releases/latest)
                string listUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases?per_page=15&t={timestamp}";

                using var request = new HttpRequestMessage(HttpMethod.Get, listUrl);
                request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
                request.Headers.Add("Accept", "application/vnd.github.v3+json");

                using var response = await _httpClient.SendAsync(request);

                JsonDocument? doc = null;
                JsonElement targetRelease = default;
                bool foundValidRelease = false;

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        Version? highestFoundVer = null;
                        foreach (var item in doc.RootElement.EnumerateArray())
                        {
                            if (item.TryGetProperty("draft", out var isDraft) && isDraft.GetBoolean())
                                continue;

                            string tag = item.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
                            var parsed = ParseCleanVersion(tag);
                            if (parsed != null)
                            {
                                if (highestFoundVer == null || parsed > highestFoundVer)
                                {
                                    highestFoundVer = parsed;
                                    targetRelease = item;
                                    foundValidRelease = true;
                                }
                            }
                        }
                    }
                }

                // Fallback jika query /releases gagal atau kosong: coba /releases/latest
                if (!foundValidRelease)
                {
                    doc?.Dispose();
                    string latestUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest?t={timestamp}";
                    using var latestReq = new HttpRequestMessage(HttpMethod.Get, latestUrl);
                    latestReq.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
                    latestReq.Headers.Add("Accept", "application/vnd.github.v3+json");

                    using var latestResp = await _httpClient.SendAsync(latestReq);
                    if (!latestResp.IsSuccessStatusCode)
                    {
                        updateInfo.CheckFailed = true;
                        updateInfo.ErrorMessage = latestResp.StatusCode switch
                        {
                            System.Net.HttpStatusCode.Forbidden => "Batas akses GitHub API terlampaui (Rate Limit). Silakan coba beberapa menit lagi.",
                            System.Net.HttpStatusCode.NotFound  => "Repositori rilis tidak ditemukan di GitHub.",
                            _ => $"Server GitHub merespons status {(int)latestResp.StatusCode} ({latestResp.ReasonPhrase})."
                        };
                        return updateInfo;
                    }

                    string latestJson = await latestResp.Content.ReadAsStringAsync();
                    doc = JsonDocument.Parse(latestJson);
                    targetRelease = doc.RootElement;
                    foundValidRelease = true;
                }

                using (doc)
                {
                    string tagName = targetRelease.TryGetProperty("tag_name", out var tagElem) ? tagElem.GetString() ?? "" : "";
                    string cleanTag = tagName.TrimStart('v', 'V').Trim();
                    string releaseNotes = targetRelease.TryGetProperty("body", out var bodyElem) ? bodyElem.GetString() ?? "" : "";
                    string releasePageUrl = targetRelease.TryGetProperty("html_url", out var htmlElem) ? htmlElem.GetString() ?? "" : "";

                    updateInfo.LatestVersion = cleanTag;
                    updateInfo.ReleaseNotes = string.IsNullOrWhiteSpace(releaseNotes) ? "Peningkatan performa dan stabilitas engine." : releaseNotes;
                    updateInfo.ReleasePageUrl = string.IsNullOrWhiteSpace(releasePageUrl)
                        ? $"https://github.com/{repoOwner}/{repoName}/releases"
                        : releasePageUrl;

                    // Cari link direct download binary executable (.exe) di assets
                    if (targetRelease.TryGetProperty("assets", out var assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var asset in assetsElem.EnumerateArray())
                        {
                            string name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                            string downloadUrl = asset.TryGetProperty("browser_download_url", out var dl) ? dl.GetString() ?? "" : "";
                            long size = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;

                            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                updateInfo.DownloadUrl = downloadUrl;
                                updateInfo.FileSizeBytes = size;
                                break;
                            }
                        }
                    }

                    // Jika tidak ditemukan asset .exe langsung, fallback ke direct release page
                    if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
                    {
                        updateInfo.DownloadUrl = updateInfo.ReleasePageUrl;
                    }

                    // Komparasi versi semantik akurat
                    var currentVer = ParseCleanVersion(currentVerStr);
                    var latestVer  = ParseCleanVersion(cleanTag);

                    if (currentVer != null && latestVer != null)
                    {
                        updateInfo.IsUpdateAvailable = latestVer > currentVer;
                    }
                    else
                    {
                        updateInfo.IsUpdateAvailable = string.Compare(cleanTag, currentVerStr, StringComparison.OrdinalIgnoreCase) > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                updateInfo.CheckFailed = true;
                updateInfo.ErrorMessage = ex.Message;
            }

            return updateInfo;
        }

        /// <summary>
        /// Mengunduh file installer/executable baru secara streaming dengan progress bar dan melakukan auto-replace.
        /// </summary>
        public async Task DownloadAndApplyUpdateAsync(
            string downloadUrl,
            IProgress<(long bytesRead, long totalBytes, int percentage)> progress,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(downloadUrl))
                throw new ArgumentException("URL unduhan tidak valid.", nameof(downloadUrl));

            string tempFilePath = Path.Combine(Path.GetTempPath(), $"HexvyrrMacro_Update_{Guid.NewGuid():N}.exe");

            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? -1L;

                using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920];
                    long totalBytesRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        totalBytesRead += bytesRead;

                        if (totalBytes > 0)
                        {
                            int percent = (int)((double)totalBytesRead / totalBytes * 100);
                            progress?.Report((totalBytesRead, totalBytes, percent));
                        }
                        else
                        {
                            progress?.Report((totalBytesRead, -1, 0));
                        }
                    }
                }
            }

            // Setelah download selesai, eksekusi mekanisme seamless replace & restart
            ApplyUpdateAndRestart(tempFilePath);
        }

        /// <summary>
        /// Mengganti file .exe aplikasi yang sedang aktif dengan file baru di temp path, lalu me-restart aplikasi.
        /// </summary>
        private static void ApplyUpdateAndRestart(string tempExePath)
        {
            string? currentExe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentExe))
            {
                currentExe = Process.GetCurrentProcess().MainModule?.FileName;
            }

            if (string.IsNullOrEmpty(currentExe) || !File.Exists(tempExePath))
            {
                throw new FileNotFoundException("Gagal mendeteksi path executable saat ini atau file unduhan.");
            }
            // Gunakan subproses PowerShell tersembunyi dengan retry loop untuk memastikan file lock terlepas sempurna,
            // menimpa file exe lama persis di lokasi aslinya dengan nama yang sama, lalu meluncurkan kembali exe baru.
            string psScript = $"Start-Sleep -Milliseconds 600; " +
                              $"$retry = 0; while ($retry -lt 15) {{ " +
                              $"  try {{ Move-Item -Path '{tempExePath}' -Destination '{currentExe}' -Force -ErrorAction Stop; break; }} " +
                              $"  catch {{ Start-Sleep -Milliseconds 300; $retry++ }} " +
                              $"}}; " +
                              $"Start-Process -FilePath '{currentExe}'";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"{psScript}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(startInfo);

            // Tutup aplikasi saat ini secara bersih
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.Application.Current.Shutdown();
                });
            }
            else
            {
                Environment.Exit(0);
            }
        }
    }
}
