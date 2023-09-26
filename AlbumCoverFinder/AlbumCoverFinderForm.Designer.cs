
namespace AlbumCoverFinder
{
    partial class AlbumCoverFinderForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.bParseFolder = new System.Windows.Forms.Button();
            this.tDisplayUpdate = new System.Windows.Forms.TextBox();
            this.bDeleteBackupFIle = new System.Windows.Forms.Button();
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            this.tCloudFunctionUrl = new System.Windows.Forms.TextBox();
            this.tAuthTokenFile = new System.Windows.Forms.TextBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.pbProgressBar = new System.Windows.Forms.ProgressBar();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();
            // 
            // pictureBox1
            // 
            this.pictureBox1.Location = new System.Drawing.Point(675, 15);
            this.pictureBox1.Margin = new System.Windows.Forms.Padding(4);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(320, 320);
            this.pictureBox1.TabIndex = 2;
            this.pictureBox1.TabStop = false;
            // 
            // bParseFolder
            // 
            this.bParseFolder.Location = new System.Drawing.Point(10, 22);
            this.bParseFolder.Margin = new System.Windows.Forms.Padding(4);
            this.bParseFolder.Name = "bParseFolder";
            this.bParseFolder.Size = new System.Drawing.Size(303, 32);
            this.bParseFolder.TabIndex = 4;
            this.bParseFolder.Text = "Parse Folder for albums";
            this.bParseFolder.UseVisualStyleBackColor = true;
            this.bParseFolder.Click += new System.EventHandler(this.bParseFolder_Click);
            // 
            // tDisplayUpdate
            // 
            this.tDisplayUpdate.Location = new System.Drawing.Point(16, 15);
            this.tDisplayUpdate.Margin = new System.Windows.Forms.Padding(4);
            this.tDisplayUpdate.Multiline = true;
            this.tDisplayUpdate.Name = "tDisplayUpdate";
            this.tDisplayUpdate.ReadOnly = true;
            this.tDisplayUpdate.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.tDisplayUpdate.Size = new System.Drawing.Size(642, 390);
            this.tDisplayUpdate.TabIndex = 5;
            // 
            // bDeleteBackupFIle
            // 
            this.bDeleteBackupFIle.Location = new System.Drawing.Point(533, 19);
            this.bDeleteBackupFIle.Margin = new System.Windows.Forms.Padding(4);
            this.bDeleteBackupFIle.Name = "bDeleteBackupFIle";
            this.bDeleteBackupFIle.Size = new System.Drawing.Size(100, 54);
            this.bDeleteBackupFIle.TabIndex = 6;
            this.bDeleteBackupFIle.Text = "Get Cloud Backups";
            this.bDeleteBackupFIle.UseVisualStyleBackColor = true;
            this.bDeleteBackupFIle.Click += new System.EventHandler(this.bDeleteBackupFIle_Click);
            // 
            // folderBrowserDialog1
            // 
            this.folderBrowserDialog1.RootFolder = System.Environment.SpecialFolder.MyMusic;
            // 
            // tCloudFunctionUrl
            // 
            this.tCloudFunctionUrl.Location = new System.Drawing.Point(14, 22);
            this.tCloudFunctionUrl.Margin = new System.Windows.Forms.Padding(4);
            this.tCloudFunctionUrl.Name = "tCloudFunctionUrl";
            this.tCloudFunctionUrl.Size = new System.Drawing.Size(509, 22);
            this.tCloudFunctionUrl.TabIndex = 7;
            // 
            // tAuthTokenFile
            // 
            this.tAuthTokenFile.Location = new System.Drawing.Point(14, 51);
            this.tAuthTokenFile.Margin = new System.Windows.Forms.Padding(4);
            this.tAuthTokenFile.Name = "tAuthTokenFile";
            this.tAuthTokenFile.Size = new System.Drawing.Size(447, 22);
            this.tAuthTokenFile.TabIndex = 8;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.bParseFolder);
            this.groupBox1.Location = new System.Drawing.Point(675, 351);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(320, 63);
            this.groupBox1.TabIndex = 10;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Parse local audio files to find album covers";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.tAuthTokenFile);
            this.groupBox2.Controls.Add(this.tCloudFunctionUrl);
            this.groupBox2.Controls.Add(this.bDeleteBackupFIle);
            this.groupBox2.Location = new System.Drawing.Point(12, 411);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(646, 88);
            this.groupBox2.TabIndex = 11;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Download album covers from the cloud through a GCP Cloud Function";
            // 
            // pbProgressBar
            // 
            this.pbProgressBar.Location = new System.Drawing.Point(13, 31);
            this.pbProgressBar.Name = "pbProgressBar";
            this.pbProgressBar.Size = new System.Drawing.Size(302, 27);
            this.pbProgressBar.TabIndex = 12;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.pbProgressBar);
            this.groupBox3.Location = new System.Drawing.Point(673, 426);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(322, 73);
            this.groupBox3.TabIndex = 13;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Progress";
            // 
            // AlbumCoverFinderForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoScroll = true;
            this.ClientSize = new System.Drawing.Size(1004, 509);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.tDisplayUpdate);
            this.Controls.Add(this.pictureBox1);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.MinimumSize = new System.Drawing.Size(557, 328);
            this.Name = "AlbumCoverFinderForm";
            this.ShowIcon = false;
            this.Text = "Album Cover Finder";
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Button bParseFolder;
        private System.Windows.Forms.TextBox tDisplayUpdate;
        private System.Windows.Forms.Button bDeleteBackupFIle;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
        private System.Windows.Forms.TextBox tCloudFunctionUrl;
        private System.Windows.Forms.TextBox tAuthTokenFile;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.ProgressBar pbProgressBar;
        private System.Windows.Forms.GroupBox groupBox3;
    }
}

