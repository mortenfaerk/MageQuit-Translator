"""Dump every UI text string found in MageQuit's serialized assets.

Scans all scenes (level*) and asset files for UnityEngine.UI.Text and
TextMeshPro components, plus free-text string fields (e.g. Spell.description)
on the game's own scripts, and writes a JSON list of
{text, component, font, field, file, object} records.

Usage: python extract_assets.py <MageQuit install dir> <out.json>
"""
import json
import os
import sys
from collections import Counter

import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

TEXT_CLASSES = {"Text": "m_Text", "TextMeshProUGUI": "m_text", "TextMeshPro": "m_text", "TextMesh": "m_Text"}


class Resolver:
    """Resolves PPtrs across serialized files by external file basename."""

    def __init__(self, env):
        self.by_name = {}
        for sf in _serialized_files(env):
            self.by_name[os.path.basename(sf.name).lower()] = sf

    def get(self, sf, file_id, path_id):
        if not path_id:
            return None
        if file_id == 0:
            target = sf
        elif file_id - 1 < len(sf.externals):
            ext = os.path.basename(sf.externals[file_id - 1].path).lower()
            target = self.by_name.get(ext)
        else:
            return None
        return target.objects.get(path_id) if target else None


def _serialized_files(env):
    for f in env.files.values():
        if type(f).__name__ == "SerializedFile":
            yield f
        elif hasattr(f, "files"):
            yield from (x for x in f.files.values() if type(x).__name__ == "SerializedFile")


def main(game_dir, out_path):
    # Phase 1: identify text components and game scripts from the MonoBehaviour base fields.
    # This must run *before* the typetree generator is attached; with it set,
    # UnityPy misreads the base fields of scene objects in this game.
    env = UnityPy.load(os.path.join(game_dir, "MageQuit_Data"))
    res = Resolver(env)
    script_cache = {}
    targets = []
    for sf in _serialized_files(env):
        for obj in sf.objects.values():
            if obj.type.name != "MonoBehaviour":
                continue
            sp = obj.read(check_read=False).m_Script
            key = (sf.name, sp.m_FileID, sp.m_PathID)
            if key not in script_cache:
                so = res.get(sf, sp.m_FileID, sp.m_PathID)
                ms = so.read() if so else None
                script_cache[key] = (ms.m_ClassName, ms.m_AssemblyName) if ms else None
            cls, asm = script_cache[key] or (None, None)
            if cls in TEXT_CLASSES or asm == "Assembly-CSharp.dll":
                targets.append((sf, obj, cls))

    # Phase 2: parse the text component fields via generated typetrees.
    gen = TypeTreeGenerator("2018.4.0f1")
    gen.load_local_game(game_dir)
    env.typetree_generator = gen
    records = []
    stats = Counter()
    for sf, obj, cls in targets:
        try:
            d = obj.parse_as_dict()
        except Exception:
            stats["unparsed"] += 1
            continue
        if cls not in TEXT_CLASSES:
            for field, value in _free_text_fields(d):
                stats["field"] += 1
                records.append({"text": value, "component": cls, "font": None, "field": field,
                                "file": os.path.basename(sf.name),
                                "object": _name(res.get(sf, *_ids(d.get("m_GameObject"))))})
            continue
        stats[cls] += 1
        text = d.get(TEXT_CLASSES[cls])
        if not text or not text.strip():
            continue
        fref = d.get("m_FontData", {}).get("m_Font") if cls == "Text" else d.get("m_fontAsset")
        records.append({"text": text, "component": cls, "field": None,
                        "font": _name(res.get(sf, *_ids(fref))),
                        "file": os.path.basename(sf.name),
                        "object": _name(res.get(sf, *_ids(d.get("m_GameObject"))))})

    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(records, f, ensure_ascii=False, indent=1)
    print(dict(stats))
    print(f"{len(records)} text records -> {out_path}")


def _free_text_fields(d, prefix=""):
    """Yield (path, value) for string fields that look like prose shown to players."""
    for k, v in d.items():
        if k in ("m_Name", "m_Script", "m_GameObject"):
            continue
        path = f"{prefix}{k}"
        if isinstance(v, str):
            if " " in v.strip() and any(c.isalpha() for c in v) and "/" not in v:
                yield path, v
        elif isinstance(v, dict):
            yield from _free_text_fields(v, path + ".")
        elif isinstance(v, list):
            for i, item in enumerate(v):
                if isinstance(item, dict):
                    yield from _free_text_fields(item, f"{path}[{i}].")
                elif isinstance(item, str) and " " in item.strip():
                    yield f"{path}[{i}]", item


def _ids(ref):
    return (ref.get("m_FileID"), ref.get("m_PathID")) if ref else (0, 0)


def _name(obj):
    try:
        return obj.peek_name() if obj else None
    except Exception:
        return None


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
