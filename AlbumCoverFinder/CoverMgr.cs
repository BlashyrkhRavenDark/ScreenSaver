using System;
using System.Drawing;
using System.IO;
using System.Collections;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.Linq;
using TagLib;
using System.Windows.Forms;
using System.Text;
using System.Drawing.Imaging;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Json;
using System.Net;
using System.Windows.Input;
using TagLib.Mpeg;

namespace AlbumCoverFinder
{



    #region cloud function classes
    /// <summary>
    /// Classes used by the gcp cloud function to serialize into a json file the file names and the corresponding bucket download link
    /// </summary>
    public class AlbumAndCover
    {
        public string sFileName { get; set; }
        public string sFileUrl { get; set; }
    }

    public class AlbumCovers
    {
        public List<AlbumAndCover> oAlbumAndCovers { get; set; }
    }

    #endregion

    /// <summary>
    /// Cover Manager class. 
    /// 
    /// Initialisation:
    /// Recursively parses a directory to fetch cover pictures from mp3s.
    /// Saves a cover database of previously indexed dirtectories.
    /// Loads a previously saved cover database 
    /// 
    /// Once initialized: 
    /// Offers covers in sequence or at random. 
    /// Sends a placeholder when empty
    /// </summary>
    /// 
    [Serializable()]
    public class AlbumCoverMgr
    {
        #region Class Members
        private string m_sConfigFolder;
        private string m_sMusicPath;
        private Dictionary<string, Image> m_dPictures;
        private static int m_iMaxPicCount = 300; // max number of covers to load in memory. provides enough variety at managed memory cost
        private Random m_iRand = new Random();
        private Thread m_oThread;
        public delegate void NewCoverFound(Image p_oPicture); // Hey, I found this album cover while parsing files!
        public delegate void CoverMessage(string p_sMessage); // Hey, you should display this in your UI!
        public delegate void ProgressUpdate(int p_iCurrent, int p_iMax); // Hey, update your progress bar!
        public event NewCoverFound oNewCoverFoundEvent;
        public event CoverMessage oCoverMessageEvent;
        public event ProgressUpdate oProgressUpdateEvent;

        private static HttpClient oHttpClient;

        #endregion

        #region Constructors
        /// <summary>
        /// Constructor with custom folder to parse for albums
        /// </summary>
        /// <param name="p_sCustomMusicPath"></param>
        public AlbumCoverMgr(string p_sCustomMusicPath)
        {
            m_sMusicPath = p_sCustomMusicPath;
            AlbumCoverMgrInit();
        }

        /// <summary>
        /// Constructor with default folder to parse for albums
        /// </summary>
        public AlbumCoverMgr()
        {
            m_sMusicPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            AlbumCoverMgrInit();
        }

        /// <summary>
        /// Initialises common variables.
        /// </summary>
        public void AlbumCoverMgrInit()
        {
            m_sConfigFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\ScreenSaverPictures\\";
            oHttpClient = new HttpClient();
            if (!Directory.Exists(m_sConfigFolder))
                Directory.CreateDirectory(m_sConfigFolder);
        }
        #endregion

        #region Public Functions
        /// <summary>
        /// Starts the background process that parses pictures in default directory
        /// </summary>
        public void ParseDirectoryForPictures()
        {
            if (m_oThread == null || m_oThread.IsAlive != true)
            {
                m_oThread = new Thread(RunCoverMrg);
                m_oThread.IsBackground = true;
                m_oThread.Start();
            }
        }
        /// <summary>
        /// Starts the background process that parses pictures
        /// </summary>
        /// <param name="p_sDirectoryToParse">Specifies Directory to parse</param>
        public void ParseDirectoryForPictures(string p_sDirectoryToParse)
        {
            m_sMusicPath = p_sDirectoryToParse;
            ParseDirectoryForPictures();
        }

        /// returns a random picture from our list
        public Image GetRandomPicture()
        {
            if (m_dPictures.Count > 0)
            {
                List<Image> lImages = Enumerable.ToList(m_dPictures.Values);
                return lImages[m_iRand.Next(m_dPictures.Count - 1)];
            }
            else
                return new Bitmap(384, 360);
        }

        /// returns a random picture from our list, but resized to a specific height and width.
        public Image GetRandomPicture(int p_iWidth, int p_iHeight)
        {
            if (m_dPictures.Count > 0)
            {
                List<Image> lImages = Enumerable.ToList(m_dPictures.Values);
                return ResizeImage(lImages[m_iRand.Next(m_dPictures.Count - 1)], p_iWidth, p_iHeight);
            }
            else
                return new Bitmap(p_iHeight, p_iWidth);
        }

