# ADR-0004: One iTunes COM poller (the Tray); dismiss the saver by PostMessage, not IPC

- Status: Accepted; the device-sync stand-down clause is **superseded 2026-06-28** (see Update at the end)
- Date: 2026-06-11 through 2026-06-24 (recorded in the Logbook 2026-06-26)
- Deciders: Ant

## Context

Two forces shaped the now-playing and dismissal design.

- **Reading iTunes.** The Microsoft Store build of iTunes for Windows does not publish to [SMTC](https://learn.microsoft.com/uwp/api/windows.media.control) (a probe returned zero sessions) and does not expose its automation object to outside processes (`GetActiveObject` returns `MK_E_UNAVAILABLE`, and it is not in the COM Running Object Table). The only way to read it is `CreateInstance`, which launches iTunes if it is not already running. A naive 2 s poll resurrected iTunes after the user closed it and blocked its quit with "a script is using it", which also wedged SMTC and the Elgato iTunes plugin so the deck stopped updating. Late-bound `dynamic` calls leak transient runtime callable wrappers that an explicit `Marshal.ReleaseComObject` does not cover.
- **Dismissing the saver from the Stream Deck.** A deck press cannot reach the screensaver through the input system: SMTC transport controls are API calls that produce no key event, the SMTC service grabs injected media keys system-wide, and an injected plain key (`SendInput`) only lands on the foreground window, which the saver routinely is not (focus-stealing prevention, the volume OSD, and multi-monitor all take foreground away seconds after launch).

## Decision

We will let **only the Tray** read iTunes over COM. It attaches only when `iTunes.exe` is already running (never `CreateInstance` otherwise, so it never launches iTunes), kills any instance a poll accidentally spawns during the shutdown race, releases every COM reference and runs a `GC.Collect()` / `WaitForPendingFinalizers()` / `GC.Collect()` reap each poll so it holds zero iTunes references between polls, and stands aside entirely (touching no COM) while an Apple mobile device is connected (`USB\VID_05AC&PID_12xx`, detected via SetupAPI present-filter) so device sync is never disturbed. The Tray publishes `nowplaying.png` and then `nowplaying.json` (both temp-then-move) in `%LOCALAPPDATA%\ScreenSaver\`; the screensaver and the plugin watch those files. The plugin dismisses the saver by enumerating the saver process's visible top-level windows and `PostMessage`-ing `WM_KEYDOWN` straight to each (focus-independent; the saver exits on any key). See `ScreenSaver/NowPlayingMonitor.cs` (the single source linked into all three assemblies) and `ScreenSaver.StreamDeck/CoverTileAction.cs`.

## Considered options

- **iTunes read path:** SMTC for iTunes (rejected: Store iTunes publishes zero SMTC sessions); `GetActiveObject` / ROT attach-only-never-launch (rejected: returns `MK_E_UNAVAILABLE`, not in the ROT); `CreateInstance` with an attach-only gate, per-poll GC reap, detect-and-kill of spawned duplicates, and a device-sync stand-down (chosen, the only path that both reads covers and lets iTunes close cleanly).
- **Dismiss path:** a shared named-event IPC dismiss handshake (`DismissSignal.cs` linked into both projects, saver listens, plugin signals) was implemented in commit 5a8041e and removed the same day to keep the suite simple; injected keys (`F15` or `VK_MEDIA_*` via `SendInput`) were tried and failed because the saver is usually not the foreground window; `PostMessage` to the enumerated saver windows (chosen) ignores focus.

## Consequences

- No process-coordination layer: the other components are plain file-watchers, and the deck dismiss needs no saver-side change.
- iTunes is funneled through one single-threaded apartment client instead of three, and a graceful iTunes close stays closed.
- `NowPlayingInfo.FromItunes` tags the source so the plugin can stay iTunes-exclusive while the screensaver shows whatever is playing. The PNG is written before the JSON so a reader without its own cover cache (the plugin) never renders a track behind.
- Negative: a duplicate iTunes can flash for under 10 s during the shutdown-window race (intrinsic, because `CreateInstance` is the only available reader and it launches on revoked COM); lengthening the poll interval shrinks the odds but cannot close the window. The Tray must be running for any iTunes art to reach the deck, and the per-poll GC is a deliberately blunt instrument for dropping leaked wrappers.

## Update (2026-06-28): device-sync stand-down removed

The "stands aside entirely while an Apple mobile device is connected" clause of the
Decision is withdrawn. It was added on the assumption that the COM poll disturbed
iTunes' device sync. Direct evidence later contradicted that: with the iPhone
confirmed connected, the failing device appeared as `USB\VID_0000&PID_0002` —
"Unknown USB Device (Device Descriptor Request Failed)", problem code 43
(`CM_PROB_FAILED_POST_START`) — i.e. it never completed USB enumeration at all, so it
never became an Apple device for any driver (or our COM) to touch. A cable reseat
(unplug/replug) fixed the sync. The fault was physical (cable/port), independent of
our read-only automation.

The stand-down's only observed effect was the inverse problem: the now-playing feed
froze (a stale Stream Deck cover) for as long as a phone was plugged in. So the guard
`if (AppleMobileDeviceConnected()) return;` and the whole `AppleMobileDeviceConnected()`
helper (its `cfgmgr32` `CM_Get_Device_ID_List*` P/Invokes and `CM_GETIDLIST_FILTER_*`
constants) were removed from `NowPlayingMonitor.PollItunes`. The Tray now polls iTunes
normally during a sync and the deck stays live. Everything else in this ADR (one
poller, attach-only, per-poll reap, detect-and-kill, PostMessage dismiss) stands.
Full record in `.claude/notes.md` (2026-06-28).

## References

- Code: `ScreenSaver/NowPlayingMonitor.cs`, `ScreenSaver.Tray/Program.cs`, `ScreenSaver.StreamDeck/CoverTileAction.cs`
- History: `.claude/notes.md` (2026-06-11, 06-12, 06-16, 06-20, 06-21, 06-22, 06-24)
- Recorded in the Journal of `docs/index.html` (rev 1, 2026-06-26).
