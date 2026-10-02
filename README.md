# MageQuit på dansk

Dansk oversættelse af [MageQuit](https://store.steampowered.com/app/572220/MageQuit/) — menuer, besværgelser, beskrivelser og knapper.
Inkluderer et lille program, der installerer, slår til/fra, afinstallerer og lader dig rette oversættelserne.

## Installation

1. Hent `MageQuit-DA-<version>-win-x64.zip` (eller `linux-x64`) og pak den ud hvor som helst.
2. Start `MageQuit-DA.exe`. Programmet finder selv spillet via Steam (ellers: **Vælg…**).
3. Klik **Installér dansk**, og start spillet.

**Afinstallér** fjerner alt igen, og spillets filer bliver gendannet byte for byte.
**Slå dansk fra** lader mod'et blive liggende, men spillet kører på engelsk.

**Steam Deck/Linux:** Sæt startindstillingen `WINEDLLOVERRIDES="winhttp=n,b" %command%` på MageQuit i Steam (programmet viser den med en kopiknap).

## Ret oversættelser

- Fanen **Tekster** viser alle tekster. Ret i kolonnen *Dansk* eller i panelet til højre, og tryk **Gem** (Ctrl+S).
- Mens spillet kører, genindlæser **Alt+R** teksterne med det samme. **Alt+T** skifter mellem dansk og engelsk.
- Fanen **Knapper** viser de menupunkter, der er billeder i spillet (Sofa, Garderobe, SPIL …). De tegnes med spillets egen skrifttype, men kræver genstart af spillet.
- Status: *Mangler*, *Udkast* (første oversættelse, ikke gennemlæst) eller *Godkendt*.
- Mangler der en tekst? Slå **Opfang i spillet** til, spil lidt, og klik **Importér opfangede**.
- **Eksportér…/Importér fil…** deler dine rettelser med andre. Dine egne rettelser overlever opdateringer af mod'et.
- **F11** i spillet gemmer et skærmbillede i `BepInEx/MageQuit-DA/screenshots`.

---

## How it works (developer notes)

MageQuit is Unity 2018.4 (Mono) with all English text hardcoded: about 1,400 UGUI `Text` components in scenes and prefabs, string literals in `Assembly-CSharp.dll`, and 11 menu labels that are images.

| Piece | What it does |
|---|---|
| [BepInEx 5](https://github.com/BepInEx/BepInEx) | Mod loader, injected via `winhttp.dll` (no game code modified) |
| [XUnity.AutoTranslator](https://github.com/bbepis/XUnity.AutoTranslator) | Replaces text at runtime from `BepInEx/Translation/da/Text/MageQuit.txt`, and the label images from `…/Texture/` |
| `src/MageQuitDA.Plugin` | Small BepInEx plugin: *capture mode* (records every UI string with scene/path/font to `captured.tsv`) and the F11 screenshot key |
| Font patch | `MageQuit-Body` and `MageQuitHeaderThin` have no Æ Ø Å. `tools/fonts` composes the letters from each font's own glyphs (A+E, O+/, A+ring). The installer appends the patched `Font` object to the asset file and repoints the object table entry. Reverting restores the entry and truncates, which gives the exact original bytes. Only a delta is shipped, not the game's fonts. |
| `src/MageQuitDA.Core` | Install/uninstall with a manifest (`BepInEx/MageQuit-DA/manifest.json`), font patcher, translation store → XUnity format, label renderer (SkiaSharp, using the patched game font) |
| `src/MageQuitDA.Manager` | Avalonia GUI + CLI (`--install`, `--uninstall`, `--enable`, `--disable`, `--status`) |

The canonical translation is `translation/strings.json` (plus `labels.json`). Users' working copies live in the game folder and are merged on update (entries they edited win).

### Rebuilding the string list

```sh
pip install UnityPy TypeTreeGeneratorAPI fonttools pillow
ilspycmd -p -o decomp "<game>/MageQuit_Data/Managed/Assembly-CSharp.dll"
python tools/extract/extract_assets.py "<game>" assets.json   # UI Text/TMP + Spell.description etc.
python tools/extract/extract_code.py decomp code.json         # string literals + enum display names
python tools/extract/build_candidates.py assets.json code.json translation/strings.json
python tools/translate/draft_da.py translation/strings.json   # draft translations, regexes, drops
python tools/fonts/make_font_patch.py build "<game>" payload/game/BepInEx/MageQuit-DA/fontpatch.json
```

### Building a release

```powershell
./build/package.ps1            # plugin + payload.zip + single-file Manager for win-x64 and linux-x64 → dist/
dotnet test --project tests/MageQuitDA.Tests
```

The plugin build references the game's `Managed` DLLs from the local install (`-GameDir`); they are never committed.

### Licenses

The mod is MIT. Bundled: BepInEx (LGPL-2.1) and XUnity.AutoTranslator (MIT); their licenses are installed to `BepInEx/MageQuit-DA/licenses`. MageQuit is © Bowlcut Studios; this is an unofficial fan translation.
