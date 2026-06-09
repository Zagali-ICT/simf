# Interests CRUD — `/admin/interests`

| | |
|--|--|
| **Route** | `/admin/interests` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel |
| **Audience** | Administrator |
| **Auth** | `[Authorize(Roles = "Administrator")]` + cookie-auth session + JWT bearer forwarded by BFF |
| **Pattern** | D-117 canonical CRUD + D-132 Multiselect / SimfBanner + D-353 centralized dialog/full-page framing (reference pilot) |
| **Status** | ✅ Real |
| **Implements use case(s)** | UC-INT-LIST, UC-INT-CREATE, UC-INT-EDIT, UC-INT-VIEW, UC-INT-DEACTIVATE _(to be authored under `SIMF-UCS-001`)_ |
| **Backend endpoints** | `POST /account/api/admin/interests/list`, `POST /account/api/admin/interests`, `PUT /account/api/admin/interests/{id}`, `DELETE /account/api/admin/interests/{id}` |
| **Source file** | [`InterestsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/InterestsList.razor) + the two reusable forms [`InterestAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/InterestAddEdit.razor) and [`InterestViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/InterestViewDelete.razor), framed by the shared `CrudShell` (D-353). _(`InterestForm.razor` was renamed to `InterestAddEdit.razor`.)_ |
| **Tests** | [`docs/tests/e2e/cp-admin-interests.md`](../../tests/e2e/cp-admin-interests.md) |
| **Last reviewed** | 2026-06-09 |

---

## 1. Purpose

Interests is a small admin-managed lookup table that visitors pick from when
they fill their profile (P9 — D-050). An interest has a bilingual name
(English + Arabic), a display order, and an active / inactive state. The page
exists so an administrator can keep the visitor-facing picker accurate without
a code change: add a new topic when a stream is announced, deactivate one that
stopped being relevant, reorder them so the most popular sit at the top of the
picker. It is **lookup-table CRUD** — no relationships beyond the
visitor-profile linking table, no workflow, no approvals. Every change takes
effect on the visitor-facing picker the next time it loads.

## 2. Audience + permissions

- **Who can reach it:** Administrator (only role with the `Administrator` CP role).
- **Who can edit/write on it:** same — every row action is admin-only.
- **Authorisation gates:**
  - Razor: `@attribute [Authorize(Roles = "Administrator")]` on `InterestsList.razor`.
  - BFF: `/account/api/admin/interests/*` routes guarded by the same role
    + `RequireApprovedAccount` (AccountState must be `Approved` — pending
    admins are blocked).
  - API: `[Authorize(Policy = AuthorizationPolicies.AdministratorOnly)]`
    + the same approved-account requirement + the `RequireRateLimiting("auth")`
    bucket so brute interactions can't drown the endpoint.
- **What an unauthenticated user sees:** redirect to `/login` via the cookie
  challenge; an authenticated non-admin sees the standard `/not-permitted`
  fallback (403).

## 3. Screenshots

| State | File | Captured |
|-------|------|----------|
| Default (with rows) | `docs/screenshots/d132-interests-canonical.png` | 2026-05-28 |
| Add modal | `docs/screenshots/d132-interests-add-modal.png` | 2026-05-28 |
| Grid (RTL, D-353) | `docs/screenshots/d353-interests-grid-rtl.png` | 2026-06-09 |
| Add — full page (D-353) | `docs/screenshots/d353-interests-add-fullpage-rtl.png` | 2026-06-09 |
| Add — dialog (D-353) | `docs/screenshots/d353-interests-add-dialog-rtl.png` | 2026-06-09 |
| Deactivate confirmation (D-353) | `docs/screenshots/d353-interests-delete-confirm-rtl.png` | 2026-06-09 |
| Edit modal | _to capture_ | — |
| Details modal | _to capture_ | — |
| Empty state (no interests) | _to capture_ | — |
| RTL (Arabic) | _to capture_ | — |
| Error state (server 500 on list) | _to capture_ | — |

## 4. UI affordances

### 4.1 Banner

`<SimfBanner Title="@L[\"Admin.Interests.Title\"]" />` — title only, no
subtitle, no Actions slot. Title resx: EN "Interests", AR "الاهتمامات".

### 4.2 Toolbar

| Button | Wired callback | Calls | Notes |
|--------|----------------|-------|-------|
| **Select all** | `ToggleSelectAllAsync` (built into `SimfDataGrid`) | — | Multiselect=true mandatory per D-132 |
| **Open as dialog / full page** | `CrudPresentationToggle` (`@bind-Value="_presentation"`) | persists `simf.cp.prefs.interests` via `CpPreferences` | D-353 — picks how the four forms below open; default dialog |
| **Add** | `OnAddAsync` | `_form = AddEdit; _isEdit = false` → `CrudShell` hosts `<InterestAddEdit IsEdit="false" />` | popup or full page per the toggle |
| **Edit** | `OnEditAsync(row)` | `_form = AddEdit; _isEdit = true; _target = row` → `<InterestAddEdit IsEdit="true" Initial="@row" />` | no extra GET — the row already has every editable field |
| **Details** | `OnDetailsAsync(row)` | `_form = ViewDelete; _isDelete = false; _target = row` → `<InterestViewDelete IsDelete="false" />` | read-only `<dl>` of every field |
| **Deactivate** | `OnDeleteAsync(row)` | `_form = ViewDelete; _isDelete = true; _target = row` → `<InterestViewDelete IsDelete="true" />` → `SimfConfirm` → `DELETE /account/api/admin/interests/{id}` | D-353 — now shows the record + a **confirmation** before the soft-delete (was one-click) |

Bulk-delete (`OnDeleteSelected`) is intentionally **not wired**: deactivation is
a per-row destructive action and a bulk-deactivate UX would be more dangerous
than useful for a 5–30-row lookup table. Copy / Paste / Duplicate / Import /
Export are also unwired — domain doesn't need them.

### 4.3 Grid columns

| Column | Source field | Sortable | Filterable | Notes |
|--------|--------------|----------|------------|-------|
| Name | `r.Name` | yes | yes | English label |
| Name (Arabic) | `r.NameArabic` | yes | yes | Arabic label |
| Order | `r.DisplayOrder` | yes | no | integer ≥ 0 |
| Status | `r.IsActive` | no | no | `SimfPill` — green `Active` / grey `Inactive` |

### 4.4 Pager

- First / Prev / numbered (5-wide window) / Next / Last
- Page-size selector: 10 / 20 / 50 / 100 (default 20)
- Caption: EN "Showing X–Y of Z" / AR "عرض X–Y من Z"
- Page label: EN "Page X of Y" / AR "الصفحة X من Y"

### 4.5 Form fields (Add + Edit, via `InterestAddEdit.razor`)

| Field | Type | Required | MaxLength | Validation | Locale |
|-------|------|----------|-----------|------------|--------|
| Name (English) | text | yes | 128 | 1–128 chars, unique | `Admin.Interests.Field.Name` + `…NameHint` + `…NameInvalid` |
| Name (Arabic) | text | yes | 128 | 1–128 chars | `Admin.Interests.Field.NameArabic` + `…NameArabicHint` + `…NameArabicInvalid` |
| Display order | number | yes | n/a | integer ≥ 0 | `Admin.Interests.Field.DisplayOrder` + `…Hint` + `…Invalid` |
| Active (Edit only) | checkbox | no | n/a | bool — toggles `IsActive` | `Admin.Interests.Field.IsActive` |

The DisplayOrder field uses `Value`/`ValueChanged` + `ValueExpression` (not
`@bind-Value`) because it parses to `int` only at submit time — the in-flight
string lives in `_displayOrderInput`. Without `ValueExpression`, an
`EditContext`-bound `EditForm` crashes with `InputText requires a value for
the 'ValueExpression' parameter`. _(This was the D-132 mid-flight bug; the fix
lives in `InterestAddEdit.razor`.)_

### 4.6 Presentation: dialog vs full page (D-353)

The four forms above are hosted by the shared `CrudShell`, which frames the
same form either as a centred popup (`CrudDialogFrame` over `SimfModal`) or as
a full-width in-place panel (`CrudPageFrame`) that replaces the grid until the
user saves or closes — **same route, no navigation**. The `CrudPresentationToggle`
in the toolbar flips between the two and persists the choice per browser in
`localStorage` (`simf.cp.prefs.interests`) via the `CpPreferences` service; the
default is dialog, so behaviour is unchanged until the admin opts in. The user
can wipe every saved choice from **Profile → Display preferences → Clear saved
layout**. The two forms expose the standard CRUD parameter surface from
`CrudAddEditFormBase<T>` / `CrudViewDeleteFormBase<T>`. This page is the
reference implementation; the same pattern rolls out to the other CP list pages
(see the [CRUD-frame dev guide](../../manuals/SIMF-Crud-Frame-Dev-Guide.md)).

## 5. Data flow

```
Administrator clicks Add
  → OnAddAsync() sets _form = AddEdit (_isEdit = false)
  → <CrudShell> frames <InterestAddEdit IsEdit="false" /> as a popup or full page (per the toggle)
  → admin fills 3 fields, clicks Create
  → HandleSubmitAsync() validates client-side
  → JS interop: simfAccount.postJson("/account/api/admin/interests", AdminCreateInterestRequest)
  → CP BFF forwards to API with the bearer token (D-121 refresh hook keeps it fresh)
  → API endpoint POST /api/v1/admin/interests
  → AdminInterestService creates the row (transactional, row-audited via D-109 interceptor)
  → ApiResult<AdminInterestSummary>
  → CP form calls OnSuccess(created)
  → list reloads, success toast "Interest 'X' was created."
```

| When | Method + path | Request body | Response shape |
|------|---------------|--------------|----------------|
| Page init | `POST /account/api/admin/interests/list` | `GridQuery` (Top=20, Skip, Sort, Filters) | `ApiResult<GridPage<AdminInterestSummary>>` |
| Toolbar Add → Submit | `POST /account/api/admin/interests` | `AdminCreateInterestRequest { Name, NameArabic, DisplayOrder }` | `ApiResult<AdminInterestSummary>` |
| Per-row Edit → Submit | `PUT /account/api/admin/interests/{id}` | `AdminUpdateInterestRequest { Name, NameArabic, DisplayOrder, IsActive }` | `ApiResult<AdminInterestSummary>` |
| Per-row Deactivate | `DELETE /account/api/admin/interests/{id}` | — | `ApiResult<bool>` |

## 6. Validation + error handling

- **Client-side guards** (`InterestAddEdit.HandleSubmitAsync`):
  - `Name`: not whitespace, 1–128 chars
  - `NameArabic`: not whitespace, 1–128 chars
  - `DisplayOrder`: integer ≥ 0
- **Server-side validation:** `AdminCreateInterestRequestValidator` /
  `AdminUpdateInterestRequestValidator` (FluentValidation). Server is the
  canonical rule set — client is the UX layer.
- **Error envelope:** standard `ApiResult<T>` with `Error.Code` from
  `ErrorCodes` (e.g. `InterestNameNotUnique`, `ValidationFailed`,
  `NotFound`) and bilingual `Message` / `MessageArabic`.
- **Toast strategy** (`Toast` record in code-behind):
  - Success: `Admin.Interests.Created` / `…Updated` / `…Deactivated` (green)
  - Error: server envelope message via `MessageForCurrentCulture()`,
    fallback `Admin.Interests.Fallback` (red)

## 7. Edge cases + known limitations

- **Empty list:** `<EmptyTemplate><SimfEmptyState Title="@L[\"Admin.Interests.None\"]" /></EmptyTemplate>` renders when no rows.
- **Duplicate name:** the unique index on `Interest.Name` enforces this at the
  DB; the server returns 409 + `ErrorCodes.InterestNameNotUnique` and the toast
  surfaces the bilingual message.
- **Deactivating an in-use interest:** allowed. Visitors who already linked
  to it keep the link; the picker just stops offering the deactivated
  interest to new visitors. Reactivating restores it.
- **DisplayOrder collision:** allowed; sort is stable but visually the order
  of equally-ordered rows depends on insertion order. The admin can fix by
  bumping one of them.
- **No deep-link Add page:** `/admin/interests/new` and `/admin/interests/{id}/edit`
  were deleted in D-132 per the lookup-CRUD precedent (D-118). The modal is
  the only way to create or edit; any bookmark to the old route will 404.
- **No bulk operations:** Multiselect renders the toolbar checkboxes for
  consistency with the canonical pattern, but no bulk callback is wired
  (see §4.2 above for the reasoning).
- **Concurrent edits:** EF Core optimistic concurrency is not configured on
  `Interest` today. Two admins editing the same row race on `last-write-wins` —
  not a typical workflow for a 5–30-row lookup table but documented here as a
  known limitation. If the row count ever grows or multiple operators start
  managing it, add a `RowVersion` column under a new decision (would touch
  the D-110 freeze).

## 8. i18n + RTL

- All visible strings come from `Strings.resx` (EN) + `Strings.ar.resx` (AR)
  via `IStringLocalizer<Strings> L` — EN/AR parity verified at 576 keys total
  in the D-132 audit.
- Toggle: the `العربية` / `English` link in the top header round-trips the
  current page with `culture=ar|en` query param.
- RTL: `<html dir="rtl" lang="ar">` set in the layout when the current UI
  culture is Arabic; the nav rail mirrors, table headers flip, the toolbar
  reverses, action buttons stay inside the row.

## 9. Accessibility

- **Keyboard:**
  - Tab order: Banner → toolbar → grid headers → filter inputs → row checkboxes
    → row action buttons → pager → page-size select.
  - Modal open: focus moves to the first form field; ESC closes the modal
    (provided by `SimfModal`).
  - Modal close: focus returns to the toolbar Add button.
- **Screen reader:**
  - `SimfDataGrid` `Caption` is announced when the grid receives focus.
  - Each row checkbox has an `aria-label` like "Select row {Name}".
  - The Select-all checkbox has an `aria-label` "Select all".
- **Colour contrast:** WCAG AA throughout via `theme.tokens.css`; the
  `Active`/`Inactive` pills meet AA on both the light and dark themes.
- **Focus indicators:** the `--focus-ring` token is visible on every
  focusable element (button, link, input, checkbox, select).

## 10. Related use cases (UCS-001)

| UC ID | Title | Notes |
|-------|-------|-------|
| UC-INT-LIST | List + filter + sort interests | _to be authored_ |
| UC-INT-CREATE | Add a new interest | _to be authored_ |
| UC-INT-EDIT | Edit an interest | _to be authored_ |
| UC-INT-VIEW | View interest details (read-only) | _to be authored_ |
| UC-INT-DEACTIVATE | Deactivate an interest | _to be authored_ |

## 11. Related E2E test scenarios

Authored at [`docs/tests/e2e/cp-admin-interests.md`](../../tests/e2e/cp-admin-interests.md).

| Scenario | ID | Coverage |
|----------|----|----------|
| Golden: sign in → Add → see new row → Edit → see update → Deactivate → see Inactive pill | E2E-INT-001 | full CRUD round-trip |
| Empty list: fresh tenant → `SimfEmptyState` renders | E2E-INT-002 | empty path |
| Auth: non-admin signed-in user navigates → redirected to `/not-permitted` | E2E-INT-003 | role gate |
| Validation: submit empty Name → toast `Admin.Interests.Field.NameInvalid` | E2E-INT-004 | validation surface |
| Duplicate name: create "X" twice → second submit returns 409, toast shows bilingual server message | E2E-INT-005 | conflict envelope |
| Server error: API returns 500 on `/list` → toast `Admin.Interests.LoadFailed` | E2E-INT-006 | resilience |
| RTL: toggle Arabic → page mirrors, nav reverses, table headers flip, Add modal renders RTL | E2E-INT-007 | i18n |

## 12. Related docs

- Admin Manual chapter: [`Admin-Manual.md#interests`](../../manuals/Admin-Manual.md#interests)
- Pattern doc: [`SIMF_TABLE_PATTERN.md`](../../dev/SIMF_TABLE_PATTERN.md) — this page is the reference implementation of the D-132-extended canonical CRUD pattern.
- Architecture: [`SIMF-SAD-001`](../../SIMF-SAD-001-Software-Architecture-Document.md) — modular monolith / DDD layering.
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) — `ApiResult<T>` envelope, error model.
- Decisions log: [`DECISIONS_LOG.md`](../../decisions/DECISIONS_LOG.md) — D-050 (original), D-117 (canonical CRUD pattern), D-132 (migration).
- Source: `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/InterestsList.razor` + `InterestAddEdit.razor` + `InterestViewDelete.razor`, framed by `CrudShell` (D-353).
- Component catalogue: [`SIMF-CMP-001`](../../SIMF-CMP-001-Component-Catalog.md) — `SimfDataGrid`, `SimfBanner`, `SimfModal`, `SimfTextField`, `SimfCheckbox`, `SimfPill`, `SimfButton`, `SimfEmptyState`, `SimfAlert`.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-26 | D-050 (P9) | Original implementation: SimfDataGrid with navigate-to-page Add / Edit. |
| 2026-05-28 | D-132 | Migrated to canonical CRUD pattern: SimfBanner, Multiselect+RowKey, modal-based Add/Edit/Details (via new `InterestForm.razor` child), full pager labels, `EmptyTemplate`, `_displayOrderInput` `ValueExpression` fix. `CreateInterest.razor` + `EditInterest.razor` deleted; their routes 404 by design. |
| 2026-06-09 | D-353 | Reference pilot for the centralized CRUD framing: `InterestForm.razor` → `InterestAddEdit.razor` (now keys off `IsEdit`); new `InterestViewDelete.razor` (read-only details + confirmed deactivate); `InterestsList` rewired to one `CrudPresentationToggle` + one `CrudShell` per form-kind (dialog or full page, persisted per browser). Delete now shows the record + a `SimfConfirm` step (was one-click). |

---

_Last reviewed:_ 2026-05-28 by Claude (D-132 / D-133 vertical slice).
If the page has changed and this doc has not been re-reviewed in 60 days, it is
**out of date**. Re-walk the page in a browser and update every section that
drifted.
