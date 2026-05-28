using System;
using System.Windows.Forms;

namespace ScreenSaver.Tray
{
    /// <summary>
    /// Tray-resident companion. Hosts:
    ///   - the desktop wallpaper renderer (feature #3)
    ///   - the lock screen mosaic renderer (#18)
    ///   - the LAN HTTP companion at http://localhost:9999 (#9, #13)
    ///   - Discord Rich Presence (#4)
    /// All four ride on top of the same shared NowPlayingMonitor and AlbumCoverMgr
    /// the screensaver uses, so config (filter, scan path) flows automatically.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayContext());
        }
    }
}
