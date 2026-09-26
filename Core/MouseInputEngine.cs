using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PbRecoil.Core
{
    /// <summary>
    /// Kategori Senjata (Weapon Classification)
    /// </summary>
    public enum WeaponCategory
    {
        Assault,    // No Recoil Auto-Tap (AR / SMG)
        AugA3,      // AUG A3 / HBAR Precision Recoil
        AllSniper,  // All Sniper (CheyTac, L115A1, Tactilite, dll)
        Kar98k,     // Kar98k Scope + Quick Switch
        Shotgun     // Shotgun (Zombie Slayer, M1887, Cerberus, dll)
    }

    /// <summary>
    /// Level / Tingkat Kecepatan Quick Change (QC)
    /// </summary>
    public enum QcLevel
    {
        Normal,     // Standar / No QC
        Qc50,       // QC 50%
        Qc75        // QC 75%
    }

    /// <summary>
    /// Kategori & Mode Senjata Hexvyrr Macro
    /// </summary>
    public enum MacroMode
    {
        AssaultNoRecoil = 0, // No Recoil Auto-Tap (AR / SMG)
        AugA3           = 1, // AUG A3 / HBAR Precision Recoil (Dynamic Pull-Down + Rapid Fire)
        AllSniperNormal = 2, // All Sniper Normal (Scope + 3-Q-1, Delay 750ms)
        AllSniperQc50   = 3, // All Sniper QC 50% (Scope + 3-Q-1, Delay 480ms)
        AllSniperQc75   = 4, // All Sniper QC 75% (Fire [J] + Scope RMB + 3-1, Delay 245ms)
        KarNormal       = 5, // Kar98k Normal (Scope + 3-Q-1, Delay 890ms)
        KarQc50         = 6, // Kar98k QC 50% (Scope + 3-Q-1, Delay 590ms)
        KarQc75         = 7, // Kar98k QC 75% (Fire [J] + Scope RMB + 3-1, Delay 300ms)
        SgNormal        = 8, // SG Normal (Fire [J] + 3-1, Delay 750ms)
        SgQc50          = 9, // SG QC 50% (Fire [J] + 3-1, Delay 480ms)
        SgQc75          = 10 // SG QC 75% (Fire [J] + 3-1, Delay 245ms)
    }

    /// <summary>
    /// Engine Hexvyrr Macro Multi-Mode untuk Point Blank (No Recoil, AUG A3, All Sniper, Kar98k, SG).
    /// </summary>
    public class MouseInputEngine : IDisposable
    {
        public volatile MacroMode CurrentMode = MacroMode.AssaultNoRecoil;
        public volatile int HoldMs    = 20; // Durasi tahan penekanan LMB (ms) [Default: 20ms]
        public volatile int ReleaseMs = 0;  // Jeda antar penekanan LMB (ms) [Default: 0ms]
        public volatile int AugPullDown = 3; // Besaran kompensasi pull-down vertikal AUG (pixels per cycle) [0 = lurus / nonaktif, default 3]

        private readonly Win32Api.LowLevelMouseProc _hookProc;
        private IntPtr _hookHandle = IntPtr.Zero;

        private Thread? _workerThread;
        private volatile bool _isDisposed;
        private volatile bool _isEnabled = false; // Default OFF saat pertama kali dijalankan
        private volatile bool _isPhysicalLmbDown;
        private volatile bool _isFiring;

        // Stopwatch reusable — hindari alokasi object baru setiap PreciseSleep call (hot-path)
        private readonly Stopwatch _sw = new Stopwatch();

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

                case MacroMode.AugA3:
                    ExecuteAugCycle();
                    break;

                // ── ALL SNIPER (Konfigurasi Multi-QC Berbasis GHUB) ────────────────────────
                case MacroMode.AllSniperNormal:
                    // Sesuai referensi gambar GHUB No QC (82ms R-Down -> 80ms L-Down -> 85ms 3-Down -> 75ms 1-Down -> 85ms 3-Up -> 200ms 1-Up -> 600ms R-Up -> L-Up)
                    ExecuteSniperNoQcCycle();
                    break;
                case MacroMode.AllSniperQc50:
                    // Sesuai referensi gambar GHUB QC 50% (40ms R-Down -> 65ms L-Down -> 35ms 3-Down -> 35ms 1-Down -> 35ms 3-Up -> 168ms 1-Up -> 350ms R-Up -> L-Up)
                    ExecuteSniperQc50Cycle();
                    break;
                case MacroMode.AllSniperQc75:
                    ExecuteSniperFastQcCycle(fireMs: 20, rmbMs: 10, keyHoldMs: 10, keyRelMs: 2, endRecoveryMs: 245);
                    break;

                // ── KAR (Kar98k QC 75 Fast Cycle / Normal Multi-QC) ───────────
                case MacroMode.KarNormal:
                    ExecuteSniperNoQcCycle();
                    break;
                case MacroMode.KarQc50:
                    ExecuteSniperQc50Cycle();
                    break;
                case MacroMode.KarQc75:
                    ExecuteSniperFastQcCycle(fireMs: 20, rmbMs: 10, keyHoldMs: 10, keyRelMs: 2, endRecoveryMs: 300);
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
        /// Mode Sniper No QC Tahan (Sesuai Referensi Gambar GHUB No QC):
        /// [R-KEY Down] -> 82ms -> [L-KEY Down] -> 80ms -> [3 Down] -> 85ms -> [1 Down] -> 75ms ->
        /// [3 Up] -> 85ms -> [1 Up] -> 200ms -> [R-KEY Up] [L-KEY Up] -> Recovery Delay (600ms)
        /// </summary>
        private void ExecuteSniperNoQcCycle()
        {
            ReleaseAllInputs();

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

            // 7. Release Scope & Fire Inputs SEBELUM jeda unholster (mencegah auto-fire hipfire di PB)
            Win32Api.SendRightMouseUp();
            Win32Api.SendKeyUp(Win32Api.VK_J);
            Win32Api.SendMouseUp();
            Win32Api.SendKeyUp(Win32Api.VK_N);

            // 8. Recovery Delay unholster sniper sampai siap ditembakkan lagi (600ms)
            PreciseSleep(650);
        }

        /// <summary>
        /// Mode Sniper QC 50% Tahan (Sesuai Referensi Gambar GHUB QC 50%):
        /// [R-KEY Down] -> 40ms -> [L-KEY Down] -> 65ms -> [3 Down] -> 35ms -> [1 Down] -> 35ms ->
        /// [3 Up] -> 35ms -> [1 Up] -> 168ms -> [R-KEY Up] [L-KEY Up] -> Recovery Delay (350ms)
        /// </summary>
        private void ExecuteSniperQc50Cycle()
        {
            ReleaseAllInputs();

            // 1. R-KEY Down (Scope In RMB + Key J) -> 40ms
            Win32Api.SendRightMouseDown();
            Win32Api.SendKeyDown(Win32Api.VK_J);
            PreciseSleep(40);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 2. L-KEY Down (Fire LMB + Key N) -> 65ms
            Win32Api.SendMouseDown();
            Win32Api.SendKeyDown(Win32Api.VK_N);
            OnRecoilTick?.Invoke();
            PreciseSleep(65);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 3. Key 3 Down (Melee) -> 35ms
            Win32Api.SendKeyDown(Win32Api.VK_3);
            PreciseSleep(35);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 4. Key 1 Down (Primary) -> 35ms
            Win32Api.SendKeyDown(Win32Api.VK_1);
            PreciseSleep(35);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 5. Key 3 Up -> 35ms
            Win32Api.SendKeyUp(Win32Api.VK_3);
            PreciseSleep(35);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 6. Key 1 Up -> 168ms
            Win32Api.SendKeyUp(Win32Api.VK_1);
            PreciseSleep(168);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 7. Release Scope & Fire Inputs SEBELUM jeda unholster (mencegah auto-fire hipfire di PB)
            Win32Api.SendRightMouseUp();
            Win32Api.SendKeyUp(Win32Api.VK_J);
            Win32Api.SendMouseUp();
            Win32Api.SendKeyUp(Win32Api.VK_N);

            // 8. Recovery Delay unholster sniper sampai siap ditembakkan lagi (350ms)
            PreciseSleep(450);
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
        /// Mode AUG A3 / HBAR (Sesuai Arsitektur Skrip Macro Logitech):
        /// - Deteksi status Scoped in-game (RMB / Right Mouse Button).
        /// - Tahap 1 (Initial Burst): Sebanyak N tembakan (8 peluru scoped / 5 peluru hipfire),
        ///   kirim Fire (LMB + J) 59ms dengan kompensasi pull-down vertikal halus (Smooth Interpolated Glide),
        ///   lalu lepas (9ms). Nilai Y dapat diatur manual oleh user (AugPullDown).
        /// - Tahap 2 (Sustained Rapid Fire): Setelah burst awal selesai, tembakan dilanjutkan terus-menerus
        ///   dengan interval stabil (59ms down / 9ms up) tanpa pull-down agar crosshair tidak terseret ke bawah.
        /// </summary>
        private void ExecuteAugCycle()
        {
            ReleaseAllInputs();

            // 1. Deteksi status Scope in-game (RMB / Right Mouse Button)
            bool isScoped = Win32Api.IsKeyPressed(Win32Api.VK_RBUTTON);
            int loopCount = isScoped ? 8 : 5;

            // Nilai pull-down berbasis konfigurasi manual user:
            // Scoped: nilai penuh AugPullDown, Hipfire: proporsional (15/17)
            int pullValue = isScoped
                ? AugPullDown
                : Math.Max(0, (int)Math.Round(AugPullDown * 15.0 / 17.0));

            bool active = true;

            // Tahap 1: Initial Burst (8 peluru saat scope / 5 peluru saat hipfire) dengan smooth pull-down
            for (int i = 0; i < loopCount; i++)
            {
                if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground())
                {
                    active = false;
                    break;
                }

                Win32Api.SendMouseDown();
                Win32Api.SendKeyDown(Win32Api.VK_J);
                OnRecoilTick?.Invoke();

                if (pullValue <= 0)
                {
                    // Stay lurus tanpa pull-down
                    PreciseSleep(59);
                }
                else
                {
                    // Kompensasi pull-down vertikal halus secara mikro-step selama 59ms
                    const int steps = 5;
                    const int stepDuration = 11;
                    int movedSoFar = 0;

                    for (int s = 1; s <= steps; s++)
                    {
                        if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground())
                        {
                            active = false;
                            break;
                        }

                        int targetMove = (pullValue * s) / steps;
                        int deltaY = targetMove - movedSoFar;
                        if (deltaY > 0)
                        {
                            Win32Api.SendMouseMove(0, deltaY);
                            movedSoFar = targetMove;
                        }

                        int sleepMs = (s == steps) ? (59 - (stepDuration * (steps - 1))) : stepDuration;
                        PreciseSleep(sleepMs);
                    }
                }

                if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground())
                {
                    active = false;
                    break;
                }

                Win32Api.SendMouseUp();
                Win32Api.SendKeyUp(Win32Api.VK_J);

                PreciseSleep(9);
            }

            // Tahap 2: Sustained Rapid Fire selama tombol tembak tetap ditahan (tanpa pull-down tambahan)
            while (active && _isPhysicalLmbDown && _isEnabled && Win32Api.IsPointBlankForeground())
            {
                Win32Api.SendMouseDown();
                Win32Api.SendKeyDown(Win32Api.VK_J);
                OnRecoilTick?.Invoke();

                PreciseSleep(59);
                if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) break;

                Win32Api.SendMouseUp();
                Win32Api.SendKeyUp(Win32Api.VK_J);

                PreciseSleep(9);
            }

            ReleaseAllInputs();
        }

        /// <summary>
        /// Mode Sniper Fast QC (All Sniper QC 75% & Kar98k QC 75%):
        /// 1. Fire (LMB Down + Key J Down) -> Tahan fireMs (20ms) -> Lepas LMB & Key J -> jeda keyRelMs (2ms)
        /// 2. Scope Tap (RMB Down) -> rmbMs (10ms) -> RMB Up -> jeda keyRelMs (2ms)
        /// 3. Switch ke Melee (Key 3 Down) -> keyHoldMs (10ms) -> Key 3 Up -> jeda keyRelMs (2ms)
        /// 4. Switch ke Primary (Key 1 Down) -> keyHoldMs (10ms) -> Key 1 Up
        /// 5. End Recovery Delay (245ms untuk All Sniper / 300ms untuk Kar98k)
        /// </summary>
        private void ExecuteSniperFastQcCycle(int fireMs, int rmbMs, int keyHoldMs, int keyRelMs, int endRecoveryMs)
        {
            ReleaseAllInputs();

            // 1. Fire (LMB Down + Key J Down -> fireMs -> LMB Up + Key J Up)
            Win32Api.SendMouseDown();
            Win32Api.SendKeyDown(Win32Api.VK_J);
            OnRecoilTick?.Invoke();
            PreciseSleep(fireMs);
            Win32Api.SendMouseUp();
            Win32Api.SendKeyUp(Win32Api.VK_J);
            PreciseSleep(keyRelMs);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 2. Scope Tap (RMB Down -> rmbMs -> RMB Up)
            Win32Api.SendRightMouseDown();
            PreciseSleep(rmbMs);
            Win32Api.SendRightMouseUp();
            PreciseSleep(keyRelMs);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 3. Switch ke Melee (Key 3: Down -> keyHoldMs -> Up)
            Win32Api.SendKeyDown(Win32Api.VK_3);
            PreciseSleep(keyHoldMs);
            Win32Api.SendKeyUp(Win32Api.VK_3);
            PreciseSleep(keyRelMs);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 4. Switch Primary Weapon (Key 1: Down -> keyHoldMs -> Up)
            Win32Api.SendKeyDown(Win32Api.VK_1);
            PreciseSleep(keyHoldMs);
            Win32Api.SendKeyUp(Win32Api.VK_1);

            // 5. Recovery Delay sebelum siklus berikutnya
            PreciseSleep(endRecoveryMs);
        }

        /// <summary>
        /// Mode SG Tahan (Fire LMB + Key J + 3-1 Quick Switch + Delay):
        /// [LMB/J Down] -> 20ms -> [LMB/J Up] -> 10ms ->
        /// [3 Down] -> 20ms -> [3 Up] -> 10ms ->
        /// [1 Down] -> 20ms -> [1 Up] -> End Recovery Delay (750ms / 480ms / 245ms)
        /// </summary>
        private void ExecuteSgCycle(int endRecoveryMs)
        {
            ReleaseAllInputs();

            // 1. Fire (LMB/J Down -> 20ms -> LMB/J Up)
            Win32Api.SendMouseDown();
            Win32Api.SendKeyDown(Win32Api.VK_J);
            OnRecoilTick?.Invoke();

            PreciseSleep(20);
            Win32Api.SendMouseUp();
            Win32Api.SendKeyUp(Win32Api.VK_J);
            PreciseSleep(10);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

            // 2. Switch ke Melee (Key 3)
            Win32Api.SendKeyDown(Win32Api.VK_3);
            PreciseSleep(20);
            Win32Api.SendKeyUp(Win32Api.VK_3);
            PreciseSleep(10);
            if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) { ReleaseAllInputs(); return; }

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
            // Reuse instance Stopwatch agar tidak membuat object baru setiap panggilan (hot-path)
            _sw.Restart();
            while (_sw.ElapsedMilliseconds < ms)
            {
                if (!_isPhysicalLmbDown || !_isEnabled || !Win32Api.IsPointBlankForeground()) break;

                if (ms - _sw.ElapsedMilliseconds > 2)
                    Thread.Sleep(1);
                else
                    Thread.SpinWait(15);
            }
        }

        private static void PlayStatusBeep(bool enabled)
        {
            Task.Run(() =>
            {
                // Win32Api.Beep adalah native kernel32 call — lebih ringan dan tidak blocking Console I/O
                try
                {
                    if (enabled)
                        Win32Api.Beep(1000, 100);
                    else
                        Win32Api.Beep(450, 100);
                }
                catch { }
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
