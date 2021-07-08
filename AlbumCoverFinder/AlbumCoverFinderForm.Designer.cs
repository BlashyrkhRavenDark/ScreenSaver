
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
            this.bChangeFolder = new System.Windows.Forms.Button();
            this.tFolderToParse = new System.Windows.Forms.TextBox();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.bParseFolder = new System.Windows.Forms.Button();
            this.tDisplayUpdate = new System.Windows.Forms.TextBox();
            this.bDeleteBackupFIle = new System.Windows.Forms.Button();
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // bChangeFolder
            // 
            this.bChangeFolder.Location = new System.Drawing.Point(355, 138);
            this.bChangeFolder.Name = "bChangeFolder";
            this.bChangeFolder.Size = new System.Drawing.Size(41, 36);
            this.bChangeFolder.TabIndex = 0;
            this.bChangeFolder.Text = "...";
            this.bChangeFolder.UseVisualStyleBackColor = true;
            this.bChangeFolder.Click += new System.EventHandler(this.bChangeFolder_Click);
            // 
            // tFolderToParse
            // 
            this.tFolderToParse.Location = new System.Drawing.Point(12, 147);
            this.tFolderToParse.Name = "tFolderToParse";
            this.tFolderToParse.Size = new System.Drawing.Size(337, 20);
            this.tFolderToParse.TabIndex = 1;
            this.tFolderToParse.Text = "N:\\GOOGLEDRIVE\\Musique\\Music\\Prince";
            // 
            // pictureBox1
            // 
            this.pictureBox1.Location = new System.Drawing.Point(276, 12);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(120, 120);
            this.pictureBox1.TabIndex = 2;
            this.pictureBox1.TabStop = false;
            // 
            // bParseFolder
            // 
            this.bParseFolder.Location = new System.Drawing.Point(94, 180);
            this.bParseFolder.Name = "bParseFolder";
            this.bParseFolder.Size = new System.Drawing.Size(302, 44);
            this.bParseFolder.TabIndex = 4;
            this.bParseFolder.Text = "Parse Folder for albums";
            this.bParseFolder.UseVisualStyleBackColor = true;
            this.bParseFolder.Click += new System.EventHandler(this.bParseFolder_Click);
            // 
            // tDisplayUpdate
            // 
            this.tDisplayUpdate.Location = new System.Drawing.Point(12, 12);
            this.tDisplayUpdate.Multiline = true;
            this.tDisplayUpdate.Name = "tDisplayUpdate";
            this.tDisplayUpdate.Size = new System.Drawing.Size(258, 120);
            this.tDisplayUpdate.TabIndex = 5;
            // 
            // bDeleteBackupFIle
            // 
            this.bDeleteBackupFIle.Location = new System.Drawing.Point(13, 180);
            this.bDeleteBackupFIle.Name = "bDeleteBackupFIle";
            this.bDeleteBackupFIle.Size = new System.Drawing.Size(75, 44);
            this.bDeleteBackupFIle.TabIndex = 6;
            this.bDeleteBackupFIle.Text = "Delete Backup File";
            this.bDeleteBackupFIle.UseVisualStyleBackColor = true;
            this.bDeleteBackupFIle.Click += new System.EventHandler(this.bDeleteBackupFIle_Click);
            // 
            // folderBrowserDialog1
            // 
            this.folderBrowserDialog1.RootFolder = System.Environment.SpecialFolder.MyMusic;
            // 
            // AlbumCoverFinderForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(406, 236);
            this.Controls.Add(this.bDeleteBackupFIle);
            this.Controls.Add(this.tDisplayUpdate);
            this.Controls.Add(this.bParseFolder);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.tFolderToParse);
            this.Controls.Add(this.bChangeFolder);
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(422, 275);
            this.MinimumSize = new System.Drawing.Size(422, 275);
            this.Name = "AlbumCoverFinderForm";
            this.ShowIcon = false;
            this.Text = "Album Cover Finder";
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button bChangeFolder;
        private System.Windows.Forms.TextBox tFolderToParse;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Button bParseFolder;
        private System.Windows.Forms.TextBox tDisplayUpdate;
        private System.Windows.Forms.Button bDeleteBackupFIle;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
    }
}

