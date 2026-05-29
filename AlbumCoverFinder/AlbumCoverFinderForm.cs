using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AlbumCoverFinder
{
    public partial class AlbumCoverFinderForm : Form
    {
        private AlbumCoverMgr oCoverMgr;
        private WallpaperRenderer m_oWallpaper;
        private LockScreenRenderer m_oLockScreen;
        public delegate void AlbumFound(int p_iAlbumFounds, Image p_oPicture);

        public AlbumCoverFinderForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Background-thread callback from the scanner. Marshals onto the UI thread
        /// so we can safely touch controls.
        /// </summary>
        private void AlbumFoundCallback(int p_iAlbumFounds, Image p_oPicture)
        {
            if (tDisplayUpdate.InvokeRequired)
            {
                var d = new NewCoverFound(NewCoverFoundCallback);
                tDisplayUpdate.Invoke(d, new object[] {p_oPicture });
            }
            else
            {
                string sStatus = "Total albums found: " + p_iAlbumFounds.ToString();
                if (oCoverMgr != null && oCoverMgr.CapReached)
                    sStatus += " (cap of " + oCoverMgr.MaxPicCount + " reached - extra albums skipped)";
                tDisplayUpdate.Text = sStatus;
                if (p_oPicture != null)
                    pictureBox1.Image = oCoverMgr.GetRandomPicture(320,320);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            if (string.IsNullOrEmpty(tFolderToParse.Text))
                tFolderToParse.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

            oCoverMgr = new AlbumCoverMgr();
            oCoverMgr.oAlbumFoundEvent += AlbumFoundCallback;
            var settings = ScreensaverSettings.Load();
            oCoverMgr.SetAlbumCap(settings.AlbumCap);

            m_oWallpaper = new WallpaperRenderer(oCoverMgr);
            m_oLockScreen = new LockScreenRenderer(oCoverMgr);

            if (oCoverMgr.GetAlbumTotal() > 0)
            {
                tDisplayUpdate.Text =
                    "Album covers in database: " + oCoverMgr.GetAlbumTotal() + "\r\n" +
                    "Hidden: " + oCoverMgr.Blocklist.Count + " album(s).\r\n" +
                    "The screensaver is ready to use. Visit the Scan tab to refresh the database.";
            }
            else
            {
                tDisplayUpdate.Text =
                    "Welcome! Start in the Scan tab: pick your music folder, then click \"Parse folder for albums\". " +
                    "Once scanning finishes, the screensaver will display your covers as a mosaic.";
            }
            pictureBox1.Image = oCoverMgr.GetRandomPicture();

            HydrateFilterUI(settings);
            HydrateHiddenUI();
            HydrateDisplayUI(settings);
            HydrateWallpaperUI(settings);
            HydrateLockScreenUI(settings);
            nAlbumCap.Value = settings.AlbumCap;
        }

        #region Display tab

        private void HydrateDisplayUI(ScreensaverSettings settings)
        {
            cbEffect.Items.Clear();
            cbEffect.Items.AddRange(new object[]
            {
                "Blink (instant)",
                "Fade to black",
                "Merge (per-pixel color blend)",
                "Flip (horizontal)"
            });
            cbEffect.SelectedIndex = (int)settings.Effect;

            nCoversWide.Value = Math.Max(nCoversWide.Minimum, Math.Min(nCoversWide.Maximum, settings.CoversWide));
            // Settings store milliseconds; the UI uses seconds for the user-facing values.
            nEffectDurationSec.Value = Math.Max(1, Math.Min(10, settings.TransitionDurationMs / 1000));
            nGapIntervalSec.Value = Math.Max(1, Math.Min(300, settings.GapBetweenTransitionsMs / 1000));
            UpdateEffectDurationEnabled();
        }

        private void cbEffect_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateEffectDurationEnabled();
        }

        private void UpdateEffectDurationEnabled()
        {
            // Blink has no animation, so the duration is meaningless.
            bool isBlink = cbEffect.SelectedIndex == (int)TransitionEffect.Blink;
            nEffectDurationSec.Enabled = !isBlink;
        }

        private void bResetCoversWide_Click(object sender, EventArgs e)
        {
            nCoversWide.Value = 0;
            tDisplayUpdate.Text = "Covers wide reset to native (0). Click \"Save display settings\" to persist.";
        }

        private void bSaveDisplay_Click(object sender, EventArgs e)
        {
            var s = ScreensaverSettings.Load();
            s.CoversWide = (int)nCoversWide.Value;
            s.Effect = (TransitionEffect)cbEffect.SelectedIndex;
            s.TransitionDurationMs = (int)nEffectDurationSec.Value * 1000;
            s.GapBetweenTransitionsMs = (int)nGapIntervalSec.Value * 1000;
            s.Save();

            int transition = s.Effect == TransitionEffect.Blink ? 0 : s.TransitionDurationMs;
            int totalMs = s.GapBetweenTransitionsMs + transition;
            tDisplayUpdate.Text =
                "Display settings saved. Each tile swap will take " +
                (totalMs / 1000.0).ToString("0.#") +
                " s total (gap " + (s.GapBetweenTransitionsMs / 1000.0).ToString("0.#") +
                " s + transition " + (transition / 1000.0).ToString("0.#") + " s).";
        }

        #endregion

        #region Scan tab

        private void bChangeFolder_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1 = new FolderBrowserDialog();
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
                tFolderToParse.Text = folderBrowserDialog1.SelectedPath;
        }

        private void bParseFolder_Click(object sender, EventArgs e)
        {
            if (Directory.Exists(tFolderToParse.Text))
                oCoverMgr.ParseDirectoryForPictures(tFolderToParse.Text);
            else
                tDisplayUpdate.Text = "Can't find that directory.";
        }

        private void bDeleteBackupFIle_Click(object sender, EventArgs e)
        {
            oCoverMgr.DeleteAlbumBackup();
            tDisplayUpdate.Text = "Cache deleted. Hit \"Parse folder for albums\" to rebuild it.";
            HydrateHiddenUI();
        }

        private void bApplyCap_Click(object sender, EventArgs e)
        {
            var s = ScreensaverSettings.Load();
            s.AlbumCap = (int)nAlbumCap.Value;
            s.Save();
            oCoverMgr.SetAlbumCap(s.AlbumCap);
            tDisplayUpdate.Text = s.AlbumCap == 0
                ? "Album cap removed (unlimited)."
                : "Album cap set to " + s.AlbumCap + ".";
        }

        #endregion

        #region Filter tab

        private void HydrateFilterUI(ScreensaverSettings settings)
        {
            cbGenreFilter.Items.Clear();
            cbGenreFilter.Items.Add(string.Empty);
            foreach (string g in oCoverMgr.GetDistinctGenres())
                cbGenreFilter.Items.Add(g);

            var range = oCoverMgr.GetYearRange();
            if (range.max > 0)
            {
                nYearMin.Minimum = 0;
                nYearMin.Maximum = range.max;
                nYearMax.Minimum = 0;
                nYearMax.Maximum = range.max + 1;
            }

            var f = CoverFilter.LoadFromRegistry();
            cbGenreFilter.Text = f.Genre ?? string.Empty;
            nYearMin.Value = Math.Max(nYearMin.Minimum, Math.Min(nYearMin.Maximum, f.YearMin));
            nYearMax.Value = Math.Max(nYearMax.Minimum, Math.Min(nYearMax.Maximum, f.YearMax));
        }

        private void bApplyFilter_Click(object sender, EventArgs e)
        {
            var f = new CoverFilter
            {
                Genre = (cbGenreFilter.Text ?? string.Empty).Trim(),
                YearMin = (int)nYearMin.Value,
                YearMax = (int)nYearMax.Value
            };
            f.SaveToRegistry();
            oCoverMgr.SetFilter(f.IsEmpty ? null : f);
            tDisplayUpdate.Text = f.IsEmpty
                ? "Filter cleared."
                : "Filter applied. The screensaver will show only matching albums.";
        }

        private void bClearFilter_Click(object sender, EventArgs e)
        {
            cbGenreFilter.Text = string.Empty;
            nYearMin.Value = 0;
            nYearMax.Value = 0;
            new CoverFilter().SaveToRegistry();
            oCoverMgr.SetFilter(null);
            tDisplayUpdate.Text = "Filter cleared.";
        }

        #endregion

        #region Hidden tab

        private void HydrateHiddenUI()
        {
            lstHidden.BeginUpdate();
            lstHidden.Items.Clear();
            foreach (string key in oCoverMgr.Blocklist.GetAll())
            {
                var meta = oCoverMgr.GetMetadata(key);
                string display = string.IsNullOrEmpty(meta.Artist) && string.IsNullOrEmpty(meta.Album)
                    ? key
                    : ($"{meta.Artist} - {meta.Album}").Trim(' ', '-');
                lstHidden.Items.Add(new HiddenItem { Key = key, Display = display });
            }
            lstHidden.EndUpdate();
        }

        private class HiddenItem
        {
            public string Key;
            public string Display;
            public override string ToString() => Display;
        }

        private void bUnhideSelected_Click(object sender, EventArgs e)
        {
            var picks = lstHidden.SelectedItems.Cast<HiddenItem>().ToList();
            foreach (var item in picks) oCoverMgr.Blocklist.Unblock(item.Key);
            HydrateHiddenUI();
            tDisplayUpdate.Text = picks.Count == 0 ? "No items selected." : "Unhidden " + picks.Count + " album(s).";
        }

        private void bUnhideAll_Click(object sender, EventArgs e)
        {
            int n = oCoverMgr.Blocklist.Count;
            oCoverMgr.Blocklist.Clear();
            HydrateHiddenUI();
            tDisplayUpdate.Text = "Unhidden " + n + " album(s).";
        }

        #endregion

        #region Wallpaper tab

        private void HydrateWallpaperUI(ScreensaverSettings settings)
        {
            cbWallpaperEnabled.Checked = settings.WallpaperEnabled;
            nWallpaperInterval.Value = Math.Max(1, Math.Min(1440, settings.WallpaperIntervalMinutes));
        }

        private void bWallpaperSave_Click(object sender, EventArgs e)
        {
            var s = ScreensaverSettings.Load();
            s.WallpaperEnabled = cbWallpaperEnabled.Checked;
            s.WallpaperIntervalMinutes = (int)nWallpaperInterval.Value;
            s.Save();
            tDisplayUpdate.Text = "Wallpaper settings saved. Tray companion will pick them up within ~1 minute.";
        }

        private void bWallpaperApplyNow_Click(object sender, EventArgs e)
        {
            try
            {
                m_oWallpaper.RefreshNow();
                tDisplayUpdate.Text = "Wallpaper applied: " + m_oWallpaper.OutputPath;
            }
            catch (Exception ex)
            {
                tDisplayUpdate.Text = "Wallpaper apply failed: " + ex.Message;
            }
        }

        #endregion

        #region Lock screen tab

        private void HydrateLockScreenUI(ScreensaverSettings settings)
        {
            cbLockScreenEnabled.Checked = settings.LockScreenEnabled;
            nLockScreenInterval.Value = Math.Max(5, Math.Min(1440, settings.LockScreenIntervalMinutes));
        }

        private void bLockScreenSave_Click(object sender, EventArgs e)
        {
            var s = ScreensaverSettings.Load();
            s.LockScreenEnabled = cbLockScreenEnabled.Checked;
            s.LockScreenIntervalMinutes = (int)nLockScreenInterval.Value;
            s.Save();
            tDisplayUpdate.Text = "Lock screen settings saved. Tray companion will pick them up within ~1 minute.";
        }

        private async void bLockScreenApplyNow_Click(object sender, EventArgs e)
        {
            bLockScreenApplyNow.Enabled = false;
            try
            {
                await m_oLockScreen.RefreshAsync();
                tDisplayUpdate.Text = "Lock screen applied: " + m_oLockScreen.OutputPath;
            }
            catch (Exception ex)
            {
                tDisplayUpdate.Text = "Lock screen apply failed: " + ex.Message;
            }
            finally
            {
                bLockScreenApplyNow.Enabled = true;
            }
        }

        #endregion
    }
}
