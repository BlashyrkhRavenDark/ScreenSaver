# ScreenSaver

A four-part Windows suite that turns a local music library into a living mosaic of album covers, surfaces the currently-playing track as a focal tile, and optionally mirrors that cover to a desktop wallpaper, a lock screen, a web page, and a Stream Deck.

> Generated from `docs/index.html` (the living document). Do not hand-edit: regenerate with the `agents-md` agent whenever `docs/index.html` changes.

## Agent brief

- **Is**: a local-only (no cloud) Windows 10/11, .NET 10 desktop suite that tiles a local music library's album covers into a full-screen idle mosaic, floats the now-playing track as a focal cover, and mirrors that cover to desktop wallpaper, the lock screen, a LAN web page, and a Stream Deck. Four cooperating components plus a launcher stub; serves one operator on one machine.
- **Run / test**: build with the `build` skill, i.e. `dotnet build ScreenSaver.sln -c Release` then three publishes (StreamDeck self-contained `win-x64`, ScreenSaver framework-dependent, Launcher framework-dependent `win-x64`); run full-screen with `ScreenSaver.exe /s` (`/p <hwnd>` preview, `/c` settings). Tests: `dotnet test ScreenSaver.Tests` runs the xUnit harness (Floor tier) over the deterministic logic and the renderer geometry; the on-screen pixels stay on a `CopyFromScreen` capture (a black 4K frame is roughly 17 KB, a real mosaic is several MB) plus `%LOCALAPPDATA%\ScreenSaver\screensaver.log`.
- **Core paths**: `ScreenSaver.sln` = 5 projects: `ScreenSaver/` (the `.scr` plus the shared `NowPlayingMonitor.cs` plus owner-draw `ScreenSaverForm.cs` / `FadingPictureBox.cs`), `AlbumCoverFinder/` (scan + settings + `CoverMgr.cs`), `ScreenSaver.Tray/` (always-on, sole iTunes poller), `ScreenSaver.StreamDeck/` (deck plugin), `ScreenSaver.Launcher/` (the System32 picker stub). Settings live in `HKCU\SOFTWARE\Demo_ScreenSaver`; cover cache at `%USERPROFILE%\ScreenSaverCovers\`; IPC and installs under `%LOCALAPPDATA%\ScreenSaver\`; automation in `.claude/skills/{build,install}`.
- **Invariants (do not break)**: render owner-draw only, no child controls (they never composite on this .NET 10 runtime, which was the black-screen bug; `FadingTile` is a plain renderer, not a `Control`); exactly one iTunes COM poller (the Tray) that attaches only when `iTunes.exe` already runs, never launches it, releases COM and forces a GC each poll; IPC writes `nowplaying.png` before `nowplaying.json` (both temp-then-move) so a reader never renders a track behind; `NowPlayingMonitor.cs` stays one compile-linked source across the three assemblies, never copied; deploy stays a registry pointer (`SCRNSAVE.EXE`) plus a per-user app plus a tiny uncompressed System32 stub, never a self-contained `.scr` in System32 (AV packer flag); bin/obj stay redirected to `out\<project>\`, and the cache stays per-file PNG (no BinaryFormatter, removed in .NET 9).
- **Before changing rendering or the mosaic**: read the owner-draw "Rendering" section of `docs/index.html` and [ADR-0002](docs/adr/0002-owner-draw-no-child-controls.md); the full black-screen investigation is in `.claude/notes.md` (2026-05-29).
- **Before touching now-playing or iTunes**: read the "Now playing" section and the IPC deep-dive in `docs/index.html`, [ADR-0004](docs/adr/0004-one-itunes-poller-postmessage-dismiss.md), and `.claude/notes.md`; the PNG-before-JSON ordering and the iTunes COM hygiene are load-bearing.
- **Before changing deploy, the registry, or System32**: read the "Deploy" section of `docs/index.html`, [ADR-0003](docs/adr/0003-scrnsave-stub-deploy.md), and the `install` skill; the Windows picker only lists `.scr` files in System32, so `SCRNSAVE.EXE` must point at the stub when it is installed.
- **Related documents**: the four architecture decisions are numbered markdown files under [docs/adr/](docs/adr/) ([0001](docs/adr/0001-net10-taglib-png-cache.md) .NET 10 + PNG cache, [0002](docs/adr/0002-owner-draw-no-child-controls.md) owner-draw, [0003](docs/adr/0003-scrnsave-stub-deploy.md) deploy, [0004](docs/adr/0004-one-itunes-poller-postmessage-dismiss.md) iTunes + dismiss); they are the only other markdown in `docs/`. This page is the human source of truth, and the generated `AGENTS.md` at the repo root is the agent front door.

## Decisions

- [ADR-0001: Rebuild on .NET 10 with TagLib# and a per-file PNG cover cache](docs/adr/0001-net10-taglib-png-cache.md) (accepted)
- [ADR-0002: Owner-draw the screensaver, with no child controls](docs/adr/0002-owner-draw-no-child-controls.md) (accepted)
- [ADR-0003: Deploy via SCRNSAVE.EXE plus a System32 stub, framework-dependent](docs/adr/0003-scrnsave-stub-deploy.md) (accepted)
- [ADR-0004: One iTunes COM poller (the Tray); dismiss the saver by PostMessage, not IPC](docs/adr/0004-one-itunes-poller-postmessage-dismiss.md) (accepted)

Full human document: docs/index.html
