using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace AlbumCoverFinder
{
    /// <summary>
    /// Renders the same square-tile mosaic the screensaver shows, dumps it as a
    /// BMP to %APPDATA%, and asks Windows to use that as the desktop wallpaper
    /// via the legacy SystemParametersInfo path (still the reliable one across
    /// Win 10/11).
    /// </summary>
    public class WallpaperRenderer
    {
        private const int SPI_SETDESKWALLPAPER = 0x0014;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDCHANGE = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        private readonly AlbumCoverMgr m_oCover;
        private static readonly Random s_oRand = new Random();

        public WallpaperRenderer(AlbumCoverMgr coverMgr) { m_oCover = coverMgr; }

        public string OutputPath
        {
            get
            {
                // %APPDATA% rather than %TEMP% so the BMP survives temp cleanups
                // and Windows doesn't lose its reference to the file.
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ScreenSaver");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "wallpaper.bmp");
            }
        }

        public void RefreshNow()
        {
            string outPath = OutputPath;
            using (Bitmap bmp = RenderMosaic(Screen.PrimaryScreen.Bounds.Size, 256))
            {
                // BMP is the legacy-safe format SystemParametersInfo always honours.
                bmp.Save(outPath, ImageFormat.Bmp);
            }
            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, outPath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        }

        /// <summary>
        /// The deterministic grid geometry of the mosaic: tile size in pixels, the
        /// column and row counts, and the canvas it covers. Pure data, no GDI.
        /// </summary>
        public readonly struct MosaicLayout
        {
            public int Tile { get; }
            public int Cols { get; }
            public int Rows { get; }
            public int CanvasWidth { get; }
            public int CanvasHeight { get; }

            public MosaicLayout(int tile, int cols, int rows, int canvasWidth, int canvasHeight)
            {
                Tile = tile; Cols = cols; Rows = rows;
                CanvasWidth = canvasWidth; CanvasHeight = canvasHeight;
            }

            public int TileCount { get { return Cols * Rows; } }
        }

        /// <summary>
        /// Pure tile/column/row math, lifted out of <see cref="RenderMosaic"/> so it
        /// can be golden-mastered without a GPU, a registry read, or a cover pool.
        /// CoversWide is passed in (the caller reads it from settings) so this stays
        /// deterministic. Behavior is identical to the old inline computation:
        /// CoversWide > 0 fixes the column count and derives the tile from canvas
        /// width; otherwise the tile is the native size and columns ceil-round to
        /// cover the full width. Rows always ceil-round so the bottom clips a partial
        /// tile rather than leaving a black bar.
        /// </summary>
        public static MosaicLayout ComputeLayout(Size canvas, int targetTilePx, int coversWide)
        {
            int tile, cols;
            if (coversWide > 0)
            {
                cols = coversWide;
                tile = Math.Max(32, canvas.Width / cols);
            }
            else
            {
                tile = Math.Max(32, targetTilePx);
                // Ceiling division: cover the full width, partial tile on the right is fine.
                cols = (canvas.Width + tile - 1) / tile;
            }
            int rows = (canvas.Height + tile - 1) / tile;
            return new MosaicLayout(tile, cols, rows, canvas.Width, canvas.Height);
        }

        /// <summary>
        /// Renders the layout (not the pixels) to a stable, diff-friendly text block:
        /// a header, a summary line, and one line per destination tile in draw order
        /// (row-major, the same order <see cref="RenderMosaic"/> paints). Golden-master
        /// target for the geometry contract; the pixels stay on the manual on-screen
        /// CopyFromScreen check.
        /// </summary>
        public static string DescribeLayout(Size canvas, int targetTilePx, int coversWide)
        {
            MosaicLayout layout = ComputeLayout(canvas, targetTilePx, coversWide);
            var sb = new StringBuilder();
            sb.Append("canvas=").Append(canvas.Width).Append('x').Append(canvas.Height)
              .Append(" targetTilePx=").Append(targetTilePx)
              .Append(" coversWide=").Append(coversWide).Append('\n');
            sb.Append("tile=").Append(layout.Tile)
              .Append(" cols=").Append(layout.Cols)
              .Append(" rows=").Append(layout.Rows)
              .Append(" count=").Append(layout.TileCount).Append('\n');
            int idx = 0;
            for (int y = 0; y < layout.Rows; y++)
            {
                for (int x = 0; x < layout.Cols; x++)
                {
                    sb.Append('r').Append(idx.ToString("D3"))
                      .Append(" col=").Append(x).Append(" row=").Append(y)
                      .Append(" dest=").Append(x * layout.Tile).Append(',').Append(y * layout.Tile)
                      .Append(',').Append(layout.Tile).Append(',').Append(layout.Tile).Append('\n');
                    idx++;
                }
            }
            if (sb.Length > 0 && sb[sb.Length - 1] == '\n') sb.Length -= 1;
            return sb.ToString();
        }

        /// <summary>
        /// Renders the mosaic to a new Bitmap with the same layout rules as the
        /// screensaver: starts at the top-left, honours the user's CoversWide
        /// setting, and ceil-rounds the row count so the bottom edge clips a
        /// partial tile instead of leaving a black bar. The geometry is computed by
        /// <see cref="ComputeLayout"/>; only the pixel drawing lives here.
        ///
        /// <paramref name="targetTilePx"/> is the native tile size used only when
        /// CoversWide is 0 (the default). If CoversWide > 0, tile size is derived
        /// from canvas width / CoversWide.
        /// </summary>
        public Bitmap RenderMosaic(Size canvas, int targetTilePx)
        {
            var s = ScreensaverSettings.Load();
            MosaicLayout layout = ComputeLayout(canvas, targetTilePx, s.CoversWide);
            int tile = layout.Tile, cols = layout.Cols, rows = layout.Rows;

            var bmp = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                // Black fill stays as a safety net in case the cover pool is empty -
                // with covers present and ceil-rounded rows/cols, no black should show.
                g.Clear(Color.Black);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SetClip(new Rectangle(0, 0, canvas.Width, canvas.Height));

                var used = new HashSet<string>();
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < cols; x++)
                    {
                        var entry = m_oCover.GetRandomEntry(used);
                        if (entry.Value == null) continue;
                        if (!string.IsNullOrEmpty(entry.Key)) used.Add(entry.Key);

                        // Tile starts at top-left (0,0) and tiles step by `tile`.
                        // The last column/row may extend past the canvas edge; GDI clips it.
                        var dest = new Rectangle(x * tile, y * tile, tile, tile);
                        int side = Math.Min(entry.Value.Width, entry.Value.Height);
                        int sx = (entry.Value.Width - side) / 2;
                        int sy = (entry.Value.Height - side) / 2;
                        g.DrawImage(entry.Value, dest, new Rectangle(sx, sy, side, side), GraphicsUnit.Pixel);
                    }
                }
            }
            return bmp;
        }
    }
}
