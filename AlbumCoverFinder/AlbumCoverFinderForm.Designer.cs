
namespace AlbumCoverFinder
{
    partial class AlbumCoverFinderForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.tDisplayUpdate = new System.Windows.Forms.TextBox();
            this.tabs = new System.Windows.Forms.TabControl();
            this.tabScan = new System.Windows.Forms.TabPage();
            this.tabFilter = new System.Windows.Forms.TabPage();
            this.tabHidden = new System.Windows.Forms.TabPage();
            this.tabDisplay = new System.Windows.Forms.TabPage();
            this.tabWallpaper = new System.Windows.Forms.TabPage();
            this.tabLockScreen = new System.Windows.Forms.TabPage();

            // Display tab controls
            this.lCoversWide = new System.Windows.Forms.Label();
            this.nCoversWide = new System.Windows.Forms.NumericUpDown();
            this.bResetCoversWide = new System.Windows.Forms.Button();
            this.lCoversWideHint = new System.Windows.Forms.Label();
            this.lEffect = new System.Windows.Forms.Label();
            this.cbEffect = new System.Windows.Forms.ComboBox();
            this.lEffectDuration = new System.Windows.Forms.Label();
            this.nEffectDurationSec = new System.Windows.Forms.NumericUpDown();
            this.lEffectDurationUnit = new System.Windows.Forms.Label();
            this.lGapInterval = new System.Windows.Forms.Label();
            this.nGapIntervalSec = new System.Windows.Forms.NumericUpDown();
            this.lGapIntervalUnit = new System.Windows.Forms.Label();
            this.bSaveDisplay = new System.Windows.Forms.Button();
            this.lDisplayHint = new System.Windows.Forms.Label();

            // Scan tab controls
            this.lFolder = new System.Windows.Forms.Label();
            this.tFolderToParse = new System.Windows.Forms.TextBox();
            this.bChangeFolder = new System.Windows.Forms.Button();
            this.bParseFolder = new System.Windows.Forms.Button();
            this.bDeleteBackupFIle = new System.Windows.Forms.Button();
            this.lAlbumCap = new System.Windows.Forms.Label();
            this.nAlbumCap = new System.Windows.Forms.NumericUpDown();
            this.lAlbumCapHint = new System.Windows.Forms.Label();
            this.bApplyCap = new System.Windows.Forms.Button();
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();

            // Filter tab controls
            this.lFilterGenre = new System.Windows.Forms.Label();
            this.cbGenreFilter = new System.Windows.Forms.ComboBox();
            this.lFilterYear = new System.Windows.Forms.Label();
            this.nYearMin = new System.Windows.Forms.NumericUpDown();
            this.lFilterYearTo = new System.Windows.Forms.Label();
            this.nYearMax = new System.Windows.Forms.NumericUpDown();
            this.bApplyFilter = new System.Windows.Forms.Button();
            this.bClearFilter = new System.Windows.Forms.Button();
            this.lFilterHint = new System.Windows.Forms.Label();

            // Hidden tab controls
            this.lHiddenHint = new System.Windows.Forms.Label();
            this.lstHidden = new System.Windows.Forms.ListBox();
            this.bUnhideSelected = new System.Windows.Forms.Button();
            this.bUnhideAll = new System.Windows.Forms.Button();

            // Wallpaper tab controls
            this.cbWallpaperEnabled = new System.Windows.Forms.CheckBox();
            this.lWallpaperInterval = new System.Windows.Forms.Label();
            this.nWallpaperInterval = new System.Windows.Forms.NumericUpDown();
            this.lWallpaperIntervalUnit = new System.Windows.Forms.Label();
            this.bWallpaperApplyNow = new System.Windows.Forms.Button();
            this.bWallpaperSave = new System.Windows.Forms.Button();
            this.lWallpaperHint = new System.Windows.Forms.Label();

