# ADR-0001: Rebuild on .NET 10 with TagLib# and a per-file PNG cover cache

- Status: Accepted
- Date: 2026-05-27 (recorded in the Logbook 2026-06-26)
- Deciders: Ant

## Context

The original ScreenSaver was a .NET Framework 4.7.2 app. It read ID3 tags through an external `id3lib` solution (ID3-only, so non-MP3 formats were second-class) and persisted covers by serializing a `Dictionary<string,Image>` with [BinaryFormatter](https://learn.microsoft.com/dotnet/standard/serialization/binaryformatter-security-guide) to a single `.bin` file in the user profile. BinaryFormatter was removed in .NET 9 for security reasons, so the persistence format could not survive a runtime upgrade. A 2026-05-27 design review pushed the project toward an [SMTC](https://learn.microsoft.com/uwp/api/windows.media.control)-aware, multi-format rewrite shared by several front-ends.

## Decision

We will target [.NET 10](https://dotnet.microsoft.com/download), read every tag and embedded cover through [TagLib#](https://github.com/mono/taglib-sharp), and drop both `id3lib` and BinaryFormatter. Each unique `artist+album` cover is resized to 384x384 and stored as a numbered PNG (`N.png`) with a small `N.json` sidecar under `%USERPROFILE%\ScreenSaverCovers\`, indexed by `index.txt` (line N maps to `N.png`). Read-only consumers open the same folder and decode lazily; only the Finder loads eagerly because it edits and re-saves. See `AlbumCoverFinder/CoverMgr.cs` and `AlbumCoverFinder/AlbumMetadata.cs`.

## Considered options

- **Keep .NET Framework with id3lib and BinaryFormatter.** Rejected: BinaryFormatter is gone in .NET 9+, and id3lib limited tag reading to ID3/MP3.
- **Re-serialize the cover dictionary with a different serializer** (System.Text.Json of base64, or a custom binary blob). Rejected: still one opaque file, no per-cover edit or delete, and readers must load the whole blob instead of decoding only the covers they show.
- **A folder of plain per-file PNGs plus JSON sidecars (chosen).** Human-debuggable, deletable per cover, needs no serialization library, and supports lazy decode.

## Consequences

- Any format TagLib# supports works (MP3, FLAC, M4A, AAC, OGG, OPUS, WMA, WAV, AIFF, APE), the cache is inspectable on disk, and there is no deserialization-of-untrusted-data risk.
- Deduplication stays cheap: the scanner hashes original artwork bytes (SHA256) only when a second cover shares the same byte length, so byte-identical art (compilations, re-releases) is skipped without hashing everything; the length and hash ride in the JSON sidecar.
- Negative: anyone upgrading from a pre-2026 build must delete the old `.bin` and rescan, since there is no automatic migration. The per-file layout also means many small files instead of one, which is more inodes and more open/close calls during a full scan.

## References

- Code: `AlbumCoverFinder/CoverMgr.cs`, `AlbumCoverFinder/AlbumMetadata.cs`, `AlbumCoverFinder/CoverFilter.cs`
- Recorded in the Journal of `docs/index.html` (rev 1, 2026-06-26).
