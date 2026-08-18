using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using PbRecoil.Core;

namespace PbRecoil.Views
{
    public partial class UpdateDialog : Window
    {
        private readonly UpdateInfo _updateInfo;
        private readonly UpdateService _updateService;
        private CancellationTokenSource? _cts;
        private bool _isDownloading = false;

        public UpdateDialog(UpdateInfo updateInfo, UpdateService updateService)
        {
            InitializeComponent();
            _updateInfo = updateInfo;
            _updateService = updateService;

            TxtCurrentVersion.Text = $"v{_updateInfo.CurrentVersion}";
            TxtLatestVersion.Text = $"v{_updateInfo.LatestVersion}";
            TxtReleaseNotes.Text = string.IsNullOrWhiteSpace(_updateInfo.ReleaseNotes)
                ? "• Pembaruan performa, optimalisasi engine, dan perbaikan bug."
                : _updateInfo.ReleaseNotes;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        private async void BtnDownloadAndInstall_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading) return;

            // Jika URL download langsung tidak tersedia, fallback buka halaman rilis di browser
            if (string.IsNullOrEmpty(_updateInfo.DownloadUrl) ||
                !_updateInfo.DownloadUrl.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string targetUrl = string.IsNullOrEmpty(_updateInfo.ReleasePageUrl)
                        ? "https://github.com/hexvyrr/pb-recoil/releases"
                        : _updateInfo.ReleasePageUrl;

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = targetUrl,
                        UseShellExecute = true
                    });
                }
                catch { }

                Close();
                return;
            }

            _isDownloading = true;
            BtnDownloadAndInstall.IsEnabled = false;
            BtnDownloadAndInstall.Visibility = Visibility.Collapsed;
            ProgressSection.Visibility = Visibility.Visible;
            BtnCancel.Content = "Batal";

            _cts = new CancellationTokenSource();

            var progress = new Progress<(long bytesRead, long totalBytes, int percentage)>(report =>
            {
                DownloadProgressBar.Value = report.percentage;
                TxtPercentage.Text = $"{report.percentage}%";

                if (report.totalBytes > 0)
                {
                    double currentMb = report.bytesRead / (1024.0 * 1024.0);
                    double totalMb = report.totalBytes / (1024.0 * 1024.0);
                    TxtStatus.Text = $"Mengunduh: {currentMb:F1} MB / {totalMb:F1} MB";
                }
                else
                {
                    double currentMb = report.bytesRead / (1024.0 * 1024.0);
                    TxtStatus.Text = $"Mengunduh: {currentMb:F1} MB...";
                }

                if (report.percentage >= 100)
                {
                    TxtStatus.Text = "Pemasangan dan me-restart aplikasi...";
                }
            });

            try
            {
                await _updateService.DownloadAndApplyUpdateAsync(_updateInfo.DownloadUrl, progress, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                TxtStatus.Text = "Unduhan dibatalkan.";
                ResetUiState();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Gagal mengunduh: {ex.Message}";
                ResetUiState();
            }
        }

        private void ResetUiState()
        {
            _isDownloading = false;
            BtnDownloadAndInstall.IsEnabled = true;
            BtnDownloadAndInstall.Visibility = Visibility.Visible;
            BtnCancel.Content = "Tutup";
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading && _cts != null)
            {
                _cts.Cancel();
            }
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading && _cts != null)
            {
                _cts.Cancel();
            }
            Close();
        }
    }
}
