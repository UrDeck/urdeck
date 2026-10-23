## MODIFIED Requirements

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
