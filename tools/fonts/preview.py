"""Render a preview sheet of original vs. patched fonts.

Usage: python preview.py <out.png> <font.ttf>...
"""
import sys

from PIL import Image, ImageDraw, ImageFont

SAMPLES = ["ÆØÅ æøå  Spiller Øvelse Åben Kæmp", "SÆT DIG PÅ HOLD ØV FÆRDIGHEDER", "AE O/ A a e o"]


def main(out, fonts):
    img = Image.new("RGB", (1500, 80 * len(fonts) * len(SAMPLES)), "white")
    dr = ImageDraw.Draw(img)
    y = 5
    for path in fonts:
        f = ImageFont.truetype(path, 52)
        for s in SAMPLES:
            dr.text((10, y), s, font=f, fill="black")
            y += 80
    img.save(out)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2:])
