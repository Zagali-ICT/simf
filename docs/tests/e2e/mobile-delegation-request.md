# E2E test catalogue — `طلب اجتماع وفد / Delegation meeting request` (bottom sheet)

> **Authority:** SIMF E2E test catalogue (D-133 / D-245). Bi-meeting rework
> (2026-07-22). The **delegation meeting request sheet**
> (`delegation_meeting_request_sheet.dart`) is the app-side twin of the speaker
> `meeting_request_sheet` — a modal bottom sheet reached two ways:
> (1) the **Bi-Meeting page** [`mobile-meetings.md`](mobile-meetings.md) "طلب اجتماع وفد"
> button (target country picked in-sheet), and (2) tapping a **delegation card** on
> [`mobile-delegations.md`](mobile-delegations.md) (target country fixed to the tapped
> delegation). It reads the delegation's real availability slots and submits a
> country-to-country meeting request.

| | |
|--|--|
| **Page** | [`mobile/delegations/`](../../pages/mobile/delegations/README.md) (sheet: `delegation_meeting_request_sheet.dart`) |
| **Opened from** | `/meetings` (طلب اجتماع وفد) · `/delegations` (tap a country card) |
| **APIs** | `GET /app/countries/{countryId}/available-slots` (slots) · `POST /app/delegation-meeting-requests` (submit) — both `RequireApprovedAccount` |
| **Surface** | Mobile (Flutter) |
| **Auth setup** | An approved app account whose profile has **`allowsDelegationMeeting = true`** (the CP sets it on the account — see [`cp-admin-delegation-availability.md`](cp-admin-delegation-availability.md) sibling flow). A non-entitled account for the gate case. TOTP via `Get-Totp` — never a literal secret. |
| **Last reviewed** | 2026-07-22 (bi-meeting rework — new sheet) |

> **Gating (bi-meeting rework).** The "طلب اجتماع وفد" button and the delegation-card
> tap target render only when `currentUserMeetingAccessProvider.delegation` is `true`
> (from the profile flag `allowsDelegationMeeting`, decoded
> `json['allowsDelegationMeeting'] as bool? ?? false`). This **replaces** the old
> `IsDelegate` gating. A guest / non-entitled user sees the plain (non-tappable)
> delegation cards and no delegation button.

> **Submit body.** `POST /app/delegation-meeting-requests` carries `targetCountryCode`,
> `attendeeCount`, `subject`, and (when a slot was picked) `slotStartUtc` + `slotEndUtc`.
> A `slotStartUtc` without a `slotEndUtc` is rejected 400 by the API.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-DELREQ-001 | Golden: open from a tapped delegation card (target fixed) → attendees + subject + a slot → Send → "تم إرسال طلب المقابلة" | happy | P0 | _to author_ (submit path: `DelegationMeetingRequestsTests`, API) |
| E2E-DELREQ-002 | Golden: open from the Bi-Meeting page "طلب اجتماع وفد" → pick a delegation → submit | happy | P0 | _to author_ |
| E2E-DELREQ-003 | Attendee-count validation — empty / non-numeric / < 1 → "أدخل عدد حضور صحيحاً"; digits-only, max 4 | error | P1 | _to author_ |
| E2E-DELREQ-004 | Subject required — empty → "يرجى إدخال الاسم والموضوع" | error | P1 | _to author_ |
| E2E-DELREQ-005 | Slot picker — day then time; "لا توجد فترات متاحة حالياً" when empty; a topic-only request (no slot) is allowed | happy | P1 | _to author_ (slots read: `DelegationAvailabilityTests`, API) |
| E2E-DELREQ-006 | Picker path — no delegation chosen → "اختر الوفد أولاً"; search + "لا نتائج مطابقة"; "لا توجد وفود متاحة" when none | error | P1 | _to author_ |
| E2E-DELREQ-007 | Not entitled — 403 → "غير مصرَّح لك بطلب اجتماعات الوفود" | auth | P0 | _to author_ (`A_non_delegate_submit_is_403`, API) |
| E2E-DELREQ-008 | Target not invited — 400 → "هذا الوفد غير متاح للاجتماعات" | error | P1 | _to author_ (`A_target_country_that_is_not_invited_is_400`, API) |
| E2E-DELREQ-009 | Duplicate — a second pending request for the same target → conflict toast | error | P1 | _to author_ (`A_second_pending_request_for_the_same_target_is_rejected`, API) |
| E2E-DELREQ-010 | RTL render (Arabic) — sheet title "طلب اجتماع وفد", fields + slots mirror | i18n | P1 | _to author_ |

