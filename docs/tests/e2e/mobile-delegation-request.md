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
> `attendeeCount`, `subject`, and (when a slot was picked) `slotStart` + `slotEnd`.
> A `slotStart` without a `slotEnd` is rejected 400 by the API.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-DELREQ-001 | Golden: open from a tapped delegation card (target fixed) → attendees + subject + a slot → Send → "تم إرسال طلب المقابلة" | happy | P0 | _to author_ (submit path: `DelegationMeetingRequestsTests`, API) |
| E2E-DELREQ-002 | Golden: open from the Bi-Meeting page "طلب اجتماع وفد" → pick a delegation → submit | happy | P0 | _to author_ |
| E2E-DELREQ-003 | Attendee-count validation — empty / non-numeric / < 1 → "أدخل عدد حضور صحيحاً"; digits-only, max 4 | error | P1 | _to author_ |
| E2E-DELREQ-004 | Subject required — empty → "يرجى إدخال الاسم والموضوع" | error | P1 | _to author_ |
| E2E-DELREQ-005 | Slot picker — day then time; **G3 (owner 2026-07-30, supersedes D-767 R1):** no free slot = "لا توجد فترات متاحة حالياً" **and a disabled Send** (API 409 `DELEGATION_MEETING_NO_AVAILABILITY`); a failed slot fetch shows a load error + Retry instead | happy | P0 | authored ✓ (slots read: `DelegationAvailabilityTests`; G3: `MeetingNoAvailabilityTests` + `delegation_meeting_request_sheet_test`) |
| E2E-DELREQ-006 | Picker path — no delegation chosen → "اختر الوفد أولاً"; search + "لا نتائج مطابقة"; "لا توجد وفود متاحة" when none | error | P1 | _to author_ |
| E2E-DELREQ-007 | Not entitled — 403 → "غير مصرَّح لك بطلب اجتماعات الوفود" | auth | P0 | _to author_ (`A_non_delegate_submit_is_403`, API) |
| E2E-DELREQ-008 | Target not invited — 400 → "هذا الوفد غير متاح للاجتماعات" | error | P1 | _to author_ (`A_target_country_that_is_not_invited_is_400`, API) |
| E2E-DELREQ-009 | Duplicate — a second pending request for the same target → conflict toast | error | P1 | _to author_ (`A_second_pending_request_for_the_same_target_is_rejected`, API) |
| E2E-DELREQ-010 | RTL render (Arabic) — sheet title "طلب اجتماع وفد", fields + slots mirror | i18n | P1 | _to author_ |
| E2E-DELREQ-011 | A35 — a server-rejected submit shows the **server's own bilingual reason**, never the speaker copy "this speaker is not accepting meeting requests" | error | P1 | authored ✓ (`delegation_meeting_request_sheet_test.dart`, widget) |
| E2E-DELREQ-012 | A35 — an offline / never-reached-the-server failure still falls back to the local "تعذّر إرسال الطلب" copy | edge | P2 | authored ✓ (`delegation_meeting_request_sheet_test.dart`, widget) |
| E2E-DELREQ-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-DELREQ-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

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
      attendeeCount 5, subject "تعاون بحري", slotStart + slotEnd
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

### E2E-DELREQ-005 — Slot picker (day → time; no availability; load failure)

> **Superseded 2026-07-30 (G3, owner).** The "topic-only request is a valid submit" line below was
> the D-767 R1 behaviour. A request against a delegation with **no free slot** is now refused: the
> Send button is disabled and the API answers **409 `DELEGATION_MEETING_NO_AVAILABILITY`**. A slot is
> mandatory on every send.

```gherkin
Scenario: Choosing a slot
  Given the delegation sheet is open with a target chosen
  Then before a day is chosen the time picker prompts "الرجاء اختيار التاريخ أولاً" / "Please choose a date first"
  When I choose a day that has slots
  Then the "اختر الوقت" (Choose the time) chips list the delegation's free slots
  When I try to send without picking a time
  Then the sheet shows "الرجاء اختيار التاريخ والوقت" / "Please choose a date and time"

Scenario: A delegation with no availability (G3)
  Given the target delegation has no free slot — no active window, or every slot already taken
  Then "لا توجد فترات متاحة حالياً" / "No meeting slots available right now" is shown
  And the "إرسال الطلب" / "Send request" button is DISABLED (dimmed) — nothing is submitted
  And posting the same request directly to the API returns 409 "DELEGATION_MEETING_NO_AVAILABILITY"

Scenario: The slot fetch FAILS (G3)
  Given the available-slots call for the target fails (network / 500)
  Then "تعذر تحميل القائمة." / "Could not load the list." is shown with a Retry action
  And the "no meeting slots available" notice is NOT shown — a network blip is never presented
    as the delegation having no availability
  When the call recovers and I tap Retry
  Then the day cards and time chips appear and Send becomes enabled
```

