# ADR-0003: Deploy via SCRNSAVE.EXE plus a System32 stub, framework-dependent

- Status: Accepted
- Date: 2026-06 (recorded in the Logbook 2026-06-26)
- Deciders: Ant

## Context

The earlier deploy copied a compressed, self-contained, single-file `.scr` (about 55 MB) into `System32`. That combination (a large compressed payload, in System32, unsigned) reads as a packer to [Bitdefender](https://www.bitdefender.com), which blocked installation. The Windows screensaver picker, however, only lists `.scr` files that live in `System32`, so the picker entry cannot simply move elsewhere.

## Decision

We will publish the screensaver framework-dependent (a small exe plus a few DLLs, which looks like any .NET app) into `%LOCALAPPDATA%\ScreenSaver\App\`, and point Windows at it through `HKCU\Control Panel\Desktop\SCRNSAVE.EXE`. To restore the picker dropdown entry, `ScreenSaver.Launcher` publishes a roughly 160 KB framework-dependent, uncompressed stub to `C:\Windows\System32\ScreenSaver.scr`; the stub reads `HKCU\SOFTWARE\Demo_ScreenSaver\ScreenSaverPath` and forwards the standard screensaver arguments (`/s`, `/p <hwnd>`, `/c`) to the real app. See `ScreenSaver.Launcher/Program.cs` and the `install` skill.

## Considered options

- **Self-contained compressed single-file `.scr` in System32** (the prior model). Rejected: flagged as a packer by Bitdefender and blocked.
- **Self-contained `.scr` outside System32.** Rejected: still large, compressed, and unsigned, and the Windows picker only enumerates `.scr` files in System32, so the dropdown entry would vanish.
- **Framework-dependent app in `%LOCALAPPDATA%` plus a registry pointer and a tiny uncompressed System32 stub (chosen).**

## Consequences

- No antivirus false positive, and idle activation runs the per-user app with no admin rights.
- Only one step needs UAC (dropping the System32 stub); everything else is per-user.
- Negative: a second tiny project (`ScreenSaver.Launcher`) exists solely to satisfy the picker, and correct behavior now depends on the `SCRNSAVE.EXE` registry pointer and `ScreenSaverPath` staying in sync with where the app was installed. If the stub is present but the pointer is stale, the picker entry launches nothing.

## References

- Code: `ScreenSaver.Launcher/Program.cs`, `ScreenSaver/Program.cs`
- Install flow: `.claude/skills/install/SKILL.md`
- Recorded in the Journal of `docs/index.html` (rev 1, 2026-06-26).
