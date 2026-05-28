using System;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Storage;
using Windows.System.UserProfile;

namespace AlbumCoverFinder
{
    /// <summary>
    /// Renders the mosaic to a JPEG and asks Windows to use it as the lock-screen
    /// wallpaper via <see cref="LockScreen.SetImageFileAsync"/>. Requires the
    /// process to run with desktop user context (no admin needed) and the
    /// net10.0-windows10.0.19041.0 TFM to see the WinRT API.
    ///
    /// This is the supported path on Win 10/11 for desktop apps; the
    /// PersonalizationCSP registry route is enterprise-only and requires admin.
    /// </summary>
    public class LockScreenRenderer
    {
        private readonly WallpaperRenderer m_oWall;

        public LockScreenRenderer(AlbumCoverMgr coverMgr)
        {
            // The lock screen render reuses the wallpaper renderer's draw routine
            // - same mosaic look, different output target.
            m_oWall = new WallpaperRenderer(coverMgr);
        }

        public string OutputPath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ScreenSaver");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "lockscreen.jpg");
            }
        }

        public async Task RefreshAsync()
        {
            // Lock screen uses the primary monitor's resolution. JPEG keeps the
            // file under 5 MB so SetImageFileAsync doesn't bounce it.
            string outPath = OutputPath;
            using (var bmp = m_oWall.RenderMosaic(Screen.PrimaryScreen.Bounds.Size, 256))
            {
                bmp.Save(outPath, ImageFormat.Jpeg);
            }

            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(outPath);
                await LockScreen.SetImageFileAsync(file);
            }
            catch
            {
                // Permission denied (group policy can block this), file too large,
                // or the WinRT API isn't available on this build - swallow and let
                // the user retry from the menu after addressing it.
            }
        }
    }
}
