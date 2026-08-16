using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PbRecoil.Core
{
    /// <summary>
    /// Engine Auto-Tap & Anti-Recoil Point Blank dengan algoritma hardcoded berkecepatan tinggi.
    /// Berjalan pada dedicated high-priority background thread dengan resolusi 1ms.
    /// </summary>
    public class MouseInputEngine : IDisposable
    {
        // ── Parameter Hardcode Optimal Point Blank ──────────────────────────────
        private const int ShotHoldMs         = 35; // Durasi penahanan klik agar 1 peluru tertembak sempurna
        private const int ReleaseRecoveryMs  = 35; // Jeda pelepasan klik agar akurasi crosshair reset (anti-spread)
        private const int VerticalPullPixels = 5;  // Kekuatan tarikan recoil vertikal ke bawah per shot (px)
        private const int SmoothSteps        = 2;  // Pembagian langkah tarikan agar pergerakan mouse mulus
        private const int JitterRange        = 1;  // Humanizer jitter acak (-1 sampai +1 px) untuk bypass heurisitk

        private readonly Random _random = new();
        private Thread? _workerThread;
        private volatile bool _isDisposed;
        private volatile bool _isEnabled;
        private volatile bool _isFiring;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnStateChanged?.Invoke(_isEnabled);
                    PlayStatusBeep(_isEnabled);
                }
            }
        }

        public bool IsFiring => _isFiring;

        public event Action<bool>? OnStateChanged;
        public event Action<bool>? OnFiringStateChanged;
        public event Action? OnRecoilTick;

        public void Start()
        {
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

                    // Deteksi kondisi fisik apakah Tombol Mouse Kiri (LMB) sedang ditekan/ditahan (HOLD)
                    var isLmbPressed = Win32Api.IsKeyPressed(Win32Api.VK_LBUTTON);

                    if (isLmbPressed)
                    {
                        if (!_isFiring)
                        {
                            _isFiring = true;
                            OnFiringStateChanged?.Invoke(true);
                        }

                        // Eksekusi 1 siklus Auto-Tap + Recoil Pull
                        ExecuteTapCycle();
                    }
                    else
                    {
                        if (_isFiring)
                        {
                            _isFiring = false;
                            OnFiringStateChanged?.Invoke(false);
                            // Safety release saat tombol fisik dilepas pemain
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
        /// Mengeksekusi satu siklus lengkap tembakan tap:
        /// 1. Trigger Klik Kiri (LMB Down)
        /// 2. Tahan ~35ms untuk pelepasan 1 peluru
        /// 3. Tarik recoil ke bawah dengan smoothing + jitter
        /// 4. Lepas Klik Kiri (LMB Up)
        /// 5. Jeda pemulihan ~35ms agar crosshair bloom reset
        /// </summary>
        private void ExecuteTapCycle()
        {
            // 1. Simulasikan klik tembak
            Win32Api.SendMouseDown();
            OnRecoilTick?.Invoke();

            // 2. Durasi peluru melesat keluar
            PreciseSleep(ShotHoldMs);

            // 3. Tarik recoil vertikal dengan pembagian smooth step
            var subY = (double)VerticalPullPixels / SmoothSteps;
            var stepDelay = Math.Max(1, 10 / SmoothSteps);
            double accY = 0;

            for (int i = 0; i < SmoothSteps; i++)
            {
                if (!_isEnabled || !Win32Api.IsKeyPressed(Win32Api.VK_LBUTTON)) break;

                accY += subY;
                var dy = (int)Math.Round(accY);
                accY -= dy;

                // Tambahkan random humanizer jitter
                var jitterX = _random.Next(-JitterRange, JitterRange + 1);
                var jitterY = _random.Next(-JitterRange, JitterRange + 1);

                Win32Api.SendMouseMove(jitterX, dy + jitterY);
                PreciseSleep(stepDelay);
            }

            // 4. Lepas klik tembak untuk menghentikan peluru liar / spread
            Win32Api.SendMouseUp();

            // 5. Jeda recovery sebelum tap berikutnya dimulai
            PreciseSleep(ReleaseRecoveryMs);
        }

        private static void PreciseSleep(int ms)
        {
            if (ms <= 0) return;
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms)
            {
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
            Win32Api.SendMouseUp();
        }
    }
}
