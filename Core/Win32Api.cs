using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PbRecoil.Core
{
    public static class Win32Api
    {
        // Virtual Key Codes
        public const int VK_LBUTTON = 0x01;
        public const int VK_RBUTTON = 0x02;
        public const int VK_F6 = 0x75;
        public const int VK_F7 = 0x76;
        public const int VK_F8 = 0x77;
        public const int VK_INSERT = 0x2D;
        public const int VK_DELETE = 0x2E;

        // Mouse Event Flags
        public const uint MOUSEEVENTF_MOVE = 0x0001;

        // Window Styles for Click-Through Transparent Overlay
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;

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

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        public const uint INPUT_MOUSE = 0;

        public static void SendMouseMove(int dx, int dy)
        {
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].mi.dx = dx;
            inputs[0].mi.dy = dy;
            inputs[0].mi.dwFlags = MOUSEEVENTF_MOVE;
            inputs[0].mi.time = 0;
            inputs[0].mi.dwExtraInfo = UIntPtr.Zero;

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
            if (GetWindowText(handle, sb, 256) > 0)
            {
                return sb.ToString();
            }
            return string.Empty;
        }
    }
}
