using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks; 
using System.Windows.Forms;
using System.IO;
using System.Threading;
using Google.Apis.Auth.OAuth2;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.Runtime.Remoting.Contexts;
using static AlbumCoverFinder.AlbumCoverMgr;

namespace AlbumCoverFinder
{
    public partial class AlbumCoverFinderForm : Form
    {
        private AlbumCoverMgr oCoverMgr;

        public AlbumCoverFinderForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Callback used to display the newly found cover album in our ImageBox
        /// (Winforms stuff doesn't like to be updated by other threads)
        /// </summary>
        /// <param name="p_oPicture"></param>
        private void NewCoverFoundCallback(Image p_oPicture)
        {
            if (tDisplayUpdate.InvokeRequired)
            {
                var d = new NewCoverFound(NewCoverFoundCallback);
                tDisplayUpdate.Invoke(d, new object[] {p_oPicture });
            }
            else
            {
                if (p_oPicture != null)
                    pictureBox1.Image = oCoverMgr.GetRandomPicture(320,320);
            }
        }


        private void CoverMessageCallback(string p_sMessage)
        {
            if (tDisplayUpdate.InvokeRequired)
            {
                var d = new CoverMessage(CoverMessageCallback);
                tDisplayUpdate.Invoke(d, new object[] { p_sMessage });
            }
            else
            {
                tDisplayUpdate.Text += "\r\n" + p_sMessage;
            }
        }


        private void ProgressUpdateCallback(int p_iStart, int p_iEnd)
        {
            if (pbProgressBar.InvokeRequired)
            {
                var d = new ProgressUpdate(ProgressUpdateCallback);
                pbProgressBar.Invoke(d, new object[] { p_iStart, p_iEnd });
            }
            else
            {
                pbProgressBar.Minimum = 0;
                pbProgressBar.Value = p_iStart;
                pbProgressBar.Maximum = p_iEnd;
            }
        }

        /// <summary>
        /// On load : Create a new AlbumCoverMgr, register the callback, update Forms with info from past parsed albums (if any)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_Load(object sender, EventArgs e)
        {
            oCoverMgr = new AlbumCoverMgr();
            oCoverMgr.oNewCoverFoundEvent += NewCoverFoundCallback;
            oCoverMgr.oCoverMessageEvent += CoverMessageCallback;
            oCoverMgr.oProgressUpdateEvent += ProgressUpdateCallback;
            tDisplayUpdate.Text += "This Album Cover Finder will look for pictures in your audio files and store them in your user directory as png files.\r\n\r\nPick a folder below and start scanning it.\r\n";
            // loads locally saved .png into memory
            oCoverMgr.LoadBackupData();
        }


        private void bParseFolder_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1 = new FolderBrowserDialog();
            folderBrowserDialog1.Description = "Select folder containing audio files and folders.";
            DialogResult oResult = folderBrowserDialog1.ShowDialog();
            if (oResult == DialogResult.OK)
                oCoverMgr.ParseDirectoryForPictures(folderBrowserDialog1.SelectedPath);
        }

        private void bDeleteBackupFIle_Click(object sender, EventArgs e)
        {
            CloudDialog oCD = new CloudDialog();
            oCD.ShowDialog();
            if (oCD.bOK == true)
                oCoverMgr.GetCloudCovers(oCD.sUrl, oCD.sToken);
        }

    }
}
