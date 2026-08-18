using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PbRecoil.Core
{
    /// <summary>
    /// Engine Auto-Tap & Anti-Recoil untuk Point Blank.
    /// Mengimplementasikan adaptive dynamic tapping curve dengan kalibrasi halus.
    /// </summary>
    public class MouseInputEngine : IDisposable
    {
        // ── Parameter Konfigurasi (Tersinkron dengan F3 Menu) ──────────────────
        public volatile int VerticalPullPixels = 1;  // Kekuatan tarikan recoil dasar (px) [Default: 1px - halus]
        public volatile int ShotHoldMs         = 15; // Waktu tahan klik per tap (ms) [Default: 15ms]
        public volatile int ReleaseRecoveryMs  = 8;  // Jeda pemulihan antar tap (ms) [Default: 8ms]
        public volatile int InitialKickBonus   = 1;  // Kompensasi kick peluru 1-3 (px) [Default: +1px]
        public volatile int SmoothSteps        = 2;  // Langkah pembagian tarikan mouse [Default: 2]
        public volatile int JitterRange        = 1;  // Humanizer jitter acak (±1 px) [Default: 1px]

        private static readonly Random _random = new();
        private readonly Win32Api.LowLevelMouseProc _hookProc;
        private IntPtr _hookHandle = IntPtr.Zero;

        private Thread? _workerThread;
        private volatile bool _isDisposed;
        private volatile bool _isEnabled = true;
        private volatile bool _isPhysicalLmbDown;
        private volatile bool _isFiring;
        private int _shotCount;

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
                        _shotCount = 0;
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
                Name = "Hexvyrr_SmartEngineThread",
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
                            return (IntPtr)1; // Tahan sinyal fisik, jalankan smart tapping loop
                        }
                    }
                    else if (msg == Win32Api.WM_LBUTTONUP)
                    {
                        _isPhysicalLmbDown = false;
                        _shotCount = 0; // Reset kurva tembakan saat tombol dilepas

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
                            _shotCount = 0;
                            OnFiringStateChanged?.Invoke(false);
                            Win32Api.SendMouseUp();
                        }
                        Thread.Sleep(10);
                        continue;
                    }

                    // Tahan (HOLD) LMB fisik -> jalankan Smart Dynamic Tapping
                    if (_isPhysicalLmbDown)
                    {
                        if (!_isFiring)
                        {
                            _isFiring = true;
                            _shotCount = 0;
                            OnFiringStateChanged?.Invoke(true);
                        }

                        ExecuteSmartTapCycle();
                    }
                    else
                    {
                        if (_isFiring)
                        {
                            _isFiring = false;
                            _shotCount = 0;
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
        /// Mengeksekusi siklus Smart Tap dengan Dynamic 10-Shot Recoil Curve:
        /// - Shot 1–3: Initial Kick Phase (Hold sedikit lebih lama, tarikan kompensasi first-shot kick)
        /// - Shot 4–10: Ramp-up Stabilization Phase (Gradual stabilization)
        /// - Shot 11+: Steady-State Laser Mode (Ritme konstan rapat, tembakan lurus)
        /// </summary>
        private void ExecuteSmartTapCycle()
        {
            _shotCount++;

            int holdMs;
            int pullPixels;
            int recoveryMs;
            int steps = Math.Max(1, SmoothSteps);
            int jitter = Math.Max(0, JitterRange);

            if (_shotCount <= 3)
            {
                // ── FASE 1: PELURU 1–3 (Initial Kick) ──
                holdMs     = Math.Max(1, ShotHoldMs + 4);
                pullPixels = Math.Max(0, VerticalPullPixels + InitialKickBonus);
                recoveryMs = Math.Max(1, ReleaseRecoveryMs + 2);
            }
            else if (_shotCount <= 10)
            {
                // ── FASE 2: PELURU 4–10 (Ramp-up Stabilization) ──
                int extraHold = Math.Max(0, 2 - (_shotCount - 3) / 2);
                holdMs     = Math.Max(1, ShotHoldMs + extraHold);
                pullPixels = Math.Max(0, VerticalPullPixels);
                recoveryMs = Math.Max(1, ReleaseRecoveryMs);
            }
            else
            {
                // ── FASE 3: PELURU 11+ (Steady-State Laser Mode) ──
                holdMs     = Math.Max(1, ShotHoldMs);
                pullPixels = Math.Max(0, VerticalPullPixels);
                recoveryMs = Math.Max(1, ReleaseRecoveryMs - 1);
            }

            // 1. Simulasikan Klik Kiri (LMB Down)
            Win32Api.SendMouseDown();
            OnRecoilTick?.Invoke();

            // 2. Durasi penahanan peluru (Hold)
            PreciseSleep(holdMs);

            // 3. Tarik recoil vertikal secara halus saat LMB masih aktif
            if (pullPixels > 0 || jitter > 0)
            {
                double subY = (double)pullPixels / steps;
                double accY = 0;
                int stepDelay = Math.Max(1, 8 / steps);

                for (int i = 0; i < steps; i++)
                {
                    if (!_isEnabled || !_isPhysicalLmbDown) break;

                    accY += subY;
                    int dy = (int)Math.Round(accY);
                    accY -= dy;

                    int jitterX = jitter > 0 ? _random.Next(-jitter, jitter + 1) : 0;
                    int jitterY = jitter > 0 ? _random.Next(-jitter, jitter + 1) : 0;

                    Win32Api.SendMouseMove(jitterX, dy + jitterY);
                    PreciseSleep(stepDelay);
                }
            }

            // 4. Lepas Klik Kiri (LMB Up) untuk reset bloom crosshair
            Win32Api.SendMouseUp();

            // 5. Jeda pemulihan (Recovery) sebelum tap berikutnya
            PreciseSleep(recoveryMs);
        }

        /// <summary>
        /// Delay ultra-presisi berbasis Stopwatch yang tidak terpengaruh kuantisasi OS thread scheduler.
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
            _shotCount = 0;

            if (_hookHandle != IntPtr.Zero)
            {
                Win32Api.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }

            Win32Api.SendMouseUp();
        }
    }
}
