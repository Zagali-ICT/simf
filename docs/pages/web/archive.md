# Website "Forum archive" — `/archive`

| | |
|--|--|
| **Route** | `/archive` — Blazor SSR Razor page (static render, live-data read) |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Audience** | Anyone (public) |
| **Auth** | None — anonymous |
| **Status** | ✅ Real — bilingual, responsive; **live** archive data (headline counters + past-edition cards) with a static fallback |
| **Source** | [`Archive.razor`](../../../src/Website/SIMF.Web/Components/Pages/Archive.razor) + [`Archive.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Archive.razor.cs) · [`LandingPageHero.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingPageHero.razor) · [`landing.css`](../../../src/Website/SIMF.Web/wwwroot/css/landing.css) (reused `ln-pghero` / `ln-stats` / `ln-sessions` / `ln-speakers` / `ln-miles`; new `ln-gallery` + `ln-miles--wrap`) |
| **Strings** | [`Strings.resx`](../../../src/Website/SIMF.Web/Resources/Strings.resx) / [`Strings.ar.resx`](../../../src/Website/SIMF.Web/Resources/Strings.ar.resx) (`Archive.*`; session cards reuse `Landing.Sessions`, collage reuses `Landing.Speakers.*`) |
| **Data** | **Live** — `SimfPublicClient.GetArchiveAsync()` (`PublicArchive` edition list) drives the headline counters + the past-edition cards; server-side SSR. Empty/hidden/unreachable → static `Landing.Milestones` fallback. The photo gallery + session titles + past speakers are static/reused (see §7). |
| **Figma** | KSA Maritime Forum — Archive (Desktop AR), node `5840-27997` |
| **E2E** | [`e2e/web-archive.md`](../../tests/e2e/web-archive.md) (`E2E-WAR-*`) |

## 1. Purpose

The forum's **archive** — headline numbers, highlights, session titles, past speakers
and the past editions of the Saudi International Maritime Forum. A **Blazor SSR** page
on the shared `ln-` chrome that reads the live archive edition list server-side and
falls back gracefully when the archive is hidden or unreachable.

## 2. Architecture

- **Rendering** — static SSR; the counters + edition cards are read from the anonymous
  public API during pre-render (`Archive.razor.cs`), like `Speakers.razor`. Shared
  chrome via `LandingShell`; the hero is the reusable `LandingPageHero` (no breadcrumb)
  and carries the page's single `<h1>`.
- **Live data + fallback** — `GetArchiveAsync()` returns the archive edition list
  (newest-first). When it is empty (archive-visibility toggle off) or null
  (unreachable), the page falls back to the landing's static `Landing.Milestones` past
  editions (reversed to newest-first) + a default headline triple, so it never blanks.
- **Reuse** — the counters reuse `ln-stats` (navy band), the session cards reuse
  `ln-sessions`/`ln-scard` + `Landing.Sessions`, the past speakers reuse `ln-speakers`
  (collage + a link to `/speakers`), and the edition cards reuse `ln-miles`/`ln-mcard`.
  Only the photo grid (`ln-gallery`) is new CSS, plus a page-scoped `ln-miles--wrap`
  modifier so the variable-length live edition list wraps (the shared band is
  dimensioned for exactly four cards).

## 3. Sections

| # | Section | Class | Content |
|---|---------|-------|---------|
| 1 | Interior hero (no breadcrumb) | `ln-pghero` (via `LandingPageHero`) | `<h1>` (`Archive.Hero.Title`) + subtitle + venue + date pills |
| 2 | Headline counters (navy) | `ln-stats` → `ln-stat` × 3 | Speakers / Attendees / Sessions from the latest live edition (static fallback) |
| 3 | Photos & video | `ln-gallery` → `ln-gallery__item` × 6 | Static highlights grid (real forum photos); the live media feed + a video player are a follow-up (§7) |
| 4 | Session titles (navy) | `ln-sessions` → `ln-scard` × 3 | Reused `Landing.Sessions` cards, each linking to `/programme/sessions` |
| 5 | Past speakers (navy) | `ln-speakers` | Title + lead + the speakers collage + a "view all" link to `/speakers` |
| 6 | Past editions | `ln-miles ln-miles--wrap` → `ln-mcard` | **Live** edition cards from `GetArchiveAsync` (photo + date + title + summary), static `Milestones` fallback |

## 4. Bilingual model (AR RTL / EN LTR)

- **Chrome/section copy** → `Archive.*` + reused `Landing.*` resx; all follow the
  `/culture` switch.
- **Edition cards** → `Bilingual` records built from the live edition's AR/EN title +
  summary + date label (`.For(rtl)`), or the landing's `Milestones` on fallback.
- **Direction** — the reused bands are direction-agnostic (`text-align: start`); the
  hero photo sits inline-end.

## 5. Responsive

The gallery grid collapses 3→2→1; the session cards + edition cards wrap
(`ln-miles--wrap` gives the live band `flex-wrap` at every width so any number of
editions reflows). Section padding uses `clamp(16px, 5.5vw, 80px)`. No horizontal
overflow at 1440 / 1280 / 1024 / 768 / 390 (`scrollWidth == clientWidth` verified in
both languages; zero elements exceed the viewport).

## 6. Verification (2026-07-19)

- **Build** — `dotnet build -c Release` 0 warnings / 0 errors.
- **Component tests** — `tests/SIMF.Web.Tests/ArchivePageTests.cs` (3, green): the
  static bands + single `<h1>`; live edition cards newest-first with stats from the
  latest edition; the fallback to the static Milestones when the archive is unreachable.
- **Live render** — visually verified at **AR@1440** and **EN@1440** with **live prod
  data** (four editions SIMF 2022–2025 + real counters): the hero, the navy counters,
  the photo grid, the session cards, the speakers collage and the wrapping edition
  band. Console clean; no horizontal overflow; the edition band wraps at 1280/1024.

## 7. Follow-ups — deferred scope & flags

1. **Live media gallery + video (deferred).** The Figma's "الصور والفيديو" section is
   rendered as a **static highlights grid** of real forum photos. The public API
   exposes **no per-edition detail** (the `PublicArchiveEditionDetail` gallery /
   session-titles / past-speakers lists exist in the contracts but are not on the
   public `SimfPublicClient`), and there is no live-video provider decision. When a
   public archive-media endpoint + a video provider land, wire this section live.
2. **Session titles + past speakers are reused static content.** For the same reason,
   the session-title cards reuse `Landing.Sessions` and the past speakers reuse the
   landing collage + a link to `/speakers`, rather than the specific past-edition
   session titles / speakers. Wire live when the archive-detail endpoint exists.
3. **Edition cover images.** `MapEdition` uses the edition's `CoverImageRelativePath`
   when present, else a reused milestone photo. The public `PublicArchiveEdition`
   contract carries **no `HasCover` flag / servable asset URL** (unlike
   `PublicSpeakerSummary.HasPhotoAsset` + the `/content/assets/SpeakerPhoto/...`
   proxy), so a CP-uploaded `ArchiveCover` asset cannot be resolved here yet — the
   card falls back to the milestone photo. An additive `HasCover` field on the public
   contract (+ a `/content/assets/ArchiveCover/{id}/image` proxy) would let this render
   the real cover.
4. **Headline counters reflect the latest edition only** (honestly labelled
   Speakers / Attendees / Sessions), and the hero reuses the shared cluster photo —
   both are interim choices, swappable when archive-specific assets/aggregates exist.

_Last reviewed:_ 2026-07-19 by Claude (Forum archive page — `ln-` Bootstrap SSR, Figma 5840-27997; live archive data + static fallback).
