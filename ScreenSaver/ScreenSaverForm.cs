using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using AlbumCoverFinder;

namespace ScreenSaver
{
    /// <summary>
    /// The visual surface of the screensaver - one Form per monitor.
    ///
    /// RENDERING: the whole UI is owner-drawn in <see cref="OnPaint"/> into a manual
    /// back-buffer that is then blitted in one go. We do NOT use child controls for
    /// the tiles/overlay, because on this machine's .NET 10 runtime child controls of
    /// this form never composite to the screen (the form paints, the children do not -
    /// verified on-screen across every control type, DPI mode, and window style). The
    /// form's own surface renders reliably, so everything lives there.
    ///
    /// Builds a mosaic of square tiles, swaps one tile per cadence with a transition,
    /// and overlays a focal "Now Playing" tile + captions whenever the Windows SMTC
    /// reports an active media session.
    /// </summary>
    public partial class ScreenSaverForm : Form
    {
        #region Win32 API functions

        [DllImport("user32.dll")]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hWnd, out Rectangle lpRect);

        #endregion

        private const int DEFAULT_TARGET_TILE_PX = 256;
        private const int PREVIEW_TARGET_TILE_PX = 80;

        private Point mouseLocation;
        private bool m_bPreviewMode = false;
        private static readonly Random s_oRand = new Random();
        private int m_iXCovers = 16;
        private int m_iYCovers = 9;
        private int m_iTilePx = 256;
        private int m_iOffsetX = 0;
        private int m_iOffsetY = 0;
        private FadingTile[,] m_aTiles;
        private string[,] m_aTileKeys;
        private AlbumCoverMgr m_oCoverMgr;
        private NowPlayingMonitor m_oNowPlaying;
        private string m_sCurrentPlayingKey;

        // Owner-draw back-buffer + animation clock.
        private Bitmap m_oBackBuffer;
        private System.Windows.Forms.Timer m_oAnimTimer;

        // Display settings (live in the registry; loaded in LoadSettings).
        private int m_iCoversWide;                              // 0 = native (use DEFAULT_TARGET_TILE_PX)
        private TransitionEffect m_oEffect = TransitionEffect.Merge;
        private int m_iTransitionDurationMs = 1500;
        private int m_iGapBetweenTransitionsMs = 2000;

        // "Feature" cover: the now-playing (or pinned) album, drawn over the mosaic
        // as a 2x2-tile cover that flips with the same transition as the rest.
        // No dimming shade, no caption text.
        private readonly FadingTile m_oFeatureTile = new FadingTile();
        private bool m_bFeatureVisible;

        // "Artist — Title" of the current track, shown only while the mouse is moving
        // (manage mode). Empty when nothing is playing.
        private string m_sNowPlayingText = string.Empty;

        // Click-to-pin overlay (#2).
        private bool m_bPinned;
        private string m_sPinnedKey;

        // Right-click-to-hide transient toast.
        private string m_sToastText = string.Empty;
        private bool m_bToastVisible;
        private System.Windows.Forms.Timer m_oToastTimer;

        // Manage mode (cursor visible, swap paused, right-click-to-hide armed).
        private bool m_bManageMode;
        private System.Windows.Forms.Timer m_oManageTimer;
        private bool m_bManageHintVisible;
        private const int MANAGE_MODE_TIMEOUT_MS = 8000;

        #region Constructors

        public ScreenSaverForm(AlbumCoverMgr pCoverMgr) : this(pCoverMgr, null) { }

        public ScreenSaverForm(AlbumCoverMgr pCoverMgr, NowPlayingMonitor pNowPlaying)
        {
            m_oCoverMgr = pCoverMgr;
            m_oNowPlaying = pNowPlaying;
            InitializeComponent();
            LoadSettings();
            ComputeGrid(new Rectangle(0, 0, 1920, 1080), DEFAULT_TARGET_TILE_PX);
        }

