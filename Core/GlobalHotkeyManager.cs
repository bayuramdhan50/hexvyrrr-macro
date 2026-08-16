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
        public event Action? OnToggleSettings;
        public event Action? OnNavigateUp;
        public event Action? OnNavigateDown;
        public event Action? OnValueLeft;
        public event Action? OnValueRight;

        public volatile bool IsSettingsOpen;

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
            bool wasF3 = false;
            bool wasUp = false;
            bool wasDown = false;
            bool wasLeft = false;
            bool wasRight = false;

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

                // F3 — Toggle Menu Pengaturan HUD
                var isF3 = Win32Api.IsKeyPressed(Win32Api.VK_F3);
                if (isF3 && !wasF3) OnToggleSettings?.Invoke();
                wasF3 = isF3;

                // Tombol Panah (Arrow Keys) aktif saat menu pengaturan HUD terbuka
                if (IsSettingsOpen)
                {
                    var isUp = Win32Api.IsKeyPressed(Win32Api.VK_UP);
                    if (isUp && !wasUp) OnNavigateUp?.Invoke();
                    wasUp = isUp;

                    var isDown = Win32Api.IsKeyPressed(Win32Api.VK_DOWN);
                    if (isDown && !wasDown) OnNavigateDown?.Invoke();
                    wasDown = isDown;

                    var isLeft = Win32Api.IsKeyPressed(Win32Api.VK_LEFT);
                    if (isLeft && !wasLeft) OnValueLeft?.Invoke();
                    wasLeft = isLeft;

                    var isRight = Win32Api.IsKeyPressed(Win32Api.VK_RIGHT);
                    if (isRight && !wasRight) OnValueRight?.Invoke();
                    wasRight = isRight;
                }
                else
                {
                    wasUp = wasDown = wasLeft = wasRight = false;
                }

                Thread.Sleep(20);
            }
        }

        public void Dispose()
        {
            _isDisposed = true;
        }
    }
}