## Scenarios

### E2E-DELREQ-001 — Golden path from a tapped delegation card

```gherkin
Feature: Delegation meeting request (from a country card)
  As an approved attendee whose account may request delegation meetings
  I want to ask an invited delegation for a country-to-country meeting
  So that our delegations can meet at the forum

Background:
  Given I am signed in and my profile has allowsDelegationMeeting = true
  And the invited delegation "France" / "فرنسا" has availability slots today

Scenario: Request a meeting with a tapped delegation
  Given I am on /delegations
  When I tap the "France" delegation card
  Then the "طلب اجتماع وفد" sheet opens with France as the fixed target (no picker)
  When I enter 5 into "عدد الحضور" (Number of attendees)
  And I enter "تعاون بحري" into "الموضوع" (Subject)
  And I choose a day and an available time slot
  And I tap "إرسال الطلب"
  Then GET /app/countries/{France}/available-slots was read to populate the slots
  And POST /app/delegation-meeting-requests fires with targetCountryCode "FR",
      attendeeCount 5, subject "تعاون بحري", slotStartUtc + slotEndUtc
  And the API returns 200 and the request is Pending
  And the sheet closes with the snackbar "تم إرسال طلب المقابلة" / "Meeting request sent"
```

**Evidence:** submit + Pending-with-target covered by
`DelegationMeetingRequestsTests.A_delegate_submits_and_the_request_is_pending_with_the_target`
(API, green). The sheet UI itself (open → fill → Send) is on-device / widget `_to author_`.

### E2E-DELREQ-002 — Golden path from the Bi-Meeting page

```gherkin
Scenario: Request a delegation meeting from /meetings
  Given I am on /meetings and my account may request delegation meetings
  When I tap "طلب اجتماع وفد" (Request a delegation meeting)
  Then the sheet opens with a delegation picker (label "اختر الوفد" / "Select the delegation")
  When I select "France", enter attendees + subject + a slot, and tap "إرسال الطلب"
  Then POST /app/delegation-meeting-requests fires and the request is Pending
```

### E2E-DELREQ-003 — Attendee-count validation

```gherkin
Scenario: The attendee count must be a positive number
  Given the delegation sheet is open with a target chosen
  When I leave "عدد الحضور" empty (or enter a non-numeric / a 0)
  And I tap "إرسال الطلب"
  Then no POST fires and the field shows "أدخل عدد حضور صحيحاً" / "Enter a valid number of attendees"
  # The field is digits-only (max 4 digits); its hint is "مثال: 5" / "e.g. 5".
```

### E2E-DELREQ-004 — Subject required

```gherkin
Scenario: The subject cannot be empty
  Given the delegation sheet is open with a target + attendee count
  When I leave "الموضوع" empty and tap "إرسال الطلب"
  Then no POST fires and the subject field shows
      "يرجى إدخال الاسم والموضوع" / "Please enter your name and a subject"
  # The subject accepts up to 1000 characters.
```

### E2E-DELREQ-005 — Slot picker (day → time; empty; topic-only)

```gherkin
Scenario: Choosing a slot
  Given the delegation sheet is open with a target chosen
  Then before a day is chosen the time picker prompts "الرجاء اختيار التاريخ أولاً" / "Please choose a date first"
  When I choose a day that has slots
  Then the "اختر الوقت" (Choose the time) chips list the delegation's free slots
  When slots exist and I try to send without picking a time
  Then the sheet shows "الرجاء اختيار التاريخ والوقت" / "Please choose a date and time"

Scenario: A day with no availability
  Given the chosen day has no free slots
  Then "لا توجد فترات متاحة حالياً" / "No meeting slots available right now" is shown
  # A topic-only request (attendees + subject, no slot) is a valid submit (slotStartUtc/EndUtc omitted).
```

**Evidence:** the slots read + the live-meeting exclusion are covered by
`DelegationAvailabilityTests` (API). `A_slot_start_without_an_end_is_400` guards a
malformed submit that carries a start with no end.

