## MODIFIED Requirements

### Requirement: Landing Page
The landing page MUST say what UrDeck is, why it exists, and where the source is. It holds, in this order of weight:

- a statement of what UrDeck is and what it runs on
- the reasons it exists, with privacy first
- a gallery of the built-in themes, each a picture of the same deck in that theme with the theme's name, description,
  tags, swatch and the backgrounds it pairs with
- the built-in widgets, each at every size the site has media for, in the chosen theme (see `site-theme-picker`)
- every built-in background with its loop, its description and the themes it pairs with
- a link to the repository on GitHub
- a link to the configuration pages and to the "Tested on" page

What the gallery, the widgets and the backgrounds list MUST come from the product's catalog and the media manifest, not
from lists kept by hand: a theme, widget or background the product ships appears once it has media, and one it no longer
ships disappears. Words the product does not hold (a picture's alt text, a widget size's note) MAY come from one file of
site overrides; an entry without an override still appears, with text made from the catalog.

Every statement about what UrDeck does over the network MUST match the application: it names the requests UrDeck makes
(a forecast for a configured place, the icon of a site a shortcut points to) and does not claim that there are none.
A feature that does not exist yet MUST be marked as planned.

#### Scenario: A visitor who reads only the first screen
- **WHEN** the landing page is opened on a desktop browser
- **THEN** what UrDeck is, a sample deck and the link to the repository are visible without scrolling

#### Scenario: A planned feature
- **WHEN** the page mentions sharing widgets, backgrounds or themes through a gallery
- **THEN** it is marked as planned

#### Scenario: Every background
- **WHEN** the landing page is built with the sixteen built-in backgrounds and their media
- **THEN** it shows sixteen backgrounds, each with the themes it pairs with

#### Scenario: Choosing from the gallery
- **WHEN** a visitor chooses Rigline's card in the theme gallery
- **THEN** the page shows Rigline, as if it had been chosen in the picker

#### Scenario: Nothing copied by hand
- **WHEN** a theme's description changes in the product and the media are refreshed
- **THEN** the gallery shows the new description without a site edit

### Requirement: Visitor Privacy
The site MUST NOT set a cookie, use local storage to identify a visitor, run analytics, or make any request to a host
other than `urdeck.app` while a page loads or is used. Fonts, scripts, styles and pictures are served by the site
itself. A link the visitor chooses to follow is not such a request. The site MAY keep the visitor's theme choice in the
browser's local storage: the name of a theme, nothing else, never sent anywhere.

#### Scenario: Loading the landing page
- **WHEN** the landing page is loaded with the network observed
- **THEN** every request goes to `urdeck.app` and no cookie is set

#### Scenario: A remembered theme
- **WHEN** a visitor chooses a theme and the browser's storage for the site is inspected
- **THEN** it holds only the theme's name, and no request carried it
