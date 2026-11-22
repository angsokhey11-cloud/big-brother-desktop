#!/usr/bin/env python3
"""Build approved BIG BROTHER Windows icon at genuine Windows icon resolutions.

The two-panel source is the user-selected BB accounting mark (left) and its
simplified small-taskbar variant (right). This is build-time only.
"""
from __future__ import annotations

from hashlib import sha256
from io import BytesIO
from pathlib import Path
import struct
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "assets" / "desktop-icon-art.webp"
TARGET = ROOT / "build" / "icon" / "big-brother.ico"
PREVIEW = ROOT / "build" / "icon" / "big-brother-preview.png"
SMALL_PREVIEW = ROOT / "build" / "icon" / "big-brother-small-preview.png"
EXPECTED_HASH = "bd0841b7323ba759f92412458c04e3039dbabb7f142f667128282f80498d79b6"
SIZES = (16, 24, 32, 48, 64, 128, 256)


def create() -> None:
    if not SOURCE.is_file():
        raise FileNotFoundError("Approved BIG BROTHER Desktop icon missing from assets/")
    actual = sha256(SOURCE.read_bytes()).hexdigest()
    if actual != EXPECTED_HASH:
        raise ValueError("Unapproved Desktop icon artwork changed: re-review before release")
    with Image.open(SOURCE) as sheet:
        if sheet.size != (384, 192):
            raise ValueError("Unexpected master/small artwork dimensions")
        full = sheet.convert("RGBA").crop((0, 0, 192, 192))
        simplified = sheet.convert("RGBA").crop((192, 0, 384, 192))

    TARGET.parent.mkdir(parents=True, exist_ok=True)
    full.resize((512, 512), Image.Resampling.LANCZOS).save(PREVIEW)
    simplified.resize((128, 128), Image.Resampling.LANCZOS).save(SMALL_PREVIEW)
    chunks = []
    for sz in SIZES:
        frame = (simplified if sz <= 32 else full).resize(
            (sz, sz), Image.Resampling.LANCZOS)
        if sz <= 32:
            frame = frame.filter(ImageFilter.UnsharpMask(
                radius=0.65, percent=120, threshold=3))
        buffer = BytesIO()
        frame.save(buffer, "PNG", optimize=True)
        data = buffer.getvalue()
        if len(data) < 100 or not data.startswith(b"\x89PNG"):
            raise ValueError(f"Invalid icon frame: {sz}x{sz}")
        chunks.append((sz, data))

    # Explicit ICO directory allows different artwork at small and large
    # resolutions, unlike PIL's ordinary single-source .ico export.
    directory = bytearray(struct.pack("<HHH", 0, 1, len(chunks)))
    offset = 6 + len(chunks) * 16
    payload = bytearray()
    for sz, data in chunks:
        byte_size = 0 if sz == 256 else sz
        directory.extend(struct.pack("<BBBBHHII",
                                     byte_size, byte_size,
                                     0, 0, 1, 32, len(data), offset))
        offset += len(data)
        payload.extend(data)
    TARGET.write_bytes(bytes(directory) + bytes(payload))

    with Image.open(TARGET) as check:
        actual_sizes = set(check.info["sizes"])
    if actual_sizes != {(x, x) for x in SIZES}:
        raise RuntimeError(f"ICO sizes differ from approved frame sizes: {actual_sizes}")
    print(f"BIG BROTHER Windows icon: {TARGET} ({TARGET.stat().st_size:,} bytes)")
    print("16–32px simplified; 48–256px detailed BB accounting mark.")


if __name__ == "__main__":
    create()
