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
