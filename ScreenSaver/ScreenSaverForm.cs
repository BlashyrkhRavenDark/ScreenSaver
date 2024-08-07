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
    /// <summary>
    /// This class is straightforward: it derives from a Windows form, and contains a grid of PictureBox to display the covers.
    /// Two constructors depending on whether it is launched in screensaver mode, or in settings mode within the configuration window used for all Windows screensavers.
    /// Once we have created our grid of PictureBoxes with a random picture in each PictureBox, we trigger an event that replaces a random PictureBox by a random cover.
    /// </summary>
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
        private int m_iCoverHeight;
        private int m_iCoverWidth;
        private int m_iXCovers;
        private int m_iYCovers;
        private PictureBox[,] m_aPictureBoxes;
        private AlbumCoverMgr m_oCoverMgr;

        #region Constructors

        /// <summary>
        /// We are creating the screensaver form within a parent form, fitting 5x3 covers.
        /// We compute the size of the covers to fit 5x3 in the screen.
        /// </summary>
        /// <param name="Bounds">The bounds of the parent form</param>
        /// <param name="pCoverMgr">A reference to the Cover Manager which manages the picture files</param>
        public ScreenSaverForm(Rectangle Bounds, AlbumCoverMgr pCoverMgr)
        {
            InitializeComponent();
            m_oCoverMgr = pCoverMgr;
            this.Bounds = Bounds;
            m_iXCovers = 5; // 5 covers horizontally
            m_iYCovers = 3; // 3 covers vertically
            m_iCoverWidth = this.Width / m_iXCovers;
            m_iCoverHeight = this.Height / m_iYCovers;
            m_aPictureBoxes = new PictureBox[m_iXCovers, m_iYCovers];

        }

        /// <summary>
        /// This is the small window inside the screensaver parameter window.
        /// A 3*3 picture table will be enough.
        /// </summary>
        /// <param name="PreviewWndHandle"></param>
        /// <param name="pCoverMgr"></param>
        public ScreenSaverForm(IntPtr PreviewWndHandle, AlbumCoverMgr pCoverMgr)
        {
            InitializeComponent();
            m_oCoverMgr = pCoverMgr;
            m_iXCovers = 3;
            m_iYCovers = 3;
            m_iCoverWidth = 50;
            m_iCoverHeight = 50;
            m_aPictureBoxes = new PictureBox[m_iXCovers, m_iYCovers];

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
                    m_aPictureBoxes[iCptX, iCptY].Image = m_oCoverMgr.GetRandomPicture(m_iCoverWidth, m_iCoverHeight);
                    m_aPictureBoxes[iCptX, iCptY].Height = m_iCoverHeight;
                    m_aPictureBoxes[iCptX, iCptY].Width = m_iCoverWidth;
                    m_aPictureBoxes[iCptX, iCptY].Left = iCptX * m_iCoverWidth;
                    m_aPictureBoxes[iCptX, iCptY].Top = iCptY * m_iCoverHeight;
                    this.Controls.Add(m_aPictureBoxes[iCptX, iCptY]);
                }
        }

        private void ChangePicture()
        {
            m_aPictureBoxes[m_iRand.Next(0, m_iXCovers), m_iRand.Next(0, m_iYCovers)].Image = m_oCoverMgr.GetRandomPicture(m_iCoverWidth, m_iCoverHeight);
        }

        /// <summary>
        /// Adds a cover to a random screen position (not aligned to the picture grid)
        /// </summary>
        private void AddPictureToScreen()
        {
            System.Windows.Forms.PictureBox pictureBox = new PictureBox();
            pictureBox.Image = m_oCoverMgr.GetRandomPicture(m_iCoverWidth, m_iCoverHeight);
            pictureBox.Height = m_iCoverHeight;
            pictureBox.Width = m_iCoverWidth;
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
