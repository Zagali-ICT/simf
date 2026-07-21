# E2E test catalogue — Website "The venue" (`/about/venue`)

| | |
|--|--|
| **Page** | [`web/venue.md`](../../pages/web/venue.md) |
| **Route** | `/about/venue` |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Test runner** | Chrome DevTools MCP (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | **None — anonymous and static.** No API call, no bearer token, no seeding. |
| **Figma** | KSA Maritime Forum — Forum Venue (Desktop AR), node `5866-40935` — **a pure stub** |
| **Last reviewed** | 2026-07-15 |

> **What this page is.** `/about/venue` (`Venue.razor`) is the Website's public,
> anonymous **venue overview** — the fifth About-cluster page. Static SSR on the
> shared `LandingShell` chrome, no CRUD. Two sections:
> 1. **Interior hero** (`ln-pghero`, via `LandingPageHero`) — photo + gradient, a
>    **3-level breadcrumb** (Home / About / The venue), the single `<h1>`, a subtitle
>    and the venue + date pills.
> 2. **Venue card** (`ln-fsection` → `ln-venue`) — a title + subtitle, then a centred
>    card: a pin icon + the venue name (`Landing.Hero.Venue`) + address + a date/time
>    meta pair (`Landing.Subnav.*`) + a "Get directions" button that opens Google Maps
>    in a new tab.
>
> **Content note.** The Figma frame is a pure stub (an un-customised Organizer
> clone); this page shows the real known venue instead (see `web/venue.md` §7).
>
> **Auth model (Website, anonymous).** No `RequirePermission`, no `/not-permitted`,
> no `/login` redirect.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WVEN-001 | Golden path — hero + venue card render + shared chrome, one `<h1>` | happy | P0 | _to author_ |
| E2E-WVEN-002 | Hero 3-level breadcrumb — Home / About / The venue, the "About" level links to `/about` | happy | P1 | _to author_ |
| E2E-WVEN-003 | Venue card — pin icon + venue name (reused `Landing.Hero.Venue`) + address + date/time (reused `Landing.Subnav.*`) | happy | P1 | _to author_ |
| E2E-WVEN-004 | Directions link — opens Google Maps in a new tab with `rel="noopener noreferrer"` (no opener leak) | happy | P1 | _to author_ |
| E2E-WVEN-005 | Static/anonymous — no `/api/...` request, no Authorization header, no `/login` or `/not-permitted` redirect | auth | P0 | _to author_ |
| E2E-WVEN-006 | RTL / Arabic ⇄ LTR / English — mirrors correctly (hero photo side flips); Arabic venue name in AR, English in EN | i18n | P0 | _to author_ |
| E2E-WVEN-007 | Responsive — the centred card holds; no horizontal overflow at 1440/1024/768/390 in both languages | responsive | P1 | _to author_ |

## Scenarios

### E2E-WVEN-001 — Golden path

```gherkin
Feature: Website Venue page shows where the forum is held
  As any visitor (anonymous or signed in)
  I want to know the forum's location, date and time
  So that I can plan to attend

Background:
  Given the Website is reachable
  And the browser is a fresh anonymous session (no auth cookie, no bearer token)

Scenario: The page renders the hero + the venue card
  When the browser opens /about/venue
  Then NO request to /api/... is made (the page is static)
  And the shared header + footer render (LandingShell chrome)
  And an interior hero (section.ln-pghero) renders with exactly one <h1>
  And a section (section.ln-fsection) renders one .ln-venue card
  And the page title is "The venue — Saudi International Maritime Forum"
```

**Evidence captured:**
- Screenshot: `docs/screenshots/web-venue-ar-1440.png` (AR) + `web-venue-en-1440.png` (EN)
- Console errors: 0 expected
- Network: no `/api/v1/...` request; the hero photo returns 200
- Audit row: none

### E2E-WVEN-002 — Hero 3-level breadcrumb

```gherkin
Scenario: The hero breadcrumb has three levels with a working parent link
  When the browser opens /about/venue
  Then the .ln-pghero__crumbs breadcrumb reads "Home / About / The venue"
  And the "About" crumb links to "/about" and "Home" links to "/"
  And the current "The venue" crumb is plain text
```

### E2E-WVEN-003 — Venue card

```gherkin
Scenario: The venue card shows the reused event facts
  When the browser opens /about/venue
  Then section.ln-fsection renders its title (Venue.Section.Title) + subtitle
  And one .ln-venue card renders with a .ln-venue__icon (pin)
  And the .ln-venue__name equals the shared Landing.Hero.Venue value
      ("Sofitel Riyadh Hotel & Convention Center" in EN / "فندق ومركز مؤتمرات سوفيتيل الرياض" in AR)
  And the .ln-venue__addr shows Venue.Address
  And the date/time meta pair shows Landing.Subnav.Date and Landing.Subnav.Time
```

### E2E-WVEN-004 — Directions link

```gherkin
Scenario: The "Get directions" button opens an external map safely
  When the browser opens /about/venue
  Then the .ln-venue a.ln-btn "Get directions" link's href points at Google Maps (google.com/maps)
  And its target is "_blank"
  And its rel contains "noopener" (and "noreferrer") — the external tab cannot access window.opener
```

### E2E-WVEN-005 — Static / anonymous

```gherkin
Scenario: The page loads anonymously and fires no API request
  Given a fresh browser with no auth cookie and no bearer token
  When the user opens /about/venue directly
  Then the page renders WITHOUT redirecting to /login or /not-permitted
  And NO request to /api/... is made and no Authorization header is sent

Scenario: A signed-in session changes nothing
  Given an Approved Visitor is signed in on the Website
  When they open /about/venue
  Then the rendered page is identical to the anonymous view
```

### E2E-WVEN-006 — RTL / LTR

```gherkin
Scenario: The page mirrors between Arabic and English
  When the browser opens /about/venue under the Arabic UI culture (<html dir="rtl" lang="ar">)
  Then the page mirrors right-to-left, the hero photo sits on the LEFT
  And the breadcrumb reads "الرئيسية / عن الملتقى / المكان" and the venue name renders Arabic

  When the browser opens /about/venue under the English UI culture (<html dir="ltr" lang="en">)
  Then the hero photo flips to the RIGHT
  And the breadcrumb reads "Home / About / The venue" and the venue name renders English
```

### E2E-WVEN-007 — Responsive

```gherkin
Scenario: The venue card holds with no horizontal overflow
  When the browser opens /about/venue and the viewport width is set to each of 1440, 1024, 768, 390
  Then the centred .ln-venue card renders (padding tightens below 560px; the date/time meta wraps)
  And the hero block goes full-width below 720px
  And at every width in {1440, 1024, 768, 390} document.scrollWidth == document.clientWidth (no horizontal overflow)
  And this holds in BOTH the EN (LTR) and AR (RTL) cultures
```

---

## Implementation notes

- **Read-only, anonymous, static, no CRUD.** The only interaction is the external
  "Get directions" link. The matrix above is exhaustive.
- **Stub Figma frame → real minimal content.** The Figma is an un-customised Organizer
  clone; this page shows the real venue facts instead. A full venue design (map +
  gallery) awaits a real Figma frame (`web/venue.md` §7).
- **Lower-layer coverage:** component (bUnit, no browser)
  `tests/SIMF.Web.Tests/VenuePageTests.cs` pins the render + the directions link.
- **Convert to Playwright** when adopted: copy each Gherkin scenario into a `.feature`
  under `tests/SIMF.E2E.Tests/`.

---

_Last reviewed:_ 2026-07-15 by Claude (The venue page — `ln-` Bootstrap SSR, Figma 5866-40935 [stub]).
