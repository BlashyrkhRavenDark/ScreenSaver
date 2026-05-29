using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using AlbumCoverFinder;

namespace ScreenSaver.Tray
{
    /// <summary>
    /// ApplicationContext for the tray-only app. Owns the singleton CoverMgr,
    /// NowPlayingMonitor, and the four companion features. Settings are loaded
    /// from HKCU on every tick so AlbumCoverFinder edits propagate without a restart.
    /// </summary>
    internal class TrayContext : ApplicationContext
    {
        private readonly NotifyIcon m_oTray;
        private readonly AlbumCoverMgr m_oCover;
        private readonly global::ScreenSaver.NowPlayingMonitor m_oMonitor;
        private readonly WallpaperRenderer m_oWallpaper;
        private readonly LockScreenRenderer m_oLockScreen;
        private readonly HttpCompanion m_oHttp;
        private readonly DiscordPresence m_oDiscord;

        private readonly System.Windows.Forms.Timer m_oTickTimer;
        private DateTime m_oLastWallpaperRefresh = DateTime.MinValue;
        private DateTime m_oLastLockScreenRefresh = DateTime.MinValue;
        private string m_sActiveDiscordAppId;

        private ScreensaverSettings m_oSettings;

        // Tick once per minute. Each tick re-reads settings, then decides whether
        // wallpaper / lock screen are due for a refresh based on their per-feature
        // interval.
        private const int TICK_INTERVAL_MS = 60_000;

        public TrayContext()
        {
            m_oCover = new AlbumCoverMgr(lazyLoad: true);
            var filter = CoverFilter.LoadFromRegistry();
            if (!filter.IsEmpty) m_oCover.SetFilter(filter);

            // Tray is the SOLE iTunes-COM poller on this machine. Everyone else
            // (screensaver, Stream Deck plugin) gets iTunes data via the shared
            // nowplaying.json/.png the polling instance publishes.
            // coverPersist captures iTunes-cache-only artwork (art not embedded in the
            // file) into the cover cache, so those albums join the mosaic after playing.
            m_oMonitor = new global::ScreenSaver.NowPlayingMonitor(
                m_oCover.GetPictureByKey, pollItunes: true,
                coverPersist: (artist, album, cover) => m_oCover.AddCover(artist, album, cover));
            m_oWallpaper = new WallpaperRenderer(m_oCover);
            m_oLockScreen = new LockScreenRenderer(m_oCover);
            m_oHttp = new HttpCompanion(m_oCover, m_oMonitor);
            m_oDiscord = new DiscordPresence();

            m_oSettings = ScreensaverSettings.Load();
            m_oCover.SetAlbumCap(m_oSettings.AlbumCap);

            m_oTray = new NotifyIcon
            {
                Icon = LoadTrayIcon(),
                Text = "ScreenSaver Companion",
                Visible = true
            };
            m_oTray.ContextMenuStrip = BuildMenu();
            m_oTray.DoubleClick += (s, e) => SafeRefreshWallpaper();

            m_oTickTimer = new System.Windows.Forms.Timer { Interval = TICK_INTERVAL_MS };
            m_oTickTimer.Tick += (s, e) => OnTick();
            m_oTickTimer.Start();

            _ = StartAsync();
        }

        private async Task StartAsync()
        {
            try { await m_oMonitor.StartAsync(); } catch { }

            m_oMonitor.Updated += OnNowPlayingUpdated;
            m_oMonitor.Cleared += OnNowPlayingCleared;

            m_oHttp.Start();
            ApplyDiscordSettings();

            // Initial wallpaper render so the user doesn't have to wait a full minute.
            if (m_oSettings.WallpaperEnabled)
            {
                SafeRefreshWallpaper();
                m_oLastWallpaperRefresh = DateTime.UtcNow;
            }
        }

        private void OnTick()
        {
            // Hot-reload settings. AlbumCoverFinder writes them; we apply them immediately.
            var prev = m_oSettings;
            m_oSettings = ScreensaverSettings.Load();
            m_oCover.SetAlbumCap(m_oSettings.AlbumCap);

            if (m_oSettings.DiscordAppId != prev.DiscordAppId)
                ApplyDiscordSettings();

            var now = DateTime.UtcNow;

            if (m_oSettings.WallpaperEnabled
                && (now - m_oLastWallpaperRefresh).TotalMinutes >= m_oSettings.WallpaperIntervalMinutes)
            {
                SafeRefreshWallpaper();
                m_oLastWallpaperRefresh = now;
            }

            if (m_oSettings.LockScreenEnabled
                && (now - m_oLastLockScreenRefresh).TotalMinutes >= m_oSettings.LockScreenIntervalMinutes)
            {
                _ = SafeRefreshLockScreenAsync();
                m_oLastLockScreenRefresh = now;
            }
        }

        private void ApplyDiscordSettings()
        {
            string id = (m_oSettings.DiscordAppId ?? string.Empty).Trim();
            if (id == m_sActiveDiscordAppId) return;
            m_sActiveDiscordAppId = id;
            m_oDiscord.Stop();
            if (!string.IsNullOrEmpty(id) && id != "0")
                m_oDiscord.Start(id);
        }

        private static Icon LoadTrayIcon()
        {
            // Use the project's CD-in-jewel-case icon. The csproj embeds it as a
            // Win32 resource via <ApplicationIcon>, so we can pull it from the
            // executing assembly without needing a side-by-side .ico file.
            try
            {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(exePath))
                    exePath = Environment.ProcessPath ?? string.Empty;
                if (!string.IsNullOrEmpty(exePath))
                {
                    var ico = Icon.ExtractAssociatedIcon(exePath);
                    if (ico != null) return ico;
                }
            }
            catch { }
            return SystemIcons.Application;
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();

