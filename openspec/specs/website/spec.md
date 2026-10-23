# website Specification

## Purpose
The public website at `urdeck.app`: what its pages hold, the addresses it promises to keep, what it never does to a
visitor, and how a change reaches it.
## Requirements
### Requirement: Address
The site MUST be served at `https://urdeck.app`, over HTTPS only. `www.urdeck.app` MUST lead to the same site.

#### Scenario: Apex address
- **WHEN** a browser opens `https://urdeck.app/`
- **THEN** the landing page is shown with a valid certificate

#### Scenario: The www name
- **WHEN** a browser opens `https://www.urdeck.app/`
- **THEN** it arrives at `https://urdeck.app/`

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

### Requirement: Download State
Until a signed release exists, the landing page MUST NOT offer a file to download. The download control says that a
download is coming and leads to the repository.

#### Scenario: No release yet
- **WHEN** a visitor uses the download control
- **THEN** no file is downloaded and the visitor arrives at the repository on GitHub

### Requirement: Sample Decks Are Real
Every picture of a deck or widget on the site MUST be rendered by UrDeck itself, with its preview tool, from a
configuration kept in the repository and the product's sample data, so that a visitor can reproduce it. A picture MUST
NOT show the owner's own shortcuts, location, readings or other personal content: every reading, place and time in it
comes from sample data.

#### Scenario: Reproducing a picture
- **WHEN** somebody renders the showcase configuration in a theme with `urdeck-cli preview render`, with the options the
  site's media manifest records for that picture
- **THEN** the result is the picture on the site, apart from the edges of text when made on another platform

#### Scenario: Nothing personal
- **WHEN** any deck or widget picture on the site is looked at
- **THEN** its readings, place and time are the sample data's

### Requirement: Configuration Pages
The site MUST hold user documentation for configuring UrDeck by hand: the config file and where it is, pages and the
dock, each first-party widget and its settings, themes, and backgrounds. Each page is reachable from a navigation that
every documentation page shows.

#### Scenario: Finding a widget's settings
- **WHEN** a visitor opens the configuration section and chooses a first-party widget
- **THEN** the page lists that widget's settings with their values and an example

#### Scenario: From the README
- **WHEN** a reader follows the README's link for configuration
- **THEN** they arrive at the configuration section of the site

### Requirement: Tested On Page
The site MUST hold a page listing the hardware UrDeck has been run on. Each entry gives the display, its resolution,
the GPU, the Windows version and what was checked. The page also says:

- that a display Windows treats as a monitor is expected to work, and that this is an expectation where it has not
  been tested
- which kinds of display do not work
- how to send a hardware report

Where the page names another company's product it MUST say that the name is that company's trademark and that UrDeck
is not affiliated with it.

#### Scenario: First entry
- **WHEN** the page is opened
- **THEN** it lists the HYTE Y70 Touch with its resolution, the GPU, the Windows version and what was checked on it

#### Scenario: Sending a report
- **WHEN** a visitor follows the page's link for reporting hardware
- **THEN** they arrive at a new-issue form on GitHub that asks for the display, the GPU, the Windows version and the
  startup lines of `urdeck.log`

### Requirement: Schema Address
The site MUST serve the background manifest schema at `/schemas/v1/shader-manifest.json`, byte for byte the file kept
in the repository, with a JSON content type. An address under `/schemas/v1/` that has been published MUST keep
answering.

#### Scenario: An editor fetches the schema
- **WHEN** `https://urdeck.app/schemas/v1/shader-manifest.json` is requested
- **THEN** the response is the repository's schema file, unchanged

#### Scenario: The schema changes in the repository
- **WHEN** a change to the schema file is merged
- **THEN** the site serves the new content at the same address after the next publication

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

### Requirement: Usable Without Script Or Motion
Every page's text, pictures and links MUST be usable with JavaScript turned off. With the visitor's "reduce motion"
preference set, the site MUST NOT animate a background or a transition.

#### Scenario: JavaScript off
- **WHEN** the landing page is loaded with JavaScript turned off
- **THEN** the text, the sample decks and every link are present and work

#### Scenario: Reduced motion
- **WHEN** the landing page is loaded with the reduce-motion preference set
- **THEN** a still picture stands where a moving background would be

### Requirement: Publication
The site MUST be published only from the `main` branch, by the repository's automation. A pull request that changes
the site or a file the site includes MUST build the site, and a failed build MUST fail the pull request's checks. A
link from one page of the site to another that does not exist MUST fail the build.

#### Scenario: A pull request
- **WHEN** a pull request changes a page of the site
- **THEN** the site is built as a check and nothing is published

#### Scenario: A merge
- **WHEN** a change to the site is merged to `main`
- **THEN** the site at `urdeck.app` shows it without anybody acting by hand

#### Scenario: A broken internal link
- **WHEN** a page links to a page of the site that does not exist
- **THEN** the build fails

