# E2E test catalogue — `Home` (`home`)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7). Mobile
> catalogue — Home's on-login bundle (`GET /app/bootstrap`) is built (D-251);
> the API implementation lives in `tests/SIMF.Api.Tests/AppBootstrapTests.cs`.
> The **Flutter screen is built** (D-296) and was **rebuilt to the KSA Wave-2
> frames** (guest = 512:1492, signed-in = 203:1236 — W2 batch): widget tests in
> `src/Mobile/simf_app/test/features/home/home_screen_test.dart` (guest banner +
> 2×2 tiles + locked بطاقتي card + sign-in CTA + FAQ→About row, signed-in
> greeting header + live banner + three tile sections + social row + discover
> card, unread badge, RTL); the unread-count provider's gating is covered in
> `src/Mobile/simf_app/test/features/notifications/notifications_repository_test.dart`.
> The old mockup screen + test are parked in `_legacy_mockup/`.

| | |
|--|--|
| **Page** | [`Page_013`](../../App/Page_013/README.md) (App page docs) |
| **Route** | `GET /api/v1/app/bootstrap` · `GET /app/account/notifications/unread-count` · app screen #13 `/` |
| **Surface** | Mobile (Flutter) + App API |
| **Test runner** | xUnit + `WebApplicationFactory` (API) · Flutter widget/integration test (screen) |
| **Auth setup** | A signed-in visitor token (`AuthFlow.SignInVisitorWithoutTwoFactorAsync`); `AuthFlow.SetAccountState` for the approved case. **No literal secrets.** |
| **Last reviewed** | 2026-06-05 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB013-001 | Bootstrap returns user + unread count + server time | happy | P0 | authored ✓ (`Bootstrap_returns_the_current_user_unread_count_and_server_time`) |
| E2E-MOB013-002 | Bootstrap unread count reflects a dispatched notification | happy | P1 | authored ✓ (`Bootstrap_unread_count_reflects_a_dispatched_notification`) |
| E2E-MOB013-003 | Bootstrap reflects an approved account (pending → approved routing) | happy | P0 | authored ✓ (`Bootstrap_reflects_an_approved_account`) |
| E2E-MOB013-004 | No token → 401 | auth | P0 | authored ✓ (`Bootstrap_without_a_token_returns_401`) |
| E2E-MOB013-005 | Guest (no token) renders the KSA guest layout, no bootstrap call | happy | P1 | authored ✓ (screen — guest banner + 2×2 tiles + sign-in CTA, no bell) |
| E2E-MOB013-006 | Privilege from the JWT claim picks the layout | auth | P1 | authored ✓ (screen — signed-in greeting header + tile sections) |
| E2E-MOB013-007 | RTL render of Home tiles + bell badge | i18n | P1 | authored ✓ (screen — Arabic RTL + badge hidden/shown) |
| E2E-MOB013-008 | Locked بطاقتي card is visible but inert as a guest | auth | P1 | authored ✓ (screen — disabled tile ignores taps) |
| E2E-MOB013-009 | FAQ row opens the About page (no app FAQ endpoint yet) | happy | P2 | authored ✓ (screen) |
| E2E-MOB013-010 | Social + Visit-Saudi links launch externally; unset URL = inert button | happy | P2 | authored ✓ (screen — 5 brand buttons render; D-369 contract) |

## Scenarios

### E2E-MOB013-001 — Bootstrap golden path

```gherkin
Feature: On-login bootstrap bundle
  As a signed-in app user
  I want one call that returns who I am, my unread badge and the server clock
  So that the app caches everything it needs on login

Background:
  Given a visitor has signed up, verified their email and signed in

Scenario: Bootstrap returns the cached-on-login bundle
  When the app calls GET /api/v1/app/bootstrap
  Then the response is 200 with success = true
  And user.id, user.email match the signed-in account
  And user.appRole = "Visitor"
  And user.registrationStatus = "Pending"
  And unreadNotificationCount equals the dedicated unread-count endpoint
  And serverTimeUtc is a recent UTC instant
```

**Evidence:** `AppBootstrapTests.Bootstrap_returns_the_current_user_unread_count_and_server_time` (green).

