## Context

Taps on a widget reach the host through `ITapTarget` and `Launcher.Launch(string)`; a widget cannot see the editor (the
license boundary: widgets reference only the SDK). `EditorWindow.ShowProviders(providerId)` is what "Set up" calls and
it only selects the Providers nav item. The Steam widget's tap falls through to `steam://open/friends` when nothing
else is hit.

## Decisions

1. **The route is a launch target, not a new SDK service.** Widgets already launch strings, and a scheme the host answers
   keeps the SDK small and the boundary intact. *Alternative:* an `IWidgetHost.OpenSettings(providerId)` method: more
   explicit, but a new member on a contract every plugin sees for one use. Chosen: the target, with an SDK builder so the
   text is not hand-written. If a second use of host-handled targets appears, the same scheme grows paths.
2. **Handled before the shell, in the engine's launcher.** The launcher routes `urdeck://settings/...` to a handler the
   host sets; with no handler (previews, tests, `--snapshot`) the launch reports failure and logs one line. It never
   reaches the shell, so no OS URL scheme is registered by the app.
3. **One wording, owned by the engine.** The reason `<Name> needs setup` comes from the provider's display name, so
   providers stop writing their own sentence for it. `MissingSettingMessage` (Home Assistant: "set up Home Assistant in
   Settings") may stay as detail the provider's page shows; whether to retire it is for the apply step once the pages are
   looked at together.
4. **The Steam widget decides the tap by the reading's state**, not by hit area alone: where it drew a notice for a
   reading that is unavailable for setup, the target is the settings target; where it drew data, today's targets.
5. **Focus is the editor's job.** `ShowProviders` already receives the id; it expands and scrolls to that provider. The
   focusing logic lives in the engine's editor model so it is tested on Linux, not through XAML.

## Risks

- The deck window never takes focus (`WS_EX_NOACTIVATE`) and the editor is a separate activatable window; it must come to
  the front like any launched application. To be looked at on the panel (task 4.2).
- Opening the editor while it is mid-close: the host keeps one reference and drops it on close; the handler makes a new
  window, as the tray does.
