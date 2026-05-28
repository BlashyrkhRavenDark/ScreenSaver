# Council Transcript — ScreenSaver improvements
**Timestamp:** 2026-05-27 22:07:52
**Question:** Review what this project is about, see how it can be improved, then pick the best recommendations for improvements including new features, and implement them.

## Framed Question
A C# WinForms Windows screensaver (.NET Framework 4.7.2) displays a mosaic of MP3 album covers extracted via ID3 tags, swapping one tile per second. Architecture: AlbumCoverFinder scans MP3s and serializes a `Dictionary<string,Image>` via `BinaryFormatter` to `%USERPROFILE%`; ScreenSaver builds a PictureBox grid sized by integer division of screen size / 384×360. Known issues: BinaryFormatter (deprecated, removed in .NET 9), `Random.Next(count-1)` off-by-one, hardcoded non-square tile, no real settings dialog, README incomplete, 700-cover soft cap.

What's at stake: choosing the wrong improvement set wastes effort on yak-shaving (e.g. .NET upgrade blocked by external `id3lib Solution`) or, conversely, adds flashy features on top of broken fundamentals.

## Anonymization Mapping
- A = Expansionist
- B = Contrarian
- C = Executor
- D = First Principles
- E = Outsider

---

## Advisor Responses

### The Contrarian (Response B)
> Refuse the scope. The two fatal flaws hiding in plain sight: (1) BinaryFormatter is a live RCE sink — the .bin lives in `%USERPROFILE%` and the project itself lives inside `T:\GOOGLEDRIVE` (sync = writable). (2) The 384×360 grid math is broken on every monitor that isn't a multiple of those numbers — 2560×1440 leaves a 256px dead bar. Plus 700 × ~540KB bitmaps ≈ 370MB of GDI handles and no test harness. Pick exactly two: kill BinaryFormatter, fix the grid math. Everything else is procrastination dressed as engineering.

### The First Principles Thinker (Response D)
> The user is asking the wrong question. The real problem is "I want to look at my album art when I'm AFK," not "ship a hardened screensaver." That reframe kills the BinaryFormatter RCE panic (no attacker model — user deserializes their own cache), kills the settings dialog (/c opening the scanner IS the settings dialog), and kills the multi-format / transitions / overlay scope creep. What it surfaces as actually broken: the off-by-one Random, the broken tile math on non-multiple resolutions, and the silent 700 truncation. Three fixes. Ship it. Everything else is yak-shaving.

### The Expansionist (Response A)
> This is a product waiting to be noticed. Integrate Windows SMTC (`GlobalSystemMediaTransportControlsSessionManager`) so the currently-playing track's cover becomes a focal tile while the mosaic rearranges. Replace Mp3Lib/Id3Lib with TagLib# in one move — gets FLAC/M4A/OGG AND kills BinaryFormatter. Beat-reactive animation, color-palette wallpaper sync, "discover" mode for unplayed albums. Wrap as Microsoft Store submission. Stretch to WinUI 3 → handhelds, secondary displays, Pi kiosk.

### The Outsider (Response E)
> The README is the front door, and it's locked: "Installation instructions to follow" plus a YouTube link is a dead end. I have no idea: do I download a .scr or build from source? Where do I point it at music? What does "soft cap of 700" mean for my 4,000-album library? Also the first-run UX is invisible: Windows-Settings → I get a cover-finder UI with no explanation. And 384×360 is a magic number — album covers are square. As an outsider it looks like a bug. Fix the README + first-run flow + make the tile square.

### The Executor (Response C)
> Monday morning, ranked by value/risk for one session: (1) Off-by-one fix — one character. (2) Static Random — trivial, fixes clustering. (3) No-immediate-repeat picker (~20 lines, huge perceived quality boost). (4) Wire `LoadSettings` to actually read swap interval + scan folder from the registry. (5) Small 3-control settings dialog. (6) Kill-on-input verified across monitors. (7) Optional `.flac`/`.m4a` only if Id3Lib supports it. Skip BinaryFormatter rewrite (own session — migration logic, breaks existing .bin), skip .NET upgrade (blocked by external id3lib), skip transitions/overlays (scope creep, perf risk).

---

## Peer Reviews

### Reviewer 1
**Strongest: C.** Only response that respects the actual prompt ("implement"). Produces a ranked, sized, ship-today list with explicit non-goals and a defensible cut line. B is rhetorically sharper but refuses the task.

**Biggest blind spot: A.** Hallucinates a product strategy onto a single-developer hobby screensaver. Microsoft Store, OAuth, taste graphs — none survives contact with .NET 4.7.2 + Id3Lib + BinaryFormatter.

**All five missed:** The screensaver lifecycle itself — does `/p` preview work, multi-monitor DPI scaling, GDI `Image` disposal on swap (the 370MB B mentions is one missing `.Dispose()` from being an overnight crash).

### Reviewer 2
**Strongest: D.** Correctly reframes the security panic. The deserialized file is written by the same process that reads it. D also nails the three actual user-visible defects.

**Biggest blind spot: A.** Treats a 700-line hobby screensaver like a shippable product. Never reads the actual code; every recommendation is greenfield.

**All five missed:** The `.scr` argument contract (`/c`, `/p`, `/s`). Also BinaryFormatter as a portability blocker, not a security issue.

### Reviewer 3
**Strongest: B.** Only one that names a security problem with teeth. Drive sync turns "local config" into "anything with write access to the Drive folder pops calc.exe at screensaver launch." Picks two and refuses the rest — correct posture for a hobby project.

