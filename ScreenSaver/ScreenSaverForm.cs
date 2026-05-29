using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using AlbumCoverFinder;

namespace ScreenSaver
{
    /// <summary>
    /// The visual surface of the screensaver - one Form per monitor.
    /// Builds a centered mosaic of square tiles, swaps one tile per second with a
    /// crossfade animation, and overlays a focal "Now Playing" tile whenever the
    /// Windows SMTC reports an active media session.
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
        private FadingPictureBox[,] m_aPictureBoxes;
        private string[,] m_aTileKeys;
        private AlbumCoverMgr m_oCoverMgr;
        private NowPlayingMonitor m_oNowPlaying;
        private string m_sCurrentPlayingKey;

        // Display settings (live in the registry; loaded in LoadSettings).
        private int m_iCoversWide;                              // 0 = native (use DEFAULT_TARGET_TILE_PX)
        private TransitionEffect m_oEffect = TransitionEffect.Merge;
        private int m_iTransitionDurationMs = 1500;
        private int m_iGapBetweenTransitionsMs = 2000;

        // Focal "Now Playing" overlay (#11 + #8)
        private FadingPictureBox m_oFocalBox;
        private FadingPictureBox m_oSiblingBox;
        private Label m_oFocalCaption;
        private Label m_oSiblingCaption;
        private Panel m_oFocalShade;

        // Click-to-pin overlay (#2). Reuses the focal shade for dimming but has
        // its own caption text so the metadata reveal is more verbose than the
        // SMTC focal caption.
        private bool m_bPinned;
        private string m_sPinnedKey;

        // Right-click-to-hide transient toast
        private Label m_oHiddenToast;
        private System.Windows.Forms.Timer m_oToastTimer;

        // Manage mode (cursor visible, swap paused, right-click-to-hide armed).
        // Triggered by any mouse movement past the exit threshold. Auto-exits after
        // MANAGE_MODE_TIMEOUT_MS of no input; left-click/key/Esc exit immediately.
        private bool m_bManageMode;
        private System.Windows.Forms.Timer m_oManageTimer;
        private Label m_oManageHint;
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
            m_oCoverMgr = pCoverMgr;
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
            m_oCoverMgr = pCoverMgr;
            m_iXCovers = 3;
            m_iYCovers = 3;
            m_iCoverWidth = 50;
            m_iCoverHeight = 50;
            m_aPictureBoxes = new PictureBox[m_iXCovers, m_iYCovers];

            SetParent(this.Handle, PreviewWndHandle);
            SetWindowLong(this.Handle, -16, new IntPtr(GetWindowLong(this.Handle, -16) | 0x40000000));
            Rectangle ParentRect;
            GetClientRect(PreviewWndHandle, out ParentRect);

