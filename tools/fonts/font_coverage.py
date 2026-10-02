"""Write the characters each MageQuit UI font can display (after the glyph patch) to fonts.json.

The Manager uses this to warn translators when a translation contains letters a game
font cannot render (e.g. 'ß' in MageQuit-Body). Only character lists are written;
no font data.

Usage: python font_coverage.py <game dir> <out fonts.json>
"""
import io
import json
import os
import sys
import tempfile

import UnityPy
from fontTools.ttLib import TTFont

sys.path.insert(0, os.path.dirname(__file__))
import build_glyphs  # noqa: E402
from make_font_patch import TARGETS  # noqa: E402

UPPERCASE_ONLY = {"MageQuitHeaderThin", "soupofjustice"}


def main(game_dir, out_path):
    patched = {font: recipe for _, font, recipe in TARGETS}
    env = UnityPy.load(os.path.join(game_dir, "MageQuit_Data"))
    fonts = {}
    for obj in env.objects:
        if obj.type.name != "Font":
            continue
        font = obj.read()
        if not font.m_FontData or font.m_Name in fonts:
            continue
        data = bytes(font.m_FontData)
        if font.m_Name in patched:
            with tempfile.TemporaryDirectory() as tmp:
                src, dst = os.path.join(tmp, "in.ttf"), os.path.join(tmp, "out.ttf")
                open(src, "wb").write(data)
                build_glyphs.build(src, patched[font.m_Name], dst)
                data = open(dst, "rb").read()
        cmap = TTFont(io.BytesIO(data)).getBestCmap()
        fonts[font.m_Name] = {
            "chars": "".join(sorted(chr(c) for c in cmap if c >= 0x20)),
            "uppercaseOnly": font.m_Name in UPPERCASE_ONLY,
        }
    with open(out_path, "w", encoding="utf-8", newline="\n") as f:
        json.dump({"fonts": fonts}, f, ensure_ascii=False, indent=1)
    print(f"{len(fonts)} fonts -> {out_path}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