            // Lock-screen tab controls
            this.cbLockScreenEnabled = new System.Windows.Forms.CheckBox();
            this.lLockScreenInterval = new System.Windows.Forms.Label();
            this.nLockScreenInterval = new System.Windows.Forms.NumericUpDown();
            this.lLockScreenIntervalUnit = new System.Windows.Forms.Label();
            this.bLockScreenApplyNow = new System.Windows.Forms.Button();
            this.bLockScreenSave = new System.Windows.Forms.Button();
            this.lLockScreenHint = new System.Windows.Forms.Label();

            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nAlbumCap)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nYearMin)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nYearMax)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nWallpaperInterval)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nLockScreenInterval)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nCoversWide)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nEffectDurationSec)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nGapIntervalSec)).BeginInit();
            this.tabs.SuspendLayout();
            this.tabScan.SuspendLayout();
            this.tabFilter.SuspendLayout();
            this.tabHidden.SuspendLayout();
            this.tabDisplay.SuspendLayout();
            this.tabWallpaper.SuspendLayout();
            this.tabLockScreen.SuspendLayout();
            this.SuspendLayout();

            //
            // pictureBox1
            //
            this.pictureBox1.Location = new System.Drawing.Point(15, 12);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(384, 256);
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            //
            // tDisplayUpdate
            //
            this.tDisplayUpdate.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.tDisplayUpdate.Location = new System.Drawing.Point(15, 275);
            this.tDisplayUpdate.Multiline = true;
            this.tDisplayUpdate.Name = "tDisplayUpdate";
            this.tDisplayUpdate.ReadOnly = true;
            this.tDisplayUpdate.Size = new System.Drawing.Size(384, 70);
            this.tDisplayUpdate.TabIndex = 1;
            //
            // tabs
            //
            this.tabs.Controls.Add(this.tabScan);
            this.tabs.Controls.Add(this.tabFilter);
            this.tabs.Controls.Add(this.tabHidden);
            this.tabs.Controls.Add(this.tabDisplay);
            this.tabs.Controls.Add(this.tabWallpaper);
            this.tabs.Controls.Add(this.tabLockScreen);
            this.tabs.Location = new System.Drawing.Point(15, 355);
            this.tabs.Name = "tabs";
            this.tabs.SelectedIndex = 0;
            this.tabs.Size = new System.Drawing.Size(384, 360);
            this.tabs.TabIndex = 2;
            this.folderBrowserDialog1.RootFolder = System.Environment.SpecialFolder.MyMusic;

            //
            // tabScan
            //
            this.tabScan.Controls.Add(this.lFolder);
            this.tabScan.Controls.Add(this.tFolderToParse);
            this.tabScan.Controls.Add(this.bChangeFolder);
            this.tabScan.Controls.Add(this.bParseFolder);
            this.tabScan.Controls.Add(this.bDeleteBackupFIle);
            this.tabScan.Controls.Add(this.lAlbumCap);
            this.tabScan.Controls.Add(this.nAlbumCap);
            this.tabScan.Controls.Add(this.lAlbumCapHint);
            this.tabScan.Controls.Add(this.bApplyCap);
            this.tabScan.Location = new System.Drawing.Point(4, 22);
            this.tabScan.Name = "tabScan";
            this.tabScan.Padding = new System.Windows.Forms.Padding(3);
            this.tabScan.Size = new System.Drawing.Size(376, 334);
            this.tabScan.TabIndex = 0;
            this.tabScan.Text = "Scan";
            this.tabScan.UseVisualStyleBackColor = true;

            this.lFolder.AutoSize = true;
            this.lFolder.Location = new System.Drawing.Point(10, 12);
            this.lFolder.Text = "Music folder:";

            this.tFolderToParse.Location = new System.Drawing.Point(10, 30);
            this.tFolderToParse.Size = new System.Drawing.Size(305, 20);
            this.tFolderToParse.Name = "tFolderToParse";

            this.bChangeFolder.Location = new System.Drawing.Point(322, 28);
            this.bChangeFolder.Size = new System.Drawing.Size(40, 25);
            this.bChangeFolder.Text = "...";
            this.bChangeFolder.UseVisualStyleBackColor = true;
            this.bChangeFolder.Click += new System.EventHandler(this.bChangeFolder_Click);

            this.bParseFolder.Location = new System.Drawing.Point(10, 65);
            this.bParseFolder.Size = new System.Drawing.Size(352, 36);
            this.bParseFolder.Text = "Parse folder for albums";
            this.bParseFolder.UseVisualStyleBackColor = true;
            this.bParseFolder.Click += new System.EventHandler(this.bParseFolder_Click);

            this.bDeleteBackupFIle.Location = new System.Drawing.Point(10, 110);
            this.bDeleteBackupFIle.Size = new System.Drawing.Size(352, 28);
            this.bDeleteBackupFIle.Text = "Delete cover cache (and rescan from scratch)";
            this.bDeleteBackupFIle.UseVisualStyleBackColor = true;
            this.bDeleteBackupFIle.Click += new System.EventHandler(this.bDeleteBackupFIle_Click);

            this.lAlbumCap.AutoSize = true;
            this.lAlbumCap.Location = new System.Drawing.Point(10, 165);
            this.lAlbumCap.Text = "Album cap:";

            this.nAlbumCap.Location = new System.Drawing.Point(85, 162);
            this.nAlbumCap.Maximum = new decimal(new int[] { 99999, 0, 0, 0 });
            this.nAlbumCap.Size = new System.Drawing.Size(80, 20);
            this.nAlbumCap.Name = "nAlbumCap";

            this.lAlbumCapHint.AutoSize = true;
            this.lAlbumCapHint.Location = new System.Drawing.Point(170, 165);
            this.lAlbumCapHint.Text = "(0 = unlimited)";

            this.bApplyCap.Location = new System.Drawing.Point(10, 195);
            this.bApplyCap.Size = new System.Drawing.Size(352, 28);
            this.bApplyCap.Text = "Save album cap";
            this.bApplyCap.UseVisualStyleBackColor = true;
            this.bApplyCap.Click += new System.EventHandler(this.bApplyCap_Click);

            //
            // tabFilter
            //
            this.tabFilter.Controls.Add(this.lFilterGenre);
            this.tabFilter.Controls.Add(this.cbGenreFilter);
            this.tabFilter.Controls.Add(this.lFilterYear);
            this.tabFilter.Controls.Add(this.nYearMin);
            this.tabFilter.Controls.Add(this.lFilterYearTo);
            this.tabFilter.Controls.Add(this.nYearMax);
            this.tabFilter.Controls.Add(this.bApplyFilter);
            this.tabFilter.Controls.Add(this.bClearFilter);
            this.tabFilter.Controls.Add(this.lFilterHint);
            this.tabFilter.Location = new System.Drawing.Point(4, 22);
            this.tabFilter.Name = "tabFilter";
            this.tabFilter.Padding = new System.Windows.Forms.Padding(3);
            this.tabFilter.Size = new System.Drawing.Size(376, 334);
            this.tabFilter.TabIndex = 1;
            this.tabFilter.Text = "Filter";
            this.tabFilter.UseVisualStyleBackColor = true;

            this.lFilterGenre.AutoSize = true;
            this.lFilterGenre.Location = new System.Drawing.Point(10, 18);
            this.lFilterGenre.Text = "Genre:";

            this.cbGenreFilter.Location = new System.Drawing.Point(60, 15);
            this.cbGenreFilter.Size = new System.Drawing.Size(300, 21);
            this.cbGenreFilter.Name = "cbGenreFilter";

            this.lFilterYear.AutoSize = true;
            this.lFilterYear.Location = new System.Drawing.Point(10, 53);
            this.lFilterYear.Text = "Year:";

            this.nYearMin.Location = new System.Drawing.Point(60, 50);
            this.nYearMin.Maximum = new decimal(new int[] { 9999, 0, 0, 0 });
            this.nYearMin.Size = new System.Drawing.Size(65, 20);
            this.nYearMin.Name = "nYearMin";

            this.lFilterYearTo.AutoSize = true;
            this.lFilterYearTo.Location = new System.Drawing.Point(132, 53);
            this.lFilterYearTo.Text = "-";

            this.nYearMax.Location = new System.Drawing.Point(145, 50);
            this.nYearMax.Maximum = new decimal(new int[] { 9999, 0, 0, 0 });
            this.nYearMax.Size = new System.Drawing.Size(65, 20);
            this.nYearMax.Name = "nYearMax";

            this.bApplyFilter.Location = new System.Drawing.Point(10, 85);
            this.bApplyFilter.Size = new System.Drawing.Size(175, 28);
            this.bApplyFilter.Text = "Apply filter";
            this.bApplyFilter.UseVisualStyleBackColor = true;
            this.bApplyFilter.Click += new System.EventHandler(this.bApplyFilter_Click);

            this.bClearFilter.Location = new System.Drawing.Point(190, 85);
            this.bClearFilter.Size = new System.Drawing.Size(170, 28);
            this.bClearFilter.Text = "Clear filter";
            this.bClearFilter.UseVisualStyleBackColor = true;
            this.bClearFilter.Click += new System.EventHandler(this.bClearFilter_Click);

            this.lFilterHint.Location = new System.Drawing.Point(10, 125);
            this.lFilterHint.Size = new System.Drawing.Size(350, 60);
            this.lFilterHint.ForeColor = System.Drawing.Color.DimGray;
            this.lFilterHint.Text = "Filter applies everywhere: screensaver mosaic, focal tile, wallpaper, lock screen, HTTP companion. Albums without a year tag always pass the year filter so unknown-year items don't get hidden.";

            //
            // tabHidden
            //
            this.tabHidden.Controls.Add(this.lHiddenHint);
            this.tabHidden.Controls.Add(this.lstHidden);
            this.tabHidden.Controls.Add(this.bUnhideSelected);
            this.tabHidden.Controls.Add(this.bUnhideAll);
            this.tabHidden.Location = new System.Drawing.Point(4, 22);
            this.tabHidden.Name = "tabHidden";
            this.tabHidden.Padding = new System.Windows.Forms.Padding(3);
            this.tabHidden.Size = new System.Drawing.Size(376, 334);
            this.tabHidden.TabIndex = 2;
            this.tabHidden.Text = "Hidden";
            this.tabHidden.UseVisualStyleBackColor = true;

            this.lHiddenHint.Location = new System.Drawing.Point(10, 10);
            this.lHiddenHint.Size = new System.Drawing.Size(355, 32);
            this.lHiddenHint.ForeColor = System.Drawing.Color.DimGray;
            this.lHiddenHint.Text = "Right-click any cover in the screensaver to hide it. The list below shows what's currently hidden.";

            this.lstHidden.Location = new System.Drawing.Point(10, 50);
            this.lstHidden.Size = new System.Drawing.Size(355, 215);
            this.lstHidden.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.lstHidden.Name = "lstHidden";

            this.bUnhideSelected.Location = new System.Drawing.Point(10, 275);
            this.bUnhideSelected.Size = new System.Drawing.Size(175, 28);
            this.bUnhideSelected.Text = "Unhide selected";
            this.bUnhideSelected.UseVisualStyleBackColor = true;
            this.bUnhideSelected.Click += new System.EventHandler(this.bUnhideSelected_Click);

            this.bUnhideAll.Location = new System.Drawing.Point(190, 275);
            this.bUnhideAll.Size = new System.Drawing.Size(175, 28);
            this.bUnhideAll.Text = "Unhide all";
            this.bUnhideAll.UseVisualStyleBackColor = true;
            this.bUnhideAll.Click += new System.EventHandler(this.bUnhideAll_Click);

            //
            // tabDisplay
            //
            this.tabDisplay.Controls.Add(this.lCoversWide);
            this.tabDisplay.Controls.Add(this.nCoversWide);
            this.tabDisplay.Controls.Add(this.bResetCoversWide);
            this.tabDisplay.Controls.Add(this.lCoversWideHint);
            this.tabDisplay.Controls.Add(this.lEffect);
            this.tabDisplay.Controls.Add(this.cbEffect);
            this.tabDisplay.Controls.Add(this.lEffectDuration);
            this.tabDisplay.Controls.Add(this.nEffectDurationSec);
            this.tabDisplay.Controls.Add(this.lEffectDurationUnit);
            this.tabDisplay.Controls.Add(this.lGapInterval);
            this.tabDisplay.Controls.Add(this.nGapIntervalSec);
            this.tabDisplay.Controls.Add(this.lGapIntervalUnit);
            this.tabDisplay.Controls.Add(this.bSaveDisplay);
            this.tabDisplay.Controls.Add(this.lDisplayHint);
            this.tabDisplay.Location = new System.Drawing.Point(4, 22);
            this.tabDisplay.Name = "tabDisplay";
            this.tabDisplay.Padding = new System.Windows.Forms.Padding(3);
            this.tabDisplay.Size = new System.Drawing.Size(376, 334);
            this.tabDisplay.TabIndex = 3;
            this.tabDisplay.Text = "Display";
            this.tabDisplay.UseVisualStyleBackColor = true;

            this.lCoversWide.AutoSize = true;
            this.lCoversWide.Location = new System.Drawing.Point(10, 18);
            this.lCoversWide.Text = "Covers wide:";

            this.nCoversWide.Location = new System.Drawing.Point(90, 15);
            this.nCoversWide.Minimum = new decimal(new int[] { 0, 0, 0, 0 });
            this.nCoversWide.Maximum = new decimal(new int[] { 60, 0, 0, 0 });
            this.nCoversWide.Size = new System.Drawing.Size(60, 20);
            this.nCoversWide.Name = "nCoversWide";

            this.bResetCoversWide.Location = new System.Drawing.Point(160, 13);
            this.bResetCoversWide.Size = new System.Drawing.Size(85, 24);
            this.bResetCoversWide.Text = "Reset";
            this.bResetCoversWide.UseVisualStyleBackColor = true;
            this.bResetCoversWide.Click += new System.EventHandler(this.bResetCoversWide_Click);

            this.lCoversWideHint.Location = new System.Drawing.Point(10, 40);
            this.lCoversWideHint.Size = new System.Drawing.Size(355, 32);
            this.lCoversWideHint.ForeColor = System.Drawing.Color.DimGray;
            this.lCoversWideHint.Text = "0 = native: covers display at 384 px and the grid ceil-rounds to fill the screen. Reset puts it back to 0.";

            this.lEffect.AutoSize = true;
            this.lEffect.Location = new System.Drawing.Point(10, 85);
            this.lEffect.Text = "Transition:";

            this.cbEffect.Location = new System.Drawing.Point(90, 82);
            this.cbEffect.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbEffect.Size = new System.Drawing.Size(150, 21);
            this.cbEffect.Name = "cbEffect";
            this.cbEffect.SelectedIndexChanged += new System.EventHandler(this.cbEffect_SelectedIndexChanged);

            this.lEffectDuration.AutoSize = true;
            this.lEffectDuration.Location = new System.Drawing.Point(10, 120);
            this.lEffectDuration.Text = "Transition duration:";

            this.nEffectDurationSec.Location = new System.Drawing.Point(130, 117);
            this.nEffectDurationSec.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.nEffectDurationSec.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            this.nEffectDurationSec.Value = new decimal(new int[] { 2, 0, 0, 0 });
            this.nEffectDurationSec.Size = new System.Drawing.Size(50, 20);
            this.nEffectDurationSec.Name = "nEffectDurationSec";

            this.lEffectDurationUnit.AutoSize = true;
            this.lEffectDurationUnit.Location = new System.Drawing.Point(185, 120);
            this.lEffectDurationUnit.Text = "seconds (1 - 10, ignored for Blink)";

            this.lGapInterval.AutoSize = true;
            this.lGapInterval.Location = new System.Drawing.Point(10, 155);
            this.lGapInterval.Text = "Gap between transitions:";

            this.nGapIntervalSec.Location = new System.Drawing.Point(155, 152);
            this.nGapIntervalSec.Minimum = new decimal(new int[] { 2, 0, 0, 0 });
            this.nGapIntervalSec.Maximum = new decimal(new int[] { 300, 0, 0, 0 });
            this.nGapIntervalSec.Value = new decimal(new int[] { 2, 0, 0, 0 });
            this.nGapIntervalSec.Size = new System.Drawing.Size(50, 20);
            this.nGapIntervalSec.Name = "nGapIntervalSec";

            this.lGapIntervalUnit.AutoSize = true;
            this.lGapIntervalUnit.Location = new System.Drawing.Point(210, 155);
            this.lGapIntervalUnit.Text = "seconds (min 2)";

            this.bSaveDisplay.Location = new System.Drawing.Point(10, 190);
            this.bSaveDisplay.Size = new System.Drawing.Size(355, 30);
            this.bSaveDisplay.Text = "Save display settings";
            this.bSaveDisplay.UseVisualStyleBackColor = true;
            this.bSaveDisplay.Click += new System.EventHandler(this.bSaveDisplay_Click);

            this.lDisplayHint.Location = new System.Drawing.Point(10, 230);
            this.lDisplayHint.Size = new System.Drawing.Size(355, 80);
            this.lDisplayHint.ForeColor = System.Drawing.Color.DimGray;
            this.lDisplayHint.Text = "Effective interval per swap = gap + transition. Example: gap 2 s + transition 5 s = 7 s between covers. Changes take effect on next screensaver launch.";

            //
            // tabWallpaper
            //
            this.tabWallpaper.Controls.Add(this.cbWallpaperEnabled);
            this.tabWallpaper.Controls.Add(this.lWallpaperInterval);
            this.tabWallpaper.Controls.Add(this.nWallpaperInterval);
            this.tabWallpaper.Controls.Add(this.lWallpaperIntervalUnit);
            this.tabWallpaper.Controls.Add(this.bWallpaperApplyNow);
            this.tabWallpaper.Controls.Add(this.bWallpaperSave);
            this.tabWallpaper.Controls.Add(this.lWallpaperHint);
            this.tabWallpaper.Location = new System.Drawing.Point(4, 22);
            this.tabWallpaper.Name = "tabWallpaper";
            this.tabWallpaper.Padding = new System.Windows.Forms.Padding(3);
            this.tabWallpaper.Size = new System.Drawing.Size(376, 334);
            this.tabWallpaper.TabIndex = 3;
            this.tabWallpaper.Text = "Wallpaper";
            this.tabWallpaper.UseVisualStyleBackColor = true;

            this.cbWallpaperEnabled.AutoSize = true;
            this.cbWallpaperEnabled.Location = new System.Drawing.Point(10, 15);
            this.cbWallpaperEnabled.Text = "Auto-update desktop wallpaper (requires Tray companion running)";
            this.cbWallpaperEnabled.UseVisualStyleBackColor = true;

            this.lWallpaperInterval.AutoSize = true;
            this.lWallpaperInterval.Location = new System.Drawing.Point(10, 50);
            this.lWallpaperInterval.Text = "Refresh every:";

            this.nWallpaperInterval.Location = new System.Drawing.Point(105, 47);
            this.nWallpaperInterval.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.nWallpaperInterval.Maximum = new decimal(new int[] { 1440, 0, 0, 0 });
            this.nWallpaperInterval.Value = new decimal(new int[] { 5, 0, 0, 0 });
            this.nWallpaperInterval.Size = new System.Drawing.Size(60, 20);
            this.nWallpaperInterval.Name = "nWallpaperInterval";

            this.lWallpaperIntervalUnit.AutoSize = true;
            this.lWallpaperIntervalUnit.Location = new System.Drawing.Point(170, 50);
            this.lWallpaperIntervalUnit.Text = "minutes";

            this.bWallpaperApplyNow.Location = new System.Drawing.Point(10, 85);
            this.bWallpaperApplyNow.Size = new System.Drawing.Size(175, 28);
            this.bWallpaperApplyNow.Text = "Apply wallpaper now";
            this.bWallpaperApplyNow.UseVisualStyleBackColor = true;
            this.bWallpaperApplyNow.Click += new System.EventHandler(this.bWallpaperApplyNow_Click);

            this.bWallpaperSave.Location = new System.Drawing.Point(190, 85);
            this.bWallpaperSave.Size = new System.Drawing.Size(170, 28);
            this.bWallpaperSave.Text = "Save settings";
            this.bWallpaperSave.UseVisualStyleBackColor = true;
            this.bWallpaperSave.Click += new System.EventHandler(this.bWallpaperSave_Click);

            this.lWallpaperHint.Location = new System.Drawing.Point(10, 125);
            this.lWallpaperHint.Size = new System.Drawing.Size(355, 70);
            this.lWallpaperHint.ForeColor = System.Drawing.Color.DimGray;
            this.lWallpaperHint.Text = "Apply now writes a fresh mosaic immediately. Auto-update is honoured by the Tray companion - if it isn't running, the wallpaper won't refresh on its own.";

            //
            // tabLockScreen
            //
            this.tabLockScreen.Controls.Add(this.cbLockScreenEnabled);
            this.tabLockScreen.Controls.Add(this.lLockScreenInterval);
            this.tabLockScreen.Controls.Add(this.nLockScreenInterval);
            this.tabLockScreen.Controls.Add(this.lLockScreenIntervalUnit);
            this.tabLockScreen.Controls.Add(this.bLockScreenApplyNow);
            this.tabLockScreen.Controls.Add(this.bLockScreenSave);
            this.tabLockScreen.Controls.Add(this.lLockScreenHint);
            this.tabLockScreen.Location = new System.Drawing.Point(4, 22);
            this.tabLockScreen.Name = "tabLockScreen";
            this.tabLockScreen.Padding = new System.Windows.Forms.Padding(3);
            this.tabLockScreen.Size = new System.Drawing.Size(376, 334);
            this.tabLockScreen.TabIndex = 4;
            this.tabLockScreen.Text = "Lock screen";
            this.tabLockScreen.UseVisualStyleBackColor = true;

            this.cbLockScreenEnabled.AutoSize = true;
            this.cbLockScreenEnabled.Location = new System.Drawing.Point(10, 15);
            this.cbLockScreenEnabled.Text = "Auto-update lock screen (requires Tray companion running)";
            this.cbLockScreenEnabled.UseVisualStyleBackColor = true;

            this.lLockScreenInterval.AutoSize = true;
            this.lLockScreenInterval.Location = new System.Drawing.Point(10, 50);
            this.lLockScreenInterval.Text = "Refresh every:";

            this.nLockScreenInterval.Location = new System.Drawing.Point(105, 47);
            this.nLockScreenInterval.Minimum = new decimal(new int[] { 5, 0, 0, 0 });
            this.nLockScreenInterval.Maximum = new decimal(new int[] { 1440, 0, 0, 0 });
            this.nLockScreenInterval.Value = new decimal(new int[] { 60, 0, 0, 0 });
            this.nLockScreenInterval.Size = new System.Drawing.Size(60, 20);
            this.nLockScreenInterval.Name = "nLockScreenInterval";

            this.lLockScreenIntervalUnit.AutoSize = true;
            this.lLockScreenIntervalUnit.Location = new System.Drawing.Point(170, 50);
            this.lLockScreenIntervalUnit.Text = "minutes";

            this.bLockScreenApplyNow.Location = new System.Drawing.Point(10, 85);
            this.bLockScreenApplyNow.Size = new System.Drawing.Size(175, 28);
            this.bLockScreenApplyNow.Text = "Apply lock screen now";
            this.bLockScreenApplyNow.UseVisualStyleBackColor = true;
            this.bLockScreenApplyNow.Click += new System.EventHandler(this.bLockScreenApplyNow_Click);

            this.bLockScreenSave.Location = new System.Drawing.Point(190, 85);
            this.bLockScreenSave.Size = new System.Drawing.Size(170, 28);
            this.bLockScreenSave.Text = "Save settings";
            this.bLockScreenSave.UseVisualStyleBackColor = true;
            this.bLockScreenSave.Click += new System.EventHandler(this.bLockScreenSave_Click);

            this.lLockScreenHint.Location = new System.Drawing.Point(10, 125);
            this.lLockScreenHint.Size = new System.Drawing.Size(355, 90);
            this.lLockScreenHint.ForeColor = System.Drawing.Color.DimGray;
            this.lLockScreenHint.Text = "Lock screen uses Windows.System.UserProfile.LockScreen. Managed-device group policies can block it silently - if Apply now appears to do nothing, that's the culprit.";

            //
            // AlbumCoverFinderForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(414, 730);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.tDisplayUpdate);
            this.Controls.Add(this.tabs);
            this.MaximizeBox = false;
            this.MinimumSize = new System.Drawing.Size(430, 770);
            this.Name = "AlbumCoverFinderForm";
            this.ShowIcon = false;
            this.Text = "Album Cover Finder - Settings";
            this.Load += new System.EventHandler(this.Form1_Load);

            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nAlbumCap)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nYearMin)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nYearMax)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nWallpaperInterval)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nLockScreenInterval)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nCoversWide)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nEffectDurationSec)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nGapIntervalSec)).EndInit();
            this.tabs.ResumeLayout(false);
            this.tabScan.ResumeLayout(false);
            this.tabScan.PerformLayout();
            this.tabFilter.ResumeLayout(false);
            this.tabFilter.PerformLayout();
            this.tabHidden.ResumeLayout(false);
            this.tabHidden.PerformLayout();
            this.tabDisplay.ResumeLayout(false);
            this.tabDisplay.PerformLayout();
            this.tabWallpaper.ResumeLayout(false);
            this.tabWallpaper.PerformLayout();
            this.tabLockScreen.ResumeLayout(false);
            this.tabLockScreen.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.TextBox tDisplayUpdate;
        private System.Windows.Forms.TabControl tabs;
        private System.Windows.Forms.TabPage tabScan;
        private System.Windows.Forms.TabPage tabFilter;
        private System.Windows.Forms.TabPage tabHidden;
        private System.Windows.Forms.TabPage tabDisplay;
        private System.Windows.Forms.TabPage tabWallpaper;
        private System.Windows.Forms.TabPage tabLockScreen;

        private System.Windows.Forms.Label lCoversWide;
        private System.Windows.Forms.NumericUpDown nCoversWide;
        private System.Windows.Forms.Button bResetCoversWide;
        private System.Windows.Forms.Label lCoversWideHint;
        private System.Windows.Forms.Label lEffect;
        private System.Windows.Forms.ComboBox cbEffect;
        private System.Windows.Forms.Label lEffectDuration;
        private System.Windows.Forms.NumericUpDown nEffectDurationSec;
        private System.Windows.Forms.Label lEffectDurationUnit;
        private System.Windows.Forms.Label lGapInterval;
        private System.Windows.Forms.NumericUpDown nGapIntervalSec;
        private System.Windows.Forms.Label lGapIntervalUnit;
        private System.Windows.Forms.Button bSaveDisplay;
        private System.Windows.Forms.Label lDisplayHint;

        private System.Windows.Forms.Label lFolder;
        private System.Windows.Forms.TextBox tFolderToParse;
        private System.Windows.Forms.Button bChangeFolder;
        private System.Windows.Forms.Button bParseFolder;
        private System.Windows.Forms.Button bDeleteBackupFIle;
        private System.Windows.Forms.Label lAlbumCap;
        private System.Windows.Forms.NumericUpDown nAlbumCap;
        private System.Windows.Forms.Label lAlbumCapHint;
        private System.Windows.Forms.Button bApplyCap;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;

        private System.Windows.Forms.Label lFilterGenre;
        private System.Windows.Forms.ComboBox cbGenreFilter;
        private System.Windows.Forms.Label lFilterYear;
        private System.Windows.Forms.NumericUpDown nYearMin;
        private System.Windows.Forms.Label lFilterYearTo;
        private System.Windows.Forms.NumericUpDown nYearMax;
        private System.Windows.Forms.Button bApplyFilter;
        private System.Windows.Forms.Button bClearFilter;
        private System.Windows.Forms.Label lFilterHint;

        private System.Windows.Forms.Label lHiddenHint;
        private System.Windows.Forms.ListBox lstHidden;
        private System.Windows.Forms.Button bUnhideSelected;
        private System.Windows.Forms.Button bUnhideAll;

        private System.Windows.Forms.CheckBox cbWallpaperEnabled;
        private System.Windows.Forms.Label lWallpaperInterval;
        private System.Windows.Forms.NumericUpDown nWallpaperInterval;
        private System.Windows.Forms.Label lWallpaperIntervalUnit;
        private System.Windows.Forms.Button bWallpaperApplyNow;
        private System.Windows.Forms.Button bWallpaperSave;
        private System.Windows.Forms.Label lWallpaperHint;

        private System.Windows.Forms.CheckBox cbLockScreenEnabled;
        private System.Windows.Forms.Label lLockScreenInterval;
        private System.Windows.Forms.NumericUpDown nLockScreenInterval;
        private System.Windows.Forms.Label lLockScreenIntervalUnit;
        private System.Windows.Forms.Button bLockScreenApplyNow;
        private System.Windows.Forms.Button bLockScreenSave;
        private System.Windows.Forms.Label lLockScreenHint;
    }
}
