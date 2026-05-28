# Feature ideas

Brainstorm of things that could be cool. Ranked by my (subjective) ratio of "joy delivered / work to ship." Not promises — pick what excites you.

## Tier S — high leverage, modest effort

### 1. Color-extracted ambient lighting
**What:** Sample dominant colors from the currently-playing cover and broadcast them to ambient hardware (Govee, Hue, Razer Chroma, Wiz, OpenRGB). The room glows in the color of whatever album is on.
**How:** A `ColorPalette.cs` that downsamples a cover to ~16x16 and runs a tiny K-means on RGB. Push to:
- **Govee LAN API** (https://app-h5.govee.com/user-manual/wlan-guide) — open HTTP, no cloud account needed.
- **Razer Chroma SDK** (https://developer.razer.com/works-with-chroma/rest-api/) — local REST.
- **OpenRGB** (https://openrgb.org/) — covers nearly every brand via a single SDK.
- **Hue** via [Q42.HueApi](https://github.com/Q42/HueApi) NuGet.

### 2. ~~Click-to-pin / metadata reveal~~ — **DONE**
Shift+click any tile in the screensaver to pin it. The clicked cover enlarges as the focal tile, with artist / album / year / genre overlaid below. Plain click (or any key, mouse move) still exits. Click-while-pinned unpins instead of exiting.

### 3. ~~Live desktop-wallpaper mode~~ — **DONE**
`ScreenSaver.Tray` renders the mosaic to `%APPDATA%\ScreenSaver\wallpaper.bmp` every 5 minutes and sets it as the Windows wallpaper via `SystemParametersInfo(SPI_SETDESKWALLPAPER)`. Toggle the auto-refresh from the tray menu; double-click tray icon to refresh now.

### 4. ~~Discord Rich Presence integration~~ — **DONE**
`ScreenSaver.Tray` subscribes to SMTC and pushes "Listening to {title} - by {artist}" to your Discord profile via the `DiscordRichPresence` NuGet. Set your Discord Application ID at `HKCU\SOFTWARE\Demo_ScreenSaver\DiscordAppId` and upload a "music" image asset to your Discord app for the icon.

## Tier A — clearly useful, medium effort

### 5. ~~Tap-to-play / pause on the Stream Deck key~~ — **DONE**
Implemented as part of the configurable `CoverTileAction`. SMTC session is tried first, with a global media-key fallback for apps that publish metadata but refuse session controls.

### 6. ~~Multi-action Stream Deck plugin (cover wall)~~ — **DONE**
`CoverTileAction` now spans 1x1, 2x2, or 3x3 keys with one cover, and each key can be individually configured for play/pause, next, previous, stop, volume +/-, or mute via the property inspector.

### 7. ~~Genre / decade filtering (local only)~~ — **DONE**
AlbumCoverFinder gained a Filter group box: genre dropdown (populated from your library's distinct genres) + year-min / year-max numeric pickers. Settings persist to `HKCU\SOFTWARE\Demo_ScreenSaver`. The screensaver reads the filter on load and applies it to every cover pick (including the no-repeat picker). No Spotify; pure TagLib# year/genre data.

### 8. ~~Smart focal tile: "more from this artist"~~ — **DONE**
When SMTC reports a track, the focal tile shows the playing cover (large) + a small thumbnail next to it of a different album by the same artist (if present in the library). Caption reads "Also: {sibling album}".

## Tier B — cool but heavier

### 9. ~~LAN HTTP companion~~ — **DONE**
`ScreenSaver.Tray` hosts an HTTP listener on `localhost:9999`. Endpoints: `/now-playing.png`, `/now-playing.json`, `/random.png`, `/index.json`, `/cover/{n}.png`. Browser companion page at `/`. Loop-back only by default; one `netsh urlacl` command opens it to the LAN for Steam Deck / phone access.

### 10. Beat-reactive mosaic
**What:** While audio is playing, the mosaic pulses gently to the beat — tiles scale up 2-5% on transient peaks.
**How:** Capture WASAPI loopback (https://github.com/naudio/NAudio), FFT a small buffer per frame, drive a smoothed envelope into FadingPictureBox scale. Watch CPU.

### 11. ~~"Listening to this album right now? Highlight it"~~ — **DONE**
When SMTC reports a track, `RefreshHighlights()` scans the mosaic for a matching tile key and sets `FadingPictureBox.Highlighted = true`, which pulses a soft yellow border in `OnPaint`. Complements the focal-tile overlay rather than replacing it. Re-evaluated every tile swap.

### 12. Visualizer mode (full-screen, no mosaic)
**What:** Toggleable mode where the screensaver becomes a single huge album cover + audio visualizer instead of a mosaic. Different vibe for late-night listening.
**How:** Mode switch via a key press in non-preview mode (consumes a keypress instead of exiting). Render Niagara-style flow particles tinted with extracted palette colors.

## Tier C — fun side quests

### 13. ~~Web companion at `localhost:9999`~~ — **DONE**
HTML+JS frontend served at `/` by the tray's HttpCompanion. Now-Playing block at the top, mobile-friendly search across the full cover library, click any tile for a lightbox view with full metadata. Backed by `/index.json` and `/cover/{n}.png`.

### 14. Last.fm scrobble overlay
**What:** Focal tile shows "you've played this album N times this year."
**How:** Pull from Last.fm API (free), match on `artist+album`. Cache results.

### 15. Album-cover-of-the-day system tray icon
**What:** A tray icon that's a tiny rotation of your covers. Hover → see album. Click → open album in your music player of choice.
**How:** `NotifyIcon` with a custom rendered icon (16x16 / 32x32), updated every minute from the cover cache. Could fold into the existing `ScreenSaver.Tray`.

### 16. Cover database sharing
**What:** Export your `ScreenSaverCovers/` folder as a `.zip` you can give to a friend, and have AlbumCoverFinder import it.
**How:** Two buttons in AlbumCoverFinder: Export Zip / Import Zip. Trivial wrapper over `System.IO.Compression.ZipFile`.

### 17. ~~"Decade tour" auto-cycle~~ — **REMOVED**
Shipped briefly then deleted at the user's request. The 30-second pause it introduced in the regular swap cadence was confusing and the visual payoff didn't justify it.

### 18. ~~Lock screen / login screen mosaic~~ — **DONE**
`LockScreenRenderer` reuses the wallpaper draw pipeline, saves as JPEG, and calls `Windows.System.UserProfile.LockScreen.SetImageFileAsync` to swap the lock-screen image. Manual trigger from the tray menu (auto-cycle disabled by default to keep IO predictable).

---

## What's left

Of the original 18:

- **Done (11):** #2, #3, #4, #5, #6, #7, #8, #9, #11, #13, #18
- **Removed:** #17 (decade tour)
- **Open by user choice:** #1 (ambient lighting), #10 (beat-reactive), #12 (visualizer mode), #14 (Last.fm), #15 (tray cover-of-the-day), #16 (database sharing)

## Where to go next

- **#1** (ambient lighting) — biggest "wow per line." Govee LAN is the no-account path.
- **#10** (beat-reactive) — pairs naturally with #1 (palette + pulse).
- **#15** (tray cover-of-the-day) — easy add to the existing tray app.
- **#16** (zip share) — fastest of the remaining.

Tell me which (or several) and I'll build them.
