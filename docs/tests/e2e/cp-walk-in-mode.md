# E2E — Gate access rules and Walk-In Mode (D-819)

**Namespace:** `E2E-WIM`
**Surfaces:** the three gate access rules, and the standby walk-in capability
armed from `appsettings` / `set-env-*`.
**Related:** [`cp-admin-gates-operator.md`](cp-admin-gates-operator.md),
[`cp-admin-hall-arrivals.md`](cp-admin-hall-arrivals.md),
[`cp-admin-visitors.md`](cp-admin-visitors.md).
**Operator guide:** [`SIMF-Offline-Badge-Desk-Guide.md`](../../manuals/SIMF-Offline-Badge-Desk-Guide.md)
— provisioning and running the offline desk (E2E-WIM-017..024).

**Split control, since D-947.** The two per-mode flags — quick register and
auto-approve — are turned on and off by an admin on `/admin/walk-in-mode`,
so they can change during an event without a deploy. The MASTER switch
(`WalkInMode:Enabled` and its window) is still armed from `appsettings` /
`set-env-*`, and that remains the access control: both modes resolve as
`IsArmed(now) && flag`, so no toggle can arm walk-in registration on an estate
that never enabled it.

Scenarios below are driven through the walk-in desk, the gate console, the
configuration file, and — for E2E-WIM-031..035 — the Control Panel page.

---

## Arming and disarming

Every switch defaults to off. Arm by setting the environment variables on the
API host and restarting the app pool, or by editing `appsettings.Production.json`
(the options are read through `IOptionsMonitor`, so a file edit applies without a
restart).

```
SIMF_API_WalkInMode__Enabled          = true    # master; everything else is inert without it
SIMF_API_WalkInMode__ExpiresAt        = 2026-11-05T20:00:00
SIMF_API_WalkInMode__QuickRegister    = true
SIMF_API_WalkInMode__AutoApprove      = true
SIMF_API_WalkInMode__SessionWalkIn    = true
```

**Disarm:** set `Enabled` to `false` (or let `ExpiresAt` pass). Every rule
returns to its normal behaviour immediately.

The key is `ExpiresAt`, **not** `ExpiresAtUtc`, and the value is **Saudi
wall-clock with no `Z`** — this block named a `...Utc` key for a long time and
the difference is not cosmetic in either half. A key that does not exist binds
to nothing, so an operator who armed the mode from this block got no expiry at
all: `AutoApprove` skips the approval queue, and it would have stayed on until
somebody noticed and set `Enabled=false` by hand. The value is compared against
`timeProvider.SimfNow()`, which is the instant re-expressed at +03:00
(`SimfClock`), so a `Z`-suffixed time is read as local and disarms three hours
late.

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
| E2E-WIM-015 | A repeated national ID is accepted at the desk (D-945) | Armed |
| E2E-WIM-016 | The arrival window widens | Armed |
| E2E-WIM-017 | Offline upload is refused while disarmed | Disarmed |
| E2E-WIM-018 | An uploaded badge works at the gate | Armed |
| E2E-WIM-019 | Re-uploading a batch changes nothing | Armed |
| E2E-WIM-020 | One bad row does not fail the batch | Armed |
| E2E-WIM-021 | An uploaded duplicate identity is accepted (D-945) | Armed |
| E2E-WIM-022 | An uploaded badge is pending without auto-approve | Armed |
| E2E-WIM-023 | A foreign-key badge is not recognised | Armed |
| E2E-WIM-024 | The desk reconciles to zero | Armed |
| E2E-WIM-025 | Offline config withholds the key while disarmed | Disarmed |
| E2E-WIM-026 | A scanner admits a badge offline | Armed |
| E2E-WIM-027 | A scanner refuses a disallowed type offline | Armed |
| E2E-WIM-028 | A scanner abstains at an offline hall door | Armed |
| E2E-WIM-029 | A mistyped identity document is rejected by name | Armed |
| E2E-WIM-030 | F3 corrects a rejected row without reprinting | Armed |

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
> Before D-819 this scenario **admitted** the visitor: the denial reason existed
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
> This is the field quick mode keeps. It is how an attendee is identified on the
> day at all, and the encrypted columns cannot be reconstructed after the event.
> It does NOT bound one person to one badge — that was the cross-profile
> duplicate guard, removed by D-945.

