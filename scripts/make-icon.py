#!/usr/bin/env python3
"""Draws the app icon (a T-8-style drum machine: red LED display, coloured knobs, step keys) with Pillow.

Usage: python3 scripts/make-icon.py   ->  assets/icon/T8Editor.png (1024 px), .ico and .icns (macOS only).
No logos: the icon only shows the T-8's general look.
"""
import os, shutil, subprocess, sys
from PIL import Image, ImageDraw, ImageFilter

S = 4                      # supersampling
N = 1024 * S
OUT = os.path.join(os.path.dirname(__file__), "..", "assets", "icon")


def s(v):
    return int(round(v * S))


def vgradient(size, top, bottom):
    w, h = size
    g = Image.new("RGBA", (1, h))
    for y in range(h):
        t = y / max(h - 1, 1)
        g.putpixel((0, y), tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,))
    return g.resize((w, h))


def rounded_mask(size, box, radius):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).rounded_rectangle(box, radius, fill=255)
    return m


def seven_segment(d, x, y, w, h, t, on, off):
    """An "8." digit: all segments lit."""
    g = t * 0.25
    segs = [
        (x + t / 2 + g, y, x + w - t / 2 - g, y + t),                         # a
        (x + w - t, y + t / 2 + g, x + w, y + h / 2 - g),                     # b
        (x + w - t, y + h / 2 + g, x + w, y + h - t / 2 - g),                 # c
        (x + t / 2 + g, y + h - t, x + w - t / 2 - g, y + h),                 # d
        (x, y + h / 2 + g, x + t, y + h - t / 2 - g),                         # e
        (x, y + t / 2 + g, x + t, y + h / 2 - g),                             # f
        (x + t / 2 + g, y + h / 2 - t / 2, x + w - t / 2 - g, y + h / 2 + t / 2),  # g
    ]
    for (x0, y0, x1, y1) in segs:
        d.rounded_rectangle((s(x0), s(y0), s(x1), s(y1)), s(t / 2), fill=on)
    dp = t * 1.1
    d.ellipse((s(x + w + t * 0.5), s(y + h - dp), s(x + w + t * 0.5 + dp), s(y + h)), fill=on)


def knob(img, cx, cy, ring, glow):
    # glow under the ring
    halo = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(halo).ellipse((s(cx - 78), s(cy - 78), s(cx + 78), s(cy + 78)), fill=glow)
    img.alpha_composite(halo.filter(ImageFilter.GaussianBlur(s(14))))
    d = ImageDraw.Draw(img)
    # shadow, ring, knob body
    d.ellipse((s(cx - 66), s(cy - 60), s(cx + 66), s(cy + 72)), fill=(0, 0, 0, 110))
    d.ellipse((s(cx - 66), s(cy - 66), s(cx + 66), s(cy + 66)), fill=ring)
    body = vgradient((s(100), s(100)), (92, 92, 98), (28, 28, 32))
    mask = Image.new("L", body.size, 0)
    ImageDraw.Draw(mask).ellipse((0, 0, body.size[0] - 1, body.size[1] - 1), fill=255)
    img.paste(body, (s(cx - 50), s(cy - 50)), mask)
    d.ellipse((s(cx - 50), s(cy - 50), s(cx + 50), s(cy + 50)), outline=(15, 15, 18, 255), width=s(3))
    # pointer at about 11 o'clock
    d.line((s(cx - 14), s(cy - 30), s(cx - 6), s(cy - 12)), fill=(235, 235, 240, 255), width=s(7))


def draw():
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))

    # Body: a rounded square like macOS icons (824 px inside a 1024 canvas), with a drop shadow.
    box = (100, 100, 924, 924)
    shadow = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle((s(100), s(116), s(924), s(940)), s(185), fill=(0, 0, 0, 150))
    img.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(s(16))))
    body = vgradient((N, N), (112, 112, 118), (58, 58, 63))
    img.paste(body, (0, 0), rounded_mask((N, N), tuple(s(v) for v in box), s(185)))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle(tuple(s(v) for v in box), s(185), outline=(30, 30, 34, 255), width=s(6))
    # top highlight
    d.rounded_rectangle((s(112), s(112), s(912), s(912)), s(175), outline=(160, 160, 168, 90), width=s(4))

    # Red LED display
    d.rounded_rectangle((s(190), s(190), s(834), s(400)), s(28), fill=(16, 12, 12, 255),
                        outline=(10, 10, 12, 255), width=s(5))
    glow = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    lit = (255, 38, 38, 255)
    for i in range(4):
        seven_segment(gd, 236 + i * 148, 228, 96, 136, 20, lit, None)
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(s(10))))
    img.alpha_composite(glow)
    d = ImageDraw.Draw(img)

    # Four knobs with the T-8's coloured rings
    rings = [((226, 32, 32, 255), (255, 40, 40, 90)), ((240, 140, 20, 255), (255, 150, 30, 80)),
             ((240, 214, 30, 255), (255, 225, 40, 70)), ((236, 236, 240, 255), (255, 255, 255, 50))]
    for i, (ring, glowc) in enumerate(rings):
        knob(img, 245 + i * 178, 545, ring, glowc)
    d = ImageDraw.Draw(img)

    # Step keys
    d.rounded_rectangle((s(176), s(684), s(848), s(694)), s(4), fill=(200, 200, 206, 255))
    for i in range(6):
        x = 190 + i * 109
        d.rounded_rectangle((s(x), s(714 + 8), s(x + 82), s(840 + 8)), s(12), fill=(0, 0, 0, 120))
        key = vgradient((s(82), s(126)), (250, 250, 252), (196, 196, 204))
        img.paste(key, (s(x), s(714)), rounded_mask(key.size, (0, 0, key.size[0] - 1, key.size[1] - 1), s(12)))
        d.rounded_rectangle((s(x), s(714), s(x + 82), s(840)), s(12), outline=(40, 40, 46, 255), width=s(4))
        if i == 0:
            d.rounded_rectangle((s(x + 22), s(800), s(x + 60), s(814)), s(4), fill=(255, 122, 26, 255))

    return img.resize((1024, 1024), Image.LANCZOS)


def main():
    os.makedirs(OUT, exist_ok=True)
    icon = draw()
    png = os.path.join(OUT, "T8Editor.png")
    icon.save(png)
    icon.resize((256, 256), Image.LANCZOS).save(os.path.join(OUT, "T8Editor-256.png"))
    icon.save(os.path.join(OUT, "T8Editor.ico"), sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    if sys.platform == "darwin" and shutil.which("iconutil"):
        iconset = os.path.join(OUT, "T8Editor.iconset")
        os.makedirs(iconset, exist_ok=True)
        for size in (16, 32, 128, 256, 512):
            icon.resize((size, size), Image.LANCZOS).save(os.path.join(iconset, f"icon_{size}x{size}.png"))
            icon.resize((size * 2, size * 2), Image.LANCZOS).save(os.path.join(iconset, f"icon_{size}x{size}@2x.png"))
        subprocess.run(["iconutil", "-c", "icns", iconset, "-o", os.path.join(OUT, "T8Editor.icns")], check=True)
        shutil.rmtree(iconset)
    print("Icon written to", os.path.abspath(OUT))


if __name__ == "__main__":
    main()
