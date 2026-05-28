using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AlbumCoverFinder
{
    /// <summary>
    /// Persistent list of cover keys the user has hidden (via right-click in the
    /// screensaver). Keys are stored one per line at
    /// %USERPROFILE%\ScreenSaverCovers\blocklist.txt.
    ///
    /// Acts as a hard filter on top of <see cref="CoverFilter"/>: blocked keys
    /// never appear in the mosaic, focal tile, sibling, wallpaper, lock screen,
    /// or HTTP companion.
    /// </summary>
    public class CoverBlocklist
    {
        private readonly string m_sFilePath;
        private HashSet<string> m_oBlocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object m_oLock = new object();

        public CoverBlocklist(string cacheDir)
        {
            m_sFilePath = Path.Combine(cacheDir, "blocklist.txt");
            Load();
        }

        public bool IsBlocked(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (m_oLock) return m_oBlocked.Contains(key);
        }

        public bool Block(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (m_oLock)
            {
                if (!m_oBlocked.Add(key)) return false;
            }
            PersistAppend(key);
            return true;
        }

        public bool Unblock(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            bool removed;
            lock (m_oLock) { removed = m_oBlocked.Remove(key); }
            if (removed) PersistRewrite();
            return removed;
        }

        public List<string> GetAll()
        {
            lock (m_oLock) return m_oBlocked.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public int Count
        {
            get { lock (m_oLock) return m_oBlocked.Count; }
        }

        public void Clear()
        {
            lock (m_oLock) m_oBlocked.Clear();
            PersistRewrite();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(m_sFilePath)) return;
                foreach (string raw in File.ReadAllLines(m_sFilePath))
                {
                    string line = raw?.Trim();
                    if (!string.IsNullOrEmpty(line)) m_oBlocked.Add(line);
                }
            }
            catch
            {
                // Best-effort: a corrupt blocklist shouldn't break the screensaver.
            }
        }

        private void PersistAppend(string key)
        {
            try
            {
                EnsureDir();
                File.AppendAllLines(m_sFilePath, new[] { key });
            }
            catch { /* swallow - file IO can fail when Drive syncs; next change retries */ }
        }

        private void PersistRewrite()
        {
            try
            {
                EnsureDir();
                lock (m_oLock)
                {
                    File.WriteAllLines(m_sFilePath, m_oBlocked.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
                }
            }
            catch { }
        }

        private void EnsureDir()
        {
            string dir = Path.GetDirectoryName(m_sFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
