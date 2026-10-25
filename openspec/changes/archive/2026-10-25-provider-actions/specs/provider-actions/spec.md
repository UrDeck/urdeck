## Purpose

Defines how a widget asks a data provider to do something (skip a track, toggle a switch) and learns the outcome: how a
provider offers actions, how an action is addressed, where and in what order it runs, the limits, the result and what
the host logs. Readings flow from providers to widgets (see data-providers); this is the path back.

## ADDED Requirements

### Requirement: Providers Opt In To Actions
A provider MAY implement `IActionProvider` beside `IDataProvider`. Implementing it is the declaration: a provider that
does not is never asked for an action, and nothing else about it changes. The interface offers:

- `DescribeActions()`: the catalog, a list of `ActionDescriptor` (path, label, name, and whether the action takes an
  argument). Called once, like `Describe`, and it MUST NOT need `Start`
- `InvokeAsync(path, argument, cancellationToken)`: performs the action and returns an `ActionResult`. `argument` is
  null or one `ReadingValue`

`InvokeAsync` can run while `SampleAsync` runs, and is never called before `Start` or after `Shutdown`. A provider
guards state it shares between them. Both interfaces are implemented by the same class, which the engine creates once
per run as it does today.

#### Scenario: A provider without actions
- **WHEN** a widget asks for an action of a provider that does not implement `IActionProvider`
- **THEN** the result is failed with a message that the provider offers no actions, and the provider is not touched

#### Scenario: Catalog needs no start
- **WHEN** an editor asks the hub for a provider's action catalog and the provider is not running
- **THEN** the catalog is returned without starting the provider, as for readings

### Requirement: Action Identity
An action id MUST have the form of a reading id, `provider:path` (see data-providers, "Reading Identity"), and is parsed
by the same code. The path names the action, for example `control/next` or `switch.desk_lamp/toggle`. A widget never
references a provider type. The argument is a single optional `ReadingValue`; there is no list of named arguments. A
provider that takes more than one value splits it into actions or encodes it in the path.

An id that does not parse, and a provider id that is not loaded, end as a failed result naming the id.

#### Scenario: Id with no provider
- **WHEN** a widget invokes `nothing:control/next` and no provider has the id `nothing`
- **THEN** the result is failed with a message that names the id and says the provider is unknown

#### Scenario: Malformed id
- **WHEN** a widget invokes `control/next`
- **THEN** the result is failed with the parse message of reading ids, and nothing is queued

### Requirement: Invocation Never Blocks The Caller
The host service `IActionInvoker.Invoke(id, argument)` MUST return a `Task<ActionResult>` without doing any work on the
calling thread beyond parsing the id and queueing. It never throws and the task never faults: every failure is a failed
`ActionResult`. A widget may ignore the task, await it, or continue from it; the continuation runs on a worker thread,
not the UI thread, so a widget that reacts must not touch UI state there.

#### Scenario: Tap that asks for an action
- **WHEN** a widget calls `Invoke` from `OnTap` and the provider takes five seconds to respond
- **THEN** `OnTap` returns at once and the task completes later

#### Scenario: A provider that throws
- **WHEN** the provider's `InvokeAsync` throws
- **THEN** the task completes with a failed result carrying the exception's message, one log line is written, and the
  host and other providers are unaffected

### Requirement: Order, Limits And Lifetime
The hub MUST run the actions of one provider one at a time, in the order they were asked, on the thread pool, and
independently of the provider's sampling loop and of other providers' actions.

- Each action has a time limit (10 seconds). On expiry the cancellation token is cancelled and, if the provider does not
  return, the result is failed with a timeout message; the hub does not wait for the provider to return before running
  the next action
- At most 8 actions per provider are waiting. One asked beyond that completes at once as failed with a message that the
  provider is busy
- Actions run only while the provider is running (it has a subscriber; see data-providers, "Demand-Driven Provider
  Lifetime"). An action for a provider that is not running completes as failed with that message. The hub never starts
  a provider for an action, so a widget that acts on a provider also subscribes to one of its readings
- When a provider stops, its waiting actions complete as failed; a plugin reload (`ReleaseAll`) does the same for every
  provider

#### Scenario: Two actions in a row
- **WHEN** a widget invokes `media:control/play` and then `media:control/pause`
- **THEN** the provider receives `play` and, after it returns, `pause`

#### Scenario: A provider that hangs
- **WHEN** an action does not return within the time limit
- **THEN** the result is failed with a timeout message, the token was cancelled, and the next waiting action runs

#### Scenario: Queue full
- **WHEN** eight actions are waiting for a slow provider and a ninth is asked
- **THEN** the ninth completes at once as failed (busy) and the eight keep their places

#### Scenario: Provider not running
- **WHEN** a widget invokes an action of a provider whose linger has ended
- **THEN** the result is failed (not running) and the provider is not started

#### Scenario: Provider stops with actions waiting
- **WHEN** a provider stops while two of its actions are waiting
- **THEN** both complete as failed

### Requirement: Results And The Log
`ActionResult` MUST have a state, done or failed, and a message (empty when done and nothing needs saying). The host writes one
log line per action when it ends: `Action 'media:control/next': done (12 ms)` or `Action 'media:control/next': failed:
<message> (12 ms)`. The argument's value is not logged. A widget that does not await the result sees the effect in the
next reading; the SDK keeps no optimistic state, and a widget that wants it keeps its own.

#### Scenario: Done
- **WHEN** a provider returns done
- **THEN** the task completes done and one log line says done with the elapsed time

#### Scenario: Failed with a message
- **WHEN** a provider returns failed with the message `no session`
- **THEN** the task completes failed with that message and the log line carries it

### Requirement: Demo And Sample Providers
A provider replaced by its sample file (see sample-data) MUST offer no actions. An action for it ends failed with a message
that the provider offers no actions, so `--demo` and `preview render` perform nothing.

#### Scenario: Demo mode
- **WHEN** the host runs with `--demo` and a widget invokes an action of a provider that carries a sample
- **THEN** the result is failed (no actions) and nothing outside the process is touched
