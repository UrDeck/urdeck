## Purpose

Lets a visitor of the website see the product in any built-in theme: one choice changes every picture of a deck or widget
on the page, and the site's accent colours, without leaving the page.

## ADDED Requirements

### Requirement: The Picker
Every page of the site MUST show a theme picker pinned to a corner of the window, above the content, that stays in place
while the page scrolls. Closed, it shows the chosen theme's name and swatch; open, it lists every built-in theme that has
media, with its swatch (background, card and accent colours) and name. The list comes from the product's catalog: a theme
added to the product appears in the picker after a media refresh, with no site edit. The picker MUST NOT cover the page's
content at phone width: there it is a small button that opens the list over the page.

#### Scenario: Eleven themes
- **WHEN** the landing page is opened
- **THEN** the picker lists the eleven built-in themes with their swatches, and glass is chosen

#### Scenario: A new theme
- **WHEN** a theme is added to the product and the media are refreshed
- **THEN** it is listed in the picker without a change to the site's code or data

### Requirement: A Choice Changes The Page
Choosing a theme MUST, without reloading the page:

- Show every deck picture and loop, and every widget picture and loop, in that theme; a loop that was playing plays on in
  the new theme, a paused one stays paused
- Mark, in the background gallery, the backgrounds that pair with the theme, its default background first
- Tint the site's accent colours (links, highlights, primary buttons, glows) with the theme's accent. When the theme's
  accent would read under 4.5:1 against the site's background, a lighter shade of it is used that reaches 4.5:1. The page
  stays dark and keeps the site's fonts

Pictures that are not of the product (icons, the site's own art) do not change. A picture that has no file for the chosen
theme keeps the glass picture.

#### Scenario: Choosing Ember
- **WHEN** a visitor chooses Ember
- **THEN** the hero deck, the gallery's highlighted card and every widget picture show Ember, and the site's accent turns
  amber

#### Scenario: A light theme's accent
- **WHEN** a visitor chooses Paper, whose accent is too dark to read on the site's background
- **THEN** the site's accent is a lighter shade of Paper's accent that reads at 4.5:1 or more

### Requirement: Remembered And Linkable
The choice MUST be kept in the visitor's browser for that visitor only, and applied on every page of the site before the
pictures are shown, so a page does not first show glass and then switch. A link with `?theme=<name>` MUST open the page in
that theme and remember it. An unknown name in a link is ignored. When the browser does not allow storage, the picker
still works for the page being viewed.

#### Scenario: Across pages
- **WHEN** a visitor chooses Pop on the landing page and opens a documentation page
- **THEN** the documentation page opens in Pop

#### Scenario: A shared link
- **WHEN** a visitor opens `https://urdeck.app/?theme=blueprint`
- **THEN** the page shows Blueprint and the picker shows Blueprint chosen

#### Scenario: Storage blocked
- **WHEN** the browser refuses local storage and the visitor chooses Carbon
- **THEN** the page shows Carbon, and the next page opens in glass

### Requirement: Without Script, Motion Or A Mouse
Without JavaScript the picker MUST NOT be shown and the page MUST show glass with everything working. With the reduce-motion
preference set, choosing a theme MUST NOT start a loop; still pictures change instead. The picker MUST be usable with a
keyboard alone (open, move through the themes, choose, close with Escape, focus returning to the button) and MUST name
itself and the chosen theme to assistive technology.

#### Scenario: JavaScript off
- **WHEN** the landing page is loaded with JavaScript turned off
- **THEN** no picker is shown and every picture is the glass one

#### Scenario: Keyboard
- **WHEN** a visitor tabs to the picker, opens it with Enter, moves with the arrow keys and presses Enter on Obsidian
- **THEN** the page shows Obsidian, the list closes and focus is on the picker's button
