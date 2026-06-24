# E2E test catalogue — `Organization Profile` (CP `/admin/organization-profile`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). D-495 — the CP page that edits
> the edition-generic forum config (name / title / slogan / bio, version + dates,
> event start/end, current year, status, location + GPS, contact, live-stream link,
> 7 social links, and the about-items + details lists). Backed by `GET`/`PUT
> /admin/organization-profile`; the app + website consume it via the public
> `GET /app/organization-profile` (anonymous, `Last-Modified`/304). The social +
> welcome values were migrated here from the old SiteSettings keys (one source of truth).

| | |
|--|--|
| **Page** | CP `/admin/organization-profile` (`OrganizationProfilePage.razor`) |
| **Route** | `/admin/organization-profile` (nav item `Module.OrganizationProfile`) |
| **APIs** | `GET /api/v1/admin/organization-profile` (prefill, `OrganizationProfile.View`); `PUT /api/v1/admin/organization-profile` — `AdminUpdateOrganizationProfileRequest` (full-document upsert, `OrganizationProfile.Manage`). Public read: `GET /api/v1/app/organization-profile` (anonymous, `Last-Modified`/`If-Modified-Since`→304). |
| **Surface** | Control Panel (Blazor) — Administrator |
| **Auth setup** | A signed-in admin with `OrganizationProfile.Manage`. Use `Get-Totp` for 2FA — never a literal secret. |
| **Last reviewed** | 2026-06-24 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-ORGP-001 | Page loads, prefilled with the current effective profile (seeded 2026 edition incl. about-items + details) | happy | P0 | authored ✓ (`OrganizationProfileTests` admin GET; `GET_public_is_anonymous_and_returns_the_seeded_edition`) |
| E2E-ORGP-002 | Edit scalars + status/year + a social URL + the live link + add/edit about-items & details → Save → success toast; values persist and `GET /app/organization-profile` returns them | happy | P0 | authored ✓ (`OrganizationProfileTests` PUT save+reflect) |
| E2E-ORGP-003 | Conditional GET — a second public read with `If-Modified-Since` = the prior `Last-Modified` returns `304 Not Modified` (no body); after an edit it returns `200` + the new body | caching | P1 | authored ✓ (`OrganizationProfileTests` 304) |
| E2E-ORGP-004 | Auth gate — a non-admin (or an admin without `OrganizationProfile.View`/`Manage`) is forbidden / the nav item is hidden | auth | P0 | authored ✓ (`OrganizationProfileTests` admin 403-gate + `CpNavigationPermissionTests`) |
| E2E-ORGP-005 | Validation — a non-http(s) URL (social / website / live link) → `400`; a name/title cleared → `400` | validation | P1 | authored ✓ (`OrganizationProfileTests` URL-reject) |
| E2E-ORGP-006 | Edition switch — change current year + status (Open→Archived) + the about/detail content → the app About screen + status badge re-skin to that edition from the cached profile | happy | P1 | spec |
| E2E-ORGP-007 | A save wire failure (5xx / network) → error toast, the form keeps the entered values | resilience | P1 | spec |
| E2E-ORGP-008 | RTL render (Arabic) — sections/labels/button mirror; URL + GPS fields stay LTR; the bilingual about-items/details edit cleanly | i18n | P1 | spec |

## Scenarios

### E2E-ORGP-002 — Edit + save flows through to the public endpoint

```gherkin
Feature: CP Organization Profile
Scenario: An admin edits the forum config and the app reads it
  Given an admin with OrganizationProfile.Manage opens /admin/organization-profile
  And the form is prefilled from GET /admin/organization-profile (the seeded 2026 edition)
  When they set the current year to 2026 and status to "Open"
  And set the main live-stream link to "https://youtube.com/watch?v=simf2026"
  And set Facebook to "https://facebook.com/simf"
  And edit the "Vision" about-item text and add a "Year : 2026" detail
  And tap Save
  Then PUT /admin/organization-profile upserts the profile and shows the success toast
  And GET /api/v1/app/organization-profile returns the new values + about-items + details
  And the app About screen renders name → title → the about cards → the details list from it
```

### E2E-ORGP-003 — Conditional GET (Last-Modified / 304)

```gherkin
Scenario: The app revalidates its cached profile cheaply
  Given the app fetched GET /app/organization-profile and stored the Last-Modified value
  When it re-requests with If-Modified-Since set to that value
  Then the API returns 304 Not Modified with no body and the app keeps its cache
  When an admin then saves a change in the CP
  And the app re-requests with the stale If-Modified-Since
  Then the API returns 200 with the new profile and the app replaces its cache
```

### E2E-ORGP-004 — Auth gate

```gherkin
Scenario: Only admins with the OrganizationProfile permission reach the page
  Given a signed-in non-admin
  When they request /api/v1/admin/organization-profile
  Then the API returns 403 Forbidden
  And the CP nav hides the Organization Profile item for accounts without OrganizationProfile.View
```

### E2E-ORGP-005 — URL validation

```gherkin
Scenario: A non-http(s) URL is rejected
  Given an admin editing the profile
  When they set the live-stream link to "javascript:alert(1)" and tap Save
  Then PUT /admin/organization-profile returns 400 ORGANIZATION_PROFILE_INVALID
  And nothing is persisted
```

---

_Last reviewed:_ `2026-06-24` by `SIMF Team` — D-495 (new page).
