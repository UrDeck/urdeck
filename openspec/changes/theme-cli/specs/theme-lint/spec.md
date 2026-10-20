## Purpose
Defines the findings about a theme: which codes exist and what each one means, which ones fail a run, where a theme can
be read from, and the rule that the deck and the command line tool report the same things. A theme author, a script and a
store's CI rely on these codes keeping their meaning.

## ADDED Requirements

### Requirement: One Set Of Rules
The findings the deck logs when it loads a theme and the findings the command line tool prints for the same theme MUST come
from the same checks:

- Every warning the deck logs about a theme's values, its rules between values and its fonts has a code, a severity and
  the key it is about, and the tool prints it with the same message
- The deck's log text for a theme MUST NOT change because the tool exists
- The tool MAY report more than the deck logs (the checks of "Checks Only A Tool Makes"); it MUST NOT report less
- A code keeps its meaning in later versions; a retired code is not reused

#### Scenario: Same finding in both
- **WHEN** a theme sets `card.shadow.blur` to 5, outside its range
- **THEN** the tool prints a `THEME004` finding for `card.shadow.blur` whose message is the one in the deck's log

#### Scenario: The log is unchanged
- **WHEN** a theme with one warning for each kind of value check is loaded by the deck
- **THEN** its log lines are those the deck wrote before the tool existed

### Requirement: Sources Of A Theme
A theme MUST be checkable from the two places the deck reads one:

- A folder holding `theme.json` and the files it names, named as the theme is
- A built-in theme, read from the product as shipped and not from a copy in the source tree, with its bundled fonts and
  licence files

Both MUST give the same findings for the same content. A check on a built-in theme therefore also finds a file that is
named but not shipped.

#### Scenario: A folder
- **WHEN** a folder `mine` holding a valid `theme.json` is checked
- **THEN** the theme is checked as `mine`

#### Scenario: A built-in theme
- **WHEN** the built-in theme `carbon` is checked
- **THEN** its settings, its fonts and their licence files are read as the deck reads them

#### Scenario: A font that is not shipped
- **WHEN** a built-in theme names a font file that is not shipped with the product
- **THEN** a `THEME020` error names the file

### Requirement: Findings About The File
A theme whose file cannot be used MUST be reported, and a theme whose values are wrong MUST be reported value by value:

| Code | Severity | Meaning |
|---|---|---|
| `THEME001` | error | `theme.json` is missing, is not valid JSON or holds no object. The theme cannot load |
| `THEME002` | warning | a key the deck does not read. The message names the closest key that is read when one is within two edits |
| `THEME003` | warning | a value of the wrong JSON type |
| `THEME004` | warning | a number outside its range; the message carries the range |
| `THEME005` | warning | a value that is not one of an enumeration's names; the message lists the names |
| `THEME006` | warning | a colour that is not `#RRGGBB` or `#AARRGGBB` |
| `THEME007` | warning | an array outside its length limits or with an invalid element |
| `THEME008` | warning | a group that cannot work without a member it does not have (a gradient without `colors`); the group is ignored |
| `THEME010` | warning | `format` is newer than the deck reads |

- Every message says what is wrong and how to fix it, in one line, without a stack trace
- Each finding names the key by its path (`card.shadow.blur`), and the file it is in
- Comments and trailing commas in `theme.json` are accepted, as the deck accepts them
- A key of the format the deck reads is not unknown; a key in the deferred-keys document is, because the deck does not
  read it
- A theme with an error from `THEME001` is not checked further

#### Scenario: Missing file
- **WHEN** a folder with no `theme.json` is checked
- **THEN** one `THEME001` error says the file is missing, and nothing else is reported

#### Scenario: Not JSON
- **WHEN** `theme.json` holds `{ "card": `
- **THEN** one `THEME001` error says the file is not valid JSON and where the reader stopped

#### Scenario: Misspelt key
- **WHEN** a theme has `card.borderwidth`
- **THEN** a `THEME002` warning says the key is not read and suggests `card.borderWidth`

#### Scenario: Wrong type
- **WHEN** a theme sets `card.gap` to `"wide"`
- **THEN** a `THEME003` warning names `card.gap` and says a number is expected

#### Scenario: Enumeration in another case
- **WHEN** a theme sets `card.borderStyle` to `Dashed`
- **THEN** there is no finding

#### Scenario: Newer format
- **WHEN** a theme declares `format` 9
- **THEN** a `THEME010` warning says the deck reads up to format 2 and ignores what it does not know

### Requirement: Findings About Files The Theme Names
A theme's fonts and pictures MUST be checked as files:

| Code | Severity | Meaning |
|---|---|---|
| `THEME020` | error | a font file is missing, is not a `.ttf`, `.otf` or `.ttc`, or its path leaves the theme's folder |
| `THEME021` | warning | a font file is larger than 3 MB, or has no licence file beside it (`*-OFL.txt`, `LICENSE*`, `*-LICENSE*`) |
| `THEME022` | warning | a font is named by family and not by file, so it is used only where it is installed |
| `THEME023` | warning | the weight a role asks for is outside a variable font's range, or is a weight a static font does not have |
| `THEME043` | warning | a still background picture is missing, leaves the folder, or is larger than 8 MB |

- A path that leaves the folder is judged the same on Windows and on Linux, and a backslash counts as a separator
- No file outside the theme's folder is read

