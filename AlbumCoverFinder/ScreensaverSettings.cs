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
    /// What the Tray draws on the Stream Deck (over raw USB HID) while Windows is
    /// locked. Mirrored to the registry as a DWORD.
    /// </summary>
    public enum LockDeckMode
    {
        NowPlaying = 0,   // default: keep showing the currently-playing cover
        Picture = 1,      // a fixed image file (LockDeckImagePath)
        Gif = 2,          // an animated GIF file (LockDeckImagePath)
        Off = 3           // draw nothing - leave the deck to Elgato (its logo)
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
        // Defaults to 12 across.
        public int CoversWide { get; set; } = 12;

        // Visual transition effect played when a tile's cover is swapped.
        public TransitionEffect Effect { get; set; } = TransitionEffect.Flip;

        // Duration of that transition in ms. Ignored when Effect = Blink. Clamped to [1000, 10000].
        public int TransitionDurationMs { get; set; } = 3000;

        // Quiet gap between transitions in ms. Minimum 1000.
        // Total cycle for a tile swap = GapBetweenTransitionsMs + (Effect == Blink ? 0 : TransitionDurationMs).
        public int GapBetweenTransitionsMs { get; set; } = 1000;

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

        // Stream Deck lock-screen display, drawn by the Tray over raw USB HID when
        // Windows locks. Default keeps showing the now-playing cover.
        public LockDeckMode LockDeckMode { get; set; } = LockDeckMode.NowPlaying;
        public string LockDeckImagePath { get; set; } = string.Empty;

        // Default HKCU location. Both Load and Save also accept an explicit path so
        // tests can point at a throwaway subkey (SOFTWARE\Demo_ScreenSaver_Tests)
        // instead of snapshotting the operator's live key. Production callers use the
        // no-arg overloads and hit DefaultRegistryPath unchanged.
        private const string DefaultRegistryPath = "SOFTWARE\\Demo_ScreenSaver";

        public static ScreensaverSettings Load() => Load(DefaultRegistryPath);

        public static ScreensaverSettings Load(string registryPath)
        {
            var s = new ScreensaverSettings();
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    if (key != null)
                    {
                        s.AlbumCap = ReadInt(key, "AlbumCap", 0);
                        s.SwapIntervalMs = ReadInt(key, "SwapIntervalMs", 1000);
                        s.CoversWide = ReadInt(key, "CoversWide", 12);
                        s.Effect = (TransitionEffect)ReadInt(key, "TransitionEffect", (int)TransitionEffect.Flip);
                        s.TransitionDurationMs = ReadInt(key, "TransitionDurationMs", 3000);
                        s.GapBetweenTransitionsMs = ReadInt(key, "GapBetweenTransitionsMs", 1000);
                        s.WallpaperEnabled = ReadBool(key, "WallpaperEnabled", false);
                        s.WallpaperIntervalMinutes = ReadInt(key, "WallpaperIntervalMinutes", 5);
                        s.LockScreenEnabled = ReadBool(key, "LockScreenEnabled", false);
                        s.LockScreenIntervalMinutes = ReadInt(key, "LockScreenIntervalMinutes", 60);
                        s.DiscordAppId = (key.GetValue("DiscordAppId") as string) ?? string.Empty;
                        s.LockDeckMode = (LockDeckMode)ReadInt(key, "LockDeckMode", (int)LockDeckMode.NowPlaying);
                        s.LockDeckImagePath = (key.GetValue("LockDeckImagePath") as string) ?? string.Empty;
                        // Clamp on read, exactly as the old inline Math.Max/Min did. Save()
                        // writes raw, so the round-trip only holds for in-range inputs.
                        Clamp(s);
                    }
                }
            }
            catch { /* defaults are fine */ }
            return s;
        }

        /// <summary>
        /// Clamps the numeric settings to their documented bounds. Pure (no registry),
        /// so the clamp contract can be tested without touching HKCU. Load() applies it
        /// after reading the raw values, reproducing the old inline clamps exactly.
        /// Returns the same instance for convenience.
        /// </summary>
        public static ScreensaverSettings Clamp(ScreensaverSettings s)
        {
            if (s == null) return null;
            s.SwapIntervalMs = Math.Max(100, s.SwapIntervalMs);
            s.CoversWide = Math.Max(0, s.CoversWide);
            s.Effect = (TransitionEffect)Math.Max(0, Math.Min(3, (int)s.Effect));
            s.TransitionDurationMs = Math.Max(1000, Math.Min(10000, s.TransitionDurationMs));
            s.GapBetweenTransitionsMs = Math.Max(1000, s.GapBetweenTransitionsMs);
            s.WallpaperIntervalMinutes = Math.Max(1, s.WallpaperIntervalMinutes);
            s.LockScreenIntervalMinutes = Math.Max(5, s.LockScreenIntervalMinutes);
            s.LockDeckMode = (LockDeckMode)Math.Max(0, Math.Min(3, (int)s.LockDeckMode));
            return s;
        }

        public void Save() => Save(DefaultRegistryPath);

        public void Save(string registryPath)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(registryPath))
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
                    key.SetValue("LockDeckMode", (int)LockDeckMode, RegistryValueKind.DWord);
                    key.SetValue("LockDeckImagePath", LockDeckImagePath ?? string.Empty);
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