        public int GetAlbumTotal()
        {
            return (m_dPictures != null ? m_dPictures.Count : 0);
        }

        /// <summary>
        /// Deletes the file containing previously parsed album covers, and fires the AlbumFound event with 0 albums.
        /// </summary>
        public void DeleteAlbumBackup()
        {

        }

        #endregion

        #region Private Functions

        /// <summary>
        /// Resize the image to the specified width and height.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        private static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                using (var wrapMode = new System.Drawing.Imaging.ImageAttributes())
                {
                    wrapMode.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }



        /// <summary>
        /// Returns an array of string containing audio files from a directory
        /// p_sExtensionFilter must be an array of strings that will filter files according to their extention, like:
        /// string[] p_sExtensionFilter = { ".mp3", ".m4a", ".flac", ".ogg" }; (for example)
        /// </summary>
        static string[] GetFilesWithSuffix(string p_sdirectoryPath, string[] p_sExtensionFilter)
         {
            List<string> audioFiles = new List<string>();

            try
            {
                // Get all files in the current directory and its subdirectories
                string[] files = Directory.GetFiles(p_sdirectoryPath, "*.*", SearchOption.AllDirectories);
                audioFiles = files.Where(file => p_sExtensionFilter.Contains(Path.GetExtension(file).ToLower())).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
            }
            return audioFiles.ToArray();
        }

        /// <summary>
        /// Main Cover Manager thread.
        /// Is launched once initialisation parameters have been given to constructors
        /// </summary>
        private void RunCoverMrg()
        {
            try
            {
                string[] sAudioExtensions = { ".mp3", ".m4a", ".flac", ".ogg" };
                string[] sAudioFiles = GetFilesWithSuffix(m_sMusicPath, sAudioExtensions);
                int iCpt = 0;
                oCoverMessageEvent?.Invoke("Processing " + sAudioFiles.Length + " files, please wait");

                foreach (string sCurrentFile in sAudioFiles)
                {
                    AddInfoAndPictureFromFile(sCurrentFile);
                    oProgressUpdateEvent?.Invoke(++iCpt, sAudioFiles.Length);
                }
            }
            catch (UnauthorizedAccessException uAEx)
            {
                Console.WriteLine(uAEx.Message);
            }
            catch (PathTooLongException pathEx)
            {
                Console.WriteLine(pathEx.Message);
            }
            oCoverMessageEvent?.Invoke("Processing done.");
            return;
        }

        /// shuffles an array
        private string[] RandomizeWithFisherYates(string[] array)
        {
            int count = array.Length;

            while (count > 1)
            {
                int i = m_iRand.Next(count--);
                (array[i], array[count]) = (array[count], array[i]);
            }
            return array;
        }

        /// <summary>
        /// Loads a previously saved database of album covers as a serialized hashtable
        /// By default : 
        /// user folder \ScreenSaverPictures.bin
        /// </summary>
        public void LoadBackupData()
        {
            try
            {
                // Let's get a list of all .png files we saved in our configuration folder
                m_dPictures = new Dictionary<string, Image>();
                string[] sPngExtension = { ".png" };
                string[] sPngFiles = GetFilesWithSuffix(m_sConfigFolder, sPngExtension);
                int iCpt = 0;
                oCoverMessageEvent?.Invoke("Found " + sPngFiles.Length + " local png files.");

                // shuffle the list to vary the covers.
                sPngFiles = RandomizeWithFisherYates(sPngFiles);

                foreach (string sCurrentFile in sPngFiles)
                {
                    // files should be Artist - Album.png. We'll use that as a key and load them into the dict.
                    try
                    {
                        string sFilenameAsArtistAlbum = Path.GetFileNameWithoutExtension(sCurrentFile);
                        if (!m_dPictures.ContainsKey(sFilenameAsArtistAlbum))
                            m_dPictures.Add(sFilenameAsArtistAlbum, new Bitmap(sCurrentFile));
                        // Update the progress bar. If we found less local .PNGs than the max we want to load, we set it as the progress bar maximum.
                        oProgressUpdateEvent?.Invoke(++iCpt, sPngFiles.Length < m_iMaxPicCount ? sPngFiles.Length : m_iMaxPicCount);
                        // Check if we've reached the album count softcap. If we do, we send a last event and break the foreach.
                        // The event is used to update the UI of the Album Cover Finder configuration tool (we're done loading covers, here's one of them)
                        if (m_dPictures.Count >= m_iMaxPicCount)
                        {
                            oNewCoverFoundEvent?.Invoke(GetRandomPicture());
                            oCoverMessageEvent?.Invoke("Loaded " + m_dPictures.Count + " local png files.");
                            break;
                        }
                    }
                    catch (Exception ex) 
                    {
                        oCoverMessageEvent?.Invoke("Error parsing file [" + sCurrentFile + "] with error :" + ex.Message);
                    }
                }

            }
            catch (UnauthorizedAccessException uAEx)
            {
                Console.WriteLine(uAEx.Message);
            }
            catch (PathTooLongException pathEx)
            {
                Console.WriteLine(pathEx.Message);
            }

            return;

        }

