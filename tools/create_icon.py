#!/usr/bin/env python3
"""Create a multi-resolution Windows ICO matching BIG BROTHER's existing PWA logo.

Build-time only. Uses bundled, pinned Dashboard SVG as the approved brand
reference, with Pillow so Windows CI does not need a native SVG rasterizer.
"""
from __future__ import annotations

from pathlib import Path
import math
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
SVG = ROOT / "build" / "web" / "big-brother-dashboard" / "desktop-pwa-icon.svg"
TARGET = ROOT / "build" / "icon" / "big-brother.ico"
PREVIEW = ROOT / "build" / "icon" / "big-brother-preview.png"


def load_font(size: int) -> ImageFont.FreeTypeFont:
    choices = [
        Path(r"C:\Windows\Fonts\ariblk.ttf"),
        Path(r"C:\Windows\Fonts\arialbd.ttf"),
        Path("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"),
    ]
    for path in choices:
        if path.is_file():
            return ImageFont.truetype(str(path), size)
    raise RuntimeError("Cannot find a bold font to render BIG BROTHER icon")


def create() -> None:
    if not SVG.is_file():
        raise RuntimeError("Approved Dashboard PWA logo not included in the frontend bundle")
    brand = SVG.read_text(encoding="utf-8")
    if 'viewBox="0 0 512 512"' not in brand or 'fill="#ffffff">BB</text>' not in brand:
        raise RuntimeError("Unexpected Dashboard logo design: review before changing Desktop branding")

    s = 3
    dim = 512 * s
    image = Image.new("RGBA", (dim, dim), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    top = (23, 63, 119)
    bottom = (11, 44, 85)

    # Match the existing Dashboard icon: navy diagonal background,
    # cyan inner outline, big white BB lettering and an upward chart.
    for y in range(dim):
        for_x = y / max(1, dim - 1)
        # Layered small stripes are blended into a rounded mask below.
        color = tuple(round(top[i] + (bottom[i] - top[i]) * for_x) for i in range(3))
        draw.line((0, y, dim, y), fill=(*color, 255))
    rounded = Image.new("L", (dim, dim), 0)
    mask_draw = ImageDraw.Draw(rounded)
    mask_draw.rounded_rectangle((0, 0, dim - 1, dim - 1), radius=108 * s, fill=255)
    image.putalpha(rounded)
    draw = ImageDraw.Draw(image)

    draw.rounded_rectangle(
        (36 * s, 36 * s, 476 * s, 476 * s),
        radius=82 * s,
        outline=(95, 183, 255, 145),
        width=8 * s,
    )

    font = load_font(228 * s)
    # The primary PWA mark uses very tight, bold white BB initials.
    left = 74 * s
    baseline = 145 * s
    draw.text((left, baseline), "B", font=font, fill="white",
              stroke_width=0)
    draw.text((left + 134 * s, baseline), "B", font=font, fill="white",
              stroke_width=0)

    # The original PWA icon's cyan accounting growth graphic.
    bars = [
        (304, 342, 328, 396, (126, 211, 255, 255)),
        (339, 316, 363, 396, (95, 198, 255, 255)),
        (374, 286, 398, 396, (50, 178, 255, 255)),
    ]
    for x1, y1, x2, y2, color in bars:
        draw.rounded_rectangle((x1 * s, y1 * s, x2 * s, y2 * s),
                               radius=5 * s, fill=color)
    pts = [(297, 397), (326, 384), (350, 365), (374, 344), (408, 313)]
    draw.line([(x * s, y * s) for x, y in pts], fill=(159, 228, 255, 255),
              width=11 * s, joint="curve")
    draw.line([(397 * s, 314 * s), (418 * s, 311 * s), (414 * s, 334 * s)],
              fill="white", width=9 * s, joint="curve")

    TARGET.parent.mkdir(parents=True, exist_ok=True)
    final = image.resize((512, 512), Image.Resampling.LANCZOS)
    final.save(PREVIEW)
    # Windows Explorer, Start Menu, installer, and taskbar use different sizes.
    final.save(TARGET, format="ICO", sizes=[
        (16, 16), (24, 24), (32, 32), (48, 48),
        (64, 64), (128, 128), (256, 256),
    ])
    if TARGET.stat().st_size < 1000:
        raise RuntimeError("Windows icon generation produced a suspiciously small file")
    print(f"BIG BROTHER branded multi-size icon: {TARGET} ({TARGET.stat().st_size:,} bytes)")
    print(f"Brand preview: {PREVIEW}")


if __name__ == "__main__":
    create()
