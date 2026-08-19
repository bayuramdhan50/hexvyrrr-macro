using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PbRecoil.Core
{
    /// <summary>
    /// Engine Hexvyrr Macro untuk Point Blank.
    /// Mengimplementasikan macro sequence loop berbasis Klik Kiri (LMB):
    /// [LMB Down] -> Delay (HoldMs) -> [LMB Up] -> Delay (ReleaseMs)
    /// </summary>
    public class MouseInputEngine : IDisposable
    {
        // ── Parameter Konfigurasi Macro Sequence ─────────────────────────────
        public volatile int HoldMs    = 20; // Durasi tahan penekanan LMB (ms) [Default: 20ms]
        public volatile int ReleaseMs = 0;  // Jeda antar penekanan LMB (ms) [Default: 0ms]

        private readonly Win32Api.LowLevelMouseProc _hookProc;
        private IntPtr _hookHandle = IntPtr.Zero;

        private Thread? _workerThread;
        private volatile bool _isDisposed;
        private volatile bool _isEnabled = true;
        private volatile bool _isPhysicalLmbDown;
        private volatile bool _isFiring;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    if (!_isEnabled)
                    {
                        _isPhysicalLmbDown = false;
                        _isFiring = false;
                        Win32Api.SendMouseUp();
                    }
                    OnStateChanged?.Invoke(_isEnabled);
                    PlayStatusBeep(_isEnabled);
                }
            }
        }

        public bool IsFiring => _isFiring;

        public event Action<bool>? OnStateChanged;
        public event Action<bool>? OnFiringStateChanged;
        public event Action? OnRecoilTick;

        public MouseInputEngine()
        {
            _hookProc = HookCallback;
        }

        public void Start()
        {
            InstallHook();

            if (_workerThread != null && _workerThread.IsAlive) return;

            _isDisposed = false;
            _workerThread = new Thread(WorkerLoop)
            {
                Name = "Hexvyrr_MacroThread",
                IsBackground = true,
                Priority = ThreadPriority.Highest
            };
            _workerThread.Start();
        }

        public void Toggle()
        {
            IsEnabled = !IsEnabled;
        }

        private void InstallHook()
        {
            if (_hookHandle != IntPtr.Zero) return;

            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            var hMod = Win32Api.GetModuleHandle(curModule?.ModuleName);
            _hookHandle = Win32Api.SetWindowsHookEx(Win32Api.WH_MOUSE_LL, _hookProc, hMod, 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = Marshal.PtrToStructure<Win32Api.MSLLHOOKSTRUCT>(lParam);
                bool isInjected = (hookStruct.flags & 1) != 0 || hookStruct.dwExtraInfo == Win32Api.INJECTED_SIGNATURE;

                if (!isInjected)
                {
                    var msg = wParam.ToInt32();
                    if (msg == Win32Api.WM_LBUTTONDOWN)
                    {
                        _isPhysicalLmbDown = true;

                        if (_isEnabled)
                        {
                            return (IntPtr)1; // Tahan sinyal fisik, delegasikan ke Hexvyrr macro loop
                        }
                    }
                    else if (msg == Win32Api.WM_LBUTTONUP)
                    {
                        _isPhysicalLmbDown = false;

                        if (_isEnabled)
                        {
                            return (IntPtr)1;
                        }
                    }
                }
            }

            return Win32Api.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        private void WorkerLoop()
        {
            Win32Api.TimeBeginPeriod(1);

            try
            {
                while (!_isDisposed)
                {
                    if (!_isEnabled)
                    {
                        if (_isFiring)
                        {
                            _isFiring = false;
                            OnFiringStateChanged?.Invoke(false);
                            Win32Api.SendMouseUp();
                        }
                        Thread.Sleep(10);
                        continue;
                    }

                    // Tahan (HOLD) LMB fisik -> jalankan Hexvyrr Macro Sequence Loop
                    if (_isPhysicalLmbDown)
                    {
                        if (!_isFiring)
                        {
                            _isFiring = true;
                            OnFiringStateChanged?.Invoke(true);
                        }

                        ExecuteMacroCycle();
                    }
                    else
                    {
                        if (_isFiring)
                        {
                            _isFiring = false;
                            OnFiringStateChanged?.Invoke(false);
                            Win32Api.SendMouseUp();
                        }

                        Thread.Sleep(1);
                    }
                }
            }
            finally
            {
                Win32Api.SendMouseUp();
                Win32Api.TimeEndPeriod(1);
            }
        }

        /// <summary>
        /// Mengeksekusi 1 siklus macro Hexvyrr:
        /// 1. Simulasikan Klik Kiri Ditekan (LMB Down)
        /// 2. Tahan selama HoldMs (default: 20 ms)
        /// 3. Simulasikan Klik Kiri Dilepas (LMB Up)
        /// 4. Jeda selama ReleaseMs (default: 0 ms)
        /// </summary>
        private void ExecuteMacroCycle()
        {
            int holdDuration = Math.Max(1, HoldMs);
            int releaseDuration = Math.Max(0, ReleaseMs);

            // 1. Kirim Klik Kiri Ditekan (LMB Down)
            Win32Api.SendMouseDown();
            OnRecoilTick?.Invoke();

            // 2. Durasi Tahan (Hold Time)
            PreciseSleep(holdDuration);

            // 3. Kirim Klik Kiri Dilepas (LMB Up)
            Win32Api.SendMouseUp();

            // 4. Jeda Antar Klik (Release Delay)
            if (releaseDuration > 0)
            {
                PreciseSleep(releaseDuration);
            }
        }

        /// <summary>
        /// Delay ultra-presisi berbasis Stopwatch dengan spin-wait adaptif.
        /// </summary>
        private void PreciseSleep(int ms)
        {
            if (ms <= 0) return;
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms)
            {
                if (!_isPhysicalLmbDown || !_isEnabled) break;

                if (ms - sw.ElapsedMilliseconds > 2)
                    Thread.Sleep(1);
                else
                    Thread.SpinWait(15);
            }
        }

        private static void PlayStatusBeep(bool enabled)
        {
            Task.Run(() =>
            {
                try
                {
                    if (enabled)
                        Console.Beep(1000, 100);
                    else
                        Console.Beep(450, 100);
                }
                catch
                {
                    try
                    {
                        if (enabled)
                            System.Media.SystemSounds.Asterisk.Play();
                        else
                            System.Media.SystemSounds.Hand.Play();
                    }
                    catch { }
                }
            });
        }

        public void Dispose()
        {
            _isDisposed = true;
            _isEnabled  = false;
            _isPhysicalLmbDown = false;

            if (_hookHandle != IntPtr.Zero)
            {
                Win32Api.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }

            Win32Api.SendMouseUp();
        }
    }
}
