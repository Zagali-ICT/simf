# E2E — Gate access rules and Walk-In Mode (D-809)

**Namespace:** `E2E-WIM`
**Surfaces:** the three gate access rules, and the standby walk-in capability
armed from `appsettings` / `set-env-*`.
**Related:** [`cp-admin-gates-operator.md`](cp-admin-gates-operator.md),
[`cp-admin-hall-arrivals.md`](cp-admin-hall-arrivals.md),
[`cp-admin-visitors.md`](cp-admin-visitors.md).

This capability has **no Control Panel page of its own by design** — arming it
requires server access, which is the access control. Scenarios below are driven
through the walk-in desk, the gate console and the configuration file.

---

## Arming and disarming

Every switch defaults to off. Arm by setting the environment variables on the
API host and restarting the app pool, or by editing `appsettings.Production.json`
(the options are read through `IOptionsMonitor`, so a file edit applies without a
restart).

```
SIMF_WalkInMode__Enabled          = true    # master; everything else is inert without it
SIMF_WalkInMode__ExpiresAtUtc     = 2026-11-05T20:00:00Z
SIMF_WalkInMode__QuickRegister    = true
SIMF_WalkInMode__AutoApprove      = true
SIMF_WalkInMode__SessionWalkIn    = true
```

**Disarm:** set `Enabled` to `false` (or let `ExpiresAtUtc` pass). Every rule
returns to its normal behaviour immediately.

---

## Coverage matrix

| Id | Scenario | Mode |
|----|----------|------|
| E2E-WIM-001 | Main gate refuses an unapproved account | Always |
| E2E-WIM-002 | Any gate refuses a profile type not on its allow-list | Always |
| E2E-WIM-003 | Session hall refuses an attendee with no booking | Disarmed |
| E2E-WIM-004 | Session hall admits a registered attendee | Always |
| E2E-WIM-005 | Leaving is never blocked | Always |
| E2E-WIM-006 | Walk-in desk still queues for approval | Disarmed |
| E2E-WIM-007 | Reduced field set is refused | Disarmed |
| E2E-WIM-008 | Walk-in badge cannot be claimed as an app account | Disarmed |
| E2E-WIM-009 | Arrival window is 15 minutes | Disarmed |
| E2E-WIM-010 | Session hall admits an unregistered attendee | Armed |
| E2E-WIM-011 | Desk issues a working badge immediately | Armed |
| E2E-WIM-012 | Quick register accepts a name and one identity document | Armed |
| E2E-WIM-013 | Quick register still demands an identity document | Armed |
| E2E-WIM-014 | A mistyped identity document is still rejected | Armed |
| E2E-WIM-015 | The same person cannot collect two badges | Armed |
| E2E-WIM-016 | The arrival window widens | Armed |

---

## Scenarios — the three gate rules (always apply)

### E2E-WIM-001 — a main gate refuses an unapproved account
```gherkin
Given a visitor account in state PendingApproval that somehow carries a QR
And a perimeter gate "G-MAIN" with no hall bound
When the operator scans that badge at "G-MAIN"
Then the response is HTTP 200 with outcome "Denied"
And the denial reason is "HolderNotApproved"
And the attempt is recorded in the append-only gate-scan log
```

### E2E-WIM-002 — any gate refuses a profile type not on its allow-list
```gherkin
Given a gate "G-VIP" whose allowed profile types are only "VVIP" and "VIP"
And an approved visitor whose profile type is "Visitor"
When the operator scans that badge at "G-VIP"
Then the response is HTTP 200 with outcome "Denied"
And the denial reason is "ProfileTypeNotAllowed"
```
> This rule is **never** relaxed by the walk-in mode. Re-run it armed and the
> result must be identical.

### E2E-WIM-003 — a session hall refuses an attendee with no booking
```gherkin
Given a hall "H-1" with a session live now
And a gate "G-H1" bound to hall "H-1"
And an approved visitor with NO seat reservation for that session
When the operator scans that badge at "G-H1"
Then the response is HTTP 200 with outcome "Denied"
And the denial reason is "BookingRequiredMissing"
And no hall-attendance row is opened for that session
```
> Before D-809 this scenario **admitted** the visitor: the denial reason existed
> in the enum but nothing ever wrote it, so any valid badge opened every hall.

### E2E-WIM-004 — a session hall admits a registered attendee
```gherkin
Given a hall "H-1" with a session live now
And an approved visitor holding an active seat reservation for that session
When the operator scans that badge at the hall-door gate
Then the response is HTTP 200 with outcome "Allowed"
And exactly one hall-attendance row is open for that attendee and session
And the attendance row is keyed by the Identity user id, not the profile id
```

### E2E-WIM-005 — leaving is never blocked
```gherkin
Given an attendee who was admitted to a live session
And their seat reservation is released while they are still inside
When the operator scans their badge in the CheckOut direction
Then the response is HTTP 200 with outcome "Allowed"
And their attendance row is closed
```

---

## Scenarios — disarmed (the shipped default)

