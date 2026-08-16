using System;
using System.Threading;

namespace PbRecoil.Core
{
    public class GlobalHotkeyManager : IDisposable
    {
        private Thread? _hotkeyThread;
        private volatile bool _isDisposed;

        public event Action? OnToggleEngine;
        public event Action? OnToggleOverlay;

        public void Start()
        {
            if (_hotkeyThread != null && _hotkeyThread.IsAlive) return;

            _isDisposed = false;
            _hotkeyThread = new Thread(HotkeyLoop)
            {
                Name = "PbRecoil_HotkeyThread",
                IsBackground = true
            };
            _hotkeyThread.Start();
        }

        private void HotkeyLoop()
        {
            bool wasF1 = false;
            bool wasF2 = false;

            while (!_isDisposed)
            {
                // F1 — Toggle Engine ON/OFF
                var isF1 = Win32Api.IsKeyPressed(Win32Api.VK_F1);
                if (isF1 && !wasF1) OnToggleEngine?.Invoke();
                wasF1 = isF1;

                // F2 — Toggle HUD Overlay on screen
                var isF2 = Win32Api.IsKeyPressed(Win32Api.VK_F2);
                if (isF2 && !wasF2) OnToggleOverlay?.Invoke();
                wasF2 = isF2;

                Thread.Sleep(20);
            }
        }

        public void Dispose()
        {
            _isDisposed = true;
        }
    }
}
