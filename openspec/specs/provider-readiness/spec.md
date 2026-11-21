# provider-readiness Specification

## Purpose
TBD - created by archiving change provider-availability. Update Purpose after archive.
## Requirements
### Requirement: Provider Readiness
Every provider MUST have one of three readiness states, worked out by the host from the provider's own declarations and
its configuration, and never written to the configuration:

- **Not available**: the provider declared that it does not apply on this PC
- **Needs setup**: it is available, and a setting it requires has no value (a required secret set by sign-in counts)
- **Ready**: neither of the above

A provider that declares nothing is available, so it is ready unless a required setting is missing. Computing the
state MUST NOT use the network and MUST NOT start the provider's sampling.

#### Scenario: Home Assistant without a token
- **WHEN** Home Assistant has an address and no token
- **THEN** its readiness is needs setup

#### Scenario: Nothing declared
- **WHEN** a third-party provider declares no availability and no required setting
- **THEN** its readiness is ready

#### Scenario: Becoming ready
- **WHEN** the user saves the missing token
- **THEN** the provider's readiness is ready without restarting the host

### Requirement: Declaring Availability And Guidance
The SDK MUST let a provider declare whether it applies on this PC, and a short guidance for a user whose provider is not
ready: a sentence, and optionally one web address. The availability check MUST be fast, side-effect free and safe to ask
repeatedly; an exception from it means available and is logged once. The host shows the guidance of a provider that is
not available or needs setup, and never one of a ready provider.

#### Scenario: Guidance with a link
- **WHEN** a provider is not available and its guidance has a web address
- **THEN** the Providers section shows the sentence and the address as a link that opens in the browser

#### Scenario: A check that throws
- **WHEN** a provider's availability check throws
- **THEN** the provider is treated as available and the log says so once

### Requirement: Readiness Is The Only Visibility Rule
The host MUST decide what the pickers offer from readiness alone. No setting turns a ready provider off, and no
provider is shown or hidden for any other reason.

#### Scenario: A ready provider with no demand
- **WHEN** a provider is ready and no widget uses it
- **THEN** it is still offered in the pickers

