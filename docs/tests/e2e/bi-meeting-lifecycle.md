# E2E test catalogue — Bi-Meeting full lifecycle (Speaker + Delegation)

> **Authority:** SIMF E2E test catalogue (D-133 / D-245). This is the **cross-surface
> lifecycle** catalogue that ties the per-page files together and proves the end-to-end
> meeting journey for BOTH flows. Per-page files stay the source of truth for each page's
> local cases; this file is the source of truth for the **lifecycle + the owner rules**
> (no-error-on-same-time, reserved-slot-hidden, email-both-parties, receiver-approve-link,
> pre-select-existing-slot, 15-minute reminder).

| | |
|--|--|
| **Flows** | Speaker meeting (`SpeakerMeetingRequest`) · Delegation meeting (`DelegationMeetingRequest`) |
| **Surfaces** | Mobile app (requester + receiver) · Control Panel (admin desks) · Website (`/meeting/confirm`) |
| **Test runner** | xUnit + `WebApplicationFactory` (backend lifecycle) · Flutter widget (app) · Chrome DevTools MCP + PowerShell (CP/Web live) |
| **Auth setup** | Admin: `superadmin@zagali-ict.com` + TOTP via `Get-Totp`. App: an approved visitor holding `AllowsSpeakerMeeting` / `AllowsDelegationMeeting`. |
| **Statuses** | `Pending=0` · `Accepted=1` · `Rejected=2` · `Cancelled=3` · `AwaitingSpeaker=4` · `Done=5` |
| **Last reviewed** | 2026-07-25 |

## Lifecycle at a glance

```
                 (app: reserve a slot)          (admin desk: 3-button)
 Requester  ──────────────────────────►  Pending  ──────────────────────────►  AwaitingSpeaker
                                             │  Approve (VerbalConfirmed=false)      │
                                             │                                       │  receiver acts
   Decline ◄── Rejected                      │  Confirm (VerbalConfirmed=true)       ▼  (email link OR in-app tap)
   Cancel  ◄── Cancelled                     └─────────────────────────────►  Accepted
                                                                                     │  operator check-in
                                                                                     ▼
                                                                                   Done
                          15-min reminder fires once while status == Accepted with a bound slot
```

## Owner rules under test (cross-cutting)

- **R1 — no error on multiple requests for the same time.** Two different requesters may reserve the
  same free slot; both sit `Pending`; the admin approves only one. The loser is never shown an error
  at *submit*; the single-winner is enforced only at *admin approve*.
- **R2 — a reserved (slot-holding: Accepted/AwaitingSpeaker/Done) slot never appears** in the
  app's selectable slot list for that target.
- **R3 — every action emails BOTH parties** (requester + receiver) to keep them up to date.
- **R4 — after admin Approve, the receiver gets an email with an Approve link** they press to confirm;
  the confirmation then updates all parties.
- **R8 — re-opening the request pre-selects the requester's existing slot** and changing it **moves**
  the request (no duplicate, no error).
- **R0 — all app validation/failure feedback is visible** (inline in the sheet, not an occluded toast).

## Coverage matrix

| ID | Scenario | Flow | Type | Priority |
|----|----------|------|------|----------|
| E2E-BML-001 | Golden lifecycle: reserve → Approve → receiver confirm (email link) → check-in → Done | Speaker | happy | P0 |
| E2E-BML-002 | Golden lifecycle: reserve → Approve → receiver confirm (in-app tap) → check-in → Done | Delegation | happy | P0 |
| E2E-BML-003 | Admin **Confirm** (verbal) books directly: Pending → Accepted (no receiver step) | Both | happy | P0 |
| E2E-BML-004 | Admin **Decline** with justification: Pending → Rejected; both parties emailed | Both | happy | P1 |
| E2E-BML-005 | Cancel after approval: AwaitingSpeaker/Accepted → Cancelled; receiver retraction email | Both | happy | P1 |
| E2E-BML-006 | **R1** two requesters, same slot → both Pending, neither errors; admin approves one | Both | rule | P0 |
| E2E-BML-007 | **R1** loser cannot then be approved onto the now-held slot (admin 409, not requester) | Both | rule | P0 |
| E2E-BML-008 | **R2** an approved/held slot is absent from `available-slots` for new requesters | Both | rule | P0 |
| E2E-BML-009 | **R3** email matrix: submit/approve/decline/confirm/check-in reach BOTH parties | Both | rule | P0 |
| E2E-BML-010 | **R4** delegation Approve emails every eligible target member an Approve link; first click confirms | Delegation | rule | P0 |
| E2E-BML-011 | **R4** speaker Approve emails the speaker Approve/Reject links; Approve confirms | Speaker | rule | P0 |
| E2E-BML-012 | **R8** re-open pre-selects my slot; changing it moves my request (no duplicate) | Both | rule | P0 |
| E2E-BML-013 | **R0** submit with no subject / no-slots delegation shows a VISIBLE inline error | Both | rule | P1 |
| E2E-BML-014 | **15-min reminder** fires once for an Accepted meeting to both parties; not for Cancelled | Both | rule | P0 |
| E2E-BML-015 | Receiver confirm link is single-use + expires (72h); a used/expired link → neutral invalid | Both | error | P1 |
| E2E-BML-016 | Home → Meeting page lists ALL my requests with status; two top request buttons | Both | ux | P1 |