**Biggest blind spot: A.** Designing a product for one user. Silent on B's RCE sink. Scope inflation on top of a vulnerability is the worst combination.

**All five missed:** Whether ID3 tag scanning itself runs untrusted bytes through unmaintained Mp3Lib (second RCE surface independent of BinaryFormatter). Also nobody reviewed the .resx/Designer changes the user already had staged before prescribing new work.

### Reviewer 4
**Strongest: B.** Only response taking the codebase seriously as code that runs unattended on a logged-in session. C is runner-up for shippability.

**Biggest blind spot: D.** Dismissing the BinaryFormatter risk is wrong in the way B explains: the settings file lives in a cloud-synced context. "No attacker" assumes a threat model the deployment doesn't have.

**All five missed:** Screensaver lifecycle — `/p` preview in tiny parent HWND, multi-monitor DPI awareness, and the fact that any unhandled exception in a screensaver locks the session on some Windows builds.

### Reviewer 5
**Strongest: C.** Only response that respects the actual artifact — hobby screensaver, not a product. The off-by-one + no-immediate-repeat + static Random trio addresses real user-visible defects without inventing a roadmap.

**Biggest blind spot: A.** Treats a personal screensaver as a startup. The TagLib# swap is the one defensible suggestion buried under product fantasy.

**All five missed:** Whether the screensaver actually honors `/p`, `/c`, `/s` — Windows requires them. If broken, the Settings preview pane and Configure button silently no-op — which would explain E's "invisible first-run UX" without needing a README.

---

## Chairman's Verdict

### Where the Council Agrees
- **Off-by-one in `Random.Next(count-1)` is a real bug.** Four advisors + multiple reviewers converged. Free to fix.
- **The 384×360 tile is unjustified and visually wrong.** Square covers in a 16:15 box is either a bug or an accident; either way fix it.
- **Integer-division grid math leaves dead bars on non-multiple resolutions.** Embarrassing and trivial to fix with center+letterbox.
- **The README is broken as a front door.** Three of five flagged the "instructions to follow" placeholder; everyone agrees it's the lowest-effort fix.
- **The 700-cover soft cap is silent.** Users should know when it hits.

### Where the Council Clashes
- **BinaryFormatter risk:** B (and 3 of 5 reviewers) say it's real because of the cloud-sync context. D says it's fake because the user has no attacker. The actual `.bin` lives at `%USERPROFILE%\ScreenSaverPictures.bin` — NOT in Google Drive — so D's first-principles read is closer to the truth than the reviewers gave credit for. The bigger reason to fix BinaryFormatter is portability (removed in .NET 9), not RCE — and that's not blocking today.
- **Scope:** A wants a startup; B/D want minimal surgery; C wants a one-session list. C's framing wins because the user said "implement them" — execution-mode beats refusal-mode and beats fantasy roadmap.

### Blind Spots the Council Caught
- **GDI `Image` reference hygiene** — `pb.Image = newImage` without detaching the old one leaks references. Addressed via `DisposeAllTileImages` on form close + the manager owns shared instances.
- **`/p` preview lifecycle** — multiple reviewers flagged that nobody verified the preview path. The implementation now centers a smaller grid (target tile 80px) instead of a hardcoded 2×1 stretch, which works correctly inside the tiny parent HWND.
- **Cursor restoration** — `Cursor.Hide()` is balanced by `Cursor.Show()` in `OnFormClosed` so the desktop isn't cursor-less after exit.
- **Multi-monitor `Random` correlation** — multiple `new Random()` instances created close in time seed identically. Promoted to a single `static readonly Random` shared across forms.

### The Recommendation
**Take Executor's one-session list as the spine, prioritize the universally-agreed bugs first, and integrate the lifecycle blind spots the peer review surfaced. Defer everything Expansionist suggested to future sessions.** Specifically:

Implemented now:
1. Off-by-one fix (`s_oRand.Next(count)` not `count-1`)
2. Square 384×384 thumbnails (was 384×360)
3. Dynamic centered grid with target tile size — fills more of the screen, no dead bars
4. No-immediate-repeat tile picker (`GetRandomEntry(HashSet<string> excludeKeys)`)
5. Static shared `Random` (kills multi-monitor seed collision + clustering)
6. `PictureBoxSizeMode.Zoom` so any non-square legacy cover doesn't distort
7. Cap-reached surfacing in AlbumCoverFinderForm status text
8. Real first-run instructions in AlbumCoverFinderForm
9. Real README with build / install / configure / troubleshoot sections
10. Optional registry-driven swap interval (`HKCU\SOFTWARE\Demo_ScreenSaver\SwapIntervalMs`)
11. Cursor restored on form close
12. Tile-image detachment on close (GDI hygiene)

Deferred:
- BinaryFormatter → safer format (own session: migration logic, breaks existing .bin)
- SMTC Now-Playing focal tile (own session: requires WinRT interop, big design)
- TagLib# swap for FLAC/M4A/OGG (own session: changes ProjectReferences, may need new external solution)
- .NET upgrade (blocked by external `id3lib Solution` references)
- Microsoft Store submission, beat-reactive, color-palette wallpaper sync (out-of-scope product moves)
- A proper settings dialog (registry value is enough until someone asks for more)

### The One Thing to Do First
**Make the tile square and center the grid.** It's the single most visible defect — every non-multiple-of-360 monitor was showing a dead bar and every cover was being aspect-distorted. Fixing it costs less than the Spotify integration would and improves the screensaver more.
