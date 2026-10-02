# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

(Recorded as `web` because the closest schema value fits: the app is an Avalonia 12 desktop app for Windows and Linux/Steam Deck that draws its own UI rather than using native OS controls. It is not a website.)

## Users

Two audiences, served equally:

- **Players** who want MageQuit in their own language. They open the app once or twice: install, pick a language, play. Later they come back to switch language, update or uninstall. Many are not technical, and some are children playing couch multiplayer with family.
- **Volunteer translators** who make or improve a language. They spend long sessions in the editor with the game open beside it, pressing Alt+R in game to reload. When they're done, they export the language folder and open a GitHub pull request.

## Product Purpose

MageQuit Translator is an unofficial fan tool that puts MageQuit's menus, spell descriptions and button art into other languages, and that anyone can extend with a new language. It installs and removes cleanly: the game folder returns byte for byte to its original state. Success means a player gets the game in their language in under a minute, and a new language can go from "New language" to a pull request without touching JSON by hand.

## Positioning

A complete and reversible MageQuit localisation layer:
- **Text:** runtime text replacement (BepInEx + XUnity.AutoTranslator).
- **Fonts:** letters the game's fonts lack, built from the fonts' own glyphs.
- **Image labels:** re-rendered in the game's own typeface.
- **Missing strings:** a capture mode that finds text the game shows but the list lacks.

Generic Unity translation tools do none of the MageQuit-specific parts.

## Operating Context

- **Next to the game:** the app runs while MageQuit runs. Text edits show in game after Alt+R; label images need a game restart. Alt+T toggles original/translated text in game.
- **Capture mode:** the plugin records every string the game shows, and the app imports the ones not yet in the list.
- **Contribution:** a language is the folder `translation/<code>/` (language.json, strings.json, labels.json). New languages arrive as pull requests on GitHub (mortenfaerk/MageQuit-Translator).
- **Distribution:** a single self-contained exe from GitHub Releases, on Windows and Linux. On the Steam Deck it needs a Steam launch option.

## Capabilities and Constraints

- Install, update, uninstall; switch the active game language or turn the translation off (English).
- Text editor:
  - each string has a status: new, draft (machine/first pass) or reviewed;
  - each string shows its context (scene/object, font) and a translator note;
  - warnings for: letters the target font can't render, uppercase-only fonts, text much longer than the original, and mismatched placeholders or rich-text tags.
- Pattern entries (regex) cover text the game builds at runtime.
- Image-label editor with live previews rendered in the game font.
- New-language creation, import/export, and PR-ready export.
- The app UI is in English; the game language is chosen separately.
- Game strings run to roughly 700 entries per language.

## Brand Commitments

- It should feel like a MageQuit companion: echo the game's mood and palette.
- It must not use the official logo or game art, so it reads clearly as an unofficial fan tool.
- MageQuit is © Bowlcut Studios.

## Evidence on Hand

- One language, Danish (`translation/da`): about 700 draft strings, 24 runtime patterns, 11 image labels, and a glossary with sources.
- No user testimonials or download numbers exist. Do not invent any.

## Product Principles

1. Reversible by default. Nothing the app does should leave the game in a state the app cannot undo.
2. Playing comes before translating. A player should never have to understand the editor to get a language.
3. Context beats guessing. Translators always see where a string appears and what will break.
4. Contributing is a first-class path. A finished language is one export away from a pull request.

## Accessibility & Inclusion

No product-specific requirement has been established. Players may be young and non-technical, so plain wording and clear states matter.
