## 1. SDK

- [ ] 1.1 Add `ActionResult` (done or failed with a message; `Done`, `Failed(message)`), `ActionDescriptor` (path, label, name, takes-argument) and `IActionProvider` (`DescribeActions`, `InvokeAsync`) in `UrDeck.Sdk.Data`, with doc comments that state the rules of the provider-actions spec (overlap with `SampleAsync`, never before `Start` or after `Shutdown`)
- [ ] 1.2 Add `IActionInvoker` and its null implementation, `Actions` as a default member of `IWidgetHost`, and a protected `Actions` on `Widget<TConfig>` that works before `Attach`
- [ ] 1.3 Move the package version one minor with a note in the csproj comment, keep `AssemblyVersion`, and confirm `SdkContractTests` passes unchanged; SDK tests for the null invoker (failed, never throws) and for a host that implements `IWidgetHost` without `Actions`

## 2. Engine

- [ ] 2.1 `ReadingHub`: implement `IActionInvoker` over the provider runtimes: parse the id with `ReadingId`, resolve the runtime, fail for unknown provider, not running, and no `IActionProvider`, each with its message and one log line
- [ ] 2.2 The per-provider queue: ordered, one at a time on the thread pool and independent of the sampling loop, a bound of 8 (busy failure beyond), a 10 second limit with cancellation and moving on, failed results for a throwing provider, and the log line `Action '<id>': done|failed: <message> (<n> ms)` without the argument value; the time source is injected as for sampling
- [ ] 2.3 Provider stop (linger end, failure teardown) and `ReleaseAll` complete waiting actions as failed; the queue of a new run starts empty
- [ ] 2.4 Action catalog: `GetActionCatalog(providerId)` beside `GetCatalog`, read without starting the provider, cached with the reading catalog and dropped on release
- [ ] 2.5 `WidgetServices` offers the hub as `Actions`; `SampleProvider` does not implement `IActionProvider`; the preview host takes the default
- [ ] 2.6 Hub tests with a fake provider: done, failed with a message, throws, hangs past the limit (token cancelled, next runs), two in a row keep order, queue full, provider not running (and not started), unknown provider, malformed id, no actions, stops with actions waiting, `ReleaseAll`, an action during a running sample, the catalog without a start; a plugin-reload test that a widget which invoked an action and was disposed leaves its load context collectible
- [ ] 2.7 The engine coverage floor still holds

## 3. Docs

- [ ] 3.1 `CONTRIBUTING.md`: actions for provider authors (opt-in, addressing, overlap with sampling, results and limits)
- [ ] 3.2 `docs/BACKLOG.md` (item 3 and item 8: what is done, `now-playing` next) and `docs/ROADMAP.md`

## 4. Verify

- [ ] 4.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, the engine coverage floor, `dotnet format urdeck.slnx --severity warn`, and `openspec validate --all --strict`
- [ ] 4.2 Close #108 from the pull request description