        public ScreenSaverForm(Rectangle Bounds, AlbumCoverMgr pCoverMgr) : this(Bounds, pCoverMgr, null) { }

        public ScreenSaverForm(Rectangle Bounds, AlbumCoverMgr pCoverMgr, NowPlayingMonitor pNowPlaying)
        {
            m_oCoverMgr = pCoverMgr;
            m_oNowPlaying = pNowPlaying;
            InitializeComponent();
            this.Bounds = Bounds;
            LoadSettings();
            ComputeGrid(Bounds, DEFAULT_TARGET_TILE_PX);
        }

        public ScreenSaverForm(IntPtr PreviewWndHandle, AlbumCoverMgr pCoverMgr) : this(PreviewWndHandle, pCoverMgr, null) { }

        public ScreenSaverForm(IntPtr PreviewWndHandle, AlbumCoverMgr pCoverMgr, NowPlayingMonitor pNowPlaying)
        {
            m_oCoverMgr = pCoverMgr;
            m_oNowPlaying = pNowPlaying;
            InitializeComponent();

            SetParent(this.Handle, PreviewWndHandle);
            SetWindowLong(this.Handle, -16, new IntPtr(GetWindowLong(this.Handle, -16) | 0x40000000));
            Rectangle ParentRect;
            GetClientRect(PreviewWndHandle, out ParentRect);
            Size = ParentRect.Size;
            Location = new Point(0, 0);
            m_bPreviewMode = true;
            LoadSettings();
            int previousCoversWide = m_iCoversWide;
            m_iCoversWide = 0;
            ComputeGrid(new Rectangle(0, 0, ParentRect.Width, ParentRect.Height), PREVIEW_TARGET_TILE_PX);
            m_iCoversWide = previousCoversWide;
        }
        #endregion

        #region Grid sizing

        /// <summary>
        /// Grid layout starts at the top-left corner. If the user picked a column
        /// count, tile size derives from screen width / columns; otherwise tile size
        /// is the native cover resolution and the column count covers the screen,
        /// allowing the right/bottom edge to clip a partial tile.
        /// </summary>
        private void ComputeGrid(Rectangle pBounds, int pTargetTilePx)
        {
            int iTile, iCols;
            if (m_iCoversWide > 0)
            {
                iCols = m_iCoversWide;
                iTile = Math.Max(32, pBounds.Width / iCols);
            }
            else
            {
                iTile = Math.Max(32, pTargetTilePx);
                iCols = (pBounds.Width + iTile - 1) / iTile;
            }
            int iRows = (pBounds.Height + iTile - 1) / iTile;

            m_iXCovers = Math.Max(1, iCols);
            m_iYCovers = Math.Max(1, iRows);
            m_iTilePx = iTile;
            m_iOffsetX = 0;
            m_iOffsetY = 0;
            m_aTiles = new FadingTile[m_iXCovers, m_iYCovers];
            m_aTileKeys = new string[m_iXCovers, m_iYCovers];
        }

        private Rectangle TileRect(int x, int y)
        {
            return new Rectangle(m_iOffsetX + x * m_iTilePx, m_iOffsetY + y * m_iTilePx, m_iTilePx, m_iTilePx);
        }

        private bool TileAt(Point p, out int x, out int y)
        {
            x = (p.X - m_iOffsetX) / m_iTilePx;
            y = (p.Y - m_iOffsetY) / m_iTilePx;
            return x >= 0 && y >= 0 && x < m_iXCovers && y < m_iYCovers;
        }

        #endregion

        #region Initialisation

