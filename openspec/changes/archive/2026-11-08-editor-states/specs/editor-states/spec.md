## Purpose

How the editor greets a new user and how it shows widgets it cannot fully handle (a plugin that failed, a widget that is
not installed, a size the widget does not declare, widgets placed where they cannot be), without ever losing settings.

## ADDED Requirements

### Requirement: Welcome On First Run
When the host created the configuration on this start (first run), it MUST open the editor and offer to start with the
sample page or with an empty page, naming the display the deck is shown on (or will be shown on). Starting with an empty
page leaves one empty page and an empty dock, as one undo step. Dismissing the welcome keeps the sample page.

#### Scenario: Empty start
- **WHEN** a first run opens the welcome and the user chooses "Start with an empty page"
- **THEN** the configuration has one empty page that is the start page and no dock, the deck shows it, and one Undo
  brings the sample page back

#### Scenario: Not a first run
- **WHEN** the host starts with an existing configuration
- **THEN** the editor does not open by itself

### Requirement: Empty Page Tip
An empty page in the editor MUST show a tip pointing at the page's first row, saying how to add a widget, with a button
that adds a clock.

#### Scenario: Add a clock from the tip
- **WHEN** the user presses "Add a clock" in the tip
- **THEN** a Clock at its default size is on the first row and selected

### Requirement: Widget That Did Not Load
A widget whose plugin failed to load MUST be shown in the preview as a placeholder in its cells saying it did not load,
and the properties pane MUST say which plugin failed and why, with ways to see the log and to remove the widget. Its
settings stay in the file unchanged and the deck leaves its space empty.

#### Scenario: Plugin failed
- **WHEN** the plugin providing `focustools.widgets.timer` fails to load and a page has a Focus timer widget
- **THEN** the preview shows "Focus timer did not load" in its cells, the pane names the plugin and the reason, and the
  widget's object in the file is unchanged after other edits

### Requirement: Widget That Is Not Installed
A widget whose type no installed plugin provides MUST be shown in the preview as a placeholder saying it is not
installed, and the properties pane MUST show its settings as read-only text and offer to remove it. Its settings stay in
the file unchanged.

#### Scenario: Unknown type
- **WHEN** a page has a widget of type `acme.widgets.ticker` and no plugin provides it
- **THEN** the preview shows "acme.widgets.ticker is not installed", the pane shows its JSON, and Remove deletes it as
  one undo step

### Requirement: Size Not Declared
A widget placed at a size its type does not declare MUST be marked in the preview, and the properties pane MUST say so,
list the declared sizes, and offer the nearest declared sizes that fit there. Nothing changes until the user chooses;
the deck keeps drawing the widget as before.

#### Scenario: Stats at 3x1
- **WHEN** a page has a Stats widget at 3x1
- **THEN** the pane says "3x1 is not a Stats size" with buttons for the nearest declared sizes that fit, and the file is
  unchanged until one is pressed

### Requirement: Invalid Placement
When widgets on a page overlap, or a widget lies outside the page's grid, the editor MUST say so above the preview,
naming the widgets, and offer to move one of them to the first free space.

#### Scenario: Overlap from a hand edit
- **WHEN** the file places a Weather widget and a Stats widget on the same cells
- **THEN** the editor says "Two widgets overlap: Weather and Stats." and offers to move Stats to the first free space
