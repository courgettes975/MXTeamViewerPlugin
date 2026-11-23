namespace MXTeamViewer.Agent
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    internal static class Native
    {
        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT rect, int size);

        public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        public const uint PW_RENDERFULLCONTENT = 0x2;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
            public int Width => this.Right - this.Left;
            public int Height => this.Bottom - this.Top;
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
    }
}
