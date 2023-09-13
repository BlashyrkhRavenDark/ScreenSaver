using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using AlbumCoverFinder;

namespace ScreenSaver
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// Parses arguments and launches relevant section of the program
        /// </summary>
        [STAThread]
        static void Main(string[] p_aArgs)
        {
            string sFirstArg = "";
            string sSecondArg = null;
            AlbumCoverFinder.AlbumCoverMgr oCoverMgr;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            oCoverMgr = new AlbumCoverFinder.AlbumCoverMgr();               // 1 instance of cover manager is enough for multiple screens
            ParseCommandLineArgs(ref sFirstArg, ref sSecondArg, p_aArgs);   // Let's sort the arguments

            if (sFirstArg == "")                                            // No arguments - treat like /c
            {
                Application.Run(new AlbumCoverFinderForm());
                //ShowScreenSaver(oCoverMgr);
                //Application.Run();
            } 
            else if (sFirstArg == "/c")                                     // Configuration mode
                Application.Run(new AlbumCoverFinderForm());
            else if (sFirstArg == "/p")                                     // Preview mode
            {
                if (sSecondArg == null)
                {
                    MessageBox.Show("Sorry, but the expected window handle was not provided.",
                        "ScreenSaver", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }
                IntPtr previewWndHandle = new IntPtr(long.Parse(sSecondArg));
                Application.Run(new ScreenSaverForm(previewWndHandle, oCoverMgr));
            }
            else if (sFirstArg == "/s")                                     // Full-screen mode
            {
                ShowScreenSaver(oCoverMgr);
                Application.Run();
            }
            else                                                            // Undefined argument
            {
                MessageBox.Show("Sorry, but the command line argument \"" + sFirstArg +
                    "\" is not valid.", "ScreenSaver",
                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        /// <summary>
        /// Display the form on each of the computer's monitors.
        /// </summary>
        static void ShowScreenSaver(AlbumCoverFinder.AlbumCoverMgr p_oCoverMgr)
        {
            int i = 0;
            foreach (Screen oScreen in Screen.AllScreens)
            {
                ScreenSaverForm oScreensaver = new ScreenSaverForm(oScreen.Bounds, p_oCoverMgr);
                oScreensaver.Show();
                i++;
            }
        }

        /// <summary>
        /// Returns the first arg from the command line, handles the case where the first arg contains both args separated by a colon (:)
        /// Possible examples: /c:1234567 or /P 1232154231 or /s
        /// Argument /p takes a long as second argument as handle to a window within which to draw the screensaver
        /// </summary>
        /// <param name="p_aArgs"></param>
        /// <returns></returns>
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
