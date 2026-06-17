using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using AlbumCoverFinder;
using Microsoft.Win32;
using OpenMacroBoard.SDK;
using StreamDeckSharp;

namespace ScreenSaver.Tray
{
    /// <summary>
    /// Draws a custom image on the Stream Deck while Windows is LOCKED, talking to
    /// the deck over raw USB HID (StreamDeckSharp) - which bypasses the Elgato app.
    /// On lock the Elgato app only sends the deck to its firmware standby (the Elgato
    /// logo) and stops driving it, so a second HID writer is effectively the sole
    /// writer during the locked window and its image wins. (Technique learned from
    /// reverse-engineering BarRaider's screensaver plugin - see .claude/notes.md.)
    ///
    /// Mode comes from HKCU\SOFTWARE\Demo_ScreenSaver (set in AlbumCoverFinder):
    ///   NowPlaying (default) - the current iTunes cover (the Tray's nowplaying.png,
    ///                          kept fresh by the COM poll even while locked)
    ///   Picture / Gif        - a user-chosen file (LockDeckImagePath)
    ///   Off                  - draw nothing; leave the deck to Elgato
    ///
    /// On unlock we release the deck so the Elgato app reclaims and redraws it.
    /// </summary>
    internal sealed class LockScreenDeck : IDisposable
    {
        private readonly object m_sync = new object();
        private IMacroBoard m_board;
        private System.Timers.Timer m_timer;

        private LockDeckMode m_mode;
        private string m_imagePath;

        // Composed full-deck frame for NowPlaying / Picture modes (488x280-ish).
        private Bitmap m_staticFrame;
        private long m_nowPlayingStamp = long.MinValue;

        // Decoded + pre-composed GIF frames for Gif mode.
        private List<(Bitmap bmp, int delayMs)> m_gif;
        private int m_gifIndex;

        // Re-push cadence for static modes - also overwrites any late standby logo
        // the Elgato app pushes right after lock, and picks up track changes.
        private const int REPUSH_MS = 1000;

        private static string NowPlayingPng => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScreenSaver", "nowplaying.png");

        public LockScreenDeck()
        {
            SystemEvents.SessionSwitch += OnSessionSwitch;
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionLock) StartLockDisplay();
            else if (e.Reason == SessionSwitchReason.SessionUnlock) StopLockDisplay();
        }

        private void StartLockDisplay()
        {
            lock (m_sync)
            {
                StopCore(); // safety, in case a prior lock never tore down

                var s = ScreensaverSettings.Load();
                m_mode = s.LockDeckMode;
                m_imagePath = s.LockDeckImagePath;
                Log($"SessionLock: mode={m_mode} path='{m_imagePath}'");
                if (m_mode == LockDeckMode.Off) { Log("  Off - leaving the deck to Elgato"); return; }

                try { m_board = StreamDeck.EnumerateDevices().FirstOrDefault()?.Open(); }
                catch (Exception ex) { m_board = null; Log("  open failed: " + ex.Message); }
                if (m_board == null) { Log("  no Stream Deck found/opened"); return; }
                Log($"  deck opened: keys={m_board.Keys.Count} area={m_board.Keys.Area.Width}x{m_board.Keys.Area.Height}");

                try { m_board.SetBrightness(100); } catch { }

                if (m_mode == LockDeckMode.Gif)
                    m_gif = LoadGifFrames(m_imagePath);
                else if (m_mode == LockDeckMode.Picture)
                    m_staticFrame = ComposeFromFile(m_imagePath);

                Tick();        // paint immediately
                if (m_disposed) return;

                m_timer = new System.Timers.Timer { AutoReset = true };
                m_timer.Elapsed += (a, b) => Tick();
                m_timer.Interval = CurrentInterval();
                m_timer.Start();
            }
        }

        private void StopLockDisplay()
        {
            lock (m_sync) { Log("SessionUnlock: releasing deck"); StopCore(); }
        }

        private void StopCore()
        {
            if (m_timer != null) { try { m_timer.Stop(); m_timer.Dispose(); } catch { } m_timer = null; }
            // Release the deck so the Elgato app reclaims it on unlock. We do NOT
            // blank first: leaving our last frame up until Elgato redraws avoids a
            // black flash.
            if (m_board != null) { try { m_board.Dispose(); } catch { } m_board = null; }
            if (m_gif != null) { foreach (var f in m_gif) { try { f.bmp?.Dispose(); } catch { } } m_gif = null; }
            if (m_staticFrame != null) { try { m_staticFrame.Dispose(); } catch { } m_staticFrame = null; }
            m_gifIndex = 0;
            m_nowPlayingStamp = long.MinValue;
        }

