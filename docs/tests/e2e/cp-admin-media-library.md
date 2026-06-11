# E2E test catalogue — `Media Library` (`/admin/media-library`)

> **Authority:** SIMF E2E test catalogue (D-133 slice 7 / D-245). Page shipped
> under D-357 (unified media-asset pipeline). The catalogue is the **source of
> truth** for what E2E coverage exists; the implementation lives next to the
> test runner.

| | |
|--|--|
| **Page** | [`media-library.md`](../../pages/cp/media-library.md) |
| **Route** | `/admin/media-library` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell driver _(or: Playwright when adopted)_ |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via `Get-Totp` helper |
| **Permissions** | page: `MediaLibrary.View`; deactivate/restore actions: `MediaLibrary.Manage` |
| **Key API** | `POST /account/api/admin/assets/list`, `GET /account/api/admin/assets/item/{id}`, `DELETE …/item/{id}`, `POST …/item/{id}/restore` |
| **Last reviewed** | `2026-06-10` |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MLIB-001 | Golden path — list every asset, open details, deactivate an active asset | happy | P0 | _to author_ |
| E2E-MLIB-002 | Empty state — no assets → SimfEmptyState | happy | P1 | _to author_ |
| E2E-MLIB-003 | Auth gate — user without `MediaLibrary.View` → /not-permitted | auth | P0 | _to author_ |
| E2E-MLIB-004 | Restore an inactive asset | happy | P1 | _to author_ |
| E2E-MLIB-005 | Restore conflict (409) — a live asset already owns (category,owner) | error | P1 | _to author_ |
| E2E-MLIB-006 | Manage-gate — `MediaLibrary.View`-only user sees no deactivate/restore | auth | P0 | _to author_ |
| E2E-MLIB-007 | Server 500 on list → load-failed alert | resilience | P2 | _to author_ |
| E2E-MLIB-008 | RTL render (Arabic) | i18n | P1 | _to author_ |
| E2E-MLIB-009 | Preview cell — uploaded image renders thumbnail; missing → placeholder icon | happy | P2 | _to author_ |
| E2E-MLIB-010 | External-link asset shows Source = External link + URL | happy | P2 | _to author_ |

## Scenarios

### E2E-MLIB-001 — Golden path

```gherkin
Feature: Media Library central management
  As an Administrator
  I want to see every uploaded/linked media asset in one grid and retire one
  So that media across all entities is governed from a single place

Background:
  Given an Administrator is signed in
  And a speaker "Cdre. Faisal" has an uploaded SpeakerPhoto asset
  And a sponsor "Acme" has an uploaded SponsorLogo asset

Scenario: List assets, open details, deactivate
  When the administrator opens /admin/media-library
  Then the grid lists both assets with columns Category, Owner, Preview, Kind, Source, Active
  And the SpeakerPhoto row shows Category "SpeakerPhoto", Owner "Cdre. Faisal", Source "Uploaded file", Active = yes
  When the administrator opens the details of the SpeakerPhoto row
  Then the details modal shows a preview image and an enabled "Deactivate" button
  When the administrator clicks "Deactivate"
  Then a success toast "Asset deactivated." appears
  And the row's Active column becomes "no"
  And a later GET of /app/assets/SpeakerPhoto/{ownerId}/image returns 404
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-media-library-golden-before.png`
- Screenshot after: `docs/screenshots/cp-admin-media-library-golden-after.png`
- Console errors: 0 expected
- Network failures: 0 expected
- Audit row: `OperationLog` row with `Event = 'AssetRemoved'` and the actor's id.

### E2E-MLIB-002 — Empty state

```gherkin
Scenario: Empty state renders SimfEmptyState
  Given no Asset rows match the current filter
  When the administrator opens /admin/media-library
  Then the grid shows the SimfEmptyState "No media assets yet."
  And no error toast appears
```

### E2E-MLIB-003 — Auth gate

```gherkin
Scenario: User without MediaLibrary.View is denied
  Given a signed-in admin whose roles grant no MediaLibrary.View permission
  When they navigate to /admin/media-library
  Then they are redirected to /not-permitted with HTTP 200
  And the Media Library nav item is not shown to them
```

### E2E-MLIB-004 — Restore an inactive asset

```gherkin
Scenario: Restore a previously deactivated asset
  Given a SponsorLogo asset for "Acme" that is inactive
  And no other live SponsorLogo asset exists for "Acme"
  When the administrator opens its details and clicks "Restore"
  Then a success toast "Asset restored." appears
  And the row's Active column becomes "yes"
  And GET /app/assets/SponsorLogo/{ownerId}/image returns 200 with the image bytes
```

### E2E-MLIB-005 — Restore conflict (409)

```gherkin
Scenario: Restore is blocked when a live asset already owns the pair
  Given an inactive SpeakerPhoto asset A for speaker "Cdre. Faisal"
  And a different active SpeakerPhoto asset B for the same speaker
  When the administrator opens A's details and clicks "Restore"
  Then the API returns 409 (the filtered unique index forbids two live rows)
  And an error toast surfaces the conflict message
  And asset A stays inactive
```

### E2E-MLIB-006 — Manage-gate hides destructive actions

```gherkin
Scenario: View-only user cannot deactivate or restore
  Given a signed-in admin with MediaLibrary.View but not MediaLibrary.Manage
  When they open /admin/media-library and open any asset's details
  Then the "Deactivate" and "Restore" buttons are not rendered
  And a direct DELETE /account/api/admin/assets/item/{id} is rejected (403)
```

### E2E-MLIB-007 — Server 500 on list

```gherkin
Scenario: List failure shows a load-failed alert
  Given the assets list endpoint returns HTTP 500
  When the administrator opens /admin/media-library
  Then a "Could not load media assets." alert is shown
  And the page does not crash (no unhandled circuit error)
```

### E2E-MLIB-008 — RTL render

```gherkin
Scenario: Arabic renders right-to-left with no overflow
  Given the interface language is Arabic
  When the administrator opens /admin/media-library
  Then the grid, column headers and details modal render RTL
  And document scrollWidth equals clientWidth (no horizontal overflow)
  And every column header shows its Arabic string
```

### E2E-MLIB-009 — Preview cell

```gherkin
Scenario: Preview thumbnail vs placeholder
  Given one asset with stored image bytes and one asset that is an external link to an unreachable host
  When the administrator opens /admin/media-library
  Then the first row's Preview cell renders an <img> thumbnail (no broken-image)
  And the second row's Preview cell renders the placeholder image icon
```

### E2E-MLIB-010 — External-link asset

```gherkin
Scenario: External link asset is labelled and carries its URL
  Given a NewsImage asset set as an external link "https://cdn.example/news/1.jpg"
  When the administrator opens /admin/media-library and opens its details
  Then Source shows "External link"
  And the URL field shows "https://cdn.example/news/1.jpg"
```

---

_Last reviewed:_ `2026-06-10` by `D-357 author`.
