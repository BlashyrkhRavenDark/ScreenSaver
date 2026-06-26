# ScreenSaver

A Windows album-cover-mosaic screensaver suite (.NET 10): the screensaver
itself, AlbumCoverFinder (config/scan UI), a tray companion, and a Stream Deck
plugin that tiles the current cover across the deck.

Durable project notes: see `.claude/notes.md` (deploy quirks, gotchas, decisions).

Build / install: use the project skills `/build` and `/install`.

## Docs

The single living document is `docs/index.html` (the Logbook convention),
maintained by this repo's doc agents (`.claude/agents/doc-writer.md`,
`doc-auditor.md`) applying the global `documentation` skill. Update it in the
same commit as any code it describes.

Environment, network, deployment, and conventions are NOT duplicated here: they
live in the jean-claude-synced `~/.claude/reference/` (`environment.md`,
`network.md`, `deployment.md`, `conventions.md`), with the documentation method
itself in `~/.claude/reference/documentation.md`.
