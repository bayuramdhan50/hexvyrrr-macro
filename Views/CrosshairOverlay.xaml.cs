using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using PbRecoil.Core;

namespace PbRecoil.Views
{
    public partial class CrosshairOverlay : Window
    {
        private readonly DispatcherTimer _updateTimer;
        private IntPtr _hwnd = IntPtr.Zero;
        private double _dpiScaleX = 1.0;
        private double _dpiScaleY = 1.0;

        public bool IsCrosshairRequested { get; set; } = false;

        public CrosshairOverlay()
        {
            InitializeComponent();

            // Atur ukuran window mini 12x12 tepat di area dot agar hemat GPU & RAM
            Width = 12;
            Height = 12;

            _updateTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _updateTimer.Tick += (s, e) => UpdatePosition();

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateDpiScale();
            UpdatePosition();
            Canvas.SetLeft(CrosshairDot, 3);
            Canvas.SetTop(CrosshairDot, 3);
            _updateTimer.Start();
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
        /// Mengatur posisi window overlay agar tepat berada di titik tengah client game.
        /// Crosshair HANYA tampil jika user berada di dalam game Point Blank (Foreground).
        /// </summary>
        public void UpdatePosition()
        {
            if (!IsCrosshairRequested)
            {
                if (Visibility != Visibility.Hidden)
                    Visibility = Visibility.Hidden;
                return;
            }

            bool isGameActive = Win32Api.IsPointBlankForeground();

            // Sembunyikan Crosshair jika tidak berada di game Point Blank
            if (!isGameActive)
            {
                if (Visibility != Visibility.Hidden)
                    Visibility = Visibility.Hidden;
                return;
            }

            if (Visibility != Visibility.Visible)
            {
                Visibility = Visibility.Visible;
            }

            var centerPixel = Win32Api.GetGameOrScreenCenter();

            // Konversi dari Physical Screen Pixels ke Logical WPF DIPs
            double wpfX = centerPixel.x / _dpiScaleX;
            double wpfY = centerPixel.y / _dpiScaleY;

            // Pusatkan window 12x12 di sekitar titik tengah (offset -6)
            Left = wpfX - 6;
            Top  = wpfY - 6;

            if (_hwnd != IntPtr.Zero && Visibility == Visibility.Visible)
            {
                Win32Api.EnsureTopmost(_hwnd);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            _hwnd = new WindowInteropHelper(this).Handle;

            // Terapkan flag Win32: Click-Through, Layered, ToolWindow, NoActivate agar tidak mencuri fokus game
            int exStyle = Win32Api.GetWindowLong(_hwnd, Win32Api.GWL_EXSTYLE);
            Win32Api.SetWindowLong(_hwnd, Win32Api.GWL_EXSTYLE,
                exStyle | Win32Api.WS_EX_TRANSPARENT | Win32Api.WS_EX_LAYERED | Win32Api.WS_EX_TOOLWINDOW | Win32Api.WS_EX_NOACTIVATE);

            Win32Api.EnsureTopmost(_hwnd);
        }
    }
}
