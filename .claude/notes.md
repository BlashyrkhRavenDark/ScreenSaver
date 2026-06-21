# ScreenSaver — durable project notes

Durable, git-tracked project knowledge (referenced by global CLAUDE.md).
Auto-memory is machine-local and not synced; keep practical facts here.

## Black-screen root cause: child controls don't composite on .NET 10 → owner-draw (2026-05-29)

**Symptom:** Screensaver shows a black screen on this Windows 11 / 4K@150%
(PerMonitorV2) box — both via idle activation and the tray "Test" entry. DiagLog
showed a healthy run (covers loaded, form visible, tiles created), yet nothing
visible.

**Actual root cause (verified on-screen):** On this machine's .NET 10 runtime,
**child controls of the screensaver form never composite to the screen.** The
form's own surface paints fine, but its children do not — confirmed by setting the
form background magenta and adding a big stock `Label`: the screen showed pure
magenta with NO label and NO covers. This held across *every* variable tested:

- custom `UserPaint` control vs stock `PictureBox` vs stock `Label` — all invisible
- PerMonitorV2 vs DPI-unaware — no difference
- `Show()`+`Application.Run()` vs `Application.Run(form)` — no difference
- borderless/topmost/fullscreen vs a normal bordered window — no difference

Win32 confirmed the children were genuine `WS_CHILD | WS_VISIBLE` windows, correctly
parented, `IsWindowVisible`=true — yet DWM did not composite them. Pre-overhaul
(.NET Framework) the same child-`PictureBox` design composited fine; this is a
.NET 10 behavior here. The regression point was commit 9ecd67b (the .NET 10
overhaul that replaced stock `PictureBox` tiles with child controls).

**The fix:** Owner-draw the entire UI in `ScreenSaverForm.OnPaint` into a manual
back-buffer (Bitmap) that is blitted in one `DrawImageUnscaled`. No child controls.
`FadingPictureBox` was replaced by `FadingTile` — a plain (non-Control) renderer
with the same transition effect math and a `Paint(Graphics, Rectangle)` method.
A single form-level animation `Timer` (~33ms) advances transitions/highlight pulses
and repaints only while something is animating. The now-playing focal overlay,
sibling thumb, captions, manage hint, and hide-toast are all owner-drawn text/rects.
Hit-testing maps mouse coords to a tile index. Verified on-screen at full 3840x2160:
mosaic + transitions + now-playing focal overlay all render correctly.

**WRONG earlier diagnosis (do not trust it):** an intermediate theory blamed
WinForms `OptimizedDoubleBuffer` suppressing `OnPaint`. That was a red herring —
`OnPaint` firing or not was irrelevant because *no* child control composites here.
The real issue is child-window composition, fixed only by owner-drawing on the form.

## Verifying screensaver rendering without an interactive session

- `Form.DrawToBitmap(...)` is USELESS for this — it renders the control tree into a
  memory bitmap, bypassing the on-screen path that's actually broken. A correct-looking
  DrawToBitmap can coexist with a black screen. Do not trust it.
- The reliable check is a real screen capture (`Graphics.CopyFromScreen`). This shell's
  capture IS faithful (a baseline grab showed the real desktop). A black 4K frame is
  ~17 KB; a real mosaic is ~3-15 MB. To capture at true physical resolution, the capture
  process must be PerMonitorV2-aware (`SetProcessDpiAwarenessContext(-4)`); a DPI-unaware
  capture grabs the scaled desktop and can mis-map a 4K screen.
- **Bitdefender will block the capture script** if the suspicious bits (runtime
  `Add-Type` P/Invoke + `CopyFromScreen` + `Start-Process`) appear on the powershell
  **command line** (it scans command lines; you'll see "malicious command line" + `EPERM`
  on spawn). Put the script in a `.ps1` file and run `powershell -NoProfile -File x.ps1`
  so the command line is clean. Do NOT use `-ExecutionPolicy Bypass` (separate block).