## Scenarios

### E2E-BML-001 — Speaker golden lifecycle (email-link confirm)

```gherkin
Feature: Speaker meeting full lifecycle
Background:
  Given a speaker "Dr. Noor" is active and AllowsMeetingRequests, with an availability window today 10:00-11:00 @ 30-min slots
  And an approved visitor "Sara" holds AllowsSpeakerMeeting

Scenario: reserve -> approve -> speaker confirms by email link -> check-in -> Done
  When Sara opens the speaker profile and picks the 10:00 slot with subject "Cooperation" and sends
  Then a SpeakerMeetingRequest is created Pending with SlotStartUtc = today 10:00
  And Sara receives an in-app notification AND an email "request received" (R3)
  And the speaker receives an email "you have a new meeting request" (R3)
  When an admin opens /admin/speaker-meeting-requests, Responds, binds a Meeting hall + the 10:00 free slot, and clicks Approve
  Then the request moves to AwaitingSpeaker
  And Sara receives an email "approved, awaiting the speaker's confirmation" (R3)
  And the speaker receives an email containing single-use Approve and Reject links (R4)
  When the speaker opens the Approve link (GET /app/meeting-actions/{token} previews without consuming) and POSTs it
  Then the request moves to Accepted and both Sara and the speaker are emailed "meeting confirmed" (R3)
  When an operator clicks Check in on the Accepted row
  Then the request moves to Done, CheckedInAt is stamped, and both parties are emailed "meeting recorded" (R3)
```

**Evidence:** CP screenshots of each desk state; app screenshot of the confirmation; `OperationLog` rows for `SpeakerMeetingRequest.Submitted/Responded/CheckedIn`; queued `EmailMessage` rows per step.

### E2E-BML-002 — Delegation golden lifecycle (in-app tap confirm)

```gherkin
Scenario: reserve -> approve -> a target member confirms in-app -> check-in -> Done
  Given invited delegations "SA" and "EG"; "Sara" (SA) holds AllowsDelegationMeeting; "Omar" (EG) holds AllowsDelegationMeeting
  And EG has an availability window producing a 12:00 slot
  When Sara requests "SA meets EG", 10 attendees, subject "Trade", picking the 12:00 slot
  Then a DelegationMeetingRequest is Pending; Sara + every EG member are emailed appropriately (R3)
  When an admin Approves with a bound hall/slot
  Then status = AwaitingSpeaker; Sara emailed "approved, awaiting confirmation"; every eligible EG member emailed an Approve link (R4) AND an in-app "please confirm" notification
  When Omar taps the in-app notification -> POST /app/delegation-meeting-requests/{id}/confirm
  Then status = Accepted (race-safe: a second member's tap 409s cleanly); Sara + EG emailed "confirmed" (R3)
  When an operator checks it in
  Then status = Done; both parties emailed
```

### E2E-BML-003 — Admin Confirm (verbal) books directly

```gherkin
Scenario: Confirm short-circuits the receiver step
  Given a Pending request with the admin holding the receiver's verbal agreement
  When the admin binds a hall/slot and clicks Confirm (VerbalConfirmed=true)
  Then status = Accepted immediately (no AwaitingSpeaker, no link email)
  And both parties are emailed "meeting confirmed" (R3)
```

### E2E-BML-004 — Decline with justification

```gherkin
Scenario: Decline requires a note and notifies both
  When the admin clicks Decline without a justification note
  Then the CP blocks with "A justification is required to decline" (Admin.Meetings.Decline.NoteRequired) and nothing is written
  When the admin enters a note and Declines
  Then status = Rejected; the requester is emailed "declined" (R3, SendEmail now true); the receiver is emailed the outcome too
```

### E2E-BML-006 — R1: two requesters, same slot, no error

