using System;
using Microsoft.Win32;

namespace AlbumCoverFinder
{
    /// <summary>
    /// Names mirrored in the registry so we can persist as DWORDs.
    /// </summary>
    public enum TransitionEffect
    {
        Blink = 0,        // instant swap (no animation)
        FadeToBlack = 1,  // old fades to black, new fades in from black
        Merge = 2,        // per-pixel color blend old -> new (alpha lerp)
        Flip = 3          // horizontal flip - tile compresses to 0 then back with new image
    }

    /// <summary>
    /// Project-wide user settings backed by HKCU\SOFTWARE\Demo_ScreenSaver.
    /// Read by every component (screensaver, tray, finder) so changes from the
    /// finder UI propagate without restarting anything else.
    /// </summary>
    public class ScreensaverSettings
    {
        // Album cap: 0 = unlimited.
        public int AlbumCap { get; set; }

        // How many covers fit horizontally. 0 means "native" - the screensaver picks
        // the column count from the cached cover resolution (384 px tile).
        public int CoversWide { get; set; }

        // Visual transition effect played when a tile's cover is swapped.
        public TransitionEffect Effect { get; set; } = TransitionEffect.Merge;

        // Duration of that transition in ms. Ignored when Effect = Blink. Clamped to [1000, 10000].
        public int TransitionDurationMs { get; set; } = 1500;

        // Quiet gap between transitions in ms. Minimum 2000.
        // Total cycle for a tile swap = GapBetweenTransitionsMs + (Effect == Blink ? 0 : TransitionDurationMs).
        public int GapBetweenTransitionsMs { get; set; } = 2000;

        // Legacy swap interval kept for backward compat with older builds that wrote it.
        // The screensaver now derives its timer interval from Gap + Transition.
        public int SwapIntervalMs { get; set; } = 1000;

        // Wallpaper companion (in the Tray app). Off by default - it's a constant
        // visual change on the desktop that some users find overwhelming; opt-in
        // from the Wallpaper tab in AlbumCoverFinder.
        public bool WallpaperEnabled { get; set; } = false;
        public int WallpaperIntervalMinutes { get; set; } = 5;

        // Lock-screen auto-update (in the Tray app). Default off because some
        // managed Windows policies block SetImageFileAsync without warning.
        public bool LockScreenEnabled { get; set; } = false;
        public int LockScreenIntervalMinutes { get; set; } = 60;

        // Discord Rich Presence.
        public string DiscordAppId { get; set; } = string.Empty;

        private const string RegistryPath = "SOFTWARE\\Demo_ScreenSaver";

        public static ScreensaverSettings Load()
        {
            var s = new ScreensaverSettings();
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        s.AlbumCap = ReadInt(key, "AlbumCap", 0);
                        s.SwapIntervalMs = Math.Max(100, ReadInt(key, "SwapIntervalMs", 1000));
                        s.CoversWide = Math.Max(0, ReadInt(key, "CoversWide", 0));
                        s.Effect = (TransitionEffect)Math.Max(0, Math.Min(3, ReadInt(key, "TransitionEffect", (int)TransitionEffect.Merge)));
                        s.TransitionDurationMs = Math.Max(1000, Math.Min(10000, ReadInt(key, "TransitionDurationMs", 1500)));
                        s.GapBetweenTransitionsMs = Math.Max(2000, ReadInt(key, "GapBetweenTransitionsMs", 2000));
                        s.WallpaperEnabled = ReadBool(key, "WallpaperEnabled", true);
                        s.WallpaperIntervalMinutes = Math.Max(1, ReadInt(key, "WallpaperIntervalMinutes", 5));
                        s.LockScreenEnabled = ReadBool(key, "LockScreenEnabled", false);
                        s.LockScreenIntervalMinutes = Math.Max(5, ReadInt(key, "LockScreenIntervalMinutes", 60));
                        s.DiscordAppId = (key.GetValue("DiscordAppId") as string) ?? string.Empty;
                    }
                }
            }
            catch { /* defaults are fine */ }
            return s;
        }

        public void Save()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key == null) return;
                    key.SetValue("AlbumCap", AlbumCap, RegistryValueKind.DWord);
                    key.SetValue("SwapIntervalMs", SwapIntervalMs, RegistryValueKind.DWord);
                    key.SetValue("CoversWide", CoversWide, RegistryValueKind.DWord);
                    key.SetValue("TransitionEffect", (int)Effect, RegistryValueKind.DWord);
                    key.SetValue("TransitionDurationMs", TransitionDurationMs, RegistryValueKind.DWord);
                    key.SetValue("GapBetweenTransitionsMs", GapBetweenTransitionsMs, RegistryValueKind.DWord);
                    key.SetValue("WallpaperEnabled", WallpaperEnabled ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("WallpaperIntervalMinutes", WallpaperIntervalMinutes, RegistryValueKind.DWord);
                    key.SetValue("LockScreenEnabled", LockScreenEnabled ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("LockScreenIntervalMinutes", LockScreenIntervalMinutes, RegistryValueKind.DWord);
                    key.SetValue("DiscordAppId", DiscordAppId ?? string.Empty);
                }
            }
            catch { }
        }

        private static int ReadInt(RegistryKey key, string name, int dflt)
        {
            object v = key.GetValue(name);
            if (v == null) return dflt;
            if (v is int i) return i;
            int parsed;
            return int.TryParse(v.ToString(), out parsed) ? parsed : dflt;
        }

        private static bool ReadBool(RegistryKey key, string name, bool dflt)
        {
            object v = key.GetValue(name);
            if (v == null) return dflt;
            if (v is int i) return i != 0;
            int parsed;
            return int.TryParse(v.ToString(), out parsed) ? parsed != 0 : dflt;
        }
    }
}
