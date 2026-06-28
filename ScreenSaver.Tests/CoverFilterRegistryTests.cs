using AlbumCoverFinder;

namespace ScreenSaver.Tests
{
    /// <summary>
    /// Boundary 3 (CoverFilter half): SaveToRegistry / LoadFromRegistry round-trip
    /// through the injectable RegistryPath, pointed at a throwaway subkey. Unlike
    /// ScreensaverSettings, CoverFilter does not clamp on read, so the round-trip is
    /// a plain identity for any value (no separate clamp contract to split out).
    /// </summary>
    public class CoverFilterRegistryTests
    {
        [Fact]
        public void SaveToRegistry_thenLoadFromRegistry_roundTripsGenreAndYears()
        {
            using var key = new TempRegistryKey();
            var original = new CoverFilter { Genre = "Shoegaze", YearMin = 1988, YearMax = 1995 };

            original.SaveToRegistry(key.Path);
            var loaded = CoverFilter.LoadFromRegistry(key.Path);

            Assert.Equal(original.Genre, loaded.Genre);
            Assert.Equal(original.YearMin, loaded.YearMin);
            Assert.Equal(original.YearMax, loaded.YearMax);
        }

        [Fact]
        public void LoadFromRegistry_fromAnAbsentKey_returnsAnEmptyFilter()
        {
            using var key = new TempRegistryKey(); // never written
            var loaded = CoverFilter.LoadFromRegistry(key.Path);

            Assert.True(loaded.IsEmpty);
            Assert.Equal(string.Empty, loaded.Genre);
            Assert.Equal(0, loaded.YearMin);
            Assert.Equal(0, loaded.YearMax);
        }

        [Fact]
        public void SaveToRegistry_anEmptyFilter_roundTripsAsEmpty()
        {
            using var key = new TempRegistryKey();
            new CoverFilter().SaveToRegistry(key.Path);

            Assert.True(CoverFilter.LoadFromRegistry(key.Path).IsEmpty);
        }
    }
}
