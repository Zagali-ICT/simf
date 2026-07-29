# E2E test catalogue — Regions lookup CRUD (`/admin/regions`)

| | |
|--|--|
| **Page** | [`cp/admin-regions.md`](../../pages/cp/admin-regions.md) |
| **Route** | `/admin/regions` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-30 (D-547 — Region lookup shipped) |

> **Page permission:** the page is gated by `@attribute [RequirePermission(PermissionCatalog.Regions.View)]`.
> The toolbar / row actions are individually gated by `Regions.Create`,
> `Regions.Edit` and `Regions.Delete` (`AdminOnly` baseline). `Administrator = "*"`
> therefore sees every action.
> The CP page calls the BFF passthroughs under `/account/api/admin/regions/*`
> (`AccountEndpoints.cs`), which forward to the API endpoints in
> `RegionEndpoints.cs`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-REGION-001 | Golden round-trip — Add → search → Edit (detail prefill) → Deactivate | happy | P0 | _to author_ |
| E2E-REGION-002 | Empty list / no-match search renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-REGION-003 | Search / per-column filter reloads the grid server-side (`GridQuery`) | function | P1 | _to author_ |
| E2E-REGION-004 | Seeded baseline — the 13 official regions are present on a fresh DB | function | P0 | _to author_ |
| E2E-REGION-005 | Auth gate — admin lacking `Regions.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-REGION-006 | Action gate — admin lacking `Regions.Create` sees no "New" button | auth | P1 | _to author_ |
| E2E-REGION-007 | Validation — blank Arabic name → bilingual error toast, no POST | error | P1 | _to author_ |
| E2E-REGION-008 | Server validation — Arabic name > 256 / Code > 16 → 400 `REGION_INVALID` | error | P2 | _to author_ |
| E2E-REGION-009 | Conflict — duplicate Code → 409 `REGION_INVALID` | error | P1 | _to author_ |
| E2E-REGION-010 | Delete confirm cancelled — SimfConfirm Cancel → no DELETE | function | P2 | _to author_ |
| E2E-REGION-011 | Not found — Edit/Delete a missing id → 404 `REGION_NOT_FOUND` | error | P2 | _to author_ |
| E2E-REGION-012 | Server 500 on `/list` → bilingual `LoadFailed` toast, no rows | resilience | P2 | _to author_ |
| E2E-REGION-013 | RTL / Arabic render mirrors page, grid, both modals | i18n | P1 | _to author_ |
| E2E-REGION-014 | Column sort toggles (`Sort` + `SortDescending`); default = SortOrder | function | P2 | _to author_ |
| E2E-REGION-015 | App picker parity — `GET /app/regions` returns the active rows ordered by SortOrder | function | P1 | _to author_ |
| E2E-REGION-016 | Delete confirmation gate — View/Delete + SimfConfirm (Cancel = no DELETE; confirm = one DELETE) | error | P0 | _to author_ |
| E2E-REGION-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-REGION-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-REGION-001 — Golden round-trip

```gherkin
Feature: Regions lookup CRUD round-trip
  As an Administrator
  I want to create, search, edit and deactivate a Saudi-regions lookup row
  So that the visitor place-of-birth / region picker stays accurate

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp (TOTP from Get-Totp)
  And they have landed on /admin/regions
  And the grid has loaded (POST /account/api/admin/regions/list returned 200)

