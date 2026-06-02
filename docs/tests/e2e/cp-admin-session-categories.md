# E2E test catalogue — Session categories (`/admin/session-categories`)

| | |
|--|--|
| **Page** | [`cp/session-categories.md`](../../pages/cp/session-categories.md) |
| **Route** | `/admin/session-categories` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Page background.** B9b (D-226) — CP management for the dynamic session-category
> lookup (SIMF-FDS-004 §5.4). A small bilingual lookup (NameEn / NameAr / display
> order / active) that backs the category picker on the session form. The table
> **ships empty** and is seeded by the team once the client confirms the list
> (open item OI-2), so the empty-state path is the default first render. Mirrors
> `BoothsList` / the Organisation lookup. **RequiredPermission:** the page is gated
> by `PermissionCatalog.SessionCategories.View`; the toolbar/row actions are gated
> by `.Create` / `.Edit` / `.Delete` (all `AdminOnly` baseline).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-SCT-001 | Full CRUD round-trip — Add → Edit (toggle Active off) → Delete | happy | P0 | _to author_ |
| E2E-SCT-002 | Empty list renders `SimfEmptyState` ("No session categories yet.") | happy | P1 | _to author_ |
| E2E-SCT-003 | Auth: signed-in admin lacking `SessionCategories.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-SCT-004 | New-category button: opens Add modal with 4 fields | function | P1 | _to author_ |
| E2E-SCT-005 | Edit button: pre-fills modal from GET detail + shows Active checkbox | function | P1 | _to author_ |
| E2E-SCT-006 | Delete button: native confirm → soft-delete (row flips to "—") | function | P1 | _to author_ |
| E2E-SCT-007 | Delete cancelled at confirm dialog → no request, no change | function | P2 | _to author_ |
| E2E-SCT-008 | Cancel button in modal closes it without saving | function | P2 | _to author_ |
| E2E-SCT-009 | Validation: blank NameEn or NameAr → client "Both names are required." | error | P1 | _to author_ |
| E2E-SCT-010 | Validation: name > 128 chars → API 400 `SESSION_CATEGORY_INVALID` | error | P1 | _to author_ |
| E2E-SCT-011 | Display-order field accepts integers; non-numeric coerces to 0 | function | P2 | _to author_ |
| E2E-SCT-012 | Action-level permission gating (Create/Edit/Delete buttons hidden) | auth | P1 | _to author_ |
| E2E-SCT-013 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-SCT-014 | RTL render: Arabic toggle mirrors page + Add modal | i18n | P1 | _to author_ |

## Scenarios

### E2E-SCT-001 — Full CRUD round-trip

```gherkin
Feature: Session categories CRUD round-trip
  As an Administrator
  I want to manage the dynamic session-category lookup
  So that the session form's category picker reflects the event programme

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp using the Get-Totp helper
  And they have navigated to /admin/session-categories
  And the page has finished loading (no "Loading categories…" text)

