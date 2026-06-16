# SIMF App — Figma Exact-Parity Program (PLAN)

Last updated: 2026-06-16

## Principle (owner-stated)

- **The KSA-Project Figma file is the single source of truth for the design.**
  File key `PSXHhY0UVTAPSaIOf9uNKd`. Where the app differs from its frame, the
  app is wrong — fix the app to the frame exactly (RTL placement, order, colour
  tokens, spacing, fonts, every element).
- **The current temp/mockup pages exist only for prototyping** and are to be
  replaced by the exact-Figma build.
- **Add any missing backend API / field.** If a frame shows data the backend
  does not expose, design + build the API/field (additive migration; preserve
  the shipped public wire — append-only). Do not fake or omit it.
- **Verification rule:** automated gap-reports (and earlier tests) repeatedly
  mis-judged RTL — e.g. `PositionedDirectional(start:)` and a leading `Row`
  child render on the **right** in RTL, not the left. Every RTL claim must be
  confirmed against the rendered frame or a deterministic Arabic-locale
  position test **before** editing (see D-436). Pump tests in `Locale('ar')`,
  not just English.

## Coordination / guardrails

- A **concurrent worker owns the sign-up photo flow** (`sign_up_visitor_screen.dart`,
  `UserProfileResponse`, the profile repository) — commit `1fea142` D-437
  (two-photo split: gallery ID document + camera face + name rules). **Do not
  touch those files**; coordinate via the owner.
- **Targeted staging only** — never `git add -A`; stage only files this program
  touches (concurrent workers share the branch). Serialize EF migrations on the
  shared snapshot; grep the snapshot diff to confirm only our tables/columns.
- Per-page **Definition of Done** (CLAUDE.md / D-246): exact-Figma build →
  docs (PAGE-INDEX + per-page ref) → unit + integration tests (incl. an Arabic
  RTL position test) → E2E catalogue file → review agents + simplify →
  **live device/emulator verify** → commit. Backend changes land as additive
  migrations with `// Tests:` headers.

## Status legend

✅ done & verified · 🟡 near-match, minor polish pending · 🔨 to build · ❓ needs owner data/decision

## Page inventory (target frame → current state → work)

| # | Page | Figma node | File | Status | Work / gaps |
|---|------|-----------|------|--------|-------------|
| 1 | Speaker list | 908-1744 | `features/speakers/speakers_screen.dart` | ✅ | RTL mirror fixed (anchor right / caret left) + RTL test — D-436 |
| 2 | Speaker CV | 908-2110 | `features/speakers/speaker_profile_screen.dart` | 🟡 | verify CV-tab pill RTL order (header forces LTR); intrinsic-width pills vs equal-width; spacing/padding polish; avatar photo = initials (no photo field — see API gaps) |
| 3 | My-seat | 898-2873 | `features/sessions/my_seat_screen.dart` | 🟡 | verify row-chip "B" value tint (gold vs white) against frame; otherwise matches |
| 4 | Session live | 934-3450 | `features/live/live_broadcast_screen.dart` | ✅ | language chip added + wired (D-436); badge already correct; **AI live-caption feed = API gap (see below)** |
| 5 | Ask question | 934-3636 | `features/questions/send_question_screen.dart` | 🟡 | form portion matches; optional faint border on the question box; confirm whether this is a sub-screen of the live frame or its own |
| 6 | Media gallery | 947-3764 | `features/gallery/gallery_screen.dart` | ✅ | tab bar un-mirrored + active navy text (D-436) |
| 7 | Media partners | 958-2246 | `features/media_partners/media_partners_screen.dart` | 🔨 | **rebuild** plain-AppBar page → KSA navy shell + 3-tab hub + 2-col partner grid (gold rounded-square logo container + label); **render real partner logo** (API gap) |
| 8 | News | 948-3961 | `features/news/news_screen.dart` | 🔨 | tab bar already fixed; verify news-card styling (gold chip / title / excerpt) against 948-3961; **render thumbnail (`imageRelativePath`) + date (`publishedAt`)** which exist on the model but are not shown |

