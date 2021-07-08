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

namespace AlbumCoverFinder
{
    public partial class AlbumCoverFinderForm : Form
    {
        private AlbumCoverMgr oCoverMgr;
        public delegate void AlbumFound(int p_iAlbumFounds, Image p_oPicture);

        public AlbumCoverFinderForm()
        {
            InitializeComponent();

        }

        /// <summary>
        /// Callback used to let the "AlbumFound" event run by the worker thread to update the textbox
        /// (Winforms stuff doesn't like to be updated by other threads)
        /// </summary>
        /// <param name="p_iAlbumFounds"></param>
        /// <param name="p_oPicture"></param>
        private void AlbumFoundCallback(int p_iAlbumFounds, Image p_oPicture)
        {
            if (tDisplayUpdate.InvokeRequired)
            {
                var d = new AlbumFound(AlbumFoundCallback);
                tDisplayUpdate.Invoke(d, new object[] { p_iAlbumFounds, p_oPicture });
            }
            else
            {
                tDisplayUpdate.Text = "Total Albums Founds: " + p_iAlbumFounds.ToString();
                if (p_oPicture != null)
                    pictureBox1.Image = p_oPicture;
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
            oCoverMgr.oAlbumFoundEvent += AlbumFoundCallback;
            tDisplayUpdate.Text = "This Album Cover Parser will look for pictures in your MP3s and store them in a small database used by the screensaver.\r\nPick a folder below and start scanning it to store albbum covers.";
            if (oCoverMgr.GetAlbumTotal() > 0)
                tDisplayUpdate.Text = "Number of Album Covers stored in database:" + oCoverMgr.GetAlbumTotal();
            pictureBox1.Image = oCoverMgr.GetRandomPicture();

        }



        private void bChangeFolder_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1 = new FolderBrowserDialog();
            DialogResult oResult = folderBrowserDialog1.ShowDialog();
            if (oResult == DialogResult.OK)
                tFolderToParse.Text = folderBrowserDialog1.SelectedPath;
        }

        private void bParseFolder_Click(object sender, EventArgs e)
        {
            if (Directory.Exists(tFolderToParse.Text))
                oCoverMgr.ParseDirectoryForPictures(tFolderToParse.Text);
            else
                tDisplayUpdate.Text = "Can't find that directory";
        }

        private void bDeleteBackupFIle_Click(object sender, EventArgs e)
        {
            oCoverMgr.DeleteAlbumBackup();
        }
    }
}