**Evidence:** the slots read + the live-meeting exclusion are covered by
`DelegationAvailabilityTests` (API); the G3 refusal by `MeetingNoAvailabilityTests`
(`Delegation_with_no_availability_windows_is_409_no_availability`,
`Delegation_whose_only_window_is_fully_taken_is_409_no_availability`) and by
`delegation_meeting_request_sheet_test` (disabled send + slot-fetch-error retry).
`A_slot_start_without_an_end_is_400` guards a malformed submit that carries a start
with no end.

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
  `submitMeetingRequest({targetCountryCode, attendeeCount, subject, slotStart?, slotEnd?})`
  → `POST /app/delegation-meeting-requests`.
- **Error → message mapping** (`delegation_meeting_request_sheet.dart` `_failureText`),
  **A35**: when the call reached the server (`httpStatus != null`) and the envelope
  carries a message, that message is shown as-is — it is already localised by
  `ApiFailure.fromEnvelope`. Only a failure that never reached the server falls back to
  the local copy (403 → `delegationNotAllowed`, 400 → `delegationTargetNotInvited`,
  otherwise `meetingRequestFailed`). The old map hard-coded one client string per
  status, so a 409 surfaced the SPEAKER copy `meetingRequestNotAllowed` ("this speaker
  is not accepting meeting requests") on a delegation sheet, and every distinct 400
  (subject length, attendee count, invalid slot, own delegation) read as "this
  delegation is not available for meetings".
- **API integration tests** — [`tests/SIMF.Api.Tests/DelegationMeetingRequestsTests.cs`](../../../tests/SIMF.Api.Tests/DelegationMeetingRequestsTests.cs)
  (submit + guards + accept/confirm) and
  [`tests/SIMF.Api.Tests/DelegationAvailabilityTests.cs`](../../../tests/SIMF.Api.Tests/DelegationAvailabilityTests.cs)
  (the slots read this sheet consumes).
- **Gate** — `currentUserMeetingAccessProvider.delegation` (profile `allowsDelegationMeeting`);
  a `flutter analyze` clean run + `delegations_screen_test.dart` (card tappability gating) +
  on-device smoke cover the UI layer.
- **Golden / widget + on-device** scenarios (`_to author_`) are the sheet open → fill →
  send flow, the validation copy, and the RTL render — driven on device / in `flutter test`.

### E2E-DELREQ-011/012 — The sheet shows the server's own reason (A35)

```gherkin
Scenario: The server rejects the submit with a specific reason
  Given I am an entitled delegate with the sheet open on a fixed target
  When I enter a subject and an attendee count and tap "إرسال الطلب"
  And the API answers 409 DELEGATION_MEETING_REQUEST_INVALID
      "A delegation cannot request a meeting with itself." /
      "لا يمكن للوفد طلب اجتماع مع نفسه."
  Then the sheet shows that exact message inline
  And it does NOT show "This speaker is not accepting meeting requests"

Scenario: A 400 names the field that failed
  When the API answers 400 "Subject must be between 1 and 1000 characters."
  Then the sheet shows that message, not "This delegation is not available for meetings"

Scenario: The request never reached the server
  When the submit fails with no HTTP status (network down / timeout)
  Then the sheet shows the local "تعذّر إرسال الطلب. حاول مرة أخرى." copy
```

---

_Last reviewed:_ 2026-07-22 by Claude — bi-meeting rework: new delegation meeting request sheet (flag-gated; opened from the Bi-Meeting page picker and from a tapped delegation card). Backend submit + slots covered by `DelegationMeetingRequestsTests` + `DelegationAvailabilityTests`; the sheet/golden/RTL layer is on-device `_to author_`.

_Last reviewed:_ 2026-07-26 by Claude — A35: the sheet surfaces the server's bilingual message instead of a hard-coded speaker string (E2E-DELREQ-011/012).
