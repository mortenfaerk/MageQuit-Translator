"""Merge extracted strings into translation/strings.json (the canonical translation file).

Usage: python build_candidates.py <assets.json> <code.json> <strings.json>

Existing entries (and their Danish text/status) are preserved; new English strings
are appended with status "new". Each entry records where the string was seen so
the translator has context.
"""
import json
import os
import re
import sys

# Fonts used only by the SRDebugger developer overlay; never shown to players.
DEBUG_FONTS = {"Orbitron Medium", "Orbitron Bold", "Orbitron Light", "Orbitron Black", "SourceCodePro-Regular"}
UPPERCASE_ONLY_FONTS = {"MageQuitHeaderThin", "soupofjustice"}
SKIP_FIELD = re.compile(r"TypeName|animationName|m_Persistent|m_Method|[Pp]ath|[Uu]rl|[Ss]hader")


def keep(text):
    t = text.strip()
    if not re.search(r"[A-Za-z]", t):
        return False
    if re.fullmatch(r"[\d\s.,:+\-/%xX]+[A-Za-z]{0,2}", t):     # numbers, sizes, times
        return False
    return True


def main(assets_path, code_path, out_path):
    entries = {}
    if os.path.exists(out_path):
        for e in json.load(open(out_path, encoding="utf-8"))["strings"]:
            entries[e["en"]] = e

    def add(text, source, font=None, confidence="high"):
        if not keep(text):
            return
        e = entries.get(text)
        if e is None:
            e = entries[text] = {"en": text, "da": "", "status": "new", "confidence": confidence,
                                 "sources": [], "fonts": [], "note": ""}
        elif confidence == "high" or e.get("confidence") == "low" and confidence == "enum":
            e["confidence"] = confidence if e.get("confidence") != "high" else "high"
        if source not in e["sources"] and len(e["sources"]) < 5:
            e["sources"].append(source)
        if font and font not in e["fonts"]:
            e["fonts"].append(font)

    for r in json.load(open(assets_path, encoding="utf-8")):
        if r["font"] in DEBUG_FONTS:
            continue
        if r.get("field"):
            if SKIP_FIELD.search(r["field"]):
                continue
            add(r["text"], f'{r["component"]}.{r["field"]} ({r["object"]})')
        else:
            add(r["text"], f'{r["file"]}: {r["object"]}', r["font"])

    for r in json.load(open(code_path, encoding="utf-8")):
        src = r["code"] if r["confidence"] == "enum" else f'{r["file"]}:{r["line"]}'
        add(r["text"], src, confidence=r["confidence"])

    for e in entries.values():
        e["uppercase_only"] = any(f in UPPERCASE_ONLY_FONTS for f in e["fonts"])

    order = {"high": 0, "enum": 1, "low": 2}
    strings = sorted(entries.values(), key=lambda e: (order.get(e["confidence"], 3), e["en"].lower()))
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with open(out_path, "w", encoding="utf-8", newline="\n") as f:
        json.dump({"language": "da", "source_language": "en", "strings": strings}, f, ensure_ascii=False, indent=1)
    print(f"{len(strings)} entries ({sum(1 for e in strings if e['confidence'] == 'low')} low-confidence) -> {out_path}")


if __name__ == "__main__":
    main(*sys.argv[1:4])
