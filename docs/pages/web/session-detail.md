# Website session detail — `/sessions/{id}`

| | |
|--|--|
| **Route** | `/sessions/{id:guid}` — Blazor SSR Razor page (static render) |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Audience** | Anyone (public) |
| **Auth** | None — anonymous |
| **Status** | ✅ Real — bilingual (AR RTL / EN LTR), responsive; live session data via `SimfPublicClient` |
| **Source** | [`SessionDetail.razor`](../../../src/Website/SIMF.Web/Components/Pages/SessionDetail.razor) · [`SessionDetail.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/SessionDetail.razor.cs) · [`LandingShell.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingShell.razor) · [`landing.css`](../../../src/Website/SIMF.Web/wwwroot/css/landing.css) (`ln-sess*` / `ln-tcard` / `ln-rcard` / `ln-docrow` / `ln-outcomes` + reused `ln-ico`/`ln-spkcard`) |
| **Strings** | [`Strings.resx`](../../../src/Website/SIMF.Web/Resources/Strings.resx) / [`Strings.ar.resx`](../../../src/Website/SIMF.Web/Resources/Strings.ar.resx) (`SessionDetail.*` keys) |
| **Data** | `GET /api/v1/app/programme/sessions/{id}` (anonymous) → `PublicSessionDetail`; `GET …/programme/sessions` for the related strip; `GET /content/sessions/{sessionId}/downloads/{presentationId}` (anonymous proxy) for file bytes |
| **Figma** | KSA Maritime Forum — Session Detail (Desktop AR), node `5991-85840` |
| **E2E** | [`e2e/web-session-detail.md`](../../tests/e2e/web-session-detail.md) (`E2E-WSDT-001..016`; the FR-702 live notice is WSDT-014..016) |

## 1. Purpose

The public **session detail** page for SIMF 2026 — one scheduled session's
overview, at-a-glance facts, key themes, speakers, related sessions and
downloadable materials — as a **Blazor SSR** page on the shared `ln-` chrome.
Reproduces Figma `5991-85840` and is bound to live data from the anonymous public
API. Reached from the agenda / related strips at `/sessions/{id}`.

## 2. Architecture

- **Rendering** — static SSR; the session is fetched server-side in
  `OnInitializedAsync` (`GetSessionAsync(Id)`), plus a best-effort related strip
  (`GetProgrammeSessionsAsync`, this session filtered out, first 3 by start). A
  null detail → the not-found state; a null agenda → an empty strip (never an
  error). All interactivity is the shared progressive `landing.js`.
- **Shared chrome** — wrapped in `LandingShell` (one shared nav/footer/head +
  `.landing` scope). One `<h1>` (the hero title) for `FocusOnNavigate`.
- **Reuse** — the speakers grid reuses the Speakers page's `ln-spkcard`; every
  glyph is a recolorable `ln-ico` mask; translucent fills use `color-mix` over
  design tokens (no raw colours). New `ln-sess*` / `ln-tcard` / `ln-rcard` /
  `ln-docrow` / `ln-outcomes` families live in `landing.css`.

## 3. Sections

| # | Section | Class | Data |
|---|---------|-------|------|
| 1 | Hero band | `ln-sesshero` | breadcrumb (static) · gold day chip (session weekday) · `<h1>` title · description lead · 4 gold-tinted pills (time/date/hall/category), event-local +03:00 |
| 2 | At-a-glance card | `ln-glance` | track (category) · language (`Session.Language`) · hall · capacity (`Seats.Capacity`) · live (`LiveStreamUrl != null`). Optional rows (track/language) omit when null. Below the rows, the optional **live notice** (`ln-glance__notice`, FR-702 — see §4a) |
| 3 | Overview | `ln-sessabout` | "Why this session matters" + the description |
| 4 | Key themes | `ln-tcard` grid | one gold-badged navy card per tagged theme: name + `Theme.Description`. Section omits when no themes |
| 5 | Speakers | `ln-spkcard` grid (reused) | photo (asset proxy) · name · gold role pill · gray country pin; empty-state text when none |
| 6 | Related sessions | `ln-rcard` grid | up to 3 other sessions, each a link to its `/sessions/{id}`. Omits when empty |
| 7 | Downloads + outcomes | `ln-docrow` / `ln-outcomes` | presentation files (public download links) + key-outcome bullets. Each omits when empty |

Not-found (`ln-sessmissing`) renders for an unknown / unpublished id.

## 4. Public downloads (owner decision 2026-07-15)