### E2E-WIM-014 — a mistyped identity document is still rejected
```gherkin
Given WalkInMode is armed with QuickRegister = true
When a desk operator submits a national ID of the right shape
     but a wrong Luhn check digit
Then the response is HTTP 400
```
> Shape rules stayed in the validator and always apply. They are now the ONLY
> thing standing between a mistyped id and the badge printed from it, since
> D-945 removed the duplicate guard that used to catch the consequence.

### E2E-WIM-015 — a repeated national ID is accepted at the desk (D-945)
```gherkin
Given WalkInMode is armed with QuickRegister = true
And a visitor already registered with national ID "1xxxxxxxxx"
When a desk operator registers again with the same national ID
Then the response is HTTP 200 and a second account is created
# The cross-profile duplicate guard was removed on owner instruction: a
# visitor whose number already sat on an earlier profile could not register
# at all, and the desk had no way to release it. A second badge for one
# person is now an operator-visible mistake, not a server-side refusal.
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

## Scenarios — the offline badge desk (D-820)

The desk is `SIMF.BadgeDesk`, a Windows application. It prints badges with no
network at all, then uploads the shift through
`POST /api/v1/admin/offline/batch`.

### E2E-WIM-017 — offline upload is refused while disarmed
```gherkin
Given WalkInMode is disarmed
And an administrator holding Visitors.RegisterOnsite
When they post a batch of one registration to /admin/offline/batch
Then the response is HTTP 403 OFFLINE_UPLOAD_DISABLED
And no account is created
```
> The permission is not the gate here. The switch is.

### E2E-WIM-018 — an uploaded badge works at the gate
```gherkin
Given WalkInMode is armed with OfflineUpload, AutoApprove and AcceptOfflineBadges
And BadgeKey on the API matches the key the desk was provisioned with
And the desk printed badge sequence 3000042 for profile-type code 1
When the desk uploads that registration
Then the response is HTTP 200 with one result of status "Created"
And its qrId is "W00003000042"
And the account is Approved
When an operator later scans the PRINTED encrypted badge at a perimeter gate
Then the response is HTTP 200 with outcome "Allowed"
```
> The scanner sends the whole encrypted blob and the server decrypts it
> independently, so the audit row is exactly what was physically presented.

### E2E-WIM-019 — re-uploading a batch changes nothing
```gherkin
Given a batch that has already been uploaded successfully
When the desk uploads the identical batch again
Then the response is HTTP 200
And every result carries status "AlreadyUploaded"
And exactly one account still exists for each sequence
```
> The desk retries after a dropped connection. A second account for a badge
> already handed out would be a second person in every count in the system.

### E2E-WIM-020 — one bad row does not fail the batch
```gherkin
Given WalkInMode is armed with OfflineUpload
And a batch of four rows where the second names a profile-type code that
    does not exist and the third carries a sequence above the maximum
