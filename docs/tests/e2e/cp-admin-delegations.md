# E2E test catalogue — Delegations CRUD (`/admin/delegations`)

| | |
|--|--|
| **Page** | [`cp/admin-delegations.md`](../../pages/cp/admin-delegations.md) |
| **Route** | `/admin/delegations` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-03 |

> **D-256 grid conversion.** This page now renders the owner-mandated `SimfDataGrid`
> (server-paged, page size **20**, full pager) instead of the old raw `<table>`. The
> row actions are quiet **icon** buttons in the grid's row-end cell — an Edit (pencil)
> icon (`OnEditOne`) and a Delete/Deactivate (trash) icon (`OnDeleteOne`) — and the
> toolbar **Add** ("+") icon opens the Create modal (`OnAdd`). The `name`, `nameArabic`
> and `country` columns carry per-column filter inputs; every column is sortable. The
> grid sets `Multiselect="true"` (select-all + per-row checkboxes) but **no bulk
> toolbar action is wired** — selection is cosmetic on this page, so there is no
> bulk-delete/approve scenario.

> **Page permission:** `@attribute [RequirePermission(PermissionCatalog.Delegations.View)]`
> (`"Delegations.View"`). The API also enforces `Delegations.Create` / `Delegations.Edit` /
> `Delegations.Delete` per action, all baselined `AdminOnly` and seeded idempotently. The
> CP page itself does **not** wrap the Add / Edit / Delete grid affordances in
> `<AuthorizedAction>` — a `View`-only admin therefore sees the icon actions but the API
> will reject the write with HTTP 403 (covered by E2E-DLG-009).

> **No uniqueness constraint.** The service (`AdminDelegationService`) does **not** check
> name uniqueness — two delegations may share the same EN/AR name by design (a country can
> field more than one delegation). The classic "duplicate → 409" scenario does **not**
> apply here; E2E-DLG-005 instead covers the real conflict surface: a stale row whose
> delegation was deleted by another admin → `DELEGATION_NOT_FOUND` (404).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-DLG-001 | Full CRUD round-trip — Add → list → Edit → Deactivate | happy | P0 | _to author_ |
| E2E-DLG-002 | Add with all optional fields — Country + Priority + International + Member count + Display order | happy | P1 | _to author_ |
| E2E-DLG-003 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-DLG-004 | Validation: blank Name (EN) → `DELEGATION_INVALID` 400 + bilingual error toast | error | P1 | _to author_ |
| E2E-DLG-005 | Conflict: edit/deactivate a row deleted by another admin → `DELEGATION_NOT_FOUND` 404 | error | P1 | _to author_ |
| E2E-DLG-006 | Deactivate confirm dialog — Cancel aborts, OK soft-deletes | happy | P1 | _to author_ |
| E2E-DLG-007 | Edit modal pre-fills row values incl. the `Active` checkbox | happy | P1 | _to author_ |
| E2E-DLG-008 | Auth gate: signed-in admin lacking `Delegations.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-DLG-009 | Per-action gate: `View`-only admin Save → API 403 + error toast | auth | P1 | _to author_ |
| E2E-DLG-010 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-DLG-011 | Countries dropdown load failure → bilingual warning toast, form still opens | resilience | P2 | _to author_ |
| E2E-DLG-012 | Member count / Display order numeric guards (negative → 400, large clamped by input) | error | P2 | _to author_ |
| E2E-DLG-013 | Cancel the Add/Edit modal — no write fires | happy | P2 | _to author_ |
| E2E-DLG-014 | RTL / Arabic render mirrors page + modal | i18n | P1 | _to author_ |
| E2E-DLG-015 | Per-column grid filter (Name / Country) narrows the grid + resets Skip | happy | P1 | _to author_ |
| E2E-DLG-016 | Column sort toggles (Name asc → desc) | happy | P2 | _to author_ |

## Scenarios

### E2E-DLG-001 — Full CRUD round-trip

```gherkin
Feature: Delegations CRUD round-trip
  As an Administrator
  I want to manage the public list of forum delegations
  So that the Website "Delegations" page (Mockup page 21) stays accurate

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator with the Delegations.View/Create/Edit/Delete permissions has
      signed in via /login + /login/totp (TOTP from the Get-Totp helper)
  And they have navigated to /admin/delegations
  And the SimfBanner title reads "Delegations"
  And the page issues POST /account/api/admin/delegations/list (HTTP 200)