        private bool AddInfoAndPictureFromFile(string p_sFile)
        {
            string sKey;
            Bitmap oImage;
            try
            {
                TagLib.File oFile = TagLib.File.Create(p_sFile);
                sKey = oFile.Tag.Performers[0] + " - " + oFile.Tag.Album;
                
                if (!m_dPictures.ContainsKey(sKey) && oFile.Tag.Pictures.Length > 0 && oFile.Tag.Pictures[0].Type != PictureType.NotAPicture)
                {
                    MemoryStream ms = new MemoryStream(oFile.Tag.Pictures[0].Data.Data);
                    oImage = ResizeImage(Image.FromStream(ms), 384, 360);
                    oImage.Save(m_sConfigFolder + sKey + ".png");
                    m_dPictures.Add(sKey, oImage);
                    /// Send a message to the windoes form that we found local albums
                    oCoverMessageEvent?.Invoke("Saving file : " + sKey + ".png");
                    oNewCoverFoundEvent?.Invoke(oImage);
                }
                return true;
            }
            catch (Exception ex)
            {
                oCoverMessageEvent?.Invoke("\r\nError while trying get album cover from file : " + p_sFile + " " + ex.Message);
                return false;
            }
        }


         async Task<AlbumCovers> GetAsync(HttpClient p_httpClient, string p_sFunctionUrl, string sAuthTokenFile, string p_sCoverDirectory)
        {
            AlbumCovers oAlbumCoversFromJson;
            string sLocalFilename;
            WebClient oWebClient = new WebClient();
            int iCpt = 0;

            try
            {
                oCoverMessageEvent?.Invoke("\r\nConnecting to the cloud to get album covers.");
                // Getting the Json file from the cloud function
                oAlbumCoversFromJson = await p_httpClient.GetFromJsonAsync<AlbumCovers>(p_sFunctionUrl);

                // Looping on the JSON file downloaded from the cloud to see what we got
                if (oAlbumCoversFromJson != null)
                {
                    if (oCoverMessageEvent != null && oAlbumCoversFromJson.oAlbumAndCovers != null)
                        oCoverMessageEvent("Albums found in the cloud: " + oAlbumCoversFromJson.oAlbumAndCovers.Count());
                    foreach (AlbumAndCover oAAC in oAlbumCoversFromJson.oAlbumAndCovers)
                    {
                        sLocalFilename = p_sCoverDirectory + "\\" + oAAC.sFileName;
                        if (!System.IO.File.Exists(sLocalFilename))
                        {
                            if (oCoverMessageEvent != null && oAlbumCoversFromJson.oAlbumAndCovers != null)
                                oCoverMessageEvent("Downloading file: " + oAAC.sFileName);
                            oWebClient.DownloadFile(oAAC.sFileUrl, sLocalFilename);
                        }
                        oProgressUpdateEvent?.Invoke(++iCpt, oAlbumCoversFromJson.oAlbumAndCovers.Count());
                    }
                    oCoverMessageEvent?.Invoke("Done processing Cloud Albums.");
                }
            }
            catch (Exception ex)
            {
                oCoverMessageEvent?.Invoke("Error while trying to get cloud albums: " + ex.Message);
                return null;
            }
            return null;

        }
        public string GetCloudCovers(string sFunctionUrl, string sAuthTokenFile)
        {
            GetAsync(oHttpClient, sFunctionUrl, sAuthTokenFile, m_sConfigFolder);
            return "";
        }

        #endregion
    }
}
