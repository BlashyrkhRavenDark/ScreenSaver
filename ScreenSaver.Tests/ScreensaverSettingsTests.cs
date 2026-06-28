using AlbumCoverFinder;

namespace ScreenSaver.Tests
{
    /// <summary>
    /// Boundary 3: settings persistence, pinned as TWO separate contracts because
    /// Save() writes raw values while Load() clamps on read:
    ///   (a) an in-range round-trip, Load(Save(x)) == x for x drawn from valid ranges;
    ///   (b) a clamp contract, an out-of-range value lands at the documented bound.
    /// (a) and the boundary-crossing case use the injectable RegistryPath pointed at a
    /// throwaway subkey; (b) drives the extracted pure Clamp helper with no registry.
    /// </summary>
    public class ScreensaverSettingsTests
    {
        // ---- (a) In-range round-trip: Load(Save(x)) == x ----

        [Fact]
        public void Save_thenLoad_roundTripsAllFields_whenEveryValueIsInRange()
        {
            using var key = new TempRegistryKey();
            var original = new ScreensaverSettings
            {
                AlbumCap = 50,
                CoversWide = 8,
                Effect = TransitionEffect.Merge,            // 2, in [0,3]
                TransitionDurationMs = 5000,                // in [1000,10000]
                GapBetweenTransitionsMs = 2000,             // >= 1000
                SwapIntervalMs = 1500,                      // >= 100
                WallpaperEnabled = true,
                WallpaperIntervalMinutes = 10,             // >= 1
                LockScreenEnabled = true,
                LockScreenIntervalMinutes = 30,            // >= 5
                DiscordAppId = "1234567890",
                LockDeckMode = LockDeckMode.Picture,        // 1, in [0,3]
                LockDeckImagePath = @"C:\covers\lock.png",
            };

            original.Save(key.Path);
            var loaded = ScreensaverSettings.Load(key.Path);

            Assert.Equal(original.AlbumCap, loaded.AlbumCap);
            Assert.Equal(original.CoversWide, loaded.CoversWide);
            Assert.Equal(original.Effect, loaded.Effect);
            Assert.Equal(original.TransitionDurationMs, loaded.TransitionDurationMs);
            Assert.Equal(original.GapBetweenTransitionsMs, loaded.GapBetweenTransitionsMs);
            Assert.Equal(original.SwapIntervalMs, loaded.SwapIntervalMs);
            Assert.Equal(original.WallpaperEnabled, loaded.WallpaperEnabled);
            Assert.Equal(original.WallpaperIntervalMinutes, loaded.WallpaperIntervalMinutes);
            Assert.Equal(original.LockScreenEnabled, loaded.LockScreenEnabled);
            Assert.Equal(original.LockScreenIntervalMinutes, loaded.LockScreenIntervalMinutes);
            Assert.Equal(original.DiscordAppId, loaded.DiscordAppId);
            Assert.Equal(original.LockDeckMode, loaded.LockDeckMode);
            Assert.Equal(original.LockDeckImagePath, loaded.LockDeckImagePath);
        }

        [Fact]
        public void Load_fromAnAbsentKey_returnsTheBuiltInDefaults()
        {
            using var key = new TempRegistryKey(); // never written, so the subkey is absent
            var s = ScreensaverSettings.Load(key.Path);

            Assert.Equal(12, s.CoversWide);
            Assert.Equal(TransitionEffect.Flip, s.Effect);
            Assert.Equal(3000, s.TransitionDurationMs);
            Assert.Equal(1000, s.GapBetweenTransitionsMs);
            Assert.Equal(LockDeckMode.NowPlaying, s.LockDeckMode);
        }

        // ---- (b) Clamp contract via the pure helper (no registry) ----

        [Fact]
        public void Clamp_pullsOutOfRangeValuesUpToTheirLowerBound()
        {
            var clamped = ScreensaverSettings.Clamp(new ScreensaverSettings
            {
                TransitionDurationMs = 500,                 // below 1000
                Effect = (TransitionEffect)9,               // above 3
                GapBetweenTransitionsMs = 200,              // below 1000
                CoversWide = -5,                            // below 0
                SwapIntervalMs = 10,                        // below 100
                WallpaperIntervalMinutes = 0,               // below 1
                LockScreenIntervalMinutes = 1,              // below 5
                LockDeckMode = (LockDeckMode)7,             // above 3
            });

            Assert.Equal(1000, clamped.TransitionDurationMs);
            Assert.Equal((TransitionEffect)3, clamped.Effect);
            Assert.Equal(1000, clamped.GapBetweenTransitionsMs);
            Assert.Equal(0, clamped.CoversWide);
            Assert.Equal(100, clamped.SwapIntervalMs);
            Assert.Equal(1, clamped.WallpaperIntervalMinutes);
            Assert.Equal(5, clamped.LockScreenIntervalMinutes);
            Assert.Equal((LockDeckMode)3, clamped.LockDeckMode);
        }

        [Fact]
        public void Clamp_pushesOverLargeValuesDownToTheirUpperBound()
        {
            var clamped = ScreensaverSettings.Clamp(new ScreensaverSettings
            {
                TransitionDurationMs = 99999,               // above 10000
                Effect = (TransitionEffect)42,              // above 3
                LockDeckMode = (LockDeckMode)42,            // above 3
            });

            Assert.Equal(10000, clamped.TransitionDurationMs);
            Assert.Equal((TransitionEffect)3, clamped.Effect);
            Assert.Equal((LockDeckMode)3, clamped.LockDeckMode);
        }

        [Fact]
        public void Clamp_leavesInRangeAndUnclampedFieldsUntouched()
        {
            var s = new ScreensaverSettings
            {
                AlbumCap = -3,                              // AlbumCap has no clamp
                TransitionDurationMs = 4000,                // already in range
                DiscordAppId = "abc",
                LockDeckImagePath = "x.png",
                WallpaperEnabled = true,
            };

            var clamped = ScreensaverSettings.Clamp(s);

            Assert.Equal(-3, clamped.AlbumCap);
            Assert.Equal(4000, clamped.TransitionDurationMs);
            Assert.Equal("abc", clamped.DiscordAppId);
            Assert.Equal("x.png", clamped.LockDeckImagePath);
            Assert.True(clamped.WallpaperEnabled);
        }

        // ---- The clamp boundary: round-trip is NOT identity across it ----

        [Fact]
        public void Load_clampsRawOutOfRangeValuesWrittenBySave()
        {
            using var key = new TempRegistryKey();
            // Save writes these raw (unclamped); Load must pull them to the bounds, so
            // Load(Save(x)) != x here. This is exactly why the round-trip is split.
            new ScreensaverSettings
            {
                TransitionDurationMs = 500,
                Effect = (TransitionEffect)9,
                GapBetweenTransitionsMs = 200,
                CoversWide = -5,
            }.Save(key.Path);

            var loaded = ScreensaverSettings.Load(key.Path);

            Assert.Equal(1000, loaded.TransitionDurationMs);
            Assert.Equal((TransitionEffect)3, loaded.Effect);
            Assert.Equal(1000, loaded.GapBetweenTransitionsMs);
            Assert.Equal(0, loaded.CoversWide);
        }
    }
}
