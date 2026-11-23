namespace MXTeamViewer.Agent
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;

    // Captures one window (the TeamViewer remote session window = the remote screen as displayed)
    // into a PNG. PrintWindow(PW_RENDERFULLCONTENT) works for minimised/occluded DirectX windows;
    // when it returns a blank image we fall back to copying the window rectangle from the screen.
    internal static class WindowCapture
    {
        public static bool Save(IntPtr hwnd, string path, out string info)
        {
            if (!Native.IsWindow(hwnd))
            {
                info = "invalid window";
                return false;
            }
            if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out var rect, System.Runtime.InteropServices.Marshal.SizeOf<Native.RECT>()) != 0)
            {
                Native.GetWindowRect(hwnd, out rect);
            }
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                info = "window has no size (minimised?)";
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
            var method = "PrintWindow";
            using (var g = Graphics.FromImage(bmp))
            {
                var hdc = g.GetHdc();
                var ok = Native.PrintWindow(hwnd, hdc, Native.PW_RENDERFULLCONTENT);
                g.ReleaseHdc(hdc);
                if (!ok || IsBlank(bmp))
                {
                    if (Native.IsIconic(hwnd))
                    {
                        info = "window minimised and PrintWindow returned nothing";
                        return false;
                    }
                    g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(rect.Width, rect.Height));
                    method = "CopyFromScreen";
                }
            }
            bmp.Save(path, ImageFormat.Png);
            info = $"{method} {rect.Width}x{rect.Height}";
            return true;
        }

        // Samples a grid of pixels: all identical => considered blank.
        private static bool IsBlank(Bitmap bmp)
        {
            var first = bmp.GetPixel(0, 0);
            for (var y = 0; y < bmp.Height; y += Math.Max(1, bmp.Height / 12))
            {
                for (var x = 0; x < bmp.Width; x += Math.Max(1, bmp.Width / 12))
                {
                    if (bmp.GetPixel(x, y) != first)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
