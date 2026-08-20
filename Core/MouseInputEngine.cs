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
        AssaultNoRecoil = 0, // No Recoil Auto-Tap (AR / SMG)
        AllSniperNormal = 1, // All Sniper Normal (Scope + 3-Q-1, Delay 750ms)
        AllSniperQc50   = 2, // All Sniper QC 50% (Scope + 3-Q-1, Delay 480ms)
        AllSniperQc75   = 3, // All Sniper QC 75% (Scope + 3-Q-1, Delay 245ms)
        KarNormal       = 4, // Kar98k Normal (Scope + 3-Q-1, Delay 890ms)
        KarQc50         = 5, // Kar98k QC 50% (Scope + 3-Q-1, Delay 590ms)
        KarQc75         = 6, // Kar98k QC 75% (Scope + 3-Q-1, Delay 300ms)
        SgNormal        = 7, // SG Normal (Fire + 3-1, Delay 750ms)
        SgQc50          = 8, // SG QC 50% (Fire + 3-1, Delay 480ms)
        SgQc75          = 9  // SG QC 75% (Fire + 3-1, Delay 245ms)
    }

    /// <summary>
    /// Engine Hexvyrr Macro Multi-Mode untuk Point Blank (No Recoil, All Sniper, Kar98k, SG).
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
        private volatile bool _isEnabled = false; // Default OFF saat pertama kali dijalankan
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
                        // Hanya intercept jika engine aktif DAN fokus berada di game Point Blank
                        if (_isEnabled && Win32Api.IsPointBlankForeground())
                        {
                            _isPhysicalLmbDown = true;
                            return (IntPtr)1; // Tahan sinyal fisik, delegasikan ke Hexvyrr macro loop
                        }
                        else
                        {
                            _isPhysicalLmbDown = false;
                        }
                    }
                    else if (msg == Win32Api.WM_LBUTTONUP)
                    {
                        if (_isPhysicalLmbDown)
                        {
                            _isPhysicalLmbDown = false;
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
                    // Jika engine nonaktif ATAU jendela aktif bukan Point Blank, stand-by
                    if (!_isEnabled || !Win32Api.IsPointBlankForeground())
                    {
                        if (_isFiring)
                        {
                            _isFiring = false;
                            OnFiringStateChanged?.Invoke(false);
                            ReleaseAllInputs();
                        }
                        _isPhysicalLmbDown = false;
                        Thread.Sleep(15);
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

                // ── ALL SNIPER (Konfigurasi Multi-QC Berbasis GHUB) ────────────────────────
                case MacroMode.AllSniperNormal:
                    // Sesuai referensi gambar GHUB No QC (82ms R-Down -> 80ms L-Down -> 85ms 3-Down -> 75ms 1-Down -> 85ms 3-Up -> 200ms 1-Up -> 600ms R-Up -> L-Up)
                    ExecuteSniperNoQcCycle();
                    break;
                case MacroMode.AllSniperQc50:
                    // Sesuai referensi gambar GHUB QC 50%: 25ms LMB -> 15ms Hold -> 1ms Release -> End Delay 480ms
                    ExecuteAllSniperCycle(fireMs: 25, keyHoldMs: 15, keyRelMs: 1, endRecoveryMs: 480);
                    break;
                case MacroMode.AllSniperQc75:
                    ExecuteAllSniperCycle(fireMs: 20, keyHoldMs: 10, keyRelMs: 1, endRecoveryMs: 245);
                    break;

                // ── KAR (Kar98k Scope + Fire + 3-Q-1, MS timing bawaan GHUB) ──
                case MacroMode.KarNormal:
                    ExecuteSniperNoQcCycle();
                    break;
                case MacroMode.KarQc50:
                    ExecuteKarCycle(590);
                    break;
                case MacroMode.KarQc75:
                    ExecuteKarCycle(300);
                    break;

                // ── SHOTGUN (Fire + 3-1 Quick Switch) ─────────────────────────
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
        /// Mode Sniper No QC Tahan (Sesuai Referensi Gambar GHUB):
        /// [R-KEY Down] -> 82ms -> [L-KEY Down] -> 80ms -> [3 Down] -> 85ms -> [1 Down] -> 75ms ->
        /// [3 Up] -> 85ms -> [1 Up] -> 200ms -> [R-KEY Up] -> 600ms -> [L-KEY Up]
        /// </summary>
        private void ExecuteSniperNoQcCycle()
        {
            // 1. R-KEY Down (Scope In RMB + Key J) -> 82ms
            Win32Api.SendRightMouseDown();
            Win32Api.SendKeyDown(Win32Api.VK_J);
            PreciseSleep(82);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 2. L-KEY Down (Fire LMB + Key N) -> 80ms
            Win32Api.SendMouseDown();
            Win32Api.SendKeyDown(Win32Api.VK_N);
            OnRecoilTick?.Invoke();
            PreciseSleep(80);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 3. Key 3 Down (Melee) -> 85ms
            Win32Api.SendKeyDown(Win32Api.VK_3);
            PreciseSleep(85);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 4. Key 1 Down (Primary) -> 75ms
            Win32Api.SendKeyDown(Win32Api.VK_1);
            PreciseSleep(75);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 5. Key 3 Up -> 85ms
            Win32Api.SendKeyUp(Win32Api.VK_3);
            PreciseSleep(85);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 6. Key 1 Up -> 200ms
            Win32Api.SendKeyUp(Win32Api.VK_1);
            PreciseSleep(200);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 7. R-KEY Up (RMB Up + Key J Up) -> 600ms
            Win32Api.SendRightMouseUp();
            Win32Api.SendKeyUp(Win32Api.VK_J);
            PreciseSleep(600);

            // 8. L-KEY Up (LMB Up + Key N Up)
            Win32Api.SendMouseUp();
            Win32Api.SendKeyUp(Win32Api.VK_N);
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
        /// Mode All Sniper Tahan (Sesuai Konfigurasi GHUB Multi-QC):
        /// [LMB/N Down] -> fireMs -> [3 Down] -> keyHoldMs -> [3 Up] -> keyRelMs ->
        /// [Q Down] -> keyHoldMs -> [Q Up] -> keyRelMs -> [1 Down] -> keyHoldMs -> [1 Up] -> keyRelMs ->
        /// [LMB/N Up] -> End Recovery Delay
        /// </summary>
        private void ExecuteAllSniperCycle(int fireMs, int keyHoldMs, int keyRelMs, int endRecoveryMs)
        {
            // 1. Fire (LMB Down + Key N) -> Tahan selama fireMs (default 25ms pada QC 50%)
            Win32Api.SendMouseDown();
            Win32Api.SendKeyDown(Win32Api.VK_N);
            OnRecoilTick?.Invoke();
            PreciseSleep(fireMs);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 2. Switch ke Melee (Key 3: Tahan keyHoldMs -> Lepas -> Jeda keyRelMs)
            Win32Api.SendKeyDown(Win32Api.VK_3);
            PreciseSleep(keyHoldMs);
            Win32Api.SendKeyUp(Win32Api.VK_3);
            PreciseSleep(keyRelMs);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 3. Quick Switch (Key Q: Tahan keyHoldMs -> Lepas -> Jeda keyRelMs)
            Win32Api.SendKeyDown(Win32Api.VK_Q);
            PreciseSleep(keyHoldMs);
            Win32Api.SendKeyUp(Win32Api.VK_Q);
            PreciseSleep(keyRelMs);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 4. Switch Primary Weapon (Key 1: Tahan keyHoldMs -> Lepas -> Jeda keyRelMs)
            Win32Api.SendKeyDown(Win32Api.VK_1);
            PreciseSleep(keyHoldMs);
            Win32Api.SendKeyUp(Win32Api.VK_1);
            PreciseSleep(keyRelMs);

            // 5. Release Fire (LMB Up + Key N Up)
            Win32Api.SendMouseUp();
            Win32Api.SendKeyUp(Win32Api.VK_N);

            // 6. Recovery Delay sebelum tembakan berikutnya
            PreciseSleep(endRecoveryMs);
        }

        /// <summary>
        /// Mode KAR (Kar98k) Tahan (Scope + Fire + 3-Q-1 Switch + Release + Delay):
        /// [RMB/J Down] -> 30ms Scope In -> [LMB/N Down] -> 25ms Fire -> [3 Down] -> 20ms -> [3 Up] -> 10ms ->
        /// [Q Down] -> 20ms -> [Q Up] -> 10ms -> [1 Down] -> 20ms -> [1 Up] -> 10ms ->
        /// [RMB/J Up] [LMB/N Up] -> End Recovery Delay (890ms / 590ms / 300ms)
        /// </summary>
        private void ExecuteKarCycle(int endRecoveryMs)
        {
            // 1. Scope In (RMB Down + Key J) -> Mengirim event mouse & hardware scancode key J
            Win32Api.SendRightMouseDown();
            Win32Api.SendKeyDown(Win32Api.VK_J);
            PreciseSleep(30);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 2. Fire (LMB Down + Key N) -> Tembak dalam status scoped
            Win32Api.SendMouseDown();
            Win32Api.SendKeyDown(Win32Api.VK_N);
            OnRecoilTick?.Invoke();
            PreciseSleep(25);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 3. Switch ke Melee (Key 3)
            Win32Api.SendKeyDown(Win32Api.VK_3);
            PreciseSleep(20);
            Win32Api.SendKeyUp(Win32Api.VK_3);
            PreciseSleep(10);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 4. Quick Switch (Key Q)
            Win32Api.SendKeyDown(Win32Api.VK_Q);
            PreciseSleep(20);
            Win32Api.SendKeyUp(Win32Api.VK_Q);
            PreciseSleep(10);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 5. Switch Primary Weapon (Key 1)
            Win32Api.SendKeyDown(Win32Api.VK_1);
            PreciseSleep(20);
            Win32Api.SendKeyUp(Win32Api.VK_1);
            PreciseSleep(10);

            // 6. Release Inputs (RMB + J + LMB + N)
            Win32Api.SendRightMouseUp();
            Win32Api.SendKeyUp(Win32Api.VK_J);
            Win32Api.SendMouseUp();
            Win32Api.SendKeyUp(Win32Api.VK_N);

            // 7. Recovery Delay sebelum tembakan berikutnya
            PreciseSleep(endRecoveryMs);
        }

        /// <summary>
        /// Mode SG Tahan (Fire + 3-1 Quick Switch + Delay):
        /// [LMB/N Down] -> 20ms -> [LMB/N Up] -> 10ms ->
        /// [3 Down] -> 20ms -> [3 Up] -> 10ms ->
        /// [1 Down] -> 20ms -> [1 Up] -> End Recovery Delay (750ms / 480ms / 245ms)
        /// </summary>
        private void ExecuteSgCycle(int endRecoveryMs)
        {
            // 1. Fire (LMB/N Down -> 20ms -> LMB/N Up)
            Win32Api.SendMouseDown();
            Win32Api.SendKeyDown(Win32Api.VK_N);
            OnRecoilTick?.Invoke();

            PreciseSleep(20);
            Win32Api.SendMouseUp();
            Win32Api.SendKeyUp(Win32Api.VK_N);
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
            Win32Api.SendKeyUp(Win32Api.VK_J);
            Win32Api.SendKeyUp(Win32Api.VK_N);
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
                if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) break;

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