Session presentation files are **public** on the website. Each row links to the
same-origin proxy `/content/sessions/{sessionId}/downloads/{presentationId}`
([`SiteContentEndpoints`](../../../src/Website/SIMF.Web/Endpoints/SiteContentEndpoints.cs))
→ `SimfPublicClient.FetchSessionDownloadAsync` → the new **anonymous** API route
`GET /app/sessions/{sessionId}/downloads/{presentationId}`
([`PublicPresentationEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Public/PublicPresentationEndpoints.cs)).
The **session scope is the authorisation** — a `GetFileAsync(sessionId, presentationId)`
overload validates the presentation belongs to that session (both active) and
streams it as an attachment; a presentation id from another session 404s. This is
distinct from (and does not weaken) the signed-in `/app/presentations/{id}/file`
attendee route.

## 4a. Live notice (FR-702 — owner decision 2026-07-31, D-815)

When the session carries a **live notice** — free bilingual text an admin writes
on that session at `/admin/sessions` (`Session.LiveNotice` /
`Session.LiveNoticeArabic`, `nvarchar(512)`, on the wire as `liveNotice` /
`liveNoticeArabic`) — the at-a-glance aside renders it under its rows as a single
`<p class="ln-glance__notice" role="note">`, picked with the same
`PickOrNull(...)` fallback every other bilingual value on this page uses. When
both languages are blank the element is not emitted at all: no empty box, no
reserved space.

**It restricts nothing.** SIMF-FDS-007 §5.1 originally specified FR-702 as a
Riyadh-region restriction under which an attendee outside the region would see a
notice *instead of* the stream. The owner reversed that — *"No restriction, this
is only notification and be added to session."* This page therefore performs no
region check, no location lookup and no gating of any kind; the at-a-glance
**Live** row still reports the session's live feed exactly as before while the
notice is displayed. The styling is deliberately calm — the `--gold-light`
informational fill from `landing.css`, not the alert/danger register — so it
reads as information, not a barrier.

## 5. Bilingual model (AR RTL / EN LTR)

Title / description / hall / theme name+description / speaker name / outcome /
language use the Arabic-preferred-in-RTL `Pick(...)` helper (fallback to the other
language). Chrome + section labels come from `IStringLocalizer<Strings>`
(`SessionDetail.*`). Direction-agnostic CSS (logical properties); the at-a-glance
sidebar sits on the reading-start side in both directions.

## 6. Data model (Phases A–C)

The page consumes fields added additively for this feature (append-only,
mobile-wire-safe): `Theme.Description` (already existed, now surfaced in
`PublicSessionTheme`), `Session.Language` + a `SessionOutcome` table (new,
migration `20260715001703`), and `PublicSessionDetail.Outcomes` / `.Language` /
`.Downloads` + `PublicSessionSpeaker.HasPhotoAsset`. `Session.Language` + the
outcomes are editable in the CP session editor (see
[`cp/admin-sessions.md`](../cp/admin-sessions.md)); downloads reuse the existing
`SpeakerPresentation` files. **2026-07-31 (D-815):** `PublicSessionDetail` also
appends `LiveNotice` / `LiveNoticeArabic` for §4a — append-only per D-219, so an
older client simply ignores them, and they are carried on the **detail** only
(never on `PublicSessionListItem`, which has no live fields at all).

## 7. Responsive

The themes + related grids step **3 → 2 → 1** columns at 1100 / 860px; the
overview and downloads/outcomes two-column grids stack at 900px; the hero tightens
at 860px. No horizontal overflow at 1440 / 1024 / 768 / 390 in both languages.

## 8. Verification (2026-07-15)

- **Build** — Website + API + CP + Infrastructure `dotnet build` 0 warnings / 0 errors.
- **Component tests** — `tests/SIMF.Web.Tests/SessionDetailPageTests.cs` (5):
  all-seven-sections render (with the download proxy URL), not-found, graceful
  omission, and (2026-07-31) the FR-702 live notice rendering when authored +
  omitting when only blank text is stored. The three original cases were verified
  green on 2026-07-15; the two notice cases are covered by this branch's suite run,
  not by that dated pass.
- **Live render (prod data)** — rendered `/sessions/{id}` at **AR@1440**, **EN@1440**
  and **mobile-390**: hero (gold chip, gold pill icons), at-a-glance (RTL/LTR mirror
  correct), overview and related strip all render; console clean; no horizontal
  overflow. The theme/speaker/outcome/download sections gracefully omit against prod
  (no seeded data yet) and are covered with full mock data by the component test.

## 9. Follow-ups (not blockers)

- The migration (`20260715001703`) + seeded outcomes/language must be deployed to
  populate those sections on the live site; the page degrades gracefully until then.
- The hero uses a navy gradient (no per-session hero image field); a future
  `Session` banner asset could back it.

_Last reviewed:_ 2026-07-31 by Claude (FR-702 — §4a live notice on the at-a-glance card: informational text shown with the stream, no region check anywhere; owner decision D-815).

_Prior:_ 2026-07-15 by Claude (Session Detail — `ln-` Bootstrap SSR, Figma 5991-85840).
