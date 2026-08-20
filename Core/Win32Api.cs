using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PbRecoil.Core
{
    public static class Win32Api
    {
        // ── Custom Magic Signature untuk menandai injected clicks ─────────────
        public static readonly UIntPtr INJECTED_SIGNATURE = (UIntPtr)0x50425243; // "PBRC"

        // ── Cached buffers untuk hot-path (hindari heap alloc berulang) ───────
        // IsPointBlankForeground dipanggil ribuan kali/menit dari WorkerLoop & PreciseSleep
        private static readonly System.Text.StringBuilder _titleBuffer = new System.Text.StringBuilder(256);
        private static readonly object _titleBufferLock = new object();

        // Cache PID foreground window terakhir agar tidak terus-menerus membuka Process handle
        private static uint   _lastFgPid;
        private static bool   _lastPidIsGame;
        private static IntPtr _lastFgHwnd;

        // ── Mouse Button Virtual Keys ──────────────────────────────────────────
        public const int VK_LBUTTON = 0x01;
        public const int VK_RBUTTON = 0x02;

        // ── Keyboard Virtual Keys (Quick Switch & Weapons) ────────────────────
        public const byte VK_1 = 0x31; // Primary Weapon ('1')
        public const byte VK_3 = 0x33; // Melee / Knife ('3')
        public const byte VK_Q = 0x51; // Quick Switch ('Q')
        public const byte VK_J = 0x4A; // Secondary Scope Key ('J')
        public const byte VK_N = 0x4E; // Secondary Fire Key ('N')

        // ── Function & Arrow Key Virtual Keys ─────────────────────────────────
        public const int VK_LEFT    = 0x25;
        public const int VK_UP      = 0x26;
        public const int VK_RIGHT   = 0x27;
        public const int VK_DOWN    = 0x28;
        public const int VK_F1      = 0x70;
        public const int VK_F2      = 0x71;
        public const int VK_F3      = 0x72;
        public const int VK_F4      = 0x73;

        // ── Mouse & Keyboard Event Flags ───────────────────────────────────────
        public const uint MOUSEEVENTF_MOVE      = 0x0001;
        public const uint MOUSEEVENTF_LEFTDOWN  = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP    = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP   = 0x0010;

        public const uint KEYEVENTF_KEYDOWN = 0x0000;
        public const uint KEYEVENTF_KEYUP   = 0x0002;

        // ── Windows Hook & Messages ────────────────────────────────────────────
        public const int WH_MOUSE_LL    = 14;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP   = 0x0202;

        // ── Window Style Constants (Click-Through Overlay) ─────────────────────
        public const int GWL_EXSTYLE       = -20;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_LAYERED     = 0x00080000;
        public const int WS_EX_TOOLWINDOW  = 0x00000080;
        public const int WS_EX_NOACTIVATE  = 0x08000000;

        // ── Delegates ───────────────────────────────────────────────────────────
        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        // ── P/Invoke Declarations ───────────────────────────────────────────────
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]
        public static extern uint TimeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]
        public static extern uint TimeEndPeriod(uint uMilliseconds);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("kernel32.dll")]
        public static extern bool Beep(uint dwFreq, uint dwDuration);

        // ── Structs ─────────────────────────────────────────────────────────────
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;

            public int Width => right - left;
            public int Height => bottom - top;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT   pt;
            public uint    mouseData;
            public uint    flags; // LLMHF_INJECTED = 0x00000001
            public uint    time;
            public UIntPtr dwExtraInfo;
        }

        // ── Extended User32 P/Invokes for Window & Display Tracking ────────────
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        public const int SM_CXSCREEN = 0;
        public const int SM_CYSCREEN = 1;

        // ── Mouse & Keyboard Simulation Helpers ─────────────────────────────────

        public static void SendMouseMove(int dx, int dy)
        {
            mouse_event(MOUSEEVENTF_MOVE, dx, dy, 0, INJECTED_SIGNATURE);
        }

        public static void SendMouseDown()
        {
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, INJECTED_SIGNATURE);
        }

        public static void SendMouseUp()
        {
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, INJECTED_SIGNATURE);
        }

        public static void SendRightMouseDown()
        {
            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, INJECTED_SIGNATURE);
        }

        public static void SendRightMouseUp()
        {
            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, INJECTED_SIGNATURE);
        }

        public static void SendKeyDown(byte vKey)
        {
            byte scanCode = (byte)MapVirtualKey(vKey, 0);
            keybd_event(vKey, scanCode, KEYEVENTF_KEYDOWN, INJECTED_SIGNATURE);
        }

        public static void SendKeyUp(byte vKey)
        {
            byte scanCode = (byte)MapVirtualKey(vKey, 0);
            keybd_event(vKey, scanCode, KEYEVENTF_KEYUP, INJECTED_SIGNATURE);
        }

        public static bool IsKeyPressed(int vKey)
        {
            return (GetAsyncKeyState(vKey) & 0x8000) != 0;
        }

        public static string GetActiveWindowTitle()
        {
            var handle = GetForegroundWindow();
            if (handle == IntPtr.Zero) return string.Empty;

            lock (_titleBufferLock)
            {
                _titleBuffer.Clear();
                return GetWindowText(handle, _titleBuffer, 256) > 0 ? _titleBuffer.ToString() : string.Empty;
            }
        }

        // ── Window Positioning & Z-Order (Topmost Enforcement) ─────────────────
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE     = 0x0001;
        public const uint SWP_NOMOVE     = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        public struct GameWindowInfo
        {
            public int X;
            public int Y;
            public int Width;
            public int Height;
            public bool IsGameFound;
        }

        /// <summary>
        /// Memaksa handle window agar selalu berada di posisi Z-Order teratas (HWND_TOPMOST) tanpa mencuri fokus.
        /// </summary>
        public static void EnsureTopmost(IntPtr hWnd)
        {
            if (hWnd != IntPtr.Zero)
            {
                SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        /// <summary>
        /// Memeriksa apakah window yang sedang aktif / fokus di layar saat ini adalah game Point Blank.
        /// </summary>
        public static bool IsPointBlankForeground()
        {
            var fgHwnd = GetForegroundWindow();
            if (fgHwnd == IntPtr.Zero) return false;

            // 1. Cek judul window foreground — reuse static buffer untuk hindari GC pressure
            lock (_titleBufferLock)
            {
                _titleBuffer.Clear();
                if (GetWindowText(fgHwnd, _titleBuffer, 256) > 0)
                {
                    var title = _titleBuffer.ToString();
                    if (title.StartsWith("Point Blank", StringComparison.OrdinalIgnoreCase) ||
                        title.StartsWith("PointBlank", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // 2. Cache PID foreground: hanya buka Process handle jika HWND berubah
            GetWindowThreadProcessId(fgHwnd, out uint pid);
            if (pid == 0) return false;

            if (fgHwnd == _lastFgHwnd && pid == _lastFgPid)
                return _lastPidIsGame;

            bool isGame = false;
            try
            {
                using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                string procName = proc.ProcessName;
                isGame = procName.Equals("PointBlank", StringComparison.OrdinalIgnoreCase) ||
                         procName.Equals("PointBlank_ID", StringComparison.OrdinalIgnoreCase) ||
                         procName.Equals("PB", StringComparison.OrdinalIgnoreCase) ||
                         procName.StartsWith("PointBlank", StringComparison.OrdinalIgnoreCase);
            }
            catch { }

            // Simpan ke cache
            _lastFgHwnd    = fgHwnd;
            _lastFgPid     = pid;
            _lastPidIsGame = isGame;

            return isGame;
        }

        /// <summary>
        /// Mencari handle window game Point Blank secara aktif (baik dari Foreground maupun FindWindow).
        /// </summary>
        public static IntPtr FindGameWindow()
        {
            // 1. Cek Foreground Window terlebih dahulu — reuse static buffer
            var fgHwnd = GetForegroundWindow();
            if (fgHwnd != IntPtr.Zero)
            {
                lock (_titleBufferLock)
                {
                    _titleBuffer.Clear();
                    if (GetWindowText(fgHwnd, _titleBuffer, 256) > 0)
                    {
                        var title = _titleBuffer.ToString();
                        if (title.Contains("Point Blank", StringComparison.OrdinalIgnoreCase) ||
                            title.Contains("PointBlank", StringComparison.OrdinalIgnoreCase))
                        {
                            return fgHwnd;
                        }
                    }
                }
            }

            // 2. Coba cari dengan title "Point Blank" atau class game engine PB
            var hwnd = FindWindow(null, "Point Blank");
            if (hwnd != IntPtr.Zero) return hwnd;

            hwnd = FindWindow(null, "PointBlank");
            if (hwnd != IntPtr.Zero) return hwnd;

            return IntPtr.Zero;
        }

        /// <summary>
        /// Mengambil informasi posisi dan dimensi window game Point Blank jika aktif, atau Primary Screen.
        /// </summary>
        public static GameWindowInfo GetGameOrScreenBounds()
        {
            var gameHwnd = FindGameWindow();
            if (gameHwnd != IntPtr.Zero)
            {
                if (GetClientRect(gameHwnd, out RECT clientRect) && clientRect.Width > 100 && clientRect.Height > 100)
                {
                    var ptTopLeft = new POINT { x = clientRect.left, y = clientRect.top };
                    ClientToScreen(gameHwnd, ref ptTopLeft);

                    return new GameWindowInfo
                    {
                        X = ptTopLeft.x,
                        Y = ptTopLeft.y,
                        Width = clientRect.Width,
                        Height = clientRect.Height,
                        IsGameFound = true
                    };
                }
            }

            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);

            return new GameWindowInfo
            {
                X = 0,
                Y = 0,
                Width = screenWidth > 0 ? screenWidth : 1920,
                Height = screenHeight > 0 ? screenHeight : 1080,
                IsGameFound = false
            };
        }

        /// <summary>
        /// Mengambil koordinat titik tengah pixel fisik layar (Game Window jika sedang aktif/windowed, atau Primary Screen).
        /// </summary>
        public static POINT GetGameOrScreenCenter()
        {
            var bounds = GetGameOrScreenBounds();
            return new POINT
            {
                x = bounds.X + (bounds.Width / 2),
                y = bounds.Y + (bounds.Height / 2)
            };
        }
    }
}
