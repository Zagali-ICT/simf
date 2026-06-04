# E2E test catalogue — Banners (`/admin/banners`)

| | |
|--|--|
| **Page** | [`cp/admin-banners.md`](../../pages/cp/admin-banners.md) |
| **Route** | `/admin/banners` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-03 |

> **Page summary.** The Banners page (D-173, gap doc G8, PDF §1) is the admin
> CRUD over time-windowed CMS banners / announcements surfaced on the public
> Website + Flutter app. As of **D-256** the raw `<table>` was migrated to the
> owner-mandated **`SimfDataGrid`** (server-paged): columns Title, Start, End,
> Order, Active, plus the grid's own **Add** toolbar button, quiet per-row
> **Edit (pencil)** and **Delete (trash)** icon actions, and one **Add/Edit**
> modal. The **Title** column carries a per-column filter input; **all five
> columns are sortable**; the grid renders a full pager (page size **20**,
> `GridQuery { Top = 20 }`). The select-all / per-row checkboxes are present
> (`Multiselect="true"`) but there is **no bulk action** wired (no
> `CustomToolbar`) — selection is cosmetic. There is **no separate "Details"
> view** — the Edit icon re-opens the same modal pre-filled and is the
> read-back path. `RequiredPermission` = `PermissionCatalog.Banners.View`.
>
> **Modal fields (in render order):** Title (English), Title (Arabic),
> Body (English), Body (Arabic), Image URL, Click-through URL,
> Start (UTC) `datetime-local`, End (UTC) `datetime-local`, Display order
> `number`, and (Edit only) an **Active** checkbox. Submit button label is
> **Save**; the create vs. edit branch is chosen by `_isEdit`.
>
> **BFF routes** (`AccountEndpoints.cs`, all under `/account/api`):
> `POST /admin/banners/list` (grid), `GET /admin/banners/{id}` (edit read-back),
> `POST /admin/banners` (create), `PUT /admin/banners/{id}` (update),
> `DELETE /admin/banners/{id}` (soft delete / deactivate). Each forwards to the
> API `CmsEndpoints` gated by `Banners.View|Create|Edit|Delete`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-BNR-001 | Full CRUD round-trip — Add → grid → Edit (read-back + toggle Active) → Delete | happy | P0 | _to author_ |
| E2E-BNR-002 | Empty list renders `SimfEmptyState` ("No banners yet.") | happy | P1 | _to author_ |
| E2E-BNR-003 | Auth gate: signed-in admin lacking `Banners.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-BNR-004 | Validation: blank title → `BANNER_INVALID` bilingual error toast | error | P1 | _to author_ |
| E2E-BNR-005 | Validation: blank body → `BANNER_INVALID` bilingual error toast | error | P1 | _to author_ |
| E2E-BNR-006 | Validation: End ≤ Start → `BANNER_INVALID_TIME_WINDOW` bilingual error toast | error | P1 | _to author_ |
| E2E-BNR-007 | Validation: negative Display order → `BANNER_INVALID` bilingual error toast | error | P2 | _to author_ |
| E2E-BNR-008 | Client-side bad date parse → local fallback error, no POST fires | error | P2 | _to author_ |
| E2E-BNR-009 | Edit read-back: `GET /{id}` pre-fills every field incl. optional Image/Link URLs | happy | P1 | _to author_ |
| E2E-BNR-010 | Delete a banner → green "Banner deleted." toast + row drops from grid | happy | P1 | _to author_ |
| E2E-BNR-011 | Edit not-found: stale id → 404 `BANNER_NOT_FOUND` bilingual toast, modal stays | error | P2 | _to author_ |
| E2E-BNR-012 | Display-order sort: grid orders by DisplayOrder then StartUtc | happy | P2 | _to author_ |
| E2E-BNR-013 | Server 500 on `/list` → bilingual fallback "The banners could not be loaded." | resilience | P2 | _to author_ |
| E2E-BNR-014 | RTL / Arabic render: page + modal mirror, Arabic title column shown | i18n | P1 | _to author_ |
| E2E-BNR-015 | Per-column filter: typing in the Title filter narrows the grid (`Filters["title"]`, Skip→0) | happy | P1 | _to author_ |
| E2E-BNR-016 | Column sort: clicking the Title / Start headers toggles `Sort` + `SortDescending` | happy | P2 | _to author_ |

## Scenarios

### E2E-BNR-001 — Full CRUD round-trip

```gherkin
Feature: Banners CRUD round-trip
  As an Administrator
  I want to publish, edit and remove time-windowed banners
  So that the public Website and Flutter app show the right announcements

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator (superadmin@zagali-ict.com) has signed in via /login + /login/totp
    using the TOTP from the Get-Totp helper
  And they have landed on /admin/banners

