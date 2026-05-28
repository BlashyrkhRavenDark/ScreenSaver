using System;
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
        [STAThread]
        static void Main(string[] p_aArgs)
        {
            string sFirstArg = string.Empty;
            string sSecondArg = null;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AlbumCoverMgr oCoverMgr = new AlbumCoverMgr();
            NowPlayingMonitor oNowPlaying = new NowPlayingMonitor(oCoverMgr.GetPictureByKey);

            ParseCommandLineArgs(ref sFirstArg, ref sSecondArg, p_aArgs);

            if (sFirstArg == string.Empty || sFirstArg == "/c")
            {
                Application.Run(new AlbumCoverFinderForm());
            }
            else if (sFirstArg == "/p")
            {
                if (sSecondArg == null)
                {
                    MessageBox.Show("Sorry, but the expected window handle was not provided.",
                        "ScreenSaver", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }
                IntPtr previewWndHandle = new IntPtr(long.Parse(sSecondArg));
                Application.Run(new ScreenSaverForm(previewWndHandle, oCoverMgr, oNowPlaying));
            }
            else if (sFirstArg == "/s")
            {
                ShowScreenSaver(oCoverMgr, oNowPlaying);
                Application.Run();
            }
            else
            {
                MessageBox.Show("Sorry, but the command line argument \"" + sFirstArg +
                    "\" is not valid.", "ScreenSaver",
                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        /// <summary>
        /// Display the form on each of the computer's monitors. All forms share the
        /// same AlbumCoverMgr and NowPlayingMonitor instances so SMTC isn't queried
        /// once per screen and so cover lookups stay cache-coherent.
        /// </summary>
        static void ShowScreenSaver(AlbumCoverMgr p_oCoverMgr, NowPlayingMonitor p_oNowPlaying)
        {
            foreach (Screen oScreen in Screen.AllScreens)
            {
                ScreenSaverForm oScreensaver = new ScreenSaverForm(oScreen.Bounds, p_oCoverMgr, p_oNowPlaying);
                oScreensaver.Show();
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
