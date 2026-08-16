using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using PbRecoil.Models;

namespace PbRecoil.Core
{
    public class MouseInputEngine : IDisposable
    {
        private readonly Random _random = new();
        private Thread? _workerThread;
        private volatile bool _isDisposed;
        private volatile bool _isEnabled;
        private WeaponPreset? _activePreset;

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

        public WeaponPreset? ActivePreset
        {
            get => _activePreset;
            set => _activePreset = value;
        }

        public event Action<bool>? OnStateChanged;
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
            var stopwatch = new Stopwatch();

            try
            {
                while (!_isDisposed)
                {
                    if (!_isEnabled || _activePreset == null)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    var isLmbPressed = Win32Api.IsKeyPressed(Win32Api.VK_LBUTTON);
                    var isRmbPressed = Win32Api.IsKeyPressed(Win32Api.VK_RBUTTON);

                    // Evaluasi kondisi tembak
                    var shouldTrigger = isLmbPressed && (!_activePreset.ScopeOnly || isRmbPressed);

                    if (shouldTrigger)
                    {
                        ExecuteRecoilStep(_activePreset);
                        OnRecoilTick?.Invoke();
                    }
                    else
                    {
                        // Polling idle cepat saat tombol dilepas
                        Thread.Sleep(1);
                    }
                }
            }
            finally
            {
                Win32Api.TimeEndPeriod(1);
            }
        }

        private void ExecuteRecoilStep(WeaponPreset preset)
        {
            var totalY = preset.VerticalRecoil;
            var totalX = preset.HorizontalRecoil;
            var steps = Math.Max(1, preset.SmoothStep);
            var delayMs = Math.Max(1, preset.DelayMs);
            var stepDelay = Math.Max(1, delayMs / steps);

            var subY = (double)totalY / steps;
            var subX = (double)totalX / steps;

            double accumulatedY = 0;
            double accumulatedX = 0;

            for (int i = 0; i < steps; i++)
            {
                // Cek ulang apakah tombol masih ditekan di tengah pembagian step
                if (!Win32Api.IsKeyPressed(Win32Api.VK_LBUTTON) || !_isEnabled)
                {
                    break;
                }

                accumulatedY += subY;
                accumulatedX += subX;

                var dy = (int)Math.Round(accumulatedY);
                var dx = (int)Math.Round(accumulatedX);

                accumulatedY -= dy;
                accumulatedX -= dx;

                // Penambahan jitter alami (humanizer)
                if (preset.Jitter > 0)
                {
                    var jitterOffset = _random.Next(-preset.Jitter, preset.Jitter + 1);
                    dy += jitterOffset;
                }

                if (dx != 0 || dy != 0)
                {
                    Win32Api.SendMouseMove(dx, dy);
                }

                PreciseSleep(stepDelay);
            }
        }

        private static void PreciseSleep(int ms)
        {
            if (ms <= 0) return;
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms)
            {
                if (ms - sw.ElapsedMilliseconds > 2)
                {
                    Thread.Sleep(1);
                }
                else
                {
                    Thread.SpinWait(10);
                }
            }
        }

        private static void PlayStatusBeep(bool enabled)
        {
            Task.Run(() =>
            {
                try
                {
                    if (enabled)
                    {
                        Console.Beep(1000, 120);
                    }
                    else
                    {
                        Console.Beep(450, 120);
                    }
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
            _isEnabled = false;
        }
    }
}
