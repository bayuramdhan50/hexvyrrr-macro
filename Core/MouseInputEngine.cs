using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PbRecoil.Core
{
    /// <summary>
    /// Engine Auto-Tap & Anti-Recoil Point Blank dengan algoritma hardcoded berkecepatan tinggi.
    /// Menggunakan WH_MOUSE_LL Hook dengan pencegahan intervensi spray DirectInput agar tembakan konsisten melakukan tap-tap.
    /// </summary>
    public class MouseInputEngine : IDisposable
    {
        // ── Parameter Hardcode Optimal Point Blank ──────────────────────────────
        private const int ShotHoldMs         = 35; // Durasi penahanan klik per peluru (ms)
        private const int ReleaseRecoveryMs  = 35; // Jeda pelepasan klik untuk reset crosshair bloom (ms)
        private const int VerticalPullPixels = 5;  // Kekuatan tarikan recoil vertikal per shot (px)
        private const int SmoothSteps        = 2;  // Langkah pembagian tarikan mouse
        private const int JitterRange        = 1;  // Humanizer jitter acak (±1 px)

        private readonly Random _random = new();
        private readonly Win32Api.LowLevelMouseProc _hookProc;
        private IntPtr _hookHandle = IntPtr.Zero;

        private Thread? _workerThread;
        private volatile bool _isDisposed;
        private volatile bool _isEnabled = true; // Default ON saat aplikasi dijalankan
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
                Name = "PbRecoil_EngineThread",
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

                // Hanya proses interaksi hardware fisik asli dari pengguna
                if (!isInjected)
                {
                    var msg = wParam.ToInt32();
                    if (msg == Win32Api.WM_LBUTTONDOWN)
                    {
                        _isPhysicalLmbDown = true;

                        // Tahan sinyal spray mentah saat Engine ON agar game hanya memproses tembakan tap teratur
                        if (_isEnabled)
                        {
                            return (IntPtr)1;
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

                    // Tahan (HOLD) LMB fisik -> jalankan looping Auto-Tap
                    if (_isPhysicalLmbDown)
                    {
                        if (!_isFiring)
                        {
                            _isFiring = true;
                            OnFiringStateChanged?.Invoke(true);
                        }

                        ExecuteTapCycle();
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
        /// Mengeksekusi 1 siklus tembakan tap:
        /// 1. Trigger Klik Kiri (LMB Down)
        /// 2. Tahan ~35ms untuk pelepasan peluru
        /// 3. Tarik recoil vertikal ke bawah dengan smoothing + jitter
        /// 4. Lepas Klik Kiri (LMB Up)
        /// 5. Jeda pemulihan ~35ms agar spread bloom reset
        /// </summary>
        private void ExecuteTapCycle()
        {
            // 1. Simulasikan klik tembak (LMB DOWN)
            Win32Api.SendMouseDown();
            OnRecoilTick?.Invoke();

            // 2. Durasi peluru melesat keluar
            PreciseSleep(ShotHoldMs);

            // 3. Tarik recoil vertikal ke bawah
            var subY = (double)VerticalPullPixels / SmoothSteps;
            var stepDelay = Math.Max(1, 10 / SmoothSteps);
            double accY = 0;

            for (int i = 0; i < SmoothSteps; i++)
            {
                if (!_isEnabled || !_isPhysicalLmbDown) break;

                accY += subY;
                var dy = (int)Math.Round(accY);
                accY -= dy;

                var jitterX = _random.Next(-JitterRange, JitterRange + 1);
                var jitterY = _random.Next(-JitterRange, JitterRange + 1);

                Win32Api.SendMouseMove(jitterX, dy + jitterY);
                PreciseSleep(stepDelay);
            }

            // 4. Lepas klik tembak (LMB UP) untuk reset crosshair
            Win32Api.SendMouseUp();

            // 5. Jeda pemulihan sebelum tap berikutnya dimulai
            PreciseSleep(ReleaseRecoveryMs);
        }

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
