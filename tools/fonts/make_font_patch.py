"""Build (and optionally apply/revert) the Danish font patch for MageQuit.

MageQuit's UGUI text renders from TTF data embedded in Unity `Font` objects. Two of
those fonts lack Æ Ø Å æ ø å. This tool extracts each font, adds the letters with
build_glyphs.py, and produces a patch descriptor that the Manager applies by
*appending* a replacement Font object to the asset file and repointing that
object's entry in the file's object table. Reverting = restore the entry, restore
the header file size, truncate. No game data is shipped: the new object is stored
as a delta against the original (copy ranges + inserted bytes).

Usage:
  python make_font_patch.py build <game dir> payload/game/BepInEx/MageQuit-DA/fontpatch.json
  python make_font_patch.py apply <game dir> <patch.json>     (test only; Manager does this)
  python make_font_patch.py revert <game dir> <patch.json>
"""
import base64
import difflib
import hashlib
import io
import json
import os
import struct
import sys
import tempfile

import UnityPy

sys.path.insert(0, os.path.dirname(__file__))
import build_glyphs  # noqa: E402

TARGETS = [
    # (asset file, Font object name, glyph recipe)
    ("MageQuit_Data/resources.assets", "MageQuit-Body", "MageQuit-Body"),
    ("MageQuit_Data/sharedassets1.assets", "MageQuitHeaderThin", "MageQuitHeaderThin"),
]


def sha256(b):
    return hashlib.sha256(b).hexdigest()


def read_header(f):
    f.seek(0)
    metadata_size, file_size, version, data_offset = struct.unpack(">IIII", f.read(16))
    if version != 17:
        raise SystemExit(f"Unsupported SerializedFile version {version}")
    return file_size, data_offset


def make_delta(old, new):
    ops = []
    sm = difflib.SequenceMatcher(None, old, new, autojunk=False)
    for tag, i1, i2, j1, j2 in sm.get_opcodes():
        if tag == "equal":
            ops.append({"copy": [i1, i2 - i1]})
        elif j2 > j1:
            ops.append({"insert": base64.b64encode(new[j1:j2]).decode()})
    return ops


def apply_delta(old, ops):
    out = bytearray()
    for op in ops:
        if "copy" in op:
            off, n = op["copy"]
            out += old[off:off + n]
        else:
            out += base64.b64decode(op["insert"])
    return bytes(out)


def build(game_dir, out_path):
    patches = []
    for rel, font_name, recipe in TARGETS:
        path = os.path.join(game_dir, rel)
        env = UnityPy.load(path)
        obj = next(o for o in env.objects if o.type.name == "Font" and o.read().m_Name == font_name)
        raw = bytes(obj.get_raw_data())
        ttf = bytes(obj.read().m_FontData)
        # m_FontData is serialized as int32 length + bytes + 4-byte alignment.
        at = raw.find(struct.pack("<I", len(ttf)) + ttf[:64])
        assert at >= 0, "font data not found in object"
        end = at + 4 + len(ttf)
        end += (-end) % 4

        with tempfile.TemporaryDirectory() as tmp:
            src, dst = os.path.join(tmp, "in.ttf"), os.path.join(tmp, "out.ttf")
            open(src, "wb").write(ttf)
            build_glyphs.build(src, recipe, dst)
            new_ttf = open(dst, "rb").read()
        new_raw = raw[:at] + struct.pack("<I", len(new_ttf)) + new_ttf + b"\0" * ((-len(new_ttf)) % 4) + raw[end:]

        with open(path, "rb") as f:
            file_size, data_offset = read_header(f)
            f.seek(0)
            head = f.read(data_offset)
        rel_start = obj.byte_start - data_offset  # UnityPy reports absolute offsets
        entry = struct.pack("<qII", obj.path_id, rel_start, obj.byte_size)
        entry_offset = head.find(entry)
        assert entry_offset > 0 and head.find(entry, entry_offset + 1) < 0, "object table entry not unique"

        patches.append({
            "file": rel.replace("\\", "/"),
            "font": font_name,
            "pathId": obj.path_id,
            "entryOffset": entry_offset,
            "origFileSize": file_size,
            "dataOffset": data_offset,
            "origByteStart": rel_start,
            "origByteSize": obj.byte_size,
            "fontDataOffset": at,  # int32 length + TTF bytes start here, in both old and new object
            "origObjectSha256": sha256(raw),
            "newObjectSha256": sha256(new_raw),
            "delta": make_delta(raw, new_raw),
        })
        print(f"{font_name}: object {len(raw)} -> {len(new_raw)} bytes, delta ops {len(patches[-1]['delta'])}")
    with open(out_path, "w", encoding="utf-8", newline="\n") as f:
        json.dump({"format": 1, "game": "MageQuit", "unity": "2018.4.0f1", "patches": patches}, f, indent=1)


def apply(game_dir, patch_path):
    for p in json.load(open(patch_path))["patches"]:
        path = os.path.join(game_dir, p["file"])
        with open(path, "r+b") as f:
            file_size, data_offset = read_header(f)
            f.seek(p["entryOffset"])
            pid, start, size = struct.unpack("<qII", f.read(16))
            if file_size != p["origFileSize"] or (pid, start, size) != (p["pathId"], p["origByteStart"], p["origByteSize"]):
                print(f"{p['font']}: file not in original state, skipping")
                continue
            f.seek(data_offset + start)
            raw = f.read(size)
            assert sha256(raw) == p["origObjectSha256"], "original object hash mismatch"
            new_raw = apply_delta(raw, p["delta"])
            assert sha256(new_raw) == p["newObjectSha256"]
            new_start = file_size - data_offset
            new_start += (-new_start) % 16
            f.seek(data_offset + new_start)
            f.write(b"\0" * 0)
            f.truncate(data_offset + new_start)
            f.write(new_raw)
            new_size = f.tell()
            f.seek(p["entryOffset"] + 8)
            f.write(struct.pack("<II", new_start, len(new_raw)))
            f.seek(4)
            f.write(struct.pack(">I", new_size))
        print(f"{p['font']}: patched")


def revert(game_dir, patch_path):
    for p in json.load(open(patch_path))["patches"]:
        path = os.path.join(game_dir, p["file"])
        with open(path, "r+b") as f:
            f.seek(p["entryOffset"])
            pid, start, size = struct.unpack("<qII", f.read(16))
            if pid != p["pathId"]:
                print(f"{p['font']}: unexpected table layout, skipping")
                continue
            f.seek(p["entryOffset"] + 8)
            f.write(struct.pack("<II", p["origByteStart"], p["origByteSize"]))
            f.seek(4)
            f.write(struct.pack(">I", p["origFileSize"]))
            f.truncate(p["origFileSize"])
        print(f"{p['font']}: reverted")


if __name__ == "__main__":
    {"build": build, "apply": apply, "revert": revert}[sys.argv[1]](sys.argv[2], sys.argv[3])
