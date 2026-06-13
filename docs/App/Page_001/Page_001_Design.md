# Page 001 — Design (البداية · Splash) — Flutter

Screen design for the Flutter app. **As-built: the KSA-Project Figma design
(node 159:573 — D-361, 2026-06-11)**; boot binds to [Page_001_API.md](Page_001_API.md);
rules in [Page_001_Logic.md](Page_001_Logic.md). The previous `Mockup.html`
placeholder is parked in `lib/features/_legacy_mockup/`. Source:
`lib/features/splash/splash_screen.dart`. Last updated 2026-06-13 (conformance pass).

## Layout (top → bottom)
A `Scaffold` on `SimfTokens.navy`, body centred with 16 px (`space4`) horizontal
padding; one min-size `Column`:
1. **Full-bleed navy** — `SimfTokens.navy` (#01132D, the design's Primary), edge to edge, no app bar.
2. **Centred brand lock-up** — `SimfLogo` (136; the palm-and-anchor mark,
   `assets/images/simf_logo.png`), then 8 px gap, then `splashTagline`
   **`SAUDI · MOD · RSNF`** (16, `beigeBorder`), then 40 px gap, then `splashTitle`
   **الملتقى البحري السعودي الدولي** / *Saudi International Maritime Forum*
   (24 semibold white, 1.5 line height), then 24 px gap, then `splashEventLine`
   **النسخة الرابعة** ⏎ **٢٣–٢٥ نوفمبر ٢٠٢٦ · الرياض** / *4th Edition* ⏎
   *23–25 Nov 2026 · Riyadh* on **two lines** (18, `beigeBorder`, 1.5 line height).
   All three texts are centre-aligned.
3. **No progress affordance** — the design shows none (no spinner); the minimum-display
   timer + hard caps (Logic L-1/L-6) bound the wait instead.
4. **Update dialog (conditional)** — an `AlertDialog` over the splash only when the
   store-native check reports an update (hard = non-dismissible, soft = dismissible) —
   see Logic L-2.

The screen is **non-interactive** apart from the conditional update dialog; there is no
bottom nav, back button, or input on the splash itself.

## Boot binding — what runs behind the lock-up
| UI moment | Backing work |
|---|---|
| Lock-up appears | Minimum display timer (1200 ms) starts (Logic L-1) |
| (behind lock-up) | Store-native update check, 5 s cap (Logic L-2) — concurrent with the timer |
| (behind lock-up) | Auth cold-start restore: secure-storage session load → `POST /app/auth/refresh` (E1, only if the access token is missing/expired) → `GET /app/users/me` (E2) — Logic L-4; waited on with an 8 s cap |
| Route-out | One replace-navigation to the destination (Logic L-5) |

## Actions & navigation
| Trigger | Behaviour |
|---|---|
| Boot completes — signed in, profile complete | Replace-navigate to the **last saved location** (if resumable) or Home (Logic L-5); back does not return to splash. |
| Boot completes — signed in, profile incomplete | Replace-navigate to the profile form (`signUpVisitor`, D-374 gate). |
| Boot completes — awaiting email-OTP | Replace-navigate to the OTP entry screen (`verifyOtp`). |
| Boot completes — first run | Replace-navigate to onboarding. |
| Boot completes — signed out / refresh failed | Replace-navigate to the sign-in entry. |
| Update dialog → **تحديث الآن / Update now** (`FilledButton`) | Calls `openStoreListing()` (store-native intent; a no-op until the store-plugin checker is wired). The button does not close the dialog. |
| Update dialog → **لاحقاً / Later** (`TextButton`, soft only) | Closes the dialog; the splash then routes out. A soft dialog routes out **however** it was closed (Later, scrim, or after Update now). |

## States
- **Loading** — the only splash state: the static lock-up while boot work runs (no spinner).
- **Empty** — not applicable (no data is rendered on this screen).
- **Update required (hard)** — non-dismissible `AlertDialog` (`barrierDismissible: false`,
  no Later button) titled **تحديث مطلوب / Update required**; the only path is the store.
- **Update available (soft)** — dismissible `AlertDialog` titled **يتوفر تحديث /
  Update available** with a Later button; closing it continues boot.
- **Error / offline** — the splash itself never shows an error UI; per Logic L-6 it takes a
  fallback route (entry, or offline-degraded resume on the cached identity) and lets the
  **destination** screen surface any retry. Hard caps guarantee the splash always advances.

## Transition out
A single **replace** navigation (`context.go` for a resumed location / `context.goNamed`
for a named route) once the minimum display timer has elapsed and boot work has resolved —
the splash leaves the back stack. A one-shot guard ensures the route-out (or dialog) fires
exactly once; no custom fade animation is applied (the router's default transition runs).

## Localization & direction
AR primary (RTL), EN secondary. The lock-up copy comes from `AppL10n`:
`splashTagline` is the EN-only constant `SAUDI · MOD · RSNF`; `splashTitle` and
`splashEventLine` are bilingual (AR primary). The update-dialog text is bilingual, and
dialog layout mirrors for RTL. The centred column is direction-neutral.

## Design notes
- The 1200 ms minimum display time prevents a logo flash; the 5 s / 8 s caps prevent an
  indefinite splash (Logic L-6).
- The update check is **store-native** — no SIMF version call, no in-screen version text.
- No privilege gate on this screen: it runs before privilege is known and resolves it (L-5).
- D-361 changed the visual subtree only — `SplashController`, the update dialogs and the
  one-shot route-out glue are unchanged from the pre-redesign build.
