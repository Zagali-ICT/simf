# E2E test catalogue — `More` (`more`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> More screen is a **navigation hub with no API of its own** (a منطقتي profile
> header card + three grouped sections of nav rows + a version line).
> **Re-skinned to Figma `1129:17224` (D-465).** Widget-tested in
> `src/Mobile/simf_app/test/features/more/more_screen_test.dart`.

| | |
|--|--|
| **Page** | [`Page_041`](../../App/Page_041/README.md) |
| **Route** | app screen #41 `/more` (no API) |
| **Surface** | Mobile (Flutter) |
| **Figma** | `1129:17224` |
| **Auth setup** | **None** to browse the sections; the منطقتي card + تسجيل الخروج show only when signed in. Destination routes keep their own auth gate. |
| **Last reviewed** | 2026-08-20 |

## Layout (D-465)

- **منطقتي profile card** (signed-in only) — avatar + name · tier (from `MyAreaDashboard`), taps to `/my-area`.
- **معلومات الملتقى**: عن الملتقى → `/about`; دليل الملتقى → `/forum-guide`; الأسئلة الشائعة → `/faq`; عروض الجلسات → **My sessions** `/my-area/sessions` (**signed-in attendee only** — D-710 reversed the D-609 removal on 2026-07-09; this line previously said the row was a ComingSoon placeholder for `/session-presentations`, and it has been neither since then); استكشف الرياض · VisitSaudi → external `VisitSaudiUrl`.
- **الإعدادات**: اللغة (shows the current language, taps to toggle); إمكانية الوصول → `/settings/accessibility`; الإشعارات → `/notifications`; إعادة تعيين كلمة المرور (**signed-in only**, D-658) → `/forgot-password` (reuses the forgot→email-code→reset flow).
- **قانوني**: الشروط والأحكام → `/terms`; تواصل معنا → `/contact-us` (a real screen since #203 was built — this line said ComingSoon); تقييم التطبيق → `/rate`.
- **تسجيل الخروج** (signed-in only) → confirm dialog → sign-out → `/sign-in`.
- Version line from the REAL installed version (`package_info_plus`, D-736):
  `SIMF 2026 · الإصدار {v}` (AR) / `SIMF 2026 · v{v}` (EN) — e.g.
  `SIMF 2026 · v1.0.0` when the installed version is 1.0.0.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB041-001 | The three grouped sections + their rows render | happy | P0 | authored ✓ (screen `renders the three grouped sections and their rows`) |
| E2E-MOB041-002 | The version line shows the real installed version — `SIMF 2026 · الإصدار {v}` / `SIMF 2026 · v{v}` (D-736) | happy | P1 | authored ✓ (screen `shows the app-version line`) |
| E2E-MOB041-003 | Guest hides the منطقتي card + sign-out | auth | P0 | authored ✓ (screen `guest hides the profile card and sign-out`) |
| E2E-MOB041-004 | Signed-in shows the منطقتي card (name · tier) + sign-out | happy | P0 | authored ✓ (screen `signed-in shows the منطقتي profile card + sign-out`) |
| E2E-MOB041-005 | Tapping About routes to the About screen | happy | P0 | authored ✓ (screen `tapping About navigates to the About route`) |
| E2E-MOB041-006 | ~~An unbuilt entry (Forum guide/FAQ/PPT/Contact us) opens ComingSoon~~ — **superseded 2026-08-20:** all four now have real screens (`ForumGuideScreen` / `FaqScreen` / `SessionPresentationsScreen` / `ContactUsScreen` in `router.dart`), so no More row falls through to `ComingSoonScreen` any more. Each row's own catalogue owns its scenarios | edge | P1 | n/a — no ComingSoon row is left on this page |
| E2E-MOB041-007 | Tapping a gated destination (Notifications) while signed-out bounces to sign-in | edge | P1 | covered (router auth gate, destination #33) |
| E2E-MOB041-008 | Signed-in shows the إعادة تعيين كلمة المرور row; guest hides it | auth | P1 | authored ✓ (screen `renders the three grouped sections…` + `guest hides…`) |
| E2E-MOB041-009 | Tapping إعادة تعيين كلمة المرور opens the forgot-password flow | happy | P1 | authored ✓ (screen `signed-in tapping Reset password opens the forgot flow`) |
| E2E-MOB041-010 | Every forward "open" caret on the page — the منطقتي profile card's **and** each nav row's — points to the inline end: right in LTR (English), left in RTL (Arabic), via the shared `SimfForwardChevron`. **This row read "authored ✓" from 2026-07-22 while the profile card was still a bare `SimfSvgIcon`** — the widget test proved the shared chevron flips, not that the card used it, so under English the card's caret pointed left while every row beneath it pointed right. Fixed 2026-08-20 | i18n | P2 | authored ✓ (`test/app/widgets/simf_forward_chevron_test.dart` — LTR flip / RTL no-flip; `test/features/forward_navigation_chevron_test.dart` — the منطقتي card, and the four other rows that had the same defect, each render their caret through the shared chevron) |
| E2E-MOB041-011 | **Two menus are no longer both "More" (BUG-017):** the side drawer (`MoreDrawer`, the flat list of every destination, opened by the header ☰) is titled **القائمة / Menu**; this **More** screen — the structured hub (My area / Forum info / Settings / Legal) that uniquely holds the **language** row — keeps **المزيد / More** | nav | P2 | authored ✓ (`more_drawer_test` — `BUG-017 — the drawer is titled "Menu", not a second "More"`) |
| E2E-MOB041-012 | **سياسة الخصوصية / Privacy policy** sits in قانوني beside the terms and, being the published web policy rather than an in-app copy, asks before leaving the app (shared `confirmThenLaunchExternal`, target `BuildConfig.privacyPolicyUrl` = `https://web.simrsnf.com/privacy`); Cancel keeps the user on More and launches nothing. The same entry appears in the side drawer, where it must NOT reach `pushNamed` — it has no route. Google Play requires the policy to be reachable from inside an app handling identity documents, photos, camera and biometrics | happy | P0 | authored ✓ (screen `Privacy policy asks before leaving the app`; drawer `the external Privacy policy entry confirms instead of routing`) |
| E2E-MOB041-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOB041-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

```gherkin
Feature: More (navigation hub, Figma 1129:17224)

Scenario: The grouped sections render
  When a user opens /more
  Then the section headers معلومات الملتقى, الإعدادات and قانوني are shown
  And rows are shown for عن الملتقى, دليل الملتقى, الأسئلة الشائعة, عروض الجلسات,
      استكشف الرياض, اللغة, إمكانية الوصول, الإشعارات, الشروط والأحكام,
      تواصل معنا and تقييم التطبيق
  And the اللغة row shows the current language value
  And the footer version line shows the REAL installed version (package_info,
      D-736) — "SIMF 2026 · الإصدار {v}" (AR) / "SIMF 2026 · v{v}" (EN), e.g.
      "SIMF 2026 · v1.0.0" only when the installed version is 1.0.0

Scenario: The profile card and sign-out are signed-in only
  Given a guest (signed out) opens /more
  Then no منطقتي card and no تسجيل الخروج link are shown
  Given an approved visitor opens /more
  Then the منطقتي card shows their name · tier
  And a تسجيل الخروج link is shown

Scenario: Tapping About routes to its screen
  When the user taps "عن الملتقى"
  Then the app navigates to /about

Scenario: Reset password is a signed-in-only account action (D-658)
  Given a guest (signed out) opens /more
  Then no "إعادة تعيين كلمة المرور" row is shown
  Given an approved visitor opens /more
  Then an "إعادة تعيين كلمة المرور" row is shown in the الإعدادات section
  When the visitor taps it
  Then the app navigates to the forgot-password screen (which emails a reset code)

Scenario: Every forward caret on the page points the same way (2026-08-20)
  Given the app is in English (LTR)
  When the user opens /more signed in
  Then the منطقتي profile card's caret points RIGHT
  And every nav row caret below it points RIGHT
  Given the app is in Arabic (RTL)
  When the user opens /more signed in
  Then the منطقتي profile card's caret points LEFT
  And every nav row caret below it points LEFT
```

> The "unbuilt entry opens ComingSoon" scenario that used to sit here was
> removed on 2026-08-20: دليل الملتقى, الأسئلة الشائعة, تواصل معنا and عروض
> الجلسات all reach real screens now, so there is no ComingSoon fall-through
> left on this page to assert (see E2E-MOB041-006).

**Evidence:** screen tests (**8**: grouped rows, version line, guest-hides-card,
signed-in-card, About-nav, reset-password → forgot nav, and the D-710 عروض الجلسات
row shown to a signed-in attendee / hidden from a guest) —
`test/features/more/more_screen_test.dart`. The caret directions are in
`test/app/widgets/simf_forward_chevron_test.dart` +
`test/features/forward_navigation_chevron_test.dart`. The old line here claimed 7
cases and pointed at a ComingSoon fall-through for routes 200–203; both were stale.

---

_Last reviewed:_ `2026-08-20` by `SIMF Team` (app deep-clean audit — the منطقتي
profile card's caret now goes through the shared `SimfForwardChevron`, so it no
longer points the Arabic way under English while every row beneath it points the
other way; **the Arabic golden `more_1129-17224.png` is unchanged**, the flip
being LTR-only. Also corrected here: the four rows this file called ComingSoon
placeholders all have real screens, and عروض الجلسات is My sessions, not
`/session-presentations` — E2E-MOB041-006 / -010).
_Prior:_ `2026-07-26` by `SIMF Team` (BUG-017 — the side drawer is renamed
**Menu** so it no longer collides with this **More** hub; the shared language
toggle was also added to the signed-in Home header so the language switch is
reachable from Home — E2E-MOB041-011 / E2E-MOB013-023). Prior: `2026-07-22` by `Claude` (forward-chevron LTR direction — E2E-MOB041-010, the row caret now points to the inline end via the shared SimfForwardChevron); `2026-07-10` by `SIMF Team` (D-736 — the version line reads the real installed version, no longer a literal).
