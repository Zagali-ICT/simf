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
> all covered by `tests/SIMF.Api.Tests/DelegationMeetingRequestsTests.cs`.

> **⚑ Bi-meeting rework update (2026-07-22).** The delegation flow is brought up to full
> parity with the speaker flow and both desks now share the **unified state machine + the
> three-button modal**:
> - **Requester eligibility** moved to the per-user **`AllowsDelegationMeeting`** flag
>   (admin-assigned on the account; **replaces** the former `IsDelegate` submit gate) —
>   grounded in `DelegationMeetingRequestService.SubmitAsync` (403 `FORBIDDEN` without it).
>   `Country.IsInvited` still validates the **target** delegation.
> - **Availability + hall-bind**: an invited delegation now has **availability windows**
>   ([`cp-admin-delegation-availability.md`](cp-admin-delegation-availability.md)); the respond
>   modal binds a **hall + free slot (+ optional meeting table)** via the shared
>   `Admin.Meetings.Bind.*` pickers (hall-hosts-meetings + free-slot + both-country overlap
>   guards), the same as the speaker desk.
> - **Three-button modal**: **Close**; the danger button is **Decline**
>   (`Admin.Meetings.Decline` "Decline"/"رفض") when the row is Pending, else **Cancel meeting**
>   (`Admin.Meetings.Cancel` "Cancel meeting"/"إلغاء الاجتماع"); **Approve**
>   (`Admin.Meetings.Approve` "Approve"/"موافقة", **Pending only**); and **Confirm**
>   (`Admin.Meetings.Confirm` "Confirm"/"تأكيد"). Decline/Cancel **require a justification**
>   (`Admin.Meetings.Decline.NoteRequired`). There is **no verbal checkbox** — Approve sends
>   `VerbalConfirmed=false`, Confirm sends `VerbalConfirmed=true`.
> - **Status set** (delegation pills): Pending; **AwaitingConfirmation**
>   (`Admin.Meetings.Status.AwaitingConfirmation` "Awaiting confirmation"/"بانتظار التأكيد" —
>   the shared `AwaitingSpeaker=4` value); **Accepted** = the *Confirmed* terminal (there is no
>   separate "Confirmed" pill); **Done** (`Admin.Meetings.Status.Done` "Done"/"منتهٍ");
>   Rejected; Cancelled (`Admin.Meetings.Status.Cancelled` "Cancelled"/"ملغى").
> - **Other-party confirm**: on Approve, each eligible **target-delegation member** is
>   notified in-app (`MeetingRequested`) + emailed and confirms from the app —
>   [`mobile-meeting-confirm.md`](mobile-meeting-confirm.md),
>   `POST /app/delegation-meeting-requests/{id}/confirm`.
> - **Operator Check-in → Done**: a Confirmed (Accepted) row exposes a **Check in**
>   (`Admin.Meetings.CheckIn` "Check in"/"تسجيل الحضور") row action that flips it to `Done`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-DLM-001 | A delegate of an invited country submits "10 attendees meet country EG" → the request lists as Pending on the desk | happy | P0 | authored ✓ (DelegationMeetingRequestsTests, API) |
| E2E-DLM-002 | Respond → Accept with a note → row flips to Accepted; the requester is notified + emailed | happy | P0 | authored ✓ (DelegationMeetingRequestsTests, API) |
| E2E-DLM-003 | Respond → Reject → row flips to Rejected; the requester is notified (no email) | happy | P1 | _to author_ |
| E2E-DLM-004 | A non-delegate submit → 403; a non-invited target country → 400 (`DELEGATION` / `DELEGATE_COUNTRY_NOT_INVITED`) | error | P0 | authored ✓ (DelegationMeetingRequestsTests, API) |
| E2E-DLM-005 | Auth gate — admin lacking `DelegationMeetings.View` → `/not-permitted`; nav item hidden; lacking `.Manage` → Respond action hidden | auth | P0 | _to author_ (gate verified by CpNavigationPermissionTests) |
| E2E-DLM-006 | RTL / Arabic render — grid + respond modal mirror | i18n | P1 | _to author_ |
| E2E-DLM-010 | Unified 3-button modal — Close / Decline (Pending) or Cancel (non-terminal) / Approve (Pending) / Confirm; justification required; no verbal checkbox (bi-meeting rework) | happy | P0 | authored ✓ (`Admin_confirm_of_an_awaiting_request_books_it_without_a_hall`, API) |
| E2E-DLM-011 | Operator Check-in — a Confirmed (Accepted) row → Check in → status `Done`; a non-Accepted check-in → 409 (bi-meeting rework) | happy | P0 | authored ✓ (`Checking_in_a_confirmed_delegation_meeting_marks_it_Done` + `Checking_in_a_non_confirmed_delegation_meeting_is_409`, API) |
| E2E-DLM-012 | Other-party confirm — Approve notifies each target-delegation member (`MeetingRequested`) who confirms from the app (cross-ref `mobile-meeting-confirm.md`) | happy | P0 | authored ✓ (`Other_party_confirm_response_does_not_leak_the_requester_email`, API) |
| E2E-DLM-013 | Confirm of an AwaitingConfirmation request books it (even on a past bound slot) → Accepted (bi-meeting rework) | happy | P1 | authored ✓ (`Admin_confirm_of_an_awaiting_request_with_a_PAST_bound_slot_still_succeeds`, API) |

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

