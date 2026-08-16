using System;
using System.Windows;
using System.Windows.Interop;
using PbRecoil.Core;

namespace PbRecoil.Views
{
    public partial class OverlayWindow : Window
    {
        public OverlayWindow()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Terapkan flag Win32 agar jendela tembus klik (Click-Through) dan tidak mencuri fokus game
            var hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = Win32Api.GetWindowLong(hwnd, Win32Api.GWL_EXSTYLE);
            Win32Api.SetWindowLong(hwnd, Win32Api.GWL_EXSTYLE, extendedStyle | Win32Api.WS_EX_TRANSPARENT | Win32Api.WS_EX_LAYERED | Win32Api.WS_EX_TOOLWINDOW);
        }
    }
}
