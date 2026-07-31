# Notification dispatch + delivery channels

| | |
|--|--|
| **Surface** | Backend — `SIMF.Application/Notifications/` |
| **Source** | `INotificationDispatcher` · `NotificationDispatcher` · `INotificationChannel` · `InAppNotificationChannel` · `EmailNotificationChannel` |
| **Consumers** | Every lifecycle event: account state changes, bookings, meetings, reminders, rating prompts, recommendations |
| **Tests** | `NotificationTests.cs`, `NotificationLifecycleTests.cs`, `NotificationChannelTests.cs` · E2E [`api-notification-channels.md`](../../tests/e2e/api-notification-channels.md) |
| **Last reviewed** | 2026-07-31 |

## Purpose

One call site, one `NotificationRequest`, and the platform decides how the message
actually reaches the person. Every lifecycle event in SIMF goes through
`INotificationDispatcher.DispatchAsync`; nothing writes a `Notification` row or
enqueues a notification email directly.

## Shape

```
DispatchAsync(request)
  ├─ D-713 dedup guard        (dispatch-wide policy — skips ALL channels)
  └─ foreach channel by Order
       ├─ channel.ShouldHandle(request)?  no → skip
       └─ channel.SendAsync(request)
```

| Channel | `Order` | Handles | Does |
|---|---:|---|---|
| `InAppNotificationChannel` | 0 | every request | Writes the `Notification` row the app bell reads, stamping `ClickUrl` + `GroupCode` from `NotificationKindCatalog` (D-677) when the caller left them null |
| `EmailNotificationChannel` | 10 | `SendEmail = true` | Resolves the address and enqueues the message; a user with none is logged and skipped, never failed |

`Order` is explicit rather than implied by DI registration order, so the in-app row is
always written **before** any outbound transport and no transport failure can cost the
user their in-app record.

## Why the seam exists (`sms-whatsapp-channels`)

The dispatcher used to hard-code those two deliveries inline, so the "one abstraction"
the implementation-gap report credited did not generalise past email: a third transport
could only be added by editing the dispatcher. Extracting `INotificationChannel` was a
**move, not a rewrite** — the row-building and email-sending code was lifted verbatim,
which is why `NotificationLifecycleTests` (trigger by trigger) is the acceptance test
for the change.

**SMS and WhatsApp are deliberately not implemented.** Both need a procured gateway,
which is an owner-action item, and a stub that silently drops messages would be worse
than none. The deliverable is the seam: adding one is a new class plus one DI line, and
the dispatcher does not change again.

## The dedup guard (D-713)

`NotificationRequest.DeduplicateByRelatedEntity`, with a non-null `RelatedEntityId`,
skips the whole dispatch when a notification of the same kind for the same entity
already exists for that user — "one per (user, kind, entity)". It lives in the
dispatcher, not in a channel, because it is policy about the dispatch as a whole;
inside a channel, every future transport would have to re-implement it and they would
drift.

It is also load-bearing beyond de-duplication: `SessionNotAttendedReminderWorker` and
`MatchRecommendationPushWorker` both use it **instead of a stamp column**, which makes
their scans idempotent by construction. See [`workers.md`](workers.md).

Default `false`, so kinds that are intentionally repeatable are untouched.

## Storage note

`Notification` rows live on **`SIMF_Identity`**. A worker reading `SIMF_App` therefore
cannot share a transaction with a dispatch (D-157), which is exactly why the workers
that need once-only semantics either claim-then-dispatch (D-217) or lean on the dedup
guard.
