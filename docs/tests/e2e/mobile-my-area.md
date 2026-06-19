# E2E test catalogue — `My Area` (`myArea`)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7). Mobile
> catalogue — the App API endpoints are built (D-249); the API implementation
> lives in `tests/SIMF.Api.Tests/MyAreaDashboardTests.cs`.
> The **Flutter screen is built** (D-297) and was **rebuilt to KSA Wave-2
> frame 512:1780 "منطقتي"** (D-378 batch): identity card (avatar + tier ·
> enrolled line + gold #qrId + bordered مشاركة button), the 2×3 tile grid —
> wired **العربية • English** language toggle, **disabled المظهر theme tile**
> (no light theme yet, owner decision), **مشاركة ملفي** → the share-my-contact
> QR screen, **مشاركة جهة اتصال** (.vcf), the two API stat tiles — then
> جدولي اليوم rows and المزيد rows (smart badge, settings + the
> function-preserving calendar-export and sign-out rows). Widget tests in
> `src/Mobile/simf_app/test/features/myarea/my_area_screen_test.dart` (approved
> card+tiles+stats+schedule, disabled theme tile, share-my-profile nav, empty
> schedule, pending→limited card with no dashboard call, 403→limited,
> error→retry→refetch, session-row→detail, RTL); the dashboard parser is
> covered in `src/Mobile/simf_app/test/features/myarea/myarea_models_test.dart`.
> The old mockup screen + test are parked in `_legacy_mockup/`.
> **Exact-parity to the LIVE frame 758:1283 (D-447):** جدولي اليوم now splits
> into a **جلسات** group and a **مقابلات** group, each under its gold
> sub-header; the two share pills were re-ordered to مشاركة جهة اتصال (right) ·
> مشاركة ملفي (left); the stat label is **مقابلات** (was "مقابلات مؤكدة"). The
> owner extras (تحديث صورة الهوية, the Face-ID toggle) and the shell header
> chrome are kept (beyond the frame, per the owner invariants).

| | |
|--|--|
| **Page** | [`Page_014`](../../App/Page_014/README.md) (App page docs) |
| **Route** | `GET /api/v1/app/account/dashboard` · `…/calendar.ics` · `…/contact-card.vcf` · app screen #14 `/my-area` |
| **Surface** | Mobile (Flutter) + App API |
| **Test runner** | xUnit + `WebApplicationFactory` (API) · Flutter widget/integration test (screen) |
| **Auth setup** | An **Approved** visitor token (sign-up → verify-email → `SetAccountState(Approved)` → sign-in); App-DB rows seeded directly. **No literal secrets.** |
| **Last reviewed** | 2026-06-19 (D-447 — exact-parity to live frame 758:1283) |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB014-001 | Dashboard: identity card + counters (1 booked, 2 meetings) + today's 3 schedule items | happy | P0 | authored ✓ (`Dashboard_returns_identity_counters_and_todays_schedule`) |
| E2E-MOB014-002 | Empty dashboard: 0 counters, empty schedule | happy | P1 | authored ✓ (`Dashboard_is_empty_for_a_visitor_with_no_bookings_or_meetings`) |
| E2E-MOB014-003 | Counter 2 unions speaker meetings (Accepted) + business meetings (Confirmed) | happy | P0 | authored ✓ (covered by 001) |
| E2E-MOB014-004 | `calendar.ics` returns RFC 5545 with one VEVENT per item | happy | P1 | authored ✓ (`Calendar_ics_returns_an_RFC5545_document_with_an_event_per_item`) |
| E2E-MOB014-005 | `contact-card.vcf` returns a vCard with name + job + org + QR id | happy | P1 | authored ✓ (`Contact_card_vcf_returns_a_vCard_with_the_name_and_qr_id`) |
| E2E-MOB014-006 | No token → 401 | auth | P0 | authored ✓ (`Dashboard_without_a_token_returns_401`) |
| E2E-MOB014-007 | Not-yet-approved account → 403 (RequireApprovedAccount) | auth | P0 | authored ✓ (`Dashboard_for_a_not_yet_approved_account_is_forbidden`) |
| E2E-MOB014-008 | RTL render of card + counters + Arabic tier/hall labels | i18n | P1 | authored ✓ (screen — Arabic RTL + pending/403 limited card + session-row nav) |
| E2E-MOB014-009 | KSA layout: language tile toggles AR/EN; theme tile visible but disabled | happy | P1 | authored ✓ (screen — disabled palette + no tap) |
| E2E-MOB014-010 | مشاركة ملفي opens the share-my-contact QR screen | happy | P2 | authored ✓ (screen) |
| E2E-MOB014-011 | **Photos-only profile edit (D-437):** the المزيد section shows an **"Update ID photo"** row that re-uploads the ID document from the gallery (`POST …/user-profile/id-image`), with a success / failure toast; the **face photo (avatar)** is changed via the existing tap-the-avatar flow. Names stay set-at-sign-up (not editable here) | happy | P1 | authored ✓ (screen — the row renders; gallery upload is a platform channel, driven live) |
| E2E-MOB014-012 | **Face-ID toggle (D-445):** the المزيد section shows an enable/disable **"Face ID sign-in"** switch that **self-hides when the device has no usable biometric**; turning it on enrols a device key (+ success toast), off revokes it (+ toast). Mirrored in the side menu. | happy | P1 | authored ✓ (widget — `FaceIdToggleTile` hidden-when-unavailable / on→enrol+flip / off→revoke+flip) |
| E2E-MOB014-013 | **جدولي اليوم grouping (758:1283, D-447):** the schedule splits into a "جلسات" group then a "مقابلات" group, each under its gold sub-header; both empty → the no-items placeholder | i18n/visual | P1 | authored ✓ (screen — groups + RTL `dy` order: sessions above meetings) |
| E2E-MOB014-014 | **Share pills order (758:1305, D-447):** مشاركة جهة اتصال at the inline-start (right), مشاركة ملفي at the end (left) | i18n | P2 | authored ✓ (screen — RTL `getCenter().dx`) |

## Scenarios

### E2E-MOB014-013 — جدولي اليوم splits into جلسات + مقابلات (758:1283, D-447)

```gherkin
Scenario: Today's schedule groups sessions and meetings
  Given an approved visitor whose today's schedule has a session and a meeting
  When the My-Area screen renders جدولي اليوم
  Then a gold "جلسات" sub-header sits above the session rows
  And a gold "مقابلات" sub-header sits above the meeting rows
  And the جلسات group renders above the مقابلات group
  And when both groups are empty the "No items today" placeholder is shown instead
```

### E2E-MOB014-014 — Share pills order (758:1305, D-447)

```gherkin
Scenario: The two share pills follow the frame order under RTL
  Given an approved visitor on /my-area in Arabic
  Then "مشاركة جهة اتصال" is at the inline-start (physical right)
  And "مشاركة ملفي" is at the end (physical left)
```

### E2E-MOB014-012 — Face-ID toggle (D-445)

```gherkin
Scenario: An approved visitor enables/disables Face-ID sign-in from My Area
  Given an approved visitor on /my-area on a device with a usable biometric
  Then the المزيد section shows a "Face ID sign-in" / "الدخول ببصمة الوجه" switch, off
  When they turn it on
  Then a device key is enrolled and a success toast "Face ID sign-in enabled" is shown
  When they turn it off
  Then the device key is revoked and the switch returns to off
  And on a device with NO usable biometric the switch is not rendered at all
```

**Evidence:** `biometric_auth_test.dart` — `FaceIdToggleTile` hidden-when-unavailable / toggle-on enrols + flips on / toggle-off revokes + flips off (green). The OS biometric prompt itself is the owner's on-device test.

### E2E-MOB014-011 — Update ID photo (photos-only edit, D-437)

```gherkin
Scenario: An approved visitor re-uploads the ID document from My Area
  Given an approved visitor on /my-area
  Then the المزيد section shows an "Update ID photo" / "تحديث صورة الهوية" row
  When they tap it
  Then the gallery opens and a picked image is uploaded to POST /app/account/user-profile/id-image
  And a success toast "ID photo updated" (or a failure toast) is shown
  And the face photo is changed separately by tapping the identity-card avatar (existing flow)
```


### E2E-MOB014-001 — Dashboard golden path

```gherkin
Feature: My-Area dashboard
  As an approved visitor
  I want my identity card, my counters and today's schedule in one call
  So that I can glance at my day

Background:
  Given an approved visitor is signed in
  And they hold one approved seat booking in a session scheduled today
  And they have one accepted speaker-meeting request
  And they are a confirmed participant in one business meeting today

Scenario: The dashboard aggregates identity, counters and today's schedule
  When the app calls GET /api/v1/app/account/dashboard
  Then the response is 200 with success = true
  And identity.fullNameAr and identity.fullNameEn are the profile names
  And identity.qrId is the profile QR id
  And identity.tierNameEn and identity.pageColor come from the ProfileType
  And counters.bookedSessionsCount = 1
  And counters.meetingsCount = 2
  And todaySchedule has 3 items, time-ordered
  And exactly one item has kind = "Session" and two have kind = "Meeting"
```

**Evidence:** `MyAreaDashboardTests.Dashboard_returns_identity_counters_and_todays_schedule` (green).

### E2E-MOB014-002 — Empty dashboard

```gherkin
Scenario: A visitor with nothing booked sees zeros
  Given an approved visitor with no bookings and no meetings
  When the app calls GET /api/v1/app/account/dashboard
  Then counters.bookedSessionsCount = 0
  And counters.meetingsCount = 0
  And todaySchedule is empty
```

**Evidence:** `MyAreaDashboardTests.Dashboard_is_empty_for_a_visitor_with_no_bookings_or_meetings` (green).

### E2E-MOB014-003 — Meeting counter unions both kinds

```gherkin
Scenario: Counter 2 = accepted speaker meetings ∪ confirmed business meetings
  Given the visitor has one accepted speaker meeting and one confirmed business meeting
  When the app calls GET /api/v1/app/account/dashboard
  Then counters.meetingsCount = 2
  And the business-meeting schedule item carries its own start/end time (no parent session)
```

### E2E-MOB014-004 — calendar.ics

```gherkin
Scenario: The calendar export is a valid RFC 5545 document
  Given the visitor has one booked session + one speaker meeting + one business meeting
  When the app calls GET /api/v1/app/account/calendar.ics
  Then the response is 200 with Content-Type text/calendar
  And the body begins with BEGIN:VCALENDAR and ends with END:VCALENDAR
  And it contains exactly 3 VEVENT blocks
  And the booked session appears as SUMMARY:Keynote
```

**Evidence:** `MyAreaDashboardTests.Calendar_ics_returns_an_RFC5545_document_with_an_event_per_item` (green).

### E2E-MOB014-005 — contact-card.vcf

```gherkin
Scenario: The contact card is a valid vCard with the badge QR id
  Given the visitor's profile has a name, job title, organisation and QR id
  When the app calls GET /api/v1/app/account/contact-card.vcf
  Then the response is 200 with Content-Type text/vcard
  And the body contains FN, TITLE, ORG and the QR id as UID/NOTE
```

**Evidence:** `MyAreaDashboardTests.Contact_card_vcf_returns_a_vCard_with_the_name_and_qr_id` (green).

### E2E-MOB014-006 — Auth gate (no token)

```gherkin
Scenario: No token is rejected
  Given no bearer token is supplied
  When the app calls GET /api/v1/app/account/dashboard
  Then the response is 401 Unauthorized
```

**Evidence:** `MyAreaDashboardTests.Dashboard_without_a_token_returns_401` (green).

### E2E-MOB014-007 — Approval gate

```gherkin
Scenario: A not-yet-approved account is forbidden
  Given a verified but not-yet-approved visitor holds a valid token
  When the app calls GET /api/v1/app/account/dashboard
  Then the response is 403 Forbidden
  And the app falls back to the limited card from cached identity (Page_014 L-5)
```

**Evidence:** `MyAreaDashboardTests.Dashboard_for_a_not_yet_approved_account_is_forbidden` (green).

### E2E-MOB014-008 — RTL render

```gherkin
Scenario: Arabic card renders right-to-left
  Given the device locale is Arabic
  When the My-Area screen renders the card, counters and schedule
  Then the layout is right-to-left
  And the tier name, hall name and times use the Arabic paired fields
  And the badge QR is hidden when qrId is null (not yet approved)
```

---

_Last reviewed:_ `2026-06-19` by `SIMF Team` (D-447).