```gherkin
Scenario: concurrent same-slot reservations both succeed
  Given the 10:00 slot is free (no slot-holding meeting yet)
  When requester A reserves 10:00 AND requester B reserves 10:00
  Then BOTH requests are created Pending with SlotStartUtc = 10:00
  And neither requester receives any error (no 409 at submit — speaker submit-time slot re-check removed)
  And the 10:00 slot still appears in available-slots (Pending does not hold a slot)
```

### E2E-BML-007 — R1: only one can be approved

```gherkin
Scenario: admin approves one; the other cannot be approved onto the held slot
  Given A and B are both Pending on 10:00
  When the admin approves A onto hall H at 10:00 (A -> AwaitingSpeaker/Accepted, slot-holding)
  Then 10:00 disappears from available-slots (R2)
  When the admin tries to approve B onto hall H at 10:00
  Then the ADMIN gets 409 "That slot is no longer available" (single-winner enforced at approve, inside the Serializable transaction) — B stays Pending, B's requester never saw an error
```

### E2E-BML-008 — R2: reserved slot hidden

```gherkin
Scenario: a held slot never appears to new requesters
  Given a meeting is Accepted (or AwaitingSpeaker or Done) on the 10:00 slot
  When a new requester loads the target's available-slots
  Then 10:00 is absent from the list (both SpeakerAvailabilityService and DelegationAvailabilityService subtract SlotHolding)
```

### E2E-BML-009 — R3: email matrix

```gherkin
Scenario Outline: every action emails both parties
  When the "<action>" occurs
  Then the requester (sender) is emailed AND the receiver is emailed
  Examples:
    | action                     |
    | submit                     |
    | approve (awaiting confirm) |
    | decline / cancel           |
    | confirm (verbal or receiver)|
    | check-in (Done)            |
    | 15-minute reminder         |
  # Receiver = the speaker's contact email (speaker flow) or every eligible target-delegation member (delegation flow)
```

### E2E-BML-010 / -011 — R4: receiver approve-by-link

```gherkin
Scenario: delegation Approve emails an Approve link to each eligible target member
  When the admin Approves a delegation request
  Then a single-use DelegationMeetingActionToken is minted (new additive table, D-767 R4; the frozen speaker MeetingActionToken table is untouched)
  And each eligible EG member is emailed a Confirm link to /meeting/confirm?token=... (the same public page + endpoints the speaker links use)
  And the in-app "please confirm" card is still delivered (it deep-links to the tap-confirm)
  When any one member opens the link and confirms (GET previews without consuming; POST confirms)
  Then the request moves to Accepted (first click wins; a second link click OR the in-app tap -> neutral invalid) and the requester is emailed + notified

Scenario: speaker Approve emails Approve/Reject links to the speaker (existing behavior, regression-locked)
  When the admin Approves a speaker request
  Then the speaker is emailed Approve and Reject links; opening Approve confirms; opening Reject reverts
```

### E2E-BML-012 — R8: pre-select existing slot + move

```gherkin
Scenario: re-open shows my slot selected; changing it moves the request
  Given Sara already has a Pending request for speaker "Dr. Noor" at 10:00
  When Sara re-opens the request sheet for "Dr. Noor"
  Then the 10:00 chip is pre-selected (from her existing pending request)
  When Sara picks 10:30 and sends
  Then her existing request is UPDATED to 10:30 (no duplicate row, no 409)
```

### E2E-BML-013 — R0: visible inline error

```gherkin
Scenario: no-slots delegation submit with empty subject shows a visible inline error
  Given a delegation with no availability windows (the sheet shows "no available slots")
  When Sara taps Send with an empty subject
  Then a VISIBLE inline error appears inside the sheet ("A subject is required") — not an occluded snackbar
  And the sheet stays open so she can correct and retry
```

### E2E-BML-014 — 15-minute reminder

```gherkin
Scenario: reminder fires once for a confirmed meeting
  Given an Accepted meeting with SlotStartUtc 15 minutes from now and ReminderSentUtc null
  When MeetingReminderWorker.RunReminderScanAsync runs
  Then ReminderSentUtc is stamped and a MeetingReminder (in-app + email) is dispatched to BOTH parties exactly once
  And a subsequent run does NOT re-send
  And a meeting Cancelled after the batch load is NOT reminded (conditional claim)
```

### E2E-BML-016 — Home → Meeting page shows all my requests

```gherkin
Scenario: the meetings page lists every one of my requests with status
  Given Sara has requests in Pending, AwaitingSpeaker, Accepted, Rejected, Done
  When she opens Home -> Meeting
  Then two top buttons appear: "Request a speaker meeting" and "Request a delegation meeting" (per her flags)
  And a list shows ALL her meeting requests, each with a status chip (pending/approved/confirmed/rejected/done)
```

---

_Last reviewed:_ 2026-07-25 by SIMF Team.