Scenario: Create, read-back via Edit, toggle Active, then delete one banner
  Given the grid currently shows {N} rows
  When the administrator clicks the grid's Add (+) toolbar action
  Then the Add modal opens titled "New banner"
  And it shows fields: Title (English), Title (Arabic), Body (English),
      Body (Arabic), Image URL, Click-through URL, Start (UTC), End (UTC),
      Display order
  And the Active checkbox is NOT shown (it is Edit-only)
  And Start (UTC) defaults to now and End (UTC) defaults to now + 1 day
  And Display order defaults to "0"

  When they fill Title (English)="SIMF 2026 Keynote"
  And they fill Title (Arabic)="الكلمة الرئيسية لمنتدى 2026"
  And they fill Body (English)="Doors open 09:00 at the main auditorium."
  And they fill Body (Arabic)="تُفتح الأبواب الساعة 09:00 في القاعة الرئيسية."
  And they fill Image URL="/content/banners/keynote.png"
  And they fill Click-through URL="https://simf.example/agenda"
  And they set Start (UTC)="2026-06-10T08:00"
  And they set End (UTC)="2026-06-12T18:00"
  And they fill Display order="5"
  And they click "Save"
  Then the BFF posts POST /account/api/admin/banners and the API returns 200
  And the modal closes
  And a green toast reads "Banner saved." / "تم حفظ اللافتة."
  And the grid reloads (POST /account/api/admin/banners/list returns 200)
  And it shows {N + 1} rows
  And a row exists with Title="SIMF 2026 Keynote", Start="2026-06-10 08:00",
      End="2026-06-12 18:00", Order=5 and the "✓" active marker

  When the administrator clicks the row's Edit (pencil) icon action
  Then the BFF calls GET /account/api/admin/banners/{id} and returns 200
  And the Edit modal opens titled "Edit banner" with every field pre-filled
      from the detail (incl. Image URL and Click-through URL)
  And the Active checkbox is now visible and ticked
  When they change Display order to "0"
  And they untick the Active checkbox
  And they click "Save"
  Then the BFF puts PUT /account/api/admin/banners/{id} and the API returns 200
  And the modal closes
  And a green toast reads "Banner saved." / "تم حفظ اللافتة."
  And the row's Order column reads "0" and the Active column shows "—"

  When the administrator clicks the row's Delete (trash) icon action
  Then the BFF calls DELETE /account/api/admin/banners/{id} and returns 200
  And a green toast reads "Banner deleted." / "تم حذف اللافتة."
  And the grid reloads and the row no longer appears (soft-deleted: IsActive=false,
      and the list shows only DisplayOrder/StartUtc ordering — deactivated rows
      drop out because the row was already inactive after the edit)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-banners-crud-before.png`
- Screenshot after (grid + each modal state): `docs/screenshots/cp-admin-banners-crud-add-modal.png`,
  `docs/screenshots/cp-admin-banners-crud-edit-modal.png`,
  `docs/screenshots/cp-admin-banners-crud-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/banners/...` call returns 200
- Audit rows: `OperationLog` / `RowAudit` rows with event keys `Banner.Created`
  (create), `Banner.Updated` (edit) and `Banner.Deactivated` (delete), each
  carrying the actor's user id

### E2E-BNR-002 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no Banner rows
  When the administrator opens /admin/banners
  Then POST /account/api/admin/banners/list returns 200 with Total=0
  And the grid body renders the SimfEmptyState component (via the grid's EmptyTemplate)
  And the empty state title reads "No banners yet." / "لا توجد لافتات بعد."
  And the grid toolbar still shows the Add (+) action
  And no error toast appears
```

