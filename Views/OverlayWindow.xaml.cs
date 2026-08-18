using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using PbRecoil.Core;

namespace PbRecoil.Views
{
    public partial class OverlayWindow : Window
    {
        private readonly DispatcherTimer _trackingTimer;
        private IntPtr _hwnd = IntPtr.Zero;
        private double _dpiScaleX = 1.0;
        private double _dpiScaleY = 1.0;

        public OverlayWindow()
        {
            InitializeComponent();

            _trackingTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _trackingTimer.Tick += (s, e) => UpdateLayoutAndPosition();

            Loaded += (s, e) =>
            {
                UpdateDpiScale();
                UpdateLayoutAndPosition();
            };

            IsVisibleChanged += (s, e) =>
            {
                if (IsVisible)
                {
                    UpdateDpiScale();
                    UpdateLayoutAndPosition();
                    _trackingTimer.Start();
                }
                else
                {
                    _trackingTimer.Stop();
                }
            };
        }

        private void UpdateDpiScale()
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                _dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                _dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
                if (_dpiScaleX <= 0) _dpiScaleX = 1.0;
                if (_dpiScaleY <= 0) _dpiScaleY = 1.0;
            }
        }

        /// <summary>
        /// Menyesuaikan ukuran skala dan posisi HUD Overlay mengikuti resolusi game Point Blank atau screen.
        /// Memaksa Z-order Topmost agar tidak pernah hilang saat Alt+Tab.
        /// </summary>
        public void UpdateLayoutAndPosition()
        {
            var bounds = Win32Api.GetGameOrScreenBounds();

            // Hitung faktor skala berdasarkan tinggi area game (Baseline 1080p = 0.95 skala dasar agar compact)
            // Di 720p/768p skala akan mengecil proporsional (~0.65 - 0.70)
            double baseScale = (double)bounds.Height / 1080.0 * 0.95;
            double targetScale = Math.Clamp(baseScale, 0.55, 1.0);

            if (HudScaleTransform != null)
            {
                if (Math.Abs(HudScaleTransform.ScaleX - targetScale) > 0.01)
                {
                    HudScaleTransform.ScaleX = targetScale;
                    HudScaleTransform.ScaleY = targetScale;
                }
            }

            // Hitung posisi margin dari pojok kiri atas area game
            double marginPx = 16 * targetScale;
            double wpfLeft  = (bounds.X + marginPx) / _dpiScaleX;
            double wpfTop   = (bounds.Y + marginPx) / _dpiScaleY;

            Left = wpfLeft;
            Top  = wpfTop;

            // Pastikan window selalu di atas game (Z-Order Topmost) bahkan setelah Alt+Tab
            if (_hwnd != IntPtr.Zero && IsVisible)
            {
                Win32Api.EnsureTopmost(_hwnd);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Simpan handle HWND window
            _hwnd = new WindowInteropHelper(this).Handle;

            // Terapkan flag Win32: Click-Through, Layered, ToolWindow, NoActivate agar tidak mencuri fokus game
            int extendedStyle = Win32Api.GetWindowLong(_hwnd, Win32Api.GWL_EXSTYLE);
            Win32Api.SetWindowLong(_hwnd, Win32Api.GWL_EXSTYLE,
                extendedStyle | Win32Api.WS_EX_TRANSPARENT | Win32Api.WS_EX_LAYERED | Win32Api.WS_EX_TOOLWINDOW | Win32Api.WS_EX_NOACTIVATE);

            Win32Api.EnsureTopmost(_hwnd);
        }
    }
}