Scenario: Create, search, edit, deactivate one region
  Given the grid summary reads "Showing 1–{N} of {N}" (the 13 seeded regions, or the SimfEmptyState if none)
  When the administrator clicks "New region"
  Then the Add modal opens titled "Add region"
  And it shows four inputs: Code, Name (Arabic), Name (English), Sort order
      (no "Active" checkbox in Create mode)
  When they fill Code="testregion"
  And they fill Name (Arabic)="منطقة تجريبية"
  And they fill Name (English)="Test Region"
  And they fill Sort order="99"
  And they click "Save"
  Then the BFF forwards POST /account/api/admin/regions and the API returns 200
  And the modal closes
  And a green SimfAlert reads "Region saved." / "تم حفظ المنطقة."
  And a row exists with Code="testregion", Name (Arabic)="منطقة تجريبية",
      Name (English)="Test Region" and the Active column showing "✓"

  When the administrator types "testregion" into the Search box
  And clicks the "Search" button
  Then POST /account/api/admin/regions/list fires with Search="testregion" and Skip=0
  And the grid shows only the matching row

  When the administrator clicks the row's Edit (pencil) action in the grid
  Then GET /account/api/admin/regions/{id} fires
  And the Edit modal opens titled "Edit region" with every field pre-filled,
      including Code="testregion", Sort order="99", and the "Active" checkbox (now shown, ticked)
  When they change Name (English) to "Test Region (edited)"
  And they click "Save"
  Then the BFF forwards PUT /account/api/admin/regions/{id} and the API returns 200
      (the {id} comes from the route; the request body carries no Id)
  And the modal closes
  And a green toast reads "Region saved." / "تم حفظ المنطقة."
  And the row's Name (English) column reads "Test Region (edited)"

  When the administrator clicks the row's Delete (trash) action in the grid
  Then the View/Delete form opens (in CrudShell — dialog by default) showing the
      row's read-only details and a red "Deactivate" button
  When they click "Deactivate"
  Then a SimfConfirm dialog appears reading
      "Deactivate “منطقة تجريبية”? It will be removed from the public lookup."
      / "تعطيل «منطقة تجريبية»؟ ستُزال من قائمة البحث العامة."
  When they click the confirm "Deactivate" button
  Then DELETE /account/api/admin/regions/{id} fires and the API returns 200
  And a green toast reads "Region deactivated." / "تم تعطيل المنطقة."
  And on reload the row no longer shows in the active-default grid
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-regions-golden-before.png`
- Screenshot after (add): `docs/screenshots/cp-admin-regions-golden-add.png`
- Screenshot after (edit modal prefill): `docs/screenshots/cp-admin-regions-golden-edit.png`
- Screenshot after (deactivated): `docs/screenshots/cp-admin-regions-golden-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/regions/*` call returns 200
- Audit rows: audit rows with `Event = 'region.created'`, then `'region.updated'`,
  then `'region.deactivated'`, each carrying the actor's id

### E2E-REGION-002 — Empty list / no-match search

```gherkin
Scenario: Empty grid renders SimfEmptyState
  Given the search matches no active Region rows
  When the administrator searches for a string no region matches (e.g. "zzzzz")
  Then POST /account/api/admin/regions/list returns 200 with an empty Items list
  And the grid body renders the SimfEmptyState titled "No regions found" / "لا توجد مناطق"
  And no error SimfAlert appears
  And the toolbar still shows the "New region" button
```

### E2E-REGION-003 — Search / filter reloads grid server-side

```gherkin
Scenario: Search box and per-column filters drive GridQuery and reset to the first page
  Given the grid shows the seeded regions
  When the administrator types "الرياض" into the Search field
  And clicks the "Search" button
  Then POST /account/api/admin/regions/list fires with Search="الرياض" and Skip=0
  And only rows matching Code / Arabic name / English name are shown

  When the administrator types "riyadh" into the "Filter column Code" input
  Then after the debounce POST /account/api/admin/regions/list fires
      with GridQuery.Filters["code"]="riyadh" and Skip reset to 0
  And the grid narrows to the matching row(s)

  When they clear the Search field and the column filter and click "Search" again
  Then the request fires with Search=null and no "code" filter key, and the full grid returns
```

> Supported per-column filter keys (lowercased): `code`, `name` (⇒ NameArabic),
> `nameen`, `isactive`. The toolbar Search box is a `LIKE` across Code / Arabic
> name / English name; both Search and a column filter reset `Skip` to 0.

### E2E-REGION-004 — Seeded baseline (13 official regions)

```gherkin
Scenario: A fresh database carries the 13 official Saudi regions
  Given the RegionSeeder has run during API start-up (it runs idempotently in ALL environments)
  When the administrator opens /admin/regions with no search/filter
  Then the grid lists 13 active regions in ascending SortOrder:
      riyadh, makkah, madinah, eastern, asir, tabuk, hail, northern,
      jazan, najran, bahah, jawf, qassim
  And each row shows its Code, Arabic name (NameArabic), English name (Name) and Active "✓"
  And re-running the seeder (e.g. an API restart) inserts no duplicates and overwrites no admin edits
```

> Seed source: `SIMF.Common.SaudiRegions.All` (13 rows). The seeder is keyed on
> `Code` (OrdinalIgnoreCase) — it inserts only missing rows and never overwrites
> an existing row's edited names/sort order.

### E2E-REGION-005 — Auth gate (page permission)

```gherkin
Scenario: Admin lacking Regions.View is denied
  Given a signed-in admin whose roles do not grant Regions.View
  When they navigate to /admin/regions
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/regions/list request fires
```

### E2E-REGION-006 — Action gate (Create permission)

```gherkin
Scenario: Admin with View but not Create cannot add
  Given a signed-in admin whose roles grant Regions.View but not Regions.Create
  When they open /admin/regions
  Then the grid loads normally
  But the "New region" button is not rendered (AuthorizedAction hides it)
  And if Regions.Edit / .Delete are missing, the per-row Edit (pencil) /
      Delete (trash) icon actions in the grid are hidden
```

### E2E-REGION-007 — Client validation (blank Arabic name)

```gherkin
Scenario: Blank Arabic name shows a bilingual error before any request
  Given the Add modal is open
  When the administrator fills Code="x" but leaves Name (Arabic) blank
  And clicks "Save"
  Then a red SimfAlert appears reading "Arabic name is required." / "الاسم بالعربية مطلوب."
  And the modal stays open
  And no POST /account/api/admin/regions request fires
```

### E2E-REGION-008 — Server validation (over length)

```gherkin
Scenario: Arabic name over 256 / Code over 16 returns 400 REGION_INVALID
  Given the Add modal is open
  When the administrator fills Name (Arabic) with 257 characters (or Code with 17 characters)
  And clicks "Save"
  Then POST /account/api/admin/regions is forwarded
  And the API returns HTTP 400 with ApiResult.Error.Code = "REGION_INVALID"
  And the modal stays open
  And the error toast surfaces the bilingual MessageForCurrentCulture()
      naming the offending field and its 1–16 (Code) / 1–256 (Arabic name) limit
```

> Note: the UI fields carry `MaxLength="16"` (Code) and `MaxLength="256"`
> (Name / NameArabic), so reproducing this typically requires programmatically
> setting the value past the cap (e.g. via `evaluate_script`) to exercise the
> server guard rather than relying on keyboard entry.

### E2E-REGION-009 — Conflict (duplicate Code)

```gherkin
Scenario: Duplicate Code returns 409 REGION_INVALID
  Given a region with Code="riyadh" already exists (seeded)
  When the administrator opens the Add modal
  And fills Code="riyadh" and Name (Arabic)="منطقة مكررة"
  And clicks "Save"
  Then the BFF forwards POST /admin/regions
  And the API returns HTTP 409 with ApiResult.Error.Code = "REGION_INVALID"
  And the modal stays open
  And the error toast reads the bilingual MessageForCurrentCulture()
      naming the clashing code 'riyadh'
```

### E2E-REGION-010 — Delete confirm cancelled

```gherkin
Scenario: Cancelling the SimfConfirm does not deactivate
  Given the grid shows at least one region
  When the administrator clicks the row's Delete (trash) action in the grid
  Then the View/Delete form opens (CrudShell) with a red "Deactivate" button
  When they click "Deactivate" and then click "Cancel" on the SimfConfirm dialog
  Then no DELETE /account/api/admin/regions/{id} request fires
  And the row remains unchanged and active
  And no toast appears
```

### E2E-REGION-011 — Not found

```gherkin
Scenario: Editing or deleting a missing id returns 404 REGION_NOT_FOUND
  Given a region id that does not exist (e.g. a hard-deleted / never-created Guid)
  When the administrator triggers GET /account/api/admin/regions/{id} for that id
  Then the API returns HTTP 404 with ApiResult.Error.Code = "REGION_NOT_FOUND"
  And the CP surfaces the bilingual not-found toast and re-shows the grid
  When a PUT or DELETE is forwarded for the same id
  Then the API also returns 404 REGION_NOT_FOUND (DELETE is otherwise idempotent for live rows)
```

### E2E-REGION-012 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the fallback bilingual toast
  Given the API is forced to return 500 on /admin/regions/list (e.g. DB unavailable)
  When the administrator opens /admin/regions
  Then the grid shows the loading text "Loading…" / "جارٍ تحميل المناطق…"
  And then a red toast reads "Could not load regions." / fallback bilingual message
  And no rows render
```

### E2E-REGION-013 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page, grid and both modals
  Given the administrator is on /admin/regions in English
  When they switch the UI language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "المناطق"
  And the toolbar Search button reads "بحث" and the actions appear in reverse order
  And the grid column headers read "الرمز", "الاسم (عربي)", "الاسم (إنجليزي)", "الترتيب", "نشط"

  When they click "إضافة منطقة"
  Then the Add modal opens in RTL titled "إضافة منطقة"
  And the field labels render in Arabic (e.g. "الرمز", "الاسم (عربي)")
  And the footer "إلغاء" / "حفظ" buttons appear in reverse order
```

### E2E-REGION-014 — Column sort toggles

```gherkin
Scenario: Clicking a sortable header toggles ascending / descending
  Given the grid shows the seeded regions in the default order (SortOrder, then NameArabic)
  And the "Code", "Name (Arabic)" and "Active" headers are sortable
  When the administrator clicks the "Code" column header
  Then POST /account/api/admin/regions/list fires with Sort="code",
      SortDescending=false and Skip reset to 0
  And the header renders aria-sort="ascending"
  When they click the "Code" header again
  Then the request fires with Sort="code", SortDescending=true
      and the header renders aria-sort="descending"
  When they instead click the "Name (Arabic)" header
  Then the request fires with Sort="name", SortDescending=false
      (switching column resets the direction to ascending)
```

> Note: with no explicit sort, the service default is `SortOrder` ascending then
> `NameArabic` — the same order the public app picker uses.

### E2E-REGION-015 — App picker parity (`GET /app/regions`)

```gherkin
Scenario: The public app endpoint returns the active regions ordered by SortOrder
  Given a signed-in app/account caller (the endpoint requires sign-in, rate-limit "auth";
      it is NOT admin-gated, NOT approval-gated, NOT AllowAnonymous)
  When they call GET /app/regions
  Then the API returns 200 with ApiResult<IReadOnlyList<RegionPickerItem>>
  And each item carries (Code, Name, NameArabic) only
  And the list contains only active regions, ordered by SortOrder then NameArabic
  When an admin deactivates a region in the CP (E2E-REGION-001)
  Then a fresh GET /app/regions no longer returns that region
  When an unauthenticated caller hits GET /app/regions
  Then the API returns 401 (sign-in required), not 200
```

### E2E-REGION-016 — Delete confirmation gate (CrudShell + SimfConfirm)

```gherkin
Scenario: Deactivate requires explicit SimfConfirm — Cancel skips, confirm fires exactly one DELETE
  Given the administrator is on /admin/regions with at least one region
      (e.g. Name (Arabic)="منطقة تجريبية")
  When they click the row's Delete (trash) action in the grid
  Then GET /account/api/admin/regions/{id} fires to load the full detail
  And the RegionViewDelete form opens in CrudShell showing the read-only details
      and a red "Deactivate" button — NOT a native window.confirm()
  When they click "Deactivate"
  Then a SimfConfirm dialog appears titled "Deactivate region" / "تعطيل المنطقة"
  And its message reads "Deactivate “منطقة تجريبية”? It will be removed from the public lookup."
      / "تعطيل «منطقة تجريبية»؟ ستُزال من قائمة البحث العامة."
  When they click "Cancel" on the SimfConfirm
  Then no DELETE request fires and the form stays open with the row unchanged
  When they click "Deactivate" again and then the confirm "Deactivate" button
  Then exactly one DELETE /account/api/admin/regions/{id} fires and returns 200
  And the form closes, the grid reloads, and a green toast reads
      "Region deactivated." / "تم تعطيل المنطقة."
  And the row's Active pill turns grey "Inactive"
```

---

## Implementation notes

- **Manual smoke is canonical today.** Until Playwright is adopted, the canonical
  run is a Chrome DevTools MCP session: sign in per the Auth setup, walk each
  scenario, and capture screenshots into `docs/screenshots/cp-admin-regions-*.png`.
  Keep the Gherkin runner-agnostic so each scenario copies cleanly into a
  `.feature` + step-definition class under a future `tests/SIMF.E2E.Tests/`.
- **Error-code source of truth:** `ErrorCodes.RegionInvalid = "REGION_INVALID"`
  (400 validation + 409 duplicate Code), `ErrorCodes.RegionNotFound =
  "REGION_NOT_FOUND"` (404). Audit event keys: `region.created` /
  `region.updated` / `region.deactivated`.
- **No Excel import/export.** Unlike Organisations, Regions has no bulk Excel
  import and no generic grid export — the seeded 13-row baseline plus manual CRUD
  is the whole surface. Do not assume an Import/Export toolbar action.
- **Seeder runs in all environments.** `RegionSeeder` is invoked unconditionally
  after migration on every start-up (modelled on `RatingSeeder` / `IdentitySeeder`,
  NOT the dev-only `OrganisationSeeder` guard), so the 13 regions exist in prod too.

---

_Last reviewed:_ 2026-06-30 by Claude (D-547 — Region lookup CP page + app picker; catalogue authored).