            Size = ParentRect.Size;
            Location = new Point(0, 0);
            m_bPreviewMode = true;
            LoadSettings();
            // The preview pane is tiny; ignore CoversWide so a "20 covers wide" setting
            // doesn't produce 20 micro-thumbs in the Windows Settings preview.
            int previousCoversWide = m_iCoversWide;
            m_iCoversWide = 0;
            ComputeGrid(new Rectangle(0, 0, ParentRect.Width, ParentRect.Height), PREVIEW_TARGET_TILE_PX);
            m_iCoversWide = previousCoversWide;
        }
        #endregion

        #region Grid sizing

        /// <summary>
        /// Grid layout starts at the top-left corner with no centering. If the
        /// user picked a column count (<see cref="m_iCoversWide"/>), tile size is
        /// derived from screen width / columns; otherwise tile size equals the
        /// native cover resolution (<paramref name="pTargetTilePx"/>) and the
        /// column count is chosen to cover the screen, allowing the right edge
        /// and bottom edge to clip a partial tile rather than leaving a black bar.
        /// </summary>
        private void ComputeGrid(Rectangle pBounds, int pTargetTilePx)
        {
            int iTile, iCols;
            if (m_iCoversWide > 0)
            {
                iCols = m_iCoversWide;
                // Integer division: tile fits cleanly across the width.
                iTile = Math.Max(32, pBounds.Width / iCols);
            }
            else
            {
                iTile = Math.Max(32, pTargetTilePx);
                // Ceiling division: cover the full width even if the last column overflows.
                iCols = (pBounds.Width + iTile - 1) / iTile;
            }
            // Ceiling division for rows: cover the full height even if the last row clips.
            int iRows = (pBounds.Height + iTile - 1) / iTile;

            m_iXCovers = Math.Max(1, iCols);
            m_iYCovers = Math.Max(1, iRows);
            m_iTilePx = iTile;
            m_iOffsetX = 0;
            m_iOffsetY = 0;
            m_aPictureBoxes = new FadingPictureBox[m_iXCovers, m_iYCovers];
            m_aTileKeys = new string[m_iXCovers, m_iYCovers];
        }

        #endregion

        #region Initialisation and other events

        private async void ScreenSaverForm_Load(object sender, EventArgs e)
        {
            try
            {
                DiagLog.Write("Form_Load start, preview=" + m_bPreviewMode + " bounds=" + this.Bounds);
                LoadSettings();
                DiagLog.Write("  settings: effect=" + m_oEffect + " trans=" + m_iTransitionDurationMs + " gap=" + m_iGapBetweenTransitionsMs + " coversWide=" + m_iCoversWide);
                ApplyFilterFromRegistry();
                if (!m_bPreviewMode) Cursor.Hide();
                TopMost = !m_bPreviewMode;
                KeyPreview = true;
                DiagLog.Write("  calling InitiatePictureBoxes, grid=" + m_iXCovers + "x" + m_iYCovers + " tile=" + m_iTilePx);
                InitiatePictureBoxes();
                DiagLog.Write("  InitiatePictureBoxes OK, Controls.Count=" + this.Controls.Count);
                BuildFocalOverlay();
                BuildManageOverlay();
                moveTimer.Interval = ComputeSwapPeriodMs();
                moveTimer.Tick += new EventHandler(moveTimer_Tick);
                moveTimer.Start();
                DiagLog.Write("  moveTimer started, interval=" + moveTimer.Interval + "ms");

                if (m_oNowPlaying != null)
                {
                    m_oNowPlaying.Updated += OnNowPlayingUpdated;
                    m_oNowPlaying.Cleared += OnNowPlayingCleared;
                    try { await m_oNowPlaying.StartAsync(); }
                    catch (Exception ex) { DiagLog.WriteError("  NowPlayingMonitor.StartAsync", ex); }
                    if (m_oNowPlaying.Current != null)
                        OnNowPlayingUpdated(m_oNowPlaying.Current);
                }
                DiagLog.Write("Form_Load complete, Visible=" + Visible + " Handle=" + Handle + " IsHandleCreated=" + IsHandleCreated + " ClientSize=" + ClientSize + " Bounds=" + Bounds);
                // Also log the first tile's actual state on screen so we can see
                // if it's being positioned/sized correctly.
                if (m_aPictureBoxes != null && m_iXCovers > 0 && m_iYCovers > 0)
                {
                    var t0 = m_aPictureBoxes[0, 0];
                    if (t0 != null)
                        DiagLog.Write("  tile[0,0]: Visible=" + t0.Visible + " Bounds=" + t0.Bounds + " HandleCreated=" + t0.IsHandleCreated + " Parent=" + (t0.Parent != null) + " ImageNull=" + (t0.Image == null));
                    var tLast = m_aPictureBoxes[m_iXCovers - 1, m_iYCovers - 1];
                    if (tLast != null)
                        DiagLog.Write("  tile[last]: Bounds=" + tLast.Bounds + " HandleCreated=" + tLast.IsHandleCreated);
                }
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

        /// <summary>
        /// Tile-swap cadence: the quiet gap plus the transition itself (zero for
        /// Blink because there's no animation). This is what the user actually
        /// experiences as "time between covers."
        /// </summary>
        private int ComputeSwapPeriodMs()
        {
            int transitionMs = m_oEffect == TransitionEffect.Blink ? 0 : m_iTransitionDurationMs;
            return Math.Max(100, m_iGapBetweenTransitionsMs + transitionMs);
        }

        private int m_iTickCounter;
        private void moveTimer_Tick(object sender, EventArgs e)
        {
            m_iTickCounter++;
            if (m_iTickCounter <= 6) DiagLog.Write("moveTimer_Tick #" + m_iTickCounter + " pinned=" + m_bPinned + " manage=" + m_bManageMode);
            ChangePicture();
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
            DisposeAllTileImages();
            base.OnFormClosed(e);
        }

        private void DisposeAllTileImages()
        {
            if (m_aPictureBoxes == null) return;
            for (int x = 0; x < m_iXCovers; x++)
                for (int y = 0; y < m_iYCovers; y++)
                {
                    var pb = m_aPictureBoxes[x, y];
                    if (pb != null)
                        pb.SetImageImmediate(null);
                }
        }
        #endregion

        #region Mosaic
        private void InitiatePictureBoxes()
        {
            HashSet<string> oUsed = new HashSet<string>();
            for (int iCptX = 0; iCptX < m_iXCovers; iCptX++)
                for (int iCptY = 0; iCptY < m_iYCovers; iCptY++)
                {
                    int x = iCptX, y = iCptY; // capture for the closure below
                    var pb = new FadingPictureBox();
                    pb.Effect = m_oEffect;
                    pb.TransitionDurationMs = m_iTransitionDurationMs;
                    var entry = m_oCoverMgr.GetRandomEntry(oUsed);
                    pb.SetImageImmediate(entry.Value);
                    pb.Width = m_iTilePx;
                    pb.Height = m_iTilePx;
                    pb.Left = m_iOffsetX + iCptX * m_iTilePx;
                    pb.Top = m_iOffsetY + iCptY * m_iTilePx;
                    pb.BackColor = Color.Black;
                    pb.MouseClick += (s, e) => OnTileClicked(x, y, e);
                    pb.MouseMove += ScreenSaverForm_MouseMove; // mouse movement on tiles should still exit
                    m_aPictureBoxes[iCptX, iCptY] = pb;
                    m_aTileKeys[iCptX, iCptY] = entry.Key;
                    if (!string.IsNullOrEmpty(entry.Key))
                        oUsed.Add(entry.Key);
                    this.Controls.Add(pb);
                }
        }

        private void ChangePicture()
        {
            if (m_iXCovers <= 0 || m_iYCovers <= 0) { if (m_iTickCounter <= 6) DiagLog.Write("ChangePicture skipped: no grid"); return; }
            if (m_bPinned) { if (m_iTickCounter <= 6) DiagLog.Write("ChangePicture skipped: pinned"); return; }

            int iX = s_oRand.Next(m_iXCovers);
            int iY = s_oRand.Next(m_iYCovers);

            HashSet<string> oOnScreen = new HashSet<string>();
            for (int x = 0; x < m_iXCovers; x++)
                for (int y = 0; y < m_iYCovers; y++)
                {
                    string k = m_aTileKeys[x, y];
                    if (!string.IsNullOrEmpty(k))
                        oOnScreen.Add(k);
                }

            var entry = m_oCoverMgr.GetRandomEntry(oOnScreen);
            if (m_iTickCounter <= 6) DiagLog.Write("ChangePicture tick #" + m_iTickCounter + " at (" + iX + "," + iY + ") newKey='" + entry.Key + "' newImgNull=" + (entry.Value == null));
            m_aPictureBoxes[iX, iY].Image = entry.Value;
            m_aTileKeys[iX, iY] = entry.Key;

            // The swapped-in tile may match what's currently playing (#11), so
            // re-evaluate highlights every tick.
            RefreshHighlights();
        }

        /// <summary>
        /// Walks every tile and flips its Highlighted flag based on whether its
        /// key matches the currently-playing SMTC key. Cheap - one pass over the
        /// mosaic, only changes flags that actually flipped.
        /// </summary>
        private void RefreshHighlights()
        {
            if (m_aPictureBoxes == null) return;
            string playing = m_sCurrentPlayingKey;
            for (int x = 0; x < m_iXCovers; x++)
                for (int y = 0; y < m_iYCovers; y++)
                {
                    var pb = m_aPictureBoxes[x, y];
                    if (pb == null) continue;
                    pb.Highlighted = !string.IsNullOrEmpty(playing) && string.Equals(m_aTileKeys[x, y], playing, StringComparison.OrdinalIgnoreCase);
                }
        }

        #endregion

        #region Focal "Now Playing" overlay

        private void BuildFocalOverlay()
        {
            int iFocal = (int)(Math.Min(ClientSize.Width, ClientSize.Height) * 0.45);
            if (iFocal < 64) iFocal = 64;
            int iSibling = Math.Max(32, iFocal / 3);

            m_oFocalShade = new Panel
            {
                BackColor = Color.FromArgb(140, 0, 0, 0),
                Size = ClientSize,
                Location = Point.Empty,
                Visible = false
            };
            this.Controls.Add(m_oFocalShade);

            int focalX = (ClientSize.Width - iFocal) / 2;
            int focalY = (ClientSize.Height - iFocal) / 2;

            m_oFocalBox = new FadingPictureBox
            {
                Size = new Size(iFocal, iFocal),
                Location = new Point(focalX, focalY),
                BackColor = Color.Black,
                Visible = false,
                TransitionDurationMs = 600
            };
            this.Controls.Add(m_oFocalBox);

            int captionH = Math.Max(40, iFocal / 8);
            m_oFocalCaption = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", Math.Max(10f, iFocal / 22f), FontStyle.Regular),
                Size = new Size((int)(iFocal * 1.6), captionH * 3),
                Location = new Point(
                    (ClientSize.Width - (int)(iFocal * 1.6)) / 2,
                    focalY + iFocal + 12),
                Visible = false
            };
            this.Controls.Add(m_oFocalCaption);

            // Sibling thumbnail (#8) - tucked just above-right of the focal tile so
            // it reads as "and there's more of this artist in your library."
            m_oSiblingBox = new FadingPictureBox
            {
                Size = new Size(iSibling, iSibling),
                Location = new Point(focalX + iFocal + 12, focalY),
                BackColor = Color.Black,
                Visible = false,
                TransitionDurationMs = 400
            };
            this.Controls.Add(m_oSiblingBox);

            m_oSiblingCaption = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(200, 200, 200),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", Math.Max(8f, iSibling / 14f), FontStyle.Italic),
                Size = new Size(iSibling + 60, captionH),
                Location = new Point(focalX + iFocal + 12 - 30, focalY + iSibling + 4),
                Visible = false
            };
            this.Controls.Add(m_oSiblingCaption);

            foreach (var pb in m_aPictureBoxes)
                if (pb != null) pb.SendToBack(); // mosaic is the bottom layer
            m_oFocalShade.BringToFront();
            m_oFocalBox.BringToFront();
            m_oSiblingBox.BringToFront();
            m_oFocalCaption.BringToFront();
            m_oSiblingCaption.BringToFront();
        }

        private void OnNowPlayingUpdated(NowPlayingMonitor.NowPlayingInfo info)
        {
            if (m_oFocalBox == null || info == null) return;
            if (m_bPinned) return; // pin overrides SMTC display until unpinned

            m_sCurrentPlayingKey = AlbumCoverMgr.BuildKey(info.Artist, info.Album);
            m_oFocalBox.Image = info.Cover;
            m_oFocalCaption.Text = BuildCaption(info, m_oCoverMgr?.GetMetadata(m_sCurrentPlayingKey));
            m_oFocalShade.Visible = true;
            m_oFocalBox.Visible = true;
            m_oFocalCaption.Visible = true;

            ShowSibling(m_sCurrentPlayingKey);
            RefreshHighlights();
        }

        private void OnNowPlayingCleared()
        {
            if (m_oFocalBox == null) return;
            m_sCurrentPlayingKey = null;
            if (m_bPinned) return; // keep the pin visible even if SMTC quiets down
            m_oFocalShade.Visible = false;
            m_oFocalBox.Visible = false;
            m_oFocalCaption.Visible = false;
            m_oSiblingBox.Visible = false;
            m_oSiblingCaption.Visible = false;
            RefreshHighlights();
        }

        private void ShowSibling(string playingKey)
        {
            if (m_oCoverMgr == null || string.IsNullOrEmpty(playingKey))
            {
                m_oSiblingBox.Visible = false;
                m_oSiblingCaption.Visible = false;
                return;
            }
            string siblingKey = m_oCoverMgr.GetSiblingKey(playingKey);
            if (string.IsNullOrEmpty(siblingKey))
            {
                m_oSiblingBox.Visible = false;
                m_oSiblingCaption.Visible = false;
                return;
            }
            var siblingImg = m_oCoverMgr.GetPictureByKey(siblingKey);
            if (siblingImg == null)
            {
                m_oSiblingBox.Visible = false;
                m_oSiblingCaption.Visible = false;
                return;
            }
            var meta = m_oCoverMgr.GetMetadata(siblingKey);
            m_oSiblingBox.Image = siblingImg;
            m_oSiblingCaption.Text = string.IsNullOrEmpty(meta.Album)
                ? "More by this artist"
                : "Also: " + meta.Album;
            m_oSiblingBox.Visible = true;
            m_oSiblingCaption.Visible = true;
        }

        private static string BuildCaption(NowPlayingMonitor.NowPlayingInfo info, AlbumMetadata meta)
        {
            string top = info.Title ?? string.Empty;
            string mid = info.Artist ?? string.Empty;
            if (!string.IsNullOrEmpty(info.Album) && info.Album != top)
                mid = string.IsNullOrEmpty(mid) ? info.Album : (mid + "  -  " + info.Album);
            string bottom = string.Empty;
            if (meta != null)
            {
                if (meta.Year > 0) bottom = meta.Year.ToString();
                if (!string.IsNullOrEmpty(meta.Genre))
                    bottom = string.IsNullOrEmpty(bottom) ? meta.Genre : (bottom + "  -  " + meta.Genre);
            }
            return top + "\n" + mid + (string.IsNullOrEmpty(bottom) ? string.Empty : ("\n" + bottom));
        }

        #endregion

        #region Manage mode (mouse moved -> pause + cursor + hint)

        private void BuildManageOverlay()
        {
            if (m_bPreviewMode) return;

            m_oManageHint = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(190, 30, 30, 30),
                Font = new Font("Segoe UI", 13f, FontStyle.Regular),
                Text = "Right-click any cover to hide it    -    Click or press Esc to exit",
                Size = new Size(Math.Min(820, ClientSize.Width - 80), 44),
                Visible = false
            };
            m_oManageHint.Location = new Point((ClientSize.Width - m_oManageHint.Width) / 2, 18);
            this.Controls.Add(m_oManageHint);
            m_oManageHint.BringToFront();

            m_oManageTimer = new System.Windows.Forms.Timer { Interval = MANAGE_MODE_TIMEOUT_MS };
            m_oManageTimer.Tick += (s, e) => ExitManageMode();
        }

        private void EnterManageMode()
        {
            if (m_bPreviewMode || m_bManageMode) return;
            m_bManageMode = true;
            Cursor.Show();
            moveTimer.Stop();
            if (m_oManageHint != null)
            {
                m_oManageHint.Visible = true;
                m_oManageHint.BringToFront();
            }
            m_oManageTimer?.Stop();
            m_oManageTimer?.Start();
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
            if (m_oManageHint != null) m_oManageHint.Visible = false;
            if (!m_bPreviewMode) Cursor.Hide();
            if (!m_bPinned) moveTimer.Start();
            // Reset the move baseline so the very next mouse twitch isn't immediately
            // treated as a "moved past threshold" event - the user needs to actually
            // start a fresh interaction to re-enter manage mode.
            mouseLocation = Point.Empty;
        }

        #endregion

        #region Click-to-pin (#2) and right-click to hide
        private void OnTileClicked(int x, int y, MouseEventArgs e)
        {
            if (m_bPreviewMode) return;

            // Right-click: hide the album (add to blocklist, swap tile, brief toast).
            // Never exits - hiding wouldn't make sense if the screensaver vanished
            // before you could see which one got hidden. Also bumps the manage-mode
            // timer so the user can chain several hides without it timing out.
            if (e.Button == MouseButtons.Right)
            {
                HideTile(x, y);
                if (m_bManageMode) ExtendManageMode();
                return;
            }

            // Shift+left-click: pin the tile.
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

            // Replace the now-hidden tile with a fresh pick (which automatically
            // skips the new blocklist entry because GetRandomEntry uses the filter).
            HashSet<string> onScreen = new HashSet<string>();
            for (int xx = 0; xx < m_iXCovers; xx++)
                for (int yy = 0; yy < m_iYCovers; yy++)
                {
                    var k = m_aTileKeys[xx, yy];
                    if (!string.IsNullOrEmpty(k)) onScreen.Add(k);
                }
            var entry = m_oCoverMgr.GetRandomEntry(onScreen);
            m_aPictureBoxes[x, y].Image = entry.Value;
            m_aTileKeys[x, y] = entry.Key;

            // Wipe any other on-screen instances of the hidden album too (rare but possible).
            for (int xx = 0; xx < m_iXCovers; xx++)
                for (int yy = 0; yy < m_iYCovers; yy++)
                {
                    if (string.Equals(m_aTileKeys[xx, yy], key, StringComparison.OrdinalIgnoreCase))
                    {
                        var alt = m_oCoverMgr.GetRandomEntry(onScreen);
                        m_aPictureBoxes[xx, yy].Image = alt.Value;
                        m_aTileKeys[xx, yy] = alt.Key;
                    }
                }

            ShowHiddenToast(meta);
            RefreshHighlights();
        }

        private void ShowHiddenToast(AlbumMetadata meta)
        {
            if (m_oHiddenToast == null)
            {
                m_oHiddenToast = new Label
                {
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.White,
                    BackColor = Color.FromArgb(180, 60, 0, 0),
                    Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                    Size = new Size(Math.Min(640, ClientSize.Width - 80), 56),
                    Visible = false
                };
                m_oHiddenToast.Location = new Point(
                    (ClientSize.Width - m_oHiddenToast.Width) / 2,
                    32);
                this.Controls.Add(m_oHiddenToast);
                m_oHiddenToast.BringToFront();

                m_oToastTimer = new System.Windows.Forms.Timer { Interval = 2200 };
                m_oToastTimer.Tick += (s, e) =>
                {
                    m_oToastTimer.Stop();
                    if (m_oHiddenToast != null) m_oHiddenToast.Visible = false;
                };
            }

            string label = !string.IsNullOrEmpty(meta?.Artist) || !string.IsNullOrEmpty(meta?.Album)
                ? ($"Hidden: {meta.Artist} - {meta.Album}").Trim(' ', '-')
                : "Hidden.";
            m_oHiddenToast.Text = label;
            m_oHiddenToast.Visible = true;
            m_oHiddenToast.BringToFront();
            m_oToastTimer.Stop();
            m_oToastTimer.Start();
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

            m_oFocalBox.Image = img;
            m_oFocalCaption.Text = BuildPinnedCaption(key);
            m_oFocalShade.Visible = true;
            m_oFocalBox.Visible = true;
            m_oFocalCaption.Visible = true;
            // Hide sibling box during pin to keep the focus clean - the user is
            // examining a specific album, not browsing.
            m_oSiblingBox.Visible = false;
            m_oSiblingCaption.Visible = false;
        }

        private void Unpin()
        {
            m_bPinned = false;
            m_sPinnedKey = null;
            // Restore whatever SMTC was showing (if anything) or hide the focal entirely.
            if (m_oNowPlaying?.Current != null)
                OnNowPlayingUpdated(m_oNowPlaying.Current);
            else
                OnNowPlayingCleared();
        }

        private string BuildPinnedCaption(string key)
        {
            if (m_oCoverMgr == null) return key;
            var meta = m_oCoverMgr.GetMetadata(key);
            string artist = string.IsNullOrEmpty(meta.Artist) ? "(unknown artist)" : meta.Artist;
            string album = string.IsNullOrEmpty(meta.Album) ? "(unknown album)" : meta.Album;
            string yearGenre = string.Empty;
            if (meta.Year > 0) yearGenre = meta.Year.ToString();
            if (!string.IsNullOrEmpty(meta.Genre))
                yearGenre = string.IsNullOrEmpty(yearGenre) ? meta.Genre : (yearGenre + "  -  " + meta.Genre);
            return artist + "\n" + album + (string.IsNullOrEmpty(yearGenre) ? string.Empty : ("\n" + yearGenre));
        }
        #endregion

        #region Quitting the Screensaver
        private void ScreenSaverForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (m_bPreviewMode) return;
            // In manage mode, every movement keeps us alive (resets the auto-exit
            // timer) so the user can roam toward a cover to hide it.
            if (m_bManageMode)
            {
                ExtendManageMode();
                mouseLocation = e.Location;
                return;
            }
            if (!mouseLocation.IsEmpty)
            {
                if (Math.Abs(mouseLocation.X - e.X) > 5 ||
                    Math.Abs(mouseLocation.Y - e.Y) > 5)
                {
                    // First real movement: enter manage mode rather than exit immediately.
                    // Gives the user a chance to right-click a cover to hide it. Left-click
                    // or any key still exits.
                    EnterManageMode();
                }
            }
            mouseLocation = e.Location;
        }

        private void ScreenSaverForm_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (m_bPreviewMode) return;
            // Escape always exits. Other keys: if pinned, unpin; otherwise exit.
            if (e.KeyChar == (char)Keys.Escape) { Application.Exit(); return; }
            if (m_bPinned) { Unpin(); return; }
            Application.Exit();
        }

        private void ScreenSaverForm_MouseClick(object sender, MouseEventArgs e)
        {
            if (m_bPreviewMode) return;
            // Right-click on the background does nothing - keeps it consistent with
            // right-click on a tile (which hides instead of exiting).
            if (e.Button != MouseButtons.Left) return;
            if (m_bPinned) { Unpin(); return; }
            Application.Exit();
        }
        #endregion
    }
}
