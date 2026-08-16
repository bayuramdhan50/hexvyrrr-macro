using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PbRecoil.Core
{
    public static class Win32Api
    {
        // ── Mouse Button Virtual Keys ──────────────────────────────────────────
        public const int VK_LBUTTON = 0x01;
        public const int VK_RBUTTON = 0x02;

        // ── Function Key Virtual Keys ──────────────────────────────────────────
        public const int VK_F1 = 0x70;
        public const int VK_F2 = 0x71;

        // ── Mouse Event Flags ──────────────────────────────────────────────────
        public const uint MOUSEEVENTF_MOVE     = 0x0001;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP   = 0x0004;

        // ── Windows Hook & Messages ────────────────────────────────────────────
        public const int WH_MOUSE_LL    = 14;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP   = 0x0202;

        // ── Input Type Constants ───────────────────────────────────────────────
        public const uint INPUT_MOUSE = 0;

        // ── Window Style Constants (Click-Through Overlay) ─────────────────────
        public const int GWL_EXSTYLE       = -20;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_LAYERED     = 0x00080000;
        public const int WS_EX_TOOLWINDOW  = 0x00000080;

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

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        // ── Structs ─────────────────────────────────────────────────────────────
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
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

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int     dx;
            public int     dy;
            public uint    mouseData;
            public uint    dwFlags;
            public uint    time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort  wVk;
            public ushort  wScan;
            public uint    dwFlags;
            public uint    time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct INPUT
        {
            [FieldOffset(0)] public uint       type;
            [FieldOffset(4)] public MOUSEINPUT mi;
            [FieldOffset(4)] public KEYBDINPUT ki;
        }

        // ── Mouse Simulation Helpers ────────────────────────────────────────────

        public static void SendMouseMove(int dx, int dy)
        {
            var inputs = new INPUT[1];
            inputs[0].type       = INPUT_MOUSE;
            inputs[0].mi.dx      = dx;
            inputs[0].mi.dy      = dy;
            inputs[0].mi.dwFlags = MOUSEEVENTF_MOVE;

            SendInput(1, inputs, Marshal.SizeOf<INPUT>());
        }

        public static void SendMouseDown()
        {
            var inputs = new INPUT[1];
            inputs[0].type       = INPUT_MOUSE;
            inputs[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;

            SendInput(1, inputs, Marshal.SizeOf<INPUT>());
        }

        public static void SendMouseUp()
        {
            var inputs = new INPUT[1];
            inputs[0].type       = INPUT_MOUSE;
            inputs[0].mi.dwFlags = MOUSEEVENTF_LEFTUP;

            SendInput(1, inputs, Marshal.SizeOf<INPUT>());
        }

        public static bool IsKeyPressed(int vKey)
        {
            return (GetAsyncKeyState(vKey) & 0x8000) != 0;
        }

        public static string GetActiveWindowTitle()
        {
            var handle = GetForegroundWindow();
            if (handle == IntPtr.Zero) return string.Empty;

            var sb = new StringBuilder(256);
            return GetWindowText(handle, sb, 256) > 0 ? sb.ToString() : string.Empty;
        }
    }
}