Scenario: Create, edit (toggle Active off), then delete one category
  Given the grid currently shows {N} rows (or the SimfEmptyState when N = 0)
  When the administrator clicks "New category"
  Then the Add modal opens titled "Add category"
  And it shows four fields: Name (English), Name (Arabic), Display order, and an "Active" checkbox
  When they fill Name (English)="Keynote"
  And they fill Name (Arabic)="الكلمة الرئيسية"
  And they set Display order="10"
  And they click "Save"
  Then a POST /account/api/admin/session-categories fires and returns 200
  And the modal closes
  And a green toast reads "Category saved." / "تم حفظ التصنيف."
  And the grid shows {N + 1} rows
  And a row exists with Name (English)="Keynote", Name (Arabic)="الكلمة الرئيسية", Order=10, Active="✓"

  When the administrator clicks "Edit" on the "Keynote" row
  Then a GET /account/api/admin/session-categories/{id} fires and returns 200
  And the Edit modal opens titled "Edit category" with the row's values pre-filled
  And the "Active" checkbox is ticked
  When they change Display order to "5"
  And they untick the "Active" checkbox
  And they click "Save"
  Then a PUT /account/api/admin/session-categories/{id} fires and returns 200
  And the modal closes
  And a green toast reads "Category saved." / "تم حفظ التصنيف."
  And the "Keynote" row now reads Order=5 and Active="—"

  When the administrator clicks "Delete" on the "Keynote" row
  And accepts the browser confirm dialog "Delete this category?" / "حذف هذا التصنيف؟"
  Then a DELETE /account/api/admin/session-categories/{id} fires and returns 200
  And a green toast reads "Category deleted." / "تم حذف التصنيف."
  And the "Keynote" row remains visible with Active="—" (soft-delete; the list has no active filter)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-session-categories-001-before.png`
- Screenshot after add: `docs/screenshots/cp-admin-session-categories-001-add.png`
- Screenshot after edit: `docs/screenshots/cp-admin-session-categories-001-edit.png`
- Screenshot after delete: `docs/screenshots/cp-admin-session-categories-001-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/session-categories/*` call returns 200
- Audit rows: `SessionCategory.Created`, `SessionCategory.Updated`, `SessionCategory.Deactivated` rows in the audit log with the actor's id (`Detail` carries `id=…; nameEn=Keynote`).

### E2E-SCT-002 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the SessionCategories table has no rows (the seed-empty default — OI-2)
  When the administrator opens /admin/session-categories
  Then the POST /account/api/admin/session-categories/list returns 200 with Total = 0
  And the grid body renders the SimfEmptyState component
  And the empty state title reads "No session categories yet." / "لا توجد تصنيفات جلسات بعد."
  And the "New category" button is still visible above the empty state
  And no error toast appears
```

### E2E-SCT-003 — Auth gate

```gherkin
Scenario: Signed-in admin without SessionCategories.View is denied
  Given a user is signed in whose role does NOT grant PermissionCatalog.SessionCategories.View
  When they navigate to /admin/session-categories
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/session-categories/list request fires
  And the "Module.SessionCategories" nav item is not shown in the rail for that user
```

### E2E-SCT-004 — New-category button opens Add modal

```gherkin
Scenario: New category opens an empty Add modal
  Given the administrator is on /admin/session-categories
  When they click "New category"
  Then the modal opens titled "Add category"
  And Name (English) and Name (Arabic) are empty
  And Display order shows 0
  And the "Active" checkbox is ticked by default
  And no request has fired yet (the POST only fires on Save)
```

### E2E-SCT-005 — Edit button pre-fills from GET detail

```gherkin
Scenario: Edit fetches the row detail and pre-fills the modal
  Given at least one session category "Workshop" exists
  When the administrator clicks "Edit" on the "Workshop" row
  Then a GET /account/api/admin/session-categories/{id} fires and returns 200
  And the modal opens titled "Edit category"
  And Name (English), Name (Arabic), Display order are pre-filled from the detail response
  And the "Active" checkbox reflects the row's current IsActive value
  And the buttons are disabled while the GET is in flight (_busy guard)
```

### E2E-SCT-006 — Delete soft-deletes via native confirm

```gherkin
Scenario: Delete confirms then soft-deletes the row
  Given an active session category "Panel" exists with Active="✓"
  When the administrator clicks "Delete" on the "Panel" row
  Then a browser confirm dialog appears reading "Delete this category?" / "حذف هذا التصنيف؟"
  When they accept the dialog
  Then a DELETE /account/api/admin/session-categories/{id} fires and returns 200
  And a green toast reads "Category deleted." / "تم حذف التصنيف."
  And the "Panel" row remains in the grid but its Active column reads "—"
  And re-deleting the same already-inactive row is idempotent (no error)
```

### E2E-SCT-007 — Delete cancelled at the confirm dialog

```gherkin
Scenario: Dismissing the confirm dialog makes no change
  Given an active session category "Roundtable" exists
  When the administrator clicks "Delete" on the "Roundtable" row
  And they dismiss the browser confirm dialog
  Then no DELETE request fires
  And the "Roundtable" row is unchanged (Active still "✓")
  And no toast appears
```

### E2E-SCT-008 — Cancel closes the modal without saving

```gherkin
Scenario: Cancel discards the in-progress form
  Given the Add modal is open with Name (English)="Discarded"
  When the administrator clicks "Cancel"
  Then the modal closes
  And no POST request fires
  And no new row appears in the grid
```

### E2E-SCT-009 — Client validation: blank names

```gherkin
Scenario: Blank English or Arabic name is blocked client-side
  Given the Add modal is open
  When the administrator leaves Name (English) blank (or Name (Arabic) blank)
  And clicks "Save"
  Then a SimfAlert error appears reading "Both names are required." / "كلا الاسمين مطلوبان."
  And the modal stays open
  And no POST /account/api/admin/session-categories request fires (guarded before the call)
```

### E2E-SCT-010 — Server validation: name over 128 chars

```gherkin
Scenario: Over-long name returns API 400 with bilingual server message
  Given the Add modal is open
  When the administrator fills Name (English) with a 129-character string
  And fills Name (Arabic)="صالح"
  And clicks "Save"
  Then the BFF forwards POST /admin/session-categories
  And the API returns HTTP 400 with ApiResult.Error.Code = "SESSION_CATEGORY_INVALID"
  And the modal stays open
  And the error toast surfaces the bilingual MessageForCurrentCulture():
      "Session category English name must be between 1 and 128 characters." /
      "يجب أن يتراوح طول الاسم الإنجليزي للتصنيف بين 1 و 128 حرفاً."
  And the Name (Arabic) over-128 path returns the matching Arabic-name message
```

### E2E-SCT-011 — Display-order field coercion

```gherkin
Scenario: Display order accepts integers and coerces invalid input to 0
  Given the Add modal is open
  When the administrator types "25" into Display order and saves a valid row
  Then the created row shows Order=25
  When they open the Add modal and clear Display order / type a non-numeric value
  Then on change the field resolves to 0 (int.TryParse fallback)
  And a row saved that way shows Order=0
```

### E2E-SCT-012 — Action-level permission gating

```gherkin
Scenario: View-only admin sees the grid but no mutating actions
  Given a user signed in with PermissionCatalog.SessionCategories.View but NOT Create/Edit/Delete
  When they open /admin/session-categories
  Then the grid and rows render
  And the "New category" button is hidden (AuthorizedAction Create)
  And the per-row "Edit" button is hidden (AuthorizedAction Edit)
  And the per-row "Delete" button is hidden (AuthorizedAction Delete)
  And a direct POST /account/api/admin/session-categories from that user is rejected by the API policy
```

### E2E-SCT-013 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the bilingual fallback toast
  Given the API is configured to return 500 on /admin/session-categories/list (e.g. DB down)
  When the administrator opens /admin/session-categories
  Then the page shows "Loading categories…" briefly
  And then a red toast appears reading "Could not load categories." / "تعذّر تحميل التصنيفات."
  And no grid rows render
```

### E2E-SCT-014 — RTL render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add modal
  Given the administrator is on /admin/session-categories in English
  When they switch the UI language to العربية from the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "تصنيفات الجلسات"
  And the column headers read "الاسم (إنجليزي)", "الاسم (عربي)", "الترتيب", "نشط"
  And the nav rail and toolbar mirror (Arabic labels, reversed order)

  When they click "تصنيف جديد"
  Then the Add modal opens in RTL titled "إضافة تصنيف"
  And the field labels read "الاسم (إنجليزي)", "الاسم (عربي)", "ترتيب العرض", "نشط"
  And the footer buttons read "إلغاء" and "حفظ" in reversed order
```

---

## Implementation notes

- **No Details modal on this page.** Unlike the Interests page, this lookup has
  only an Add/Edit modal — Edit re-fetches the row via
  `GET /account/api/admin/session-categories/{id}` to pre-fill. There is no
  read-only details view to test.
- **Delete is a soft-delete (Deactivate) behind a native `confirm()`.** The page
  calls the browser `confirm` dialog with "Delete this category?" before the
  `DELETE`; the service runs `category.Deactivate()` (sets `IsActive = false`) and
  is idempotent on already-inactive rows. The list endpoint applies **no default
  active filter** (the page sends `GridQuery { Top = 100 }`), so a deleted row
  stays visible with Active="—" rather than disappearing — assert that, not row
  removal. When driving via Chrome DevTools MCP, pre-arm the dialog handler
  (`handle_dialog`) before clicking Delete.
- **Two distinct validation layers.** "Both names are required." is a **client**
  guard in `SaveAsync` (no request fires). The 1–128 length bound is enforced
  **server-side** and returns `SESSION_CATEGORY_INVALID` (HTTP 400) with the
  bilingual message; the EF column and the `MaxLength="128"` field cap also bound
  the input. There is no uniqueness/duplicate-name constraint on this lookup, so a
  409/conflict scenario does not apply (omitted deliberately).
- **API integration tests** at `tests/SIMF.Api.Tests/SessionCategoriesTests.cs`
  cover the create → get → list, update, deactivate, validation (400) and the
  permission policy at the API layer (no browser). The E2E catalogue layers the
  CP-driven UI behaviour (modals, toasts, confirm dialog, action-button gating,
  RTL) on top of that lower-layer coverage.
- **Audit keys:** `SessionCategory.Created`, `SessionCategory.Updated`,
  `SessionCategory.Deactivated` (`AuditEvents`), one row per mutation with the
  actor id.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