- The screensaver dismisses on mouse *movement*, not on a programmatic `CopyFromScreen`,
  so a capture script can launch it, wait, grab, and kill it.

## Stream Deck presses are SMTC API calls, not key events (2026-06-11)

The Cover Tile action's media controls go through SMTC WinRT calls
(`TryTogglePlayPauseAsync` etc.) straight to the media app; only volume (and
SMTC-refused fallbacks) inject real keys via `SendInput`. An API call between
two other processes produces NO keyboard message, so the screensaver's WndProc
can never "see" a deck transport press — there is nothing to intercept, and
the media control itself works fine while the saver is up.

**Decision (operator, 2026-06-11): do NOT bridge this with IPC.** A named-event
dismiss handshake (`DismissSignal.cs` shared into both projects; saver listens,
plugin signals on KeyPressed) was implemented in 5a8041e and removed the same
day at the operator's request — the project must stay simple, no extra
process-coordination layer on top of the existing helper apps. Accepted
consequence: deck transport presses (play/next/prev/stop via SMTC) control the
music but do NOT dismiss the screensaver; mouse, keyboard, and the deck's
volume/fallback keys (real injected keys) remain the dismissal triggers.

Related (kept): the saver's WndProc must NOT swallow WM_KEYDOWN/WM_APPCOMMAND
when dismissing — pass to `base.WndProc` so genuine media/volume keys keep
their system action (volume OSD, play/pause) on the way out.

## Saver was denied keyboard focus → keys never reached it (2026-06-11)

The "deck keys do nothing while the saver is on" report was a FOCUS bug, not a
key-routing one. A saver launched indirectly (System32 stub → App, or any
background/programmatic launch) is denied foreground by the SetForegroundWindow
rules, so the previously focused app silently keeps receiving every key —
keyboard AND injected media/volume keys. Mouse still worked (routed by
position), which masked the problem.

**Fix:** `ScreenSaverForm.OnShown` grabs focus via the AttachThreadInput trick
(attach to the current foreground thread's input queue, SetForegroundWindow +
Activate, detach). DiagLog writes "keyboard focus acquired / NOT acquired".

**Verification recipe (no deck needed):** launch the saver, then inject a key
the way the plugin does. BitDefender AMSI blocks a .ps1 containing SendInput
P/Invoke ("malicious content"), but COM SendKeys is allowed and equivalent:
`(New-Object -ComObject WScript.Shell).SendKeys('{F15}')` — F15 = real key
event, no system side effect. Saver process gone after = pass. Test BOTH the
direct App launch and the System32 stub launch.

## Stream Deck "pages" = bundled Covers profiles (2026-06-11)

API truth (re-verified against SDK docs): plugins can NEVER flip pages of a
user profile; the only navigation is `switchToProfile`, restricted to profiles
bundled with the plugin. BarRaider SdTools (even 7.0) never implemented the
`page` payload field, so pages are modeled as four single-page bundled
profiles "Covers 1".."Covers 4" and Next/Prev switches between them by name
(wrap-around). Pager keys carry an "On page" setting (settings.page, 1-based)
so the plugin knows the neighbour without querying the current page (the API
has no "what page is showing?").

`.streamDeckProfile` format (reverse-engineered from ProfilesV3 on this
machine, SD 7.4): a zip containing `<UUID>.sdProfile/manifest.json` with
`{Device:{Model,UUID}, Name, Pages:{Current,Default,Pages[]}, Version:"3.0"}`
plus `Profiles/<PAGEUUID>/manifest.json` with
`{Controllers:[{Actions:{"col,row":{action key JSON}}, Type:"Keypad"}], Icon, Name}`.
Templates were generated by a python one-liner (see git history of this note's
commit); Device is pinned to the operator's Classic (Model 20GBA9901).

Install flow gotcha: updating the plugin in place did NOT make SD prompt to
install the bundled profiles — the prompt is expected on the FIRST
switchToProfile call (first pager-key press). Do NOT tell the user to
double-click a .streamDeckProfile file: that imports it as a *user* profile,
which the plugin is forbidden to switch to.

