using System;
using Microsoft.Win32;

namespace AlbumCoverFinder
{
    /// <summary>
    /// Filter applied by the screensaver when picking covers from the cache.
    /// Persisted to HKCU\SOFTWARE\Demo_ScreenSaver so AlbumCoverFinder writes
    /// it and the screensaver reads it on launch.
    /// </summary>
    public class CoverFilter
    {
        public string Genre { get; set; } = string.Empty;
        public int YearMin { get; set; }
        public int YearMax { get; set; }

        public bool IsEmpty
        {
            get
            {
                return string.IsNullOrWhiteSpace(Genre)
                    && YearMin <= 0
                    && YearMax <= 0;
            }
        }

        public bool Matches(AlbumMetadata meta)
        {
            if (meta == null) meta = new AlbumMetadata();
            if (!string.IsNullOrWhiteSpace(Genre))
            {
                if (string.IsNullOrWhiteSpace(meta.Genre)) return false;
                if (meta.Genre.IndexOf(Genre, StringComparison.OrdinalIgnoreCase) < 0) return false;
            }
            if (YearMin > 0 && meta.Year > 0 && meta.Year < YearMin) return false;
            if (YearMax > 0 && meta.Year > 0 && meta.Year > YearMax) return false;
            // If the album has no year at all, treat it as passing a year filter -
            // hiding unknown-year albums entirely would empty the mosaic on legacy caches.
            return true;
        }

        // Default HKCU location, shared with ScreensaverSettings. Both registry
        // methods also accept an explicit path so tests can use a throwaway subkey
        // instead of snapshotting the live key; production uses the no-arg overloads.
        private const string DefaultRegistryPath = "SOFTWARE\\Demo_ScreenSaver";

        public static CoverFilter LoadFromRegistry() => LoadFromRegistry(DefaultRegistryPath);

        public static CoverFilter LoadFromRegistry(string registryPath)
        {
            var f = new CoverFilter();
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    if (key != null)
                    {
                        f.Genre = (key.GetValue("FilterGenre") as string) ?? string.Empty;
                        int.TryParse(key.GetValue("FilterYearMin")?.ToString() ?? "0", out int yMin);
                        int.TryParse(key.GetValue("FilterYearMax")?.ToString() ?? "0", out int yMax);
                        f.YearMin = yMin;
                        f.YearMax = yMax;
                    }
                }
            }
            catch { /* registry read shouldn't crash a screensaver */ }
            return f;
        }

        public void SaveToRegistry() => SaveToRegistry(DefaultRegistryPath);

        public void SaveToRegistry(string registryPath)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(registryPath))
                {
                    if (key == null) return;
                    key.SetValue("FilterGenre", Genre ?? string.Empty);
                    key.SetValue("FilterYearMin", YearMin, RegistryValueKind.DWord);
                    key.SetValue("FilterYearMax", YearMax, RegistryValueKind.DWord);
                }
            }
            catch { /* swallow - filter is optional */ }
        }
    }
}
