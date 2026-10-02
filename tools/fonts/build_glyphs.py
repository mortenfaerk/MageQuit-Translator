"""Add Latin-1 letters (Æ Ø Å Ä Ö Ü É Ñ Ç …) to MageQuit fonts that lack them.

New glyphs are composed from each font's own outlines so they keep the hand-drawn
look:
  * Æ Ø Å are hand-tuned recipes (A+E, O+slash, A + small o as ring).
  * Every other accented letter in U+00C0–U+00FF is built automatically from its
    Unicode decomposition: the base letter plus an accent made from the font's own
    punctuation (`.` for dieresis, `` ` `` for grave/acute (mirrored), `^`, `~`, `,`).
    Capitals are squashed slightly so the accent stays inside the font's ascent;
    accented i's use a dotless i.
Outlines are flattened into simple glyphs; vertical metrics are left untouched so
line layout in the game does not shift.

Usage: python build_glyphs.py <in.ttf> <recipe-name> <out.ttf>
"""
import sys
import unicodedata

from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.recordingPen import RecordingPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont

# Hand-tuned recipes: glyph -> (advance spec, [(source char, (xx, xy, yx, yy, dx, dy))]).
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

# Combining mark -> (accent source char, mirrored, kind)
MARKS = {
    "̀": ("`", False, "above"),     # grave
    "́": ("`", True, "above"),      # acute = mirrored grave
    "̂": ("^", False, "above"),     # circumflex
    "̃": ("~", False, "above"),     # tilde
    "̈": (".", False, "dieresis"),  # dieresis = two dots
    "̧": (",", False, "below"),     # cedilla
}
CAPITAL_SQUASH = 0.82
ACCENT_GAP = 40
ACCENT_MAX_HEIGHT = 170


def build(in_path, recipe_name, out_path):
    font = TTFont(in_path)
    cmap = font.getBestCmap()
    gs = font.getGlyphSet()
    glyf, hmtx = font["glyf"], font["hmtx"]
    order = font.getGlyphOrder()
    added = {}  # codepoint -> glyph name

    def gname(ch):
        return cmap[ord(ch)]

    def add_glyph(name, glyph, adv):
        glyf[name] = glyph
        glyph.recalcBounds(glyf)
        hmtx[name] = (adv, glyph.xMin if glyph.numberOfContours else 0)
        if name not in order:
            order.append(name)

    for new, (adv_spec, parts) in RECIPES.get(recipe_name, {}).items():
        pen = TTGlyphPen(gs)
        for ch, t in parts:
            gs[gname(ch)].draw(TransformPen(pen, t))
        if isinstance(adv_spec, tuple):
            _, b, dx = adv_spec
            adv = dx + hmtx[gname(b)][0]
        else:
            adv = hmtx[gname(adv_spec)][0]
        add_glyph(new, pen.glyph(), adv)
        added[CODEPOINTS[new]] = new

    for cp in range(0xC0, 0x100):
        if cp in cmap or cp in added:
            continue
        decomposed = unicodedata.normalize("NFD", chr(cp))
        if len(decomposed) != 2 or decomposed[1] not in MARKS or ord(decomposed[0]) not in cmap:
            continue
        base, mark = decomposed
        src, mirrored, kind = MARKS[mark]
        if ord(src) not in cmap:
            continue
        name = f"uni{cp:04X}"
        pen = TTGlyphPen(gs)
        _compose(gs, gname, pen, base, src, mirrored, kind)
        add_glyph(name, pen.glyph(), hmtx[gname(base)][0])
        added[cp] = name

    font.setGlyphOrder(order)
    for cp, name in added.items():
        _map(font, cp, name)
    # Uppercase-only fonts: map lowercase letters onto the new capitals.
    if ord("a") not in cmap:
        for cp, name in list(added.items()):
            lower = ord(chr(cp).lower())
            if lower != cp and lower not in added and lower not in cmap:
                _map(font, lower, name)
    font["maxp"].numGlyphs = len(order)
    font.save(out_path)
    return sorted(added)


def _bounds(gs, name_or_recording):
    bp = BoundsPen(gs)
    if isinstance(name_or_recording, RecordingPen):
        name_or_recording.replay(bp)
    else:
        gs[name_or_recording].draw(bp)
    return bp.bounds


def _record(gs, name, drop_dot=False):
    """Record a glyph's outline; with drop_dot, remove contours that sit above the x-height (the i dot)."""
    rec = RecordingPen()
    gs[name].draw(rec)
    if not drop_dot:
        return rec
    contours, cur = [], []
    for op in rec.value:
        cur.append(op)
        if op[0] in ("closePath", "endPath"):
            contours.append(cur)
            cur = []
    lows = []
    for c in contours:
        r = RecordingPen()
        r.value = c
        lows.append(_bounds(gs, r)[1])
    cutoff = min(lows) + 0.55 * (max(lows) - min(lows)) if len(contours) > 1 else None
    out = RecordingPen()
    out.value = [op for c, low in zip(contours, lows) if cutoff is None or low <= cutoff for op in c]
    return out


def _compose(gs, gname, pen, base, src, mirrored, kind):
    capital = base.isupper()
    base_rec = _record(gs, gname(base), drop_dot=(base == "i" and kind != "below"))
    bx0, by0, bx1, by1 = _bounds(gs, base_rec)
    squash = CAPITAL_SQUASH if capital and kind != "below" else 1.0
    base_rec.replay(TransformPen(pen, (1, 0, 0, squash, 0, by0 * (1 - squash))))
    top = by0 + (by1 - by0) * squash
    cx, bw = (bx0 + bx1) / 2, bx1 - bx0

    ax0, ay0, ax1, ay1 = _bounds(gs, gname(src))
    aw, ah = ax1 - ax0, ay1 - ay0
    acc = gs[gname(src)]
    if kind == "dieresis":
        k = min(0.18 * bw / aw, ACCENT_MAX_HEIGHT * 0.6 / ah)
        for offset in (-0.2 * bw, 0.2 * bw):
            dx = cx + offset - k * (ax0 + ax1) / 2
            acc.draw(TransformPen(pen, (k, 0, 0, k, dx, top + ACCENT_GAP - k * ay0)))
    elif kind == "below":
        k = 0.8
        dx = cx - k * (ax0 + ax1) / 2
        acc.draw(TransformPen(pen, (k, 0, 0, k, dx, by0 + 30 - k * ay1)))
    else:
        width_ratio = {"`": 0.45, "^": 0.6, "~": 0.65}[src]
        k = min(width_ratio * max(bw, 250) / aw, ACCENT_MAX_HEIGHT / ah)
        sx = -k if mirrored else k
        dx = cx - sx * (ax0 + ax1) / 2
        acc.draw(TransformPen(pen, (sx, 0, 0, k, dx, top + ACCENT_GAP - k * ay0)))


def _map(font, cp, name):
    for table in font["cmap"].tables:
        if table.isUnicode():
            table.cmap[cp] = name


if __name__ == "__main__":
    added = build(sys.argv[1], sys.argv[2], sys.argv[3])
    print("added:", "".join(chr(c) for c in added))