### E2E-DELREQ-006 — Picker path guards

```gherkin
Scenario: The picker requires a delegation before sending
  Given the sheet was opened from /meetings (no fixed target)
  When I tap "إرسال الطلب" without choosing a delegation
  Then the snackbar "اختر الوفد أولاً" / "Select a delegation first" is shown and no POST fires
  When I search the picker for a delegation that does not exist
  Then "لا نتائج مطابقة" / "No matching speakers" replaces the list
  When there are no invited delegations at all
  Then the picker shows "لا توجد وفود متاحة" / "No delegations available"
```

### E2E-DELREQ-007/008/009 — Server guards → toasts

```gherkin
Scenario: Not entitled (403)
  Given a submit is attempted by an account whose profile lacks allowsDelegationMeeting
  When POST /app/delegation-meeting-requests returns 403
  Then the sheet shows "غير مصرَّح لك بطلب اجتماعات الوفود" /
      "You are not permitted to request delegation meetings"

Scenario: Target not an invited delegation (400)
  When POST returns 400 (target country not invited)
  Then the sheet shows "هذا الوفد غير متاح للاجتماعات" / "This delegation is not available for meetings"

Scenario: Duplicate pending request (409)
  Given I already have a Pending request for the same target
  When POST returns 409
  Then the sheet shows "هذا المتحدث لا يستقبل طلبات المقابلة" /
      "This speaker is not accepting meeting requests"
  # (Shared 409 copy with the speaker sheet; the row is a duplicate for the same target.)
  When any other error occurs
  Then the default toast "تعذّر إرسال الطلب. حاول مرة أخرى." / "Could not send the request. Try again." is shown
```

**Evidence:** `DelegationMeetingRequestsTests.A_non_delegate_submit_is_403`,
`A_target_country_that_is_not_invited_is_400`,
`A_second_pending_request_for_the_same_target_is_rejected` (all green, API).

### E2E-DELREQ-010 — RTL render

```gherkin
Scenario: The sheet mirrors under Arabic
  Given the app language is Arabic
  When I open the delegation meeting request sheet
  Then the title reads "طلب اجتماع وفد"
  And the attendee-count ("عدد الحضور"), subject ("الموضوع"), day/time labels and the send
      button ("إرسال الطلب") mirror right-to-left
  And there is no horizontal overflow
```

---

## Implementation notes

- **Repository** — `delegations_repository.dart`:
  `getAvailableSlots(int countryId)` → `GET /app/countries/{countryId}/available-slots`;
  `submitMeetingRequest({targetCountryCode, attendeeCount, subject, slotStartUtc?, slotEndUtc?})`
  → `POST /app/delegation-meeting-requests`.
- **Error → message mapping** (`delegation_meeting_request_sheet.dart` `_failureText`):
  403 → `delegationNotAllowed`; 400 → `delegationTargetNotInvited`;
  409 → `meetingRequestNotAllowed`; default → `meetingRequestFailed`.
- **API integration tests** — [`tests/SIMF.Api.Tests/DelegationMeetingRequestsTests.cs`](../../../tests/SIMF.Api.Tests/DelegationMeetingRequestsTests.cs)
  (submit + guards + accept/confirm) and
  [`tests/SIMF.Api.Tests/DelegationAvailabilityTests.cs`](../../../tests/SIMF.Api.Tests/DelegationAvailabilityTests.cs)
  (the slots read this sheet consumes).
- **Gate** — `currentUserMeetingAccessProvider.delegation` (profile `allowsDelegationMeeting`);
  a `flutter analyze` clean run + `delegations_screen_test.dart` (card tappability gating) +
  on-device smoke cover the UI layer.
- **Golden / widget + on-device** scenarios (`_to author_`) are the sheet open → fill →
  send flow, the validation copy, and the RTL render — driven on device / in `flutter test`.

---

_Last reviewed:_ 2026-07-22 by Claude — bi-meeting rework: new delegation meeting request sheet (flag-gated; opened from the Bi-Meeting page picker and from a tapped delegation card). Backend submit + slots covered by `DelegationMeetingRequestsTests` + `DelegationAvailabilityTests`; the sheet/golden/RTL layer is on-device `_to author_`.
