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
| **Last reviewed** | 2026-06-20 |

## Layout (D-465)

- **منطقتي profile card** (signed-in only) — avatar + name · tier (from `MyAreaDashboard`), taps to `/my-area`.
- **معلومات الملتقى**: عن الملتقى → `/about`; دليل الملتقى → ComingSoon (`/forum-guide`); الأسئلة الشائعة → ComingSoon (`/faq`); عروض الجلسات → ComingSoon (`/session-presentations`); استكشف الرياض · VisitSaudi → external `VisitSaudiUrl`.
- **الإعدادات**: اللغة (shows the current language, taps to toggle); إمكانية الوصول → `/settings/accessibility`; الإشعارات → `/notifications`; إعادة تعيين كلمة المرور (**signed-in only**, D-658) → `/forgot-password` (reuses the forgot→email-code→reset flow).
- **قانوني**: الشروط والأحكام → `/terms`; تواصل معنا → ComingSoon (`/contact-us`); تقييم التطبيق → `/rate`.
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
| E2E-MOB041-006 | An unbuilt entry (Forum guide/FAQ/PPT/Contact us) opens ComingSoon | edge | P1 | covered (routes 200–203 fall through to `ComingSoonScreen`) |
| E2E-MOB041-007 | Tapping a gated destination (Notifications) while signed-out bounces to sign-in | edge | P1 | covered (router auth gate, destination #33) |
| E2E-MOB041-008 | Signed-in shows the إعادة تعيين كلمة المرور row; guest hides it | auth | P1 | authored ✓ (screen `renders the three grouped sections…` + `guest hides…`) |
| E2E-MOB041-009 | Tapping إعادة تعيين كلمة المرور opens the forgot-password flow | happy | P1 | authored ✓ (screen `signed-in tapping Reset password opens the forgot flow`) |

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

Scenario: An unbuilt entry opens the ComingSoon placeholder
  When the user taps "دليل الملتقى"
  Then the ComingSoon screen for /forum-guide is shown (no dead-end)
```

**Evidence:** screen tests (7: grouped rows, version, guest-hides-card, signed-in-card, About-nav, reset-password-row present/hidden, reset-password → forgot nav).
ComingSoon routing covered by the router's fall-through for routes 200–203.

---

_Last reviewed:_ `2026-07-10` by `SIMF Team` (D-736 — the version line reads the real installed version, no longer a literal).
