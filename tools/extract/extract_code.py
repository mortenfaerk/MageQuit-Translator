"""Harvest player-facing string literals from MageQuit's decompiled code.

Decompile first:  ilspycmd -p -o <decomp dir> MageQuit_Data/Managed/Assembly-CSharp.dll
Usage: python extract_code.py <decomp dir> <out.json>

Writes [{text, file, line, code, confidence}] where confidence is "high" when the
literal sits on a line that assigns UI text, and "low" otherwise. Also emits
display names for enums that the game shows via GameUtility.AddSpaces().
"""
import json
import os
import re
import sys

THIRD_PARTY_DIRS = ("AmplifyColor", "ExitGames", "LPWAsset", "LowPolyWater", "Photon", "PigeonCoop",
                    "Rewired", "SRDebugger", "SRF", "UnityEngine.PostProcessing", "Properties")
THIRD_PARTY_FILES = re.compile(r"^(CFX|Photon|Pun|Steam|Discord|Amplify|SR|Network|Room|Lobby)", re.I)
LITERAL = re.compile(r'(?<![@$\w])"((?:[^"\\\n]|\\.)*)"')
UI_LINE = re.compile(r"\.text\s*\+?=|\.SetText\(|[Mm]essage|[Tt]itle|[Dd]escription|[Tt]ooltip|Popup|Show\w*\(|return\s+\"")
SKIP_LINE = re.compile(r"Debug\.Log|Log(Warning|Error)?\(|Exception\(|PlayerPrefs|Animator|SetTrigger|SetBool|SetFloat|"
                       r"GetInt|SetInt|Instantiate\(|Resources\.Load|Find\(|CompareTag|tag ==|Shader|\.Play\(|"
                       r"RPC\(|PhotonNetwork|Invoke\(|StartCoroutine|PostEvent|RuntimeManager|EventInstance|"
                       r"GetComponent|GetType|LayerMask|layer|Input\.Get|GetButton|GetAxis|url|http")
# Enums the game turns into visible labels with GameUtility.AddSpaces(x.ToString()).
DISPLAY_ENUMS = ("SpellName", "Element", "StageName", "Stage", "GameMode", "SpellSelectionMode", "ElementName")


def add_spaces(s):
    s = re.sub(r"((?<=\p{Ll})\p{Lu})|((?!\A)\p{Lu}(?>\p{Ll}))".replace(r"\p{Ll}", "[a-z]").replace(r"\p{Lu}", "[A-Z]")
               .replace("(?>", "(?:"), r" \g<0>", s)
    return re.sub(r"([0-9]+)", r" \1", s).replace("  ", " ").strip()


def is_candidate(text):
    if not re.search(r"[A-Za-z]{2}", text):
        return False
    if re.fullmatch(r"[a-z_][A-Za-z0-9_]*", text):          # identifiers / keys
        return False
    if re.fullmatch(r"[A-Za-z0-9_]+([./:][A-Za-z0-9_ ]+)+", text):  # paths, dotted names
        return False
    if text.startswith(("<", "{", "#", "_")) and " " not in text:
        return False
    if re.fullmatch(r"[A-Z0-9_]*_[A-Z0-9_]*", text):          # shader keywords / constants
        return False
    if " " not in text and ("/" in text or "(" in text or "-" in text):  # sound events, calls
        return False
    return True


def main(decomp_dir, out_path):
    records = []
    enum_names = {}
    for root, dirs, files in os.walk(decomp_dir):
        rel = os.path.relpath(root, decomp_dir)
        if rel != "." and rel.split(os.sep)[0].startswith(THIRD_PARTY_DIRS):
            continue
        for fn in files:
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(root, fn)
            src = open(path, encoding="utf-8", errors="replace").read()
            m = re.search(r"public enum (\w+)\s*(?::\s*\w+\s*)?\{([^}]*)\}", src)
            if m and m.group(1) in DISPLAY_ENUMS:
                enum_names[m.group(1)] = [re.sub(r"\s*=.*", "", v).strip()
                                          for v in m.group(2).split(",") if v.strip()]
            if THIRD_PARTY_FILES.match(fn):
                continue
            for i, line in enumerate(src.splitlines(), 1):
                ui = UI_LINE.search(line)
                if SKIP_LINE.search(line) and not (ui and ".text" in ui.group(0)):
                    continue
                for lit in LITERAL.findall(line):
                    text = bytes(lit, "utf-8").decode("unicode_escape", errors="ignore") if "\\" in lit else lit
                    if not is_candidate(text):
                        continue
                    records.append({"text": text, "file": os.path.relpath(path, decomp_dir), "line": i,
                                    "code": line.strip()[:200],
                                    "confidence": "high" if ui else "low"})
    for enum, values in enum_names.items():
        for v in values:
            records.append({"text": add_spaces(v), "file": f"enum {enum}", "line": 0,
                            "code": f"{enum}.{v}", "confidence": "enum"})
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(records, f, ensure_ascii=False, indent=1)
    print(f"{len(records)} code records ({', '.join(enum_names)}) -> {out_path}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
