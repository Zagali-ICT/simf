# Website "Key themes" — `/about/themes`

| | |
|--|--|
| **Route** | `/about/themes` — Blazor SSR Razor page (static render + progressive JS) |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Audience** | Anyone (public) |
| **Auth** | None — anonymous |
| **Status** | ✅ Real — bilingual (AR RTL / EN LTR), responsive; static content (no API); interactive theme explorer (progressive JS) |
| **Source** | [`Themes.razor`](../../../src/Website/SIMF.Web/Components/Pages/Themes.razor) · [`Themes.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Themes.razor.cs) · [`LandingPageHero.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingPageHero.razor) · [`landing.css`](../../../src/Website/SIMF.Web/wwwroot/css/landing.css) (`ln-themex` + reused `ln-pghero` / `ln-fsection`) · [`landing.js`](../../../src/Website/SIMF.Web/wwwroot/js/landing.js) (`initThemeTabs`) |
| **Strings** | [`Strings.resx`](../../../src/Website/SIMF.Web/Resources/Strings.resx) / [`Strings.ar.resx`](../../../src/Website/SIMF.Web/Resources/Strings.ar.resx) (`Themes.*` + reused `PageHero.Home` / `About.Breadcrumb`) |
| **Data** | None — static. The five themes' title + description **reuse the landing's `Landing.Themes`** (single-sourced); this page adds only the ordinal tab labels (`Themes.razor.cs`) |
| **Figma** | KSA Maritime Forum — Key Themes (Desktop AR), node `5865-35289` (hero `5865:35291`; explorer `5963:39940`; TOC-item component `5002:167043`) |
| **E2E** | [`e2e/web-themes.md`](../../tests/e2e/web-themes.md) (`E2E-WTHM-*`) |

## 1. Purpose

The forum's **key themes** overview — the third page of the About cluster. A
**Blazor SSR** page on the shared `ln-` chrome: the interior photo-hero, then an
**interactive theme explorer** where a vertical tab list (Theme 1–5) selects one
of the forum's five strategic themes, whose title + description show beside an image.

## 2. Architecture

- **Rendering** — static SSR; the explorer is enhanced by progressive vanilla JS
  (`landing.js` → `initThemeTabs`). **Graceful degradation:** the single-panel view
  is keyed on an `is-enhanced` class that `initThemeTabs` adds **only after it has
  wired the tabs** — so if the JS is disabled *or fails to load*, every theme panel
  renders stacked (content is never hidden) and the tab list is hidden. Once enhanced,
  the tabs drive which single panel is visible (default: the first).
- **Shared chrome + hero** — `LandingShell` + the reusable `LandingPageHero` with a
  **3-level breadcrumb** (Home / About / Key themes). The hero carries the page's
  single `<h1>`.
- **Explorer** (`ln-themex`) — a flex row: the image (`ln-themex__media`, inline-END),
  the panels (`ln-themex__panels`, one per theme), and the nav (`ln-themex__nav`:
  the "On this page" label + the vertical `ln-themex__tabs`). The active tab shows a
  3px gold selector bar on its inline-start edge; the active panel shows its theme's
  title + description. `initThemeTabs` toggles `is-active` on click and keeps
  `aria-selected` in sync.
- **Content reuse** — the five themes are `Landing.Themes` (the landing's own theme
  list), so the wording is single-sourced; `Themes.razor.cs` only adds the short
  ordinal tab labels ("Theme 1" … "Theme 5"). The two index-aligned lists are paired
  with `Math.Min(...)` so a future length change can't index out of range.

## 3. Sections

| # | Section | Class | Content |
|---|---------|-------|---------|
| 1 | Interior hero | `ln-pghero` (via `LandingPageHero`) | Breadcrumb Home / About / Key themes, `<h1>` (`Themes.Hero.Title`), subtitle, venue + date pills |
| 2 | Theme explorer | `ln-fsection` → `ln-themex` | Title (`Themes.Section.Title`) + sub (`Themes.Section.Sub`); then the explorer: image + 5 theme panels (reused `Landing.Themes` title/desc) + the "On this page" label (`Themes.OnThisPage`) + 5 vertical tabs |

## 4. Bilingual model (AR RTL / EN LTR)

- **Hero + section headers + tab labels** → resx / `Bilingual` resolved for the
  active culture, following the `/culture` switch.
- **Theme panels** → `Landing.Themes` `.Title` / `.Desc` `.For(rtl)`.
- **Direction** — logical properties throughout; the hero gradient keeps its explicit
  `[dir=ltr]` flip and the tab selector bar uses `inset-inline-start`.

## 5. Responsive

Below 860px the explorer stacks (image on top, then panels, then the tabs become a
horizontal wrap with the selector bar under the active tab). The hero block goes
full-width below 720px. No horizontal overflow at 1440 / 1024 / 768 / 390
(`scrollWidth == clientWidth` verified in both languages).

## 6. Verification (2026-07-15)

- **Build** — `dotnet build -c Release` 0 warnings / 0 errors.
- **Component tests** — `tests/SIMF.Web.Tests/ThemesPageTests.cs` (3, green):
  single-`<h1>` + 3-level breadcrumb; 5 tabs + 5 panels with the first active + the
  ARIA wiring; the panels reuse `Landing.Themes` while the tabs use ordinal labels.
- **Live render** — visually verified against Figma at **AR@1440** and **EN@1440**
  (correct RTL→LTR mirror). **Interaction:** clicking tab 3 switches the active tab
  (gold bar) + shows theme 3's panel + updates `aria-selected`. **No-JS fallback:**
  with `is-enhanced` absent (JS off / not run), all five panels are visible and the
  tab list is hidden.
  Console clean; no horizontal overflow. The shared `landing.js` change was
  re-verified against the landing (guarded `initThemeTabs` early-returns — no error).

## 7. Follow-ups (not blockers)

- **Explorer image** — the explorer shows a single shared image (the Figma defines one
  image for the selected theme); a per-theme image set is a trivial extension (swap the
  `src` in `initThemeTabs`) if the client supplies distinct theme images.
- **Tab labels** — ordinal ("Theme 1" … "Theme 5"), matching the Figma's narrow tabs;
  the full theme title shows in the panel.
- Shared DRY follow-ups (interior-hero unification, etc.) are tracked in
  [`about.md`](about.md) §7.

_Last reviewed:_ 2026-07-15 by Claude (Key themes page — `ln-` Bootstrap SSR, Figma 5865-35289).