        private async void ScreenSaverForm_Load(object sender, EventArgs e)
        {
            try
            {
                DiagLog.Write("Form_Load start, preview=" + m_bPreviewMode + " bounds=" + this.Bounds);
                LoadSettings();
                ApplyFilterFromRegistry();
                if (!m_bPreviewMode) Cursor.Hide();
                TopMost = !m_bPreviewMode;
                KeyPreview = true;

                InitiateTiles();
                DiagLog.Write("  InitiateTiles OK, grid=" + m_iXCovers + "x" + m_iYCovers + " tile=" + m_iTilePx);

                // The feature cover flips like the mosaic tiles.
                m_oFeatureTile.Effect = m_oEffect;
                m_oFeatureTile.TransitionDurationMs = m_iTransitionDurationMs;

                moveTimer.Interval = ComputeSwapPeriodMs();
                moveTimer.Tick += new EventHandler(moveTimer_Tick);
                moveTimer.Start();

                // Animation clock: advances transitions / highlight pulses and repaints
                // only while something is actually animating.
                m_oAnimTimer = new System.Windows.Forms.Timer { Interval = 16 };
                m_oAnimTimer.Tick += OnAnimTick;
                m_oAnimTimer.Start();

                if (m_oNowPlaying != null)
                {
                    m_oNowPlaying.Updated += OnNowPlayingUpdated;
                    m_oNowPlaying.Cleared += OnNowPlayingCleared;
                    try { await m_oNowPlaying.StartAsync(); }
                    catch (Exception ex) { DiagLog.WriteError("  NowPlayingMonitor.StartAsync", ex); }
                    if (m_oNowPlaying.Current != null)
                        OnNowPlayingUpdated(m_oNowPlaying.Current);
                }
                DiagLog.Write("Form_Load complete, Visible=" + Visible + " ClientSize=" + ClientSize);
                Invalidate();
            }
            catch (Exception ex)
            {
                DiagLog.WriteError("Form_Load exception", ex);
            }
        }

        private void ApplyFilterFromRegistry()
        {
            if (m_oCoverMgr == null) return;
            var f = CoverFilter.LoadFromRegistry();
            m_oCoverMgr.SetFilter(f.IsEmpty ? null : f);
        }

        private void LoadSettings()
        {
            var s = ScreensaverSettings.Load();
            m_iCoversWide = s.CoversWide;
            m_oEffect = s.Effect;
            m_iTransitionDurationMs = s.TransitionDurationMs;
            m_iGapBetweenTransitionsMs = s.GapBetweenTransitionsMs;
            if (m_oCoverMgr != null) m_oCoverMgr.SetAlbumCap(s.AlbumCap);
        }

        /// <summary>Tile-swap cadence: quiet gap plus the transition itself (zero for Blink).</summary>
        private int ComputeSwapPeriodMs()
        {
            int transitionMs = m_oEffect == TransitionEffect.Blink ? 0 : m_iTransitionDurationMs;
            return Math.Max(100, m_iGapBetweenTransitionsMs + transitionMs);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (!m_bPreviewMode)
                Cursor.Show();
            if (m_oNowPlaying != null)
            {
                m_oNowPlaying.Updated -= OnNowPlayingUpdated;
                m_oNowPlaying.Cleared -= OnNowPlayingCleared;
            }
            m_oAnimTimer?.Stop();
            m_oBackBuffer?.Dispose();
            m_oBackBuffer = null;
            base.OnFormClosed(e);
        }

        #endregion

        #region Owner-draw rendering

        // The buffer already clears to black, so suppress the background erase
        // (avoids flicker and a redundant fill).
        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (m_aTiles == null) { base.OnPaint(e); return; }

            int w = ClientSize.Width, h = ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            // A freshly (re)allocated buffer has no valid contents, so it must be
            // painted in full regardless of the invalidated region.
            bool full = false;
            if (m_oBackBuffer == null || m_oBackBuffer.Width != w || m_oBackBuffer.Height != h)
            {
                m_oBackBuffer?.Dispose();
                m_oBackBuffer = new Bitmap(w, h);
                full = true;
            }

            // DIRTY-RECT: only repaint the invalidated region. The mosaic is mostly
            // static - normally just one tile is mid-transition - so redrawing all
            // ~84 tiles + clearing/blitting the whole 4K buffer every frame (the old
            // behaviour) was what made transitions stutter. Here we clear only the
            // dirty rect, redraw only the tiles that intersect it, and blit just that
            // rect. GDI clips each draw, so non-intersecting tiles cost a cheap reject.
            Rectangle clip = full ? new Rectangle(0, 0, w, h) : e.ClipRectangle;
            if (clip.Width <= 0 || clip.Height <= 0) return;