            var refreshWall = new ToolStripMenuItem("Refresh wallpaper now");
            refreshWall.Click += (s, e) => SafeRefreshWallpaper();
            menu.Items.Add(refreshWall);

            var refreshLock = new ToolStripMenuItem("Refresh lock screen now");
            refreshLock.Click += async (s, e) => await SafeRefreshLockScreenAsync();
            menu.Items.Add(refreshLock);

            menu.Items.Add(new ToolStripSeparator());

            var openSettings = new ToolStripMenuItem("Open settings (Album Cover Finder)");
            openSettings.Click += (s, e) => LaunchFinder();
            menu.Items.Add(openSettings);

            var testScreensaver = new ToolStripMenuItem("Test screensaver");
            testScreensaver.Click += (s, e) => LaunchScreensaver();
            menu.Items.Add(testScreensaver);

            var openCompanion = new ToolStripMenuItem("Open web companion");
            openCompanion.Click += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("http://localhost:9999/") { UseShellExecute = true });
                }
                catch { }
            };
            menu.Items.Add(openCompanion);

            menu.Items.Add(new ToolStripSeparator());

            var quit = new ToolStripMenuItem("Quit");
            quit.Click += (s, e) => ExitThread();
            menu.Items.Add(quit);

            return menu;
        }

        /// <summary>
        /// Resolves Album Cover Finder's exe path from (in order):
        ///   1. HKCU\SOFTWARE\Demo_ScreenSaver\FinderPath - written by the install skill
        ///   2. A sibling Finder\ folder next to this tray exe (same deploy root)
        ///   3. A plain "AlbumCoverFinder.exe" shell lookup (PATH / Start menu)
        /// Pops a dialog if none of the three find it.
        /// </summary>
        private void LaunchFinder()
        {
            string path = ResolveFinderPath();
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
                    {
                        UseShellExecute = true,
                        WorkingDirectory = System.IO.Path.GetDirectoryName(path)
                    });
                    return;
                }
                catch
                {
                    // Fall through to the error dialog below.
                }
            }
            MessageBox.Show(
                "Couldn't find AlbumCoverFinder.exe. Run the project's /install skill again to deploy it, or open it from the Start menu (ScreenSaver -> Album Cover Finder).",
                "ScreenSaver", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Launches the screensaver in /s (full-screen) mode using the path Windows
        /// would use - HKCU\Control Panel\Desktop\SCRNSAVE.EXE - falling back to
        /// our own ScreenSaverPath pointer or a sibling App\ folder.
        /// </summary>
        private void LaunchScreensaver()
        {
            string path = ResolveScreensaverPath();
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                MessageBox.Show(
                    "Couldn't find the screensaver executable. Run the project's /install skill to deploy it.",
                    "ScreenSaver", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
                {
                    Arguments = "/s",
                    UseShellExecute = true,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(path)
                });
            }
            catch { }
        }

        private static string ResolveScreensaverPath()
        {
            // Prefer Windows' own SCRNSAVE.EXE so Test mirrors what idle-activation does.
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop"))
                {
                    string p = key?.GetValue("SCRNSAVE.EXE") as string;
                    if (!string.IsNullOrEmpty(p) && System.IO.File.Exists(p)) return p;
                }
            }
            catch { }
            // Fallback: our own copy of the path under HKCU\SOFTWARE\Demo_ScreenSaver.
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Demo_ScreenSaver"))
                {
                    string p = key?.GetValue("ScreenSaverPath") as string;
                    if (!string.IsNullOrEmpty(p) && System.IO.File.Exists(p)) return p;
                }
            }
            catch { }
            // Last resort: sibling App folder next to the tray exe.
            try
            {
                string trayDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string sibling = System.IO.Path.GetFullPath(System.IO.Path.Combine(trayDir, "..", "App", "ScreenSaver.exe"));
                if (System.IO.File.Exists(sibling)) return sibling;
            }
            catch { }
            return null;
        }

        private static string ResolveFinderPath()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Demo_ScreenSaver"))
                {
                    string fromReg = key?.GetValue("FinderPath") as string;
                    if (!string.IsNullOrEmpty(fromReg) && System.IO.File.Exists(fromReg))
                        return fromReg;
                }
            }
            catch { }

            try
            {
                string trayDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string sibling = System.IO.Path.Combine(trayDir, "..", "Finder", "AlbumCoverFinder.exe");
                sibling = System.IO.Path.GetFullPath(sibling);
                if (System.IO.File.Exists(sibling)) return sibling;
            }
            catch { }

            return "AlbumCoverFinder.exe"; // last-ditch PATH lookup
        }

        private void SafeRefreshWallpaper()
        {
            try { m_oWallpaper.RefreshNow(); }
            catch { /* an IO blip shouldn't crash the tray */ }
        }

        private async Task SafeRefreshLockScreenAsync()
        {
            try { await m_oLockScreen.RefreshAsync(); }
            catch { }
        }

        private void OnNowPlayingUpdated(global::ScreenSaver.NowPlayingMonitor.NowPlayingInfo info)
        {
            m_oDiscord.Update(info);
        }

        private void OnNowPlayingCleared()
        {
            m_oDiscord.Clear();
        }

        protected override void ExitThreadCore()
        {
            try
            {
                m_oMonitor.Updated -= OnNowPlayingUpdated;
                m_oMonitor.Cleared -= OnNowPlayingCleared;
            }
            catch { }
            m_oTickTimer.Stop();
            m_oTray.Visible = false;
            m_oTray.Dispose();
            m_oHttp.Stop();
            m_oDiscord.Stop();
            base.ExitThreadCore();
        }
    }
}
