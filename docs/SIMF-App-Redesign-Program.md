# SIMF App Redesign Programme — KSA-Project Figma

Last updated: 2026-06-12 · Owner directive: replace the current Flutter entry/auth
screens with the delivered KSA-Project Figma designs, one page per changeset.
This document is the **continuity anchor** (same pattern as the D-356 programme
doc): the binding decisions, the per-page status board, and the per-page gate
checklist live here. Decisions log entries: D-358 (login preview), D-359
(programme + Phase 0), D-370 (Wave 2 start).

Figma file: `PSXHhY0UVTAPSaIOf9uNKd` ("KSA Project"). Wave 1 covered the 10
frames delivered first — the **entry/auth flow** (8 app screens; the 3
onboarding frames are one carousel screen). The designer has since delivered
**11 further frames** — Wave 2 (owner-approved 2026-06-12, starting with
Page 005); see the Wave 2 board below.

## Binding programme decisions (owner-approved, 2026-06-11)

1. **Replace in place; old screens parked in a temp directory.** Each redesigned
   screen lands at the **real route and real file path**; the previous screen
   file + its widget test move to `lib/features/_legacy_mockup/` /
   `test/_legacy_mockup/` (kept compiling, never routed). Deletion of the legacy
   directory is an owner decision at programme close (§6 freeze rules).
2. **Design system first (Phase 0).** The Figma palette/typography live in
   `SimfTokens` + `SimfTheme`; screens consume tokens, never literals.
3. **Login v2 is done and gets promoted** to the official `/sign-in` (Phase 1);
   no further redesign work on it.
4. **Full Definition of Done per page** (D-246): implement → widget tests →
   analyze + full suite → live browser check vs the Figma frame → E2E catalogue
   + PAGE-INDEX + per-page doc → decisions-log entry → commit.
5. **Behaviour changes are never silent.** A design that changes a flow or
   contract (fields moved between pages, buttons added/removed) is flagged to
   the owner before implementation — design fidelity never silently changes an
   API/flow contract.

## Design system (Phase 0 as-built)

Figma variables → `SimfTokens`:

| Figma variable | Value | Token |
|---|---|---|
| Primary- Color | `#01132D` | `navy` (dark scaffold) |
| BG | `#192B41` | `navyDeep` (boxes/cards on navy) |
| Secondary- Color | `#C9A84C` | `accent` (gold) |
| Pragraph Color | `#C2B8A2` | `beigeBorder` |
| — (login frame) | `#102238` | `navySurface` (elevated navy surface) |
| — (login card) | `#F1ECE4` | `cardBeige` |
| — | `#D0AC77` | `goldSoft` |
| — | `#111827` / `#6C7278` / `#00245E` | `headlineInk` / `greyText` / `linkNavy` |

