using AlbumCoverFinder;

namespace ScreenSaver.Tests
{
    /// <summary>
    /// Boundary 1: the cover-filter decision. CoverFilter.Matches is pure, the prime
    /// unit target. Genre is a case-insensitive substring; a year bound constrains
    /// only when both the bound and meta.Year are > 0 (unknown-year covers always
    /// pass). The == bound cases pin the &lt; / &gt; comparisons against off-by-one
    /// mutants.
    /// </summary>
    public class CoverFilterTests
    {
        private static AlbumMetadata Meta(string genre = "", int year = 0) =>
            new AlbumMetadata { Genre = genre, Year = year };

        // ---- IsEmpty ----

        [Fact]
        public void IsEmpty_isTrue_forADefaultFilter()
        {
            Assert.True(new CoverFilter().IsEmpty);
        }

        [Theory]
        [InlineData("rock", 0, 0)]
        [InlineData("", 1990, 0)]
        [InlineData("", 0, 2000)]
        public void IsEmpty_isFalse_whenAnyCriterionIsSet(string genre, int yearMin, int yearMax)
        {
            var f = new CoverFilter { Genre = genre, YearMin = yearMin, YearMax = yearMax };
            Assert.False(f.IsEmpty);
        }

        [Fact]
        public void IsEmpty_treatsWhitespaceGenreAndNonPositiveYearsAsEmpty()
        {
            var f = new CoverFilter { Genre = "   ", YearMin = 0, YearMax = -5 };
            Assert.True(f.IsEmpty);
        }

        // ---- Empty filter passes everything ----

        [Fact]
        public void Matches_emptyFilter_passesAnyMetadata()
        {
            Assert.True(new CoverFilter().Matches(Meta("Jazz", 1959)));
        }

        [Fact]
        public void Matches_nullMetadata_isTreatedAsEmptyAndPassesAnEmptyFilter()
        {
            Assert.True(new CoverFilter().Matches(null));
        }

        // ---- Genre: case-insensitive substring ----

        [Theory]
        [InlineData("rock", "Rock")]
        [InlineData("rock", "ROCK")]
        [InlineData("rock", "Classic Rock")]
        [InlineData("ock", "Rock")]          // substring, not equality
        [InlineData("Rock", "progressive rock")]
        public void Matches_genreSubstring_isCaseInsensitive(string filterGenre, string metaGenre)
        {
            var f = new CoverFilter { Genre = filterGenre };
            Assert.True(f.Matches(Meta(metaGenre, 1990)));
        }

        [Theory]
        [InlineData("rock", "Jazz")]
        [InlineData("rock", "")]
        public void Matches_genreMismatchOrMissing_failsWhenGenreFilterIsSet(string filterGenre, string metaGenre)
        {
            var f = new CoverFilter { Genre = filterGenre };
            Assert.False(f.Matches(Meta(metaGenre, 1990)));
        }

        // ---- Year lower bound (only constrains when meta.Year > 0) ----

        [Fact]
        public void Matches_yearMin_failsBelowTheBound()
        {
            var f = new CoverFilter { YearMin = 1990 };
            Assert.False(f.Matches(Meta(year: 1989)));
        }

        [Fact]
        public void Matches_yearMin_passesExactlyAtTheBound()
        {
            // Pins meta.Year < YearMin (a < to <= mutant would reject 1990 here).
            var f = new CoverFilter { YearMin = 1990 };
            Assert.True(f.Matches(Meta(year: 1990)));
        }

        [Fact]
        public void Matches_yearMin_passesAboveTheBound()
        {
            var f = new CoverFilter { YearMin = 1990 };
            Assert.True(f.Matches(Meta(year: 1991)));
        }

        // ---- Year upper bound ----

        [Fact]
        public void Matches_yearMax_failsAboveTheBound()
        {
            var f = new CoverFilter { YearMax = 2000 };
            Assert.False(f.Matches(Meta(year: 2001)));
        }

        [Fact]
        public void Matches_yearMax_passesExactlyAtTheBound()
        {
            // Pins meta.Year > YearMax (a > to >= mutant would reject 2000 here).
            var f = new CoverFilter { YearMax = 2000 };
            Assert.True(f.Matches(Meta(year: 2000)));
        }

        // ---- Unknown-year pass-through ----

        [Fact]
        public void Matches_unknownYear_passesAnyYearBound()
        {
            // Documented: hiding unknown-year covers would empty the mosaic on
            // legacy caches, so Year == 0 ignores both bounds.
            var f = new CoverFilter { YearMin = 1990, YearMax = 2000 };
            Assert.True(f.Matches(Meta(year: 0)));
        }

        // ---- Both bounds + genre combined ----

        [Theory]
        [InlineData(1985, false)]
        [InlineData(1990, true)]
        [InlineData(1995, true)]
        [InlineData(2000, true)]
        [InlineData(2005, false)]
        public void Matches_yearRange_includesTheEndpoints(int year, bool expected)
        {
            var f = new CoverFilter { YearMin = 1990, YearMax = 2000 };
            Assert.Equal(expected, f.Matches(Meta("Rock", year)));
        }

        [Fact]
        public void Matches_genreAndYear_bothMustPass()
        {
            var f = new CoverFilter { Genre = "rock", YearMin = 1990, YearMax = 2000 };
            Assert.True(f.Matches(Meta("Classic Rock", 1995)));
            Assert.False(f.Matches(Meta("Jazz", 1995)));    // wrong genre
            Assert.False(f.Matches(Meta("Classic Rock", 1980))); // out of range
        }
    }
}