When the desk uploads that batch
Then the response is HTTP 200
And two results are "Created" and two are "Rejected"
And both rejections carry error code OFFLINE_BADGE_INVALID
```

### E2E-WIM-021 — an uploaded duplicate identity is accepted (D-945)
```gherkin
Given a batch of two rows carrying the SAME national ID
When the desk uploads it
Then BOTH results are "Created"
And no row is rejected with DUPLICATE_IDENTITY — that code no longer exists
# Batch rejection on a repeated identity went with the cross-profile guard.
# The OFFLINE_BADGE_SEQUENCE_TAKEN conflict on IX_UserProfiles_QrId is a
# different guard and still applies.
```

### E2E-WIM-022 — an uploaded badge is pending without auto-approve
```gherkin
Given WalkInMode is armed with OfflineUpload but NOT AutoApprove
When the desk uploads a registration
Then the result status is "CreatedPendingApproval"
And scanning that printed badge at a gate is Denied with HolderNotApproved
```
> Reported distinctly on purpose: the badge is already in someone's hand, so
> "created" without saying "and it will be refused" would mislead the desk.

### E2E-WIM-023 — a foreign-key badge is not recognised
```gherkin
Given WalkInMode is armed with AcceptOfflineBadges
And a badge encrypted with a key the server does not hold
When it is scanned at any gate
Then the response is HTTP 200 with outcome "Denied"
And the denial reason is "QrUnknown"
```
> The same answer any unrecognised code gets, so a scan is never an oracle for
> which keys are loaded.

### E2E-WIM-024 — the desk reconciles to zero
```gherkin
Given a desk that registered 50 visitors with the network unplugged
When the network is restored and the operator presses F5 and pastes a token
Then the upload reports 50 submitted and 0 rejected
And the desk's "waiting to upload" counter reads 0
And the operation log carries one Admin.OfflineBadgeBatchUploaded row
    naming the same tallies
```

---

## Scenarios — offline scanning (D-821)

A scanner caches its rules from `GET /app/gates/offline-config` while online,
then judges badges on-device when the link drops. **Its verdict is advisory** —
every scan is still queued and the server re-decides it on upload.

### E2E-WIM-025 — offline config withholds the key while disarmed
```gherkin
Given WalkInMode is disarmed
And a gate operator holding Gates.Operate
When they GET /app/gates/offline-config
Then the response is HTTP 200
And badgeKey is null
And the device therefore cannot verify anything offline
```
> The key travels only while the capability is armed, which is what makes
> disarming the lever if a device goes missing.

### E2E-WIM-026 — a scanner admits a badge offline
```gherkin
Given WalkInMode is armed with AcceptOfflineBadges
And the scanner has cached its offline config for gate "G-MAIN"
And "G-MAIN" admits profile-type code 1
When the network is unplugged
And the operator scans a badge encoding (code 1, sequence 3000042)
Then the console shows an ALLOWED verdict
And it shows the badge id "W00003000042"
And the scan is queued for upload
When the network is restored
Then the queued scan uploads and the server records the authoritative verdict
```

### E2E-WIM-027 — a scanner refuses a disallowed type offline
```gherkin
Given the scanner has cached rules for a gate that admits only codes 1 and 2
When it scans a badge encoding profile-type code 7 with no network
Then the console shows DENIED with reason "profile type not allowed"
And the holder's badge id is still shown, so the operator knows who was refused
```
> The rule the walk-in mode never relaxes, online or offline.

### E2E-WIM-028 — a scanner abstains at an offline hall door
```gherkin
Given a hall-door gate and SessionWalkIn NOT armed
When a badge is scanned there with no network
Then the console shows NO verdict — queued, decision pending
And it does NOT show a denial
```
> A booking needs live data the device does not have. Denying on that would turn
> every offline hall door into a wall; the server decides on upload. With
> SessionWalkIn armed the same scan is admitted immediately.

---

## Scenarios — correcting a rejected row (D-824)

### E2E-WIM-029 — a mistyped identity document is rejected by name
```gherkin
Given WalkInMode is armed with OfflineUpload and QuickRegister
And a batch of two rows, the second carrying a national ID of the right shape
    but a wrong check digit
