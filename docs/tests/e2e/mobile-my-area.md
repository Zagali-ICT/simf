# E2E test catalogue — `My Area` (`myArea`)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7). Mobile
> catalogue — the App API endpoints are built (D-249); the API implementation
> lives in `tests/SIMF.Api.Tests/MyAreaDashboardTests.cs`.
> The **Flutter screen is built** (D-297) and was **rebuilt to KSA Wave-2
> frame 512:1780 "منطقتي"** (D-378 batch): identity card (avatar + tier ·
> enrolled line + gold #qrId + bordered مشاركة button), the 2×3 tile grid —
> wired **العربية • English** language toggle, **disabled المظهر theme tile**
> (no light theme yet, owner decision), a single **مشاركة جهة اتصال** share
> pill → the share-my-contact QR screen (**#21** — re-pointed from the old
> native `.vcf` share sheet; the duplicate **مشاركة ملفي** pill was dropped),
> the two API
> stat tiles — then
> جدولي اليوم rows and المزيد rows (smart badge, settings + the
> function-preserving calendar-export and sign-out rows). Widget tests in
> `src/Mobile/simf_app/test/features/myarea/my_area_screen_test.dart` (approved
> card+tiles+stats+schedule, disabled theme tile, share-contact nav (#21,
> single share pill; مشاركة ملفي dropped), empty
> schedule, pending→limited card with no dashboard call, 403→limited,
> error→retry→refetch, session-row→detail, RTL); the dashboard parser is
> covered in `src/Mobile/simf_app/test/features/myarea/myarea_models_test.dart`.
> The old mockup screen + test are parked in `_legacy_mockup/`.
> **Exact-parity to the LIVE frame 758:1283 (D-447):** جدولي اليوم now splits
> into a **جلسات** group and a **مقابلات** group, each under its gold
> sub-header; the share row is now a single مشاركة جهة اتصال pill (**#21** — the
> duplicate مشاركة ملفي pill was dropped); the stat label is **مقابلات** (was
> "مقابلات مؤكدة"). The
> owner extra (the Face-ID toggle) and the shell header chrome are kept (beyond
> the frame, per the owner invariants). **D-654:** the "تحديث صورة الهوية"
> (Update ID photo) row was removed from the المزيد list (owner).

| | |
|--|--|
| **Page** | [`Page_014`](../../App/Page_014/README.md) (App page docs) |
| **Route** | `GET /api/v1/app/account/dashboard` · `…/calendar.ics` · `…/contact-card.vcf` · app screen #14 `/my-area` |
| **Surface** | Mobile (Flutter) + App API |
| **Test runner** | xUnit + `WebApplicationFactory` (API) · Flutter widget/integration test (screen) |
| **Auth setup** | An **Approved** visitor token (sign-up → verify-email → `SetAccountState(Approved)` → sign-in); App-DB rows seeded directly. **No literal secrets.** |
| **Last reviewed** | 2026-07-22 (forward-chevron LTR direction — E2E-MOB014-017, المزيد caret points to the inline end via SimfForwardChevron; #21 — share-contact re-pointed to the QR screen; prev D-447 exact-parity to live frame 758:1283) |

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
| E2E-MOB014-010 | ~~مشاركة ملفي opens the share-my-contact QR screen~~ — **🗑️ REMOVED (#21, owner):** the مشاركة ملفي pill was a duplicate of مشاركة جهة اتصال (same QR-screen nav) and was dropped; see E2E-MOB014-016 | happy | — | removed ✓ (screen asserts مشاركة ملفي is absent) |
| E2E-MOB014-016 | **مشاركة جهة اتصال opens the share-my-contact QR screen (#21):** the single share-contact pill (and the identity-card مشاركة button) navigate to `RouteNames.shareMyContact`; the old native `.vcf` OS share sheet and the duplicate مشاركة ملفي pill are both gone | happy | P2 | authored ✓ (screen — tapping "Share contact" routes to the QR screen; مشاركة ملفي absent) |
| E2E-MOB014-017 | **Header back chevron → Home (bug fix):** on the Profile tab of the bottom-nav shell the header back chevron returns to the **Home** tab. Previously it was a dead no-op — an in-shell tab never leaves the shell's `/` location, so `backOrHome`'s `goNamed(home)` navigated to `/` while already there | nav | P1 | authored ✓ (`simf_page_shell_test` — `backOrHome on an in-shell tab (nothing to pop) switches the shell to the Home tab`) |
| E2E-MOB014-011 | ~~Photos-only profile edit (D-437): "Update ID photo" row~~ — **🗑️ REMOVED (D-654, owner):** the "تحديث صورة الهوية" row is gone from My Area; the face photo (avatar) is still changed via the tap-the-avatar flow (`_changeAvatar`) | happy | — | removed ✓ (screen asserts the row is absent) |
| E2E-MOB014-012 | **Face-ID toggle (D-445):** the المزيد section shows an enable/disable **"Face ID sign-in"** switch that **self-hides when the device has no usable biometric**; turning it on enrols a device key (+ success toast); turning it **off first asks to confirm the permanent delete** ("…permanently deleted from this device") and only revokes after the user taps **Delete** (Cancel keeps the key). Mirrored in the side menu. | happy | P1 | authored ✓ (widget — `FaceIdToggleTile` hidden-when-unavailable / on→enrol+flip / off→confirm→revoke+flip / cancel→keep) |
| E2E-MOB014-013 | **جدولي اليوم grouping (758:1283, D-447):** the schedule splits into a "جلسات" group then a "مقابلات" group, each under its gold sub-header; both empty → the no-items placeholder | i18n/visual | P1 | authored ✓ (screen — groups + RTL `dy` order: sessions above meetings) |
| E2E-MOB014-014 | **Single share pill (#21 — was the 758:1305 two-pill order):** only the مشاركة جهة اتصال pill is rendered; the duplicate مشاركة ملفي pill was dropped (owner) | i18n | P2 | authored ✓ (screen — مشاركة جهة اتصال present, مشاركة ملفي absent) |
| E2E-MOB014-015 | **Saved stat tiles → Coming soon (owner 2026-06-21):** the الإحصائيات tiles **مقابلات** and **جلسات محفوظة** still show their live counts but are now tappable; each opens the **ComingSoon** placeholder (saved meetings / saved sessions are not built yet) | happy | P2 | authored ✓ (widget — `KsaStatTile` fires `onTap`) |
| E2E-MOB014-017 | The المزيد rows' forward "open" caret points to the inline end — right in LTR (English), left in RTL (Arabic) — via the shared SimfForwardChevron | i18n | P2 | authored ✓ (`test/app/widgets/simf_forward_chevron_test.dart`) |
| E2E-MOB014-018 | **True guest gets guest copy + a way in (BUG-013):** a visitor with NO account reaching the Profile tab sees "sign in or create an account to see your profile and schedule" and working Sign in / Create account actions — never the "under review" copy | auth | P1 | authored ✓ (screen `BUG-013 — a TRUE guest gets the guest copy and a working sign-in CTA, never the under-review copy`) |

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

### E2E-MOB014-014 — Single share pill (#21 — was the 758:1305 two-pill order)

```gherkin
Scenario: Only the مشاركة جهة اتصال pill is rendered
  Given an approved visitor on /my-area in Arabic
  Then the "مشاركة جهة اتصال" share pill is shown
  And the "مشاركة ملفي" pill is no longer present (dropped as a duplicate, #21)
```

### E2E-MOB014-016 — مشاركة جهة اتصال opens the share-my-contact QR screen (#21)

```gherkin
Scenario: The share-contact pill opens the in-app contact-QR screen
  Given an approved visitor on /my-area
  When they tap the "Share contact" / "مشاركة جهة اتصال" pill
  Then the app navigates to the share-my-contact QR screen (RouteNames.shareMyContact)
  And no native OS .vcf share sheet is invoked
```

**Note (#21):** the share-contact pill and the identity-card مشاركة button used
to fetch `…/contact-card.vcf` and open the native OS share sheet; the owner
re-pointed both to the in-app "شارك جهة اتصالي" QR screen. Because the sibling
مشاركة ملفي pill opened the *same* screen, it was a duplicate and the owner
dropped it — the share row is now a single مشاركة جهة اتصال pill.

**Evidence:** `my_area_screen_test.dart` — `the share-contact tile opens the
contact-QR screen (#21)` (tapping "Share contact" routes to the stubbed
`SHARE-MY-CONTACT` screen) and `only مشاركة جهة اتصال remains, مشاركة ملفي
dropped (#21)`.

### E2E-MOB014-012 — Face-ID toggle (D-445)

```gherkin
Scenario: An approved visitor enables/disables Face-ID sign-in from My Area
  Given an approved visitor on /my-area on a device with a usable biometric
  Then the المزيد section shows a "Face ID sign-in" / "الدخول ببصمة الوجه" switch, off
  When they turn it on
  Then a device key is enrolled and a success toast "Face ID sign-in enabled" is shown
  When they turn it off
  Then a confirm dialog warns the data "will be permanently deleted from this device"
  And tapping Delete revokes the device key and the switch returns to off
  And tapping Cancel keeps the device key and the switch stays on
  And on a device with NO usable biometric the switch is not rendered at all
```

**Evidence:** `biometric_auth_test.dart` — `FaceIdToggleTile` hidden-when-unavailable / toggle-on enrols + flips on / toggle-off **confirm→revoke+flip** / **cancel→keep** (green). The OS biometric prompt itself is the owner's on-device test.

### E2E-MOB014-015 — Saved stat tiles are display-only (D-653 / D-609 / B18)

```gherkin
Scenario: The الإحصائيات stat tiles show counts and are not tappable
  Given an approved visitor on /my-area
  Then the مقابلات and جلسات محفوظة tiles show their live counts
  And neither tile carries an onTap — there is no drill-down to open
```

**Evidence:** `my_area_dashboard_body.dart` builds both `SimfStatTile`s with a
`value` + `label` and **no** `onTap` (D-653, owner: display-only). The drill-down
list screens were retired by D-609; **B18 (2026-07-27)** then deleted the last
dangling sentinel routes, `savedMeetings` (206) and `bilateralMeetings` (204),
which had no screen and no caller left. `savedSessions` (205) went with D-609.

### E2E-MOB014-011 — Update ID photo — 🗑️ REMOVED (D-654, owner)

```gherkin
Scenario: The "Update ID photo" row is no longer on My Area
  Given an approved visitor on /my-area
  Then the المزيد section does NOT show an "Update ID photo" / "تحديث صورة الهوية" row
  And the face photo (avatar) is still changed by tapping the identity-card avatar (existing flow)
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

### E2E-MOB014-017 — Header back chevron returns to Home

```gherkin
Scenario: The back chevron on the Profile tab returns to Home
  Given the user is on the Profile tab of the bottom-nav shell
  When they tap the back chevron in the header
  Then the shell switches to the Home tab
```

> The five bottom-nav tabs render inside `SimfAppShell`'s IndexedStack at the
> shell's `/` location, so `context.canPop()` is false and the old
> `goNamed(home)` was a no-op (navigating to `/` while already at `/`). The
> shared `backOrHome` now switches the shell tab to Home when it can't pop.

**Evidence:** `simf_page_shell_test.dart` — `backOrHome on an in-shell tab
(nothing to pop) switches the shell to the Home tab`.

### E2E-MOB014-018 — A true guest is not shown "your account is under review"

```gherkin
Scenario: A visitor with no account at all opens the Profile tab
  Given I have never signed in (no account)
  When I tap the Profile tab in the bottom nav
  Then I do NOT see "Your account is under review"
  And I see "Sign in or create an account to see your profile and schedule."
  And a "Sign in" button and a "Create account" link are offered
  When I tap "Sign in"
  Then the sign-in screen opens
  And GET /app/account/dashboard is never called
```

> The bottom nav switches tabs **inside** `SimfAppShell`'s IndexedStack, so the
> router's auth redirect never runs and a signed-out visitor really lands here.
> The limited card previously described an account "under review" that was never
> submitted, with no way out (BUG-013). The pending copy is unchanged for a
> genuinely pending/rejected signed-in account.

**Evidence:** screen test `BUG-013 — a TRUE guest gets the guest copy and a
working sign-in CTA, never the under-review copy`.

---

_Last reviewed:_ `2026-07-26` by `SIMF Team` — **bug fix: the true-guest state on
the Profile tab (E2E-MOB014-018, BUG-013).** _Prior:_ `2026-07-24` — bug fix: the header back chevron
on the in-shell Profile tab was a dead no-op; the shared `backOrHome` now switches
the shell to the Home tab when there is nothing to pop (E2E-MOB014-017);
`2026-06-19` by `SIMF Team` (D-447).
