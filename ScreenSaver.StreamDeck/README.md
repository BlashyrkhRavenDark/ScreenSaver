# ScreenSaver Stream Deck Plugin

A Stream Deck plugin that paints the currently-playing track's album cover across **one, four, or nine** keys, with every key configurable to do its own thing on press: play/pause, next, previous, stop, volume +/-, or mute.

Built on the same `NowPlayingMonitor` the screensaver itself uses, listening to Windows SMTC — so it works with anything that publishes a media session (Spotify, Apple Music, Foobar, VLC, browsers, iTunes-on-Windows).

## What it does

- **1x1 grid** — single key shows the current cover.
- **2x2 grid** — same cover spread across 4 keys.
- **3x3 grid** — same cover spread across 9 keys.

When playback is **paused or stopped**, the last album cover stays on the deck — it doesn't blank back to the key icon — so the artwork remains until a new track actually starts.

For each key in the grid, the property inspector lets you pick:

| Action | Behavior |
|---|---|
| `PlayPause` | Toggle play/pause on the active SMTC session; falls back to the global `VK_MEDIA_PLAY_PAUSE` key if the session refuses. |
| `Next` | `TrySkipNextAsync` then fallback to `VK_MEDIA_NEXT_TRACK`. |
| `Previous` | `TrySkipPreviousAsync` then fallback to `VK_MEDIA_PREV_TRACK`. |
| `Stop` | `TryStopAsync` then fallback to `VK_MEDIA_STOP`. |
| `VolumeUp` / `VolumeDown` / `Mute` | Direct global media key (`VK_VOLUME_UP/DOWN/MUTE`). |
| `None` | Visual tile only; press does nothing. |

The fallback path matters because some apps (notably some browser tabs and iTunes-on-Windows) publish metadata to SMTC but reject session-level `TrySkipNext`. The global media key works universally.

There's also a **Show action icon** checkbox per key. On (default) stamps a small badge in the lower-left corner hinting at the key's on-press action; off lets the cover artwork fill the entire key with nothing overlaid.

## Setup walkthrough for a 3x3 cover wall

1. Build and install (below).
2. Open Stream Deck, drag **Cover Tile** onto **9 keys** in a 3x3 arrangement.
3. For each key, click it and in the property inspector set:
   - **Grid size**: `3 x 3 (9 keys)`
   - **Row** / **Column**: 0/0 for top-left, 0/1 for top-middle, ... 2/2 for bottom-right.
   - **On press**: whichever action you want for that key (e.g. top-left = `VolumeUp`, top-right = `Mute`, middle = `PlayPause`, bottom-left = `Previous`, bottom-right = `Next`, bottom-middle = `VolumeDown`, etc).
4. Play something. The album cover spans all 9 keys; each key still does its own thing.

The plugin computes a single shared SMTC subscription per process, so 9 tiles cost one monitor instance.

## What you need (one-time setup)

| Tool | Purpose | Where to get it |
|---|---|---|
| Stream Deck app | Hosts plugins; required to use | https://www.elgato.com/downloads |
| .NET 10 SDK | Build the plugin | https://dotnet.microsoft.com/download |
| DistributionTool | Package as `.streamDeckPlugin` for distribution (optional during development) | Bundled with the Stream Deck SDK at https://docs.elgato.com/sdk/ |

The `streamdeck-tools` NuGet package (BarRaider) is pulled in automatically by the csproj. Source/docs: https://github.com/BarRaider/streamdeck-tools.

## Building

```powershell
dotnet publish -c Release -r win-x64 --self-contained
```

This produces a self-contained executable at:

```
bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\com.blashyrkh.screensaver.exe
```

## Installing the plugin for development

Stream Deck plugins live in `%APPDATA%\Elgato\StreamDeck\Plugins\<UUID>.sdPlugin\`.

```powershell
$pluginDir = "$env:APPDATA\Elgato\StreamDeck\Plugins\com.blashyrkh.screensaver.sdPlugin"
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item -Recurse -Force sdPlugin\* $pluginDir
Copy-Item -Recurse -Force bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\* $pluginDir
```

Then restart the Stream Deck app (right-click tray icon → **Quit**, relaunch). The **Cover Tile** action shows up under the **ScreenSaver** category.

## Packaging for distribution

```powershell
DistributionTool.exe -b -i sdPlugin -o .
```

That produces `com.blashyrkh.screensaver.streamDeckPlugin` — a single file you can double-click on any machine with Stream Deck installed.

## Publishing to Elgato Marketplace

Distribution and review go through **Maker Console** (https://maker.elgato.com). The modern toolchain is the **Stream Deck CLI** (`npm i -g @elgato/cli`, command `streamdeck`), which supersedes the old `DistributionTool.exe`.

1. **Register as a Maker.** Sign in at https://maker.elgato.com with your Elgato account and create a Maker organization — the org name becomes your public author name.
2. **Make the manifest submission-ready** (`sdPlugin/manifest.json`):
   - `UUID` reverse-DNS, author + plugin name, unchangeable after publish — `com.blashyrkh.screensaver` is fine. Action UUIDs must be prefixed by it (they are).
   - `Version` numeric `x.y.z[.b]` (e.g. `1.0.0`).
   - `Name`, `Description`, `Category`, `URL` (homepage/README) all accurate.
3. **Provide Marketplace art:**
   - Plugin icon PNG **256×256** + **512×512** (`@2x`).
   - Category/action icons monochrome white (`#FFFFFF`) on transparent, SVG preferred (28×28/56×56 category, 20×20/40×40 action).
   - At least **one preview image** in a `previews/` folder (a real screenshot of the cover wall on a deck sells it).
4. **Validate:** `streamdeck validate com.blashyrkh.screensaver.sdPlugin` — fix every error/warning (schema, file layout, icon sizes).
5. **Package:** `streamdeck pack com.blashyrkh.screensaver.sdPlugin` → `.streamDeckPlugin` (re-runs validation).
6. **Test the packaged build** on a clean install; optionally get beta feedback in the Marketplace Makers Discord.
7. **Submit in Maker Console:** upload the package, fill in store listing (name, description, category, previews, pricing — free is fine), submit for review. Elgato may request changes; once approved it appears on the Marketplace and Maker Console tracks downloads/updates.

Guidelines worth pre-checking: 2–30 actions per plugin, property inspector auto-saves on change (ours does — no Save button), no donation/copyright text in the inspector, no static non-configurable actions. Refs: https://docs.elgato.com/streamdeck/sdk/v1/introduction/distribution/ and https://docs.elgato.com/guidelines/stream-deck/plugins/

## TODOs and placeholders

- **Multi-action button defaults.** Each tile defaults to `PlayPause`. If you'd rather have them default to "no action" so users explicitly opt in, change the `TileSettings` default in `CoverTileAction.cs`.
- **Marketplace icon sizes.** `sdPlugin\icons\` has the dev icons. Elgato Marketplace submission wants a 256×256 (+512×512 @2x) plugin icon and at least one preview image — see "Publishing to Elgato Marketplace" below.

## Code reuse note

`NowPlayingMonitor.cs` is **linked** from the main ScreenSaver project rather than copied (`<Compile Include="..\ScreenSaver\NowPlayingMonitor.cs" Link="..." />`). One source, two assemblies. If you refactor it on the screensaver side, the plugin gets the change for free.
