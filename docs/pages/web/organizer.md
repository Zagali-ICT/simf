# Website "The organizer" — `/about/organizer`

| | |
|--|--|
| **Route** | `/about/organizer` — Blazor SSR Razor page (static render) |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Audience** | Anyone (public) |
| **Auth** | None — anonymous |
| **Status** | ✅ Real shell — bilingual, responsive; static content (no API). Content is REAL (MOD + RSNF) filling a placeholder Figma frame — see §7 |
| **Source** | [`Organizer.razor`](../../../src/Website/SIMF.Web/Components/Pages/Organizer.razor) · [`Organizer.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Organizer.razor.cs) · [`LandingPageHero.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingPageHero.razor) · [`landing.css`](../../../src/Website/SIMF.Web/wwwroot/css/landing.css) (`ln-orgcard` + reused `ln-pghero` / `ln-fsection`) |
| **Strings** | [`Strings.resx`](../../../src/Website/SIMF.Web/Resources/Strings.resx) / [`Strings.ar.resx`](../../../src/Website/SIMF.Web/Resources/Strings.ar.resx) (`Organizer.*` + reused `PageHero.Home` / `About.Breadcrumb`) |
| **Data** | None — static (the two organiser cards live in `Organizer.razor.cs`) |
| **Figma** | KSA Maritime Forum — The Organizer (Desktop AR), node `5865-38003` (hero `5865:38005`; cards `5866:39706`) |
| **E2E** | [`e2e/web-organizer.md`](../../tests/e2e/web-organizer.md) (`E2E-WORG-*`) |

## 1. Purpose

The forum's **organising bodies** — the fourth page of the About cluster. A
**Blazor SSR** page on the shared `ln-` chrome: the interior photo-hero, then a
section presenting the two organising bodies (Ministry of Defense + Royal Saudi
Naval Forces) as centred cards.

## 2. Architecture

- **Rendering** — static SSR (no API). Shared chrome via `LandingShell`; the hero
  is the reusable `LandingPageHero` with a **3-level breadcrumb** (Home / About /
  The organizer). The hero carries the page's single `<h1>`.
- **Organiser cards** (`ln-orgcard`) — two centred white cards on the `ln-fsection`
  chrome, each a logo + a bilingual name + a description. The Ministry-of-Defense
  card renders its colour emblem as an `<img>`; the Royal-Saudi-Naval-Forces card
  recolours the forum mark to **navy** via a CSS `mask` (the mark ships white for the
  dark nav and would be invisible on the white card — same recolour idiom as `ln-ico`).
- **Content** — the two bodies are `Bilingual` records in `Organizer.razor.cs`;
  section headers are `Organizer.*` resx keys.

## 3. Sections

| # | Section | Class | Content |
|---|---------|-------|---------|
| 1 | Interior hero | `ln-pghero` (via `LandingPageHero`) | Breadcrumb Home / About / The organizer, `<h1>` (`Organizer.Hero.Title`), subtitle, venue + date pills |
| 2 | Organising bodies | `ln-fsection` → `ln-orgs` (2× `ln-orgcard`) | Title (`Organizer.Section.Title`) + sub (`Organizer.Section.Sub`) + two cards: Ministry of Defense (colour emblem) + Royal Saudi Naval Forces (navy-masked forum mark), each with a name + description |

## 4. Bilingual model (AR RTL / EN LTR)

- **Hero + section headers** → resx (`Organizer.*` + reused `PageHero.Home` /
  `About.Breadcrumb`), following the `/culture` switch.
- **Card content** → `Bilingual` records resolved `.For(rtl)`.
- **Direction** — logical properties; the hero gradient keeps its `[dir=ltr]` flip.

## 5. Responsive

The two cards sit side by side and stack to one column below 720px (card padding
tightens). The hero block goes full-width below 720px. No horizontal overflow at
1440 / 1024 / 768 / 390 (`scrollWidth == clientWidth` verified in both languages).

## 6. Verification (2026-07-22)

- **Build** — `dotnet build -c Release` 0 warnings / 0 errors.
- **Component tests** — `tests/SIMF.Web.Tests/OrganizerPageTests.cs` (3, green):
  single-`<h1>` + 3-level breadcrumb; the two cards with real MOD/RSNF content;
  the two logo treatments (colour `<img>` emblem vs the navy-masked forum mark).
  The masked-mark test now pins the **root-relative** `--logo` url as a regression
  guard (see the fix below).
- **Live render** — visually verified against Figma at **AR@1440** and **AR@390**
  (mobile stacks to one column). The MOD emblem renders in colour; the masked forum
  mark renders navy and visible on the white card. Console clean; no horizontal overflow.
- **Fix (2026-07-22)** — the RSNF masked mark had been rendering **blank**: the
  `--logo:url('assets/…')` custom property resolved against the stylesheet base
  (`/css/assets/…` → 404), not `<base href>`, on the nested `/about/organizer`
  route. Made the url **root-relative** (`/assets/…`) so the mask loads on any route.

## 7. Follow-ups — content flagged (this was a placeholder Figma frame)

The Figma frame `5865-38003` is a **partially-templated placeholder**: one card
showed the developer's own "STARTIME" logo, both cards duplicated the same body
text, and the section subtitle was lorem-style ("هنا يمكنك إضافة وصف مختصر…").
This page instead fills the shell with the **real** organising bodies per the
forum's "MOD.RSNF" identity — **confirm the exact copy + logos with the client**:

- **RSNF logo** — the Royal-Saudi-Naval-Forces card currently recolours the SIMF
  **forum mark** to navy as a stand-in (no distinct RSNF emblem asset exists in the
  repo). Swap in the real RSNF emblem when supplied (drop `LogoMasked`, point `Logo`
  at the asset).
- **Copy** — the MOD patronage text is taken from the Figma; the RSNF description is
  authored from the patronage wording — confirm both with the client.

_Last reviewed:_ 2026-07-22 by Claude (The organizer page — `ln-` Bootstrap SSR, Figma 5865-38003; RSNF masked-logo root-relative fix).