### E2E-BNR-003 — Auth gate

```gherkin
Scenario: Signed-in admin without Banners.View is denied
  Given a signed-in Control Panel user whose role does NOT grant
    PermissionCatalog.Banners.View (and is not Administrator "*")
  When they navigate to /admin/banners
  Then the [RequirePermission(PermissionCatalog.Banners.View)] attribute denies them
  And they land on /not-permitted with HTTP 200
  And no POST /account/api/admin/banners/list request fires
```

### E2E-BNR-004 — Validation: blank title

```gherkin
Scenario: Empty English or Arabic title returns BANNER_INVALID
  Given the Add modal is open
  When the administrator leaves Title (English) blank
  And fills Title (Arabic)="عنوان", Body (English)="x", Body (Arabic)="س"
  And sets a valid Start/End window and Display order="0"
  And clicks "Save"
  Then the BFF posts POST /account/api/admin/banners
  And the API returns HTTP 400 with ApiResult.Error.Code = "BANNER_INVALID"
  And the modal stays open (_editOpen stays true)
  And a red SimfAlert toast surfaces the bilingual MessageForCurrentCulture():
      "Banner title (EN + AR) must be between 1 and 256 characters."
      / "يجب أن يتراوح طول العنوان (إنجليزي + عربي) بين 1 و 256 حرفاً."
```

### E2E-BNR-005 — Validation: blank body

```gherkin
Scenario: Empty English or Arabic body returns BANNER_INVALID
  Given the Add modal is open
  When the administrator fills both titles
  And leaves Body (English) blank
  And sets a valid Start/End window and Display order="0"
  And clicks "Save"
  Then the API returns HTTP 400 with ApiResult.Error.Code = "BANNER_INVALID"
  And the modal stays open
  And a red toast reads
      "Banner body (EN + AR) must be between 1 and 2000 characters."
      / "يجب أن يتراوح طول النص (إنجليزي + عربي) بين 1 و 2000 حرف."
```

### E2E-BNR-006 — Validation: End ≤ Start

```gherkin
Scenario: End not after Start returns BANNER_INVALID_TIME_WINDOW
  Given the Add modal is open
  When the administrator fills both titles and both bodies
  And sets Start (UTC)="2026-06-12T18:00"
  And sets End (UTC)="2026-06-10T08:00"
  And fills Display order="0"
  And clicks "Save"
  Then the BFF posts POST /account/api/admin/banners
  And the API returns HTTP 400 with ApiResult.Error.Code = "BANNER_INVALID_TIME_WINDOW"
  And the modal stays open
  And a red toast reads "Banner end must be after its start."
      / "يجب أن تكون نهاية البانر بعد بدايته."
```

### E2E-BNR-007 — Validation: negative Display order

```gherkin
Scenario: Negative Display order returns BANNER_INVALID
  Given the Add modal is open
  When the administrator fills both titles, both bodies and a valid window
  And fills Display order="-1"
  And clicks "Save"
  Then the API returns HTTP 400 with ApiResult.Error.Code = "BANNER_INVALID"
  And a red toast reads "Display order must be zero or a positive integer."
      / "يجب أن يكون ترتيب العرض صفراً أو عدداً صحيحاً موجباً."
  And the modal stays open
```

### E2E-BNR-008 — Client-side bad date parse

