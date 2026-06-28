using System;
using System.Drawing;
using System.IO;
using AlbumCoverFinder;

namespace ScreenSaver.Tests
{
    /// <summary>
    /// Boundary 4: the rendered mosaic GEOMETRY (tile / column / row math and the
    /// dest-rectangle grid), pinned via a golden master on the pure, extracted
    /// WallpaperRenderer.DescribeLayout. The expected text was produced by an
    /// independent oracle (Expected/generate-golden.ps1) implementing the same
    /// documented rule, human-approved once, and committed, so a match is a
    /// differential agreement rather than a self-snapshot. The non-deterministic
    /// pixels (random cover pick, GDI compositing) stay on the manual on-screen
    /// CopyFromScreen check and are deliberately NOT golden-mastered here.
    /// </summary>
    public class WallpaperLayoutTests
    {
        private static string Normalize(string s) =>
            s.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n');

        private static string ReadExpected(string fileName)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Expected", fileName);
            return Normalize(File.ReadAllText(path));
        }

        [Fact]
        public void DescribeLayout_4k_coversWide12_matchesGolden()
        {
            string expected = ReadExpected("wallpaper_layout_3840x2160_w12.txt");
            string actual = WallpaperRenderer.DescribeLayout(new Size(3840, 2160), 256, 12);
            Assert.Equal(expected, Normalize(actual));
        }

        [Fact]
        public void DescribeLayout_1080p_nativeTile256_matchesGolden()
        {
            string expected = ReadExpected("wallpaper_layout_1920x1080_w0_t256.txt");
            string actual = WallpaperRenderer.DescribeLayout(new Size(1920, 1080), 256, 0);
            Assert.Equal(expected, Normalize(actual));
        }

        // ---- Focused ComputeLayout edges (mutation targets the golden also covers) ----

        [Fact]
        public void ComputeLayout_coversWideFixesColumns_andDerivesTileFromWidth()
        {
            var layout = WallpaperRenderer.ComputeLayout(new Size(3840, 2160), 256, 12);
            Assert.Equal(12, layout.Cols);
            Assert.Equal(320, layout.Tile);   // 3840 / 12
            Assert.Equal(7, layout.Rows);     // ceil(2160 / 320)
        }

        [Fact]
        public void ComputeLayout_rowsCeilRound_soABottomRemainderAddsARow()
        {
            // height a hair over 4 tiles must round UP to 5 (the partial bottom row).
            Assert.Equal(4, WallpaperRenderer.ComputeLayout(new Size(256, 1024), 256, 0).Rows); // 1024 = 4*256 exactly
            Assert.Equal(5, WallpaperRenderer.ComputeLayout(new Size(256, 1025), 256, 0).Rows); // one px over -> 5
        }

        [Fact]
        public void ComputeLayout_tileNeverGoesBelow32px()
        {
            // 12 columns across a 100px canvas would give an 8px tile; floored to 32.
            var layout = WallpaperRenderer.ComputeLayout(new Size(100, 100), 256, 12);
            Assert.Equal(32, layout.Tile);
        }
    }
}
