# E2E test catalogue — Offline gate roster (`GET /api/v1/app/gates/offline-roster`)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7). Registry row in
> [`README.md`](README.md).

| | |
|--|--|
| **Page** | [`mobile-gate-scan.md`](mobile-gate-scan.md) (the scanner that consumes it) |
| **Route** | `GET /api/v1/app/gates/offline-roster?since=` |
| **Surface** | App API, authenticated — `Gates.Operate` + an approved account |
| **Test runner** | xUnit + `SimfApiFactory` (`tests/SIMF.Api.Tests/GateOfflineRosterTests.cs`) |
| **Auth setup** | Sign in as a gate operator; the `Get-Totp` helper covers the admin steps |
| **Last reviewed** | 2026-08-14 |

## What it is for

A device at a door could already answer three questions from the badge alone:
is it genuine (it decrypts), is it from the open year, and is this tier admitted
at this gate. It could not answer **"is this person admitted"** or **"do they
hold a seat in THIS session"**, so a hall door abstained on both — and an
abstention at a hall door is a queue.

This is the missing third. After it, the only thing a device cannot decide
offline is change its mind: a revocation issued after its last sync. That is
bounded by the roster's expiry and closed by reconciliation — one residual gap
rather than one per question.

## Scoped by seat reservation, not by attendee list

That scoping is what makes it affordable. The set is bounded by **hall capacity,
not event size** — a 400-seat hall downloads at most 400 people however many
thousands attend — and it is precisely the set the door is asked about, so
nothing irrelevant travels. It also shrinks as the schedule narrows: a device
serving one evening carries only that evening.

Only a **confirmed, still-held** reservation counts. `SeatReservation` carries an
approval workflow, so a pending or rejected request must never read as an
admitted seat at the door, and a released one is somebody else's seat now.

## Three constraints that are load-bearing

- **Scoped to the operator's own gates.** `Gates.Operate` is held by every Staff
  and Moderator account, not only the provisioned tablets. A roster is attendee
  names and movements — more sensitive than the badge key, which is already
  scoped exactly this way (`handOutKey = armed && rules.Count > 0`).
- **Minimum fields.** No identity-document number, no mobile, no email, no
  organisation. Those columns are encrypted at rest precisely so they do not
  travel; a gate needs a decision and a name to show the operator.
- **Stamped and expiring.** `IssuedAt` doubles as the delta cursor; `ValidUntil`
  is explicit, and the device refuses a roster older than it. A stale roster
  admits someone approved this morning and disabled since, which is the failure
  the abstention existed to prevent.

Every verdict a device reaches with it stays **advisory**. The scan is queued and
the server re-decides it against live data.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-GOR-001 | A reserved attendee is downloaded for a hall the operator works | happy | P0 | automated |
| E2E-GOR-002 | An operator does not receive a hall they do not work | security | P0 | automated |
| E2E-GOR-003 | An operator with no hall door gets an empty roster, not an error | empty | P1 | automated |
| E2E-GOR-004 | Only a confirmed reservation reads as an expected attendee | correctness | P0 | automated |
| E2E-GOR-005 | A released seat drops out | correctness | P1 | automated |
| E2E-GOR-006 | The since-cursor returns only what appeared after it | efficiency | P1 | automated |
| E2E-GOR-007 | A general-admission hold still says the person is expected | happy | P1 | automated |
| E2E-GOR-008 | The roster carries no identity document, mobile, email or organisation | security | P0 | automated |
| E2E-GOR-009 | An account without Gates.Operate is refused | auth-gate | P0 | manual |
| E2E-GOR-010 | A device refuses a roster older than ValidUntil and abstains | resilience | P1 | manual |

## Scenarios

```gherkin
Scenario: E2E-GOR-001 A reserved attendee is downloaded for a hall the operator works
  Given a hall with a live session
    And the operator is assigned to a hall door on it
    And an approved attendee holds a confirmed seat A7 in that session
   When the device fetches the offline roster
   Then the response contains that attendee
    And it carries the session, the hall, row A and seat 7
    And isAdmitted is true - a decided boolean, not a raw state for the device
        to re-interpret
    And validUntil is later than issuedAt

Scenario: E2E-GOR-002 An operator does not receive a hall they do not work
  Given a confirmed reservation in a hall served by ANOTHER operator's door
   When this operator fetches the offline roster
   Then that attendee is absent
    And no name, seat or movement of theirs appears anywhere in the response

Scenario: E2E-GOR-003 An operator with no hall door gets an empty roster
  Given an operator assigned only to a perimeter gate
   When they fetch the offline roster
   Then the response is 200 with an empty attendee list
    And it still carries issuedAt and validUntil, so the device can cache it

Scenario: E2E-GOR-004 Only a confirmed reservation reads as an expected attendee
  Given a seat request in the Pending, Rejected or Cancelled state
   When the device fetches the offline roster
   Then that attendee is absent - a request that was never approved must never
        read as an admitted seat at the door

Scenario: E2E-GOR-005 A released seat drops out
  Given a confirmed reservation that has since been released
   When the device fetches the offline roster
   Then that attendee is absent - the seat is somebody else's now

Scenario: E2E-GOR-006 The since-cursor returns only what appeared after it
  Given one reservation created two hours ago and one created five minutes ago
   When the device fetches with since = one hour ago
   Then only the newer reservation is returned

Scenario: E2E-GOR-007 A general-admission hold still says the person is expected
  Given a confirmed hold with no row and no seat number
   When the device fetches the offline roster
   Then the attendee is present with a null row and a null seat
    And the row still answers "this person is expected in this session"

Scenario: E2E-GOR-008 The roster carries no identity document, mobile, email or organisation
  Given any confirmed reservation
   When the device fetches the offline roster
   Then the response body contains no identity-document number, mobile number,
        email address or organisation name for any attendee

Scenario: E2E-GOR-009 An account without Gates.Operate is refused
  Given a signed-in account that does not hold "Gates.Operate"
   When it calls the offline roster
   Then the response is 403

Scenario: E2E-GOR-010 A device refuses a roster older than ValidUntil and abstains
  Given a cached roster whose validUntil has passed
   When a badge is scanned with no network
   Then the device abstains ("queued, decision pending") rather than deciding
        from stale data
```

## Not covered here

- The badge itself — that is
  [`api-badge-account-creation.md`](api-badge-account-creation.md) and the codec
  fixtures pinned in both languages.
- The offline gate rules and badge key — `GET /app/gates/offline-config`.