When the desk uploads it
Then the response is HTTP 200
And the first row is "Created"
And the second is "Rejected" naming the national ID
And the desk's "waiting to upload" counter still shows that one row
```
> The check digit is what keeps a mistyped id from being accepted as a real one.
> Since D-945 nothing downstream catches the consequence — there is no
> duplicate guard left to fire — so the validator is the whole defence.

### E2E-WIM-030 — F3 corrects a rejected row without reprinting
```gherkin
Given a registration the server rejected for a bad identity number
Then the upload report NAMES that badge number and the reason on screen
When the operator types only the corrected ID and presses F3
Then the dialog offers that rejected number, not the newest pending one
And it shows whose record is about to be overwritten
When they confirm
Then the stored row keeps its ORIGINAL sequence
And no new badge number is consumed
And the mobile number and Arabic name it was not given are unchanged
And no badge is printed, because the NAME did not change
When they press F5
Then the row uploads and "waiting to upload" reaches 0
And scanning the ORIGINAL printed badge at a gate is Allowed
```
> The paper stays valid because the QR encodes only the badge type and the
> sequence, and a correction touches neither. A corrected NAME is the exception:
> the name is printed on the badge too, so the desk prompts for F2 and the same
> number reprints. A row that has already uploaded is refused — the account
> exists by then and the Control Panel owns it.

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
- The gate scan endpoints carry **no rate limit** (D-819), so a burst of
  scenarios will not trip a 429. If one appears, the `operational` policy has
  been mis-applied.
- Denials are **HTTP 200 with a Denied outcome**, not an error envelope. Assert
  on the outcome and denial reason, never on the status code alone.

## The Control Panel page (D-947)

### Coverage matrix

| Id | Scenario | Category | Priority | Status |
|----|----------|----------|----------|--------|
| E2E-WIM-031 | An admin turns quick register on from the CP, with no deploy | crud | P0 | _to author_ |
| E2E-WIM-032 | An admin turns auto-approve off mid-event | crud | P0 | _to author_ |
| E2E-WIM-033 | The page refuses to lie: a toggle is inert while disarmed | validation | P1 | _to author_ |
| E2E-WIM-034 | A View-only holder sees the modes and cannot change them | auth | P0 | _to author_ |
| E2E-WIM-035 | Turning a mode on is audited as its own event | audit | P1 | _to author_ |


`/admin/walk-in-mode`. Gated by `WalkInMode.View`; the Save button by
`WalkInMode.Manage`. Its own permission pair rather than `Configuration.*`,
because auto-approve relaxes an approval gate and granting somebody the run of
the configuration page should not hand them that switch by accident.

### E2E-WIM-031 — an admin turns quick register on, with no deploy

```gherkin
Scenario: the toggle overrides deployment configuration immediately
  Given WalkInMode:Enabled is true and WalkInMode:QuickRegister is false
  And an Administrator is on /admin/walk-in-mode
  When they tick "Quick register" and Save
  Then the response is 200 and the page reports quick register ON
  And a walk-in submitted with only a name and one identity document succeeds
  And no application restart or deploy happened in between
```

### E2E-WIM-032 — an admin turns auto-approve off mid-event

```gherkin
Scenario: the override wins in the OFF direction too
  Given WalkInMode:AutoApprove is true in configuration
  And an Administrator is on /admin/walk-in-mode
  When they untick "No approval needed" and Save
  Then a subsequent on-site visitor lands PendingApproval with no QR
  # Both directions matter: a service that quietly kept reading options would
  # pass the ON case and fail this one.
```

### E2E-WIM-033 — the page refuses to lie about an inert toggle

```gherkin
Scenario: disarmed, the toggles show their value and say they do nothing
  Given WalkInMode:Enabled is false
  And an override sets quick register ON
  When an Administrator opens /admin/walk-in-mode
  Then the page shows quick register as ON
  And a warning states that walk-in mode is switched off for this deployment
  And a walk-in with the reduced field set is still REFUSED
  # The master switch is not admin-editable. Showing the toggle as OFF would
  # misreport what they set; hiding the warning would misreport its effect.
```

### E2E-WIM-034 — a View-only holder cannot change the modes

```gherkin
Scenario: read and write are separate grants
  Given an admin holds WalkInMode.View but not WalkInMode.Manage
  When they open /admin/walk-in-mode
  Then the current modes are visible
  And the Save button is not rendered
  And POST /api/v1/admin/walk-in-mode answers 403 if called directly
```

### E2E-WIM-035 — the change is audited as its own event

```gherkin
Scenario: a SOC reader can find the moment a mode was switched on
  Given an Administrator changes either mode on /admin/walk-in-mode
  When the operation log is read
  Then one Admin.WalkInModeChanged entry carries BOTH values
  # One line for the pair, because the pair is what the operator changed and a
  # reader correlating a burst of desk approvals needs them together.
```
