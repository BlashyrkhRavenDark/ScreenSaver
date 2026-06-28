using AlbumCoverFinder;

namespace ScreenSaver.Tests
{
    /// <summary>
    /// Boundary 2: blocklist decisions. CoverBlocklist takes a cacheDir in its ctor,
    /// so each test points it at a throwaway temp dir (no global state, no touching
    /// the operator's real %USERPROFILE%\ScreenSaverCovers). Keys are case-insensitive;
    /// Block returns false on a duplicate; the blocklist.txt persists across instances.
    /// </summary>
    public class CoverBlocklistTests
    {
        [Fact]
        public void NewBlocklist_isEmpty()
        {
            using var dir = new TempDir();
            var bl = new CoverBlocklist(dir.Path);
            Assert.Equal(0, bl.Count);
            Assert.False(bl.IsBlocked("anything"));
        }

        [Fact]
        public void Block_thenIsBlocked_isTrue_andCaseInsensitive()
        {
            using var dir = new TempDir();
            var bl = new CoverBlocklist(dir.Path);

            Assert.True(bl.Block("Pink Floyd|The Wall"));
            Assert.True(bl.IsBlocked("Pink Floyd|The Wall"));
            Assert.True(bl.IsBlocked("pink floyd|the wall")); // case-insensitive
            Assert.Equal(1, bl.Count);
        }

        [Fact]
        public void Block_duplicate_returnsFalse_andDoesNotGrow()
        {
            using var dir = new TempDir();
            var bl = new CoverBlocklist(dir.Path);

            Assert.True(bl.Block("A|B"));
            Assert.False(bl.Block("A|B"));        // exact duplicate
            Assert.False(bl.Block("a|b"));        // case-insensitive duplicate
            Assert.Equal(1, bl.Count);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Block_nullOrEmpty_returnsFalse(string? key)
        {
            using var dir = new TempDir();
            var bl = new CoverBlocklist(dir.Path);

            Assert.False(bl.Block(key));
            Assert.Equal(0, bl.Count);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void IsBlocked_nullOrEmpty_returnsFalse(string? key)
        {
            using var dir = new TempDir();
            var bl = new CoverBlocklist(dir.Path);

            Assert.False(bl.IsBlocked(key));
        }

        [Fact]
        public void Unblock_removesTheKey_andReturnsWhetherItWasPresent()
        {
            using var dir = new TempDir();
            var bl = new CoverBlocklist(dir.Path);
            bl.Block("A|B");

            Assert.True(bl.Unblock("a|b"));   // case-insensitive removal
            Assert.False(bl.IsBlocked("A|B"));
            Assert.Equal(0, bl.Count);
            Assert.False(bl.Unblock("A|B"));  // already gone
        }

        [Fact]
        public void Clear_emptiesTheList()
        {
            using var dir = new TempDir();
            var bl = new CoverBlocklist(dir.Path);
            bl.Block("A|B");
            bl.Block("C|D");

            bl.Clear();

            Assert.Equal(0, bl.Count);
            Assert.False(bl.IsBlocked("A|B"));
        }

        [Fact]
        public void GetAll_returnsKeysSortedCaseInsensitively()
        {
            using var dir = new TempDir();
            var bl = new CoverBlocklist(dir.Path);
            bl.Block("zeta|z");
            bl.Block("Alpha|a");
            bl.Block("mike|m");

            Assert.Equal(new[] { "Alpha|a", "mike|m", "zeta|z" }, bl.GetAll());
        }

        // ---- Persistence across instances (the blocklist.txt round-trip) ----

        [Fact]
        public void Block_persists_toASecondInstanceOnTheSameDir()
        {
            using var dir = new TempDir();
            new CoverBlocklist(dir.Path).Block("Artist|Album");

            var reopened = new CoverBlocklist(dir.Path);
            Assert.True(reopened.IsBlocked("Artist|Album"));
            Assert.Equal(1, reopened.Count);
        }

        [Fact]
        public void Unblock_persists_toASecondInstance()
        {
            using var dir = new TempDir();
            var first = new CoverBlocklist(dir.Path);
            first.Block("Keep|1");
            first.Block("Drop|2");
            first.Unblock("Drop|2");

            var reopened = new CoverBlocklist(dir.Path);
            Assert.True(reopened.IsBlocked("Keep|1"));
            Assert.False(reopened.IsBlocked("Drop|2"));
        }

        [Fact]
        public void Clear_persists_toASecondInstance()
        {
            using var dir = new TempDir();
            var first = new CoverBlocklist(dir.Path);
            first.Block("A|B");
            first.Clear();

            Assert.Equal(0, new CoverBlocklist(dir.Path).Count);
        }
    }
}
