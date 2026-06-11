# Page 001 — Design (البداية · Splash) — Flutter

Screen design for the Flutter app. **As-built: the KSA-Project Figma design
(node 159:573 — D-361)**; boot binds to [Page_001_API.md](Page_001_API.md);
rules in [Page_001_Logic.md](Page_001_Logic.md). The previous `Mockup.html`
placeholder is parked in `lib/features/_legacy_mockup/`.

## Layout (top → bottom)
1. **Full-bleed navy** — `SimfTokens.navy` (#01132D, the design's Primary), edge to edge, no app bar.
2. **Centred brand lock-up** — `SimfLogo` (136), then **SAUDI · MOD · RSNF**
   (16, `beigeBorder`), then the forum name **الملتقى البحري السعودي الدولي**
   (24 semibold white, 1.5 line height), then **النسخة الرابعة / ٢٣–٢٥ نوفمبر ٢٠٢٦ · الرياض**
   on two lines (18, `beigeBorder`).
3. **No progress affordance** — the design shows none; the minimum-display timer +
   hard caps (Logic L-1/L-6) bound the wait instead.
4. **Update dialog (conditional)** — a modal over the splash only when the store-native check
   reports an update (hard = non-dismissible, soft = dismissible) — see Logic L-2.

The screen is **non-interactive** apart from the conditional update dialog; there is no
bottom nav, back button, or input on the splash itself.

## Boot binding — what runs behind the logo
| UI moment | Backing work |
|---|---|
| Logo appears | Minimum display timer starts (Logic L-1) |
| (behind logo) | Store-native update check (Logic L-2) + local DB first-run/resume probe (L-3) |
| (behind logo) | Stored-session load → `POST /app/auth/refresh` (E1) → `GET /app/users/me` (E2) |
| Logo fades out | Route-out to last saved screen / entry (Logic L-5) |

## Actions & navigation
| Trigger | Behaviour |
|---|---|
| Boot completes — valid session | Replace-navigate to the **last saved screen** (Logic L-5); back does not return to splash. |
| Boot completes — first run / signed-out / refresh failed | Replace-navigate to onboarding / sign-in entry (Guest entry). |
| Update dialog → Update | Open the native store listing (store-native intent). |
| Update dialog → Later (soft only) | Dismiss and continue route-out. |

## States
- **Loading** — the default splash state: logo + optional spinner while boot work runs.
- **Empty** — not applicable (no data is rendered on this screen).
- **Update required (hard)** — non-dismissible modal over the logo; the only path is to the store.
- **Update available (soft)** — dismissible modal; "Later" continues boot.
- **Error / offline** — the splash itself never shows an error UI; per Logic L-6 it takes a
  fallback route (entry, or offline-degraded resume on cached identity) and lets the
  **destination** screen surface any retry. A hard cap guarantees the splash always advances.

## Transition out
A single **replace** navigation (so the splash is removed from the back stack) with a short
fade once the minimum display timer has elapsed and boot work has resolved.

## Localization & direction
AR primary (RTL), EN secondary. The splash carries no body copy; the centered logo is
direction-neutral. Any update-dialog text is bilingual (AR primary), and dialog layout
mirrors for RTL.

## Design notes
- Minimum display time prevents a sub-100 ms logo flash; a hard cap prevents an indefinite splash.
- The update check is **store-native** — no SIMF version call, no in-screen version text.
- No privilege gate on this screen: it runs before privilege is known and resolves it (L-5).