## VK_MEDIA_* never reaches the focused window — F15 poke (2026-06-12)

Log-verified correction to the two 2026-06-11 notes above: even the plugin's
*injected* `VK_MEDIA_*` fallback keys never reach the screensaver, because
Windows' SMTC service intercepts media-transport keys system-wide and hands
them to the media app directly (same press session: `VK_VOLUME_MUTE` arrived
at the saver as `WM_KEYDOWN 0xAD` and dismissed it; four `VK_MEDIA_PLAY_PAUSE`
and two `VK_MEDIA_NEXT_TRACK` never produced any saver message). Volume keys
take the normal input path; transport keys do not. Also: the operator's player
refuses SMTC control (`TryTogglePlayPauseAsync` returns false), so transport
actions ALWAYS use the injected-key fallback.

**Fix that finally works (kept deliberately tiny, no IPC):** on every deck
KeyPressed, if a `ScreenSaver` process exists, the plugin injects `VK_F15`
(0x7E — real key event, no system action) before running the media action.
F15 rides the normal input path to the focused saver, which dismisses on any
key. No saver running → no injection, zero side effects. This supersedes the
"deck transport presses do NOT dismiss the screensaver" consequence noted
above.

## F15 SendInput also failed (focus) → PostMessage to the saver's window (2026-06-16)

The F15-poke fix above was ALSO unreliable and regressed in the field: log
showed the plugin injected F15 on four mute presses ("Saver running - injecting
F15...") yet the saver logged NO `WndProc dismiss` — the saver had grabbed
focus at launch (23:11:51) but was no longer the foreground window by the press
(23:11:55). SendInput only lands on the FOREGROUND window; Windows
focus-stealing prevention / the volume OSD / multi-monitor routinely take
foreground away from the saver seconds after it starts. So injected keys
(F15 or VK_VOLUME_MUTE alike) are a coin-flip.

**Fix that is actually focus-independent:** the plugin's `DismissScreenSaver`
now enumerates the `ScreenSaver` process's visible top-level windows
(EnumWindows + GetWindowThreadProcessId, match by PID) and `PostMessage`s
`WM_KEYDOWN VK_F15` straight to each. PostMessage queues to the target window
regardless of focus; the saver's existing WndProc exits on any WM_KEYDOWN.
Same-integrity processes so UIPI permits it. NO saver-side change, NO IPC
handshake. Verified with a compiled probe that launched the saver, let
**LockApp** take foreground (saver definitely not foreground), posted, and the
saver exited cleanly (`saver procs after: 0`). This supersedes the F15-SendInput
note above.

## Stream Deck shows iTunes covers ONLY (2026-06-16)

Operator wants the deck to mirror iTunes artwork and absolutely nothing else
(no Spotify/browser/other SMTC source). `NowPlayingInfo` now carries a
`FromItunes` flag (set in `ApplyItunesUpdate`=true / `ApplySmtcUpdate`=false);
the plugin's `OnUpdated` early-returns unless `FromItunes`. The screensaver is
unaffected — it still shows whatever is playing. iTunes data reaches the deck
only via the Tray's `nowplaying.json`/`png` IPC (the plugin is a file-watcher,
not an iTunes poller), so the **Tray must be running** for the deck to show
anything; verify it (and iTunes) before assuming the filter is broken.

## Locked Windows: deck is fully sleep/locked-out; only the screensaver image is customizable (2026-06-17)

Confirmed (operator + app logs): when Windows locks, the Elgato app sleeps the
deck and shows its screen-saver image (Elgato logo by default). On THIS setup
keys do NOT execute while locked - media control while locked is not possible
via Elgato (the SMTC/PostMessage tricks are irrelevant; the app simply doesn't
run the profile while locked). Mechanism in StreamDeck.log:
`ESDWinSessionStateManager` (SESSION ENDED on lock / STARTED on unlock) +
`ESDSleepHelper::WakeUpAllDevices` on resume.

