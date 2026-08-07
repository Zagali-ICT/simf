# E2E test catalogue — `تأكيد الاجتماع / Confirm meeting` (`/meeting-confirm`)

> **Authority:** SIMF E2E test catalogue (D-133 / D-245). Bi-meeting rework
> (2026-07-22). The **other-party confirm-on-tap** screen
> (`meeting_confirm_screen.dart`, route **#117** `RouteNames.meetingConfirm`,
> `/meeting-confirm?requestId=…`). When an admin **approves** a delegation meeting
> request it moves to *AwaitingConfirmation*; each eligible member of the **target**
> delegation gets a `MeetingRequested` in-app notification (and an email) whose deep
> link opens this screen. Tapping **Confirm** flips the meeting to *Confirmed* over
> the wire. This is the **app** confirm path for the **delegation** side; the
> **speaker** side confirms over an emailed token page —
> [`web-meeting-confirm.md`](web-meeting-confirm.md).

| | |
|--|--|
| **Page** | [`mobile/meeting-confirm.md`](../../pages/mobile/meeting-confirm.md) |
| **Route** | `/meeting-confirm?requestId=…` (route #117; `RouteNames.meetingConfirm`) |
| **Reached from** | a `MeetingRequested` notification tap ([`mobile-notifications.md`](mobile-notifications.md)) |
| **APIs** | `POST /app/delegation-meeting-requests/{id}/confirm` (`RequireApprovedAccount`, rate-limited) |
| **Surface** | Mobile (Flutter) |
| **Auth setup** | An approved app account that is a **member of the target delegation** (profile `NationalityId == TargetCountryId`) **and** has `allowsDelegationMeeting = true`. TOTP via `Get-Totp` — never a literal secret. |
| **Last reviewed** | 2026-07-22 (bi-meeting rework — new screen) |

> **Confirm transition + guards (grounded in `DelegationMeetingRequestService.ConfirmByOtherPartyAsync`).**
> The endpoint (1) 404s `DELEGATION_MEETING_REQUEST_NOT_FOUND` for an unknown id;
> (2) pre-checks the row is `AwaitingSpeaker` (the shared *AwaitingConfirmation* value),
> else 409 `APP_REQUEST_ALREADY_RESPONDED`; (3) authorises the caller — their profile
> `NationalityId` must equal the request's `TargetCountryId` **and** `AllowsDelegationMeeting`
> must be `true`, else 403 `FORBIDDEN`; (4) flips the status with a conditional
> `UPDATE … WHERE Status == AwaitingSpeaker` to `Accepted` and stamps
> `ConfirmedAt` / `ConfirmedByUserId` (the race loser also gets 409). The confirm
> response **strips the requester email** (`RequesterEmail = null`, fixed in `a908f22c`)
> so a peer app user can never read another user's private login email over the app wire.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOBMC-001 | Golden: tap the MeetingRequested notification → confirm screen → Confirm → "تم تأكيد الاجتماع"; the meeting is Confirmed | happy | P0 | _to author_ (transition + PII: `DelegationMeetingRequestsTests`, API) |
| E2E-MOBMC-002 | Not awaiting (409) — an already-confirmed / decided meeting → "هذا الاجتماع ليس بانتظار التأكيد" | error | P0 | _to author_ (`Responding_to_an_already_decided_request_is_409`, API) |
| E2E-MOBMC-003 | Not eligible (403) — caller is not a target-delegation member → "غير مصرَّح لك بطلب اجتماعات الوفود" | auth | P0 | _to author_ |
| E2E-MOBMC-004 | Missing / blank requestId → "لم يتم العثور على الاجتماع"; no POST fires | edge | P1 | _to author_ |
| E2E-MOBMC-005 | The confirm response carries no requester email (PII strip) | security | P0 | authored ✓ (`Other_party_confirm_response_does_not_leak_the_requester_email`, API) |
| E2E-MOBMC-006 | Deep-link allow-list — only `/meeting-confirm` (with `?requestId=`) is a permitted notification click path | security | P1 | _to author_ |
| E2E-MOBMC-007 | RTL render (Arabic) — title "تأكيد اجتماع الوفد", intro + both buttons mirror | i18n | P1 | _to author_ |
| E2E-MOBMC-008 | Golden decline (B8): tap **رفض الاجتماع** → the meeting is Rejected, the hall slot is released, the requester is notified + emailed | happy | P0 | authored ✓ (`meeting_confirm_screen_test.dart`, widget; `B8_a_target_member_can_decline_an_awaiting_meeting`, API) |
| E2E-MOBMC-009 | Decline is refused for a non-member (403) / a non-awaiting meeting (409) | auth-gate / conflict | P0 | authored ✓ (`B8_a_member_of_another_delegation_cannot_decline`, `B8_declining_a_pending_meeting_is_a_conflict`, `B8_declining_twice_is_a_conflict`, API) |
| E2E-MOBMC-010 | A30 — the screen names the DELEGATION meeting, so it is not confused with the website's speaker token page | i18n | P2 | authored ✓ (`meeting_confirm_screen_test.dart`, widget) |
| E2E-MOBMC-011 | D2 — one member's decline retracts the prompt from the OTHER eligible members of the same delegation; the decliner is skipped | data | P0 | authored ✓ (`DelegationMeetingQaFixesTests.D2_*`, API) |
| E2E-MOBMC-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOBMC-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-MOBMC-001 — Golden path (notification → confirm)

```gherkin
Feature: Confirm a delegation meeting from a notification
  As a member of the target delegation
  I want to confirm the meeting an admin approved
  So that the requesting delegation knows it is on

Background:
  Given I am signed in, my profile NationalityId is the target country, and allowsDelegationMeeting = true
  And a delegation meeting request targeting my delegation is AwaitingConfirmation
  And I received a "MeetingRequested" notification (group "Meetings") with a /meeting-confirm?requestId=… link

Scenario: Tap the notification and confirm
  When I open the notification
  Then it navigates to /meeting-confirm?requestId={id}
  And the screen title reads "تأكيد الاجتماع" / "Confirm meeting"
  And the intro reads "اضغط لتأكيد هذا الاجتماع مع الطرف الآخر." /
      "Tap to confirm this meeting with the other party."
  When I tap "تأكيد الاجتماع" (Confirm meeting)
  Then POST /app/delegation-meeting-requests/{id}/confirm returns 200
  And the success view shows "تم تأكيد الاجتماع" / "Meeting confirmed"
  And the request status is now Confirmed (Accepted), with ConfirmedAt / ConfirmedByUserId stamped
```

**Evidence:** the AwaitingConfirmation → Confirmed transition over the confirm endpoint is
exercised by `DelegationMeetingRequestsTests.Other_party_confirm_response_does_not_leak_the_requester_email`
(API, green — it calls the confirm endpoint and asserts the stripped email). The screen
open → Confirm → success view is on-device `_to author_`.

### E2E-MOBMC-002 — Not awaiting (409)

```gherkin
Scenario: Confirming a meeting that already left AwaitingConfirmation
  Given the request is already Confirmed (or Cancelled / Rejected / Done)
  When I open /meeting-confirm?requestId={id} and tap Confirm
  Then POST .../confirm returns 409 APP_REQUEST_ALREADY_RESPONDED
  And the screen shows "هذا الاجتماع ليس بانتظار التأكيد" / "This meeting is not awaiting confirmation"
  # Two members racing the same request: the first wins (200), the second gets this 409.
```

**Evidence:** `DelegationMeetingRequestsTests.Responding_to_an_already_decided_request_is_409` (API, green).

### E2E-MOBMC-003 — Not eligible (403)

```gherkin
Scenario: A caller who is not a target-delegation member cannot confirm
  Given the request targets delegation X but my profile NationalityId is a different country
      (or allowsDelegationMeeting is false)
  When I tap Confirm
  Then POST .../confirm returns 403 FORBIDDEN
  And the screen shows "غير مصرَّح لك بطلب اجتماعات الوفود" /
      "You are not permitted to request delegation meetings"
  # 403 reuses the delegationNotAllowed copy on this screen.
```

### E2E-MOBMC-004 — Missing / blank requestId

```gherkin
Scenario: Opening the screen with no requestId
  When /meeting-confirm is opened with no ?requestId= (or a blank value)
  Then the screen shows "لم يتم العثور على الاجتماع" / "Meeting not found"
  And no POST /app/delegation-meeting-requests/…/confirm fires
```

### E2E-MOBMC-005 — PII: the confirm response omits the requester email

```gherkin
Scenario: A peer confirmer cannot read the requester's login email
  Given an eligible target-delegation member confirms an AwaitingConfirmation request
  When POST /app/delegation-meeting-requests/{id}/confirm returns 200
  Then the response body's requester email field is null
  # The requester's Identity login email is an admin-desk / detail-only PII field; it is
  # stripped from the app confirm response (a908f22c: `return detail with { RequesterEmail = null };`).
```

**Evidence:** `DelegationMeetingRequestsTests.Other_party_confirm_response_does_not_leak_the_requester_email` (API, green).

### E2E-MOBMC-006 — Deep-link allow-list

```gherkin
Scenario: Only the meeting-confirm deep link is a permitted notification click path
  Given the notifications screen guards click-throughs by _allowedClickPaths
  Then "/meeting-confirm" is an allowed path (added by the bi-meeting rework)
  And a MeetingRequested notification (relatedId = the request id) resolves to
      /meeting-confirm?requestId={relatedId}
  And a MeetingReminder notification carries no navigation (no ClickUrl arm)
  And both MeetingRequested and MeetingReminder group under "Meetings"
```

### E2E-MOBMC-007 — RTL render

```gherkin
Scenario: The confirm screen mirrors under Arabic
  Given the app language is Arabic
  When I open /meeting-confirm?requestId={id}
  Then the title "تأكيد الاجتماع", the intro, and the "تأكيد الاجتماع" button mirror right-to-left
  And on success the "تم تأكيد الاجتماع" view renders RTL with no horizontal overflow
```

### E2E-MOBMC-008/009 — The target delegation declines

```gherkin
Scenario: A member of the target delegation cannot host the meeting
  Given an admin approved a delegation meeting and it is AwaitingConfirmation
  And I am an eligible member of the TARGET delegation
  When I open /meeting-confirm?requestId={id}
  Then I see both "تأكيد الاجتماع" (Confirm the meeting) and "رفض الاجتماع" (Decline)
  When I tap "رفض الاجتماع"
  Then POST /app/delegation-meeting-requests/{id}/decline returns 200
  And the request status is Rejected and its hall + table binding is cleared
  And the requester receives a "Delegation meeting declined" notification and email
  And I see "تم رفض الاجتماع" with the note that the hall slot is released
  And the response never carries the requester's email (same PII strip as confirm)

Scenario: Someone outside the target delegation tries to decline
  Given I am a member of a third delegation
  When I POST /app/delegation-meeting-requests/{id}/decline
  Then the response is 403 FORBIDDEN and the meeting stays AwaitingConfirmation

Scenario: Declining a meeting that is not awaiting confirmation
  When I decline a still-Pending meeting, or decline the same meeting twice
  Then the response is 409 APP_REQUEST_ALREADY_RESPONDED
  And the screen shows "هذا الاجتماع ليس بانتظار التأكيد"
```

### E2E-MOBMC-010 — The screen names the delegation (A30)

```gherkin
Scenario: The app screen and the website page are told apart
  When I open /meeting-confirm?requestId={id}
  Then the title reads "تأكيد اجتماع الوفد" / "Confirm delegation meeting"
  And the website's /meeting/confirm?token= page keeps the generic
      "تأكيد الاجتماع" / "Confirm meeting" — it serves BOTH the speaker links and
      the delegation confirm link (D-767)
  And driving a SPEAKER meeting through this app screen is a 403/409 by design
```

### E2E-MOBMC-011 — A decline retracts the prompt from the rest of the delegation (D2)

```gherkin
Scenario: One member's decline is a decline for the whole delegation
  Given two eligible members of the TARGET delegation each hold the
        "awaiting your confirmation" card + the emailed confirm link
  When one of them taps "رفض الاجتماع" and the decline returns 200
  Then the OTHER member receives a MeetingCancelled notification for the same
       request (bilingual EN + AR), so their prompt is retracted
  And the member who declined receives no extra card — they already have the answer
  And re-tapping the other member's old card would have returned
      409 APP_REQUEST_ALREADY_RESPONDED
```

**Evidence:** `DelegationMeetingQaFixesTests.D2_a_decline_retracts_the_prompt_from_the_other_target_members`.

---

## Implementation notes

- **Screen** — `meeting_confirm_screen.dart`; route in `router.dart`
  (`_Route(number: 117, name: RouteNames.meetingConfirm, path: '/meeting-confirm', …)`),
  builder reads `state.uri.queryParameters['requestId'] ?? ''`.
- **Repository** — `delegations_repository.dart` `confirmMeeting(String requestId)` →
  `POST /app/delegation-meeting-requests/{requestId}/confirm` (empty body) →
  `DelegationMeetingSummary`.
- **Error switch** (`meeting_confirm_screen.dart`): `409 → meetingConfirmNotAwaiting`,
  `403 → delegationNotAllowed`, `_ → meetingConfirmFailed` (or `meetingDeclineFailed`
  on the decline arm); blank id → `meetingConfirmMissing`.
- **B8 decline** — `delegations_repository.dart` `declineMeeting(String requestId)` →
  `POST /app/delegation-meeting-requests/{requestId}/decline`; the screen's buttons
  carry the keys `delegation-meeting-confirm` / `delegation-meeting-decline`.
  Server side `DelegationMeetingRequestService.DeclineByOtherPartyAsync` mirrors the
  confirm guard exactly (`LoadAwaitingForOtherPartyAsync`) and flips
  AwaitingConfirmation → **Rejected**, clearing `HallId` / `MeetingTableId`.
- **Notifications** — `notifications_screen.dart`: `_allowedClickPaths` includes
  `'/meeting-confirm'`; `_groupForItem` maps `MeetingRequested` / `MeetingReminder` → `'Meetings'`.
  `NotificationKindCatalog.ClickUrlFor(MeetingRequested)` → `/meeting-confirm?requestId={relatedId}`;
  `MeetingReminder` has no click-url arm.
- **Backend** — `POST /app/delegation-meeting-requests/{id:guid}/confirm`
  (`RequireApprovedAccount`); guard = caller `NationalityId == TargetCountryId` &&
  `AllowsDelegationMeeting`; conditional `UPDATE … WHERE Status == AwaitingSpeaker` → `Accepted`;
  errors `DELEGATION_MEETING_REQUEST_NOT_FOUND` (404), `APP_REQUEST_ALREADY_RESPONDED` (409),
  `FORBIDDEN` (403); the response strips `RequesterEmail` (a908f22c).
- **Notification kinds** — `NotificationKind.MeetingRequested = 54`,
  `NotificationKind.MeetingReminder = 55` (append-only; persisted by name).

---

_Last reviewed:_ 2026-07-22 by Claude — bi-meeting rework: new delegation other-party confirm-on-tap screen (route 117), reached from the `MeetingRequested` notification deep link. The confirm transition + PII strip are covered by `DelegationMeetingRequestsTests`; the screen / RTL / deep-link-allow-list layer is on-device `_to author_`.

_Last reviewed:_ 2026-07-26 by Claude — B8 target-side decline + A30 delegation-specific copy (E2E-MOBMC-008..010).

_Last reviewed:_ 2026-07-27 by Claude — D2: the decline retracts the confirm prompt from the other target members (E2E-MOBMC-011).