Scenario: Create, list, edit, then deactivate one delegation
  Given the grid currently shows N rows (or the SimfEmptyState if N = 0)
  When the administrator clicks the grid toolbar Add ("+") action
  Then the Create modal opens titled "Create delegation"
  And it shows the fields: Name (English), Name (Arabic), Country (select, default "(none)"),
      Member count (number, default 0), "Priority (sorted first in the public list)" checkbox,
      "International" checkbox, "Display order (lower = earlier)" (number, default 0)
  And the modal does NOT show an "Active" checkbox (Create always lands IsActive=true)
  When they fill Name (English) = "Royal Saudi Naval Forces"
  And they fill Name (Arabic) = "القوات البحرية الملكية السعودية"
  And they leave Country = "(none)"
  And they fill Member count = "12"
  And they leave Display order = "0"
  And they click "Save"
  Then the BFF forwards POST /account/api/admin/delegations and the API returns HTTP 200
  And the modal closes
  And a green toast reads "Delegation saved." ("تم حفظ الوفد." in Arabic)
  And the grid reloads and shows N + 1 rows
  And a row exists with Name="Royal Saudi Naval Forces", Members=12, Order=0 and Active="✓"

  When the administrator clicks the row's Edit (pencil) icon action
  Then the Edit modal opens titled "Edit delegation" with the row values pre-filled
  And an "Active" checkbox is now visible and ticked
  When they tick "Priority (sorted first in the public list)"
  And they change Display order to "5"
  And they click "Save"
  Then the BFF forwards PUT /account/api/admin/delegations/{id} and the API returns HTTP 200
  And the modal closes
  And a green toast reads "Delegation saved." ("تم حفظ الوفد.")
  And the row now shows Priority="✓" and Order=5

  When the administrator clicks the row's Delete/Deactivate (trash) icon action
  Then a browser confirm() dialog appears reading
      "Deactivate this delegation? It will disappear from the public list immediately."
  When they accept the dialog
  Then the BFF forwards DELETE /account/api/admin/delegations/{id} and the API returns HTTP 200
  And a green toast reads "Delegation deactivated." ("تم تعطيل الوفد.")
  And the grid reloads; the row now shows Active="—"
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-delegations-golden-before.png`
- Screenshot after (create): `docs/screenshots/cp-admin-delegations-golden-created.png`
- Screenshot after (edit): `docs/screenshots/cp-admin-delegations-golden-edited.png`
- Screenshot after (deactivate): `docs/screenshots/cp-admin-delegations-golden-deactivated.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/delegations/*` and `/account/api/admin/countries/list` call returns 200
- Audit rows: one `Delegation.Created`, one `Delegation.Updated`, one `Delegation.Deactivated` row with the actor's id (table backing `IAuditLog`)

### E2E-DLG-002 — Add with all optional fields

```gherkin
Scenario: Create a priority international delegation linked to a country
  Given the Create modal is open
  And the countries dropdown loaded at mount (POST /account/api/admin/countries/list 200)
  When they fill Name (English) = "Hellenic Navy"
  And they fill Name (Arabic) = "البحرية اليونانية"
  And they select Country = "Greece — اليونان" from the dropdown
  And they fill Member count = "8"
  And they tick "Priority (sorted first in the public list)"
  And they tick "International"
  And they fill Display order = "1"
  And they click "Save"
  Then the POST body carries CountryId = <Greece id>, MemberCount = 8,
      IsPriority = true, IsInternational = true, DisplayOrder = 1
  And the API returns HTTP 200
  And a green toast reads "Delegation saved."
  And the new row shows Country="Greece", Members=8, Priority="✓", International="✓", Order=1
```

### E2E-DLG-003 — Empty list

```gherkin
Scenario: No delegations renders the SimfEmptyState
  Given the Delegations table is empty (0 rows after any active/priority filter)
  When the administrator opens /admin/delegations
  Then POST /account/api/admin/delegations/list returns HTTP 200 with Total = 0
  And the SimfEmptyState renders with the title "No delegations yet." ("لا توجد وفود حتى الآن.")
  And the toolbar still shows the Add ("+") icon action
  And no error toast appears
```

### E2E-DLG-004 — Validation: blank Name

```gherkin
Scenario: Blank English name is rejected by the API with a bilingual message
  Given the Create modal is open
  When the administrator leaves Name (English) blank
  And fills Name (Arabic) = "اسم تجريبي"
  And clicks "Save"
  Then the BFF forwards POST /account/api/admin/delegations (the page has NO client-side
      name guard, so the request fires)
  And the API returns HTTP 400 with ApiResult.Error.Code = "DELEGATION_INVALID"
  And the modal stays open
  And a red toast surfaces the bilingual MessageForCurrentCulture():
      "Delegation name (EN + AR) must be between 1 and 256 characters."
      ("يجب أن يتراوح طول اسم الوفد (إنجليزي + عربي) بين 1 و 256 حرفاً.")
  And the grid is unchanged
```

### E2E-DLG-005 — Conflict: row deleted by another admin (stale row)

```gherkin
Scenario: Editing a delegation another admin already deleted returns 404
  Given the grid shows a delegation row "Stale Delegation"
  And in another session a second admin has deactivated/removed that delegation's id
  When the administrator clicks the "Stale Delegation" row's Edit (pencil) icon action
  And changes Display order to "9"
  And clicks "Save"
  Then the BFF forwards PUT /account/api/admin/delegations/{id}
  And the API returns HTTP 404 with ApiResult.Error.Code = "DELEGATION_NOT_FOUND"
  And the modal stays open
  And a red toast reads "Delegation not found." ("لم يتم العثور على الوفد.")

Scenario: Deactivating an already-deactivated delegation is a no-op success
  Given a delegation row exists whose underlying record is already IsActive=false
  When the administrator clicks its Delete/Deactivate (trash) icon action and confirms
  Then the API returns HTTP 200 (DeactivateAsync early-returns when not active)
  And a green toast reads "Delegation deactivated."
```

### E2E-DLG-006 — Deactivate confirm dialog

```gherkin
Scenario: Cancelling the confirm dialog aborts the delete
  Given the grid shows a delegation "Cancel Me"
  When the administrator clicks that row's Delete/Deactivate (trash) icon action
  Then a confirm() dialog appears reading
      "Deactivate this delegation? It will disappear from the public list immediately."
  When they dismiss / cancel the dialog
  Then NO DELETE /account/api/admin/delegations/{id} request fires
  And the row stays Active="✓"
  And no toast appears

Scenario: Accepting the confirm dialog soft-deletes the row
  Given the grid shows a delegation "Delete Me"
  When the administrator clicks the row's Delete/Deactivate (trash) icon action and accepts the confirm dialog
  Then DELETE /account/api/admin/delegations/{id} returns HTTP 200
  And a green toast reads "Delegation deactivated."
  And the reloaded row shows Active="—"
```

### E2E-DLG-007 — Edit modal pre-fill

```gherkin
Scenario: Edit pre-fills every field from the grid row
  Given a delegation row: Name="Egyptian Navy", Name (Arabic)="البحرية المصرية",
      Country="Egypt", Members=6, Priority="✓", International="✓", Order=3, Active="✓"
  When the administrator clicks that row's Edit (pencil) icon action
  Then the Edit modal opens titled "Edit delegation"
  And Name (English) shows "Egyptian Navy"
  And Name (Arabic) shows "البحرية المصرية"
  And the Country select shows the matching country option
  And Member count shows 6
  And the "Priority …" checkbox is ticked
  And the "International" checkbox is ticked
  And Display order shows 3
  And the "Active" checkbox is visible and ticked
```

### E2E-DLG-008 — Auth gate (page permission)

```gherkin
Scenario: Admin lacking Delegations.View cannot open the page
  Given a signed-in admin assigned a role WITHOUT the Delegations.View permission
  When they navigate to /admin/delegations
  Then the RequirePermission attribute denies access
  And they land on /not-permitted with HTTP 200
  And no POST /account/api/admin/delegations/list request fires
  And the "Delegations" item is hidden from the CP nav rail
      (CpNavigation RequiredPermission = PermissionCatalog.Delegations.View)
```

### E2E-DLG-009 — Per-action gate (View-only admin)

```gherkin
Scenario: View-only admin can open the page but cannot save
  Given a signed-in admin with Delegations.View but NOT Delegations.Create/Edit
  When they open /admin/delegations
  Then the grid loads (POST /list returns 200)
  And the toolbar Add ("+") icon and the per-row Edit (pencil) / Delete (trash) icon
      actions are still rendered (the page does not wrap them in <AuthorizedAction>)
  When they click the Add ("+") action, fill valid data and click "Save"
  Then the BFF forwards POST /account/api/admin/delegations
  And the API returns HTTP 403 (policy PermissionCatalog.PolicyFor(Delegations.Create))
  And the modal stays open
  And a red toast surfaces the forbidden / fallback error message
```

### E2E-DLG-010 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the bilingual fallback toast
  Given the API is forced to return HTTP 500 on /admin/delegations/list (e.g. DB down)
  When the administrator opens /admin/delegations
  Then the page first shows "Loading…" ("جارٍ التحميل…")
  And then a red toast appears reading "Could not load delegations." ("تعذّر تحميل الوفود.")
  And no grid rows render
  And the toolbar Add ("+") icon action is still present
```

### E2E-DLG-011 — Countries dropdown load failure

```gherkin
Scenario: Countries list fails to load but the page and form still work
  Given the API is forced to fail /admin/countries/list (HTTP 500 or auth error)
  When the administrator opens /admin/delegations
  Then a red toast reads
      "Could not load countries list for the form. The country dropdown will be empty until reload."
      ("تعذّر تحميل قائمة الدول للنموذج. ستظل قائمة الدول فارغة حتى يتم إعادة التحميل.")
  And the delegations grid still loads normally (separate /list call)
  When they click the toolbar Add ("+") icon action
  Then the Create modal opens with the Country dropdown showing only "(none)"
  And they can still create a delegation with Country = "(none)"
```

### E2E-DLG-012 — Numeric field guards

```gherkin
Scenario: Negative member count is rejected by the API
  Given the Create modal is open with a valid Name (EN + AR)
  And the admin coerces Member count to a negative value (e.g. via DevTools, bypassing min=0)
  When they click "Save"
  Then the API returns HTTP 400 with ApiResult.Error.Code = "DELEGATION_INVALID"
  And a red toast reads "Member count must be zero or positive."
      ("يجب أن يكون عدد الأعضاء صفراً أو موجباً.")

Scenario: Negative display order is rejected by the API
  Given the Create modal is open with a valid Name (EN + AR)
  And the admin coerces Display order to a negative value (bypassing min=0)
  When they click "Save"
  Then the API returns HTTP 400 with ApiResult.Error.Code = "DELEGATION_INVALID"
  And a red toast reads "Display order must be zero or positive."
      ("يجب أن يكون ترتيب العرض صفراً أو موجباً.")

Scenario: Input attributes clamp the typed range
  Given the Create modal is open
  Then the Member count input carries min="0" max="100000"
  And the Display order input carries min="0" max="99999"
```

### E2E-DLG-013 — Cancel the modal

```gherkin
Scenario: Cancelling the Create modal fires no write
  Given the Create modal is open with Name (English) = "Discard Me" typed in
  When the administrator clicks "Cancel"
  Then the modal closes
  And NO POST /account/api/admin/delegations request fires
  And the grid is unchanged
  And no toast appears
```

### E2E-DLG-014 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors page + Create modal
  Given the administrator is on /admin/delegations in English
  When they switch the UI language to العربية from the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "الوفود"
  And the column headers read الاسم / الاسم (عربي) / الدولة / عدد الأعضاء / أولوية / دولي / الترتيب / مفعّل
  And the grid toolbar Add ("+") icon carries the title "إضافة" (Grid.Add)
  And the per-row icon actions carry the titles "تعديل" (Grid.Edit) / "حذف" (Grid.Delete)

  When they click the Add ("+") toolbar icon action
  Then the Create modal opens in RTL titled "إنشاء وفد"
  And the field labels are Arabic (الاسم (إنجليزي) / الاسم (عربي) / الدولة / عدد الأعضاء /
      أولوية (تظهر أولاً في القائمة العامة) / دولي / ترتيب العرض (الأصغر يظهر أولاً))
  And the footer shows "حفظ" and "إلغاء" in reverse order
```

### E2E-DLG-015 — Per-column grid filter narrows the grid

```gherkin
Scenario: Typing in the Name column filter narrows the grid and resets paging
  Given the grid lists at least two delegations across more than one page
      (page size 20, so Total > 20 shows a second page)
  And the administrator has paged to page 2 (GridQuery.Skip = 20)
  When they type "Royal" into the per-column filter input under the "Name" column
      (input aria-label "Filter column Name")
  Then after the 300 ms debounce the page issues
      POST /account/api/admin/delegations/list with GridQuery.Filters["name"] = "Royal"
  And GridQuery.Skip is reset to 0 (the filter returns to page 1)
  And the grid reloads showing only rows whose Name contains "Royal"
  And any prior row selection is cleared (the grid drops selection on a query change)

Scenario: The Country column filter resolves through the Country navigation
  Given the grid lists delegations linked to several countries
  When they type "Greece" into the per-column filter under the "Country" column
      (input aria-label "Filter column Country")
  Then the page issues POST /account/api/admin/delegations/list with
      GridQuery.Filters["country"] = "Greece" and Skip = 0
  And the grid narrows to delegations whose country NameEn/NameAr contains "Greece"

Scenario: Clearing the filter input restores the full grid
  Given the Name column filter currently holds "Royal"
  When the administrator clears the filter input
  Then the page issues POST /account/api/admin/delegations/list with the "name"
      key removed from GridQuery.Filters and Skip = 0
  And the grid reloads with the unfiltered result set
```

### E2E-DLG-016 — Column sort toggles

```gherkin
Scenario: Clicking the Name header toggles ascending then descending
  Given the grid lists several delegations (default order DisplayOrder asc, NameArabic asc)
  When the administrator clicks the sortable "Name" column header
  Then the page issues POST /account/api/admin/delegations/list with
      GridQuery.Sort = "name", SortDescending = false and Skip = 0
  And the grid re-renders ordered by Name ascending (the header shows the ▲ arrow)
  When they click the "Name" header again
  Then the page re-issues the list call with Sort = "name", SortDescending = true
  And the grid re-renders ordered by Name descending (the header shows the ▼ arrow)
  And switching to another sortable header (e.g. "Members") resets SortDescending to false
```

---

## Implementation notes

- **Manual smoke is the canonical run today.** Until Playwright is adopted, the canonical
  execution is a Chrome DevTools MCP session: sign in via the Background steps, walk each
  scenario, and capture screenshots into `docs/screenshots/cp-admin-delegations-*.png`.
- **No name-uniqueness check by design.** `AdminDelegationService.Validate` only enforces
  length (1–256 EN + AR), `MemberCount >= 0` and `DisplayOrder >= 0`. There is no duplicate
  guard, so the usual "duplicate → 409" case is intentionally absent — E2E-DLG-005 covers the
  real conflict surface (`DELEGATION_NOT_FOUND` on a stale row) instead.
- **Soft-delete, no undo.** "Deactivate" sets `IsActive = false` via DELETE; there is no
  reactivate button on this page (re-enable by Edit → tick "Active"). The destructive action
  is guarded by a JS `confirm()` dialog, so the runner must handle the dialog.
- **Sort order.** The admin grid's *default* order is `DisplayOrder` asc then `NameArabic`
  asc (the public list additionally sorts `IsPriority` desc first). After D-256 every column
  header is clickable to override that order (`GridQuery.Sort` / `SortDescending`, server-side
  in `AdminDelegationService`) — see E2E-DLG-016. Assert default ordering only where a scenario
  sets `DisplayOrder` explicitly and does not change the sort.
- **Per-column grid filters (D-256).** Only the `name`, `nameArabic` and `country` columns
  expose a per-column filter input; the service honours those three keys (plus `isActive` /
  `isPriority` if supplied) via `GridQuery.Filters` and ignores unknown columns. Typing
  debounces 300 ms, resets `Skip` to 0, and drops any selection — see E2E-DLG-015.
- **Per-action API gates.** `Delegations.Create/Edit/Delete` are enforced server-side even
  though the grid's Add ("+") toolbar icon and the per-row Edit (pencil) / Delete (trash)
  icon actions are unconditionally rendered (no `<AuthorizedAction>` wrapper on this page).
  E2E-DLG-009 documents the resulting 403 path; treat the missing action-level gate as a
  known UX gap, not a security hole (the API is the enforcement boundary).
- **API integration tests** at `tests/SIMF.Api.Tests/DelegationsTests.cs` cover the same
  CRUD + validation + not-found surface at a lower layer (no browser). Keep both during the
  Playwright transition.

---

_Last reviewed:_ 2026-06-03 by Claude (E2E catalogue rebuild) (D-256/D-257 grid affordances reconciled).
