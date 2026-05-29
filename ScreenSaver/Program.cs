using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AlbumCoverFinder;

namespace ScreenSaver
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// Parses arguments and launches relevant section of the program.
        /// </summary>
        // PerMonitorAwareV2 - forces Windows to give us raw pixel coordinates on
        // screens with display scaling, instead of the scaled "logical" pixels.
        // Without this, a 4K monitor at 150% scaling reports as 2560x1440 to us.
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);
        [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll")] private static extern IntPtr GetThreadDpiAwarenessContext();
        [DllImport("user32.dll")] private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr value);

        [STAThread]
        static void Main(string[] p_aArgs)
        {
            // Force PerMonitorV2 before WinForms initializes. Even with the csproj
            // ApplicationHighDpiMode set, picker-launched screensavers sometimes
            // ignore the manifest. P/Invoke wins.
            try { SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2); }
            catch { /* best effort */ }

            // Log everything to a file so we can post-mortem any "black screen"
            // black-box failure - WinExe has no console, no event log entry on
            // a silent exit, and screensavers vanish before the user can react.
            DiagLog.Init();
            try
            {
                IntPtr ctx = GetThreadDpiAwarenessContext();
                int awareness = GetAwarenessFromDpiAwarenessContext(ctx);
                DiagLog.Write("DPI awareness = " + awareness + " (0=Unaware, 1=System, 2=PerMonitor, 3=PerMonitorV2, 4=UnawareGdiScaled)");
            }
            catch { }
            DiagLog.Write("Main started. args=[" + string.Join(", ", p_aArgs) + "]");
            AppDomain.CurrentDomain.UnhandledException += (s, e) => DiagLog.WriteError("UnhandledException", e.ExceptionObject as Exception);
            Application.ThreadException += (s, e) => DiagLog.WriteError("Application.ThreadException", e.Exception);

            string sFirstArg = string.Empty;
            string sSecondArg = null;

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                DiagLog.Write("Creating AlbumCoverMgr");
                AlbumCoverMgr oCoverMgr = new AlbumCoverMgr();
                DiagLog.Write("AlbumCoverMgr ready, " + oCoverMgr.GetAlbumTotal() + " covers in cache");

                DiagLog.Write("Creating NowPlayingMonitor");
                NowPlayingMonitor oNowPlaying = new NowPlayingMonitor(oCoverMgr.GetPictureByKey);

                ParseCommandLineArgs(ref sFirstArg, ref sSecondArg, p_aArgs);
                DiagLog.Write("Parsed args: first='" + sFirstArg + "' second='" + (sSecondArg ?? "null") + "'");

                if (sFirstArg == string.Empty || sFirstArg == "/c")
                {
                    DiagLog.Write("Branch /c: opening AlbumCoverFinderForm");
                    Application.Run(new AlbumCoverFinderForm());
                }
                else if (sFirstArg == "/p")
                {
                    if (sSecondArg == null)
                    {
                        DiagLog.Write("Branch /p: no hwnd provided");
                        MessageBox.Show("Sorry, but the expected window handle was not provided.",
                            "ScreenSaver", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        return;
                    }
                    IntPtr previewWndHandle = new IntPtr(long.Parse(sSecondArg));
                    DiagLog.Write("Branch /p: hwnd=" + previewWndHandle);
                    Application.Run(new ScreenSaverForm(previewWndHandle, oCoverMgr, oNowPlaying));
                }
                else if (sFirstArg == "/s")
                {
                    DiagLog.Write("Branch /s: enumerating screens");
                    ShowScreenSaver(oCoverMgr, oNowPlaying);
                    DiagLog.Write("All screensaver forms shown; entering Application.Run");
                    Application.Run();
                    DiagLog.Write("Application.Run returned");
                }
                else
                {
                    DiagLog.Write("Branch unknown arg: " + sFirstArg);
                    MessageBox.Show("Sorry, but the command line argument \"" + sFirstArg +
                        "\" is not valid.", "ScreenSaver",
                        MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
            catch (Exception ex)
            {
                DiagLog.WriteError("Main caught", ex);
            }
            DiagLog.Write("Main returning");
        }

        /// <summary>
        /// Display the form on each of the computer's monitors. All forms share the
        /// same AlbumCoverMgr and NowPlayingMonitor instances so SMTC isn't queried
        /// once per screen and so cover lookups stay cache-coherent.
        /// </summary>
        static void ShowScreenSaver(AlbumCoverMgr p_oCoverMgr, NowPlayingMonitor p_oNowPlaying)
        {
            int idx = 0;
            foreach (Screen oScreen in Screen.AllScreens)
            {
                DiagLog.Write("  screen[" + idx + "] bounds=" + oScreen.Bounds + " primary=" + oScreen.Primary);
                try
                {
                    ScreenSaverForm oScreensaver = new ScreenSaverForm(oScreen.Bounds, p_oCoverMgr, p_oNowPlaying);
                    DiagLog.Write("  screen[" + idx + "] form ctor OK, calling Show()");
                    oScreensaver.Show();
                    DiagLog.Write("  screen[" + idx + "] Show() returned; Visible=" + oScreensaver.Visible + " Handle=" + oScreensaver.Handle);
                }
                catch (Exception ex)
                {
                    DiagLog.WriteError("  screen[" + idx + "] failed", ex);
                }
                idx++;
            }
        }

        /// <summary>
        /// Returns the first arg from the command line, handles the case where the first arg contains both args separated by a colon (:)
        /// Possible examples: /c:1234567 or /P 1232154231 or /s
        /// Argument /p takes a long as second argument as handle to a window within which to draw the screensaver
        /// </summary>
        static void ParseCommandLineArgs(ref string p_sFirstArg, ref string p_sSecondArg, string[] p_aArgs)
        {
            if (p_aArgs.Length > 0)
                p_sFirstArg = p_aArgs[0].ToLower().Trim();
            if (p_sFirstArg.Length > 2)
            {
                p_sSecondArg = p_sFirstArg.Substring(3).Trim();
                p_sFirstArg = p_sFirstArg.Substring(0, 2);
            }
            else if (p_aArgs.Length > 1)
                p_sSecondArg = p_aArgs[1];
        }

    }
}
