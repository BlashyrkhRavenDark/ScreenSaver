using System;
using System.Drawing;
using System.IO;
using System.Collections;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.Linq;
using TagLib;


namespace AlbumCoverFinder
{
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
        private string m_sConfigFile;
        private int m_iLastBackupCount = 0;
        private string m_sMusicPath;
        private Dictionary<string, Image> m_dPictures;
        private ArrayList m_aReadFiles;
        private static int m_iMaxPicCount = 5000;
        private Random m_iRand = new Random();
        private Thread m_oThread;
        public delegate void AlbumFound(int p_iAlbumFounds, Image p_oPicture);
        public event AlbumFound oAlbumFoundEvent;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor with custom folder to parse for albums
        /// </summary>
        /// <param name="p_sCustomMusicPath"></param>
        public AlbumCoverMgr(string p_sCustomMusicPath)
        {
            m_sMusicPath = p_sCustomMusicPath;
            m_sConfigFile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\ScreenSaverPictures.bin";
            LoadBackupData();
        }

        /// <summary>
        /// Constructor with default folder to parse for albums
        /// </summary>
        public AlbumCoverMgr()
        {
            m_sMusicPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            m_sConfigFile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\ScreenSaverPictures.bin";
            LoadBackupData();
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


        public Image GetRandomPicture()
        {
            if (m_dPictures.Count > 0)
            {
                List<Image> lImages = Enumerable.ToList(m_dPictures.Values);
                return lImages[m_iRand.Next(m_dPictures.Count - 1)];
            }
            else
                return new Bitmap(120, 120);
            //Bitmap(120, 120, System.Drawing.Imaging.PixelFormat.DontCare);
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
            if (m_dPictures != null)
                m_dPictures.Clear();
            else
                m_dPictures = new Dictionary<string, Image>();
            try
            {
                if (System.IO.File.Exists(m_sConfigFile))
                    System.IO.File.Delete(m_sConfigFile);
            }
            catch
            {
                return;
            }
            oAlbumFoundEvent(0, GetRandomPicture());
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
        /// Main Cover Manager thread.
        /// Is launched once initialisation parameters have been given to constructors
        /// </summary>
        private void RunCoverMrg()
        {

            m_aReadFiles = new ArrayList();

            try
            {
                var files = Directory.EnumerateFiles(m_sMusicPath, "*.mp3,*.wma,*.mp4,*.m4a", SearchOption.AllDirectories);
                foreach (string sCurrentFile in files)
                {
                    if (!m_aReadFiles.Contains(sCurrentFile))
                        AddInfoAndPictureFromFile(sCurrentFile);

                    // Check if we've reached the album count softcap. If we do, we send a last event and break the foreach.
                    if (m_dPictures.Count > m_iMaxPicCount)
                    {
                        if (oAlbumFoundEvent != null)
                            oAlbumFoundEvent(m_dPictures.Count, GetRandomPicture());
                        break;
                    }
                }
                SaveBackupData(m_dPictures.Count);
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



        /// <summary>
        /// Loads a previously saved database of album covers as a serialized hashtable
        /// By default : 
        /// user folder \ScreenSaverPictures.bin
        /// </summary>
        private void LoadBackupData()
        {
            Stream openFileStream = null;
            try
            {
                if (System.IO.File.Exists(m_sConfigFile))
                {
                    Console.WriteLine("Reading saved file");
                    openFileStream = System.IO.File.OpenRead(m_sConfigFile);
                    BinaryFormatter deserializer = new BinaryFormatter();
                    m_dPictures = (Dictionary<string, Image>)deserializer.Deserialize(openFileStream);
                    openFileStream.Close();
                    if (oAlbumFoundEvent != null)
                        oAlbumFoundEvent(m_dPictures.Count, GetRandomPicture());

                }
                else
                    m_dPictures = new Dictionary<string, Image>();
            }
            catch
            {
                m_dPictures = new Dictionary<string, Image>();
                if (openFileStream != null)
                    openFileStream.Close();
                System.IO.File.Delete(m_sConfigFile);
            }

        }

        private void SaveBackupData(int p_iLastCount)
        {
            if (p_iLastCount == m_iLastBackupCount)
                return;
            try
            {
                Stream SaveFileStream = System.IO.File.Create(m_sConfigFile);
                BinaryFormatter serializer = new BinaryFormatter();
                serializer.Serialize(SaveFileStream, m_dPictures);
                SaveFileStream.Close();
                m_iLastBackupCount = p_iLastCount;
            }
            catch { }
        }

        private bool AddInfoAndPictureFromFile(string p_sFile)
        {
            string sKey;
            Bitmap oImage;
            try
            {
                TagLib.File oFile = TagLib.File.Create(p_sFile);
                sKey = oFile.Tag.Performers + oFile.Tag.Album;

                if (!m_dPictures.ContainsKey(sKey) && oFile.Tag.Pictures.Length > 0 && oFile.Tag.Pictures[0].Type != PictureType.NotAPicture)
                {
                    MemoryStream ms = new MemoryStream(oFile.Tag.Pictures[0].Data.Data);
                    oImage = ResizeImage(Image.FromStream(ms), 128, 120);
                    m_dPictures.Add(sKey, oImage);
                    m_aReadFiles.Add(p_sFile);
                    if (oAlbumFoundEvent != null)
                        oAlbumFoundEvent(m_dPictures.Count, oImage);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }
}