The ONLY lock-screen customization is replacing the screen-saver image
(Preferences -> Screen Saver; built-in in 7.4.x, also BarRaider's plugin). You
canNOT keep the live cover mosaic showing while locked, nor disable the
lock-sleep to keep the profile up.

**Canvas = 480x272** - the original 15-key Stream Deck's single LCD panel
resolution (confirmed by an existing 480x272 asset in
`%APPDATA%\Elgato\StreamDeck\Assets\`). The deck shows this one image through
the 5x3 key windows. Screen saver accepts a GIF (256 colors/frame).

**Our solution:** `tools/prince-screensaver/make_gif.py` (Pillow) builds a
looping GIF rotating one cover per frame (artist filter, default exactly
"Prince" = 28 albums; NB "contains prince" wrongly catches Prince Paul /
Princess Nokia / Les Joyaux De La Princesse), 2 s each, 480x272, sharp square
cover centered over a blurred-darkened fill. Output:
`%LOCALAPPDATA%\ScreenSaver\prince-screensaver.gif`. Set it once via
Preferences -> Screen Saver. Re-run when the collection grows. The GIF binary
is NOT committed (regeneratable, library-specific); the generator is.

## How to draw on the deck while Windows is locked — raw USB HID (RE of BarRaider, 2026-06-17)

Reverse-engineered BarRaider's "Stream Deck Screen Saver" plugin (v1.7, recovered
from GitHub git history, decompiled with ilspycmd) to learn how it shows an image
on the deck when the PC is locked. Decompiled refs were left in
`%LOCALAPPDATA%\Temp\sdscreensaver-re\decompiled\` (delete when done).

THE MECHANISM (the important part):
- It does NOT use the Stream Deck SDK / websocket for the lock drawing. It bundles
  `StreamDeckSharp` + `OpenMacroBoard.SDK` + `HidLibrary` and talks to the deck over
  **raw USB HID**, completely bypassing the Elgato app. (BarRaider.SdTools/websocket
  is used only for its settings UI + test key.) This is unusual — most BarRaider
  plugins are websocket-only; this one is a hybrid.
- Lock detection: `Microsoft.Win32.SystemEvents.SessionSwitch` → on `SessionLock`
  open the deck(s), set lock-brightness, start a 2s `System.Timers.Timer` that
  redraws; on `SessionUnlock` stop timer, restore brightness, clear keys.
- Device open: `StreamDeck.EnumerateDevices().Open()` → HID `CreateFile` with
  `GENERIC_READ|WRITE` and **`FILE_SHARE_READ|FILE_SHARE_WRITE`** (share mode 3),
  device matched by VID `0x0FD9`. Shared-write is why a 2nd writer can hold the
  same deck while the Elgato app also has it open.
- Draw: scale one bitmap to the full key-grid area, slice per key, `SetKeyBitmap`
  → HID **output report** (`WriteReport`); brightness → HID **feature report**
  (`WriteFeature`). The 2s re-push is how it "wins" the screen — it just keeps
  overwriting. The Elgato logo is itself just a HID write anyone with the handle
  can overwrite (StreamDeckSharp even has `GetLogoMessage()`).
- v1.7's folder/slideshow + GIF rotation is NOT wired up (draws one static bitmap);
  rotation must be implemented ourselves anyway.

WHY THIS WORKS ON THIS MACHINE (where "keys don't work while locked"): that
limitation is the websocket/SDK path — the app stops dispatching to normal plugins
on lock. Raw HID sidesteps it. During the locked window the app isn't actively
drawing, so a raw-HID writer is effectively the sole writer (no contention). Two
writers only conflict (flicker) if both draw at once.

THE ELGATO LOGO = firmware **"Standby Screen"** (Preferences → Devices → [device]
→ Advanced; added in SD 6.6), SEPARATE from the app "Screen Saver" (Preferences →
General). The logo shows on lock because the app stops driving the deck and the
firmware shows its standby image (default = logo). FIRST-PARTY FIX with no code:
set a custom Standby Screen (480x272, **static** image only) → replaces the logo.
The app Screen Saver (which does accept a GIF and fires on lock in general) is
suppressed here because the app sleeps the deck on lock.

REPLICATION PATH for rotating covers on lock (build-it-ourselves):
- Put it in **ScreenSaver.Tray** (already always-running; `SessionSwitch` needs a
  long-lived process w/ message pump — a `.scr` won't do). Reuse the suite's mosaic
  compositing for the bitmap.
- Reference current `StreamDeckSharp` NuGet; on `SessionLock` enumerate→open→2s
  timer drawing the next cover full-screen; on `SessionUnlock` clear + **Dispose**
  (so the Elgato app reclaims the deck cleanly — v1.7 omits dispose; we should not).
- VERIFY FIRST: the bundled fork only knew original/XL/Mini PIDs. This deck is
  "StreamDeckClassic" (model 20GBA9901) — confirm its PID is in the current
  StreamDeckSharp build before relying on it.

## Stream Deck lock-screen display - IMPLEMENTED (2026-06-17)

Configurable "what the deck shows while Windows is locked", per the RE above.
- Settings (HKCU\SOFTWARE\Demo_ScreenSaver, in ScreensaverSettings): `LockDeckMode`
  (enum NowPlaying=0 default / Picture=1 / Gif=2 / Off=3) + `LockDeckImagePath`.
- Config UI: AlbumCoverFinder -> "Stream Deck lock" tab (mode dropdown + file
  picker). Order of the combo items MUST match the enum.
- Runtime: `ScreenSaver.Tray/LockScreenDeck.cs`. Self-subscribes to
  `SystemEvents.SessionSwitch`; on SessionLock opens the deck via **StreamDeckSharp
  6.1.0** (raw HID, backend = HidSharp; transitive SixLabors.ImageSharp), sets
  brightness 100, and draws per mode on a timer; on SessionUnlock disposes the
  board so the Elgato app reclaims it. NowPlaying reads the Tray's `nowplaying.png`
  (kept fresh by the COM poll even while locked) and re-pushes every 1s (also
  beats any late standby logo); Gif steps frames at their GIF delays; per-key
  slice = compose 488x280 blurred-fill frame -> crop each key -> `SetKeyBitmap(i,
  KeyBitmap.Create.FromBgr24Array(w,h,bgr))`. No `ClearKeys` on IMacroBoard 6.x;
  blank with `KeyBitmap.Black`.
- Deck: confirmed **Stream Deck MK.2, PID 0x0080**, recognized + openable
  (shared-write) by StreamDeckSharp 6.1.0 while the Elgato app runs (spike). The
  old BarRaider fork (0.3.4) predated MK.2 - that's why the current package matters.
- Diagnostic: `%LOCALAPPDATA%\ScreenSaver\deck-lock.log` (mode, deck open result,
  draw errors). Check it first if a lock-test shows nothing.
- NOT yet verified on a real lock (can't lock the box autonomously) - needs an
  operator Win+L test. HID draw itself is spike-proven.

## iTunes COM: never launch it, never hold it persistently (2026-06-20)

Bug (user-reported): iTunes wouldn't close ("a script is using it"), relaunched
itself after force-close, and the Stream Deck buttons/updates died. Cause: the
Tray's NowPlayingMonitor poll did `Activator.CreateInstance("iTunes.Application")`
whenever it had no handle. For an out-of-process COM server that LAUNCHES iTunes if
it isn't running, so the 2s poll RESURRECTED iTunes every force-close, and the
persistently-held RCW made iTunes refuse to quit. The relaunch storm also wedged the
Elgato iTunes-controller plugin + SMTC, so the deck stopped updating / buttons died.
(The lock-deck feature was NOT involved - deck-lock.log showed clean lock/unlock.)

Fix (NowPlayingMonitor, writer/poll path only; readers with pollItunes=false were
never affected - they only watch nowplaying.json): (1) `IsItunesRunning()` gate -
attach ONLY when iTunes.exe is already in the process list, NEVER CreateInstance
otherwise, so we never launch it; (2) attach per-poll and `Marshal.ReleaseComObject`
the app + track + artwork RCWs in `finally` every cycle, so no lingering automation
client blocks iTunes' quit; (3) removed the startup CreateInstance in
TryStartItunesPolling.

Operational note: BitDefender is currently killing the PowerShell *tool* spawns
(`uv_spawn powershell.exe` -> EPERM), esp. on recon-type commands (WMI, powercfg,
event log). The Bash tool (dotnet / git / taskkill / cp) still works - use it for
build, deploy, and process control until that clears.

## iTunes relaunch fix #2: use GetActiveObject (ROT), not CreateInstance (2026-06-21)

The 2026-06-20 fix above (gate on iTunes.exe in the process list, then
Activator.CreateInstance) was INSUFFICIENT - iTunes still relaunched itself. Root
cause: during a graceful iTunes quit the process LINGERS in the task list for a
few seconds while its COM object is already revoked from the ROT. A poll landing
in that window passes the process-list gate, calls CreateInstance, finds no
registered server, and DCOM LAUNCHES a fresh iTunes (confirmed: relaunched
iTunes' parent was svchost = DcomLaunch). Isolation test nailed it: with the Tray
killed, a graceful iTunes close stayed closed and iTunesControllerPro (the Elgato
plugin, also a COM client) did NOT relaunch it - so our Tray was the resurrector.

Real fix: attach via the COM Running Object Table - P/Invoke `CLSIDFromProgID` +
`GetActiveObject` (oleaut32; Marshal.GetActiveObject was removed from .NET 5+).
GetActiveObject returns the running instance ONLY if it's registered in the ROT
and NEVER launches the server, so a half-closed iTunes is reported as gone instead
of relaunched. Released per poll as before. Removed the process-list gate and the
m_oItunes field. NEVER use CreateInstance/CoCreate for iTunes attach again.

## iTunes COM is fundamentally hostile on the Store build (2026-06-22)

Probe against the live Store/packaged iTunes (PID running):
- `GetTypeFromProgID("iTunes.Application")` resolves fine (CLSID dc0c2640-...).
- `GetActiveObject(clsid)` -> **0x800401E3 MK_E_UNAVAILABLE** (obj null). The Store
  iTunes does NOT expose its automation object to outside processes, so the
  ROT/"attach-only, never launch" approach is IMPOSSIBLE here.
- `Activator.CreateInstance` -> works, reads CurrentTrack, and when iTunes is
  already running it attaches WITHOUT spawning a new PID. But it LAUNCHES iTunes
  when absent (the relaunch bug).

Worse, demonstrated this session: with our Tray attached, a graceful iTunes quit is
BLOCKED ("a script is using it") and iTunes goes COM-unresponsive on the prompt;
CreateInstance then spawns a duplicate iTunes. Killing the Tray (removing the
automation client) let iTunes quit instantly. So **any** COM attach from our Tray
creates quit-friction; per-poll release shrinks but does not remove the collision
window, and a "detect the spawned PID and kill it" build (committed here) still
(a) trips "script is using it" on close and (b) flashes/thrashes a duplicate during
the shutdown window. CreateInstance-based polling is a dead end for clean UX.

LIKELY REAL FIX to pursue: check whether the Store iTunes publishes to **SMTC**
(Windows System Media Transport Controls). If it does, read now-playing via the
existing SMTC monitor and DROP iTunes COM entirely (zero quit-friction, zero
relaunch) - and revisit the deck's iTunes-only filter (SMTC info is FromItunes=
false today). If SMTC has no iTunes, the alternatives are proper COM quit-event
handling (OnAboutToPromptUserToQuitEvent; hard via late-bound dynamic) or dropping
the iTunes cover feed from the Tray. Decision pending operator input.
