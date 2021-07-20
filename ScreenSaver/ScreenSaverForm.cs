/*

 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using AlbumCoverFinder;

namespace ScreenSaver
{
    public partial class ScreenSaverForm : Form
    {
        #region Win32 API functions

        [DllImport("user32.dll")]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hWnd, out Rectangle lpRect);

        #endregion


        private Point mouseLocation;
        private bool previewMode = false;
        private Random m_iRand = new Random();
        // create iX and iY for picturebox size, matching screen bounds / 120, to get correct number of 120 covers to display.
        private int m_iXCovers = 16;
        private int m_iYCovers = 9;
        private PictureBox[,] m_aPictureBoxes;
        private AlbumCoverMgr m_oCoverMgr;

        #region Constructors

        /// <summary>
        /// We are creating the screensaver form without any reference,
        /// so we will assume a generic 16 * 9 1080p screen. 
        /// We never laumch this, though.
        /// </summary>
        /// <param name="pCoverMgr"></param>
        public ScreenSaverForm(AlbumCoverMgr pCoverMgr)
        {
            m_oCoverMgr = pCoverMgr;
            m_aPictureBoxes = new PictureBox[16, 9];
            InitializeComponent();
        }
        /// <summary>
        ///  We are creating the screensaver form within a parent form,
        /// so we will compute how many 120*120 images fit in.
        /// </summary>
        /// <param name="Bounds">The bounds of the parent form</param>
        /// <param name="pCoverMgr">A reference to the Cover Manager which manages the picture files</param>
        public ScreenSaverForm(Rectangle Bounds, AlbumCoverMgr pCoverMgr)
        {
            m_oCoverMgr = pCoverMgr;
            m_iXCovers = Bounds.Width / 128;
            m_iYCovers = Bounds.Height / 120;
            m_aPictureBoxes = new PictureBox[m_iXCovers, m_iYCovers];
            InitializeComponent();
            this.Bounds = Bounds;
        }

        /// <summary>
        /// This is the small window inside the screensaver parameter window
        /// A 2*2 picture table will be enough
        /// </summary>
        /// <param name="PreviewWndHandle"></param>
        /// <param name="pCoverMgr"></param>
        public ScreenSaverForm(IntPtr PreviewWndHandle, AlbumCoverMgr pCoverMgr)
        {
            m_oCoverMgr = pCoverMgr;
            m_iXCovers = 2;
            m_iYCovers = 2;
            m_aPictureBoxes = new PictureBox[m_iXCovers, m_iYCovers];
            InitializeComponent();

            // Set the preview window as the parent of this window
            SetParent(this.Handle, PreviewWndHandle);
            // Make this a child window so it will close when the parent dialog closes
            SetWindowLong(this.Handle, -16, new IntPtr(GetWindowLong(this.Handle, -16) | 0x40000000));
            // Place our window inside the parent
            Rectangle ParentRect;
            GetClientRect(PreviewWndHandle, out ParentRect);
            Size = ParentRect.Size;
            Location = new Point(0, 0);
            previewMode = true;
        }
        #endregion

        /// <summary>
        /// Our form is being loaded on screen. Let's set everything up.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScreenSaverForm_Load(object sender, EventArgs e)
        {            
            LoadSettings();
            Cursor.Hide();            
            TopMost = true;
            InitiatePictureBoxes();
            moveTimer.Interval = 1000;
            moveTimer.Tick += new EventHandler(moveTimer_Tick);
            moveTimer.Start();
        }

        private void moveTimer_Tick(object sender, System.EventArgs e)
        {
            ChangePicture();
        } 

        private void InitiatePictureBoxes()
        {
            int iCptX, iCptY = 0;

            for (iCptX = 0; iCptX < m_iXCovers; iCptX++)
                for (iCptY = 0; iCptY < m_iYCovers; iCptY++)
                {
                    m_aPictureBoxes[iCptX, iCptY] = new PictureBox();
                    m_aPictureBoxes[iCptX, iCptY].Image = m_oCoverMgr.GetRandomPicture();
                    m_aPictureBoxes[iCptX, iCptY].Height = 120;
                    m_aPictureBoxes[iCptX, iCptY].Width = 128;
                    m_aPictureBoxes[iCptX, iCptY].Left = iCptX * 128;
                    m_aPictureBoxes[iCptX, iCptY].Top = iCptY * 120;
                    this.Controls.Add(m_aPictureBoxes[iCptX, iCptY]);
                }
        }

        private void ChangePicture()
        {
            m_aPictureBoxes[m_iRand.Next(0, m_iXCovers), m_iRand.Next(0, m_iYCovers)].Image = m_oCoverMgr.GetRandomPicture();
        }

        /// <summary>
        /// Adds a cover to a random screen position (not aligned to the picture grid)
        /// </summary>
        private void AddPictureToScreen()
        {
            System.Windows.Forms.PictureBox pictureBox = new PictureBox();
            pictureBox.Image = m_oCoverMgr.GetRandomPicture();
            pictureBox.Height = 120;
            pictureBox.Width = 128;
            pictureBox.Left = m_iRand.Next(Math.Max(1, Bounds.Width - pictureBox.Width));
            pictureBox.Top = m_iRand.Next(Math.Max(1, Bounds.Height - pictureBox.Height));
            this.Controls.Add(pictureBox);

        }

        private void LoadSettings()
        {
            // Use the string from the Registry if it exists
            RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Demo_ScreenSaver");
        }

        private void ScreenSaverForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (!previewMode)
            {
                if (!mouseLocation.IsEmpty)
                {
                    // Terminate if mouse is moved a significant distance
                    if (Math.Abs(mouseLocation.X - e.X) > 5 ||
                        Math.Abs(mouseLocation.Y - e.Y) > 5)
                        Application.Exit();
                }

                // Update current mouse location
                mouseLocation = e.Location;
            }
        }

        private void ScreenSaverForm_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!previewMode)
                Application.Exit();
        }

        private void ScreenSaverForm_MouseClick(object sender, MouseEventArgs e)
        {
            if (!previewMode)
                Application.Exit();
        }
    }
}