        private double CurrentInterval()
        {
            if (m_mode == LockDeckMode.Gif && m_gif != null && m_gif.Count > 0)
                return Math.Max(20, m_gif[m_gifIndex].delayMs);
            return REPUSH_MS;
        }

        private void Tick()
        {
            lock (m_sync)
            {
                if (m_board == null) return;
                try
                {
                    if (m_mode == LockDeckMode.Gif)
                    {
                        if (m_gif != null && m_gif.Count > 0)
                        {
                            PushFrame(m_gif[m_gifIndex].bmp);
                            m_gifIndex = (m_gifIndex + 1) % m_gif.Count;
                        }
                        else BlankDeck();
                    }
                    else if (m_mode == LockDeckMode.Picture)
                    {
                        if (m_staticFrame != null) PushFrame(m_staticFrame); else BlankDeck();
                    }
                    else // NowPlaying
                    {
                        RefreshNowPlayingFrame();
                        if (m_staticFrame != null) PushFrame(m_staticFrame); else BlankDeck();
                    }
                    if (m_timer != null) m_timer.Interval = CurrentInterval();
                }
                catch (Exception ex)
                {
                    // Transient HID/IO error - drop the handle and try to re-acquire next tick.
                    Log("  draw error: " + ex.Message);
                    try { m_board?.Dispose(); } catch { }
                    try { m_board = StreamDeck.EnumerateDevices().FirstOrDefault()?.Open(); m_board?.SetBrightness(100); }
                    catch { m_board = null; }
                }
            }
        }

        /// <summary>NowPlaying: recompose only when the Tray's nowplaying.png changes.</summary>
        private void RefreshNowPlayingFrame()
        {
            string path = NowPlayingPng;
            long stamp = 0;
            try { if (File.Exists(path)) stamp = File.GetLastWriteTimeUtc(path).Ticks; } catch { }

            if (stamp == m_nowPlayingStamp && m_staticFrame != null) return;

            if (stamp == 0) // nothing playing
            {
                if (m_staticFrame == null || m_nowPlayingStamp != 0)
                {
                    m_staticFrame?.Dispose();
                    m_staticFrame = BlackFrame();
                    m_nowPlayingStamp = 0;
                }
                return;
            }

            Bitmap nf = ComposeFromFile(path);
            if (nf == null) return; // mid-write; keep last frame, retry next tick (stamp not advanced)
            m_staticFrame?.Dispose();
            m_staticFrame = nf;
            m_nowPlayingStamp = stamp;
        }

        // --- Rendering ---------------------------------------------------------

        private Bitmap ComposeFromFile(string path)
        {
            using (var src = LoadImageShared(path))
                return src == null ? null : ComposeDeckFrame(src);
        }

        /// <summary>
        /// Compose the full-deck frame: the whole source centred and letterboxed
        /// over a blurred, darkened, zoomed copy of itself that fills the wide
        /// margins - so nothing is distorted or cropped, and the deck reads as one image.
        /// </summary>
        private Bitmap ComposeDeckFrame(Image src)
        {
            int w = m_board.Keys.Area.Width;
            int h = m_board.Keys.Area.Height;
            var canvas = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.Black);
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // Background: zoom-to-fill, faked blur via downscale->upscale, then darken.
                double sFill = Math.Max((double)w / src.Width, (double)h / src.Height);
                int bw = Math.Max(1, (int)Math.Round(src.Width * sFill));
                int bh = Math.Max(1, (int)Math.Round(src.Height * sFill));
                using (var small = new Bitmap(Math.Max(1, w / 16), Math.Max(1, h / 16), PixelFormat.Format24bppRgb))
                {
                    using (var sg = Graphics.FromImage(small))
                    {
                        sg.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        sg.DrawImage(src, new Rectangle(0, 0, small.Width, small.Height));
                    }
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(small, new Rectangle((w - bw) / 2, (h - bh) / 2, bw, bh));
                }
                using (var shade = new SolidBrush(Color.FromArgb(125, 0, 0, 0)))
                    g.FillRectangle(shade, 0, 0, w, h);

