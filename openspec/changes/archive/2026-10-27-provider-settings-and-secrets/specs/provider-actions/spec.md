## MODIFIED Requirements

### Requirement: Order, Limits And Lifetime
The hub MUST run the actions of one provider one at a time, in the order they were asked, on the thread pool, and
independently of the provider's sampling loop and of other providers' actions.

- Each action has a time limit, the provider's `actionTimeoutMs` setting (default 10,000 milliseconds; a smaller value
  than 1,000 is raised to 1,000 and logged). On expiry the cancellation token is cancelled and, if the provider does not
  return, the result is failed with a timeout message; the hub does not wait for the provider to return before running
  the next action
- An action that ends, done or failed, after longer than the provider's `actionWarnMs` setting (default 2,000
  milliseconds) is logged as a warning that names its duration and the threshold
- At most 8 actions per provider are waiting. One asked beyond that completes at once as failed with a message that the
  provider is busy
- An action for a provider that is not running starts it, as a first subscriber would (see data-providers,
  "Demand-Driven Provider Lifetime"), and runs once it has started. Afterwards the provider lingers as if its last
  subscriber had left, unless a widget subscribes to it. A provider that cannot start (its required settings are missing,
  or it fails to start) fails the action with that reason
- When a provider stops, its waiting actions complete as failed; a plugin reload (`ReleaseAll`) does the same for every
  provider

#### Scenario: Two actions in a row
- **WHEN** a widget invokes `media:control/play` and then `media:control/pause`
- **THEN** the provider receives `play` and, after it returns, `pause`

#### Scenario: A provider that hangs
- **WHEN** an action does not return within the provider's time limit
- **THEN** the result is failed with a timeout message, the token was cancelled, and the next waiting action runs

#### Scenario: A configured time limit
- **WHEN** `providers.homeassistant.actionTimeoutMs` is `5000` and an action does not return
- **THEN** it fails with a timeout message after 5 seconds

#### Scenario: A slow action
- **WHEN** an action of a provider with the default threshold completes after 2.4 seconds
- **THEN** the result is done and the log has a warning naming 2.4 seconds and the 2 second threshold

#### Scenario: Queue full
- **WHEN** eight actions are waiting for a slow provider and a ninth is asked
- **THEN** the ninth completes at once as failed (busy) and the eight keep their places

#### Scenario: Provider not running
- **WHEN** a widget with no reading of the provider invokes `homeassistant:scene.movie/toggle` and the provider is not
  running
- **THEN** the provider is started, the action runs, and the provider stops after its linger if nothing subscribes

#### Scenario: Provider cannot start
- **WHEN** an action is invoked for a provider whose required settings are missing
- **THEN** the result is failed with the reason the provider reports for the missing setting

#### Scenario: Provider stops with actions waiting
- **WHEN** a provider stops while two of its actions are waiting
- **THEN** both complete as failed
