using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PbRecoil.Core
{
    /// <summary>
    /// Kategori & Mode Senjata Hexvyrr Macro
    /// </summary>
    public enum MacroMode
    {
        AssaultNoRecoil = 0, // Mode tap Klik Kiri Assault / SMG (Hold 20ms, Release 0ms)
        AwpNormal       = 1, // AWP Tahan No QC (End Delay: 890ms)
        AwpQc50         = 2, // AWP Tahan QC 50% (End Delay: 590ms)
        AwpQc75         = 3, // AWP Tahan QC 75% (End Delay: 300ms)
        SgNormal        = 4, // SG Tahan No QC (End Delay: 750ms)
        SgQc50          = 5, // SG Tahan QC 50% (End Delay: 480ms)
        SgQc75          = 6  // SG Tahan QC 75% (End Delay: 245ms)
    }

    /// <summary>
    /// Engine Hexvyrr Macro Multi-Mode untuk Point Blank (Assault, AWP, SG).
    /// </summary>
    public class MouseInputEngine : IDisposable
    {
        // ── Parameter Konfigurasi Macro Sequence ─────────────────────────────
        public volatile MacroMode CurrentMode = MacroMode.AssaultNoRecoil;
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
                        ReleaseAllInputs();
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
                            ReleaseAllInputs();
                        }
                        Thread.Sleep(10);
                        continue;
                    }

                    // Tahan (HOLD) LMB fisik -> jalankan Macro Sequence Loop sesuai mode yang dipilih
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
                            ReleaseAllInputs();
                        }

                        Thread.Sleep(1);
                    }
                }
            }
            finally
            {
                ReleaseAllInputs();
                Win32Api.TimeEndPeriod(1);
            }
        }

        /// <summary>
        /// Mengeksekusi siklus macro berdasarkan mode senjata yang sedang aktif.
        /// </summary>
        private void ExecuteMacroCycle()
        {
            switch (CurrentMode)
            {
                case MacroMode.AssaultNoRecoil:
                    ExecuteAssaultCycle();
                    break;

                case MacroMode.AwpNormal:
                    ExecuteAwpCycle(890);
                    break;

                case MacroMode.AwpQc50:
                    ExecuteAwpCycle(590);
                    break;

                case MacroMode.AwpQc75:
                    ExecuteAwpCycle(300);
                    break;

                case MacroMode.SgNormal:
                    ExecuteSgCycle(750);
                    break;

                case MacroMode.SgQc50:
                    ExecuteSgCycle(480);
                    break;

                case MacroMode.SgQc75:
                    ExecuteSgCycle(245);
                    break;

                default:
                    ExecuteAssaultCycle();
                    break;
            }
        }

        /// <summary>
        /// Mode Assault / SMG No-Recoil:
        /// [LMB Down] -> HoldMs (20ms) -> [LMB Up] -> ReleaseMs (0ms)
        /// </summary>
        private void ExecuteAssaultCycle()
        {
            int holdDuration = Math.Max(1, HoldMs);
            int releaseDuration = Math.Max(0, ReleaseMs);

            Win32Api.SendMouseDown();
            OnRecoilTick?.Invoke();

            PreciseSleep(holdDuration);

            Win32Api.SendMouseUp();

            if (releaseDuration > 0)
            {
                PreciseSleep(releaseDuration);
            }
        }

        /// <summary>
        /// Mode AWP Tahan (Scope + Fire + 3-Q-1 Switch + Release + Delay):
        /// [RMB Down] -> 20ms Scope In -> [LMB Down] -> 20ms Fire -> [3 Down] -> 20ms -> [3 Up] -> 10ms ->
        /// [Q Down] -> 20ms -> [Q Up] -> 10ms -> [1 Down] -> 20ms -> [1 Up] -> 10ms ->
        /// [RMB Up] [LMB Up] -> End Recovery Delay (890ms / 590ms / 300ms)
        /// </summary>
        private void ExecuteAwpCycle(int endRecoveryMs)
        {
            // 1. Scope In (RMB Down) -> Beri jeda agar animasi zoom PB terinisiasi
            Win32Api.SendRightMouseDown();
            PreciseSleep(20);
            if (!_isPhysicalLmbDown || !_isEnabled) { ReleaseAllInputs(); return; }

            // 2. Fire (LMB Down) -> Tembak dalam status scoped
            Win32Api.SendMouseDown();
            OnRecoilTick?.Invoke();
            PreciseSleep(20);
            if (!_isPhysicalLmbDown || !_isEnabled) { ReleaseAllInputs(); return; }

            // 3. Switch ke Melee (Key 3)
            Win32Api.SendKeyDown(Win32Api.VK_3);
            PreciseSleep(20);
            Win32Api.SendKeyUp(Win32Api.VK_3);
            PreciseSleep(10);
            if (!_isPhysicalLmbDown || !_isEnabled) { ReleaseAllInputs(); return; }

            // 3. Quick Switch (Key Q)
            Win32Api.SendKeyDown(Win32Api.VK_Q);
            PreciseSleep(20);
            Win32Api.SendKeyUp(Win32Api.VK_Q);
            PreciseSleep(10);
            if (!_isPhysicalLmbDown || !_isEnabled) { ReleaseAllInputs(); return; }

            // 4. Switch Primary Weapon (Key 1)
            Win32Api.SendKeyDown(Win32Api.VK_1);
            PreciseSleep(20);
            Win32Api.SendKeyUp(Win32Api.VK_1);
            PreciseSleep(10);

            // 5. Release Mouse Buttons (RMB Up & LMB Up)
            Win32Api.SendRightMouseUp();
            Win32Api.SendMouseUp();

            // 6. Recovery Delay sebelum tembakan berikutnya
            PreciseSleep(endRecoveryMs);
        }

        /// <summary>
        /// Mode SG Tahan (Fire + 3-1 Quick Switch + Delay):
        /// [LMB Down] -> 20ms -> [LMB Up] -> 10ms ->
        /// [3 Down] -> 20ms -> [3 Up] -> 10ms ->
        /// [1 Down] -> 20ms -> [1 Up] -> End Recovery Delay (750ms / 480ms / 245ms)
        /// </summary>
        private void ExecuteSgCycle(int endRecoveryMs)
        {
            // 1. Fire (LMB Down -> 20ms -> LMB Up)
            Win32Api.SendMouseDown();
            OnRecoilTick?.Invoke();

            PreciseSleep(20);
            Win32Api.SendMouseUp();
            PreciseSleep(10);
            if (!_isPhysicalLmbDown || !_isEnabled) { ReleaseAllInputs(); return; }

            // 2. Switch ke Melee (Key 3)
            Win32Api.SendKeyDown(Win32Api.VK_3);
            PreciseSleep(20);
            Win32Api.SendKeyUp(Win32Api.VK_3);
            PreciseSleep(10);
            if (!_isPhysicalLmbDown || !_isEnabled) { ReleaseAllInputs(); return; }

            // 3. Switch Primary Weapon (Key 1)
            Win32Api.SendKeyDown(Win32Api.VK_1);
            PreciseSleep(20);
            Win32Api.SendKeyUp(Win32Api.VK_1);

            // 4. Recovery Delay sebelum tembakan berikutnya
            PreciseSleep(endRecoveryMs);
        }

        /// <summary>
        /// Melepas seluruh status penekanan mouse dan keyboard untuk mencegah tombol tersangkut.
        /// </summary>
        private static void ReleaseAllInputs()
        {
            Win32Api.SendMouseUp();
            Win32Api.SendRightMouseUp();
            Win32Api.SendKeyUp(Win32Api.VK_3);
            Win32Api.SendKeyUp(Win32Api.VK_Q);
            Win32Api.SendKeyUp(Win32Api.VK_1);
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

            ReleaseAllInputs();
        }
    }
}
