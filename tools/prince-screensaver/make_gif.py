#!/usr/bin/env python3
"""
Build an animated GIF that cycles one album cover at a time, for use as the
Stream Deck's lock-screen / screen-saver image (Stream Deck app -> Preferences
-> Screen Saver). When Windows locks, Elgato sleeps the deck and shows this
screen-saver image instead of running the profile, so this is the only way to
get album art (rather than the Elgato logo) on the keys while locked. Key
presses do NOT run while locked on this setup - this is purely visual.

Defaults reproduce the requested behaviour: every album whose artist is exactly
"Prince", one per frame, 2 seconds each, looping forever.

Canvas is 480x272 - the native single-LCD panel resolution of the original
15-key Stream Deck (Classic / MK.2); the deck shows this image through the key
windows. Each frame is the square cover scaled to fit the panel height and
centred, over a blurred-and-darkened copy of the same cover that fills the wide
side margins, so nothing is distorted or cropped.

Source covers come from the local cache the screensaver suite already builds:
  %USERPROFILE%\\ScreenSaverCovers\\{N.png, N.json}   (N.json has Artist/Album/Year)

Re-run this whenever the Prince collection grows. Requires Pillow
(pip install Pillow).
"""

import argparse
import json
import os
import sys

from PIL import Image, ImageFilter, ImageEnhance

CACHE = os.path.join(os.path.expanduser("~"), "ScreenSaverCovers")
DEFAULT_OUT = os.path.join(
    os.environ.get("LOCALAPPDATA", os.path.expanduser("~")),
    "ScreenSaver", "prince-screensaver.gif")

CANVAS_W, CANVAS_H = 480, 272   # original Stream Deck LCD panel
BG_BLUR = 14
BG_BRIGHTNESS = 0.45


def collect(artist_filter):
    """Return [(year, album, png_path)] for covers whose artist matches."""
    want = artist_filter.strip().lower()
    out = []
    for name in os.listdir(CACHE):
        if not name.endswith(".json"):
            continue
        stem = os.path.splitext(name)[0]
        png = os.path.join(CACHE, stem + ".png")
        if not os.path.isfile(png):
            continue
        try:
            meta = json.load(open(os.path.join(CACHE, name), encoding="utf-8", errors="replace"))
        except Exception:
            continue
        if (meta.get("Artist") or "").strip().lower() != want:
            continue
        year = meta.get("Year") or 0
        out.append((year if year else 9999, meta.get("Album") or stem, png))
    # Chronological by release year, then album title.
    out.sort(key=lambda t: (t[0], t[1].lower()))
    return out


def render_frame(png_path):
    """One 480x272 frame: sharp square cover centred over a blurred fill."""
    cover = Image.open(png_path).convert("RGB")

    # Background: cover-fit (fill canvas, centre-crop), blurred + darkened.
    scale = max(CANVAS_W / cover.width, CANVAS_H / cover.height)
    bw, bh = max(1, round(cover.width * scale)), max(1, round(cover.height * scale))
    bg = cover.resize((bw, bh), Image.LANCZOS).crop((
        (bw - CANVAS_W) // 2, (bh - CANVAS_H) // 2,
        (bw - CANVAS_W) // 2 + CANVAS_W, (bh - CANVAS_H) // 2 + CANVAS_H))
    bg = bg.filter(ImageFilter.GaussianBlur(BG_BLUR))
    bg = ImageEnhance.Brightness(bg).enhance(BG_BRIGHTNESS)

    # Foreground: full square cover scaled to the panel height, centred.
    side = CANVAS_H
    fg = cover.resize((side, side), Image.LANCZOS)
    x = (CANVAS_W - side) // 2
    bg.paste(fg, (x, 0))

    # Thin separating frame around the sharp cover.
    from PIL import ImageDraw
    d = ImageDraw.Draw(bg)
    d.rectangle([x, 0, x + side - 1, side - 1], outline=(0, 0, 0), width=2)
    return bg


def main():
    ap = argparse.ArgumentParser(description="Build a rotating album-cover GIF for the Stream Deck screen saver.")
    ap.add_argument("--artist", default="Prince", help='exact artist match (default "Prince")')
    ap.add_argument("--seconds", type=float, default=2.0, help="seconds per cover (default 2)")
    ap.add_argument("--out", default=DEFAULT_OUT, help="output GIF path")
    args = ap.parse_args()

    if not os.path.isdir(CACHE):
        sys.exit(f"cover cache not found: {CACHE}")

    items = collect(args.artist)
    if not items:
        sys.exit(f'no covers found for artist == "{args.artist}"')

    print(f'{len(items)} covers for "{args.artist}":')
    for year, album, _ in items:
        print(f"  {year if year != 9999 else '----'}  {album}")

    frames = [render_frame(p).convert("P", palette=Image.ADAPTIVE, colors=256) for _, _, p in items]

    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    frames[0].save(
        args.out, save_all=True, append_images=frames[1:],
        duration=int(args.seconds * 1000), loop=0, disposal=2, optimize=False)
    size = os.path.getsize(args.out)
    print(f"\nwrote {args.out}  ({len(frames)} frames, {CANVAS_W}x{CANVAS_H}, {size/1_000_000:.1f} MB)")


if __name__ == "__main__":
    main()