Type: Title 24 SemiBold / Sub-title 18 / Paragraph 16 — rendered in the bundled
IBM Plex Sans Arabic (D-329; the design's Inter/Plus Jakarta are not bundled).
Theme: gold `FilledButton` = white bold text, radius 4, height 48; dark
`OutlinedButton` = beige border, radius 4; selected chips = solid-gold pills.
Shared widgets: `lib/app/widgets/simf_logo.dart` (`SimfLogo`, 4x asset
`assets/images/simf_logo.png`, Figma node 159:580).

## Status board

| # | Screen | Route | Figma node(s) | Status | Commit |
|---|--------|-------|---------------|--------|--------|
| P0 | Design system (tokens/theme/logo) | — | variables + 159:580 | ✅ shipped 2026-06-11 | `e7f8c7b` |
| P1 | Promote Login v2 → official sign-in | `/sign-in` | 168:2800 | ✅ shipped 2026-06-11 (D-360) | preview `be81082`; promotion — see git log |
| 1 | Splash (Page 001) | `/splash` | 159:573 | ✅ shipped 2026-06-11 (D-361) | — |
| 2 | Onboarding carousel (Page 002) | `/onboarding` | 148:22, 159:942, 159:1052 | ✅ shipped 2026-06-11 (D-362 — videos dropped for static panels, owner decision) | — |
| 3 | Profile-data form (Page 007) | `/sign-up/visitor` | 168:2972 | ✅ shipped 2026-06-11 (D-368 — frame's "رقم اللوحة" skipped: no backend field; DOB/place-of-birth/national-ID path kept: API-required; live check N/A: auth-gated). ~~Page 005 register keeps its current UI (no frame delivered)~~ — superseded: the register frame arrived in Wave 2 and shipped as W2-1 (D-370). | — |
| 4 | Email-OTP verify (Page 006) | `/sign-up/otp` | 505:837 | ✅ shipped 2026-06-11 (D-364). The 2FA OTP screen (`/auth/verify-otp`) keeps its old look — restyling it with the same segmented-box pattern is a tracked follow-up. | — |
| 5 | Interests picker (Page 007-01) | `/sign-up/interests` | 505:1083 | ✅ shipped 2026-06-11 (D-365 — live check N/A: auth+draft-gated; widget tests stand in) | — |
| 6 | Registration success (Page 010) | `/registration/success` | 505:1451 | ✅ shipped 2026-06-11 (D-366 — masked reference card, visual-only contact tiles; live check N/A: auth-gated) | — |
| 7 | Terms & conditions | `/terms` | 505:1553 | ✅ shipped 2026-06-11 (D-367 — bullet cards; checkbox gate replaced by the design's single موافق consent button) | — |

## Wave 2 status board (11 new frames, owner-approved 2026-06-12 — D-370)

Build order owner-set: Page 005 first; the rest follow one page per changeset,
each with its own plan→approve step before code.

| # | Screen | Route | Figma node(s) | Status | Commit |
|---|--------|-------|---------------|--------|--------|
| W2-1 | Register (Page 005) | `/sign-up` | 168:3454 | ✅ shipped 2026-06-12 (D-370 — login chrome + beige card; logic byte-identical) | — |
| W2-2 | Home (guest) | `/` | 203:1236 | pending | — |
| W2-3 | Visitor home states 1–3 | `/` (signed-in) | 512:1335 / 512:1492 / 512:1659 | pending | — |
| W2-4 | Profile | TBD (profile/account — frame review first) | 213:963 | pending | — |
| W2-5 | My Place (منطقتي) | `/my-area` | 512:1780 | pending | — |
| W2-6 | Location (venue map) | `/map` | 215:562 | pending | — |
| W2-7 | Calendar (sessions) | `/sessions` | 215:767 | pending | — |
| W2-8 | QR (badge) | `/badge` | 221:769 | pending | — |
| W2-9 | Notifications | `/notifications` | 223:4264 | pending | — |

## Per-page gate checklist (every row above must pass all of these)

1. Figma design context + screenshot fetched; visual target confirmed.
2. Old screen + test parked in the legacy directory; new screen at the real path
   consumes `SimfTokens`/shared widgets; existing behaviour contract preserved.
3. Widget tests ported + extended; `flutter analyze` clean; full suite green.
4. Live browser check vs the frame: screenshot, console clean, network clean,
   no horizontal overflow, no broken images.
5. Docs in the same changeset: E2E catalogue file, PAGE-INDEX row, per-page doc.
6. Decisions-log entry; commit with a descriptive message.

## Known open items

- **Contact tiles (Page 010) — wired via config (D-369):** the tiles open the
  OS dialer/mail through `url_launcher` (owner-approved) gated on
  `BuildConfig.supportPhone` / `supportEmail` (`--dart-define
  SIMF_SUPPORT_PHONE / SIMF_SUPPORT_EMAIL`). **Open input:** the owner still
  needs to supply the official values — empty keeps a tile inert.
- **2FA OTP screen restyle — DONE (D-369):** `/auth/verify-otp` now uses the
  shared `OtpCodeBoxes`/`OtpMark` (extracted from D-364 when this second
  consumer appeared); verified live.
- **Regression pass (D-369):** suite 334/334 + analyze clean; live drive of
  sign-in / OTP screens / terms chrome; backend API started locally —
  `/health` 200 and `GET /app/content/terms` 200 with real bilingual content
  (the in-browser loaded render is CORS-blocked for the dev-web origin; native
  builds are unaffected — the widget tests cover the loaded rendering).

- **Sign-up flow question — RESOLVED (owner, 2026-06-11):** no flow change.
  Register (Page 005, user+pwd+confirm → its own API) keeps its current UI —
  no frame was delivered for it; then OTP; then ONE profile API across TWO
  pages (frame 168:2972 = the Page 007 data form; frame 505:1083 = the
  interests page); then the success/wait screen. Frame 168:2972 is therefore
  the **Page 007 redesign**, mapped onto the existing fields/logic only.
- The design's Arabic header on the login frame ("الملتقى الدولى البحرى")
  differs in spelling from the app's `appName` ("الملتقى البحري") — designer
  typo suspected, owner to confirm.
- Legacy directory deletion at programme close (freeze §6) — owner decision.
- **Shared entry-chrome extraction (flagged by the D-370 simplify pass) — owner
  decision.** The KSA entry chrome is now copy-pasted across the redesigned
  screens: the rotated sweep block exists in **8** screens, the back+globe top
  controls and `_toggleLanguage` in 3, `_Header`/`_FieldLabel` and the bordered
  field decoration in 2–3, the gold-button style (incl. the un-tokenised
  `Color(0x80C9A84C)` disabled colour) in 2, and the test `_FakePrefs` fake in
  6 test files. Per the D-369 second-consumer rule this is past the extraction
  threshold; the fix is a dedicated changeset (shared `lib/app/widgets/` /
  `features/auth/widgets/` chrome + a `SimfTokens.accentDisabled` token +
  a shared test fake) touching the already-shipped screens — too broad to
  bundle into a single page's changeset, so it is parked here for approval.