```gherkin
Scenario: Unparseable Start/End is caught before any request fires
  Given the Add modal is open with all text fields filled
  When the Start (UTC) or End (UTC) value cannot be parsed by DateTime.TryParse
    (e.g. a cleared / malformed datetime-local value)
  And the administrator clicks "Save"
  Then SubmitAsync returns early WITHOUT posting to the BFF
  And a red toast surfaces the local fallback "The banners could not be loaded."
      / "تعذّر تحميل اللافتات." (the page reuses LoadFailed as the parse-error message)
  And no /account/api/admin/banners POST request appears in the network log
```

### E2E-BNR-009 — Edit read-back pre-fills all fields

```gherkin
Scenario: Edit re-reads the detail and pre-fills every field
  Given a banner exists with Image URL="/img/a.png" and Click-through URL="https://x.test"
  When the administrator clicks the row's Edit (pencil) icon action
  Then the BFF calls GET /account/api/admin/banners/{id} and returns 200
  And the Edit modal opens with Title (EN/AR), Body (EN/AR), Image URL,
      Click-through URL, Start (UTC), End (UTC), Display order and the
      Active checkbox all populated from AdminBannerDetail
  And the Start/End values render as yyyy-MM-ddTHH:mm in the datetime-local inputs
```

### E2E-BNR-010 — Delete a banner

```gherkin
Scenario: Delete removes the row from the grid
  Given an active banner row "SIMF 2026 Keynote" exists in the grid
  When the administrator clicks the row's Delete (trash) icon action
  Then the BFF calls DELETE /account/api/admin/banners/{id} and the API returns 200
  And a green toast reads "Banner deleted." / "تم حذف اللافتة."
  And the grid reloads (POST /account/api/admin/banners/list returns 200)
  And the deactivated row no longer renders in the active grid
```

### E2E-BNR-011 — Edit not-found (stale id)

```gherkin
Scenario: Editing a since-deleted banner returns 404
  Given a banner row is visible but the underlying record was deleted in another tab
  When the administrator clicks the row's Edit (pencil) icon action
  Then the BFF calls GET /account/api/admin/banners/{id}
  And the API returns HTTP 404 with ApiResult.Error.Code = "BANNER_NOT_FOUND"
  And OnEditAsync returns early so the Edit modal does NOT open
  And (if the row is then saved against the stale id) the PUT returns 404
      "Banner not found." / "لم يتم العثور على البانر." in a red toast
```

### E2E-BNR-012 — Display-order sort

```gherkin
Scenario: Grid orders by DisplayOrder then StartUtc
  Given three banners exist with Display order 0, 2 and 2
    (the two order-2 banners start one day apart)
  When the administrator opens /admin/banners
  Then the grid lists the Order=0 banner first
  And then the two Order=2 banners in ascending StartUtc order
  (server-side: rows.OrderBy(b => b.DisplayOrder).ThenBy(b => b.StartUtc))
```

### E2E-BNR-013 — Server 500 on /list

```gherkin
Scenario: API 500 on /list shows the bilingual fallback toast
  Given the API is configured to return 500 on /admin/banners/list (e.g. DB down)
  When the administrator opens /admin/banners
  Then the page shows the "Loading banners…" / "جارٍ تحميل اللافتات…" indicator
  And then a red toast appears reading "The banners could not be loaded."
      / "تعذّر تحميل اللافتات."
  And no rows render and no SimfEmptyState is shown
```

### E2E-BNR-014 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the modal
  Given the administrator is on /admin/banners in English
  When they switch the UI culture to Arabic ("العربية")
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "اللافتات"
  And the grid's Add (+) toolbar action reads "إضافة" (Grid.Add)
  And the grid headers read "العنوان", "البداية", "النهاية", "الترتيب", "مفعّلة"
  And the Title column shows the Arabic title (TitleLabel picks TitleAr when culture is ar)

  When they click the grid's Add (+) toolbar action ("إضافة")
  Then the Add modal opens in RTL titled "لافتة جديدة" (Admin.Banners.Add.Title)
  And the field labels are Arabic ("العنوان (الإنجليزية)", "النص (العربية)", "ترتيب العرض", …)
  And the footer actions read "حفظ" (Save) and "إلغاء" (Cancel) in reversed order