### E2E-DLM-010 — Unified 3-button respond modal (bi-meeting rework)

```gherkin
Feature: The delegation respond modal is the unified three-button control
Background:
  Given an Administrator with DelegationMeetings.Manage has signed in
  And a Pending delegation meeting request is on the desk

Scenario: Pending row — Close / Decline / Approve / Confirm
  When they open Respond on the Pending row
  Then the footer shows Close ("إغلاق"), Decline ("رفض"), Approve ("موافقة"), Confirm ("تأكيد")
  And there is NO verbal-confirmation checkbox

Scenario: Approve binds a hall + slot and awaits the other party's confirmation
  When they pick a hall + a free slot (+ optional table) and click Approve
  Then PUT .../respond fires with Status=Accepted, VerbalConfirmed=false, HallId + SlotStart/End
  And the row moves to AwaitingConfirmation (pill "Awaiting confirmation" / "بانتظار التأكيد")
  And each eligible target-delegation member is notified (MeetingRequested) + emailed to confirm
  When they click Approve / Confirm without a hall + slot
  Then the CP shows "Select a hall and a free slot to approve or confirm." /
      "اختر قاعة وفترة متاحة للموافقة أو التأكيد." and does not submit

Scenario: Confirm books the meeting (verbal), Cancel/Decline needs a justification
  When they click Confirm on an AwaitingConfirmation row
  Then PUT .../respond fires with Status=Accepted, VerbalConfirmed=true and the row becomes Accepted
  When instead they click Cancel meeting ("إلغاء الاجتماع") on a non-terminal row with the note empty
  Then the CP blocks submit with "A justification is required to decline or cancel." /
      "يلزم إدخال مبرّر للرفض أو الإلغاء."
  # On a Pending row the danger button reads Decline ("رفض"); on a non-terminal row it reads
  # Cancel meeting ("إلغاء الاجتماع"). Both release any held hall slot.
```

**Evidence:** `DelegationMeetingRequestsTests.Admin_confirm_of_an_awaiting_request_books_it_without_a_hall` (green — the Confirm path books an AwaitingConfirmation request without needing a hall bind).

### E2E-DLM-011 — Operator Check-in → Done (bi-meeting rework)

```gherkin
Scenario: Check a confirmed delegation meeting in → Done
  Given a delegation meeting request is Confirmed (Accepted)
  And the Accepted row shows the "Check in" (log-in icon) row action ("تسجيل الحضور");
      no non-Accepted row shows it
  When the operator clicks Check in
  Then POST /account/api/admin/delegation-meeting-requests/{id}/check-in returns 200
  And the row status becomes Done (pill "Done" / "منتهٍ")
  And a green toast reads "Meeting checked in." / "تم تسجيل حضور الاجتماع."

Scenario: Checking in a non-Confirmed meeting is rejected
  Given a Pending / AwaitingConfirmation / Rejected request
  When POST .../{id}/check-in is issued
  Then the API returns 409 APP_REQUEST_ALREADY_RESPONDED
    ("Only a confirmed meeting can be checked in." / "لا يمكن تسجيل الحضور إلا لاجتماع مؤكَّد.")
  # Gated DelegationMeetings.Manage; no ?requesterQr= param exists.
```

**Evidence:** `DelegationMeetingRequestsTests.Checking_in_a_confirmed_delegation_meeting_marks_it_Done`
(submit → Approve → check-in: `Accepted → Done`, stamps `CheckedInAt`/`CheckedInByUserId`) and
`Checking_in_a_non_confirmed_delegation_meeting_is_409` (a Pending row → 409
`APP_REQUEST_ALREADY_RESPONDED`) — both green — grounded in
`DelegationMeetingRequestService.CheckInAsync`.

### E2E-DLM-012/013 — Other-party confirm + Confirm books a past-slot request

```gherkin
Scenario: The other party confirms from the app
  Given an admin approved a request and it is AwaitingConfirmation
  When an eligible target-delegation member confirms via
      POST /app/delegation-meeting-requests/{id}/confirm (mobile-meeting-confirm.md)
  Then the row becomes Accepted (Confirmed) and the requester is notified
  And the confirm response never carries the requester's email (PII strip, a908f22c)

Scenario: Admin Confirm of an AwaitingConfirmation request with a past bound slot still books it
  Given an AwaitingConfirmation request whose bound slot is already in the past
  When the admin clicks Confirm
  Then PUT .../respond returns 200 and the row becomes Accepted (a past slot does not block Confirm)
```

**Evidence:** `DelegationMeetingRequestsTests.Other_party_confirm_response_does_not_leak_the_requester_email`,
`Admin_confirm_of_an_awaiting_request_with_a_PAST_bound_slot_still_succeeds` (both green).

---

_Last reviewed:_ 2026-07-22 by Claude — bi-meeting rework: requester gate moved to `AllowsDelegationMeeting`; availability windows + hall-bind; unified 3-button modal (Close/Decline-or-Cancel/Approve/Confirm, no verbal checkbox); AwaitingConfirmation + Done statuses; other-party app-tap confirm; operator Check-in (E2E-DLM-010/011/012/013). Prior: on-site W2b (M-3 accept-slot validation; E2E-DLM-007/008/009, 2026-07-11); D-478 (#11) delegation↔delegation meeting desk (Group G phase 2, 2026-06-20).
