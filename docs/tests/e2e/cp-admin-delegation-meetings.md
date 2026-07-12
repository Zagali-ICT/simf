# E2E test catalogue — Delegation meetings (`/admin/delegation-meetings`)

| | |
|--|--|
| **Route** | `/admin/delegation-meetings` |
| **Surface** | Control Panel |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-20 (D-478 #11, Group G phase 2) |

> **What this page does (grounded in `DelegationMeetingsList.razor`, D-478).**
> The team's review desk for **delegation↔delegation (G2G)** meeting requests: a
> **delegate** (`UserProfile.IsDelegate`, D-473) of an **invited** country asks to meet
> another invited country's delegation — "count X (attendees) meets country Y" + a
> subject + a proposed slot. The page is a `SimfDataGrid` (server-paged, status filter)
> with columns From / To / Attendees / Subject / Status / Submitted; the **Respond**
> action (quiet reply button, **Pending rows only**) opens a modal showing the
> from/to delegations, attendee count, the requester's email (fetched on open), and
> the subject, with an Accept/Reject decision + an optional note. On **Accept** the
> requester is notified in-app **and emailed** (the requester is a SimfUser). Gated by
> `DelegationMeetings.View` (page + nav); the Respond action by `DelegationMeetings.Manage`.
> API: `POST /admin/delegation-meeting-requests/list`, `GET …/{id}`,
> `PUT …/{id}/respond`; the public submit is `POST /app/delegation-meeting-requests` —
> all covered by `tests/SIMF.Api.Tests/DelegationMeetingRequestsTests.cs` (5/5).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-DLM-001 | A delegate of an invited country submits "10 attendees meet country EG" → the request lists as Pending on the desk | happy | P0 | authored ✓ (DelegationMeetingRequestsTests, API) |
| E2E-DLM-002 | Respond → Accept with a note → row flips to Accepted; the requester is notified + emailed | happy | P0 | authored ✓ (DelegationMeetingRequestsTests, API) |
| E2E-DLM-003 | Respond → Reject → row flips to Rejected; the requester is notified (no email) | happy | P1 | _to author_ |
| E2E-DLM-004 | A non-delegate submit → 403; a non-invited target country → 400 (`DELEGATION` / `DELEGATE_COUNTRY_NOT_INVITED`) | error | P0 | authored ✓ (DelegationMeetingRequestsTests, API) |
| E2E-DLM-005 | Auth gate — admin lacking `DelegationMeetings.View` → `/not-permitted`; nav item hidden; lacking `.Manage` → Respond action hidden | auth | P0 | _to author_ (gate verified by CpNavigationPermissionTests) |
| E2E-DLM-006 | RTL / Arabic render — grid + respond modal mirror | i18n | P1 | _to author_ |

## Scenarios

### E2E-DLM-001/002 — Submit, then accept

```gherkin
Feature: Delegation meeting requests review desk
Background:
  Given a delegate of the invited country "SA" has signed in to the app
  And an Administrator has signed in to the Control Panel

Scenario: A delegate submits a G2G meeting request
  When the delegate POSTs /api/v1/app/delegation-meeting-requests
       with TargetCountryCode "EG", AttendeeCount 10, Subject "Naval cooperation talks"
  Then the response is 200 and the request status is Pending
  And on /admin/delegation-meetings the row shows From "SA", To "EG", Attendees 10, Status Pending

Scenario: The team accepts the request
  When the Administrator opens Respond on the Pending row
  Then GET /account/api/admin/delegation-meeting-requests/{id} returns the requester email
  When they choose Accept, add a note, and Send response
  Then PUT /account/api/admin/delegation-meeting-requests/{id}/respond returns 200
  And the row status becomes Accepted
  And the requesting delegate receives an in-app notification and an email
```

### E2E-DLM-004 — Submit guards

```gherkin
Scenario: A non-delegate cannot submit
  Given a normal (non-delegate) visitor is signed in to the app
  When they POST /api/v1/app/delegation-meeting-requests
  Then the response is 403

Scenario: The target country must be an invited delegation
  Given a delegate of the invited country "SA" is signed in to the app
  When they POST /api/v1/app/delegation-meeting-requests with TargetCountryCode "US" (not invited)
  Then the response is 400 with error code DELEGATE_COUNTRY_NOT_INVITED
```

### E2E-DLM-007 — Accept a request whose proposed slot is free + future (M-3)

```gherkin
Scenario: The Accepted row + its slot becomes the reservation
  Given an Administrator opens Respond on a Pending SA -> EG request carrying a
    future slot (e.g. 2030-02-01 09:00-10:00)
  When they choose Accept and Send response
  Then PUT /account/api/admin/delegation-meeting-requests/{id}/respond returns 200
  And the row status becomes Accepted and the slot is persisted
```

### E2E-DLM-008 — Accept with a slot in the past is rejected (M-3)

```gherkin
Scenario: A past proposed slot cannot be reserved
  Given a Pending request whose proposed slot is in the past (submit only checks end>start)
  When the Administrator responds Accept
  Then the API returns 400 DELEGATION_MEETING_REQUEST_INVALID
    (bilingual toast: "The proposed meeting slot is in the past." /
    "فترة الاجتماع المقترحة في الماضي.")
```

### E2E-DLM-009 — Accept clashes with an existing delegation meeting (M-3)

```gherkin
Scenario: Neither delegation may be double-booked
  Given SA -> EG (09:00-10:00) is already Accepted
  When the Administrator accepts SA -> US (09:30-10:30) — the SA delegation overlaps
  Then the API returns 409 DELEGATION_MEETING_REQUEST_INVALID
    ("One of the delegations already has a meeting at that time.")
  # Overlap is keyed on either delegation (requesting or target) with a half-open
  # [start,end) window; a topic-only accept (no slot) is unaffected.
```

**Evidence:** `DelegationMeetingRequestsTests.Accepting_a_request_with_a_free_future_slot_succeeds`, `Accepting_a_request_with_a_slot_in_the_past_is_400`, `Accepting_an_overlapping_slot_for_the_same_delegation_is_409` (all green).

---

_Last reviewed:_ 2026-07-11 by Claude — on-site W2b (M-3 accept-slot validation: not-in-past + no delegation double-book; added E2E-DLM-007/008/009). Prior: 2026-06-20 by SIMF Team — D-478 (#11) delegation↔delegation meeting desk (Group G phase 2, batch complete).
