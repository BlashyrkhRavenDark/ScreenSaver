using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace IconGen
{
    /// <summary>
    /// Procedurally draws a "CD in a jewel case" icon at multiple sizes and
    /// emits PNGs + a multi-resolution ICO. Run once to regenerate assets in
    /// the project root's icons\ folder.
    /// </summary>
    internal static class Program
    {
        // PNG sizes we emit. The 16/32/48/256 set ends up bundled into the ICO.
        // 72 and 144 are the Stream Deck "@1x" / "@2x" sizes.
        private static readonly int[] PngSizes = { 16, 32, 48, 72, 128, 144, 256, 512 };
        private static readonly int[] IcoSizes = { 16, 32, 48, 256 };

        private static int Main(string[] args)
        {
            string outDir = args.Length > 0 ? args[0] : "icons";
            Directory.CreateDirectory(outDir);

            var pngBytes = new Dictionary<int, byte[]>();
            foreach (int s in PngSizes)
            {
                using (var bmp = Render(s))
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    byte[] data = ms.ToArray();
                    pngBytes[s] = data;
                    string pngPath = Path.Combine(outDir, $"app-{s}.png");
                    File.WriteAllBytes(pngPath, data);
                    Console.WriteLine($"wrote {pngPath} ({data.Length} bytes)");
                }
            }

            // Multi-resolution ICO containing PNG-encoded frames (Vista+ format).
            string icoPath = Path.Combine(outDir, "app.ico");
            File.WriteAllBytes(icoPath, BuildIco(IcoSizes.Select(s => pngBytes[s]).ToArray(), IcoSizes));
            Console.WriteLine($"wrote {icoPath}");

            return 0;
        }

        /// <summary>
        /// Draws the icon at the given square size. Composition:
        ///   - rounded-rect jewel case with a glossy highlight
        ///   - silver CD inside with a center hub hole + reflection sheen
        /// </summary>
        private static Bitmap Render(int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.Clear(Color.Transparent);

                // Inset so we have room for the case shadow and don't bleed to the edge.
                float pad = size * 0.04f;
                var caseRect = RectangleF.FromLTRB(pad, pad, size - pad, size - pad);

                DrawJewelCase(g, caseRect);
                DrawCd(g, caseRect);
                DrawCaseHighlight(g, caseRect);
            }
            return bmp;
        }

        private static void DrawJewelCase(Graphics g, RectangleF rect)
        {
            float radius = rect.Width * 0.10f;
            using (var path = RoundedRect(rect, radius))
            {
                // Translucent dark base, like real polycarbonate held up to light.
                using (var brush = new LinearGradientBrush(rect,
                    Color.FromArgb(220, 30, 35, 55),
                    Color.FromArgb(220, 12, 16, 30),
                    LinearGradientMode.ForwardDiagonal))
                {
                    g.FillPath(brush, path);
                }

                // Outer edge: thin lighter stroke so it reads against dark backgrounds too.
                using (var pen = new Pen(Color.FromArgb(140, 200, 220, 255), Math.Max(1f, rect.Width * 0.014f)))
                {
                    g.DrawPath(pen, path);
                }
            }
        }

        private static void DrawCd(Graphics g, RectangleF caseRect)
        {
            float diameter = caseRect.Width * 0.82f;
            float cx = caseRect.Left + caseRect.Width / 2f;
            float cy = caseRect.Top + caseRect.Height / 2f;
            var disk = new RectangleF(cx - diameter / 2f, cy - diameter / 2f, diameter, diameter);

            // CD body: silver radial gradient with a subtle rainbow tint at the edges.
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(disk);
                using (var brush = new PathGradientBrush(path))
                {
                    brush.CenterPoint = new PointF(cx, cy);
                    brush.CenterColor = Color.FromArgb(255, 240, 240, 245);
                    brush.SurroundColors = new[] { Color.FromArgb(255, 180, 195, 210) };
                    g.FillPath(brush, path);
                }
            }

            // Faint iridescent ring near the outer edge.
            float ringWidth = diameter * 0.04f;
            using (var pen = new Pen(Color.FromArgb(80, 120, 180, 240), ringWidth))
            {
                g.DrawEllipse(pen, RectangleF.Inflate(disk, -ringWidth, -ringWidth));
            }

            // Inner data ring (slightly darker).
            float innerD = diameter * 0.36f;
            var innerRing = new RectangleF(cx - innerD / 2f, cy - innerD / 2f, innerD, innerD);
            using (var pen = new Pen(Color.FromArgb(60, 0, 0, 0), Math.Max(1f, diameter * 0.012f)))
            {
                g.DrawEllipse(pen, innerRing);
            }

            // Hub hole.
            float hubD = diameter * 0.16f;
            var hub = new RectangleF(cx - hubD / 2f, cy - hubD / 2f, hubD, hubD);
            using (var brush = new SolidBrush(Color.FromArgb(255, 18, 22, 38)))
            {
                g.FillEllipse(brush, hub);
            }
            using (var pen = new Pen(Color.FromArgb(160, 200, 220, 255), Math.Max(1f, hubD * 0.10f)))
            {
                g.DrawEllipse(pen, hub);
            }

            // Sheen: a thin white arc on the upper-left for the "ooh shiny" feel.
            using (var path = new GraphicsPath())
            {
                path.AddArc(RectangleF.Inflate(disk, -ringWidth * 1.5f, -ringWidth * 1.5f), 200, 80);
                using (var pen = new Pen(Color.FromArgb(180, 255, 255, 255), Math.Max(1f, diameter * 0.045f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawPath(pen, path);
                }
            }
        }

        private static void DrawCaseHighlight(Graphics g, RectangleF rect)
        {
            // Glossy diagonal highlight across the upper portion of the case,
            // clipped to the rounded-rect path so it doesn't spill over the edges.
            float radius = rect.Width * 0.10f;
            using (var clip = RoundedRect(rect, radius))
            {
                var state = g.Save();
                g.SetClip(clip);

                var highlight = new RectangleF(rect.Left, rect.Top, rect.Width, rect.Height * 0.55f);
                using (var brush = new LinearGradientBrush(highlight,
                    Color.FromArgb(70, 255, 255, 255),
                    Color.FromArgb(0, 255, 255, 255),
                    LinearGradientMode.Vertical))
                {
                    g.FillRectangle(brush, highlight);
                }

                g.Restore(state);
            }
        }

        private static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = radius * 2f;
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Builds a multi-resolution ICO file containing PNG-encoded frames.
        /// Each frame is referenced via ICONDIRENTRY; PNG data follows in sequence.
        /// Format: https://en.wikipedia.org/wiki/ICO_(file_format)
        /// </summary>
        private static byte[] BuildIco(byte[][] frames, int[] sizes)
        {
            if (frames.Length != sizes.Length) throw new ArgumentException("frame/size mismatch");
            int count = frames.Length;
            int headerLen = 6 + 16 * count;
            int totalLen = headerLen + frames.Sum(f => f.Length);
            var buf = new byte[totalLen];

            // ICONDIR
            WriteU16(buf, 0, 0);     // reserved
            WriteU16(buf, 2, 1);     // type = icon
            WriteU16(buf, 4, (ushort)count);

            int dataOffset = headerLen;
            for (int i = 0; i < count; i++)
            {
                int entryOff = 6 + 16 * i;
                int s = sizes[i];
                buf[entryOff + 0] = (byte)(s >= 256 ? 0 : s); // 0 means 256+
                buf[entryOff + 1] = (byte)(s >= 256 ? 0 : s);
                buf[entryOff + 2] = 0; // color count (palette)
                buf[entryOff + 3] = 0; // reserved
                WriteU16(buf, entryOff + 4, 1);  // planes
                WriteU16(buf, entryOff + 6, 32); // bits per pixel
                WriteU32(buf, entryOff + 8, (uint)frames[i].Length); // image size
                WriteU32(buf, entryOff + 12, (uint)dataOffset);      // image offset

                Buffer.BlockCopy(frames[i], 0, buf, dataOffset, frames[i].Length);
                dataOffset += frames[i].Length;
            }
            return buf;
        }

        private static void WriteU16(byte[] buf, int offset, ushort value)
        {
            buf[offset + 0] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static void WriteU32(byte[] buf, int offset, uint value)
        {
            buf[offset + 0] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
            buf[offset + 2] = (byte)((value >> 16) & 0xFF);
            buf[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}
