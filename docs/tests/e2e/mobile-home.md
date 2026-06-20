# E2E test catalogue — `Home` (`home`)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7). Mobile
> catalogue — Home's on-login bundle (`GET /app/bootstrap`) is built (D-251);
> the API implementation lives in `tests/SIMF.Api.Tests/AppBootstrapTests.cs`.
> The **Flutter screen is built** (D-296), and the signed-in layout was
> **re-laid out to the LIVE Figma frame `758:1134` (exact-parity, D-446)** — the
> guest layout stays on `512:1492`. Widget tests in
> `src/Mobile/simf_app/test/features/home/home_screen_test.dart` cover: guest
> banner + 2×2 tiles + locked بطاقتي card + sign-in CTA + FAQ→About row; the
> signed-in greeting header + discover hero + live banner; the three **bordered
> section bars** (عن الملتقى / الرعاة / الأخبار والتغطية, `KsaLinkRow`); the tile
> groups (المتحدثون·المعرض·جلسات + اسأل المحاور; اللقاءات الثنائية·الأرشيف; the
> smart 2×2); the latest-post card (source row + lead paragraph + the NewsImage
> via the D-357 route); the social row + discover row; the unread badge; and the
> **D-436 RTL `getCenter().dx` order assertions**. The unread-count provider's
> gating is covered in
> `src/Mobile/simf_app/test/features/notifications/notifications_repository_test.dart`.
> The old mockup screen + test are parked in `_legacy_mockup/`. **Phase 2 (not
> yet built):** the post-card engagement counts (58/340/1.2k, node `758:1252`)
> need admin-entered count columns on `News` + API/CP — the row stays hidden
> until the wire carries them (never faked).

