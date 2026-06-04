# E2E test catalogue — `Past-edition detail` (`archive-detail`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> per-edition public detail read added in **D-273** (`GET /app/archive/{id}` +
> the `Location*` / `DateLabel*` columns). API implementation lives in
> `tests/SIMF.Api.Tests/ArchiveTests.cs` (public detail) and
> `tests/SIMF.Api.Tests/AdminArchiveTests.cs` (admin authoring round-trip).

| | |
|--|--|
| **Page** | [`Page_024-01`](../../App/Page_024-01/README.md) (App page docs) |
| **Route** | `GET /api/v1/app/archive/{id}` (anonymous) · app screen #24-01 `/archive/:editionId` (public) |
| **Surface** | Mobile (Flutter) + App API |
| **Test runner** | xUnit + `WebApplicationFactory` (API) · Flutter widget/integration test (screen) |
| **Auth setup** | **None for the read** — the detail is anonymous/public. An **Admin** token (admin TOTP via the `Get-Totp` helper, **no literal secrets**) only to seed an edition and to flip the archive-visibility toggle. |
| **Last reviewed** | 2026-06-04 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB024D-001 | Visible archive → `GET /app/archive/{id}` returns the edition (title, summary, counters) | happy | P0 | authored ✓ (`Public_detail_returns_edition_with_location_and_date_when_visible`) |
| E2E-MOB024D-002 | Place + date label round-trip in the public payload (`location*` / `dateLabel*`) | happy | P0 | authored ✓ (`Public_detail_returns_edition_with_location_and_date_when_visible`) |
| E2E-MOB024D-003 | Anonymous caller (no token) can read the detail (public) | auth | P0 | authored ✓ (`Public_detail_returns_edition_with_location_and_date_when_visible`) |
| E2E-MOB024D-004 | Unknown edition id → 404 `archive_edition_not_found` | edge | P0 | authored ✓ (`Public_detail_returns_404_for_an_unknown_id`) |
| E2E-MOB024D-005 | Archive visibility OFF → detail 404 (single surface, no leak) | edge | P0 | authored ✓ (`Public_detail_is_404_when_archive_visibility_is_off`) |
| E2E-MOB024D-006 | Soft-deleted (`IsActive == false`) edition → 404 | edge | P1 | authored (covered by the inactive filter in `PublicArchiveService.GetAsync`) |
| E2E-MOB024D-007 | Null optional scalars (summary / location / date / cover) → boxes hidden, gradient fallback | edge | P1 | authored (screen) |
| E2E-MOB024D-008 | Deferred sections (gallery / session titles / past speakers) show "coming soon" | edge | P2 | authored (screen) |
| E2E-MOB024D-009 | RTL render; year + counter numbers LTR | i18n | P1 | authored (screen) |

## Scenarios

### E2E-MOB024D-001 — Detail returned for a visible edition

```gherkin
Feature: Past-edition detail
  As any app user (no login)
  I want to open one past forum edition
  So that I can read its title, summary, place, date and counters

Scenario: A visible archive returns the edition detail
  Given the archive-visibility toggle is on
  And an admin has created the 2024 edition "الملتقى البحري السعودي الدولي 2024"
  When an anonymous client calls GET /api/v1/app/archive/{id}
  Then the response is 200
  And data.year is 2024
  And data.titleEn is "SIMF 2024" (titleAr in Arabic)
  And data.attendees, data.sessions and data.speakers are the seeded counters
```

**Evidence:** `ArchiveTests.Public_detail_returns_edition_with_location_and_date_when_visible` (green).

### E2E-MOB024D-002 — Place + date label round-trip

```gherkin
Scenario: The new place and date-label scalars are returned
  Given the 2024 edition has locationEn "Riyadh · Riyadh Front" and dateLabelEn "November 2024 · 3 days"
  When the detail is fetched
  Then data.locationEn is "Riyadh · Riyadh Front" and data.locationAr is "الرياض · واجهة الرياض"
  And data.dateLabelEn is "November 2024 · 3 days" and data.dateLabelAr is "نوفمبر 2024 · 3 أيام"
  And the screen renders the المكان and الزمن boxes
```

**Evidence:** `ArchiveTests.Public_detail_returns_edition_with_location_and_date_when_visible` +
`AdminArchiveTests.Admin_create_roundtrips_location_and_date_label` (green).

### E2E-MOB024D-003 — Anonymous read

```gherkin
Scenario: The detail is public — no token required
  Given a visible 2024 edition
  When a client with no Authorization header calls GET /api/v1/app/archive/{id}
  Then the response is 200 (not 401)
```

**Evidence:** `ArchiveTests.Public_detail_returns_edition_with_location_and_date_when_visible`
(the detail call uses the no-token client) (green).

### E2E-MOB024D-004 — Unknown id

```gherkin
Scenario: An unknown edition id is not found
  Given the archive-visibility toggle is on
  When a client calls GET /api/v1/app/archive/{a random guid}
  Then the response is 404
  And the error code is "archive_edition_not_found"
```

**Evidence:** `ArchiveTests.Public_detail_returns_404_for_an_unknown_id` (green).

### E2E-MOB024D-005 — Visibility gate

```gherkin
Scenario: A hidden archive does not leak an edition by id
  Given a 2014 edition exists and is active
  And the archive-visibility toggle is turned off
  When a client calls GET /api/v1/app/archive/{the 2014 edition id}
  Then the response is 404 (the same surface as an unknown id)
```

**Evidence:** `ArchiveTests.Public_detail_is_404_when_archive_visibility_is_off` (green).

### E2E-MOB024D-006 — Soft-deleted edition

```gherkin
Scenario: A deactivated edition is not found
  Given an edition has been soft-deleted (IsActive == false)
  When its detail is fetched while the archive is visible
  Then the response is 404 archive_edition_not_found
```

### E2E-MOB024D-007 — Null optionals

```gherkin
Scenario: Missing optional fields hide their boxes
  Given an edition with no summary, no location, no date label and no cover image
  When the detail renders
  Then the نبذة paragraph, the المكان box and the الزمن box are hidden
  And the cover uses the gradient fallback
  And the three counters still render
```

### E2E-MOB024D-008 — Deferred sections

```gherkin
Scenario: The not-yet-modelled lists show a placeholder
  Given the detail is shown
  Then the الصور والفيديو, عناوين الجلسات and المتحدثون السابقون sections
       render a "coming soon" placeholder (not broken/empty rows)
```

### E2E-MOB024D-009 — RTL render

```gherkin
Scenario: The detail renders right-to-left in Arabic
  Given the device locale is Arabic
  When the detail renders
  Then the layout and back chevron are right-to-left
  And the year overlay and the counter numbers render left-to-right
```

---

_Last reviewed:_ `2026-06-04` by `SIMF Team`.
