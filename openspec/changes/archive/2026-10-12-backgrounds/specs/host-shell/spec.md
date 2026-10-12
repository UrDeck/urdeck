## MODIFIED Requirements

### Requirement: GPU Composition
The host window MUST be composited by the GPU from independent layers: the window background, the configured
background if there is one (see the `backgrounds` capability), and one layer per widget surface. The host MUST NOT
produce frames while no layer changes, and MUST NOT run a render loop while no widget is animating (see "Animation
Frames") and no moving background is being shown (see backgrounds, "When Frames Are Produced").

#### Scenario: Still page
- **WHEN** a page is shown without a moving background and no widget is repainted
- **THEN** no widget surface is redrawn and the host's GPU use is not measurably above zero

#### Scenario: Layer changes
- **WHEN** one widget is repainted
- **THEN** only that widget's layer is updated and the page is composited again

#### Scenario: Moving background
- **WHEN** a moving background is shown and no widget is repainted
- **THEN** only the background's layer is redrawn and no widget surface is

### Requirement: Theme Application
The host MUST apply the selected theme (see the `theme` capability) to everything it draws:

- The theme is selected on startup and again on every configuration reload
- The window background uses the theme's background colour. A configured background (see the `backgrounds`
  capability) is drawn over it; the colour shows when there is none, until it is ready and when it fails
- Every widget surface receives the same theme, resolved for the target monitor's physical pixels
- When the theme or the monitor's size or scaling changes, the page is rebuilt so that layout, cards and widgets all
  use the new values

#### Scenario: Window background follows the theme
- **WHEN** the active theme changes from `default-dark` to `default-light` and no background is configured
- **THEN** the window background changes to the light theme's background colour

#### Scenario: Display scaling changes
- **WHEN** the target monitor's scaling changes while the host runs
- **THEN** the page is rebuilt and cards keep the same proportions relative to the grid cell

#### Scenario: Generated background follows the theme
- **WHEN** the active theme changes while a built-in shader background without its own colours is shown
- **THEN** the background is drawn in the new theme's palette