| | |
|--|--|
| **Page** | [`Page_013`](../../App/Page_013/README.md) (App page docs) |
| **Route** | `GET /api/v1/app/bootstrap` · `GET /app/account/notifications/unread-count` · app screen #13 `/` |
| **Surface** | Mobile (Flutter) + App API |
| **Test runner** | xUnit + `WebApplicationFactory` (API) · Flutter widget/integration test (screen) |
| **Auth setup** | A signed-in visitor token (`AuthFlow.SignInVisitorWithoutTwoFactorAsync`); `AuthFlow.SetAccountState` for the approved case. **No literal secrets.** |
| **Last reviewed** | 2026-06-19 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB013-001 | Bootstrap returns user + unread count + server time | happy | P0 | authored ✓ (`Bootstrap_returns_the_current_user_unread_count_and_server_time`) |
| E2E-MOB013-002 | Bootstrap unread count reflects a dispatched notification | happy | P1 | authored ✓ (`Bootstrap_unread_count_reflects_a_dispatched_notification`) |
| E2E-MOB013-003 | Bootstrap reflects an approved account (pending → approved routing) | happy | P0 | authored ✓ (`Bootstrap_reflects_an_approved_account`) |
| E2E-MOB013-004 | No token → 401 | auth | P0 | authored ✓ (`Bootstrap_without_a_token_returns_401`) |
| E2E-MOB013-005 | Guest (no token) renders the KSA guest layout, no bootstrap call | happy | P1 | authored ✓ (screen — guest banner + 2×2 tiles + sign-in CTA, no bell) |
| E2E-MOB013-006 | Privilege from the cached auth session picks the layout | auth | P1 | authored ✓ (screen — signed-in greeting + 3 section bars + tile groups) |
| E2E-MOB013-007 | RTL render of Home tiles + bell badge | i18n | P1 | authored ✓ (screen — Arabic RTL + badge hidden/shown) |
| E2E-MOB013-008 | Locked بطاقتي card is visible but inert as a guest | auth | P1 | authored ✓ (screen — disabled tile ignores taps) |
| E2E-MOB013-009 | FAQ row opens the About page (no app FAQ endpoint yet) | happy | P2 | authored ✓ (screen) |
| E2E-MOB013-010 | Social + Visit-Saudi links launch externally; unset URL = inert button | happy | P2 | authored ✓ (screen — 5 brand buttons render; D-369 contract) |
| E2E-MOB013-011 | Greeting shows the App-profile name, never the email (frame 758:1134, D-408) | happy | P1 | authored ✓ (screen — profile name wins; email fallback suppressed) |
| E2E-MOB013-012 | Discovery hero banner renders and opens News (node 758:1203, D-408) | happy | P2 | authored ✓ (screen — banner tap → News) |
| E2E-MOB013-013 | Home tiles render the exact iconify SVG glyphs (frame 758:1134, D-446) | i18n/visual | P2 | authored ✓ (screen — `KsaNavTile.iconAsset`) |
| E2E-MOB013-014 | أحدث منشوراتنا card: source row + lead paragraph + post image (758:1240, D-446) | happy | P1 | authored ✓ (screen — image via D-357 NewsImage; counts deferred Phase 2) |
| E2E-MOB013-015 | Section bars open their routes (عن الملتقى→About, الرعاة→Sponsors, الأخبار والتغطية→News) | happy | P1 | authored ✓ (screen — `KsaLinkRow` ×3) |
| E2E-MOB013-016 | The full-width اسأل المحاور tile opens send-question (1052:12856, D-446) | happy | P1 | authored ✓ (screen — tile → `sendQuestion`) |
| E2E-MOB013-017 | RTL tile/row order matches the frame (D-436 position assertions) | i18n | P1 | authored ✓ (screen — `getCenter().dx` about/news/smart-row-2) |
| E2E-MOB013-018 | Discover badge is the filled "السعودية" (signed-in), not "KSA" (758:1280, D-446) | i18n/visual | P2 | authored ✓ (screen) |

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
Scenario: A signed-in visitor gets the greeting home (frame 758:1134)
  Given a signed-in user whose cached appRole is "Visitor"
  When Home renders
  Then the greeting header shows the avatar, "صباح الخير/مساء الخير", the
       user's name, the bell (with the unread badge), the language + inert
       dark-mode controls and the menu
  And the discover hero banner and the red LIVE banner render
  And three bordered section bars render — عن الملتقى, الرعاة, الأخبار والتغطية
  And the "عن الملتقى" tile group is المتحدثون · المعرض · جلسات plus the
      full-width اسأل المحاور tile
  And the news tiles render اللقاءات الثنائية · الأرشيف
  And the "الميزات الذكية" group renders قابل أشخاص مثلك · المساعد الذكي ·
      ملخص الجلسات · بطاقتي الذكية
  And the follow-us row and the روح السعودية discover row render
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

### E2E-MOB013-014 — أحدث منشوراتنا latest-post teaser (frame 758:1240, D-446)

```gherkin
Scenario: Signed-in home shows the latest news post
  Given there is at least one published news item (GET /app/news)
  And the user is signed in (visitor/moderator/staff)
  When Home loads and the user scrolls to "أحدث منشوراتنا / Latest posts"
  Then the section header is shown (no "المزيد" link — the frame omits it)
  And the card shows, at the inline-end, the gold "SIMF" chip + the source
      name; at the inline-start the "@SIMF · <relative time>" line
      (just-now / N min / N h / N d ago)
  And the lead paragraph (the excerpt, else the title) is shown
  And the post image is the article's NewsImage asset (D-357 anon route),
      with a navy image-glyph fallback when none is uploaded
  And NO engagement counts appear yet (Phase 2 admin-entered data — never faked)

Scenario: Tapping the post opens the article
  Given the latest-post card is visible
  When the user taps it
  Then the news article screen opens (GET /app/news/{id})

Scenario: No posts hides the section
  Given GET /app/news returns an empty list (or errors)
  When the signed-in Home loads
  Then the أحدث منشوراتنا section is omitted entirely (no empty placeholder)
```

### E2E-MOB013-011 — Greeting shows the profile name, never the email (D-408)

