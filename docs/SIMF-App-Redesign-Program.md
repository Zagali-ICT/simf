# SIMF App Redesign Programme — KSA-Project Figma

Last updated: 2026-06-11 · Owner directive: replace the current Flutter entry/auth
screens with the delivered KSA-Project Figma designs, one page per changeset.
This document is the **continuity anchor** (same pattern as the D-356 programme
doc): the binding decisions, the per-page status board, and the per-page gate
checklist live here. Decisions log entries: D-358 (login preview), D-359
(programme + Phase 0).

Figma file: `PSXHhY0UVTAPSaIOf9uNKd` ("KSA Project"). All 10 delivered frames
identified — the programme scope is the **entry/auth flow** (8 app screens; the
3 onboarding frames are one carousel screen).

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
| P0 | Design system (tokens/theme/logo) | — | variables + 159:580 | 🔨 in progress | — |
| P1 | Promote Login v2 → official sign-in | `/sign-in` | 168:2800 | ⏳ pending | preview shipped `be81082` |
| 1 | Splash (Page 001) | `/splash` | 159:573 | ⏳ pending | — |
| 2 | Onboarding carousel (Page 002) | `/onboarding` | 148:22, 159:942, 159:1052 | ⏳ pending | — |
| 3 | Sign-up form (Page 005) | `/sign-up` | 168:2972 | ⏳ pending — ⚠️ flow question open (profile fields merged in; owner call needed before build) | — |
| 4 | Email-OTP verify (Page 006) | `/sign-up/otp` | 505:837 | ⏳ pending (evaluate reuse for `/auth/verify-otp` 2FA) | — |
| 5 | Interests picker (Page 007-01) | `/sign-up/interests` | 505:1083 | ⏳ pending | — |
| 6 | Registration success (Page 010) | `/registration/success` | 505:1451 | ⏳ pending | — |
| 7 | Terms & conditions | `/terms` | 505:1553 | ⏳ pending | — |

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

- **Sign-up flow question (page 3)** — the design merges document/gender/
  organisation/job/attachment fields into sign-up; today those live in the
  Page 007 profile upsert. Owner must choose: one screen driving the two
  existing API calls, or a backend contract change (needs its own approval).
- The design's Arabic header on the login frame ("الملتقى الدولى البحرى")
  differs in spelling from the app's `appName` ("الملتقى البحري") — designer
  typo suspected, owner to confirm.
- Legacy directory deletion at programme close (freeze §6) — owner decision.
