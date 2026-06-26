# ScreenSaver

A Windows album-cover-mosaic screensaver suite (.NET 10): the screensaver
itself, the **AlbumCoverFinder** scan/settings UI, an always-on **tray
companion** (desktop wallpaper, lock screen, Discord Rich Presence, a LAN web
page, and locked-deck artwork), and a **Stream Deck plugin** that tiles the
currently-playing cover across one, four, or nine keys.

Inspired by the old iTunes album-art screensaver on macOS. Demo:
https://www.youtube.com/watch?v=YUvwzdiCS0g

## Documentation

The full design and technical documentation is one self-contained living
document: open [`docs/index.html`](docs/index.html) in a browser. It covers all
four components, the cover-finding pipeline, the `.scr` packaging, the registry
and file-layout reference, demos, and a QA bench.

- **Build / install**: the project skills under `.claude/skills/` (`/build`, `/install`).
- **Durable project notes**: `.claude/notes.md`.