#### Scenario: Font outside the folder
- **WHEN** `typography.font` is `..\other\x.ttf`, on Windows or on Linux
- **THEN** a `THEME020` error says the path must stay inside the theme's folder, and no file outside it is read

#### Scenario: No licence
- **WHEN** a theme folder holds `Mono.ttf` and no licence file
- **THEN** a `THEME021` warning says a licence file is missing

#### Scenario: Weight the font lacks
- **WHEN** a role asks for weight 500 and the named file is a static Bold face
- **THEN** a `THEME023` warning names the role, the weight and the weight the file has

#### Scenario: Font by family
- **WHEN** `typography.font` is `Segoe UI`
- **THEN** a `THEME022` warning says the font is used only where it is installed

### Requirement: Findings About Backgrounds And Pairing
A theme's background and its pairings MUST be checked against the backgrounds that exist. The backgrounds that exist are
the built-in ones and those in the directories the tool is given:

| Code | Severity | Meaning |
|---|---|---|
| `THEME040` | warning | `background.default.name` is not a background that exists |
| `THEME041` | warning | `meta.pairsWith.backgrounds` names a background that does not exist |
| `THEME042` | warning | the theme's default background is not in its own `pairsWith` list |
| `THEME044` | warning | `icons.set` names an icon set that does not exist |

- A theme with no `background` or no `pairsWith` has no finding from these codes
- The deck does not check any of these when it loads a theme; the tool alone reports them

#### Scenario: Unknown default background
- **WHEN** `background.default.name` is `nosuch`
- **THEN** a `THEME040` warning names `nosuch`

#### Scenario: Pairing names a missing background
- **WHEN** `meta.pairsWith.backgrounds` is `["contour", "nosuch"]`
- **THEN** a `THEME041` warning names `nosuch` and none names `contour`

#### Scenario: Default not paired
- **WHEN** a theme's default background is `contour` and `pairsWith` lists only `streams`
- **THEN** a `THEME042` warning says so

#### Scenario: User background directory
- **WHEN** the tool is given a directory holding a background `mine` and a theme pairs with `mine`
- **THEN** there is no `THEME041` finding

### Requirement: Findings About Rules Between Values
The rules between values that the deck applies when it loads a theme MUST each be a finding with its own code:

| Code | Severity | Meaning |
|---|---|---|
| `THEME050` | warning | a border style, gradient or animation with a card border width of 0 |
| `THEME051` | warning | `borderAnimate` `spin` without a conic `borderGradient`; the deck switches the animation off |
| `THEME052` | warning | a pattern whose marks are closer than three pixels at the panel width |
| `THEME053` | warning | outside corner marks reaching past the gap, counting the mark's thickness |
| `THEME054` | warning | a shadow or glow whose reach is more than the gap over a translucent card fill; the deck limits it |
| `THEME055` | warning | a line (a border, highlight, accent strip or corner mark) that resolves under one pixel; the deck draws it 1 px wide |
| `THEME056` | info | `card.compact.shape` `circle` on a card that is not square, which is drawn as a capsule |
| `THEME090` | info | more than one continuous animation is on |

- `THEME054` MUST use the reach the deck uses when it draws, so the tool and the picture agree
- An `info` finding never changes the exit code

#### Scenario: Style without a border
- **WHEN** `card.borderStyle` is `dashed` and `card.borderWidth` is 0
- **THEN** a `THEME050` warning says the style has no effect

#### Scenario: Reach into the neighbour
- **WHEN** a translucent card fill has a shadow whose reach is larger than `card.gap`
- **THEN** a `THEME054` warning gives the reach, the gap and the reach the deck will use

#### Scenario: Several animations
- **WHEN** a theme turns on two continuous animations
- **THEN** a `THEME090` information finding says each repaints on every frame, and no warning is made

### Requirement: Checks Only A Tool Makes
The tool MUST report findings that need a judgement and not only a parse:

| Code | Severity | Meaning |
|---|---|---|
| `THEME060` | warning | the text colour on the card fill is under 4.5:1 |
| `THEME061` | warning | the text colour on a card tint is under 4.5:1; a tinted card keeps the theme's text colour |
| `THEME062` | info | the muted text colour on the card fill is under 3:1 |
| `THEME080` | warning | `meta` lacks `name`, `description` or `mode` |

- Contrast is the WCAG ratio. A translucent card fill is composited over `colors.background` first, and the message says
  the page behind the cards may be a moving background that the tool does not know
- A fill that is a gradient is checked at its darkest and lightest stop

#### Scenario: Low contrast
- **WHEN** a theme's text is `#808080` on a card fill `#909090`
- **THEN** a `THEME060` warning gives the ratio and the two colours

#### Scenario: Translucent fill
- **WHEN** the card fill is `#20ffffff` over `colors.background` `#000000`
- **THEN** the ratio is that of the text against the composited colour, and the message names `colors.background`

#### Scenario: Tint
- **WHEN** one of `colors.cardTints` is too light for the text colour
- **THEN** a `THEME061` warning names the tint's position in the list

#### Scenario: No metadata
- **WHEN** a theme has no `meta`
- **THEN** a `THEME080` warning lists what is missing

### Requirement: Built-in Themes Check Clean
Every built-in theme MUST be free of warnings and errors under all the checks above.

#### Scenario: The shipped themes
- **WHEN** the built-in themes are checked with warnings counted as failures
- **THEN** there are no findings that fail the run
