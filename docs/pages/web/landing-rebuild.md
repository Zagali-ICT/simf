# Website marketing landing — Bootstrap rebuild — `/landing`

| | |
|--|--|
| **Route** | `/landing` (Blazor SSR Razor page; slated to take over `/` at cutover) |
| **Surface** | Website (public, anonymous) |
| **Audience** | Anyone (public marketing site) |
| **Auth** | None — anonymous |
| **Status** | ✅ Built — bilingual (AR RTL / EN LTR), responsive; awaiting owner sign-off for `/` cutover |
| **Source** | [`Landing.razor`](../../../src/Website/SIMF.Web/Components/Pages/Landing.razor) · [`Landing.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Landing.razor.cs) · [`LandingLayout.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingLayout.razor) · [`landing.css`](../../../src/Website/SIMF.Web/wwwroot/css/landing.css) · [`landing.js`](../../../src/Website/SIMF.Web/wwwroot/js/landing.js) |
| **Strings** | [`Strings.resx`](../../../src/Website/SIMF.Web/Resources/Strings.resx) / [`Strings.ar.resx`](../../../src/Website/SIMF.Web/Resources/Strings.ar.resx) (`Landing.*` keys) |
| **Figma** | KSA Maritime Forum — Home Page (Desktop AR/EN), node `5328:22998` |

## 1. Purpose

A from-Figma **Bootstrap 5 rebuild** of the public SIMF 2026 marketing landing,
delivered as a **Blazor SSR** Razor page (server-rendered plain HTML + Bootstrap,
**not** MudBlazor). It reproduces the full Figma home page — hero, intro +
threat-landscape marquee, participation stats, about, milestones, secondary-hero
CTA, five key-theme pillars, the forum programme, speakers collage, partners band,
sponsors carousel, news, discover-Saudi grid and footer.

It coexists with the existing static `wwwroot/index.html` landing (still at `/`)
until the owner approves the cutover; see [`landing.md`](landing.md) for the
static page it supersedes.

## 2. Architecture

- **Rendering** — static SSR (no interactive circuit). All interactivity is
  progressive vanilla JS in `landing.js` (page-loader fade, reveal-on-scroll,
  search panel, themes crossfade) plus Bootstrap's bundle for the mobile
  offcanvas. The page is fully readable/navigable with JS disabled.
- **Layout** — uses a minimal `LandingLayout` (no shared public `<nav>`); the
  landing renders its own full chrome. Bootstrap + `landing.css` are injected
  per-page via `<HeadContent>`, so they never touch the app's other pages.
- **Bootstrap** — local `wwwroot/lib/bootstrap/5.3.3`; the **RTL** or **LTR**
  stylesheet is chosen by culture (`CultureInfo.CurrentUICulture.IsRightToLeft`).
- **Scoping** — every landing style is scoped under `.landing` and prefixed
  `ln-`, so the globally-loaded app stylesheets (theme.tokens.css, simf-components)
  cannot collide with it and vice-versa. Design-token **values** mirror the Figma
  variable collection (verified: gold `#e8c060`, primary `#244a77`, Almarai font).
  One app-global style *does* reach the landing: `Routes.razor`'s
  `<FocusOnNavigate Selector="h1">` programmatically focuses the hero `<h1>` on
  load, triggering the global `h1:focus-visible` ring — suppressed with a scoped
  `.ln-hero__title:focus` override (the h1 is `tabindex=-1`, never a keyboard stop).
- **Hero** — the hero content is right-aligned (RTL) / start-aligned (EN) per Figma
  node `5328:23001`: title, subtitle, a description paragraph, and two info pills
  (venue + event dates). The pills reuse the sub-nav date string + secondnav icons.

## 3. Bilingual model (AR RTL / EN LTR)

- **Chrome + section headers** → resx `IStringLocalizer<Strings>` (`Landing.*`
  keys), so they follow the request culture and the existing `/culture` switch.
- **Content collections** (threat stats, participation counters, milestones,
  themes, sessions, partners, news, discover, footer links) → `static readonly`
  lists in `Landing.razor.cs`; each carries AR+EN via a `Bilingual(Ar, En)` record
  resolved with `.For(rtl)` in the view. This mirrors the backend feed's
  `field`/`field_en` convention (`SiteContentEndpoints`).
- **Direction** — `<html dir/lang>` is set in `App.razor`; the CSS is
  direction-agnostic via logical properties (`margin-inline`, `text-align:start`,
  `inset-inline`) with a few explicit `[dir="ltr"]` / `[dir="rtl"]` overrides for
  genuinely mirrored art (hero2 gradient/photo, milestone arrow, carousel chevrons).

## 4. Dynamic sections (the `@foreach` loops)

Repeated sections are driven by server-side `@foreach`/`@for` over the content
models — not hand-copied markup:

| Section | Model | Notes |
|---------|-------|-------|
| Threat marquee | `ThreatStats` (12) | rendered ×2 for the seamless CSS loop |
| Participation stats | `Stats` (4) | 2 rows |
| Milestones | `Milestones` (4) | last card = future edition |
| Themes | `Themes` (5) | crossfade bg + auto-rotate active card (`landing.js`) |
| Programme | `Sessions` (3) | displayed as "The Forum Programme"; navy day-cards tagged Day One/Two/Three (Figma `برنامج الملتقي`) |
| Partners band | `PartnerLogos` (4) | rendered ×4 for the seamless marquee |
| Sponsors | placeholder ×16 | marquee; real sponsor data is a follow-up |
| News | `News` (3) | |
| Discover | `DiscoverCards` (6) | 3-col → 2-col → 1-col responsive grid |
| Footer links | `FooterImportantLinks` (5) | external gov sites |

## 5. Responsive

Breakpoints verified: nav collapses to a Bootstrap **offcanvas** hamburger below
1100px; card rows wrap/stack below 1000px; the discover grid steps 3→2→1 columns
at 1000/640px; sub-nav drops weather+venue on mobile. No horizontal overflow at
1440 / 1024 / 768 / 390 (`scrollWidth == clientWidth` verified in both languages).

## 6. Follow-ups (not blockers)

- Live-data hydration: the static page's `/content/site` feed can be wired into
  these `@foreach` models later (sessions/speakers/news/sponsors) if live content
  is wanted on the SSR page.
- Sponsors + a few section descriptions use authored placeholder copy pending
  real content; the Figma stats frame shows 6 counters (2 are placeholder
  duplicates) — per owner decision (2026-07-13) this build keeps the **4**
  meaningful counters rather than padding to 6.
- Owner-confirmed Figma-parity pass (2026-07-13): the sessions section is
  relabelled to the Figma **programme** (`برنامج الملتقي` / "The Forum
  Programme"), day-tagged navy cards (Day One/Two/Three); the **footer** matches
  the Figma 426px footer — logo-only brand block, a `Last modified` line, real
  contact block retained. App-store badges are **intentionally omitted** (the
  SIMF app is not yet on the App/Play stores, so a badge would link nowhere).
- Almarai is self-hosted (woff2 under `wwwroot/lib/almarai`, CSP-safe). Known
  minor follow-up: the hero-font `<link rel="preload">` href is fingerprinted
  while the `@font-face src` url is not, so the browser can't match them and the
  700-weight file is fetched-but-unused on cold load — align the two URLs.

## 7. E2E

Catalogue: [`e2e/web-landing-rebuild.md`](../../tests/e2e/web-landing-rebuild.md)
(`E2E-WLB-*`).

_Last reviewed:_ 2026-07-13 by Claude (Bootstrap rebuild, bilingual AR/EN).
