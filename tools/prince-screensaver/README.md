# Prince screen-saver GIF for the Stream Deck

When Windows locks, Elgato puts the Stream Deck to sleep and shows its
**screen saver** image (the Elgato logo by default) instead of running your
profile. Keys do **not** execute while locked on this machine — so the only
lock-screen customization possible is replacing that image. This tool builds a
GIF that rotates through album covers so the locked deck shows Prince art
instead of the logo.

## Build

```
python make_gif.py
```

Requires [Pillow](https://pypi.org/project/Pillow/) (`pip install Pillow`).
Reads the cover cache the suite already maintains at
`%USERPROFILE%\ScreenSaverCovers\` and writes
`%LOCALAPPDATA%\ScreenSaver\prince-screensaver.gif`.

Defaults: every album whose artist is exactly **Prince**, one per frame,
**2 s** each, looping, chronological by release year. Re-run whenever the
collection grows.

Options: `--artist "Name"`, `--seconds 2`, `--out path.gif`.

## Install on the deck (one-time, GUI)

Stream Deck app → **Preferences** → **Screen Saver**: set the image to the
generated GIF and pick a "Sleep after" delay. It then shows whenever the deck
sleeps, including when Windows is locked.

## Format

480×272 (the original 15-key Stream Deck's LCD panel). Each frame is the square
cover scaled to the panel height and centered, over a blurred, darkened copy of
the same cover that fills the wide side margins — undistorted and uncropped.