                // Foreground: whole source, letterboxed, centred.
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                double sFit = Math.Min((double)w / src.Width, (double)h / src.Height);
                int fw = Math.Max(1, (int)Math.Round(src.Width * sFit));
                int fh = Math.Max(1, (int)Math.Round(src.Height * sFit));
                g.DrawImage(src, new Rectangle((w - fw) / 2, (h - fh) / 2, fw, fh));
            }
            return canvas;
        }

        private Bitmap BlackFrame()
        {
            var b = new Bitmap(m_board.Keys.Area.Width, m_board.Keys.Area.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(b)) g.Clear(Color.Black);
            return b;
        }

        /// <summary>Slice the composed frame into per-key bitmaps and push each over HID.</summary>
        private void PushFrame(Bitmap full)
        {
            var keys = m_board.Keys;
            for (int i = 0; i < keys.Count; i++)
            {
                var k = keys[i];
                using (var key = new Bitmap(k.Width, k.Height, PixelFormat.Format24bppRgb))
                {
                    using (var g = Graphics.FromImage(key))
                        g.DrawImage(full, new Rectangle(0, 0, k.Width, k.Height),
                                    new Rectangle(k.X, k.Y, k.Width, k.Height), GraphicsUnit.Pixel);
                    byte[] bgr = ToBgr24(key);
                    m_board.SetKeyBitmap(i, KeyBitmap.Create.FromBgr24Array(k.Width, k.Height, bgr));
                }
            }
        }

        private void BlankDeck()
        {
            var keys = m_board.Keys;
            for (int i = 0; i < keys.Count; i++)
                m_board.SetKeyBitmap(i, KeyBitmap.Black);
        }

        private static byte[] ToBgr24(Bitmap bmp)
        {
            // System.Drawing's Format24bppRgb is BGR in memory, which is what
            // FromBgr24Array expects. Repack to a tight width*3 stride (LockBits
            // pads rows to 4 bytes).
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                int rowBytes = bmp.Width * 3;
                byte[] outp = new byte[rowBytes * bmp.Height];
                for (int y = 0; y < bmp.Height; y++)
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), outp, y * rowBytes, rowBytes);
                return outp;
            }
            finally { bmp.UnlockBits(data); }
        }

        private static Bitmap LoadImageShared(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var img = Image.FromStream(fs, false, false))
                    return new Bitmap(img); // copy so the file/stream can be released
            }
            catch { return null; }
        }

        private List<(Bitmap bmp, int delayMs)> LoadGifFrames(string path)
        {
            var frames = new List<(Bitmap, int)>();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return frames;
            try
            {
                using (var img = Image.FromFile(path))
                {
                    var dim = new FrameDimension(img.FrameDimensionsList[0]);
                    int count = img.GetFrameCount(dim);
                    int[] delays = ReadGifDelays(img, count);
                    for (int i = 0; i < count; i++)
                    {
                        img.SelectActiveFrame(dim, i);
                        using (var frameCopy = new Bitmap(img))
                            frames.Add((ComposeDeckFrame(frameCopy), delays[i]));
                    }
                }
            }
            catch { }
            return frames;
        }

        private static int[] ReadGifDelays(Image img, int count)
        {
            var ms = new int[count];
            for (int i = 0; i < count; i++) ms[i] = 1000;
            try
            {
                PropertyItem pi = img.GetPropertyItem(0x5100); // PropertyTagFrameDelay, 1/100 s per frame
                if (pi?.Value != null)
                    for (int i = 0; i < count && (i * 4 + 4) <= pi.Value.Length; i++)
                    {
                        int cs = BitConverter.ToInt32(pi.Value, i * 4);
                        ms[i] = cs > 0 ? cs * 10 : 1000;
                    }
            }
            catch { }
            return ms;
        }

        private static void Log(string msg)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenSaver");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "deck-lock.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + msg + Environment.NewLine);
            }
            catch { }
        }

        private bool m_disposed;

        public void Dispose()
        {
            if (m_disposed) return;
            m_disposed = true;
            try { SystemEvents.SessionSwitch -= OnSessionSwitch; } catch { }
            lock (m_sync) { StopCore(); }
        }
    }
}
