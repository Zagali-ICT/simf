# E2E test catalogue — Notification channel abstraction (`INotificationChannel`)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7). Registry row in
> [`README.md`](README.md).

| | |
|--|--|
| **Page** | [`mobile-notifications.md`](mobile-notifications.md) · [`cp-account-notifications.md`](cp-account-notifications.md) |
| **Route** | No HTTP route — the delivery seam behind `INotificationDispatcher` |
| **Surface** | Backend (Application layer) |
| **Test runner** | xUnit + `SimfApiFactory` (`tests/SIMF.Api.Tests/NotificationChannelTests.cs`) |
| **Auth setup** | None — resolved from DI and driven directly |
| **Last reviewed** | 2026-07-31 |

## What changed and why

`sms-whatsapp-channels`. A case-insensitive search for `sms|whatsapp` across the
backend returned exactly one unrelated hit (the word "mechanisms"). More to the
point, `INotificationDispatcher` hard-coded two deliveries — write the in-app row,
then enqueue an email — so the "one abstraction" the gap report credited did not
generalise past email: a third transport could only be added by editing the
dispatcher.

`INotificationChannel` is now that seam, with two implementations:

| Channel | `Order` | Handles |
|---|---:|---|
| `InAppNotificationChannel` | 0 | every request — the in-app row is the baseline delivery |
| `EmailNotificationChannel` | 10 | only requests with `SendEmail = true` |

The dispatcher keeps exactly one thing: the D-713 one-per-(user, kind, entity) dedup
guard, which is policy about the dispatch as a whole rather than about any one
transport. The row-building and email-sending code moved **verbatim** — this is a
move, not a rewrite, so every existing notification behaves identically.

## Scope — what is deliberately NOT here

**No SMS or WhatsApp channel.** Both need a procured gateway, which is an
owner-action item, and shipping a stub that silently drops messages would be worse
than shipping none. The deliverable is the seam: when a provider is chosen, the whole
change is one new class implementing `INotificationChannel` plus one DI line. The
dispatcher does not change again.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-NCH-001 | Both shipped channels are registered in a deterministic order | happy | P0 | automated |
| E2E-NCH-002 | In-app handles every request; email only opt-ins | happy | P0 | automated |
| E2E-NCH-003 | The dispatcher writes the in-app row through the channel | regression | P0 | automated |
| E2E-NCH-004 | The dedup guard short-circuits every channel | regression | P0 | automated |
| E2E-NCH-005 | A user with no email on file is skipped, not failed | resilience | P1 | code-reviewed |
| E2E-NCH-006 | Every pre-existing notification trigger still fires | regression | P0 | automated |

## Scenarios

### E2E-NCH-001 — Deterministic channel order

```gherkin
Feature: A notification delivery seam
  As the platform
  I want transports to be pluggable
  So that adding SMS does not mean editing the dispatcher

Scenario: In-app first, email second
  When the registered INotificationChannel implementations are resolved and ordered
  Then their names are exactly ["in-app", "email"]
  And the in-app channel's Order is lower than the email channel's
```

Order is explicit rather than implied by DI registration order, so the in-app row is
written before any outbound transport runs and no transport failure can cost the
user their in-app record.

**Evidence captured:** `NotificationChannelTests.Both_shipped_channels_are_registered_in_a_deterministic_order`.

### E2E-NCH-002 — Each channel decides for itself

```gherkin
Scenario Outline: ShouldHandle
  Given a notification request with sendEmail = <sendEmail>
  Then the in-app channel handles it: true
  And the email channel handles it: <sendEmail>
  Examples:
    | sendEmail |
    | false     |
    | true      |
```

A channel that returns false is skipped entirely — no work, no log noise.

**Evidence captured:** `NotificationChannelTests.In_app_channel_handles_every_request_email_channel_only_opt_ins`.

### E2E-NCH-003 — Behaviour parity

```gherkin
Scenario: Routing through the seam produces exactly the row the inline code did
  When a SessionReminder is dispatched for a user with relatedEntityId {sessionId}
  Then a notification row exists for that user, kind and related entity
  And its ClickUrl and GroupCode are stamped from NotificationKindCatalog (D-677)
```

**Evidence captured:** `NotificationChannelTests.Dispatcher_writes_the_in_app_row_through_the_channel`,
plus the whole pre-existing `NotificationTests` / `NotificationLifecycleTests` suites.

### E2E-NCH-004 — The dedup guard is dispatch-wide

```gherkin
Scenario: A deduplicated kind fires once across ALL channels
  Given a SessionNotAttended request with deduplicateByRelatedEntity = true
  When it is dispatched twice for the same user and session
  Then exactly 1 notification row exists
  And no second email was enqueued either
```

The guard sits in front of the channel loop, not inside a channel — otherwise each
new transport would have to re-implement it and they would drift.

**Evidence captured:** `NotificationChannelTests.Dedup_guard_still_short_circuits_every_channel`.

### E2E-NCH-005 — Missing email address

```gherkin
Scenario: An address-less user still gets the in-app row
  Given a user with no email on file
  When a notification with sendEmail = true is dispatched
  Then the in-app row is written
  And the email channel logs a warning and returns
  And no exception propagates
```

Preserved verbatim from the pre-seam dispatcher.

### E2E-NCH-006 — Full trigger regression

```gherkin
Scenario: Every P13 lifecycle trigger still delivers
  When the NotificationLifecycleTests suite runs
  Then every trigger writes its in-app row and, where applicable, queues its email
```

This is the real acceptance test for a "move, not a rewrite" claim.

**Evidence captured:** `tests/SIMF.Api.Tests/NotificationLifecycleTests.cs`, unchanged.
