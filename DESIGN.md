---
name: MageQuit Translator
description: A bilingual facing-page spellbook for putting MageQuit into another language and playing it.
colors:
  gilt: "#E8C25A"
  gilt-deep: "#B8902F"
  gilt-bright: "#F2D27A"
  binding: "#1B1230"
  vellum: "#2B1D48"
  vellum-raised: "#36265A"
  vellum-selected: "#3E2C68"
  field-well: "#241839"
  field-well-hover: "#2A1D43"
  menu-board: "#0D0A17"
  hairline: "#4A3A70"
  chalk: "#EFE7FF"
  chalk-dim: "#B9ADD6"
  pencil: "#A196C6"
  ink-faded: "#5E5180"
  ink-fire: "#FF6B3D"
  ink-fire-pale: "#FFC2AD"
  ink-water: "#3FA7FF"
  ink-ice: "#9FE8FF"
  ink-nature: "#7ED957"
  ink-electric: "#F5E14A"
  ink-earth: "#D09A5B"
  ink-arcane: "#C78BFF"
  ink-air: "#D8E2FF"
  ribbon-madder: "#A3322C"
  ribbon-verdigris: "#1F6E64"
  ribbon-lapis: "#2E4AA6"
  ribbon-umber: "#8A4E14"
  ribbon-amethyst: "#5F3A9C"
  ribbon-moss: "#386A20"
  ribbon-mulberry: "#8E2F61"
  ribbon-slate: "#46546E"
  ribbon-original: "#4A3D70"
typography:
  display:
    fontFamily: "IM FELL English, Georgia, serif"
    fontSize: "60px"
    fontWeight: 400
    lineHeight: "64px"
  display-frontispiece:
    fontFamily: "IM FELL English, Georgia, serif"
    fontSize: "76px"
    fontWeight: 400
    lineHeight: "80px"
  headline:
    fontFamily: "IM FELL English, Georgia, serif"
    fontSize: "30px"
    fontWeight: 400
  wordmark:
    fontFamily: "IM FELL English SC, Georgia, serif"
    fontSize: "22px"
    fontWeight: 400
  nav:
    fontFamily: "IM FELL English SC, Georgia, serif"
    fontSize: "17px"
    fontWeight: 400
  title:
    fontFamily: "IM FELL English, Georgia, serif"
    fontSize: "21px"
    fontWeight: 400
  tab-title:
    fontFamily: "IM FELL English, Georgia, serif"
    fontSize: "17px"
    fontWeight: 400
  marginalia:
    fontFamily: "IM FELL English, Georgia, serif"
    fontSize: "15.5px"
    fontWeight: 400
  body:
    fontFamily: "Inter, Segoe UI, sans-serif"
    fontSize: "14.5px"
    fontWeight: 400
  body-prose:
    fontFamily: "Inter, Segoe UI, sans-serif"
    fontSize: "14px"
    fontWeight: 400
    lineHeight: "22px"
  label:
    fontFamily: "Inter, Segoe UI, sans-serif"
    fontSize: "12.5px"
    fontWeight: 400
  gloss:
    fontFamily: "Inter, Segoe UI, sans-serif"
    fontSize: "11.5px"
    fontWeight: 400
  action:
    fontFamily: "Inter, Segoe UI, sans-serif"
    fontSize: "16px"
    fontWeight: 600
rounded:
  none: "0px"
  field: "3px"
  book: "4px"
  tab: "5px"
spacing:
  xs: "4px"
  sm: "6px"
  md: "10px"
  lg: "18px"
  xl: "30px"
  spine: "36px"
  page-inset: "40px"
  page-inset-wide: "56px"
