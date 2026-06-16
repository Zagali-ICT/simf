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
| 7 | Media partners | 958-2246 | `features/media_partners/media_partners_screen.dart` | ✅ | **DONE (P1)** — rebuilt to KSA navy shell + shared 3-tab hub (partners active) + 2-col partner grid; renders the **real uploaded logo** via the existing anonymous D-357 route `GET /app/assets/MediaPartnerLogo/{id}/image` (no new endpoint), initials fall-back. Arabic RTL position test + E2E + docs. |
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
| **Media-partner logo** | 958-2246 | **DONE (P1)** — served by the existing anonymous D-357 route `GET /app/assets/MediaPartnerLogo/{id}/image`; CP upload already ships (`MediaPartnerAddEdit.razor` `SimfImageUpload Category="MediaPartnerLogo"`) | App renders the logo from that route, initials fall-back. **No new endpoint / DTO field / migration** — reuse the unified media-asset pipeline (the controlled doc `docs/dev/SIMF-Media-Asset-The-One-Way.md` forbids a per-entity duplicate). | ✅ **real logos (D-357 reuse)** |
| **News thumbnail + date** | 948-3961 | `NewsListItem` has `imageRelativePath` + `publishedAt`; image endpoint likely exists | render both in `_NewsCard`; add the image endpoint only if missing | ✅ render |
| **Live AI captions / transcript** | 934-3450 | none — `LiveSession` has no transcript field | add a caption/transcript field + endpoint (`GET /app/programme/sessions/{id}/captions` or a field on the live slice); app renders when present; **provider integration stubbed/later** | ✅ **API surface now, provider later** |
| **Speaker photo** | 908-2110 / 908-1744 | no speaker photo URL field | `Speaker.PhotoRelativePath` + `GET /app/speakers/{id}/photo` + CP upload; app renders photo, falls back to initials/anchor | ✅ **add now** |

## P1 design notes — AS-BUILT (corrected 2026-06-16)

> **Correction (owner-approved):** the original P1 notes proposed a *new*
> `GET /app/media-partners/{id}/logo` endpoint + a `logoUrl` DTO field. That
> contradicts the controlled doc `docs/dev/SIMF-Media-Asset-The-One-Way.md`
> (authority D-357 — "the single mechanism… do **not** add a new per-entity
> upload column, controller, or storage path"), and the byte-serve **already
> exists**. So P1 reuses the D-357 pipeline; no backend/CP/migration change.

- **No migration, no new endpoint, no DTO change.** The media-partner logo is a
  registered `AssetCategory.MediaPartnerLogo` (D-357). The bytes are already
  served **anonymously** at `GET /app/assets/MediaPartnerLogo/{ownerId}/image`
  (`SIMF.Api/Endpoints/Assets/AssetEndpoints.cs` → `PublicFetchAssetEndpoint`),
  gated for write by `MediaPartners.Edit` via `AssetPermissionRegistry`, and the
  owner-name resolves in `AssetService.ResolveOwnerNamesAsync`.
- **CP upload already ships.** `MediaPartnerAddEdit.razor` carries
  `<SimfImageUpload Category="MediaPartnerLogo" OwnerId="@Initial.Id" />`
  (edit-only) and `MediaPartnerViewDelete.razor` shows the thumb — no CP change.
- **The legacy `MediaPartner.LogoRelativePath` free-text column is NOT the byte
  source** (nothing serves bytes from it; it is disconnected from the D-357
  `Asset` row). It stays on the public wire untouched (append-only), but the app
  ignores it for the logo image.
- **App render (built):** the card builds `{baseUrl}/app/assets/MediaPartnerLogo/{id}/image`
  and uses `Image.network` with a spinner + an initials-on-gold fall-back
  (mirrors `gallery_screen.dart:_Thumbnail`). The wire carries no asset-presence
  flag, so every card attempts the asset URL and falls back on 404 — correct
  given the wire; a future append-only `hasLogo` flag could avoid the 404s.
- **App rebuild (built):** `MediaPartnersScreen` → `KsaPage` navy shell + the
  shared 3-tab hub (`[gallery, partners, news]`, partners active — mirrors
  `news_screen.dart:_MediaTabs`, same frame) + the 2-col partner grid per frame
  958-2246. Deterministic Arabic RTL tab-order position test + E2E file +
  PAGE-INDEX updated.

## Execution plan (sequence)

Each item is its own plan→build→verify→commit cycle; backend gaps get an
additive migration + tests first, then the app render.

1. **P1 — Media-Partners hub rebuild + real logo** ✅ **DONE** (owner approved
   D-357 reuse 2026-06-16)
   - Backend/CP: **no change** — the anonymous `GET /app/assets/MediaPartnerLogo/{id}/image`
     serve + the CP upload already ship (D-357). No new endpoint / migration.
   - App: rebuilt to KSA shell + 3-tab hub (`[gallery, partners, news]`,
     partners active) + 2-col partner grid rendering the real logo; Arabic RTL
     position test; E2E; PAGE-INDEX/plan docs.
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