### E2E-WIM-006 — the walk-in desk still queues for approval
```gherkin
Given WalkInMode is disarmed
When a desk operator registers a walk-in with the full field set
Then the response is HTTP 200
And the returned QR id is empty
And the account is in state PendingApproval
And the account appears in the pending-visitors queue
```

### E2E-WIM-007 — the reduced field set is refused
```gherkin
Given WalkInMode is disarmed
When a desk operator submits a registration with no Arabic name,
     no nationality, no organisation and no mobile
Then the response is HTTP 400
And the message names the first missing field, bilingually
```
> Proves the reduced set is not reachable simply by omitting fields.

### E2E-WIM-008 — a walk-in badge cannot be claimed as an app account
```gherkin
Given a walk-in registered with NO email address, later approved
And an attacker who has photographed the printed badge
When the attacker posts the badge QR to /app/auth/badge-activation/start
     with their own email address
Then the response is HTTP 404 "badge not recognised"
And no verification code is sent to the attacker's address
```
> The 404 is deliberately the same response an unknown badge gets, so the
> endpoint is not an oracle for which badges exist.

### E2E-WIM-009 — the arrival window is 15 minutes
```gherkin
Given a session starting in 40 minutes
When the operator scans a registered attendee at its hall door
Then no hall attendance is recorded
And the allowed scan carries the advisory "no session attendance" notice
```

---

## Scenarios — armed

### E2E-WIM-010 — a session hall admits an unregistered attendee
```gherkin
Given WalkInMode is armed with SessionWalkIn = true
And an approved visitor with NO seat reservation for the live session
When the operator scans that badge at the hall-door gate
Then the response is HTTP 200 with outcome "Allowed"
And a hall-attendance row is opened
And an open-seating reservation is created for them with no row or seat number
And that reservation has no expiry, so the no-show sweep cannot release it
```

### E2E-WIM-011 — the desk issues a working badge immediately
```gherkin
Given WalkInMode is armed with AutoApprove = true
When a desk operator registers a walk-in visitor
Then the response is HTTP 200 and carries a non-empty QR id
And the account is in state Approved
And the operation log records BOTH Admin.WalkInRegistered
    AND Admin.VisitorAutoApproved for that account
And scanning the printed badge at a perimeter gate is Allowed
```
> The two audit events are written together on purpose: an auditor can diff all
> walk-ins against those that skipped review from one table.

### E2E-WIM-012 — quick register accepts a name and one identity document
```gherkin
Given WalkInMode is armed with QuickRegister = true
When a desk operator registers a visitor with only an English name,
     a profile type and a national ID
Then the response is HTTP 200
And the stored profile carries the name in BOTH language columns
And the stored nationality id is 0
And the stored organisation is null
And the operation log records Admin.QuickRegistered with the omitted fields
```

### E2E-WIM-013 — quick register still demands an identity document
```gherkin
Given WalkInMode is armed with QuickRegister = true
And QuickRegisterRequiresIdentityDocument is true (the default)
When a desk operator submits a registration with no national ID,
     no Iqama and no passport number
Then the response is HTTP 400
And the message asks for an identity document, bilingually
```
> This is the field quick mode keeps. It is the only thing preventing one person
> collecting several badges, and the encrypted columns cannot be reconstructed
> after the event.

### E2E-WIM-014 — a mistyped identity document is still rejected
```gherkin
Given WalkInMode is armed with QuickRegister = true
When a desk operator submits a national ID of the right shape
     but a wrong Luhn check digit
Then the response is HTTP 400
```
> Shape rules stayed in the validator and always apply: a mistyped id would
> create a false-unique row and defeat duplicate detection permanently.

### E2E-WIM-015 — the same person cannot collect two badges
```gherkin
Given WalkInMode is armed with QuickRegister = true
And a visitor already registered with national ID "1xxxxxxxxx"
When a desk operator registers again with the same national ID
Then the response is HTTP 409 DUPLICATE_IDENTITY
```

### E2E-WIM-016 — the arrival window widens
```gherkin
Given WalkInMode is armed with ArrivalGraceMinutes = 60
And a session starting in 40 minutes
When the operator scans a registered attendee at its hall door
Then hall attendance IS recorded
And the scan carries no advisory notice
```

---

## Post-event reconciliation

```gherkin
Given the event has finished and WalkInMode has been disarmed
When an administrator exports the operation log for the event window
Then every auto-approved account is listed under Admin.VisitorAutoApproved
And every reduced-data registration is listed under Admin.QuickRegistered
     with the fields it omitted
And each can be reviewed and either kept Approved or set to Disabled
```
> Export per day: the operation-log export caps at 5,000 rows.

---

## Notes for the runner

- **Arming is not a UI action.** Set the environment variables on the API host.
  There is deliberately no Control Panel switch.
- **Disarm after every armed scenario**, or later runs inherit the state.
- The gate scan endpoints carry **no rate limit** (D-809), so a burst of
  scenarios will not trip a 429. If one appears, the `operational` policy has
  been mis-applied.
- Denials are **HTTP 200 with a Denied outcome**, not an error envelope. Assert
  on the outcome and denial reason, never on the status code alone.