components:
  button-quiet:
    backgroundColor: "transparent"
    textColor: "{colors.chalk-dim}"
    rounded: "{rounded.field}"
    padding: "7px 12px"
  button-quiet-hover:
    backgroundColor: "{colors.vellum-raised}"
    textColor: "{colors.chalk}"
  button-quiet-pressed:
    backgroundColor: "{colors.vellum-selected}"
  button-quiet-disabled:
    backgroundColor: "transparent"
    textColor: "{colors.ink-faded}"
  button-bare:
    backgroundColor: "transparent"
    textColor: "{colors.chalk-dim}"
    padding: "6px 8px"
  button-gilt:
    backgroundColor: "{colors.gilt}"
    textColor: "{colors.binding}"
    typography: "{typography.action}"
    rounded: "{rounded.field}"
    padding: "12px 22px"
  button-gilt-hover:
    backgroundColor: "{colors.gilt-bright}"
    textColor: "{colors.binding}"
  button-gilt-pressed:
    backgroundColor: "{colors.gilt-deep}"
  field:
    backgroundColor: "{colors.field-well}"
    textColor: "{colors.chalk}"
    rounded: "{rounded.field}"
  field-hover:
    backgroundColor: "{colors.field-well-hover}"
  field-ink:
    backgroundColor: "transparent"
    textColor: "{colors.chalk}"
    typography: "{typography.body}"
    padding: "6px 0"
  spread-line-hover:
    backgroundColor: "#31224F"
  spread-line-selected:
    backgroundColor: "{colors.vellum-selected}"
  ribbon:
    textColor: "{colors.chalk}"
    typography: "{typography.title}"
    rounded: "{rounded.none}"
    height: "58px"
    width: "196px"
  ribbon-current:
    width: "224px"
  thumb-tab:
    textColor: "{colors.binding}"
    typography: "{typography.tab-title}"
    rounded: "0 {rounded.tab} {rounded.tab} 0"
    padding: "7px 10px 8px 14px"
    width: "132px"
  thumb-tab-current:
    width: "148px"
  menu-board:
    backgroundColor: "{colors.menu-board}"
    textColor: "{colors.chalk}"
    rounded: "{rounded.field}"
    padding: "34px 44px"
  tooltip:
    backgroundColor: "{colors.vellum-raised}"
    textColor: "{colors.chalk}"
---

# Design System: MageQuit Translator

## Overview

**Creative North Star: "The Bilingual Spellbook"**

The whole app is one open book lying on a night-violet binding. English sits on the verso, the translation on the recto, and a soft spine fold runs between them. Every surface is a page of that book: the frontispiece where a player picks a language and plays, the facing-page spread where a translator works line by line, the title page that installs the translator, and the two-page "new language" leaf. Navigation is physical: language ribbons hang from the left edge of the book like bookmarks, chapter tabs are cut into its right edge like a thumb index, and each line is closed with a seal in the outer margin.

