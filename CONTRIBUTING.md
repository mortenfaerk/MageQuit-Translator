# Contributing a translation

Every language is a folder in [`translation/`](translation). Adding or improving one is a pull request against that folder; no code is needed.

```
translation/
  da/
    language.json   code, English name, native name, authors
    strings.json    every line of the game, with your translation
    labels.json     the menu words the game draws as pictures
    GLOSSARY.md     optional: the words you settled on, so others stay consistent
```

## Start a new language

1. Download the latest release and install it into your MageQuit folder.
2. In the app, choose **New language**. Enter the language code (`de`, `sv`, `es`, `pt-BR` …). The names fill in themselves; check them.
3. Choose **Create language**. You get every line of the game, sorted into chapters, with context and warnings.
4. Translate in the book. Keep MageQuit running beside the app: **Ctrl+S** in the app, then **Alt+R** in the game, shows your text straight away. Menu art needs a game restart.
5. Seal a line when you are happy with it: click the circle at the outer margin, or press **Ctrl+Enter**. Sealed lines are marked as reviewed.
6. Choose **Export** and pick your fork's `translation/` folder. The app writes `translation/<code>/` exactly as the repository expects.
7. Open a pull request. CI checks the folder. Once it is merged, the next release ships it to every player.

## Improve an existing language

Do the same, but pick the language's ribbon instead of creating one. Export over the existing folder and open a pull request. The diff then shows exactly which lines changed.

## Things the app warns about

- **Letters the game cannot draw.** The game's fonts have been extended with À–ÿ (Ä Ö Ü É Ñ Ç Æ Ø Å …), but not every alphabet. If a line uses a letter a font lacks, the app marks it in the proof. Those letters show up as boxes in the game. Open an issue if your language needs more letters; the glyph builder can usually add them.
- **Capitals only.** Some headers use a font with only capital letters. The app shows those lines in capitals.
- **Length.** The hairline under each translation shows its length compared to the English. Text much longer than the original may be clipped in the game.
- **Patterns.** Some text is built by the game while it runs ("Round 3 of 9"). These lines are regular expressions: keep `$1`, `$2` or `${name}` where the numbers and names go.

## Missing lines

If you see English in the game that is not in the book, turn on **Capture** at the bottom of the spread. Play the screens where the text appears, then choose **Import captured lines**. New lines appear in the Uncertain chapter.

## Rules for pull requests

- Change only `translation/<code>/`, unless you are fixing code.
- Export from the app rather than editing the JSON by hand. The export clears local editing flags that CI rejects.
- Statuses are `new` (not translated), `machine` (draft, also used for machine-assisted first passes) and `reviewed` (sealed).
- MageQuit is © Bowlcut Studios. This is an unofficial fan project, so do not add the game's own art or text other than translations.

## Building from source

See the developer notes in [README.md](README.md).
