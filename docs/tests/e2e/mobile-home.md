# E2E test catalogue — `Home` (`home`)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7). Mobile
> catalogue — Home's on-login bundle (`GET /app/bootstrap`) is built (D-251);
> the API implementation lives in `tests/SIMF.Api.Tests/AppBootstrapTests.cs`.
> The **Flutter screen is built** (D-296), and the signed-in layout was
> **re-laid out to the LIVE Figma frame `758:1134` (exact-parity, D-446)** — the
> guest layout stays on `512:1492`. Widget tests in
> `src/Mobile/simf_app/test/features/home/home_screen_test.dart` cover: guest
> banner + 2×2 tiles + locked بطاقتي card + sign-in CTA + FAQ→FAQ-screen row; the
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
| E2E-MOB013-009 | FAQ row opens the FAQ screen (Wave 1 `GET /app/faq`) | happy | P2 | authored ✓ (screen) |
| E2E-MOB013-010 | Social + Visit-Saudi links launch externally; unset URL = inert button | happy | P2 | authored ✓ (screen — 5 brand buttons render; D-369 contract) |
| E2E-MOB013-011 | Greeting shows the App-profile name, never the email (frame 758:1134, D-408) | happy | P1 | authored ✓ (screen — profile name wins; email fallback suppressed) |
| E2E-MOB013-012 | **#43 — rotating edition hero:** forum name/theme/dates/location (GET /app/organization-profile) over the rotating GET /app/banners images (dots); empty config → the static discover fallback; taps open News | happy | P1 | authored ✓ (widget — home_hero_banner_test; screen — tap → News) |
| E2E-MOB013-013 | Home tiles render the exact iconify SVG glyphs (frame 758:1134, D-446) | i18n/visual | P2 | authored ✓ (screen — `KsaNavTile.iconAsset`) |
| E2E-MOB013-014 | أحدث منشوراتنا card: source row + lead paragraph + post image (758:1240, D-446) | happy | P1 | authored ✓ (screen — image via D-357 NewsImage; counts deferred Phase 2) |
| E2E-MOB013-015 | Section bars open their routes (عن الملتقى→About, الرعاة→Sponsors, الأخبار والتغطية→News) | happy | P1 | authored ✓ (screen — `KsaLinkRow` ×3) |
| E2E-MOB013-016 | The full-width اسأل المحاور tile opens send-question (1052:12856, D-446) | happy | P1 | authored ✓ (screen — tile → `sendQuestion`) |
| E2E-MOB013-017 | RTL tile/row order matches the frame (D-436 position assertions) | i18n | P1 | authored ✓ (screen — `getCenter().dx` about/news/smart-row-2) |
| E2E-MOB013-018 | Discover badge is the filled "السعودية" (signed-in), not "KSA" (758:1280, D-446) | i18n/visual | P2 | authored ✓ (screen) |
| E2E-MOB013-019 | **اللقاءات الثنائية → the VIP meetings page (D-745; ComingSoon retired by B18, 2026-07-27):** the bilateral-meetings tile opens the real VIP `/meetings` page for a VIP, and is hidden for a non-VIP. The old `bilateralMeetings` ComingSoon sentinel (route 204) has been **deleted** — it had no screen and no caller once D-745 landed | happy | P2 | authored ✓ (screen `the bilateral-meetings tile opens the VIP meetings page` + `the bilateral-meetings tile is hidden for a non-VIP (D-745)`) |
| E2E-MOB013-020 | **Smart-features tile → AI-summaries (D-580→D-583):** the smart-row-2 tile reads "ملخص الجلسات" and opens the AI-summaries list (1388:8392, header "ملخص الجلسات") | happy | P2 | authored ✓ (screen — smart-row-2 label + section-scan) |
| E2E-MOB013-021 | **About-tile → session downloads (D-583):** the Home about-row (4-up) tile reads "الجلسات" and opens the session-materials downloads screen (1388:7621, header "الجلسات"); label matches the screen title | happy | P2 | authored ✓ (screen — about-row label + order) |
| E2E-MOB013-023 | **Language switch reachable from Home (BUG-017; owner-confirmed 2026-07-27, D-772):** the signed-in Home greeting header carries the shared `SimfLanguageToggle` as a **sibling beside** the bell + ☰ cluster (not inside it). Before, the only language entry point was the Profile "More" menu, so from Home there was no route to the language switch at all | nav | P1 | authored ✓ (screen `BUG-017 — the greeting header carries the shared language toggle …`; placement + flip in `simf_page_shell_test`) |
| E2E-MOB013-025 | **The Home pill really drives the locale (D-772):** tapping it re-renders the greeting header in the other language — greeting `Welcome` ↔ `مرحبًا`, pill `ع` ↔ `EN` — and persists the choice. Asserted in **both** directions (EN→AR and AR→EN) | i18n | P1 | authored ✓ (screen `the Home language toggle flips EN → AR …` + `… flips AR → EN …`) |
| E2E-MOB013-024 | **Locked guest badge tile announces why (BUG-014):** the guest home's locked "بطاقتي" tile stays intentionally inert but now carries a semantics hint ("Locked — sign in to unlock your smart badge") so a screen-reader user learns it is locked and why | a11y | P2 | authored ✓ (`simf_page_shell_test` — `BUG-014 — a locked tile announces WHY it is locked and stays inert`) |
| E2E-MOB013-022 | **Hero background video (D-756 / D-761):** when `OrganizationProfile.backgroundVideoUrl` is a **direct MP4/HLS** link the home hero plays it muted + looping + no-controls, cover-fitted as the base layer (edition text overlay + scrim stay on top); a **YouTube** link is NOT played in-app (an Android WebView can't be clipped into the band — D-761) and falls back to the banner-image carousel / discover photo, same as null/unsupported | happy | P2 | authored ✓ (widget — `HeroBackgroundVideo.isSupported` gate + hero base-layer selection) |

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
  Then the greeting header shows the avatar, the static "مرحبًا" welcome, the
       user's first name, the bell (with the unread badge), the language + inert
       dark-mode controls and the menu
  And the discover hero banner and the red LIVE banner render
  And three bordered section bars render — عن الملتقى, الرعاة, الأخبار والتغطية
  And the "عن الملتقى" tile group is المتحدثون · المعرض · جلسات plus the
      full-width اسأل المحاور tile
  And the news tiles render اللقاءات الثنائية · الأرشيف
  And the "الميزات الذكية" group renders قابل أشخاص مثلك · المساعد الذكي ·
      عروض الجلسات · بطاقتي الذكية
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

### E2E-MOB013-009 — FAQ row → FAQ screen

```gherkin
Scenario: The FAQ row opens the FAQ screen
  Given the guest home is open
  When the user taps the "الأسئلة الشائعة" row
  Then the FAQ screen (الأسئلة الشائعة) opens
  # Wave 1 shipped the FAQ screen + GET /app/faq; the row opens it directly
  # (it was temporarily pointed at About before the endpoint existed).
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

Scenario: Profile name wins over the auth display name (first name only)
  Given a signed-in visitor whose App profile name is "مهند زقالي محمد"
  And GET /app/account/dashboard returns that identity
  When the signed-in Home loads
  Then the greeting reads "مرحبًا مهند 👋" (the static welcome + first name only, owner 2026-07-21)
  And the auth session display name is NOT shown

Scenario: The email is never rendered as the name
  Given a signed-in visitor whose auth display name IS their email
  And the profile name has not resolved (loading, or a 403 for a pending account)
  When the signed-in Home loads
  Then the greeting shows the wave only ("👋") with no name
  And no "@"-bearing text appears in the header
```

### E2E-MOB013-012 — Rotating edition hero (opens News) (#43; was 758:1203)

```gherkin
Feature: Home edition hero (#43)
  As a signed-in visitor
  I want the hero under the header to show the current forum edition
  So that the home leads with the event, and the image stays fresh

Scenario: The hero shows the edition over rotating banner images
  Given the edition config is loaded (GET /app/organization-profile) with a name,
        theme, event dates and a location
  And GET /app/banners returns two or more active, in-window banners with images
  When the signed-in Home is shown
  Then the hero overlays, under a dark scrim: the forum name (gold), the theme,
       the date range (e.g. "23-25 نوفمبر 2026") and the location
  And the background image auto-advances through the banner images every ~4s with
      position dots (each image at /app/assets/Banner/{id}/image)
  When the user taps the hero
  Then the News list opens

Scenario: No banners / no edition → the static discover fallback
  Given GET /app/banners returns [] (or the edition config is not loaded)
  When the signed-in Home is shown
  Then the hero shows the bundled discover photo with the gold "اكتشف السعودية"
       title and the white "تعال واكتشف جديدك المفضل" sub-line (no dots)
  When the user taps the hero
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
       بطاقتي الذكية the card glyph, عروض الجلسات the new-session glyph
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

### E2E-MOB013-019 — اللقاءات الثنائية opens the VIP meetings page (D-745 / B18)

```gherkin
Scenario: The bilateral-meetings tile opens the real VIP meetings page
  Given the signed-in Home is shown for a VIP attendee
  When the user taps the "اللقاءات الثنائية" news tile
  Then the VIP meetings page opens (RouteNames.meetings, route 116)
  And the media gallery does NOT open

Scenario: The tile is not offered to a non-VIP
  Given the signed-in Home is shown for a non-VIP attendee
  Then no "اللقاءات الثنائية" tile is rendered
```

**B18 (2026-07-27):** the `bilateralMeetings` ComingSoon sentinel (route 204) and
`savedMeetings` (route 206) were **removed**. Both were declared routes with no
screen, no inbound navigation and nothing persisted behind them — 204's tile went
to the real VIP page with D-745, and 206's My-Area stat tile went with the D-609
screen deletion (which had already removed 205 Saved-sessions the same way).

### E2E-MOB013-017 — RTL tile/row order matches the frame (D-436)

```gherkin
Feature: RTL order is proven by position, not by eye (D-436)
  Given the device locale is Arabic and the signed-in Home is shown
  Then in the "عن الملتقى" row, المتحدثون is right of المعرض right of جلسات
  And in the news row, اللقاءات الثنائية is right of الأرشيف
  And in the smart row 2, عروض الجلسات is right of بطاقتي الذكية
```

### E2E-MOB013-018 — Discover badge is the filled "السعودية" (node 758:1280, D-446)

```gherkin
Scenario: Signed-in discover row uses the Arabic filled badge
  Given the signed-in Home is shown
  Then the روح السعودية row's gold badge reads "السعودية" (not "KSA")
  # The guest home keeps the outlined "KSA" badge (frame 758:2910).
```

### E2E-MOB013-020 — Smart-features tile opens the AI-summaries list (D-583)

```gherkin
Scenario: The smart-row-2 tile opens the AI-summaries list
  Given the signed-in Home is shown
  Then the smart-row-2 tile reads "ملخص الجلسات" (Session summaries)
  When the user taps it
  Then the AI-summaries list (1388:8392, header "ملخص الجلسات") opens
  # The session-downloads screen (1388:7621) is the "الجلسات" about-tile;
  # My-Sessions (1388:9067) stays on the My-Area dashboard.
```

### E2E-MOB013-021 — About-tile opens the session downloads screen (D-583)

```gherkin
Scenario: The Home about-tile "الجلسات" opens the session downloads screen
  Given the signed-in Home is shown
  Then the about-row (4-up) tile reads "الجلسات"
  And it sits at the inline-end (leftmost, RTL) of المتحدثون · المعرض · الوفود
  When the user taps it
  Then the session-materials downloads screen (1388:7621, header "الجلسات") opens
  # Its label matches the Figma screen title; the AI-summaries list
  # (1388:8392) is the "ملخص الجلسات" smart-features tile.
```

### E2E-MOB013-022 — Hero plays the CP-configured background video (D-756 / D-761)

```gherkin
Scenario: A direct MP4/HLS background video plays behind the hero
  Given the CP has set OrganizationProfile.backgroundVideoUrl to "https://cdn.example.com/hero.mp4"
  And the app has warmed the organization profile
  When a signed-in visitor opens Home
  Then the hero plays that video muted, looping, with no controls, cover-fitted into the 160px band
  And the video never overflows the hero band (no full-screen spill over the greeting header)
  And the edition name / theme / date / location overlay and the dark scrim stay on top
  And tapping the hero still opens News (the video is non-interactive)

Scenario: A YouTube background link falls back to the banner-image hero (D-761)
  Given the CP has set OrganizationProfile.backgroundVideoUrl to "https://youtu.be/rmW5sJTp-Zo"
  When a signed-in visitor opens Home
  Then the hero shows the rotating banner images (or the discover photo when there are none)
  And no video surface is mounted
  # A YouTube embed is an Android WebView that cannot be clipped into the band, so
  # it is not played in-app; the website still plays the same link (it crops the
  # iframe in CSS). To show a moving video in the app, set a direct MP4/HLS URL.

Scenario: No configured video keeps the banner-image hero
  Given OrganizationProfile.backgroundVideoUrl is null
  When a signed-in visitor opens Home
  Then the hero shows the rotating banner images (or the discover photo when there are none)
  And no video surface is mounted
```

### E2E-MOB013-023 — The language switch is reachable from Home

```gherkin
Scenario: Switching language without leaving Home
  Given I am signed in and on the Home tab
  Then the greeting header shows the language pill next to the bell and the menu
  And the pill offers the language I would switch TO ("EN" under Arabic, "ع" under English)
  When I tap it
  Then the app language flips and the choice is persisted
```

> Every other screen carries the toggle in its header; Home did not, and the
> language row lives only in the Profile "More" menu — a different menu from the
> Home header "More", so from Home there was no route to the language switch at
> all (BUG-017). **Owner-confirmed 2026-07-27 ("keep home lang", D-772),**
> superseding the 2026-07-11 removal of the pill from the shared cluster: the
> Home pill is a **sibling beside** `SimfHeaderActions`, not a member of it, so
> every sub-page keeps the bell + ☰ cluster shape.

**Evidence:** screen test `BUG-017 — the greeting header carries the shared
language toggle, so the language switch is reachable from Home (owner 2026-07-27
"keep home lang", D-772)`; the pill's placement + flip + persist behaviour is
covered by `simf_page_shell_test` (`the signed-in Home greeting header exposes a
WORKING language toggle, beside the bell + ☰ cluster rather than inside it`), and
the re-render is covered by E2E-MOB013-025.

### E2E-MOB013-025 — Tapping the Home pill re-renders Home in the other language

```gherkin
Scenario Outline: The Home language pill actually drives the app locale
  Given I am signed in and on the Home tab with the app in <from>
  Then the greeting reads "<greeting_from>" and the pill offers "<pill_from>"
  When I tap the language pill
  Then the greeting header re-renders in <to> and reads "<greeting_to>"
  And the pill now offers "<pill_to>"
  And "<greeting_from>" is no longer on screen
  And the stored preferred language is "<stored_to>"

  Examples:
    | from    | to      | greeting_from | greeting_to | pill_from | pill_to | stored_to |
    | English | Arabic  | Welcome       | مرحبًا       | ع         | EN      | ar        |
    | Arabic  | English | مرحبًا         | Welcome     | EN        | ع       | en        |
```

> Presence alone is not proof — the pill has to drive the live locale. Both
> directions are asserted so neither language is the only one exercised.

**Evidence:** screen tests `the Home language toggle flips EN → AR and the
greeting header re-renders in Arabic (owner 2026-07-27)` and `… flips AR → EN and
the greeting header re-renders in English (owner 2026-07-27)`, both pumped
against a **live** `localeControllerProvider` (a fixed `MaterialApp.locale` would
swallow the re-render).

### E2E-MOB013-025 — Moderator home lists جلساتي (FR-MOD-001)

```gherkin
Scenario: The moderator's operational home lists the sessions they moderate
  Given a signed-in approved Moderator opens Home
  Then the programme entry ("Sessions") is still offered
  And a "جلساتي / My sessions" section lists one row per session they hold a
    SessionModerator grant on, soonest first, showing the bilingual title,
    the hall and the Saudi 12-hour start time (never a UTC instant)
  When they tap a row
  Then the app opens that session's Q&A desk directly

Scenario: No grants / a failed load
  Given the moderator holds no grants
  Then "لم يتم إسنادك إلى أي جلسة بعد. / You are not assigned to any session
    yet." shows in place of the rows
  And when GET /app/sessions/moderated fails instead
  Then the shared error surface shows "تعذّر تحميل جلساتك. حاول مرة أخرى. /
    Could not load your sessions. Try again." with Retry
  And the list is pull-to-refresh in every state
```

> Full behaviour, the endpoint contract and the matching session-detail gate are
> catalogued in [`mobile-session-moderate.md`](mobile-session-moderate.md)
> **E2E-MOBMOD-009** — this row exists so the Home catalogue records the section.

**Evidence:** `test/features/home/moderator_home_test.dart` (5 cases).

---

_Last reviewed:_ `2026-07-27` by `SIMF Team` — D-772: the owner confirmed the
signed-in Home keeps its language toggle, superseding the 2026-07-11 removal;
added E2E-MOB013-025 for the AR/EN re-render.
_Last reviewed:_ `2026-07-27` by `SIMF Team` — FR-MOD-001: the Moderator
operational home lists جلساتي, the sessions they actually hold a grant on
(E2E-MOB013-025).
_Prior:_ `2026-07-26` by `SIMF Team` — BUG-017: the shared language toggle
was added to the signed-in Home greeting header (E2E-MOB013-023); BUG-014: the
locked guest badge tile now carries a semantics hint (E2E-MOB013-024).
_Prior:_ `2026-07-01` by `SIMF Team` — D-583: the two Home session tiles
were crossed against their Figma node titles and swapped so each label opens the
same-titled screen — about "الجلسات" → session-downloads (1388:7621), smart-features
"ملخص الجلسات" → AI-summaries list (1388:8392). Supersedes D-582 (which relabelled the
wrong tile) and corrects the D-580 smart-tile. (Prior 2026-06-19 D-462: the "تابعنا"
social row URLs come from the CP-editable site-settings, falling back to build-time
config then inert.)
