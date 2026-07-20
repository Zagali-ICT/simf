# Website "Plenary sessions" — `/programme/sessions`

| | |
|--|--|
| **Route** | `/programme/sessions` — Blazor SSR Razor page (static render) |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Audience** | Anyone (public) |
| **Auth** | None — anonymous |
| **Status** | ✅ Real — bilingual, responsive; static (reuses the landing's session cards). The ln-styled successor to the old MudBlazor `/programme` (see §7) |
| **Source** | [`Plenary.razor`](../../../src/Website/SIMF.Web/Components/Pages/Plenary.razor) · [`LandingPageHero.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingPageHero.razor) · [`landing.css`](../../../src/Website/SIMF.Web/wwwroot/css/landing.css) (reused `ln-pghero` / `ln-sessions` / `ln-scard`) · [`Landing.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Landing.razor.cs) (`Landing.Sessions`) |
| **Strings** | [`Strings.resx`](../../../src/Website/SIMF.Web/Resources/Strings.resx) / [`Strings.ar.resx`](../../../src/Website/SIMF.Web/Resources/Strings.ar.resx) (`Plenary.*`; reuses `About.Hero.Subtitle`) |
| **Data** | None — static; the three sessions reuse the shared `Landing.Sessions` list (single-sourced) |
| **Figma** | KSA Maritime Forum — Plenary Sessions (Desktop AR), node `5867-22842` (hero `5867:22844`; cards `5867:28244`) |
| **E2E** | [`e2e/web-plenary.md`](../../tests/e2e/web-plenary.md) (`E2E-WPLN-*`) |

## 1. Purpose

The forum's **plenary sessions** — the second page of the Programme cluster. A
**Blazor SSR** page on the shared `ln-` chrome: the interior photo-hero (no
breadcrumb), then the three plenary-session day cards (Day 1–3).

## 2. Architecture

- **Rendering** — static SSR (no API). Shared chrome via `LandingShell`; the hero is
  the reusable `LandingPageHero` (no breadcrumb — the Programme cluster omits it) and
  carries the page's single `<h1>`.
- **Sessions** — the section **reuses the landing's `ln-sessions` / `ln-scard` family
  verbatim** (navy card, image, gold day badge, white title, light description, a
  transparent CTA button), driven by the shared **`Landing.Sessions`** list — so the
  three plenary sessions (Day 1: energy supply chains; Day 2: logistics infrastructure;
  Day 3: seabed digital domain) are single-sourced with the landing's Programme section.
- **CTA** — each card's "Explore the sessions" button (`Plenary.Card.Button`) is an
  `<a class="ln-scard__btn" href="/programme">` linking to the live session agenda.
- **Content** — section title/subtitle + CTA are `Plenary.*` resx keys; the hero tagline
  reuses `About.Hero.Subtitle`. No new CSS, no code-behind, no new assets.

## 3. Sections

| # | Section | Class | Content |
|---|---------|-------|---------|
| 1 | Interior hero (no breadcrumb) | `ln-pghero` (via `LandingPageHero`) | `<h1>` (`Plenary.Hero.Title`) + subtitle (`About.Hero.Subtitle`) + venue + date pills |
| 2 | Plenary sessions | `ln-sessions` → 3× `ln-scard` | Title (`Plenary.Section.Title`) + sub + the three `Landing.Sessions` day cards, each with an "Explore the sessions" CTA → `/programme` |

## 4. Bilingual model (AR RTL / EN LTR)

- **Hero + section headers + CTA** → resx (`Plenary.*` + reused `About.Hero.Subtitle`),
  following the `/culture` switch.
- **Card content** → `Landing.Sessions` `.Tag` / `.Title` / `.Text` `.For(rtl)`.
- **Direction** — the reused `ln-sessions` / `ln-scard` are direction-agnostic; the RTL
  card order (Day 1 on the right → Day 3 on the left) matches the Figma.

## 5. Responsive

The reused `ln-sessions__row` wraps the three cards on narrow viewports (per the
landing's rules); the hero block goes full-width below 720px. No horizontal overflow at
1440 / 1024 / 768 / 390 (`scrollWidth == clientWidth` verified in both languages).

## 6. Verification (2026-07-18)

- **Build** — `dotnet build -c Release` 0 warnings / 0 errors.
- **Component tests** — `tests/SIMF.Web.Tests/PlenaryPageTests.cs` (3, green):
  single-`<h1>` with no breadcrumb; the three reused `Landing.Sessions` cards; each CTA
  links to `/programme`.
- **Live render** — visually verified against Figma at **AR@1440** and **EN@1440**
  (correct RTL→LTR mirror): the three navy day cards (naval image, gold day badge, white
  title, transparent CTA), identical to the landing's Programme section. Console clean;
  no horizontal overflow.

## 7. Follow-ups (supersession — deferred)

The plan positions this page as superseding the old MudBlazor **`/programme`**
(`Programme.razor`, a live session agenda). This changeset adds the new ln-styled page
**additively** and links its CTAs to the old `/programme` live agenda; **retiring or
redirecting `/programme` + its `ProgrammePageTests` is deferred** (it touches
shipped code and is the owner's call). Once retired, update the CTA target and the
`PAGE-INDEX` / E2E rows to mark the old page superseded. Ideally each day card would
deep-link to its `/sessions/{id}` detail page once the static `Landing.Sessions` list
carries real session ids.

_Last reviewed:_ 2026-07-18 by Claude (Plenary sessions page — `ln-` Bootstrap SSR, Figma 5867-22842).
