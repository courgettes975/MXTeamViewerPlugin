namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;

    internal static class Win32
    {
        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder text, int max);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

        public const int SW_RESTORE = 9;
        public const int SW_SHOW = 5;
        public const int SW_MAXIMIZE = 3;
        public const uint WM_CLOSE = 0x0010;
        public const uint WM_MOUSEWHEEL = 0x020A;
        public const int WHEEL_DELTA = 120;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        public static List<IntPtr> TopLevelWindows()
        {
            var list = new List<IntPtr>();
            EnumWindows((h, _) => { list.Add(h); return true; }, IntPtr.Zero);
            return list;
        }

        public static uint ProcessIdOf(IntPtr hwnd)
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            return pid;
        }

        public static string TitleOf(IntPtr hwnd)
        {
            var sb = new StringBuilder(512);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static string ClassOf(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        // Restores a minimised window and brings it to the foreground. Windows restricts
        // SetForegroundWindow for background processes; attaching to the current foreground
        // thread's input queue is the documented-behaviour workaround (no simulated keystroke).
        public static bool Activate(IntPtr hwnd)
        {
            if (!IsWindow(hwnd))
            {
                return false;
            }
            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, SW_RESTORE);
            }
            else
            {
                ShowWindow(hwnd, SW_SHOW);
            }
            if (GetForegroundWindow() == hwnd)
            {
                return true;
            }
            var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
            var current = GetCurrentThreadId();
            var attached = foregroundThread != 0 && foregroundThread != current && AttachThreadInput(current, foregroundThread, true);
            try
            {
                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
            }
            finally
            {
                if (attached)
                {
                    AttachThreadInput(current, foregroundThread, false);
                }
            }
            return GetForegroundWindow() == hwnd;
        }

        public static void PostWheel(IntPtr hwnd, int detents)
        {
            GetWindowRect(hwnd, out var r);
            var x = (r.Left + r.Right) / 2;
            var y = (r.Top + r.Bottom) / 2;
            var lParam = (IntPtr)((y << 16) | (x & 0xFFFF));
            var wParam = (IntPtr)((detents * WHEEL_DELTA) << 16);
            PostMessage(hwnd, WM_MOUSEWHEEL, wParam, lParam);
        }

        // ---- keyboard ----------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData, dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk, wScan;
            public uint dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x2;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x1;

        // Presses the modifiers, then the key, then releases everything in reverse order.
        public static bool SendChord(IReadOnlyList<ushort> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return false;
            }
            var inputs = new List<INPUT>();
            foreach (var vk in keys)
            {
                inputs.Add(Key(vk, false));
            }
            for (var i = keys.Count - 1; i >= 0; i--)
            {
                inputs.Add(Key(keys[i], true));
            }
            var arr = inputs.ToArray();
            return SendInput((uint)arr.Length, arr, Marshal.SizeOf<INPUT>()) == arr.Length;
        }

        private static INPUT Key(ushort vk, bool up)
        {
            var extended = vk is >= 0x21 and <= 0x2E; // navigation block
            return new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        dwFlags = (up ? KEYEVENTF_KEYUP : 0) | (extended ? KEYEVENTF_EXTENDEDKEY : 0),
                    },
                },
            };
        }
    }
}
