## ADDED Requirements

### Requirement: Action Service
The host services a widget receives (`IWidgetHost`) MUST offer `Actions`, an `IActionInvoker`, as a default member whose
default fails every action with a message that the host offers none, so a host or a test double written before actions
existed keeps compiling. `Widget<TConfig>` offers it as a protected member that works before `Attach` (it fails the
same way).

- `Invoke(string id, ReadingValue? argument = null)` returns `Task<ActionResult>`. It does not block, throw or fault (see
  provider-actions, "Invocation Never Blocks The Caller")
- A widget calls it from `OnTap` or from anywhere else; it needs no particular thread
- The service is per widget, with the rest of its host services, and holds no reference to the widget, so it never keeps
  a plugin load context alive. A task still pending when the widget is disposed is not cancelled, and the continuation
  of a widget that has gone does nothing visible

#### Scenario: Widget before attach
- **WHEN** a widget calls `Invoke` before `Attach`
- **THEN** the task is completed and failed, and nothing throws

#### Scenario: Host written before actions
- **WHEN** a host implements `IWidgetHost` without `Actions`
- **THEN** it still compiles, and a widget's `Invoke` ends failed with the message that the host offers no actions
