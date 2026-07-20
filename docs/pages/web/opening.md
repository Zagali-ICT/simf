# Website "Opening ceremony" — `/programme/opening`

| | |
|--|--|
| **Route** | `/programme/opening` — Blazor SSR Razor page (static render) |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Audience** | Anyone (public) |
| **Auth** | None — anonymous |
| **Status** | ✅ Real — bilingual (AR RTL / EN LTR), responsive; static marketing content (no API) |
| **Source** | [`Opening.razor`](../../../src/Website/SIMF.Web/Components/Pages/Opening.razor) · [`Opening.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Opening.razor.cs) · [`LandingPageHero.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingPageHero.razor) · [`landing.css`](../../../src/Website/SIMF.Web/wwwroot/css/landing.css) (`ln-fsection--dark` / `ln-vcard--dark` / `ln-overview__grid` / `ln-numlist` + reused `ln-pghero` / `ln-vcard` / `ln-fsection`) |
| **Strings** | [`Strings.resx`](../../../src/Website/SIMF.Web/Resources/Strings.resx) / [`Strings.ar.resx`](../../../src/Website/SIMF.Web/Resources/Strings.ar.resx) (`Opening.*`; reuses `About.Hero.Subtitle` for the hero tagline) |
| **Data** | None — static; the highlights + participant segments live in `Opening.razor.cs` |
| **Figma** | KSA Maritime Forum — Opening Ceremony (Desktop AR), node `5867-22242` (hero `5867:22244`; overview `5867:28024`; participants `5867:28063`) |
| **E2E** | [`e2e/web-opening.md`](../../tests/e2e/web-opening.md) (`E2E-WOPN-*`) |

## 1. Purpose

The opening-ceremony / programme overview — the first page of the Programme
cluster. A **Blazor SSR** page on the shared `ln-` chrome: the interior photo-hero
(no breadcrumb — the Programme cluster omits it), a dark **overview** grid of the
forum's activity highlights, and a numbered list of the **target participant** segments.

## 2. Architecture

- **Rendering** — static SSR (no API). Shared chrome via `LandingShell`; the hero is
  the reusable `LandingPageHero`. **The Programme-cluster heroes have no breadcrumb**,
  so `LandingPageHero.Crumb` was made optional (when null, the breadcrumb `<nav>` is
  not rendered) — the About-cluster pages still pass a crumb and keep theirs. The hero
  carries the page's single `<h1>`.
- **Overview** (`ln-fsection ln-fsection--dark`, dark navy) — an 8-card grid of
  forum-activity highlights. It reuses the `ln-fsection` chrome via a `--dark` modifier
  (navy bg + white heading), with only the 4-up `ln-overview__grid` section-specific.
  The cards reuse the `ln-vcard` (values card) with a new `ln-vcard--dark` modifier
  (translucent-blue on navy, a blue icon circle, a gold label — matching the landing's
  `ln-tstat` dark-card treatment).
- **Target participants** (`ln-fsection` → `ln-numlist`) — a numbered `<ol>` of the
  segments the forum invites, two columns; each `ln-numitem` is a light card with the
  text and a gold number badge (`list-style: none`, so the visual badge is the only number).
- **Content** — highlights + participants are `Bilingual` records in `Opening.razor.cs`;
  section headers are `Opening.*` resx keys; the hero tagline reuses `About.Hero.Subtitle`.

## 3. Sections

| # | Section | Class | Content |
|---|---------|-------|---------|
| 1 | Interior hero (no breadcrumb) | `ln-pghero` (via `LandingPageHero`) | `<h1>` (`Opening.Hero.Title`) + subtitle (`About.Hero.Subtitle`) + venue + date pills |
| 2 | Overview | `ln-fsection ln-fsection--dark` → 8× `ln-vcard ln-vcard--dark` | Title (`Opening.Overview.Title`) + sub + 8 highlight cards (workshops, exhibition, sessions, three forum days, association meetings, side events, media, B2B) |
| 3 | Target participants | `ln-fsection` → `ln-numlist` (9× `ln-numitem`) | Title (`Opening.Participants.Title`) + sub + a numbered list of the nine participant segments |

## 4. Bilingual model (AR RTL / EN LTR)

- **Hero + section headers** → resx (`Opening.*` + reused `About.Hero.Subtitle`),
  following the `/culture` switch.
- **Card + list content** → `Bilingual` records resolved `.For(rtl)`.
- **Direction** — logical properties; the hero gradient keeps its `[dir=ltr]` flip; the
  number badge keeps `direction: ltr` so multi-digit numbers read correctly.

## 5. Responsive

`ln-overview__grid` steps **4 → 2 → 1** columns at ≤900 / ≤520px; `ln-numlist` steps
**2 → 1** columns at ≤720px; the hero block goes full-width below 720px. No horizontal
overflow at 1440 / 1024 / 768 / 390 (`scrollWidth == clientWidth` verified in both languages).

## 6. Verification (2026-07-18)

- **Build** — `dotnet build -c Release` 0 warnings / 0 errors.
- **Component tests** — `tests/SIMF.Web.Tests/OpeningPageTests.cs` (3, green):
  single-`<h1>` with **no** breadcrumb; the eight dark overview cards; the nine numbered
  participant items (badges 1..9).
- **Live render** — visually verified against Figma at **AR@1440** and **EN@1440**
  (correct RTL→LTR mirror): the dark overview cards (blue icon circle + gold label) and
  the numbered participant list. Console clean; no horizontal overflow. The About-cluster
  breadcrumb re-checked unchanged after the optional-`Crumb` change (its tests stay green).

## 7. Follow-ups (not blockers)

- **Participant count** — the Figma listed ten slots with the tenth duplicating the
  eighth; the page renders the **nine distinct** segments. Confirm the full list with the
  client (a real tenth segment can append to `Opening.razor.cs`).
- The "View all" button in the Figma participants header was an unwired placeholder and is
  omitted. Shared DRY follow-ups are tracked in [`about.md`](about.md) §7.

_Last reviewed:_ 2026-07-18 by Claude (Opening ceremony page — `ln-` Bootstrap SSR, Figma 5867-22242).
