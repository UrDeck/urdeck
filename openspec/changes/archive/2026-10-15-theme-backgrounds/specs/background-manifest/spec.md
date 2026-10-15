## MODIFIED Requirements

### Requirement: Manifest Content
The version 1 manifest MAY hold the following; every key but `manifest` is optional:

- `$schema`: a string for editors; ignored
- `title` and `description`: text for a person
- `entry`: see "Background Folder"
- `author`, `license`, `version` and `tags` (an array of strings): text for a person and for a store; never used to
  decide what runs
- `pairsWith`: an object whose `themes` is an array of theme names the background suits, best first; it is used to
  recommend, never to decide what runs (see background-pairing)
- `parameters`: an object keyed by the name of a uniform the shader declares, each value an object with `label`,
  `description`, `kind`, `min`, `max`, `step`, `unit` and `hidden`
- `samples`: an array of objects with `type` (`image`, or `video`, which is accepted and not used), `file`, `order`,
  `caption` and `settings` (an object)

The meaning of the parameter keys:

- `kind` is `number` (the default) or `color`; other values are reserved for later versions (a `vector` is the likely
  first) and are a lint warning until then. `color` MUST be given only for a parameter with three or four
  components; it tells an editor to offer a colour, and does not change how the configuration sets the parameter
- `min`, `max` and `step` are numbers; `min` and `max` apply to each component of a vector
- `hidden` true keeps the parameter out of an editor. It MUST NOT stop the configuration from setting it
- The shader is the source of truth for which parameters exist, their types and their defaults. The manifest MUST
  NOT change them: a default in the shader's comment stays the default, whatever the manifest says
- A sample's `file` MUST name a file inside the folder, with a relative path that does not leave it
- A key the format does not define, a `kind` or sample `type` that is not defined, and a value of the wrong JSON type
  each log one lint warning and are otherwise ignored

The meaning of a sample's `settings`:

- It says how the sample was made, so the same picture can be made again: `size` (text, `<width>x<height>` in
  pixels), `time` (a number, the shader's time in seconds), `theme` (the name of a theme), `colors` (an array of
  colours, as the configuration's `colors`) and `parameters` (an object of parameter values, as the configuration
  sets them). Every key is optional
- It is a record for a person and for tools. The host MUST NOT use it to decide what is shown
- A reader of the manifest gets `settings` as it is written
- Its content is not checked: a key it does not define, or a value of another type, is not a diagnostic. Only a
  `settings` that is not an object is one

#### Scenario: Annotation
- **WHEN** the manifest gives `parameters.intensity` a `label`, a `description`, `min` 0 and `max` 3
- **THEN** those are what a reader of the manifest gets for `intensity`, and the parameter's type and default are
  still the shader's

#### Scenario: Hidden parameter
- **WHEN** the manifest marks `seed` as `hidden` and the configuration has `"seed": 7`
- **THEN** the shader runs with `seed` at 7

#### Scenario: Parameter the shader does not have
- **WHEN** the manifest annotates `densty` and the shader declares no uniform of that name
- **THEN** a lint diagnostic names `densty` and the background is shown

#### Scenario: Colour kind on a number
- **WHEN** the manifest gives `kind` `color` to a `float` parameter
- **THEN** a lint diagnostic says a colour needs three or four components, and the kind is ignored

#### Scenario: Unknown key
- **WHEN** the manifest has a key `"rating": 5`
- **THEN** a lint warning names it and the background is shown

#### Scenario: Pairing
- **WHEN** the manifest has `"pairsWith": { "themes": ["carbon", "rigline"] }`
- **THEN** a reader of the manifest gets those two names in that order, and there is no lint diagnostic

#### Scenario: Pairing of the wrong type
- **WHEN** the manifest has `"pairsWith": { "themes": "carbon" }`
- **THEN** one lint warning says `themes` must be an array of strings, no pairing is read, and the background is shown

#### Scenario: Unknown key inside pairing
- **WHEN** the manifest has `"pairsWith": { "themes": ["carbon"], "moods": ["calm"] }`
- **THEN** one lint warning names `pairsWith.moods` and the themes are still read

#### Scenario: Reserved sample type
- **WHEN** a sample has `"type": "video"`
- **THEN** it is accepted, and the host never plays it

#### Scenario: Sample settings are kept
- **WHEN** a sample has `"settings": { "size": "270x480", "time": 10, "theme": "default" }`
- **THEN** a reader of the manifest gets those three values for that sample, and the deck shows the background as the
  configuration says, not as the sample's settings do

#### Scenario: Sample settings are not checked
- **WHEN** a sample's `settings` has a key `"note": "evening"`
- **THEN** there is no diagnostic for it
