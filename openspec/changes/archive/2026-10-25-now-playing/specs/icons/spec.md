## ADDED Requirements

### Requirement: A Widget Can Release A Source
`IIconSource` MUST offer `Release(string source)`, a default member that does nothing, so a host written before it keeps
compiling. The engine's implementation forgets that this widget asked for `source` at any size: an icon that no other
widget asked for is released at once (an image still loading is dropped when the load finds it gone, as for a disposed
widget), and an icon another widget asked for stays. Asking for the same source again afterwards loads it again. A
source that was never asked for, an empty one and one that does not parse are ignored.

A widget whose source changes over time (an album art that follows the track) releases the one it stops showing; a widget
whose source is fixed never needs to.

#### Scenario: Track changes
- **WHEN** a widget asked for `art-a.png`, then shows `art-b.png` and releases `art-a.png`
- **THEN** the decoded image of `art-a.png` is disposed and only `art-b.png` is held

#### Scenario: Another widget still shows it
- **WHEN** two widgets asked for the same file and one releases it
- **THEN** the image stays for the other, and is released when it releases it or is disposed

#### Scenario: Unknown source
- **WHEN** a widget releases a source it never asked for
- **THEN** nothing happens and nothing throws
