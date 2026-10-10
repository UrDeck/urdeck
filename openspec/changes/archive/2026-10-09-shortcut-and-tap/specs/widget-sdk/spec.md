## ADDED Requirements

### Requirement: Tap Input
A widget MAY accept taps by implementing the SDK's tap interface in addition to `IWidget`. The interface has two
members:

- "can tap": given a point in the widget's own pixels, whether a tap there would do something. The default answers
  yes for every point. The host asks it when the pointer goes down, to decide whether to show press feedback, and
  again before delivering the tap. It MUST be cheap and free of side effects
- "on tap": given the point, performs the action. It is called on the UI thread when the tap is recognised, and MUST
  return quickly; it MAY call the host's launch service

Further rules:

- The interface is the declaration: the host treats a widget that does not implement it as not interested in taps and
  calls nothing on it for input
- The point uses the same space as `WidgetRenderContext.PixelSize`, with `(0, 0)` at the top-left corner of the card
- A widget MUST NOT draw a pressed state for a tap; the host shows it (see widget-input, "Press Feedback")
- After "on tap" the host asks `NeedsRender` and repaints the widget when it returns `true`
- The interface is additive: a widget built against the SDK before it existed loads and runs unchanged, and the SDK
  assembly version does not change
- Other kinds of input (scrolling, a long press) will be offered as further optional interfaces beside this one; a
  widget that implements only the tap interface is never sent them

#### Scenario: Widget opts in
- **WHEN** a widget class implements the tap interface
- **THEN** the host delivers taps on its card to it

#### Scenario: Widget does not opt in
- **WHEN** a widget class does not implement the tap interface
- **THEN** it is created, configured and painted as before and receives no input calls

#### Scenario: Widget declines a point
- **WHEN** a widget answers no to "can tap" for a point
- **THEN** no press feedback is shown and no tap is delivered for that point

#### Scenario: Widget built before the interface existed
- **WHEN** a plugin compiled against the SDK without the tap interface is loaded
- **THEN** its widgets are registered and painted as before

### Requirement: Launch And Icon Services
`IWidgetHost` MUST additionally offer a launch service and an icon service (see launcher and icons), and
`Widget<TConfig>` MUST offer both to the derived class.

- Launching takes a target text and optional arguments and returns whether the launch was started
- The SDK also offers the classification of a target text (valid or not, its kind and a short display name) as a pure
  function that needs no host, so a widget and the host always agree on what a target is
- The icon service takes a source text and a wanted pixel size and returns an image or nothing, and whether an absent
  image is still loading
- Both are default members of `IWidgetHost`: a host that does not provide them launches nothing and has no icons
- A widget that was never attached (for example one created directly in a test) MUST still work: launching reports
  failure and every icon is absent and not loading
- The members are additive and the SDK assembly version does not change

#### Scenario: Attached widget launches
- **WHEN** an attached widget calls the launch service with a valid target inside "on tap"
- **THEN** the target is started and the call returns that it was

#### Scenario: Unattached widget
- **WHEN** a widget created directly in a test asks for an icon and launches a target
- **THEN** the icon is absent, the launch reports failure, and nothing throws

#### Scenario: Community widget uses the services
- **WHEN** a widget that references only the SDK asks for the icon of an application and launches it on tap
- **THEN** it compiles and works without referencing the engine or the host
