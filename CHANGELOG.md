# Changelog

All notable changes to MageQuit Translator. A push to `prod` releases the version in
`VERSION` when this file has a section for it (see `.github/workflows/release.yml`).

## [0.2.0] - 2026-10-02

### Added
- **Multiple languages.** Translations live in `translation/<code>/` (language.json, strings.json, labels.json). Every language in the repo ships in the app, and the game language can be switched from the app.
- **New languages from the app.** "New language" starts a language with every game line ready to translate. "Export" writes the folder exactly as a pull request needs it.
- **Accented letters for many languages.** The font patch now adds À–Ý and à–ÿ (Ä Ö Ü, É È Ê, Ñ, Ç and more), not just Æ Ø Å, to the two game fonts that lacked them. The app warns when a translation uses a letter a game font cannot draw, such as ß.
- **Redesigned app.** It is styled as a facing-page spellbook: English on the left page and the translation on the right, line by line. It adds:
  - chapters (Menus, Spells, Tips…) on a thumb index sized by extent;
  - a seal for each reviewed line;
  - a length budget per line;
  - the game's main menu previewed in its own font.
- Contributor guide (CONTRIBUTING.md) and CI that validates every language folder in pull requests.

### Changed
- The project is renamed from MageQuit-DA to **MageQuit Translator**. The app is `MageQuit-Translator.exe`, and its data lives in `BepInEx/MageQuit-Translator`.
- The app interface is now in English. The game language is chosen separately.
- The capture plugin no longer needs the game's DLLs to build.

### Upgrading from 0.1.0
Uninstall 0.1.0 with its own app first (`MageQuit-DA.exe --uninstall`), then install 0.2.0.

## [0.1.0] - 2026-10-02

### Added
- First Danish translation: menus, spells, tips and messages (about 700 lines), runtime patterns, and the 11 image labels in the main menu and lobby.
- Installer app with clean uninstall, the Danish-letter font patch, and capture mode.
