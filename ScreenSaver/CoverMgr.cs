﻿using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Collections;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Linq;
using System.Text;
using Id3Lib;
using Mp3Lib;

namespace ScreenSaver
{
    /// <summary>
    /// Cover Manager class. 
    /// Offers covers in sequence or at random. 
    /// Sends a placeholder when empty
    /// Recursively parses a directory to fetch cover pictures from mp3s.
    /// Saves a cover database of previously indexed dirtectories.
    /// Loads a previously saved cover database 
    /// </summary>
    /// 
    [Serializable()]
    public class CoverMgr
    {
        private string m_sConfigFile;
        private int m_iLastBackupCount = 0;
        private string m_sMusicPath;
        private Dictionary<string, Image> m_dPictures;
        private ArrayList m_aReadFiles;
        private static int m_iMaxPicCount = 50;
        private Random m_iRand = new Random();
        private Thread m_oThread;

        public CoverMgr(string p_sCustomMusicPath)
        {
            m_sMusicPath = p_sCustomMusicPath;
            m_sConfigFile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\ScreenSaverPictures.bin";
            //RunCoverMrg();
            m_oThread = new Thread(RunCoverMrg);
            m_oThread.IsBackground = true;
            m_oThread.Start();
        }

        public CoverMgr()
        {
            m_sMusicPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            m_sConfigFile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\ScreenSaverPictures.bin";
            //RunCoverMrg();
            m_oThread = new Thread(RunCoverMrg);
            m_oThread.IsBackground = true;
            m_oThread.Start();
        }

        void RunCoverMrg()
        {
            LoadBackupData();
            m_aReadFiles = new ArrayList();
            
            try
            {
                var files = Directory.EnumerateFiles(m_sMusicPath, "*.mp3", SearchOption.AllDirectories);
                foreach (string sCurrentFile in files)
                {
                    if (!m_aReadFiles.Contains(sCurrentFile))
                        AddInfoAndPictureFromFile(sCurrentFile);

                    if (m_dPictures.Count % 25 == 0 && m_dPictures.Count > 0 && m_dPictures.Count <= m_iMaxPicCount )
                        SaveBackupData(m_dPictures.Count);
                    if (m_dPictures.Count > m_iMaxPicCount)
                        break;
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

            return ;
        }

        void LoadBackupData()
        {
            Stream openFileStream = null;
            try
            {
                if (File.Exists(m_sConfigFile))
                {
                    Console.WriteLine("Reading saved file");
                    openFileStream = File.OpenRead(m_sConfigFile);
                    BinaryFormatter deserializer = new BinaryFormatter();
                    m_dPictures = (Dictionary<string, Image>)deserializer.Deserialize(openFileStream);
                    openFileStream.Close();
                }
                else
                    m_dPictures = new Dictionary<string, Image>();
            }
            catch
            {
                m_dPictures = new Dictionary<string, Image>();
                if (openFileStream != null)
                    openFileStream.Close();
                File.Delete(m_sConfigFile);
            }
                
        }

        void SaveBackupData(int p_iLastCount)
        {
            if (p_iLastCount == m_iLastBackupCount)
                return;
            try
            {
                Stream SaveFileStream = File.Create(m_sConfigFile);
                BinaryFormatter serializer = new BinaryFormatter();
                serializer.Serialize(SaveFileStream, m_dPictures);
                SaveFileStream.Close();
                m_iLastBackupCount = p_iLastCount;
            }
            catch { }
            }

        bool AddInfoAndPictureFromFile(string p_sFile)
        {
            string sKey;
            Bitmap oImage;
            try
            {
                Mp3File oFile = new Mp3File(p_sFile);
                sKey = oFile.TagHandler.Artist + oFile.TagHandler.Album;

                if (!m_dPictures.ContainsKey(sKey) && oFile.TagHandler.Picture != null)
                {
                    oImage = ResizeImage(oFile.TagHandler.Picture, 120, 120);
                    m_dPictures.Add(sKey, oImage);
                    m_aReadFiles.Add(p_sFile);
                }
                return true;
            }
            catch
            {
                return false;
            }
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


        /// <summary>
        /// Resize the image to the specified width and height.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new System.Drawing.Imaging.ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }


    }
}
