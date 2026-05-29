using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace AlbumCoverFinder
{
    /// <summary>
    /// Cover Manager class.
    ///
    /// Initialisation:
    /// Recursively parses a directory to fetch cover pictures from any audio file format
    /// supported by TagLib# (MP3, FLAC, M4A/AAC, OGG, WMA, OPUS, WAV with tags).
    /// Loads a previously saved cover database.
    ///
    /// Once initialized:
    /// Offers covers in sequence or at random.
    /// Sends a placeholder when empty.
    ///
    /// Storage layout:
    ///   %USERPROFILE%\ScreenSaverCovers\
    ///     index.txt        - one artist+album key per line; line N maps to N.png
    ///     0.png, 1.png ... - resized 384x384 cover thumbnails
    /// </summary>
    public class AlbumCoverMgr
    {
        #region Class Members
        private static readonly HashSet<string> s_audioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".flac", ".m4a", ".m4b", ".aac", ".ogg", ".oga", ".opus", ".wma", ".wav", ".aiff", ".aif", ".ape"
        };

        private string m_sCacheDir;
        private string m_sIndexFile;
        private int m_iLastBackupCount = 0;
        private string m_sMusicPath;
        private Dictionary<string, Image> m_dPictures;
        // Lazy load (read-only consumers: screensaver, tray): on startup we record
        // each key's PNG path here WITHOUT decoding it, then decode on demand in
        // GetImage and cache the result into m_dPictures. This turns a multi-second
        // "decode all 2000+ covers" startup into an instant index read - tiles decode
        // their own cover the first time they're shown. AlbumCoverFinder loads eager
        // (m_bLazyLoad = false) because it scans, edits, and re-saves the cache.
        private readonly bool m_bLazyLoad;
        private readonly Dictionary<string, string> m_dKeyToPng = new Dictionary<string, string>();
        // Guards the cover/key collections. Needed because lazy decoding writes the
        // decode cache from whatever thread calls GetImage (e.g. the iTunes file
        // watcher / poll thread), and AddCover appends from the poll thread while the
        // UI thread reads. A single coarse lock is plenty for these low-frequency ops.
        private readonly object m_oSync = new object();
        private Dictionary<string, AlbumMetadata> m_dMetadata = new Dictionary<string, AlbumMetadata>();
        private List<string> m_aOrderedKeys = new List<string>();
        // Default cap is 0 = unlimited. Now that each cover is its own PNG file,
        // the old "memory hog at 700" reason for capping is much weaker.
        // Configurable via HKCU\SOFTWARE\Demo_ScreenSaver\AlbumCap.
        private int m_iMaxPicCount = 0;
        private static readonly Random s_oRand = new Random();
        private Thread m_oThread;
        private bool m_bCapReached = false;
        private CoverFilter m_oFilter;
        private CoverBlocklist m_oBlocklist;
        // Byte-length -> set of SHA256 hex hashes already in the cache. Used by
        // the scanner to skip covers whose original embedded artwork is byte-
        // identical to one we've already added under a different artist+album
        // (compilations, re-releases). Hash is only computed when a length
        // collision is found, per the user's "trigger only if same byte count"
        // requirement.
        private Dictionary<int, HashSet<string>> m_dHashesByLength = new Dictionary<int, HashSet<string>>();
        private static readonly JsonSerializerOptions s_jsonOpts = new JsonSerializerOptions { WriteIndented = false };

        public delegate void AlbumFound(int p_iAlbumFounds, Image p_oPicture);
        public event AlbumFound oAlbumFoundEvent;

        public bool CapReached { get { return m_bCapReached; } }
        public int MaxPicCount { get { return m_iMaxPicCount; } }
        public CoverBlocklist Blocklist { get { return m_oBlocklist; } }

        /// <summary>
        /// Override the album cap. 0 disables the cap entirely. Affects in-flight scans.
        /// </summary>
        public void SetAlbumCap(int cap)
        {
            m_iMaxPicCount = Math.Max(0, cap);
        }
        #endregion

        #region Constructors
        public AlbumCoverMgr(string p_sCustomMusicPath, bool lazyLoad = false)
        {
            m_bLazyLoad = lazyLoad;
            m_sMusicPath = p_sCustomMusicPath;
            InitializePaths();
            LoadBackupData();
        }

        public AlbumCoverMgr(bool lazyLoad = false)
        {
            m_bLazyLoad = lazyLoad;
            m_sMusicPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            InitializePaths();
            LoadBackupData();
        }

        private void InitializePaths()
        {
            string sUserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            m_sCacheDir = Path.Combine(sUserProfile, "ScreenSaverCovers");
            m_sIndexFile = Path.Combine(m_sCacheDir, "index.txt");
            m_oBlocklist = new CoverBlocklist(m_sCacheDir);
        }

        #endregion

        #region Public Functions

        public void ParseDirectoryForPictures()
        {
            if (m_oThread == null || m_oThread.IsAlive != true)
            {
                m_bCapReached = false;
                m_oThread = new Thread(RunCoverMrg);
                m_oThread.IsBackground = true;
                m_oThread.Start();
            }
        }

        public void ParseDirectoryForPictures(string p_sDirectoryToParse)
        {
            m_sMusicPath = p_sDirectoryToParse;
            ParseDirectoryForPictures();
        }

        /// returns a random picture from our list
        public Image GetRandomPicture()
        {
            var pool = GetFilteredPool();
            if (pool.Count > 0)
                return GetImage(pool[s_oRand.Next(pool.Count)]) ?? BuildPlaceholder();
            return BuildPlaceholder();
        }

        /// <summary>
        /// Set the active cover filter. Null disables filtering (the default).
        /// Callers downstream of this (the screensaver) will see only matching keys
        /// from <see cref="GetRandomEntry"/>, <see cref="GetRandomPicture"/>, and
        /// <see cref="GetKeysSortedByYear"/>.
        /// </summary>
        public void SetFilter(CoverFilter filter)
        {
            m_oFilter = filter;
        }

        public CoverFilter GetFilter() { return m_oFilter; }

        /// <summary>
        /// Returns the list of cover keys currently allowed by the active filter.
        /// When no filter is set, returns all keys in scan order.
        /// </summary>
        public List<string> GetFilteredPool()
        {
            lock (m_oSync)
            {
                if (m_aOrderedKeys.Count == 0) return new List<string>();

                List<string> result = new List<string>(m_aOrderedKeys.Count);
                foreach (string k in m_aOrderedKeys)
                {
                    if (!IsAvailable(k)) continue;
                    if (m_oBlocklist != null && m_oBlocklist.IsBlocked(k)) continue;
                    if (m_oFilter != null)
                    {
                        AlbumMetadata meta;
                        m_dMetadata.TryGetValue(k, out meta);
                        if (!m_oFilter.Matches(meta)) continue;
                    }
                    result.Add(k);
                }
                return result;
            }
        }

        /// <summary>
        /// Returns a random key matching the same artist as <paramref name="p_sKey"/>
        /// but for a different album. Used by the #8 "more from this artist" thumbnail.
        /// Returns null if there's no sibling album in the cache.
        /// </summary>
        public string GetSiblingKey(string p_sKey)
        {
            if (string.IsNullOrEmpty(p_sKey)) return null;
            AlbumMetadata meta;
            if (!m_dMetadata.TryGetValue(p_sKey, out meta)) return null;
            if (string.IsNullOrEmpty(meta.Artist)) return null;

            List<string> siblings = null;
            foreach (var kv in m_dMetadata)
            {
                if (kv.Key == p_sKey) continue;
                if (m_oBlocklist != null && m_oBlocklist.IsBlocked(kv.Key)) continue;
                if (string.Equals(kv.Value.Artist, meta.Artist, StringComparison.OrdinalIgnoreCase))
                {
                    if (siblings == null) siblings = new List<string>();
                    siblings.Add(kv.Key);
                }
            }
            if (siblings == null || siblings.Count == 0) return null;
            return siblings[s_oRand.Next(siblings.Count)];
        }

        public AlbumMetadata GetMetadata(string p_sKey)
        {
            AlbumMetadata meta;
            return m_dMetadata.TryGetValue(p_sKey ?? string.Empty, out meta) ? meta : new AlbumMetadata();
        }

        /// <summary>
        /// Returns all distinct genres seen in the scan, sorted alphabetically.
        /// Used to populate the filter dropdown in AlbumCoverFinder.
        /// </summary>
        public List<string> GetDistinctGenres()
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var meta in m_dMetadata.Values)
                if (!string.IsNullOrWhiteSpace(meta.Genre))
                    seen.Add(meta.Genre);
            var sorted = new List<string>(seen);
            sorted.Sort(StringComparer.OrdinalIgnoreCase);
            return sorted;
        }

        /// <summary>
        /// Returns (minYear, maxYear) across the scanned library. (0, 0) when no
        /// year metadata is available.
        /// </summary>
        public (int min, int max) GetYearRange()
        {
            int mn = int.MaxValue, mx = int.MinValue;
            foreach (var meta in m_dMetadata.Values)
            {
                if (meta.Year <= 0) continue;
                if (meta.Year < mn) mn = meta.Year;
                if (meta.Year > mx) mx = meta.Year;
            }
            if (mn == int.MaxValue) return (0, 0);
            return (mn, mx);
        }

        /// <summary>
        /// Returns a random (key, image) pair, avoiding keys already in <paramref name="p_oExcludeKeys"/>
        /// when possible. Falls back to an unconstrained random pick if every key is excluded
        /// or no exclusion set is provided.
        /// </summary>
        public KeyValuePair<string, Image> GetRandomEntry(HashSet<string> p_oExcludeKeys)
        {
            var pool = GetFilteredPool();
            if (pool.Count == 0)
                return new KeyValuePair<string, Image>(string.Empty, BuildPlaceholder());

            if (p_oExcludeKeys != null && p_oExcludeKeys.Count > 0)
            {
                List<string> available = new List<string>(pool.Count);
                foreach (string k in pool)
                    if (!p_oExcludeKeys.Contains(k))
                        available.Add(k);
                if (available.Count > 0)
                {
                    string pick = available[s_oRand.Next(available.Count)];
                    return new KeyValuePair<string, Image>(pick, GetImage(pick));
                }
            }

            string chosen = pool[s_oRand.Next(pool.Count)];
            return new KeyValuePair<string, Image>(chosen, GetImage(chosen));
        }

        /// <summary>
        /// Returns the cover for a specific artist+album key, or null when not in the cache.
        /// Used by the Now-Playing focal tile to look up the active track's cover.
        /// </summary>
        public Image GetPictureByKey(string p_sKey)
        {
            return GetImage(p_sKey);
        }

        /// <summary>
        /// Returns the decoded cover for a key. In lazy mode the PNG is decoded on
        /// first request and cached into m_dPictures; subsequent calls are dictionary
        /// hits. Returns null if the key is unknown or the PNG can't be decoded.
        /// </summary>
        private Image GetImage(string p_sKey)
        {
            if (string.IsNullOrEmpty(p_sKey)) return null;
            lock (m_oSync)
            {
                Image img;
                if (m_dPictures != null && m_dPictures.TryGetValue(p_sKey, out img)) return img;
                if (m_dKeyToPng.TryGetValue(p_sKey, out string sPng))
                {
                    try
                    {
                        img = LoadImageNoLock(sPng);
                        if (img != null)
                        {
                            if (m_dPictures == null) m_dPictures = new Dictionary<string, Image>();
                            m_dPictures[p_sKey] = img;
                        }
                        return img;
                    }
                    catch { return null; }
                }
                return null;
            }
        }

        /// <summary>
        /// Persists a cover fetched from somewhere other than the file's embedded tags
        /// (e.g. iTunes' artwork cache, extracted via COM for the now-playing track) into
        /// the shared cover cache, so it shows up in the mosaic on the next load. No-op if
        /// the album is already known. Returns true if it was newly added.
        ///
        /// This is how albums whose .m4a files carry no embedded artwork (their art lives
        /// only in iTunes' Album Artwork cache) get into the screensaver: play the track
        /// once and the Tray captures its cover here.
        /// </summary>
        public bool AddCover(string p_sArtist, string p_sAlbum, Image p_oCover)
        {
            if (p_oCover == null) return false;
            string key = BuildKey(p_sArtist, p_sAlbum);
            if (string.IsNullOrEmpty(key)) return false;

            lock (m_oSync)
            {
                if (IsAvailable(key)) return false;
                try
                {
                    if (!Directory.Exists(m_sCacheDir)) Directory.CreateDirectory(m_sCacheDir);

                    // Index line N maps to N.png - append, so the new line's index is the
                    // current line count.
                    string[] lines = File.Exists(m_sIndexFile) ? File.ReadAllLines(m_sIndexFile) : Array.Empty<string>();
                    int idx = lines.Length;
                    string sPng = Path.Combine(m_sCacheDir, idx + ".png");

                    using (var resized = ResizeImage(p_oCover, 384, 384))
                        resized.Save(sPng, ImageFormat.Png);
                    File.AppendAllLines(m_sIndexFile, new[] { key });

                    var meta = new AlbumMetadata { Artist = p_sArtist ?? string.Empty, Album = p_sAlbum ?? string.Empty };
                    try { File.WriteAllText(Path.Combine(m_sCacheDir, idx + ".json"), JsonSerializer.Serialize(meta, s_jsonOpts)); }
                    catch { /* sidecar is optional */ }

                    // In-memory: make it usable in THIS process immediately too.
                    m_dKeyToPng[key] = sPng;
                    m_aOrderedKeys.Add(key);
                    m_dMetadata[key] = meta;
                    return true;
                }
                catch { return false; }
            }
        }

        /// <summary>True if a cover exists for this key (decoded or lazily on disk).</summary>
        private bool IsAvailable(string p_sKey)
        {
            return !string.IsNullOrEmpty(p_sKey)
                && ((m_dPictures != null && m_dPictures.ContainsKey(p_sKey)) || m_dKeyToPng.ContainsKey(p_sKey));
        }

        public static string BuildKey(string p_sArtist, string p_sAlbum)
        {
            return (p_sArtist ?? string.Empty) + (p_sAlbum ?? string.Empty);
        }

        private void RegisterHash(int byteLen, string hexHash)
        {
            if (byteLen <= 0 || string.IsNullOrEmpty(hexHash)) return;
            if (!m_dHashesByLength.TryGetValue(byteLen, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                m_dHashesByLength[byteLen] = set;
            }
            set.Add(hexHash);
        }

        private static string ComputeSha256Hex(byte[] data)
        {
            byte[] hash = SHA256.HashData(data);
            return Convert.ToHexString(hash);
        }

        private static Bitmap BuildPlaceholder()
        {
            Bitmap oBmp = new Bitmap(384, 384);
            using (Graphics g = Graphics.FromImage(oBmp))
                g.Clear(Color.Black);
            return oBmp;
        }

        public int GetAlbumTotal()
        {
            // Count of known covers (decoded or lazily on disk), not just decoded ones.
            lock (m_oSync)
                return m_aOrderedKeys != null ? m_aOrderedKeys.Count : 0;
        }

        public void DeleteAlbumBackup()
        {
            m_bCapReached = false;
            if (m_dPictures != null)
                m_dPictures.Clear();
            else
                m_dPictures = new Dictionary<string, Image>();
            m_dKeyToPng.Clear();
            m_dMetadata.Clear();
            m_aOrderedKeys.Clear();
            m_iLastBackupCount = 0;

            try
            {
                if (Directory.Exists(m_sCacheDir))
                    Directory.Delete(m_sCacheDir, true);
            }
            catch
            {
                // Best-effort: a locked file shouldn't crash the UI.
            }
            if (oAlbumFoundEvent != null)
                oAlbumFoundEvent(0, GetRandomPicture());
        }

        #endregion

        #region Private Functions

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

        private void RunCoverMrg()
        {
            try
            {
                var files = Directory.EnumerateFiles(m_sMusicPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => s_audioExtensions.Contains(Path.GetExtension(f)));

                foreach (string sCurrentFile in files)
                {
                    AddInfoAndPictureFromFile(sCurrentFile);

                    // Cap of 0 disables the limit entirely (the default since per-file PNG cache made bulk memory non-issue).
                    if (m_iMaxPicCount > 0 && m_dPictures.Count > m_iMaxPicCount)
                    {
                        m_bCapReached = true;
                        if (oAlbumFoundEvent != null)
                            oAlbumFoundEvent(m_dPictures.Count, GetRandomPicture());
                        break;
                    }
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
        }

        private void LoadBackupData()
        {
            m_dPictures = new Dictionary<string, Image>();
            m_aOrderedKeys.Clear();

            try
            {
                if (File.Exists(m_sIndexFile))
                    LoadFromCacheDir();
            }
            catch
            {
                m_dPictures = new Dictionary<string, Image>();
                m_aOrderedKeys.Clear();
            }

            m_iLastBackupCount = m_aOrderedKeys.Count;
            if (oAlbumFoundEvent != null && m_aOrderedKeys.Count > 0)
                oAlbumFoundEvent(m_aOrderedKeys.Count, GetRandomPicture());
        }

        private void LoadFromCacheDir()
        {
            string[] lines;
            try { lines = File.ReadAllLines(m_sIndexFile); }
            catch { return; }

            for (int i = 0; i < lines.Length; i++)
            {
                string sKey = lines[i];
                if (string.IsNullOrEmpty(sKey)) continue;
                if (IsAvailable(sKey)) continue;
                string sPngPath = Path.Combine(m_sCacheDir, i + ".png");
                if (!File.Exists(sPngPath)) continue;
                try
                {
                    if (m_bLazyLoad)
                    {
                        // Defer decoding: just remember where this cover lives.
                        m_dKeyToPng[sKey] = sPngPath;
                    }
                    else
                    {
                        m_dPictures[sKey] = LoadImageNoLock(sPngPath);
                    }
                    m_aOrderedKeys.Add(sKey);

                    // Metadata sidecar - optional, missing is fine for caches written
                    // before metadata support landed.
                    string sJsonPath = Path.Combine(m_sCacheDir, i + ".json");
                    if (File.Exists(sJsonPath))
                    {
                        try
                        {
                            var meta = JsonSerializer.Deserialize<AlbumMetadata>(File.ReadAllText(sJsonPath));
                            if (meta != null)
                            {
                                m_dMetadata[sKey] = meta;
                                // Seed the dedup pool with what we already have on disk.
                                // Old sidecars without hash info simply aren't part of
                                // the pool - their dedup is best-effort and gets fixed
                                // on the first scan that actually re-encounters them.
                                if (meta.CoverByteLength > 0 && !string.IsNullOrEmpty(meta.CoverHash))
                                    RegisterHash(meta.CoverByteLength, meta.CoverHash);
                            }
                        }
                        catch { /* corrupt sidecar - skip silently */ }
                    }
                }
                catch
                {
                    // Skip unreadable / corrupt cover but keep going.
                }
            }
        }

        /// <summary>
        /// Loads a PNG into an in-memory Bitmap without holding a lock on the source file.
        /// </summary>
        private static Image LoadImageNoLock(string p_sPath)
        {
            using (FileStream fs = new FileStream(p_sPath, FileMode.Open, FileAccess.Read))
                return new Bitmap(fs);
        }

        private void EnsureCacheDir()
        {
            if (!Directory.Exists(m_sCacheDir))
                Directory.CreateDirectory(m_sCacheDir);
        }

        private void SaveBackupData(int p_iLastCount)
        {
            if (p_iLastCount == m_iLastBackupCount)
                return;
            try
            {
                EnsureCacheDir();

                HashSet<string> oAlreadyOnDisk = new HashSet<string>(m_aOrderedKeys);
                List<string> aNewKeys = new List<string>();

                foreach (KeyValuePair<string, Image> kvp in m_dPictures)
                {
                    if (oAlreadyOnDisk.Contains(kvp.Key)) continue;
                    int iIndex = m_aOrderedKeys.Count + aNewKeys.Count;
                    string sPngPath = Path.Combine(m_sCacheDir, iIndex + ".png");
                    try
                    {
                        using (Bitmap oCopy = new Bitmap(kvp.Value))
                            oCopy.Save(sPngPath, ImageFormat.Png);
                        aNewKeys.Add(kvp.Key);

                        // Write the metadata sidecar if we captured any during scan.
                        AlbumMetadata meta;
                        if (m_dMetadata.TryGetValue(kvp.Key, out meta))
                        {
                            try
                            {
                                string sJsonPath = Path.Combine(m_sCacheDir, iIndex + ".json");
                                File.WriteAllText(sJsonPath, JsonSerializer.Serialize(meta, s_jsonOpts));
                            }
                            catch { /* sidecar IO failure is non-fatal */ }
                        }
                    }
                    catch
                    {
                        // Skip the cover that failed to save; the next scan can retry.
                    }
                }

                if (aNewKeys.Count > 0)
                {
                    File.AppendAllLines(m_sIndexFile, aNewKeys);
                    m_aOrderedKeys.AddRange(aNewKeys);
                }

                m_iLastBackupCount = p_iLastCount;
            }
            catch
            {
                // Best-effort persistence; an IO failure shouldn't crash a background scan.
            }
        }

        private bool AddInfoAndPictureFromFile(string p_sFile)
        {
            try
            {
                using (TagLib.File oFile = TagLib.File.Create(p_sFile))
                {
                    string sArtist = !string.IsNullOrEmpty(oFile.Tag.FirstAlbumArtist)
                        ? oFile.Tag.FirstAlbumArtist
                        : oFile.Tag.FirstPerformer ?? string.Empty;
                    string sAlbum = oFile.Tag.Album ?? string.Empty;
                    string sKey = BuildKey(sArtist, sAlbum);

                    if (string.IsNullOrEmpty(sKey)) return false;
                    if (m_dPictures.ContainsKey(sKey)) return true;

                    var pictures = oFile.Tag.Pictures;
                    if (pictures == null || pictures.Length == 0) return false;

                    byte[] pictureData = pictures[0].Data.Data;
                    if (pictureData == null || pictureData.Length == 0) return false;

                    // Dedup gate: same byte count is a necessary condition for
                    // byte-identical images, so we only hash when there's actually
                    // a chance of a collision. This keeps cheap-case scans fast.
                    int byteLen = pictureData.Length;
                    string coverHash = null;
                    if (m_dHashesByLength.TryGetValue(byteLen, out var existingHashes))
                    {
                        coverHash = ComputeSha256Hex(pictureData);
                        if (existingHashes.Contains(coverHash))
                        {
                            // Identical artwork already in the cache under a different
                            // artist+album (e.g. a compilation track). Skip silently;
                            // m_dPictures stays as-is.
                            return true;
                        }
                    }
                    if (coverHash == null) coverHash = ComputeSha256Hex(pictureData);

                    Bitmap oImage;
                    using (var ms = new MemoryStream(pictureData))
                    using (var src = new Bitmap(ms))
                    {
                        oImage = ResizeImage(src, 384, 384);
                    }

                    m_dPictures.Add(sKey, oImage);
                    m_dMetadata[sKey] = new AlbumMetadata
                    {
                        Artist = sArtist,
                        Album = sAlbum,
                        Year = (int)oFile.Tag.Year,
                        Genre = oFile.Tag.FirstGenre ?? string.Empty,
                        CoverByteLength = byteLen,
                        CoverHash = coverHash
                    };
                    RegisterHash(byteLen, coverHash);
                    if (oAlbumFoundEvent != null)
                        oAlbumFoundEvent(m_dPictures.Count, oImage);
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Unsupported format, corrupt tag, locked file - skip and continue scanning.
                return false;
            }
        }
        #endregion
    }
}
