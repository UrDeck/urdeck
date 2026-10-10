# icons Specification

## Purpose
Defines the host's icon service: where the picture for an application, a file or a web address comes from, what is
fetched from the network and when, and how icons are cached, so that any widget can show a recognisable icon without
platform code of its own.
## Requirements
### Requirement: Icon Requests
A widget MUST be able to ask the host for the icon of a source text and a wanted size in pixels, and the request MUST
NOT block or perform I/O on the calling thread.

- The answer is an image when the icon is ready, and nothing when it is still being loaded or cannot be had; the
  widget can tell "loading" from "none"
- When an icon the widget asked for becomes ready or is given up on, the host MUST repaint that widget once
- The image belongs to the host: the widget draws it during the paint in which it received it and MUST NOT dispose it
  or keep it for a later paint
- Asking again for the same source in a later paint is cheap and returns the same image
- A widget that is not attached to a host gets "none" for every source

#### Scenario: First paint
- **WHEN** a widget asks for an icon that has not been loaded yet
- **THEN** it receives nothing, marked as loading, and is repainted once when the icon is ready

#### Scenario: Later paints
- **WHEN** the widget asks for the same source again
- **THEN** it receives the image without any loading

#### Scenario: No icon to be had
- **WHEN** a widget asks for the icon of an empty source
- **THEN** it receives nothing, marked as none, and no repaint follows

### Requirement: Icon Sources
The icon service MUST work out the icon from the source text, using the same classification as the launcher (see
launcher, "Launch Targets"):

- A file target that is an image file (PNG, JPEG, WebP, BMP, GIF or ICO) is that image
- Any other file target and a `shell:` item give the icon Windows shows for it: the application's icon for an
  executable or a `.lnk`, the package's icon for a packaged application, the folder or document icon otherwise
- A web address gives the site's own icon (see "Site Icons"); when the site yields none, the icon Windows shows for
  the address, which is the default browser's
- Any other address gives the icon Windows shows for it
- A source that is invalid, or for which Windows has nothing, gives none

Icons from Windows are requested at 256 pixels and keep their transparency. The image a widget receives is never
larger than needed for the wanted size by more than a factor of two.

#### Scenario: Executable
- **WHEN** a widget asks for the icon of the path of `steam.exe`
- **THEN** it receives Steam's icon with a transparent background

#### Scenario: Packaged application
- **WHEN** a widget asks for the icon of `shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App`
- **THEN** it receives the Windows Terminal icon

#### Scenario: Image file
- **WHEN** a widget asks for the icon of the path of a PNG file
- **THEN** it receives that picture

#### Scenario: Web address whose site has no usable icon
- **WHEN** a widget asks for the icon of a web address and the site yields no icon
- **THEN** it receives the default browser's icon

#### Scenario: File that does not exist
- **WHEN** a widget asks for the icon of a path that does not exist
- **THEN** it receives none and one line is logged

### Requirement: Site Icons
For a web address the icon service MUST look for the site's own icon in this order, and take the first candidate that
passes the checks below:

1. The address itself, when its answer is an image
2. The icons listed in the web manifest the page links to, leaving out those meant only as maskable or monochrome
3. The page's `apple-touch-icon` and `icon` links
4. `/favicon.ico` at the site's root

- Among candidates with a declared size, the smallest one that is at least the wanted size is tried first, then the
  larger ones, then the smaller ones from large to small; at most four candidates are downloaded for one address
- A candidate counts only if its bytes decode as a raster image and its shorter side is at least 48 pixels. The
  status code and the declared content type are not trusted: a sign-in page answered with status 200 in place of an
  image is not an icon
- SVG candidates are skipped
- Redirects are followed, at most five
- Icon links to another host are followed only over `https`

#### Scenario: Site with a manifest
- **WHEN** the page links a manifest that lists PNG icons of 192, 384 and 512 pixels and the wanted size is 256
- **THEN** the 384 pixel icon is used

#### Scenario: Site behind a sign-in page
- **WHEN** every request to the site, including its manifest and its icon paths, is answered with a sign-in page
- **THEN** no site icon is found and the default browser's icon is used

#### Scenario: Tiny favicon
- **WHEN** the only icon the site offers is 16 pixels
- **THEN** it is rejected and the default browser's icon is used

#### Scenario: Direct image address
- **WHEN** the source is the address of a PNG file
- **THEN** that image is used and no page is parsed

### Requirement: Network Behaviour
The icon service MUST make network requests only for web addresses a widget asked an icon for, and MUST keep them
rare, bounded and anonymous.

- No request is made for file targets, shell items or other addresses, and none while no widget asks for a web icon
- A site icon that was found is kept on disk and is not fetched again for 30 days; after that it is refreshed in the
  background the next time it is asked for, and the kept icon is used until the refresh succeeds and whenever it fails
- An address that yielded no icon is not tried again until the application is started again
- Requests never happen on the UI thread, are never triggered by a paint or a tap of an icon that is already known,
  and are not repeated in a loop after a failure
- Each request has a time limit of ten seconds and a size limit (2 MB for a page or manifest, 5 MB for an image)
- Requests send no cookies and no credentials, and identify the application in the `User-Agent`
- A certificate error is a failure; it is never ignored
- The first failure for an address is logged with the reason; a success is logged with where the icon came from

#### Scenario: No web shortcuts
- **WHEN** the configuration contains no widget with a web address as its icon source
- **THEN** the icon service makes no request

#### Scenario: Restart with a cached icon
- **WHEN** the application is restarted a day after it fetched a site's icon
- **THEN** the icon is shown from disk and no request is made

#### Scenario: Offline
- **WHEN** the network is down the first time a web address is asked for
- **THEN** the default browser's icon is shown, one line is logged, and no further request is made in this run

#### Scenario: Local icon avoids the network
- **WHEN** a widget asks for the icon of a local image file while its target is a web address
- **THEN** no request is made

### Requirement: Icon Caches
The icon service MUST keep decoded icons in memory only while a live widget uses them, and MUST keep fetched site icons
in a cache folder on disk.

- Two widgets that ask for the same source and a similar size share one image
- When the last widget that asked for an icon is disposed (its page is left, the configuration is reloaded), the
  image is released
- The disk cache holds one PNG per web address, at most 512 pixels on its longer side, named so that an address
  cannot escape the cache folder
- A cache file that cannot be read or decoded is treated as absent and replaced
- Deleting the cache folder is safe at any time; the icons are fetched again

#### Scenario: Page left
- **WHEN** the user swipes away from the only page that shows a shortcut and the slide settles
- **THEN** that shortcut's icon is no longer held in memory

#### Scenario: Shared icon
- **WHEN** two shortcuts on one page have the same target
- **THEN** one image is loaded and both draw it

#### Scenario: Damaged cache file
- **WHEN** a cache file holds bytes that are not an image
- **THEN** it is ignored, the icon is looked up again, and the file is replaced

### Requirement: Icons In A Snapshot
The snapshot command MUST wait for the icons the page's widgets asked for, up to a few seconds, before it paints the
image, so that a snapshot shows icons and not placeholders. Icons that are not ready in that time are drawn as their
placeholders.

#### Scenario: Snapshot of a page with shortcuts
- **WHEN** a snapshot is taken of a page with a shortcut to an installed application
- **THEN** the image shows the application's icon

