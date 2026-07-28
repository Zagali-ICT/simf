# E2E test catalogue — Website marketing landing (`/`)

| | |
|--|--|
| **Page** | [`web/landing.md`](../../pages/web/landing.md) |
| **Route** | `/` (static `wwwroot/index.html` + `content.js`; data feed `GET /content/site`) |
| **Surface** | Website (public, anonymous) |
| **Test runner** | Chrome DevTools MCP + PowerShell driver (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | None — the landing and its `/content/site` feed are anonymous. Seed the API so the public reads return rows (≥1 published session / speaker / sponsor / media-partner / news / archive edition / media image), and ensure the `hero.*` content blocks are seeded (they are, by the idempotent seeder). |
| **Last reviewed** | 2026-06-05 |

> **What this page is.** `/` is the **static** marketing site (`index.html` +
> `content.js`), not a Blazor page. It renders `SITE_DEFAULTS` client-side, then
> calls `loadSiteContentRemote()` which `fetch()`es **`GET /content/site`** — a
> same-origin Website proxy that reshapes the API's anonymous public reads into
> the content model and merges them in (D-294). The API has no CORS, so the
> proxy is the same-origin bridge; gallery images are re-streamed via
> `GET /content/media/{id}/image`. Anything the feed omits (or a failed fetch)
> leaves the built-in defaults — the page never blanks.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WLD-001 | Golden — landing loads and the dynamic sections (sessions/speakers/partners/news/archive) come from `/content/site` | happy | P0 | _to author_ |
| E2E-WLD-002 | `/content/site` shape — only sections with rows are present; each row carries `field` + `field_en` | happy | P1 | _to author_ |
| E2E-WLD-003 | Partners strip merges sponsors **then** media partners | happy | P1 | _to author_ |
| E2E-WLD-004 | Resilience — API offline / 503: `loadSiteContentRemote()` returns null, landing keeps `SITE_DEFAULTS`, no error shown | resilience | P0 | _to author_ |
| E2E-WLD-005 | Empty section — archive hidden (D-166 toggle off) → `archive` omitted → section keeps default | resilience | P1 | _to author_ |
| E2E-WLD-006 | Gallery image proxy — `spirit` images load via `GET /content/media/{id}/image`; a missing image 404s without breaking the page | happy | P2 | _to author_ |
| E2E-WLD-007 | Hero CMS text — hero renders the seeded `hero.*` blocks; all-or-nothing (a missing key keeps the default hero) | happy | P1 | _to author_ |
| E2E-WLD-008 | Bilingual — EN/AR toggle swaps `field`/`field_en` across dynamic sections + hero | i18n | P1 | _to author_ |
| E2E-WLD-009 | Editorial sections CMS-driven — About / stats strip (incl. the 4 numbers) / Pillars header / Goals render the seeded `about.* / stats.* / pillars.* / goals.*` blocks; an unseeded key keeps the built-in copy (D-336) | happy | P1 | _to author_ |
| E2E-WLD-010 | Skeleton loading — while `/content/site` is in flight the dynamic sections (sessions/speakers/partners/news) show shimmer placeholders, replaced by real rows once it resolves; no `.skeleton` remains and no unhandled rejection (D-337) | happy | P1 | _to author_ |
| E2E-WLD-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-WLD-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-WLD-001 — Golden: landing loads live content

```gherkin
Feature: Marketing landing pulls live content from the API
  As a public visitor
  I want the landing's sessions/speakers/partners/news/history to be current
  So that the marketing site reflects the real event without a redeploy

Background:
  Given the API is reachable and seeded with at least one published session,
        speaker, sponsor, media partner, news article and archive edition
  And the Website is reachable

Scenario: The landing renders the dynamic sections from /content/site
  When a visitor opens "/"
  Then the page issues a GET to /content/site that returns 200 application/json
  And the sessions grid shows the seeded session title (not the SITE_DEFAULTS sample)
  And the speakers marquee shows the seeded speaker name
  And the partners strip shows the seeded sponsor and media-partner names
  And the news grid shows the seeded article title
  And the archive timeline shows the seeded edition year
  And no console error is logged
```

**Evidence captured:**
- Screenshot: `docs/screenshots/web-landing-live.png` (live sections visible)
- Network: one `GET /content/site` → 200; gallery tiles request `GET /content/media/{id}/image`
- Console errors: 0 expected

### E2E-WLD-002 — Feed shape

```gherkin
Scenario: /content/site is bilingual and omits empty sections
  When a client GETs /content/site
  Then every section present has at least one row
  And each text-bearing row carries both `field` and `field_en` (e.g. name / name_en)
  And a section with no API rows is absent from the document (not an empty array)
```

### E2E-WLD-003 — Partners merge order

```gherkin
Scenario: Sponsors come before media partners in the partners strip
  Given the API has 2 sponsors and 1 media partner
  When the landing renders the partners strip from /content/site
  Then the first two partner cards are the sponsors (tier order preserved)
  And the third card is the media partner
  And a partner with no servable logo shows its name text instead of a broken image
```

### E2E-WLD-004 — Resilience: API offline

```gherkin
Scenario: An unreachable API leaves the built-in defaults
  Given the API is stopped (or /content/site returns 503)
  When a visitor opens "/"
  Then loadSiteContentRemote() resolves to null
  And the landing still renders its SITE_DEFAULTS content
  And no error toast or broken section is shown
  And no unhandled exception reaches the browser console
```

### E2E-WLD-005 — Empty section (archive hidden)

```gherkin
Scenario: A hidden archive does not replace the default section
  Given the D-166 archive-visibility toggle is OFF (GET /app/archive returns empty Items)
  When the landing loads /content/site
  Then the document has no `archive` key
  And the archive section keeps its SITE_DEFAULTS editions
```

### E2E-WLD-006 — Gallery image proxy

```gherkin
Scenario: Spirit images stream same-origin
  Given the API has an active media image item M
  When the landing renders the Saudi-spirit gallery
  Then each spirit slot's image src is /content/media/{M.Id}/image
  And that request returns 200 with the image content-type (proxied from the API)
  And a deleted/inactive media id returns 404 without breaking the page layout
```

### E2E-WLD-007 — Hero CMS text (all-or-nothing)

```gherkin
Scenario: The hero renders the seeded CMS blocks
  Given the hero.* content blocks are seeded
  When the landing loads /content/site
  Then the document's `hero` object carries titleStart / titleHighlight / titleEnd /
       tagline / metaDate / metaVenue / ctaSecondary (each with an _en sibling)
  And the hero headline + date + venue render those values

Scenario: A missing hero key keeps the default hero
  Given one hero.* block is absent
  When the landing loads /content/site
  Then the document has no `hero` key
  And the hero keeps its hardcoded default text (no half-populated hero)
```

### E2E-WLD-008 — Bilingual render

```gherkin
Scenario: The language toggle swaps base and _en values
  Given the landing has loaded live content
  When the visitor switches the site language EN <-> AR
  Then dynamic rows show `field` (Arabic) or `field_en` (English) accordingly
  And the hero text follows the same rule
  And Arabic renders RTL with no Latin leakage in the dynamic sections
```

### E2E-WLD-009 — Editorial sections are CMS-driven (D-336)

```gherkin
Scenario: About / stats / Pillars header / Goals render the seeded CMS text
  Given the about.* / stats.* / pillars.* / goals.* content blocks are seeded
  When the landing loads /content/site
  Then the document carries `about`, `stats`, `pillars` and `goals` objects
  And each text node has a base (Arabic) value and an `_en` sibling
  And the four stat cells show stats.count1..4 with stats.label1..4
  And the five goal cards show goals.item1..5.t / .d

Scenario: An unseeded editorial key keeps the page's built-in copy
  Given the goals.item3.d content block is absent (or the whole goals.* set is)
  When the landing loads /content/site
  Then the missing field falls back to the page's data-i18n dictionary text
  And no editorial section is blank
  # Binding order is data-cms (CMS) over data-i18n (built-in): API > seeded CMS > dictionary.

Scenario: An admin edits a section from the CP and the landing reflects it
  Given an admin updates about.h2 via /admin/content-blocks
  When a visitor reloads "/"
  Then the About heading shows the edited value (no redeploy)
```

### E2E-WLD-010 — Skeleton loading (D-337)

```gherkin
Scenario: Dynamic sections show a shimmer skeleton while loading, then real data
  When a visitor opens "/"
  Then before /content/site resolves the sessions/speakers/partners/news
       containers show .skeleton shimmer placeholders (not the sample defaults)
  When /content/site resolves
  Then every .skeleton is removed and the real rows render
  And window.__contentReady is true
  And the browser console logs no unhandled promise rejection

Scenario: A section that throws on edge data does not strand other skeletons
  Given the live archive is empty (arch[archIdx] is undefined)
  When the content loader runs
  Then the archive paint is skipped without throwing
  And every other section (incl. speakers) still renders — no stuck skeleton
  # finish() runs each render step in its own try/catch (regression guard, D-337).
```

---

## Implementation notes

- **Same-origin proxy, anonymous.** `GET /content/site` and
  `GET /content/media/{id}/image` are anonymous Website endpoints
  (`SiteContentEndpoints`) — they exist because the API has no CORS policy, so
  the browser cannot read it cross-origin. The proxy uses the extended
  `SimfPublicClient`.
- **Graceful degradation is the contract.** The mapper omits a section unless it
  has ≥1 row, and emits `hero` only when every hero key resolved. A failed fetch
  returns `null` and the page keeps `SITE_DEFAULTS`. Author E2E-WLD-004/005/007
  against that contract — do not assert a blank section on missing data.
- **Lower-layer coverage.** Unit tests back the reshape and the client:
  `tests/SIMF.Web.Tests/SiteContentMapperTests.cs` (Compose: bilingual emission,
  partners merge, archive counts, spirit proxy, hero all-or-nothing, empty →
  empty, **landing-section nested bilingual projection + per-section presence,
  D-336**) and `tests/SIMF.ApiClient.Tests/SimfPublicClientTests.cs` (routes,
  envelope-failure → null, transport-failure → null, media bytes fetch).
- **Convert to Playwright** when the runner is adopted: each Gherkin scenario
  maps to a `.feature` step; the steps are already runner-agnostic.

## Dynamic forum date (D-755)

```gherkin
Scenario: E2E-WLD-008 — the forum date reflects OrganizationProfile config, not a literal
  Given the OrganizationProfile has EventStartDate 2026-11-23 and EventEndDate 2026-11-25
  When a visitor opens the landing page in English
  Then the sub-nav date and the speakers band read "23-25 November 2026"
  And in Arabic they read "23-25 نوفمبر 2026"
  When an admin edits the OrganizationProfile event dates to 2027-03-01..2027-03-03
  And the 5-minute ForumDates cache expires
  Then every public date label re-renders from the new config (no code change)

Scenario: E2E-WLD-009 — the marketing hero never blanks when the profile API is unreachable
  Given the OrganizationProfile API returns an error or has no event dates
  When a visitor opens the landing page
  Then the date label falls back to the resx string and the page still renders
  And the miss is not cached, so the next request retries
```

Evidence: `EventDateRangeTests` (formatter, 8/8); `ForumDatesTests` (cache + fallback, pending the pre-existing `SIMF.Web.Tests` compile fix). Config source: `OrganizationProfile.EventStartDate/EventEndDate`; formatter `SIMF.Common.EventDateRange`.

---

_Last reviewed:_ 2026-07-21 by Claude (D-755 — dynamic forum dates from OrganizationProfile config; was D-336 CMS-driven landing).
