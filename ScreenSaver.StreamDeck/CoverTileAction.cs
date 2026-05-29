using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Threading.Tasks;
using BarRaider.SdTools;
using Newtonsoft.Json.Linq;

namespace ScreenSaver.StreamDeck
{
    /// <summary>
    /// One Stream Deck action that becomes one tile of an NxN album-cover grid.
    /// The user drops the same action on N*N keys, then uses the Property Inspector
    /// to tell each key:
    ///   - grid size (1, 2 or 3)
    ///   - its (row, column) inside that grid
    ///   - what media control to perform when pressed
    ///
    /// When SMTC reports a track, every CoverTileAction instance receives the
    /// shared NowPlayingMonitor event, crops its slice of the cover, and pushes
    /// the bitmap to its key.
    /// </summary>
    [PluginActionId("com.blashyrkh.screensaver.covertile")]
    public class CoverTileAction : KeypadBase
    {
        public enum KeyAction
        {
            None,
            PlayPause,
            Next,
            Previous,
            VolumeUp,
            VolumeDown,
            Mute,
            Stop
        }

        private class TileSettings
        {
            public int GridSize = 1;
            public int Row = 0;
            public int Column = 0;
            public KeyAction Action = KeyAction.PlayPause;

            public static TileSettings FromPayload(JObject settings)
            {
                var t = new TileSettings();
                if (settings == null) return t;
                t.GridSize = Clamp(settings.Value<int?>("gridSize") ?? 1, 1, 3);
                t.Row = Clamp(settings.Value<int?>("row") ?? 0, 0, t.GridSize - 1);
                t.Column = Clamp(settings.Value<int?>("column") ?? 0, 0, t.GridSize - 1);
                Enum.TryParse(settings.Value<string>("action") ?? "PlayPause", true, out t.Action);
                return t;
            }

            public JObject ToJson()
            {
                return new JObject
                {
                    ["gridSize"] = GridSize,
                    ["row"] = Row,
                    ["column"] = Column,
                    ["action"] = Action.ToString()
                };
            }

