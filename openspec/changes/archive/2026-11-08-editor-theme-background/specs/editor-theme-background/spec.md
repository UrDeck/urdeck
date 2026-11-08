## Purpose

The editor's section for the deck's look: choosing the theme from pictures of the user's own page, choosing the
background with the theme's suggestions first, and the background's motion, options and dimming.

## ADDED Requirements

### Requirement: Choosing A Theme
The editor MUST show every installed theme (built-in and the user's own) as a picture of the user's current page drawn
in that theme, with its name, the current theme marked. Choosing one sets the deck's theme at once. Until a picture is
ready, the theme's colours are shown in its place. The Pages section MUST also offer the themes in a short list in its
header.

#### Scenario: Pictures of my page
- **WHEN** the user opens Theme and background
- **THEN** each theme shows the user's current page in that theme, and the current theme is marked

#### Scenario: Quick switch
- **WHEN** the user picks Carbon from the Theme list in the Pages header
- **THEN** the preview and the deck show Carbon within half a second

### Requirement: Choosing A Background
The editor MUST offer, in this order: the theme's choice (naming what the theme shows), none, a picture of the user's
own, the backgrounds suggested for the current theme (the theme's default first, then those that pair with it), and all
other backgrounds, each built-in or installed background shown with its sample picture. The backgrounds stay visible and
can be chosen whatever is currently chosen.

#### Scenario: Suggestions for Rigline
- **WHEN** the theme is Rigline
- **THEN** "Suggested for Rigline" starts with Horizon, followed by the backgrounds that pair with Rigline

#### Scenario: Theme's choice
- **WHEN** the user chooses Theme's choice
- **THEN** the configuration has no `background`, and changing the theme changes the background with it

### Requirement: Still Or Moving
For a shader background the editor MUST offer Moving and Still, and say what moving costs. Still keeps the chosen
background's look as one frame. Choosing another background keeps Still if it was chosen.

#### Scenario: Still
- **WHEN** the user chooses Still for Nebula
- **THEN** the deck shows Nebula without motion and draws no frames for it

### Requirement: Background Options
The editor MUST show a shader's parameters as a form generated from its manifest (label, range, step, unit,
description), with Reset to the shader's own values, and show a picture background's file with Change and what happens
if the file moves. When the configured background could not be shown, the section MUST say so and what the deck shows
instead.

#### Scenario: A parameter
- **WHEN** the user drags Nebula's "Star density" slider
- **THEN** the preview and the deck show the new density, and the background object in the file has the parameter

#### Scenario: Picture moved
- **WHEN** the configured picture file no longer exists
- **THEN** the section says the file was not found and that the deck shows the theme's background

### Requirement: Dim Behind Cards
The editor MUST offer a "Dim behind cards" control from 0 to 70 percent for a shader or picture background, starting at
the theme's own dimming, which sets the configuration's `background.dim`.

#### Scenario: Dimming a photo
- **WHEN** the user sets Dim behind cards to 40 percent over a picture
- **THEN** the picture is drawn 40 percent darker behind the cards on the deck

### Requirement: Motion Level
The editor MUST offer the deck-wide motion level: follow Windows, full, reduced or off.

#### Scenario: Motion off
- **WHEN** the user chooses Off
- **THEN** the configuration has `"motion": "off"` and the deck stops all motion