```

### E2E-BNR-015 — Per-column filter narrows the grid

```gherkin
Scenario: Typing in the Title column filter narrows the grid to matching banners
  Given several banners exist including "SIMF 2026 Keynote" and "Closing Ceremony"
  And the administrator is on /admin/banners with the grid showing all rows
  And the Title column is the only filterable column (Key="title", header "Title" / "العنوان")
  When the administrator types "Keynote" into the per-column input "Filter column Title"
  Then the BFF posts POST /account/api/admin/banners/list and the API returns 200
  And the GridQuery carries Filters["title"]="Keynote" and Skip=0 (paging resets to the first page)
  And the grid narrows to only the row(s) whose TitleEn or TitleAr contains "Keynote"
      (server-side: TitleEn.Contains(v) || TitleAr.Contains(v))
  And the "Closing Ceremony" row no longer renders
  When the administrator clears the "Filter column Title" input
  Then a fresh POST /account/api/admin/banners/list fires with Filters["title"] absent/empty
  And the full set of banner rows renders again
```

### E2E-BNR-016 — Column sort toggles

```gherkin
Scenario: Clicking a sortable header toggles ascending / descending order
  Given the administrator is on /admin/banners
  And the grid defaults to DisplayOrder asc, then StartUtc asc (no Sort key sent)
  When the administrator clicks the "Title" column header
  Then the BFF posts POST /account/api/admin/banners/list and the API returns 200
  And the GridQuery carries Sort="title" with SortDescending=false (server: OrderBy(b => b.TitleEn))
  And the grid re-renders ordered by Title A→Z
  When the administrator clicks the "Title" header again
  Then a fresh POST fires with Sort="title" and SortDescending=true (server: OrderByDescending(b => b.TitleEn))
  And the grid re-renders ordered by Title Z→A
  When the administrator clicks the "Start" column header (Key="startUtc")
  Then a fresh POST fires with Sort="startUtc", SortDescending=false (server: OrderBy(b => b.StartUtc))
  And the grid re-renders by earliest StartUtc first
```

---

## Implementation notes

- **Manual smoke is canonical today.** Until a Playwright project exists, the
  canonical "run" of these scenarios is a Chrome DevTools MCP session: sign in
  per the Background, walk each scenario, and capture screenshots into
  `docs/screenshots/cp-admin-banners-*.png`. Keep the Gherkin runner-agnostic.
- **API integration tests cover the same surface at a lower layer** —
  `tests/SIMF.Api.Tests/CmsTests.cs`:
  - `Banner_create_then_public_list_returns_active_only_within_window` (golden
    create + the public read window, covers BNR-001's create leg + the time
    window behind BNR-012),
  - `Banner_create_with_end_before_start_is_BANNER_INVALID_TIME_WINDOW`
    (covers BNR-006 at the API layer),
  - `Non_admin_caller_is_forbidden_on_content_block_upsert` (sibling CMS
    surface; the banner endpoints share the same `RequireApprovedAccount` +
    per-permission policy that BNR-003 exercises in the CP).
  The blank-title / blank-body / negative-order validation branches
  (BNR-004/005/007) and the 404 read-back (BNR-011) are **not** yet covered by
  an `Api.Tests` case — author those at the E2E layer (and optionally back-fill
  `CmsTests`).
- **No page reference doc yet.** `docs/pages/cp/admin-banners.md` does not exist
  at time of writing — the page-summary block above stands in until it is authored.
- **Permission gate.** Page = `[RequirePermission(PermissionCatalog.Banners.View)]`;
  API actions gated by `Banners.View` (list/get), `Banners.Create`, `Banners.Edit`,
  `Banners.Delete`; `CpNavigation` item `Module.Banners` carries
  `RequiredPermission = PermissionCatalog.Banners.View`.

---

_Last reviewed:_ 2026-06-03 by Claude (E2E catalogue rebuild) (D-256/D-257 grid affordances reconciled).
