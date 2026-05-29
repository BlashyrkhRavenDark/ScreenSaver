namespace AlbumCoverFinder
{
    partial class CloudDialog
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
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.tAuthTokenFile = new System.Windows.Forms.TextBox();
            this.tCloudFunctionUrl = new System.Windows.Forms.TextBox();
            this.bDeleteBackupFIle = new System.Windows.Forms.Button();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.tAuthTokenFile);
            this.groupBox2.Controls.Add(this.tCloudFunctionUrl);
            this.groupBox2.Controls.Add(this.bDeleteBackupFIle);
            this.groupBox2.Location = new System.Drawing.Point(12, 12);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(640, 92);
            this.groupBox2.TabIndex = 12;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Download album covers from the cloud through a GCP Cloud Function";
            // 
            // tAuthTokenFile
            // 
            this.tAuthTokenFile.Location = new System.Drawing.Point(14, 51);
            this.tAuthTokenFile.Margin = new System.Windows.Forms.Padding(4);
            this.tAuthTokenFile.Name = "tAuthTokenFile";
            this.tAuthTokenFile.Size = new System.Drawing.Size(509, 22);
            this.tAuthTokenFile.TabIndex = 8;
            this.tAuthTokenFile.Text = "Paste your Auth token here";
            // 
            // tCloudFunctionUrl
            // 
            this.tCloudFunctionUrl.Location = new System.Drawing.Point(14, 22);
            this.tCloudFunctionUrl.Margin = new System.Windows.Forms.Padding(4);
            this.tCloudFunctionUrl.Name = "tCloudFunctionUrl";
            this.tCloudFunctionUrl.Size = new System.Drawing.Size(509, 22);
            this.tCloudFunctionUrl.TabIndex = 7;
            this.tCloudFunctionUrl.Text = "Enter the URL of your GCP cloud function here";
            // 
            // bDeleteBackupFIle
            // 
            this.bDeleteBackupFIle.Location = new System.Drawing.Point(531, 19);
            this.bDeleteBackupFIle.Margin = new System.Windows.Forms.Padding(4);
            this.bDeleteBackupFIle.Name = "bDeleteBackupFIle";
            this.bDeleteBackupFIle.Size = new System.Drawing.Size(100, 54);
            this.bDeleteBackupFIle.TabIndex = 6;
            this.bDeleteBackupFIle.Text = "Get Cloud Backups";
            this.bDeleteBackupFIle.UseVisualStyleBackColor = true;
            this.bDeleteBackupFIle.Click += new System.EventHandler(this.bDeleteBackupFIle_Click);
            // 
            // CloudDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(664, 116);
            this.Controls.Add(this.groupBox2);
            this.Name = "CloudDialog";
            this.Text = "Get your covers from the cloud!";
            this.Load += new System.EventHandler(this.CloudDialog_Load);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TextBox tAuthTokenFile;
        private System.Windows.Forms.TextBox tCloudFunctionUrl;
        private System.Windows.Forms.Button bDeleteBackupFIle;
    }
}