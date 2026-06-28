using AlbumCoverFinder;

namespace ScreenSaver.Tests
{
    /// <summary>
    /// Boundary 5 (part): AlbumMetadata.Decade buckets a year down to its decade,
    /// and reports 0 when the year is unknown.
    /// </summary>
    public class AlbumMetadataTests
    {
        [Theory]
        [InlineData(1985, 1980)]
        [InlineData(1980, 1980)] // exact decade start stays put
        [InlineData(1989, 1980)] // decade end rounds down
        [InlineData(1990, 1990)]
        [InlineData(1999, 1990)]
        [InlineData(2000, 2000)]
        [InlineData(2007, 2000)]
        public void Decade_bucketsTheYearDownToItsDecade(int year, int expected)
        {
            Assert.Equal(expected, new AlbumMetadata { Year = year }.Decade);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Decade_isZero_whenYearIsUnknown(int year)
        {
            Assert.Equal(0, new AlbumMetadata { Year = year }.Decade);
        }
    }
}
