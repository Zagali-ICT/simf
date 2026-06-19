# Page 041 — المزيد · More

Per-page documentation folder (App screen 41).

## Identity
| | |
|---|---|
| Mockup page | **41** (`Mockup.html`) |
| Route | `RouteNames.more` → `/more` (**public, anonymous**) |
| Titles | AR **المزيد** · EN **More** |
| Section | 8 — Settings & legal |
| Nature | **Navigation hub** — a منطقتي profile card + three grouped sections of nav rows + a static version line |
| App privilege | **Guest+ (anonymous).** No API of its own; the منطقتي card resolves the signed-in dashboard. |
| Status | **No API** (navigation hub); **Flutter screen BUILT; grouped re-skin Figma `1129:17224` (D-465)** |

## API (authoritative contract)
None of its own. The منطقتي header card (signed-in only) reuses
`GET /app/account/dashboard` (best-effort) for the name · tier; everything else is
in-app navigation (`context.pushNamed`) or the locale toggle.

## Behaviour
On the navy `KsaPage` shell (Figma `1129:17224`): a **منطقتي profile card**
(signed-in only — avatar + name · tier, taps to `/my-area`), three grouped
sections of nav rows, a **تسجيل الخروج** link (signed-in only — confirm dialog →
sign-out → `/sign-in`, shared `features/auth/sign_out.dart`), and the
`SIMF 2026 · v1.0.0` version line.

| Section | Rows → destination |
|---------|--------------------|
| معلومات الملتقى | عن الملتقى → `/about` · دليل الملتقى → ComingSoon `/forum-guide` · الأسئلة الشائعة → ComingSoon `/faq` · عروض الجلسات → ComingSoon `/session-presentations` · استكشف الرياض · VisitSaudi → external |
| الإعدادات | اللغة (shows current value, toggles) · إمكانية الوصول → `/settings/accessibility` · الإشعارات → `/notifications` |
| قانوني | الشروط والأحكام → `/terms` · تواصل معنا → ComingSoon `/contact-us` · تقييم التطبيق → `/rate` |

The four ComingSoon entries route to the standard placeholder (owner choice
"parity now, ComingSoon for unbuilt" — D-465); the destination routes keep their
own auth gate (e.g. Notifications #33 bounces a signed-out user to sign-in).

## Tests
- Widget: `src/Mobile/simf_app/test/features/more/more_screen_test.dart`
  (grouped rows; version line; guest hides card+sign-out; signed-in shows
  card+sign-out; tap About → navigates).
- E2E: [`docs/tests/e2e/mobile-more.md`](../../tests/e2e/mobile-more.md).