### Also in the wider re-skin wave (verify to frame — confirm scope with owner)
| Session detail 889-2450 · Booth 922-2458 · Sponsors 922-2824 · Archive/History 925-3079 | task #93/#97/#98/#99 | 🟡/❓ | parity-check each the same way (RTL-verified) |

## Backend API / field gaps to design (Figma = source of truth)

| Gap | Frame | Current backend | Proposed (additive) | Decision |
|-----|-------|-----------------|---------------------|----------|
| **Media-partner logo** | 958-2246 (partner cards show a logo container) | `PublicMediaPartnerItem` already carries `logoRelativePath`; no image endpoint confirmed | add `GET /app/media-partners/{id}/logo` (bytes, like the booth/news image endpoints) + CP upload field; app renders the logo, falls back to initials | ❓ confirm: real uploaded logos, or keep the gold-icon placeholder? |
| **News thumbnail + date** | 948-3961 (news cards) | `NewsListItem` already has `imageRelativePath` + `publishedAt`; image endpoint likely exists | render both in `_NewsCard`; add the image endpoint only if missing | ❓ confirm the card shows a thumbnail + date |
| **Live AI captions / transcript** | 934-3450 (AI caption strip = live spoken-word text) | none — `LiveSession` has no transcript field; D-349 deferred this (YouTube CC) | this is a **feature** (real-time transcription provider + a caption stream API), not a field | ❓ build now (large) or keep YouTube CC + the static strip placeholder? |
| **Speaker photo** | 908-2110 / 908-1744 (avatar) | no speaker photo URL field | add `Speaker.PhotoRelativePath` + `GET /app/speakers/{id}/photo` + CP upload; app renders photo, falls back to initials/anchor | ❓ in scope now, or keep initials until the SIMF-VID-001 asset pass? |

## Execution plan (sequence)

Each item is its own plan→build→verify→commit cycle; backend gaps get an
additive migration + tests first, then the app render.

1. **P1 — Media-Partners hub rebuild + logo API** (owner: "yes do")
   - Backend: confirm/add the partner-logo endpoint + CP upload (additive).
   - App: rebuild to KSA shell + 3-tab hub (`[gallery, partners, news]`,
     partners active) + 2-col partner grid with logo; RTL test; E2E; docs.
2. **P2 — News cards exact (948-3961)** — render thumbnail + date; verify card
   styling; RTL test; E2E; docs. (image endpoint only if missing.)
3. **P3 — Speaker CV + My-seat polish (908-2110 / 898-2873)** — verified
   per-element fixes only.
4. **P4 — Speaker photo API** (if owner approves) — additive field + endpoint +
   CP upload + app render across speaker list & CV.
5. **P5 — Live AI captions** (only if owner approves building the feature).
6. **P6 — Wider wave parity-check** (session detail / booth / sponsor / archive)
   — confirm scope, then RTL-verified fixes.

## Data still needed from the owner

- **Figma node-ids** for any page not listed above that must be exact (full
  list, so nothing is missed) — especially confirm the wider-wave four.
- **Backend gap decisions** (the ❓ rows): partner logo (real vs placeholder),
  speaker photo (now vs defer), live AI captions (build vs defer), news
  thumbnail/date confirmation.
- **CP scope**: for each new media field (partner logo, speaker photo), confirm
  the Control-Panel upload UI is in scope this pass.
- **A signed-in visitor account** (email + password) for live emulator
  verification of the auth-gated pages (or confirm you'll tap-test on device).

## Done so far this program (branch `feature/app-cp-api-split`, not pushed)

- `c67c339` D-435 restore face detection · `8b55717` speaker-list RTL ·
  `b2ea7e6` media/news tab RTL · `36a02b2` live language chip ·
  `2ba3d13` D-436 decision log. (All beneath the concurrent `1fea142` D-437.)
