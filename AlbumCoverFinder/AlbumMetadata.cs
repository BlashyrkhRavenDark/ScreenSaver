using System;

namespace AlbumCoverFinder
{
    /// <summary>
    /// Lightweight metadata captured per album cover during the TagLib# scan.
    /// Persisted as a small JSON sidecar alongside each PNG in the cache so the
    /// screensaver can filter, sort by year, and overlay info without re-reading
    /// the source audio files.
    /// </summary>
    public class AlbumMetadata
    {
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public int Year { get; set; }
        public string Genre { get; set; } = string.Empty;

        /// <summary>
        /// Byte length of the ORIGINAL embedded artwork (before resize). Used as
        /// the cheap first-pass gate in dedup: if two covers don't have the same
        /// byte count, they can't be byte-identical, so we skip hashing entirely.
        /// </summary>
        public int CoverByteLength { get; set; }

        /// <summary>
        /// SHA256 of the original embedded artwork bytes, hex-encoded. Only
        /// computed when a byte-length collision is detected during scan, OR
        /// when this is the first cover at that length. Empty for cache entries
        /// written before dedup support; those are excluded from the dedup pool.
        /// </summary>
        public string CoverHash { get; set; } = string.Empty;

        /// <summary>
        /// The decade bucket as a plain integer (e.g. 1985 -> 1980). Zero when no
        /// year is known. Useful for the #7 filter UI.
        /// </summary>
        public int Decade
        {
            get { return Year > 0 ? (Year / 10) * 10 : 0; }
        }
    }
}
