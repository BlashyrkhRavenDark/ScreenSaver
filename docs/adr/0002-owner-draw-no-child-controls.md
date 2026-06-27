# ADR-0002: Owner-draw the screensaver, with no child controls

- Status: Accepted
- Date: 2026-05-29 (recorded in the Logbook 2026-06-26)
- Deciders: Ant

## Context

After the .NET 10 overhaul (see [ADR-0001](0001-net10-taglib-png-cache.md)), the screensaver showed a black screen on the operator's Windows 11 / 4K@150% (PerMonitorV2) box, both via idle activation and the tray "Test" entry, even though the run log was healthy (covers loaded, form visible, tiles created). The investigation (`.claude/notes.md`, 2026-05-29) established that on this machine's .NET 10 runtime, child controls of the screensaver form never composite to the screen: the form's own surface paints, but its children do not. This held across every variable tested (custom `UserPaint` control vs stock `PictureBox` vs stock `Label`; PerMonitorV2 vs DPI-unaware; every window style). Win32 confirmed the children were genuine, visible, correctly-parented windows that [DWM](https://learn.microsoft.com/windows/win32/dwm/dwm-overview) simply did not composite. The regression entered with commit 9ecd67b, which replaced stock `PictureBox` tiles with child controls.

## Decision

We will render the entire screensaver UI owner-draw: `ScreenSaverForm.OnPaint` composes the mosaic, the now-playing focal cover, captions, the manage hint, and the hide-toast into a single back-buffer bitmap that is blitted in one `DrawImageUnscaled`, with no child controls. `FadingPictureBox.cs` holds `FadingTile`, a plain renderer (not a `Control`) carrying the transition math and a `Paint(Graphics, Rectangle)` method. One form-level animation timer (roughly 16 to 33 ms) advances transitions and highlight pulses and invalidates only the dirty tile rects. Hit-testing maps mouse coordinates to a tile index by hand.

## Considered options

- **Keep child `PictureBox` / control tiles** (the pre-overhaul design). Rejected: proven not to composite on this .NET 10 runtime across every control type and window style tested.
- **Chase the `OptimizedDoubleBuffer` / suppressed-`OnPaint` theory.** Rejected as a red herring: whether `OnPaint` fired was irrelevant, because no child window composited at all; the fix had to move all rendering onto the form surface.
- **Owner-draw into a back-buffer with a non-control `FadingTile` renderer (chosen).**

## Consequences

- Rendering is reliable and transitions are smooth at full 3840x2160; invalidating only dirty rects avoids clearing a 4K surface every frame.
- Negative: all layout and hit-testing are manual rather than delegated to WinForms, so adding an interactive element means writing its paint and mouse-mapping code by hand.
- Verification cannot trust `Form.DrawToBitmap` (it renders the control tree and bypasses the broken on-screen path); a real `Graphics.CopyFromScreen` capture is the only faithful check (a black 4K frame is roughly 17 KB, a real mosaic is several MB). See `.claude/notes.md`.

## References

- Code: `ScreenSaver/ScreenSaverForm.cs`, `ScreenSaver/FadingPictureBox.cs`, `ScreenSaver/Program.cs`
- Investigation: `.claude/notes.md` (2026-05-29)
- Recorded in the Journal of `docs/index.html` (rev 1, 2026-06-26).
