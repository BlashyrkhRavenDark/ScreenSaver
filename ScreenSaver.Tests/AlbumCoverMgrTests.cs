using AlbumCoverFinder;

namespace ScreenSaver.Tests
{
    /// <summary>
    /// Boundary 5 (part): AlbumCoverMgr.BuildKey is the identity used everywhere a
    /// cover is looked up. It is a plain artist+album concatenation with null treated
    /// as empty; the whole cache, blocklist, and sibling logic depend on this exact
    /// shape, so it is pinned directly.
    /// </summary>
    public class AlbumCoverMgrTests
    {
        [Fact]
        public void BuildKey_concatenatesArtistThenAlbum()
        {
            Assert.Equal("Pink FloydThe Wall", AlbumCoverMgr.BuildKey("Pink Floyd", "The Wall"));
        }

        [Fact]
        public void BuildKey_treatsNullArtistAsEmpty()
        {
            Assert.Equal("The Wall", AlbumCoverMgr.BuildKey(null, "The Wall"));
        }

        [Fact]
        public void BuildKey_treatsNullAlbumAsEmpty()
        {
            Assert.Equal("Pink Floyd", AlbumCoverMgr.BuildKey("Pink Floyd", null));
        }

        [Fact]
        public void BuildKey_bothNull_isEmptyString()
        {
            Assert.Equal(string.Empty, AlbumCoverMgr.BuildKey(null, null));
        }

        [Fact]
        public void BuildKey_bothEmpty_isEmptyString()
        {
            Assert.Equal(string.Empty, AlbumCoverMgr.BuildKey("", ""));
        }
    }
}
