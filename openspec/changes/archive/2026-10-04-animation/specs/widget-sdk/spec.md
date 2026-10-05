## ADDED Requirements

### Requirement: Animation Frames
A widget MAY tell the host that it is in the middle of an animation. `IWidget` exposes `bool IsAnimating` as a default
interface member that returns `false`; `Widget<TConfig>` exposes it as a virtual member. While it returns `true` the
host repaints the widget at the host's frame rate (see host-shell, "Animation Frames"); the widget does not choose the
rate.

- `IsAnimating` MUST be cheap and MUST NOT have side effects; the host reads it after each paint of the widget
- A widget MUST compute what it draws from `WidgetRenderContext.Time`, never from a count of frames, so the motion
  looks the same at any frame rate and when frames are dropped
- A widget that has finished animating MUST have drawn its resting state in the paint after which `IsAnimating` first
  returns `false`
- A widget MUST draw its resting state on its first paint and on the first paint after `Configure`
- Animation frames MUST NOT trigger `UpdateAsync`; data refresh stays on the widget's refresh policy
- The member is additive: a widget built against the SDK before this member existed loads and runs unchanged

#### Scenario: Default behavior
- **WHEN** a widget does not override `IsAnimating`
- **THEN** it returns `false` and the widget is painted only by its refresh policy

#### Scenario: Widget animates and stops
- **WHEN** a widget returns `true` from `IsAnimating` after a paint, and `false` after a later paint
- **THEN** it is repainted at the host's frame rate in between, and not again after the paint that returned `false`
  until its refresh policy repaints it

#### Scenario: First paint is at rest
- **WHEN** a widget that can animate is painted for the first time
- **THEN** it draws its resting state and `IsAnimating` returns `false`

#### Scenario: Widget built before the member existed
- **WHEN** a plugin compiled against the SDK without `IsAnimating` is loaded
- **THEN** its widgets are registered and painted as before