```gherkin
Feature: Signed-in greeting (frame 758:1134)
  As a signed-in visitor
  I want to be greeted by my name
  So that the home feels personal — and my email is never shown as a name

Scenario: Profile name wins over the auth display name
  Given a signed-in visitor whose App profile name is "مهند زقالي محمد"
  And GET /app/account/dashboard returns that identity
  When the signed-in Home loads
  Then the greeting reads "<time-of-day> مهند زقالي محمد 👋"
  And the auth session display name is NOT shown

Scenario: The email is never rendered as the name
  Given a signed-in visitor whose auth display name IS their email
  And the profile name has not resolved (loading, or a 403 for a pending account)
  When the signed-in Home loads
  Then the greeting shows the wave only ("👋") with no name
  And no "@"-bearing text appears in the header
```

### E2E-MOB013-012 — Discovery hero banner opens News (node 758:1203, D-408)

```gherkin
Feature: Discovery hero banner
  As a signed-in visitor
  I want the "اكتشف / Discover" banner under the header
  So that I can jump into the latest content

Scenario: The banner renders and opens News
  Given the signed-in Home is shown
  Then a hero banner shows the event image under a dark scrim
  And the gold "اكتشف / Discover" title and the white
      "تعال واكتشف جديدك المفضل / Come discover your favourites" sub-line
  When the user taps the banner
  Then the News list opens
```

### E2E-MOB013-013 — Tiles render the exact iconify glyphs (frame 758:1134, D-446)

```gherkin
Feature: Home tile icons match the design
  As the design owner
  I want the home tiles to use the frame's exact iconify glyphs
  So that the app matches Figma 758:1134, not near-equivalents

Scenario: Each signed-in tile shows its bundled SVG glyph
  Given the signed-in Home is shown
  Then المتحدثون shows the people glyph (bi:people), المعرض the chart glyph,
       جلسات the target glyph (streamline:target-3)
  And اسأل المحاور shows the user glyph (solar:user-outline)
  And الأرشيف shows the archive glyph, اللقاءات الثنائية the video glyph
  And المساعد الذكي the message glyph, قابل أشخاص مثلك the users glyph,
       بطاقتي الذكية the card glyph, ملخص الجلسات the new-session glyph
  And each glyph is tinted to the tile foreground (gold when enabled)
```

### E2E-MOB013-015 — Section bars open their routes (frame 758:1207/1049:12844/758:1211)

```gherkin
Feature: The signed-in home section bars are navigable
  As a signed-in visitor
  I want the bordered section bars to take me to their pages

Scenario: Each bar opens its destination
  Given the signed-in Home is shown
  When the user taps the "عن الملتقى" bar
  Then the About-the-forum page opens
  When the user taps the "الرعاة" bar
  Then the Sponsors page opens
  When the user taps the "الأخبار والتغطية" bar
  Then the News list opens
```

### E2E-MOB013-016 — اسأل المحاور opens send-question (node 1052:12856, D-446)

```gherkin
Scenario: The full-width اسأل المحاور tile opens the send-question screen
  Given the signed-in Home is shown
  When the user taps the full-width "اسأل المحاور" tile
  Then the send-a-question screen opens (RouteNames.sendQuestion)
```

### E2E-MOB013-017 — RTL tile/row order matches the frame (D-436)

```gherkin
Feature: RTL order is proven by position, not by eye (D-436)
  Given the device locale is Arabic and the signed-in Home is shown
  Then in the "عن الملتقى" row, المتحدثون is right of المعرض right of جلسات
  And in the news row, اللقاءات الثنائية is right of الأرشيف
  And in the smart row 2, ملخص الجلسات is right of بطاقتي الذكية
```

### E2E-MOB013-018 — Discover badge is the filled "السعودية" (node 758:1280, D-446)

```gherkin
Scenario: Signed-in discover row uses the Arabic filled badge
  Given the signed-in Home is shown
  Then the روح السعودية row's gold badge reads "السعودية" (not "KSA")
  # The guest home keeps the outlined "KSA" badge (frame 758:2910).
```

---

_Last reviewed:_ `2026-06-19` by `SIMF Team` — D-462: the "تابعنا" social row URLs
now come from the CP-editable site-settings (`GET /app/site-settings`), falling
back to the build-time config then inert (D-369); the five brand buttons render
unchanged (covered by the existing social-row widget test).