Density follows the job. The frontispiece is sparse and theatrical (one huge language name, the game's own menu words on a dark board, one gilt button). The spread is dense and calm: about 700 lines in quiet Inter body on plum vellum, with old-face italic marginalia for sources, a hairline length budget under every translation, and nothing that shouts except a fire-ink warning when something will break in game.

The palette and typeface are carried from the world of MageQuit (violet night, element inks, a printed spellbook face) without using any official logo or game art. The game's own menu art appears only as rendered previews of what the player will see.

**Key Characteristics:**
- One open book per screen, two facing pages split by a 36px spine fold.
- Gilt is scarce: the one primary action, sealed lines, and the point of entry (caret, focus).
- Element inks colour chapters; muted dyed-cloth colours mark languages.
- IM FELL English for the book's voice, Inter for the working text.
- A fixed three-state seal legend that means only translation review state.

## Colors

A night-violet book with chalk text, one precious metal, and two families of dyed colour: bright element inks for chapters and muted cloth for language ribbons.

### Primary
- **Seal Gilt** (gilt): the one primary action on a page ("Play in Dansk", "Install the translator", "Create language"), the filled seal of a reviewed line, the sealed-progress bar along each thumb tab, the text caret, and the focus rule on fields and buttons. **Bright Gilt** (gilt-bright) is its hover; **Deep Gilt** (gilt-deep) is its pressed state and the seal's rim.

### Secondary
- **Element Inks** (ink-water, ink-fire, ink-electric, ink-nature, ink-ice, ink-earth, ink-air, ink-arcane): one per chapter tab of the thumb index (Menus water, Spells fire, Stages electric, Tips nature, Messages ice, Patterns earth, Uncertain air, Menu art arcane). Tabs are filled with the ink and lettered in Binding.
- **Fire Ink** (ink-fire) doubles as the danger ink: the over-budget length mark, the warning icon, and letters the game font cannot draw in the proof line. **Pale Fire** (ink-fire-pale) is warning and error text on vellum, where full fire would be too loud to read in a sentence.

### Tertiary
- **Ribbon Cloths** (ribbon-madder, ribbon-verdigris, ribbon-lapis, ribbon-umber, ribbon-amethyst, ribbon-moss, ribbon-mulberry, ribbon-slate): language bookmarks, assigned in that order and cycled. **Original Ribbon** (ribbon-original) always marks "English, mod off". Ribbons carry Chalk text, so these stay dark and muted.

### Neutral
- **Night Binding** (binding): the window ground around the book, and the lettering colour on gilt and on element-ink tabs.
- **Plum Vellum** (vellum): the page of the book. **Raised Vellum** (vellum-raised) is button hover and tooltips; **Selected Vellum** (vellum-selected) is the selected spread line and button press.
- **Field Well** (field-well, field-well-hover): the sunk ground of boxed text fields and combo boxes.
- **Menu Board** (menu-board): the near-black board behind the game's menu words and art previews, so they read as the game will show them.
- **Hairline** (hairline): every 1px rule and border: quiet buttons, fields, the marginalia divider, scroll thumbs.
- **Chalk** (chalk): primary text. **Dim Chalk** (chalk-dim): secondary text, icons at rest, marginalia. **Pencil** (pencil): glosses, placeholders, the unsealed ring, the draft dash, the in-budget length mark. **Faded Ink** (ink-faded): disabled text and icons only.

### Named Rules
**The Scarce Gilt Rule.** Gilt appears on at most one button per page, plus seals, sealed progress, caret and focus. Never on decoration, headings or secondary actions.

**The Two Dye Families Rule.** Element inks belong to chapters, ribbon cloths belong to languages. Never swap them, and never use either family for state.

**The Fire Means Breakage Rule.** Fire ink marks only what will break in game (too long, a missing letter, mismatched placeholders). It is never an accent.

## Typography

**Display Font:** IM FELL English (with Georgia, serif), bundled with the app
**Small-caps Font:** IM FELL English SC, for the wordmark and the Play / Translate switch
**Body Font:** Inter (with Segoe UI, sans-serif)

**Character:** A seventeenth-century printing face gives the book its voice (titles, language names, chapter tabs, italic marginalia), while Inter does the working text a translator reads for hours. The old face never sets editable text.

### Hierarchy
- **Display** (400, 60px / 64px): title-page and leaf headings. The frontispiece language name runs larger (76px / 80px); the "Begin a new language" leaf runs at 48px / 52px.
- **Headline** (400, 30px; 32px in the spread's running head): the open chapter's title and right-page headings.
- **Wordmark / Nav** (IM FELL SC, 22px / 17px): the app name and the two-way Play / Translate switch, its current item underlined by a 1.5px Chalk rule.
- **Title** (400, 21px): language names on ribbons. **Tab title** (17px) on thumb tabs.
- **Marginalia** (IM FELL italic, 15.5px, Dim Chalk; 14.5px inside a line): sources, font names, "in Dansk", image sizes. Annotation, never instruction.
- **Body** (Inter 400, 14.5px): English lines and translations in the spread. Prose paragraphs run 14-15px with 22-23px leading and a 440-460px measure.
- **Label** (Inter 400, 12.5px, Dim Chalk): install line, progress counts, colophon, warnings, hints.
- **Gloss** (Inter 400, 11.5px, Pencil): version number, keyboard hints, the folder path. Only for metadata the user can do without.
- **Action** (Inter 600, 16px): text on the gilt button.

### Named Rules
**The Printed Voice Rule.** IM FELL sets what the book says (titles, names, marginalia); Inter sets what the user reads and writes. Never set an input, a button label, or a warning in IM FELL.

## Layout

The window is a three-row frame: a binding bar (wordmark left, Play / Translate centred, install line and four icon tools right), the book row, and a one-line colophon (the last thing that happened). The book row is three columns: a 232px ribbon rail, the book (fills), and on the spread only, the thumb index outside the book's right edge. Minimum window 1040 x 640; designed at 1280 x 820.

Every page is two facing columns of equal width split by the 36px spine. Page insets are 40px (spread, running head, foot) or 56px (title pages and the frontispiece's outer edge). In the spread each line is a row of four columns: verso, spine, recto, and a 56px outer margin that holds the seal. Selecting a line opens its marginalia beneath it across both pages, separated by a Hairline rule.

The thumb index divides the full book height between chapters by the square root of each chapter's line count (minimum 46px, 4px apart), so all tabs always fit without scrolling and small chapters stay readable. The open chapter's tab sticks out further (148px against 132px); the current language ribbon hangs longer (224px against 196px).

Vertical rhythm comes from a small step set: 4, 6, 10, 18 and 30px between stacked items, with 30px separating a page's content from its action.

## Elevation & Depth

Mostly flat and tonal: pages, wells and selections differ by lightness of violet, not by shadow. Two shadows exist, both describing physical objects, and the spine is a gradient fold rather than a rule.

### Shadow Vocabulary
- **Book lift** (`0 10px 28px rgba(7,3,18,0.63), 0 2px 4px rgba(7,3,18,0.38)`): the open book resting on the binding. Used once, on the book.
- **Board lift** (`0 6px 18px rgba(5,2,16,0.56)`): the game's menu board on the title page.
- **Spine fold** (horizontal gradient, transparent vellum to #150D27 at centre and back, 36px wide): the fold between facing pages.

### Named Rules
**The Physical Shadow Rule.** A shadow means a physical object sitting on another: the book on its binding, a board on a page. Controls, tabs, ribbons and lines are never shadowed.

## Shapes

Small, bookbinder's corners. Fields, buttons, combo boxes and the menu board are 3px; the book itself is 4px. Thumb tabs are square where they meet the book and rounded 5px on their free edge only. Ribbons are cut, not rounded: a rectangle with a swallowtail notch at the free left end (the notch is 16px deep and meets at mid-height). Seals are geometric: a 14px hollow ring, a 14 x 2.5px dash, a 15px filled disc. Borders are 1px Hairline; fields focus to a single 2px gilt underline rather than a ring.

## Components

### Buttons
Quiet and outlined by default, with exactly one gilt action per page.
- **Shape:** gently squared (3px).
- **Quiet (default):** transparent, 1px Hairline border, Dim Chalk text, 7px 12px padding. Leading icon 16px, 7-8px gap.
- **Hover / Pressed / Focus:** hover raises to Raised Vellum with Chalk text and a Pencil border; pressed sinks to Selected Vellum; keyboard focus turns the border gilt.
- **Gilt (primary):** Seal Gilt fill, Binding text at 16px semibold, Deep Gilt border, 12px 22px padding; icon stroked and filled in Binding. Hover Bright Gilt, pressed Deep Gilt.
- **Bare:** no border, 6px 8px padding. Used for icon tools in the binding bar, the Play / Translate switch, "New language", ribbons, tabs and seals.
- **Disabled:** Faded Ink text and icon on transparent.

### Icons
24-unit stroked paths in one weight (2px, round caps and joins), drawn at 16px (14px inline). Dim Chalk at rest, Chalk on button hover, Binding on gilt, Fire for warnings, Pencil inside the search field.

### Inputs / Fields
- **Boxed field:** Field Well ground, 1px Hairline border, 3px corners, Chalk text, gilt caret, violet selection. Hover lightens the well and borders it in Pencil; focus replaces the border with a 2px gilt underline.
- **Ink field (spread translations):** no box at all. Text sits on the page at body size; hover shows a 1px Hairline underline, focus a 1px gilt underline.
- **Combo boxes** match the boxed field.

### Navigation
- **Play / Translate switch:** two IM FELL SC words, Dim Chalk, the current one underlined by a 1.5px Chalk rule.
- **Language ribbons:** see Signature Components.
- **Thumb index:** see Signature Components.

### Spread line
A row of the facing pages: English in Chalk body with its first source as italic marginalia beneath, the translation as an ink field on the recto with its length budget under it, and the seal in the outer margin. Hover tints the row (#31224F); selection fills it with Selected Vellum and opens the marginalia: context, "set in" font, warnings in Pale Fire with a fire alert icon, the proof line, and a boxed note field.

### Signature Components
**The Seal.** A fixed legend in the outer margin of every line and menu-art row: hollow Pencil ring (not translated), Pencil dash (draft), Gilt disc with Deep Gilt rim (sealed, reviewed). Clicking toggles sealed.

**The Length Budget.** A 1.5px rule under each translation, 60px wide when the translation matches the English length and growing up to 120px at twice the length. Pencil while in budget, Fire past 140% of the English.

**The Proof.** The translation re-printed at 15px with every letter the game font cannot draw in Fire and underlined. Shown only when a letter is missing.

**Language Ribbon.** A 58px-tall cloth bookmark hanging from the book's left edge with a swallowtail notch, its language's own name in IM FELL 21px Chalk, progress in 11.5px Chalk beneath, and a 7px Chalk dot when the game is set to it. The current ribbon hangs 28px longer.

**Thumb Tab.** A chapter tab filled with its element ink, lettered in Binding (title IM FELL 17px, count Inter 12px), with a 3px sealed-progress bar at its foot: Binding at 25% for the track, Gilt for the sealed share.

**Menu Board.** A near-black board that shows the game's menu words exactly as the game will draw them, flipping letter by letter into the chosen language. On the frontispiece it fills the recto; on the title page it is a 3px-cornered card with Board lift.

## Do's and Don'ts

### Do:
- **Do** keep every screen inside the one open book: two facing pages split by the 36px spine fold.
- **Do** reserve Seal Gilt for the page's single primary action, sealed state, sealed progress, caret and focus.
- **Do** use the seal legend (ring, dash, disc) for translation state and for nothing else.
- **Do** colour chapters with element inks and languages with ribbon cloths, each lettered for contrast (Binding on inks, Chalk on cloths).
- **Do** set titles, names and marginalia in IM FELL English, and everything typed, clicked or warned in Inter.
- **Do** show the game's own text on the Menu Board (#0D0A17) so previews read as the game draws them.
- **Do** mark breakage in Fire ink and its explanatory sentence in Pale Fire.

### Don't:
- **Don't** put a second gilt button on a page; secondary actions are quiet outlined buttons.
- **Don't** reuse the ring, dash or disc shapes as bullets, status lights or decoration.
- **Don't** use Fire ink as an accent or for anything that will not break in game.
- **Don't** shadow controls, tabs, ribbons or lines; shadows belong to the book and the board.
- **Don't** separate the facing pages with a flat rule; use the spine fold.
- **Don't** set editable text, buttons or warnings in IM FELL.
- **Don't** use MageQuit's official logo or game art as decoration; game imagery appears only as rendered previews of the player's own translation.
