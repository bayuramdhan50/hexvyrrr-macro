using System;
using System.Threading;

namespace PbRecoil.Core
{
    public class GlobalHotkeyManager : IDisposable
    {
        private Thread? _hotkeyThread;
        private volatile bool _isDisposed;

        public event Action? OnToggleEngine;
        public event Action? OnCyclePreset;
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
            bool wasF6Down = false;
            bool wasF7Down = false;
            bool wasF8Down = false;

            while (!_isDisposed)
            {
                // F6 - Toggle Recoil ON / OFF
                var isF6Down = Win32Api.IsKeyPressed(Win32Api.VK_F6);
                if (isF6Down && !wasF6Down)
                {
                    OnToggleEngine?.Invoke();
                }
                wasF6Down = isF6Down;

                // F7 - Cycle Next Weapon Preset
                var isF7Down = Win32Api.IsKeyPressed(Win32Api.VK_F7);
                if (isF7Down && !wasF7Down)
                {
                    OnCyclePreset?.Invoke();
                }
                wasF7Down = isF7Down;

                // F8 - Toggle In-Game HUD Overlay
                var isF8Down = Win32Api.IsKeyPressed(Win32Api.VK_F8);
                if (isF8Down && !wasF8Down)
                {
                    OnToggleOverlay?.Invoke();
                }
                wasF8Down = isF8Down;

                Thread.Sleep(20);
            }
        }

        public void Dispose()
        {
            _isDisposed = true;
        }
    }
}
