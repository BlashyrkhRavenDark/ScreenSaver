# ScreenSaver

A Windows screensaver that turns your music library into a living mosaic of album covers — with the currently-playing track surfacing as a focal tile via Windows SMTC, and an optional Stream Deck companion plugin that mirrors the same cover to a key.

Inspired by the old iTunes album-art screensaver on macOS.

Demo: https://www.youtube.com/watch?v=YUvwzdiCS0g

## What's in this repo

| Project | What it is |
|---|---|
| `ScreenSaver/` | The `.scr` itself. Centered mosaic, crossfading tile swaps, SMTC-driven focal tile, playing-album highlight, click-to-pin, sibling thumbnail, registry-driven filter. Targets `net10.0-windows10.0.19041.0`. |
| `AlbumCoverFinder/` | WinForms scanner + filter UI. Walks a folder, extracts cover art and metadata (artist, album, year, genre) from any TagLib#-supported audio format (MP3, FLAC, M4A, OGG, WMA, OPUS, …), and writes a PNG + JSON cache to `%USERPROFILE%\ScreenSaverCovers\`. Targets `net10.0-windows`. |
| `ScreenSaver.StreamDeck/` | Optional Stream Deck plugin: 1x1/2x2/3x3 cover-grid action with configurable per-key controls (play/pause, next, previous, stop, volume+/-, mute). See its own README. |
| `ScreenSaver.Tray/` | Optional tray companion: desktop wallpaper mosaic, Discord Rich Presence, LAN HTTP companion (`http://localhost:9999`), and lock-screen mosaic. See its own README. |

## Requirements

- Windows 10 build 19041 (May 2020) or newer — required for SMTC (`Windows.Media.Control`).
- .NET 10 SDK to build: https://dotnet.microsoft.com/download
- Visual Studio 2022 17.12+ or VS Code with the C# Dev Kit (any IDE that speaks `net10.0-windows10.0.19041.0`).

## How it works

1. **AlbumCoverFinder** scans a folder of audio files via [TagLib#](https://github.com/mono/taglib-sharp). For each unique `artist+album`, it resizes the embedded cover to 384×384 and writes it to `%USERPROFILE%\ScreenSaverCovers\<n>.png`. The `index.txt` file maps line N → `N.png`.
2. **ScreenSaver** (the `.scr`) launches one Form per monitor, lays out a centered grid of square tiles sized dynamically from the screen, and uses `FadingPictureBox` to crossfade one tile per second (excluding covers already on screen).
3. When something is playing — Spotify, Apple Music, Foobar, VLC, browsers, anything publishing SMTC — `NowPlayingMonitor` subscribes via `GlobalSystemMediaTransportControlsSessionManager` and the screensaver dims the mosaic and shows a large central focal tile with the active cover + track title + artist.
4. The optional **Stream Deck plugin** does the same SMTC listening in its own process and pushes the cover image (144×144 PNG, base64-encoded) to a Stream Deck key.

## Building

```powershell
dotnet restore ScreenSaver.sln
dotnet build   ScreenSaver.sln -c Release
```

Outputs:

- `ScreenSaver\bin\Release\net10.0-windows10.0.19041.0\ScreenSaver.exe`
- `AlbumCoverFinder\bin\Release\net10.0-windows\AlbumCoverFinder.exe`
- For the Stream Deck plugin: see `ScreenSaver.StreamDeck\README.md` (uses `dotnet publish` for a self-contained build).

## Installing as a Windows screensaver

1. Rename `ScreenSaver.exe` to `ScreenSaver.scr`.
2. Copy `ScreenSaver.scr` into `C:\Windows\System32\` (or right-click the file → **Install**).
3. Open Windows Settings → **Personalization → Lock screen → Screen saver**, pick **ScreenSaver**, click **Settings** to configure your music folder.
4. First-run only: the Settings dialog opens AlbumCoverFinder. Pick your music folder, click **Parse Folder for albums**, wait for the scan to finish.

## Configuration

- **Music folder**: pick any folder via AlbumCoverFinder. It scans recursively.
- **Genre / year filter**: at the bottom of AlbumCoverFinder, pick a genre and/or year range and click **Apply filter**. The screensaver, the wallpaper renderer, and the LAN HTTP companion all read the filter and show only matching covers. Click **Clear filter** to undo.
- **Swap interval**: optional registry override at `HKCU\SOFTWARE\Demo_ScreenSaver\SwapIntervalMs` (REG_DWORD or REG_SZ, value ≥ 100). Defaults to 1000 ms.
- **Discord App ID** (for the tray companion's Rich Presence): `HKCU\SOFTWARE\Demo_ScreenSaver\DiscordAppId` (REG_SZ).
- **Wipe cache**: click **Delete Backup File** in AlbumCoverFinder.

## Screensaver gestures

| Gesture | Result |
|---|---|
| Mouse move > 5 px | Exits the screensaver (standard). |
| Plain click on a tile | Exits. |
| Plain click on the form background | Exits. |
| **Shift + click on a tile** | Pins that tile: enlarges it as the focal cover with artist / album / year / genre overlay. |
| Click while pinned | Unpins (returns to mosaic). |
| Esc | Exits. |
| Any other key | Exits (or unpins, if pinned). |

## Troubleshooting

- **Mostly black tiles** — database is empty or small. Run AlbumCoverFinder.
- **Focal tile never appears** — nothing is currently playing through an app that publishes to SMTC. Spotify, AppleMusic Preview, Foobar2000, VLC, Edge, Chrome all do; some niche players don't.
- **Covers look slightly letterboxed** — covers that weren't perfectly square get drawn with letterboxing instead of distortion. Working as intended.
- **"BinaryFormatter" / "MissingMethodException" on startup** — you have a stale `%USERPROFILE%\ScreenSaverPictures.bin` from a pre-2026 version. Delete it and rescan.

## Architecture notes

- **Cache format**: `%USERPROFILE%\ScreenSaverCovers\<index.txt + N.png>`. Human-debuggable, deletable per cover, no serialization library required.
- **No `BinaryFormatter`**: removed in .NET 9+; the project is now clean of it.
- **SMTC monitor decoupled from cache**: `NowPlayingMonitor` takes a `Func<string, Image>` for cover lookups instead of a hard dep on `AlbumCoverMgr`, which lets the Stream Deck plugin reuse the same source file.
- **Single source of truth**: `NowPlayingMonitor.cs` lives in the ScreenSaver project; the Stream Deck plugin pulls it in via `<Compile Include="..\ScreenSaver\NowPlayingMonitor.cs" Link="..." />`.
- **DPI awareness**: app.manifest declares `PerMonitorV2` so the mosaic stays crisp across mixed-DPI multi-monitor setups.

## Notes for upgraders from pre-2026 versions

The cache format and target framework both changed. To upgrade cleanly:

1. Delete `%USERPROFILE%\ScreenSaverPictures.bin` (the old BinaryFormatter cache).
2. Build the new solution against .NET 10.
3. Run AlbumCoverFinder once to repopulate `%USERPROFILE%\ScreenSaverCovers\`.

The external `id3lib Solution\` dependency has been removed entirely — all tagging is now done by TagLib#.

## See also

- `ScreenSaver.StreamDeck\README.md` — Stream Deck plugin build and install
- `IDEAS.md` — feature ideas under consideration