            using (var g = Graphics.FromImage(m_oBackBuffer))
            {
                g.SetClip(clip);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;

                using (var black = new SolidBrush(Color.Black))
                    g.FillRectangle(black, clip);

                // Mosaic - only tiles overlapping the dirty rect.
                for (int x = 0; x < m_iXCovers; x++)
                    for (int y = 0; y < m_iYCovers; y++)
                    {
                        Rectangle r = TileRect(x, y);
                        if (r.IntersectsWith(clip))
                            m_aTiles[x, y]?.Paint(g, r);
                    }

                // Feature cover (now-playing / pinned) over the mosaic as a 2x2 cover.
                if (m_bFeatureVisible)
                {
                    Rectangle fr = FeatureRect();
                    if (fr.IntersectsWith(clip)) m_oFeatureTile.Paint(g, fr);
                }

                // Transient text overlays (drawn last; GDI clips them to the dirty rect).
                if (m_bManageHintVisible) PaintManageHint(g);
                if (m_bToastVisible) PaintToast(g);
            }

            e.Graphics.DrawImage(m_oBackBuffer, clip, clip, GraphicsUnit.Pixel);
        }

        /// <summary>The centered 2x2-tile block the feature (now-playing/pinned) cover
        /// occupies, clamped for tiny grids.</summary>
        private Rectangle FeatureRect()
        {
            int bw = Math.Min(2, m_iXCovers);
            int bh = Math.Min(2, m_iYCovers);
            int cx = (m_iXCovers - bw) / 2;
            int cy = (m_iYCovers - bh) / 2;
            return new Rectangle(m_iOffsetX + cx * m_iTilePx, m_iOffsetY + cy * m_iTilePx,
                                 bw * m_iTilePx, bh * m_iTilePx);
        }

        private void PaintManageHint(Graphics g)
        {
            int w = ClientSize.Width;
            int bw = Math.Min(820, w - 80), bh = 44;
            var r = new Rectangle((w - bw) / 2, 18, bw, bh);
            using (var bg = new SolidBrush(Color.FromArgb(190, 30, 30, 30)))
                g.FillRectangle(bg, r);
            using (var f = new Font("Segoe UI", 13f, FontStyle.Regular))
            using (var br = new SolidBrush(Color.White))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString("Right-click any cover to hide it    -    Click or press Esc to exit", f, br, r, sf);

            // Now-playing line (artist + song title), shown just below the hint.
            if (!string.IsNullOrEmpty(m_sNowPlayingText))
            {
                int nh = 48;
                var nr = new Rectangle((w - bw) / 2, r.Bottom + 10, bw, nh);
                using (var bg = new SolidBrush(Color.FromArgb(190, 30, 30, 30)))
                using (var f = new Font("Segoe UI", 16f, FontStyle.Bold))
                using (var br = new SolidBrush(Color.White))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                {
                    g.FillRectangle(bg, nr);
                    g.DrawString(m_sNowPlayingText, f, br, nr, sf);
                }
            }
        }

        private void PaintToast(Graphics g)
        {
            int w = ClientSize.Width;
            int bw = Math.Min(640, w - 80), bh = 56;
            var r = new Rectangle((w - bw) / 2, 32, bw, bh);
            using (var bg = new SolidBrush(Color.FromArgb(180, 60, 0, 0)))
                g.FillRectangle(bg, r);
            using (var f = new Font("Segoe UI", 14f, FontStyle.Bold))
            using (var br = new SolidBrush(Color.White))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(m_sToastText, f, br, r, sf);
        }

        /// <summary>Advance animations and invalidate ONLY the tiles/feature that are
        /// actually animating (fades + highlight pulses), so each frame repaints a
        /// couple of small rects instead of the whole 4K surface.</summary>
        private void OnAnimTick(object sender, EventArgs e)
        {
            if (m_aTiles != null)
                for (int x = 0; x < m_iXCovers; x++)
                    for (int y = 0; y < m_iYCovers; y++)
                    {
                        var t = m_aTiles[x, y];
                        if (t == null) continue;
                        bool was = t.Animating;
                        t.Advance();
                        // Repaint while animating, plus the one frame it settles
                        // (was=true, now=false) so the final image lands.
                        if (was || t.Animating) Invalidate(TileRect(x, y));
                    }

            bool featWas = m_oFeatureTile.Animating;
            m_oFeatureTile.Advance();
            if (m_bFeatureVisible && (featWas || m_oFeatureTile.Animating))
                Invalidate(FeatureRect());
        }

        #endregion

        #region Mosaic

        private void InitiateTiles()
        {
            HashSet<string> oUsed = new HashSet<string>();
            for (int x = 0; x < m_iXCovers; x++)
                for (int y = 0; y < m_iYCovers; y++)
                {
                    var tile = new FadingTile { Effect = m_oEffect, TransitionDurationMs = m_iTransitionDurationMs };
                    var entry = m_oCoverMgr.GetRandomEntry(oUsed);
                    tile.SetImageImmediate(entry.Value);
                    m_aTiles[x, y] = tile;
                    m_aTileKeys[x, y] = entry.Key;
                    if (!string.IsNullOrEmpty(entry.Key))
                        oUsed.Add(entry.Key);
                }
        }

        private int m_iTickCounter;
        private void moveTimer_Tick(object sender, EventArgs e)
        {
            m_iTickCounter++;
            ChangePicture(); // invalidates just the swapped tile; the anim timer drives its fade
        }

        private void ChangePicture()
        {
            if (m_iXCovers <= 0 || m_iYCovers <= 0) return;
            if (m_bPinned) return;

            int iX = s_oRand.Next(m_iXCovers);
            int iY = s_oRand.Next(m_iYCovers);

            HashSet<string> oOnScreen = new HashSet<string>();
            for (int x = 0; x < m_iXCovers; x++)
                for (int y = 0; y < m_iYCovers; y++)
                {
                    string k = m_aTileKeys[x, y];
                    if (!string.IsNullOrEmpty(k)) oOnScreen.Add(k);
                }

            var entry = m_oCoverMgr.GetRandomEntry(oOnScreen);
            m_aTiles[iX, iY].SetImage(entry.Value);
            m_aTileKeys[iX, iY] = entry.Key;

            // Kick off the swapped tile's transition; the anim timer carries the rest.
            Invalidate(TileRect(iX, iY));
            RefreshHighlights();
        }

        /// <summary>Flip each tile's highlight based on whether its key matches the
        /// currently-playing SMTC key.</summary>
        private void RefreshHighlights()
        {
            if (m_aTiles == null) return;
            string playing = m_sCurrentPlayingKey;
            for (int x = 0; x < m_iXCovers; x++)
                for (int y = 0; y < m_iYCovers; y++)
                {
                    var t = m_aTiles[x, y];
                    if (t == null) continue;
                    t.Highlighted = !string.IsNullOrEmpty(playing) &&
                        string.Equals(m_aTileKeys[x, y], playing, StringComparison.OrdinalIgnoreCase);
                }
        }

        #endregion

        #region Focal "Now Playing" overlay

        private void OnNowPlayingUpdated(NowPlayingMonitor.NowPlayingInfo info)
        {
            if (info == null) return;
            if (m_bPinned) return;

            m_sCurrentPlayingKey = AlbumCoverMgr.BuildKey(info.Artist, info.Album);
            m_sNowPlayingText = BuildNowPlayingText(info);
            m_oFeatureTile.SetImage(info.Cover);
            m_bFeatureVisible = info.Cover != null;

            RefreshHighlights();
            Invalidate(FeatureRect());
        }

        private void OnNowPlayingCleared()
        {
            m_sCurrentPlayingKey = null;
            m_sNowPlayingText = string.Empty;
            if (m_bPinned) return;
            Rectangle fr = FeatureRect();
            m_bFeatureVisible = false;
            RefreshHighlights();
            Invalidate(fr); // repaint the 4 central tiles now that the cover is gone
        }

        /// <summary>"Artist - Title" for the manage-mode now-playing line.</summary>
        private static string BuildNowPlayingText(NowPlayingMonitor.NowPlayingInfo info)
        {
            string artist = info.Artist ?? string.Empty;
            string title = info.Title ?? string.Empty;
            if (string.IsNullOrEmpty(artist)) return title;
            if (string.IsNullOrEmpty(title)) return artist;
            return artist + "  —  " + title;
        }

        #endregion

        #region Manage mode (mouse moved -> pause + cursor + hint)

        private void EnterManageMode()
        {
            if (m_bPreviewMode || m_bManageMode) return;
            m_bManageMode = true;
            Cursor.Show();
            moveTimer.Stop();
            m_bManageHintVisible = true;
            if (m_oManageTimer == null)
            {
                m_oManageTimer = new System.Windows.Forms.Timer { Interval = MANAGE_MODE_TIMEOUT_MS };
                m_oManageTimer.Tick += (s, e) => ExitManageMode();
            }
            m_oManageTimer.Stop();
            m_oManageTimer.Start();
            Invalidate();
        }

        private void ExtendManageMode()
        {
            if (!m_bManageMode) return;
            m_oManageTimer?.Stop();
            m_oManageTimer?.Start();
        }

        private void ExitManageMode()
        {
            if (!m_bManageMode) return;
            m_bManageMode = false;
            m_oManageTimer?.Stop();
            m_bManageHintVisible = false;
            if (!m_bPreviewMode) Cursor.Hide();
            if (!m_bPinned) moveTimer.Start();
            mouseLocation = Point.Empty;
            Invalidate();
        }

        #endregion

        #region Click-to-pin (#2) and right-click to hide

        private void HandleTileClick(int x, int y, MouseEventArgs e)
        {
            if (m_bPreviewMode) return;

            if (e.Button == MouseButtons.Right)
            {
                HideTile(x, y);
                if (m_bManageMode) ExtendManageMode();
                return;
            }

            if (Control.ModifierKeys.HasFlag(Keys.Shift))
            {
                PinTile(x, y);
                return;
            }
            if (m_bPinned) { Unpin(); return; }
            if (e.Button == MouseButtons.Left) Application.Exit();
        }

        private void HideTile(int x, int y)
        {
            if (x < 0 || y < 0 || x >= m_iXCovers || y >= m_iYCovers) return;
            string key = m_aTileKeys[x, y];
            if (string.IsNullOrEmpty(key)) return;
            if (m_oCoverMgr == null || m_oCoverMgr.Blocklist == null) return;

            var meta = m_oCoverMgr.GetMetadata(key);
            m_oCoverMgr.Blocklist.Block(key);

            HashSet<string> onScreen = new HashSet<string>();
            for (int xx = 0; xx < m_iXCovers; xx++)
                for (int yy = 0; yy < m_iYCovers; yy++)
                {
                    var k = m_aTileKeys[xx, yy];
                    if (!string.IsNullOrEmpty(k)) onScreen.Add(k);
                }
            var entry = m_oCoverMgr.GetRandomEntry(onScreen);
            m_aTiles[x, y].SetImage(entry.Value);
            m_aTileKeys[x, y] = entry.Key;

            // Wipe any other on-screen instances of the hidden album.
            for (int xx = 0; xx < m_iXCovers; xx++)
                for (int yy = 0; yy < m_iYCovers; yy++)
                {
                    if (string.Equals(m_aTileKeys[xx, yy], key, StringComparison.OrdinalIgnoreCase))
                    {
                        var alt = m_oCoverMgr.GetRandomEntry(onScreen);
                        m_aTiles[xx, yy].SetImage(alt.Value);
                        m_aTileKeys[xx, yy] = alt.Key;
                    }
                }

            ShowHiddenToast(meta);
            RefreshHighlights();
            Invalidate();
        }

        private void ShowHiddenToast(AlbumMetadata meta)
        {
            m_sToastText = !string.IsNullOrEmpty(meta?.Artist) || !string.IsNullOrEmpty(meta?.Album)
                ? ($"Hidden: {meta.Artist} - {meta.Album}").Trim(' ', '-')
                : "Hidden.";
            m_bToastVisible = true;
            if (m_oToastTimer == null)
            {
                m_oToastTimer = new System.Windows.Forms.Timer { Interval = 2200 };
                m_oToastTimer.Tick += (s, e) =>
                {
                    m_oToastTimer.Stop();
                    m_bToastVisible = false;
                    Invalidate();
                };
            }
            m_oToastTimer.Stop();
            m_oToastTimer.Start();
            Invalidate();
        }

        private void PinTile(int x, int y)
        {
            if (x < 0 || y < 0 || x >= m_iXCovers || y >= m_iYCovers) return;
            string key = m_aTileKeys[x, y];
            if (string.IsNullOrEmpty(key)) return;
            var img = m_oCoverMgr?.GetPictureByKey(key);
            if (img == null) return;

            m_bPinned = true;
            m_sPinnedKey = key;

            m_oFeatureTile.SetImage(img);
            m_bFeatureVisible = true;
            Invalidate(FeatureRect());
        }

        private void Unpin()
        {
            m_bPinned = false;
            m_sPinnedKey = null;
            if (m_oNowPlaying?.Current != null)
                OnNowPlayingUpdated(m_oNowPlaying.Current);
            else
                OnNowPlayingCleared();
        }

        #endregion

        #region Input / quitting

        // --- Any key dismisses the screensaver -----------------------------------
        // Character keys raise WM_CHAR (handled by ScreenSaverForm_KeyPress), but the
        // function, arrow, modifier, and media/volume keys do NOT - and media transport
        // buttons arrive only as WM_APPCOMMAND. We catch all of those here so EVERY key
        // tears the saver down, matching standard screensaver behaviour. Mouse movement
        // is deliberately not a dismiss trigger - it raises the manage overlay instead.
        private const int WM_KEYDOWN    = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_APPCOMMAND = 0x0319;

        protected override void WndProc(ref Message m)
        {
            if (!m_bPreviewMode &&
                (m.Msg == WM_KEYDOWN || m.Msg == WM_SYSKEYDOWN || m.Msg == WM_APPCOMMAND))
            {
                Application.Exit();
                return;
            }

            base.WndProc(ref m);
        }

        private void ScreenSaverForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (m_bPreviewMode) return;
            if (m_bManageMode)
            {
                ExtendManageMode();
                mouseLocation = e.Location;
                return;
            }
            if (!mouseLocation.IsEmpty)
            {
                if (Math.Abs(mouseLocation.X - e.X) > 5 || Math.Abs(mouseLocation.Y - e.Y) > 5)
                    EnterManageMode();
            }
            mouseLocation = e.Location;
        }

        private void ScreenSaverForm_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (m_bPreviewMode) return;
            // Any character key dismisses (non-character/media keys are handled in WndProc).
            Application.Exit();
        }

        private void ScreenSaverForm_MouseClick(object sender, MouseEventArgs e)
        {
            if (m_bPreviewMode) return;
            // Route the click to the tile under the cursor (pin / hide / exit).
            if (TileAt(e.Location, out int x, out int y))
            {
                HandleTileClick(x, y, e);
                return;
            }
            // Outside the grid: left-click exits (or unpins), right-click does nothing.
            if (e.Button != MouseButtons.Left) return;
            if (m_bPinned) { Unpin(); return; }
            Application.Exit();
        }
        #endregion
    }
}