            private static int Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));
        }

        private TileSettings m_oSettings = new TileSettings();
        private string m_sLastRenderedHash = string.Empty;

        public CoverTileAction(SDConnection connection, InitialPayload payload)
            : base(connection, payload)
        {
            m_oSettings = TileSettings.FromPayload(payload.Settings);

            // Subscribe to the shared monitor. Every action instance in this process
            // listens to the same singleton - SMTC is queried once for the plugin.
            CoverMonitorHost.Instance.Updated += OnUpdated;
            CoverMonitorHost.Instance.Cleared += OnCleared;
            CoverMonitorHost.EnsureStarted();

            // Push whatever is already playing (if anything) so we don't sit blank
            // until the next track change.
            var current = CoverMonitorHost.Instance.Current;
            if (current != null) OnUpdated(current);
        }

        public override void Dispose()
        {
            try
            {
                CoverMonitorHost.Instance.Updated -= OnUpdated;
                CoverMonitorHost.Instance.Cleared -= OnCleared;
            }
            catch { }
        }

        public override async void KeyPressed(KeyPayload payload)
        {
            await ExecuteActionAsync(m_oSettings.Action);
        }

        public override void KeyReleased(KeyPayload payload) { }

        /// <summary>
        /// Stream Deck pings us once per second. Used as a fallback refresh in case
        /// the initial SMTC event was missed (e.g. plugin starts mid-track).
        /// </summary>
        public override void OnTick()
        {
            var cur = CoverMonitorHost.Instance.Current;
            if (cur != null) OnUpdated(cur);
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            m_oSettings = TileSettings.FromPayload(payload?.Settings);
            m_sLastRenderedHash = string.Empty; // force re-render with new crop
            var cur = CoverMonitorHost.Instance.Current;
            if (cur != null) OnUpdated(cur);
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        private async void OnUpdated(global::ScreenSaver.NowPlayingMonitor.NowPlayingInfo info)
        {
            if (info == null || info.Cover == null)
            {
                await SetIdleAsync();
                return;
            }

            // Cache key: cover identity + this tile's crop parameters + the action
            // (so the badge updates when the user reassigns the key).
            string hash = (info.Title ?? "") + "|" + (info.Artist ?? "") + "|"
                          + m_oSettings.GridSize + "|" + m_oSettings.Row + "|" + m_oSettings.Column
                          + "|" + m_oSettings.Action;
            if (hash == m_sLastRenderedHash) return;
            m_sLastRenderedHash = hash;

            string base64 = RenderTile(info.Cover, m_oSettings.GridSize, m_oSettings.Row, m_oSettings.Column, m_oSettings.Action);
            if (!string.IsNullOrEmpty(base64))
                await Connection.SetImageAsync(base64);

            // No title overlay on any tile, so the artwork reads as one clean
            // image across the deck.
            await Connection.SetTitleAsync(string.Empty);
        }

        private async void OnCleared()
        {
            await SetIdleAsync();
        }

        private async Task SetIdleAsync()
        {
            if (m_sLastRenderedHash == "__idle__") return;
            m_sLastRenderedHash = "__idle__";
            await Connection.SetImageAsync((string)null);
            await Connection.SetTitleAsync(string.Empty);
        }

        /// <summary>
        /// Crops the (row, column) slice of an NxN grid out of the source cover,
        /// stamps a small action badge in the lower-left corner indicating which
        /// media control this key triggers, and returns the result as a
        /// Stream-Deck-friendly base64 data URL.
        /// </summary>
        private static string RenderTile(Image source, int gridSize, int row, int column, KeyAction action)
        {
            try
            {
                // Centre-crop the source to a square first so non-square covers
                // (rare but possible from SMTC's thumbnail) don't distort.
                int side = Math.Min(source.Width, source.Height);
                int srcX0 = (source.Width - side) / 2;
                int srcY0 = (source.Height - side) / 2;

                int cellSide = side / gridSize;
                int srcX = srcX0 + column * cellSide;
                int srcY = srcY0 + row * cellSide;

                using (var bmp = new Bitmap(144, 144))
                using (var g = Graphics.FromImage(bmp))
                using (var ms = new MemoryStream())
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.DrawImage(source,
                        new Rectangle(0, 0, 144, 144),
                        new Rectangle(srcX, srcY, cellSide, cellSide),
                        GraphicsUnit.Pixel);

                    DrawActionBadge(g, action);

                    bmp.Save(ms, ImageFormat.Png);
                    return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Draws a small translucent badge with a Segoe MDL2 glyph in the lower-left
        /// of the 144x144 tile, hinting at the on-press action. Skipped for None.
        /// Segoe MDL2 Assets is included on every Windows 10/11 install.
        /// </summary>
        private static void DrawActionBadge(Graphics g, KeyAction action)
        {
            string glyph = action switch
            {
                KeyAction.PlayPause  => "", // Play
                KeyAction.Next       => "", // Next
                KeyAction.Previous   => "", // Previous
                KeyAction.Stop       => "", // Stop
                KeyAction.VolumeUp   => "", // Volume3
                KeyAction.VolumeDown => "", // Volume1
                KeyAction.Mute       => "", // Mute
                _ => null
            };
            if (glyph == null) return;

            const int padding = 8;
            const int size = 36;
            var badgeRect = new Rectangle(padding, 144 - padding - size, size, size);

            // Soft dark backdrop, rounded so it reads as a button-like chip.
            using (var path = RoundedRect(badgeRect, 8))
            using (var bgBrush = new SolidBrush(Color.FromArgb(170, 0, 0, 0)))
            {
                g.FillPath(bgBrush, path);
            }

            // White glyph centred inside the chip.
            using (var font = new Font("Segoe MDL2 Assets", 20, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(Color.FromArgb(245, 255, 255, 255)))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(glyph, font, brush, badgeRect, sf);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static async Task ExecuteActionAsync(KeyAction action)
        {
            var monitor = CoverMonitorHost.Instance;
            switch (action)
            {
                case KeyAction.None:
                    return;
                case KeyAction.PlayPause:
                    // SMTC first; if the session refuses, fall back to the global media key.
                    if (!await monitor.TryTogglePlayPauseAsync())
                        MediaKeys.Press(MediaKeys.VK_MEDIA_PLAY_PAUSE);
                    return;
                case KeyAction.Next:
                    if (!await monitor.TrySkipNextAsync())
                        MediaKeys.Press(MediaKeys.VK_MEDIA_NEXT_TRACK);
                    return;
                case KeyAction.Previous:
                    if (!await monitor.TrySkipPreviousAsync())
                        MediaKeys.Press(MediaKeys.VK_MEDIA_PREV_TRACK);
                    return;
                case KeyAction.Stop:
                    if (!await monitor.TryStopAsync())
                        MediaKeys.Press(MediaKeys.VK_MEDIA_STOP);
                    return;
                case KeyAction.VolumeUp:
                    MediaKeys.Press(MediaKeys.VK_VOLUME_UP);
                    return;
                case KeyAction.VolumeDown:
                    MediaKeys.Press(MediaKeys.VK_VOLUME_DOWN);
                    return;
                case KeyAction.Mute:
                    MediaKeys.Press(MediaKeys.VK_VOLUME_MUTE);
                    return;
            }
        }
    }

    /// <summary>
    /// Process-scope singleton for the shared NowPlayingMonitor. All
    /// CoverTileAction instances subscribe to one monitor so SMTC is only
    /// queried once per plugin process, even with nine keys configured.
    /// </summary>
    internal static class CoverMonitorHost
    {
        public static readonly global::ScreenSaver.NowPlayingMonitor Instance = new global::ScreenSaver.NowPlayingMonitor();
        private static bool s_started;
        private static readonly object s_lock = new object();

        public static void EnsureStarted()
        {
            lock (s_lock)
            {
                if (s_started) return;
                s_started = true;
            }
            _ = Instance.StartAsync();
        }
    }
}
