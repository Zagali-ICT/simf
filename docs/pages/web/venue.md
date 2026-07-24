# Website "The venue" — `/about/venue`

| | |
|--|--|
| **Route** | `/about/venue` — Blazor SSR Razor page (static render) |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Audience** | Anyone (public) |
| **Auth** | None — anonymous |
| **Status** | ✅ Real minimal shell — bilingual, responsive; mostly static (the event **date** is config-driven, D-755). The Figma frame is a pure stub, so this shows the REAL venue facts — see §7 |
| **Source** | [`Venue.razor`](../../../src/Website/SIMF.Web/Components/Pages/Venue.razor) · [`LandingPageHero.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingPageHero.razor) · [`landing.css`](../../../src/Website/SIMF.Web/wwwroot/css/landing.css) (`ln-venue` + reused `ln-pghero` / `ln-fsection` / `ln-btn`) |
| **Strings** | [`Strings.resx`](../../../src/Website/SIMF.Web/Resources/Strings.resx) / [`Strings.ar.resx`](../../../src/Website/SIMF.Web/Resources/Strings.ar.resx) (`Venue.*` + reused `PageHero.Home` / `About.Breadcrumb` / `Landing.Hero.Venue` / `Landing.Subnav.Date` / `Landing.Subnav.Time`) |
| **Data** | The event **date** is config-driven — `OrganizationProfile` dates via the `ForumDates` service (D-755), with `Landing.Subnav.Date` as fallback; the venue name / time reuse the shared landing event-fact keys (single-sourced) |
| **Figma** | KSA Maritime Forum — Forum Venue (Desktop AR), node `5866-40935` — **a pure stub (un-customised Organizer clone)** |
| **E2E** | [`e2e/web-venue.md`](../../tests/e2e/web-venue.md) (`E2E-WVEN-*`) |

## 1. Purpose

Where the forum is held — the fifth (final) page of the About cluster. A **Blazor
SSR** page on the shared `ln-` chrome: the interior photo-hero, then a single
venue-info card with the location, date, time and a "get directions" link.

## 2. Architecture

- **Rendering** — Blazor SSR. The only API call is the config-driven event date
  (`ForumDates` → `OrganizationProfile`, D-755); everything else is static resx.
  Shared chrome via `LandingShell`; the hero is the reusable `LandingPageHero` with a
  **3-level breadcrumb** (Home / About / The venue), carrying the page's single `<h1>`.
- **Venue card** (`ln-venue`) — a centred white info card on the `ln-fsection` chrome:
  a location-pin icon, the venue name, address, a date/time meta pair and a primary
  "get directions" button that opens Google Maps in a new tab (`rel="noopener noreferrer"`).
- **Single-sourced facts** — the venue name reuses `Landing.Hero.Venue` and the time
  reuses `Landing.Subnav.Time`. The **date** is config-driven via the injected
  `ForumDates` service (the same `OrganizationProfile` dates Landing/Speakers use,
  D-755), falling back to `Landing.Subnav.Date` when the profile carries no dates — so
  a CP date edit propagates here and the event facts are not duplicated. Page-specific
  labels (title, address, "get directions") are `Venue.*` resx keys.

## 3. Sections

| # | Section | Class | Content |
|---|---------|-------|---------|
| 1 | Interior hero | `ln-pghero` (via `LandingPageHero`) | Breadcrumb Home / About / The venue, `<h1>` (`Venue.Hero.Title`), subtitle, venue + date pills |
| 2 | Venue card | `ln-fsection` → `ln-venue` | Title (`Venue.Section.Title`) + sub, then a pin icon + venue name (`Landing.Hero.Venue`) + address (`Venue.Address`) + config-driven date (`ForumDates`, `Landing.Subnav.Date` fallback) + time (`Landing.Subnav.Time`) + a "Get directions" button → Google Maps |

## 4. Bilingual model (AR RTL / EN LTR)

- **Hero + section headers + labels** → resx (`Venue.*` + reused shared keys),
  following the `/culture` switch.
- **Direction** — logical properties; the hero gradient keeps its `[dir=ltr]` flip.

## 5. Responsive

The venue card is a single centred column (max-width 720px) at all widths; padding
tightens below 560px and the date/time meta wraps. The hero block goes full-width
below 720px. No horizontal overflow at 1440 / 1024 / 768 / 390 (`scrollWidth ==
clientWidth` verified in both languages).

## 6. Verification

- **Build** — `dotnet build -c Release` 0 warnings / 0 errors.
- **Component tests** — `tests/SIMF.Web.Tests/VenuePageTests.cs` (5, green):
  single-`<h1>` + 3-level breadcrumb; the venue card reusing the shared event facts;
  the external directions link (Google Maps, `target=_blank`, `rel` includes `noopener`);
  **the config-driven date** (shows the CP `OrganizationProfile` range when set) and
  **the resx fallback** (keeps `Landing.Subnav.Date` when the profile carries no dates).
- **Live render** — visually verified 2026-07-15 at **AR@1440** and **EN@1440** (correct
  RTL→LTR mirror): the centred venue card renders the pin, the Sofitel Riyadh name,
  address, date/time and the directions button. Console clean; no horizontal overflow.
  The 2026-07-22 date-wiring change is layout-neutral (same cell, config-driven text).

## 7. Follow-ups — content flagged (this was a stub Figma frame)

The Figma frame `5866-40935` is a **pure stub** — an un-customised clone of the
Organizer placeholder (same "الجهة المنظمة" title + MOD cards, no venue design at
all). Rather than replicate the placeholder, this page shows the **real, known
venue** (Sofitel Riyadh Hotel & Convention Center, reused from the event facts) in a
clean minimal card. **When a real venue Figma exists, extend this shell** with the
intended venue design — likely an **interactive/embedded map** and a **photo
gallery** — and confirm the exact venue name, address and coordinates with the client.

_Last reviewed:_ 2026-07-22 by Claude (config-driven event date via ForumDates, D-755 / #40).
