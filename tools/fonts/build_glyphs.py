"""Add Danish letters (Æ Ø Å æ ø å) to MageQuit fonts that lack them.

The new glyphs are composed from each font's own outlines (A+E, O+slash,
A+small o as ring), so they keep the hand-drawn look. Outlines are flattened
into simple glyphs; the font's vertical metrics are left untouched so line
layout in the game does not shift.

Usage: python build_glyphs.py <in.ttf> <recipe-name> <out.ttf>
"""
import sys

from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont

# Each recipe: new glyph -> (advance-width expression, [(source char, (xx, xy, yx, yy, dx, dy))]).
# Transforms are in font units; "adv" may reference a callable taking the bounds helper.
RECIPES = {
    "MageQuit-Body": {
        "Oslash": ("O", [("O", (1, 0, 0, 1, 0, 0)), ("/", (0.72, 0, 0, 1.0, 26, 0))]),
        "oslash": ("o", [("o", (1, 0, 0, 1, 0, 0)), ("/", (0.66, 0, 0, 0.74, 25, -58))]),
        # Capital ring: shrink the A vertically so the ring stays inside the ascent.
        "Aring": ("A", [("A", (1, 0, 0, 0.82, 0, -36)), ("o", (0.32, 0, 0, 0.30, 204, 712))]),
        "aring": ("a", [("a", (1, 0, 0, 1, 0, 0)), ("o", (0.34, 0, 0, 0.32, 185, 590))]),
        "AE": (("A", "E", 360), [("A", (1, 0, 0, 1, 0, 0)), ("E", (1, 0, 0, 1, 360, 0))]),
        "ae": (("a", "e", 330), [("a", (1, 0, 0, 1, 0, 0)), ("e", (1, 0, 0, 1, 330, 0))]),
    },
    "MageQuitHeaderThin": {
        "Oslash": ("O", [("O", (1, 0, 0, 1, 0, 0)), ("/", (0.85, 0, 0, 1.0, 18, 0))]),
        "Aring": ("A", [("A", (1, 0, 0, 0.82, 0, -36)), ("O", (0.28, 0, 0, 0.22, 214, 696))]),
        "AE": (("A", "E", 400), [("A", (1, 0, 0, 1, 0, 0)), ("E", (1, 0, 0, 1, 400, 0))]),
    },
}

CODEPOINTS = {"AE": 0xC6, "Oslash": 0xD8, "Aring": 0xC5, "ae": 0xE6, "oslash": 0xF8, "aring": 0xE5}
# Uppercase-only fonts: map lowercase Danish letters onto the capitals.
LOWER_FALLBACK = {"ae": "AE", "oslash": "Oslash", "aring": "Aring"}


def build(in_path, recipe_name, out_path):
    font = TTFont(in_path)
    cmap = font.getBestCmap()
    gs = font.getGlyphSet()
    glyf, hmtx = font["glyf"], font["hmtx"]
    recipe = RECIPES[recipe_name]

    def gname(ch):
        return cmap[ord(ch)]

    order = font.getGlyphOrder()
    for new, (adv_spec, parts) in recipe.items():
        pen = TTGlyphPen(gs)
        for ch, t in parts:
            gs[gname(ch)].draw(TransformPen(pen, t))
        glyph = pen.glyph()
        if isinstance(adv_spec, tuple):
            a, b, dx = adv_spec
            adv = dx + hmtx[gname(b)][0]
        else:
            adv = hmtx[gname(adv_spec)][0]
        glyf[new] = glyph
        glyph.recalcBounds(glyf)
        hmtx[new] = (adv, glyph.xMin if glyph.numberOfContours else 0)
        if new not in order:
            order.append(new)

    font.setGlyphOrder(order)
    for new in recipe:
        _map(font, CODEPOINTS[new], new)
    for lower, upper in LOWER_FALLBACK.items():
        if lower not in recipe and upper in recipe and ord("a") not in cmap:
            _map(font, CODEPOINTS[lower], upper)
    font["maxp"].numGlyphs = len(order)
    # Keep vertical metrics stable; only the bbox in 'head' is recalculated.
    font.save(out_path)


def _map(font, cp, name):
    for table in font["cmap"].tables:
        if table.isUnicode():
            table.cmap[cp] = name


if __name__ == "__main__":
    build(sys.argv[1], sys.argv[2], sys.argv[3])
