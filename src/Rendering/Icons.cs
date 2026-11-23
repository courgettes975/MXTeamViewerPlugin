namespace Loupedeck.MXTeamViewerPlugin
{
    using System;

    public enum Glyph
    {
        TeamViewer, NewSession, QuickConnect, Desktop, Laptop, Server, Client, Star,
        Mic, Speaker, Record, Stop, Folder, Camera, Fullscreen, ZoomIn, ZoomOut, Zoom100,
        Disconnect, Meeting, Diagnostics, Test, Dial, VolumeUp, VolumeDown, Empty,
    }

    internal static class Palette
    {
        public static readonly BitmapColor Background = new(18, 20, 24);
        public static readonly BitmapColor Text = new(235, 238, 242);
        public static readonly BitmapColor Secondary = new(150, 156, 166);
        public static readonly BitmapColor Grey = new(95, 100, 110);
        public static readonly BitmapColor Blue = new(14, 140, 255);
        public static readonly BitmapColor Green = new(46, 204, 113);
        public static readonly BitmapColor Red = new(231, 60, 60);
        public static readonly BitmapColor DarkRed = new(120, 18, 18);
        public static readonly BitmapColor Orange = new(245, 160, 40);
        public static readonly BitmapColor White = new(255, 255, 255);
    }

    // Every key image is drawn at runtime with BitmapBuilder primitives (no bitmap assets, crisp at
    // every LCD size, no TeamViewer trademark artwork). Layout: glyph in the top ~70 %, label below.
    internal static class Icons
    {
        public sealed class Spec
        {
            public Glyph Glyph;
            public string Label = "";
            public BitmapColor Fg = Palette.Text;
            public BitmapColor Bg = Palette.Background;
            public bool Strike;
            public BitmapColor? Frame;
            public string Badge = "";      // small text top-right (e.g. "N/D", "x2")
        }

        public static BitmapImage Render(PluginImageSize size, Spec spec)
        {
            using var b = new BitmapBuilder(size);
            var w = b.Width;
            var h = b.Height;
            var s = Math.Min(w, h);
            b.Clear(spec.Bg);

            if (spec.Frame.HasValue)
            {
                var t = Math.Max(2, (int)(s * 0.04f));
                b.FillRectangle(0, 0, w, t, spec.Frame.Value);
                b.FillRectangle(0, h - t, w, t, spec.Frame.Value);
                b.FillRectangle(0, 0, t, h, spec.Frame.Value);
                b.FillRectangle(w - t, 0, t, h, spec.Frame.Value);
            }

            var hasLabel = !string.IsNullOrEmpty(spec.Label);
            var cx = w / 2f;
            var cy = hasLabel ? h * 0.40f : h * 0.5f;
            var r = s * (hasLabel ? 0.26f : 0.32f);
            DrawGlyph(b, spec.Glyph, cx, cy, r, spec.Fg, spec.Bg);

            if (spec.Strike)
            {
                b.DrawLine(cx - r * 1.05f, cy - r * 1.05f, cx + r * 1.05f, cy + r * 1.05f, spec.Bg, Math.Max(4f, s * 0.08f));
                b.DrawLine(cx - r * 1.05f, cy - r * 1.05f, cx + r * 1.05f, cy + r * 1.05f, Palette.Red, Math.Max(2.5f, s * 0.045f));
            }

            if (hasLabel)
            {
                var font = FitFont(spec.Label, s);
                b.DrawText(spec.Label, 0, (int)(h * 0.74f), w, (int)(h * 0.22f), spec.Bg.Equals(Palette.Red) ? Palette.White : Palette.Text, font);
            }
            if (!string.IsNullOrEmpty(spec.Badge))
            {
                b.DrawText(spec.Badge, (int)(w * 0.50f), 0, (int)(w * 0.48f), (int)(h * 0.20f), Palette.Orange, Math.Max(8, (int)(s * 0.12f)));
            }
            return b.ToImage();
        }

        private static int FitFont(string text, int s)
        {
            var size = s * 0.15f;
            if (text.Length > 9)
            {
                size *= 9f / text.Length;
            }
            return Math.Max(8, (int)size);
        }

        private static void Rect(BitmapBuilder b, float x, float y, float w, float h, BitmapColor c, float t)
        {
            b.DrawLine(x, y, x + w, y, c, t);
            b.DrawLine(x + w, y, x + w, y + h, c, t);
            b.DrawLine(x + w, y + h, x, y + h, c, t);
            b.DrawLine(x, y + h, x, y, c, t);
        }

        private static void Ring(BitmapBuilder b, float cx, float cy, float r, BitmapColor c, BitmapColor bg, float t)
        {
            b.FillCircle(cx, cy, r, c);
            b.FillCircle(cx, cy, r - t, bg);
        }

        private static void Arrow(BitmapBuilder b, float x1, float y1, float x2, float y2, BitmapColor c, float t, float head)
        {
            b.DrawLine(x1, y1, x2, y2, c, t);
            var a = Math.Atan2(y2 - y1, x2 - x1);
            for (var k = -1; k <= 1; k += 2)
            {
                var aa = a + Math.PI + k * 0.55;
                b.DrawLine(x2, y2, x2 + (float)(Math.Cos(aa) * head), y2 + (float)(Math.Sin(aa) * head), c, t);
            }
        }

        private static void DrawGlyph(BitmapBuilder b, Glyph g, float cx, float cy, float r, BitmapColor fg, BitmapColor bg)
        {
            var t = Math.Max(2f, r * 0.14f);
            switch (g)
            {
                case Glyph.TeamViewer:
                    // Generic "remote" pictogram: blue disc + two opposed arrows (not the trademark logo).
                    b.FillCircle(cx, cy, r, Palette.Blue);
                    Arrow(b, cx - r * 0.65f, cy - r * 0.22f, cx + r * 0.62f, cy - r * 0.22f, Palette.White, t, r * 0.30f);
                    Arrow(b, cx + r * 0.65f, cy + r * 0.22f, cx - r * 0.62f, cy + r * 0.22f, Palette.White, t, r * 0.30f);
                    break;
                case Glyph.NewSession:
                    Monitor(b, cx, cy, r, fg, t);
                    b.DrawLine(cx - r * 0.3f, cy - r * 0.2f, cx + r * 0.3f, cy - r * 0.2f, Palette.Blue, t);
                    b.DrawLine(cx, cy - r * 0.5f, cx, cy + r * 0.1f, Palette.Blue, t);
                    break;
                case Glyph.QuickConnect:
                    Monitor(b, cx, cy, r, fg, t);
                    Arrow(b, cx - r * 0.35f, cy - r * 0.2f, cx + r * 0.35f, cy - r * 0.2f, Palette.Blue, t, r * 0.22f);
                    break;
                case Glyph.Desktop:
                    Monitor(b, cx, cy, r, fg, t);
                    break;
                case Glyph.Laptop:
                    Rect(b, cx - r * 0.7f, cy - r * 0.6f, r * 1.4f, r * 0.95f, fg, t);
                    b.DrawLine(cx - r, cy + r * 0.55f, cx + r, cy + r * 0.55f, fg, t * 1.4f);
                    break;
                case Glyph.Server:
                    for (var i = 0; i < 3; i++)
                    {
                        var y = cy - r * 0.9f + i * r * 0.65f;
                        Rect(b, cx - r * 0.8f, y, r * 1.6f, r * 0.5f, fg, t);
                        b.FillCircle(cx + r * 0.5f, y + r * 0.25f, t * 0.8f, Palette.Green);
                    }
                    break;
                case Glyph.Client:
                    b.FillCircle(cx, cy - r * 0.45f, r * 0.33f, fg);
                    b.FillRectangle((int)(cx - r * 0.6f), (int)(cy + r * 0.05f), (int)(r * 1.2f), (int)(r * 0.75f), fg);
                    break;
                case Glyph.Star:
                    for (var i = 0; i < 5; i++)
                    {
                        var a1 = -Math.PI / 2 + i * 2 * Math.PI / 5;
                        var a2 = -Math.PI / 2 + (i + 2) * 2 * Math.PI / 5;
                        b.DrawLine(cx + (float)Math.Cos(a1) * r, cy + (float)Math.Sin(a1) * r, cx + (float)Math.Cos(a2) * r, cy + (float)Math.Sin(a2) * r, Palette.Orange, t);
                    }
                    break;
                case Glyph.Mic:
                    b.FillCircle(cx, cy - r * 0.55f, r * 0.32f, fg);
                    b.FillRectangle((int)(cx - r * 0.32f), (int)(cy - r * 0.55f), (int)(r * 0.64f), (int)(r * 0.65f), fg);
                    b.FillCircle(cx, cy + r * 0.1f, r * 0.32f, fg);
                    b.DrawLine(cx - r * 0.6f, cy - r * 0.05f, cx - r * 0.55f, cy + r * 0.35f, fg, t);
                    b.DrawLine(cx + r * 0.6f, cy - r * 0.05f, cx + r * 0.55f, cy + r * 0.35f, fg, t);
                    b.DrawLine(cx - r * 0.55f, cy + r * 0.35f, cx, cy + r * 0.62f, fg, t);
                    b.DrawLine(cx + r * 0.55f, cy + r * 0.35f, cx, cy + r * 0.62f, fg, t);
                    b.DrawLine(cx, cy + r * 0.62f, cx, cy + r * 0.95f, fg, t);
                    b.DrawLine(cx - r * 0.35f, cy + r * 0.95f, cx + r * 0.35f, cy + r * 0.95f, fg, t);
                    break;
                case Glyph.Speaker:
                case Glyph.VolumeUp:
                case Glyph.VolumeDown:
                    b.FillRectangle((int)(cx - r * 0.85f), (int)(cy - r * 0.3f), (int)(r * 0.45f), (int)(r * 0.6f), fg);
                    for (var i = 0f; i <= 1f; i += 0.1f)
                    {
                        b.DrawLine(cx - r * 0.4f, cy - r * 0.3f - i * r * 0.4f, cx - r * 0.4f, cy + r * 0.3f + i * r * 0.4f, fg, t);
                        b.DrawLine(cx - r * 0.4f + i * r * 0.45f, cy - r * 0.3f - i * r * 0.4f, cx - r * 0.4f + i * r * 0.45f, cy + r * 0.3f + i * r * 0.4f, fg, t);
                    }
                    if (g == Glyph.Speaker)
                    {
                        b.DrawLine(cx + r * 0.35f, cy - r * 0.35f, cx + r * 0.5f, cy, fg, t);
                        b.DrawLine(cx + r * 0.5f, cy, cx + r * 0.35f, cy + r * 0.35f, fg, t);
                        b.DrawLine(cx + r * 0.6f, cy - r * 0.6f, cx + r * 0.85f, cy, fg, t);
                        b.DrawLine(cx + r * 0.85f, cy, cx + r * 0.6f, cy + r * 0.6f, fg, t);
                    }
                    else
                    {
                        b.DrawLine(cx + r * 0.4f, cy, cx + r * 0.95f, cy, fg, t);
                        if (g == Glyph.VolumeUp)
                        {
                            b.DrawLine(cx + r * 0.675f, cy - r * 0.28f, cx + r * 0.675f, cy + r * 0.28f, fg, t);
                        }
                    }
                    break;
                case Glyph.Record:
                    b.FillCircle(cx, cy, r * 0.75f, Palette.Red);
                    break;
                case Glyph.Stop:
                    b.FillRectangle((int)(cx - r * 0.6f), (int)(cy - r * 0.6f), (int)(r * 1.2f), (int)(r * 1.2f), Palette.Red);
                    break;
                case Glyph.Folder:
                    b.FillRectangle((int)(cx - r * 0.9f), (int)(cy - r * 0.6f), (int)(r * 0.7f), (int)(r * 0.3f), fg);
                    b.FillRectangle((int)(cx - r * 0.9f), (int)(cy - r * 0.4f), (int)(r * 1.8f), (int)(r * 1.1f), fg);
                    b.DrawLine(cx - r * 0.9f, cy - r * 0.1f, cx + r * 0.9f, cy - r * 0.1f, bg, t * 0.7f);
                    break;
                case Glyph.Camera:
                    b.FillRectangle((int)(cx - r * 0.9f), (int)(cy - r * 0.45f), (int)(r * 1.8f), (int)(r * 1.15f), fg);
                    b.FillRectangle((int)(cx - r * 0.35f), (int)(cy - r * 0.65f), (int)(r * 0.7f), (int)(r * 0.25f), fg);
                    b.FillCircle(cx, cy + r * 0.12f, r * 0.42f, bg);
                    b.FillCircle(cx, cy + r * 0.12f, r * 0.26f, fg);
                    break;
                case Glyph.Fullscreen:
                    foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                    {
                        var x = cx + sx * r * 0.85f;
                        var y = cy + sy * r * 0.85f;
                        b.DrawLine(x, y, x - sx * r * 0.45f, y, fg, t);
                        b.DrawLine(x, y, x, y - sy * r * 0.45f, fg, t);
                    }
                    break;
                case Glyph.ZoomIn:
                case Glyph.ZoomOut:
                case Glyph.Zoom100:
                    Ring(b, cx - r * 0.15f, cy - r * 0.15f, r * 0.62f, fg, bg, t);
                    b.DrawLine(cx + r * 0.3f, cy + r * 0.3f, cx + r * 0.9f, cy + r * 0.9f, fg, t * 1.5f);
                    if (g != Glyph.Zoom100)
                    {
                        b.DrawLine(cx - r * 0.42f, cy - r * 0.15f, cx + r * 0.12f, cy - r * 0.15f, fg, t);
                    }
                    if (g == Glyph.ZoomIn)
                    {
                        b.DrawLine(cx - r * 0.15f, cy - r * 0.42f, cx - r * 0.15f, cy + r * 0.12f, fg, t);
                    }
                    if (g == Glyph.Zoom100)
                    {
                        b.DrawText("1:1", (int)(cx - r * 0.7f), (int)(cy - r * 0.55f), (int)(r * 1.1f), (int)(r * 0.8f), fg, Math.Max(8, (int)(r * 0.42f)));
                    }
                    break;
                case Glyph.Disconnect:
                    Ring(b, cx, cy, r * 0.85f, Palette.White, bg, t);
                    b.DrawLine(cx - r * 0.4f, cy - r * 0.4f, cx + r * 0.4f, cy + r * 0.4f, Palette.White, t * 1.3f);
                    b.DrawLine(cx + r * 0.4f, cy - r * 0.4f, cx - r * 0.4f, cy + r * 0.4f, Palette.White, t * 1.3f);
                    break;
                case Glyph.Meeting:
                    for (var i = -1; i <= 1; i++)
                    {
                        var x = cx + i * r * 0.62f;
                        var big = i == 0 ? 1.15f : 0.9f;
                        b.FillCircle(x, cy - r * 0.35f * big, r * 0.22f * big, fg);
                        b.FillRectangle((int)(x - r * 0.28f * big), (int)(cy + r * 0.0f), (int)(r * 0.56f * big), (int)(r * 0.5f * big), fg);
                    }
                    break;
                case Glyph.Diagnostics:
                    Ring(b, cx, cy, r * 0.85f, fg, bg, t);
                    b.FillCircle(cx, cy - r * 0.42f, t * 0.9f, fg);
                    b.DrawLine(cx, cy - r * 0.15f, cx, cy + r * 0.5f, fg, t * 1.2f);
                    break;
                case Glyph.Test:
                    b.DrawLine(cx - r * 0.6f, cy, cx - r * 0.15f, cy + r * 0.45f, Palette.Green, t * 1.4f);
                    b.DrawLine(cx - r * 0.15f, cy + r * 0.45f, cx + r * 0.7f, cy - r * 0.5f, Palette.Green, t * 1.4f);
                    break;
                case Glyph.Dial:
                    Ring(b, cx, cy, r * 0.85f, fg, bg, t);
                    b.DrawLine(cx, cy, cx + r * 0.45f, cy - r * 0.45f, Palette.Blue, t * 1.3f);
                    break;
                default:
                    break;
            }
        }

        private static void Monitor(BitmapBuilder b, float cx, float cy, float r, BitmapColor fg, float t)
        {
            Rect(b, cx - r * 0.9f, cy - r * 0.8f, r * 1.8f, r * 1.2f, fg, t);
            b.DrawLine(cx, cy + r * 0.4f, cx, cy + r * 0.75f, fg, t);
            b.DrawLine(cx - r * 0.45f, cy + r * 0.8f, cx + r * 0.45f, cy + r * 0.8f, fg, t);
        }
    }
}