### E2E-MOB013-002 — Unread count reflects notifications

```gherkin
Scenario: A new notification bumps the bootstrap unread count
  Given the visitor's current bootstrap unread count is N
  When a notification is dispatched to the visitor
  And the app calls GET /api/v1/app/bootstrap again
  Then unreadNotificationCount = N + 1
```

**Evidence:** `AppBootstrapTests.Bootstrap_unread_count_reflects_a_dispatched_notification` (green).

### E2E-MOB013-003 — Approved routing

```gherkin
Scenario: An approved account bootstraps as Approved
  Given a signed-in visitor whose account is set to Approved
  When the app calls GET /api/v1/app/bootstrap
  Then user.registrationStatus = "Approved"
  And the app routes into the full experience (not the pending screen)
```

**Evidence:** `AppBootstrapTests.Bootstrap_reflects_an_approved_account` (green).

### E2E-MOB013-004 — Auth gate

```gherkin
Scenario: No token is rejected
  Given no bearer token is supplied
  When the app calls GET /api/v1/app/bootstrap
  Then the response is 401 Unauthorized
```

**Evidence:** `AppBootstrapTests.Bootstrap_without_a_token_returns_401` (green).

### E2E-MOB013-005 — Guest Home (KSA layout, no bootstrap)

```gherkin
Scenario: A guest sees the KSA guest home without bootstrapping
  Given no user is signed in
  When the Home screen opens at "/"
  Then the header reads "الرئيسية • ضيف"
  And the "أنت تتصفح كضيف …" banner highlights "البطاقة الذكية" in gold
  And the 2×2 tiles render: الجلسات، المتحدثون، الخريطة، المعرض
  And the gold "تسجيل الدخول" button opens the sign-in screen
  And no /app/bootstrap call is made and no bell is shown
```

### E2E-MOB013-006 — Privilege picks the layout

```gherkin
Scenario: A signed-in visitor gets the greeting home
  Given a signed-in user whose cached appRole is "Visitor"
  When Home renders
  Then the greeting header shows the avatar, "صباح الخير/مساء الخير", the
       user's name, the bell (with the unread badge) and the menu
  And the red LIVE banner opens the live broadcast
  And the three tile sections render: عن الملتقى · المحاور (المتحدثون،
      الأجنحة، الرعاة) / الأخبار والتغطية (اللقاءات الثنائية، الأرشيف) /
      الميزات الذكية (قابل أشخاص مثلك، المساعد الذكي، ملخص الجلسات،
      بطاقتي الذكية)
```

### E2E-MOB013-007 — RTL render

```gherkin
Scenario: Home renders right-to-left in Arabic
  Given the device locale is Arabic
  When Home renders the tiles and the bell badge
  Then the layout is right-to-left
  And the unread badge is hidden when the count is 0
```

### E2E-MOB013-008 — Locked badge card (guest)

```gherkin
Scenario: The بطاقتي card is locked for a guest
  Given no user is signed in
  When the guest home renders
  Then the full-width "بطاقتي" card renders on the disabled palette
  And tapping it does nothing (no navigation, no dialog)
```

### E2E-MOB013-009 — FAQ row → About

```gherkin
Scenario: The FAQ row opens the about page
  Given the guest home is open
  When the user taps the "الأسئلة الشائعة" row
  Then the About page (عن الملتقى) opens
  # A dedicated app FAQ screen + GET /app/faq endpoint is a tracked follow-up;
  # the About page carries the venue/event info this row promises today.
```

### E2E-MOB013-010 — External links (config-driven)

```gherkin
Scenario: Social + Visit-Saudi links open externally
  Given the build defines SIMF_SOCIAL_X / _INSTAGRAM / _LINKEDIN / _YOUTUBE /
        _TIKTOK and SIMF_VISIT_SAUDI_URL
  When the user taps a follow-us button or the روح السعودية row
  Then the OS opens the configured URL externally
  And a button whose URL is not configured is inert (never a dead link)
```

---

_Last reviewed:_ `2026-06-13` by `SIMF Team`.
