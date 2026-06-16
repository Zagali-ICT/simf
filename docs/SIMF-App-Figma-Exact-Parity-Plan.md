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

### Wider re-skin wave — IN SCOPE (owner confirmed 2026-06-16)
| 9 Session detail 889-2450 · 10 Booth 922-2458 · 11 Sponsors 922-2824 · 12 Archive/History 925-3079 | task #93/#97/#98/#99 | 🟡 | parity-check each the same way (RTL-verified) — P6 |

## Backend API / field gaps to design (Figma = source of truth)

**Decisions locked (owner, 2026-06-16):** partner logo → **real uploaded logos
(full API + CP upload)**; live AI captions → **design the API surface + stub the
provider** (app renders captions when present; YouTube CC meanwhile); speaker
photo → **add the photo API now**; wider-wave four pages → **all included**.

| Gap | Frame | Current backend | Build (additive) | Decision |
|-----|-------|-----------------|---------------------|----------|
| **Media-partner logo** | 958-2246 | `PublicMediaPartnerItem` carries `logoRelativePath`; no image endpoint confirmed | `GET /app/media-partners/{id}/logo` (bytes) + CP upload; app renders logo, falls back to initials | ✅ **real logos** |
| **News thumbnail + date** | 948-3961 | `NewsListItem` has `imageRelativePath` + `publishedAt`; image endpoint likely exists | render both in `_NewsCard`; add the image endpoint only if missing | ✅ render |
| **Live AI captions / transcript** | 934-3450 | none — `LiveSession` has no transcript field | add a caption/transcript field + endpoint (`GET /app/programme/sessions/{id}/captions` or a field on the live slice); app renders when present; **provider integration stubbed/later** | ✅ **API surface now, provider later** |
| **Speaker photo** | 908-2110 / 908-1744 | no speaker photo URL field | `Speaker.PhotoRelativePath` + `GET /app/speakers/{id}/photo` + CP upload; app renders photo, falls back to initials/anchor | ✅ **add now** |

## P1 design notes (investigated 2026-06-16 — ready to build)

- **No migration needed.** `MediaPartner.LogoRelativePath` already exists on the
  entity, the public wire (`PublicMediaPartnerItem.logoRelativePath`, via
  `PublicMediaPartnerService` which also coalesces a linked `Contact.LogoRelativePath`),
  and the admin CRUD (`UpdateMediaPartnerRequest.LogoRelativePath`).
- **Gap 1 — serve endpoint:** there is **no** byte-serve for the logo. Mirror the
  media pattern: `GET /app/media-partners/{id}/logo` (anonymous, `image/*`),
  reading the stored asset by `LogoRelativePath`. Reference impl: the media
  item image at `SIMF.Api/Endpoints/Public/PublicMediaEndpoints.cs`
  (`/app/media/{id}/thumbnail|image`) + the asset storage in
  `SIMF.Api/Endpoints/Assets/AssetEndpoints.cs` (D-357). The public list DTO
  should also expose a `logoUrl` (server-relative) like media's `imageUrl`/
  `thumbnailUrl`, **append-only** (do not rename existing fields).
- **Gap 2 — app render:** the app card renders **initials** (`_PartnerCard._initials`).
  Build the logo via `{baseUrl}/app/media-partners/{id}/logo` and use
  `Image.network` with a spinner + initials fallback — exactly like
  `gallery_screen.dart:_Thumbnail` (lines ~381-479).
- **Gap 3 — CP upload:** confirm the CP media-partners add/edit page has a logo
  upload (it sends `LogoRelativePath`); if it only takes a text path, add an
  image-upload control that stores via the Assets endpoint and sets the path.
- **App rebuild:** replace the plain-`AppBar` `MediaPartnersScreen` with the
  `KsaPage` navy shell + the shared 3-tab hub (`[gallery, partners, news]`,
  partners active — reuse the now-fixed tab order) + the 2-col partner grid
  (gold rounded-square logo container + label), per frame 958-2246. Add an
  Arabic RTL position test + E2E file + PAGE-INDEX/per-page docs.

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
