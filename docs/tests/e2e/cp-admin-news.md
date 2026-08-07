# E2E test catalogue — News CRUD (`/admin/news`)

| | |
|--|--|
| **Page** | [`cp/admin-news.md`](../../pages/cp/admin-news.md) |
| **Route** | `/admin/news` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (canonical SIMF browser smoke). Convertible to Playwright later — keep scenario steps tool-agnostic. |
| **Auth setup** | `superadmin@simrsnf.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **Page permission:** `[RequirePermission(PermissionCatalog.News.View)]` (`"News.View"`).
> The mutating actions hit endpoints gated by `News.Create` / `News.Edit` /
> `News.Delete`. The `PublicRelations` role holds the whole `News.*` baseline;
> `Administrator` holds it via the `"*"` wildcard. A signed-in admin lacking
> `News.View` lands on `/not-permitted`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-NWS-001 | Full CRUD round-trip — Add → list → Edit (toggle Active) → Delete | happy | P0 | _to author_ |
| E2E-NWS-002 | Empty list renders `SimfEmptyState` ("No news articles yet.") | happy | P1 | _to author_ |
| E2E-NWS-003 | Auth gate: signed-in user without `News.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-NWS-004 | Add — Add modal opens with all 12 fields (no Active checkbox) | crud | P1 | _to author_ |
| E2E-NWS-005 | Edit — modal pre-fills from detail fetch + shows Active checkbox | crud | P1 | _to author_ |
| E2E-NWS-006 | Delete — CrudShell ViewDelete + SimfConfirm gates the soft-delete; Cancel aborts (D-353) | crud | P1 | _to author_ |
| E2E-NWS-007 | Client validation: blank required field → bilingual modal error, no POST | error | P1 | _to author_ |
| E2E-NWS-008 | Server validation: over-length title (>200) → 400 `NEWS_INVALID` | error | P2 | _to author_ |
| E2E-NWS-009 | Conflict: duplicate English title → 409 `NEWS_TITLE_DUPLICATE` | error | P1 | _to author_ |
| E2E-NWS-010 | Publish date + Display order round-trip through `<input type="date/number">` | crud | P2 | _to author_ |
| E2E-NWS-011 | Reactivate a soft-deleted article via Edit → Active checkbox | crud | P2 | _to author_ |
| E2E-NWS-012 | Server 500 on `/admin/news/list` → bilingual fallback toast | resilience | P2 | _to author_ |
| E2E-NWS-013 | RTL render: Arabic toggle mirrors page + Add modal | i18n | P1 | _to author_ |
| E2E-NWS-014 | Per-column grid filter (Title/Category EN) narrows the grid, Skip resets to 0 | crud | P1 | _to author_ |
| E2E-NWS-015 | Column sort (Title EN / Publish date / Display order) toggles asc↔desc | crud | P2 | _to author_ |
| E2E-NWS-016 | Presentation toggle: switch to full-page + persists across reload (D-353) | happy | P1 | _to author_ |
| E2E-NWS-017 | Full-page mode: Add/Edit/View take over the content area, Save returns to grid (D-353) | happy | P1 | _to author_ |
| E2E-NWS-018 | Delete confirmation gate: CrudShell + SimfConfirm — Cancel = no DELETE, confirm = one DELETE (D-353) | error | P0 | _to author_ |
| E2E-NWS-019 | Excel export: toolbar Export downloads an .xlsx of the filtered grid / selected rows (D-356) | happy | P1 | _to author_ |
| E2E-NWS-020 | Excel import: upload a workbook → rows created + result modal with per-row outcome (D-356) | happy | P1 | _to author_ |
| E2E-NWS-021 | Excel import: a non-workbook / wrong-sheet upload → bilingual rejection, nothing created (D-356) | error | P1 | _to author_ |
| E2E-NWS-022 | Image via the unified media-asset pipeline — upload then external link (D-357) | happy | P1 | _to author_ |
| E2E-NWS-023 | Excel round-trip: BodyArabic + ExcerptArabic survive export and import (D-506) | happy | P1 | _to author_ |
| E2E-NWS-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-NWS-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-NWS-001 — Full CRUD round-trip

```gherkin
Feature: News CRUD round-trip
  As an Administrator (or a PublicRelations role member)
  I want to author, edit and retire News articles
  So that the public News feed (Mockup screen 29) stays current

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And the Website is reachable on http://localhost:5115
  And an Administrator has signed in via /login + /login/totp using the Get-Totp helper
  And they have landed on /admin/news

Scenario: Create, edit (toggle Active), then delete one article
  Given the grid summary reads "Showing 1–{N} of {N}" (or the SimfEmptyState if empty)
  When the administrator clicks "Add news article"
  Then the modal titled "Add news article" opens
  And it shows the fields: Title (English), Title (Arabic), Category (English),
      Category (Arabic), Excerpt (English), Excerpt (Arabic), Body (English),
      Body (Arabic), Image path, Publish date, Display order
  And no "Active" checkbox is shown (Add hides it; only Edit shows it)

  When they fill Title (English)="SIMF 2026 opens registration"
  And they fill Title (Arabic)="منتدى الدفاع البحري 2026 يفتح التسجيل"
  And they fill Category (English)="Announcements"
  And they fill Category (Arabic)="إعلانات"
  And they fill Body (English)="Registration for the Saudi International Maritime Forum is now open."
  And they fill Body (Arabic)="التسجيل في المنتدى البحري الدولي السعودي مفتوح الآن."
  And they set Publish date="2026-06-10"
  And they set Display order="10"
  And they click "Save"
  Then a POST /account/api/admin/news fires and the BFF forwards POST /admin/news
  And the API returns HTTP 200 with ApiResult.Success = true
  And the modal closes
  And a green SimfAlert toast reads "News article saved." / "تم حفظ الخبر."
  And the grid shows a row with Title (EN)="SIMF 2026 opens registration",
      Category (EN)="Announcements", Published="2026-06-10", Order=10, and Active="✓"

  When the administrator clicks the row's Edit (pencil) icon action
  Then a GET /account/api/admin/news/{id} fires (the grid summary omits body/excerpt/image)
  And the modal titled "Edit news article" opens with every field pre-filled
  And an "Active" checkbox is now visible and ticked
  When they change Display order to "0"
  And they click "Save"
  Then a PUT /account/api/admin/news/{id} fires and returns HTTP 200
  And the modal closes and the toast reads "News article saved." / "تم حفظ الخبر."
  And the row's Order column now reads "0"

  When the administrator clicks the row's Delete (trash) icon action
  Then the CrudShell opens the NewsViewDelete form (dialog by default) showing the
      article's read-only details and a red "Delete" button (D-353 — the old native
      browser confirm() is gone)
  When they click "Delete"
  Then a SimfConfirm dialog appears naming the article
  When they confirm
  Then a DELETE /account/api/admin/news/{id} fires and returns HTTP 200
  And the toast reads "News article deleted." / "تم حذف الخبر."
  And the row's Active column now reads "—" (soft-deleted; admin grid still lists it)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-news-crud-before.png`
- Screenshot after (add): `docs/screenshots/cp-admin-news-add-modal.png`
- Screenshot after (edit): `docs/screenshots/cp-admin-news-edit-modal.png`
- Screenshot after (delete): `docs/screenshots/cp-admin-news-crud-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/news/*` call returns 200
- Audit rows: `OperationLog` rows with `Event = 'news.created'`, `'news.updated'`,
  `'news.deactivated'` (constants `AuditEvents.NewsCreated/NewsUpdated/NewsDeactivated`),
  each carrying the actor's user id.

### E2E-NWS-002 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the News table has no rows
  When the administrator opens /admin/news
  Then a POST /account/api/admin/news/list returns 200 with Total = 0
  And the grid body renders the SimfEmptyState component
  And it shows the bilingual copy "No news articles yet." / "لا توجد أخبار بعد."
  And the toolbar still shows the "Add news article" button
  And no error toast appears
```

### E2E-NWS-003 — Auth gate

```gherkin
Scenario: Signed-in user without News.View is denied
  Given a user is signed in who lacks the News.View permission
      (e.g. a role granted only Interests.View, not the News.* baseline)
  When they navigate to /admin/news
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/news/list request fires
```

### E2E-NWS-004 — Add modal field set

```gherkin
Scenario: Add modal exposes the full author form without the Active toggle
  Given the administrator is on /admin/news
  When they click "Add news article"
  Then the modal titled "Add news article" opens
  And the fields render in order: Title (English), Title (Arabic),
      Category (English), Category (Arabic), Excerpt (English),
      Excerpt (Arabic), Body (English), Body (Arabic), Image path,
      Publish date, Display order
  And Publish date defaults to today (the form seeds PublishedAt = UtcNow)
  And Display order defaults to "0"
  And the "Active" checkbox is NOT present (Add path always creates IsActive = true)
  And the footer shows "Cancel" and "Save"
```

### E2E-NWS-005 — Edit pre-fill + Active checkbox

```gherkin
Scenario: Edit fetches the full detail and surfaces the Active checkbox
  Given at least one News article exists
  When the administrator clicks the row's Edit (pencil) icon action
  Then a GET /account/api/admin/news/{id} returns 200 with the full AdminNewsDetail
  And the modal titled "Edit news article" opens
  And Title (English), Title (Arabic), Category (English), Category (Arabic),
      Excerpt (English), Excerpt (Arabic), Body (English), Body (Arabic),
      Image path, Publish date and Display order are all pre-filled from the detail
  And an "Active" checkbox is visible (this control appears only in Edit)
  And the checkbox state matches the row's current IsActive
```

### E2E-NWS-006 — Delete confirm gate

```gherkin
Scenario: Cancelling the confirm dialog aborts the soft-delete
  Given a News article row is visible
  When the administrator clicks the row's Delete (trash) icon action
  Then the CrudShell opens the NewsViewDelete form with a red "Delete" button
      (D-353 — the native browser confirm() this page used to carry is gone)
  When they click "Delete"
  Then a SimfConfirm dialog appears naming the article
      ("Delete this news article? It will be removed from the public feed immediately."
       / "هل تريد حذف هذا الخبر؟ ستتم إزالته من الواجهة العامة فورًا.")
  When they dismiss the SimfConfirm (Cancel)
  Then no DELETE /account/api/admin/news/{id} request fires
  And the form stays open and the row is unchanged (Active still "✓")
  And no toast appears
```

### E2E-NWS-007 — Client validation (blank required field)

```gherkin
Scenario: Blank required field is blocked before any POST
  Given the Add modal is open
  When the administrator leaves Title (English) blank (or any of Title/Body/Category EN/AR)
  And clicks "Save"
  Then no /account/api/admin/news request fires (the client guard short-circuits)
  And a red SimfAlert toast appears reading
      "Please fill in the required fields: title, body and category (English and Arabic)."
      / "يرجى تعبئة الحقول المطلوبة: العنوان والمحتوى والتصنيف (بالإنجليزية والعربية)."
  And the modal stays open
```

### E2E-NWS-008 — Server validation (over-length title)

```gherkin
Scenario: Over-length English title returns a 400 the server validator owns
  Given the Add modal is open with all required fields filled
  And Title (English) is forced past 200 characters (bypassing the MaxLength="200" client cap)
  When the administrator clicks "Save"
  Then the BFF forwards POST /admin/news
  And the API returns HTTP 400 with ApiResult.Error.Code = "NEWS_INVALID"
      (CreateNewsValidator MaximumLength(200) and the service RequireText guard agree)
  And the modal stays open
  And the error toast surfaces the bilingual MessageForCurrentCulture()
      "News English title must be 200 characters or fewer."
      / "يجب ألا يتجاوز العنوان الإنجليزي 200 حرفاً."
```

### E2E-NWS-009 — Duplicate English title (409)

```gherkin
Scenario: Duplicate English title returns 409 with a bilingual server message
  Given a News article with Title (English)="SIMF 2026 opens registration" already exists
  When the administrator opens the Add modal
  And fills Title (English)="SIMF 2026 opens registration"
      + the Arabic title + both categories + both bodies
  And clicks "Save"
  Then the BFF forwards POST /admin/news
  And the API returns HTTP 409 with ApiResult.Error.Code = "NEWS_TITLE_DUPLICATE"
  And the modal stays open
  And the error toast reads
      "A news article with the English title 'SIMF 2026 opens registration' already exists."
      / "يوجد خبر بالعنوان الإنجليزي 'SIMF 2026 opens registration' بالفعل."
  And the same 409 fires from Edit only when the English title is changed to clash with another row
```

### E2E-NWS-010 — Publish date + Display order round-trip

```gherkin
Scenario: Date and number inputs round-trip cleanly
  Given the Add modal is open with all required fields filled
  When the administrator sets Publish date="2026-12-01" via the <input type="date">
  And sets Display order="42" via the <input type="number">
  And clicks "Save" (HTTP 200)
  Then the new grid row shows Published="2026-12-01" and Order=42
  When they re-open the row via its Edit (pencil) icon action
  Then Publish date reads "2026-12-01" and Display order reads "42"
      (PublishedAt is parsed AssumeUniversal/AdjustToUniversal; the date text mirror holds)
```

### E2E-NWS-011 — Reactivate a soft-deleted article

```gherkin
Scenario: A deleted article is recovered by re-ticking Active in Edit
  Given a News article that was soft-deleted (its Active column reads "—")
  When the administrator clicks the row's Edit (pencil) icon action
  Then the modal opens with the "Active" checkbox unticked
  When they tick "Active"
  And click "Save"
  Then a PUT /account/api/admin/news/{id} returns 200 with IsActive = true
  And the toast reads "News article saved." / "تم حفظ الخبر."
  And the row's Active column now reads "✓" (the article is back in the public feed)
```

### E2E-NWS-012 — Server 500 on list

```gherkin
Scenario: API 500 on /admin/news/list shows the fallback bilingual toast
  Given the API is configured to return 500 on /admin/news/list (e.g. DB down)
  When the administrator opens /admin/news
  Then the grid shows the "Loading news…" / "جارٍ تحميل الأخبار…" indicator
  And then a red toast appears reading
      "Could not complete the request. Please try again."
      / "تعذّر إكمال الطلب. يرجى المحاولة مرة أخرى."
  And no rows render
```

### E2E-NWS-013 — RTL render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add modal
  Given the administrator is on /admin/news in English
  When they switch the UI language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "الأخبار"
  And the toolbar button reads "إضافة خبر"
  And the grid headers read "العنوان (إنجليزي)", "العنوان (عربي)", "التصنيف (إنجليزي)",
      "التصنيف (عربي)", "تاريخ النشر", "الترتيب", "نشط"
  And the table + actions mirror right-to-left

  When they click "إضافة خبر"
  Then the Add modal opens in RTL titled "إضافة خبر"
  And the field labels are Arabic (العنوان (الإنجليزية), المحتوى (العربية), ترتيب العرض, …)
  And the footer buttons read "إلغاء" and "حفظ" in reverse order
```

### E2E-NWS-014 — Per-column filter narrows the grid

```gherkin
Scenario: Typing into a per-column filter input narrows the grid and resets paging
  Given the administrator is on /admin/news with several articles loaded
      (the SimfDataGrid renders a per-column filter row under the headers,
       with a search input under each Filterable column:
       Title (English), Title (Arabic), Category (English), Category (Arabic))
  When the administrator types "registration" into the filter input
      labelled "Filter Title (English)" (the "titleen" column)
  Then after the 300 ms debounce a POST /account/api/admin/news/list fires
  And the GridQuery carries Filters["titleen"]="registration" with Skip reset to 0
  And the grid re-renders showing only rows whose English title contains "registration"
  And the pager summary updates to "Showing 1–{matched} of {matched}"

  When the administrator also types "Announcements" into the filter input
      labelled "Filter Category (English)" (the "categoryen" column)
  Then a further POST /account/api/admin/news/list fires
  And the GridQuery carries both Filters["titleen"]="registration"
      and Filters["categoryen"]="Announcements" (filters combine, AND-style)
  And Skip stays 0

  When the administrator clears the Title (English) filter input
  Then a POST /account/api/admin/news/list fires with Filters no longer
      containing "titleen" (only "categoryen" remains) and Skip = 0
  And the publishedat / displayorder / isactive columns expose no filter input
      (they are not Filterable)
```

### E2E-NWS-015 — Column sort toggles

```gherkin
Scenario: Clicking a sortable header toggles ascending then descending
  Given the administrator is on /admin/news with several articles loaded
      (the sortable headers are Title (English) "titleen", Publish date
       "publishedat" and Display order "displayorder"; Title/Category Arabic
       and Active are not sortable)
  When the administrator clicks the "Publish date" column header
  Then a POST /account/api/admin/news/list fires with
      GridQuery.Sort="publishedat", SortDescending=false and Skip reset to 0
  And the header shows the ascending arrow (▲) and aria-sort="ascending"
  And the rows order by PublishedAt ascending

  When the administrator clicks the "Publish date" header again
  Then a POST /account/api/admin/news/list fires with
      Sort="publishedat", SortDescending=true (the same key flips direction)
  And the header shows the descending arrow (▼) and aria-sort="descending"

  When the administrator clicks the "Title (English)" header
  Then Sort switches to "titleen" with SortDescending=false (a new key starts ascending)
  And the previous "publishedat" header returns to the neutral (↕) state
```

### E2E-NWS-016 — Presentation toggle persists (D-353)

```gherkin
Scenario: Switch to full-page mode and it persists across reload
  Given the administrator is on /admin/news with the default "dialog" presentation
  And the grid toolbar shows the CrudPresentationToggle ("Open as full page", maximize icon)
  When they click the toggle
  Then the toggle label changes to "Open as dialog" (window icon)
  And localStorage key "simf.cp.prefs.news" holds {"v":1,"presentation":"page"}
      (CpPreferences writes the versioned per-page blob via the simfPrefs JS helper)
  When they reload /admin/news
  Then OnInitializedAsync reads the saved preference (Prefs.GetPresentationAsync("news"))
  And the toggle still reads "Open as dialog"
  And opening "Add news article" now renders the full-page CrudPageFrame (not a popup)
```

### E2E-NWS-017 — Full-page mode round-trip (D-353)

```gherkin
Scenario: Add/Edit/View take over the content area; Save returns to the grid
  Given the presentation is set to "full page" (localStorage simf.cp.prefs.news = "page")
  When the administrator clicks "Add news article"
  Then GridHidden becomes true: the grid + SimfBanner are replaced by the CrudShell
      page frame (title header + close button + the NewsAddEdit form)
  And there is no modal backdrop
  When they fill the required EN/AR title, category and body fields and click "Save"
  Then a POST /account/api/admin/news returns 200, the page frame closes,
      and the grid re-appears with the new row and the "News article saved." toast
  When they click a row's Edit (pencil) icon then the frame's close (X) button
      (CloseLabel "Close" / "إغلاق")
  Then the form closes and the grid re-appears unchanged (no PUT fired)
```

### E2E-NWS-018 — Delete confirmation gate (CrudShell + SimfConfirm) (D-353)

```gherkin
Scenario: Delete requires explicit SimfConfirm confirmation
  Given the administrator is on /admin/news
  When they click the Delete (trash) icon on a row
  Then a GET /account/api/admin/news/{id} fetches the full AdminNewsDetail
  And the CrudShell opens the NewsViewDelete form showing the article's read-only
      details (incl. the cover-image preview when ImageRelativePath is set) and a
      red "Delete" button (the old native browser confirm() is gone)
  When they click "Delete"
  Then a SimfConfirm dialog appears reading
      "Delete this news article? It will be removed from the public feed immediately."
      / "هل تريد حذف هذا الخبر؟ ستتم إزالته من الواجهة العامة فورًا."
      (Admin.News.Delete.Message formatted with the article title; Danger=true)
  And it cannot be dismissed by a backdrop click
  When they click "Cancel"
  Then no DELETE /account/api/admin/news/{id} request fires and the row is unchanged
  When they re-open the form, click "Delete", then confirm
  Then exactly one DELETE /account/api/admin/news/{id} fires (HTTP 200)
  And the form closes, the "News article deleted." / "تم حذف الخبر." toast appears,
      and the row's Active column flips to "—" (soft-deleted; admin grid still lists it)
```

### E2E-NWS-019 — Excel export (D-356)

```gherkin
Scenario: Export the filtered grid (or selected rows) to an XLSX workbook
  Given the administrator is on /admin/news with at least two articles
      (the page holds News.Export via the News.* baseline / "*" wildcard)
  When they click the toolbar "Export" action with no rows selected
  Then a POST /account/api/admin/news/export fires carrying an AdminGridExportRequest
      with an empty Ids list and the current GridQuery (Query is sent only when no
      rows are selected)
  And the browser saves a file named simf-news-{timestamp}.xlsx
  And the workbook's "News" sheet header row reads
      Title | TitleArabic | Category | CategoryArabic | PublishedAt | DisplayOrder | IsActive | BodyArabic | ExcerptArabic
      (BodyArabic + ExcerptArabic appended last so the existing column order is unchanged — D-506)
  When they instead select two rows then click "Export"
  Then the request carries those two Ids and a null Query
  And the workbook contains exactly those two rows
  And the API caps the export at 5000 rows
```

### E2E-NWS-020 — Excel import (D-356)

```gherkin
Scenario: Import news articles from a workbook and see the per-row outcome
  Given the administrator is on /admin/news (the page holds News.Import)
  When they click the toolbar "Import" action
  Then the hidden file input id="news-import-input" (accept=".xlsx") opens the OS picker
  When they choose an .xlsx whose "News" sheet has the required headers
      Title, TitleArabic, Body, BodyArabic, Category, CategoryArabic and two new rows
  Then a POST /account/api/admin/news/import fires as multipart form data
  And the import-result modal shows "2 created, 0 updated, 0 skipped."
      (Grid.Import.ResultBody) and the shared green "Grid.Import.Done" toast appears
  And the grid reloads and lists both new articles
  When they import a workbook whose first row repeats an existing English title
      and whose second row is a new title
  Then the modal shows 1 created and a per-row error naming the duplicate row
      (the service rejects the duplicate English title per-row — it is not a batch abort)
```

### E2E-NWS-021 — Excel import rejection (D-356)

```gherkin
Scenario: A bad or wrong-sheet upload is rejected without creating anything
  Given the administrator is on /admin/news
  When they import a file that is not a valid .xlsx (fails the ZIP-magic / 5MB gate)
  Then the request returns HTTP 400 and the page shows a bilingual error toast
      (the CrudGridExcel OnError surfaces ApiResult.Error.MessageForCurrentCulture())
  And no News article is created
  When they import a workbook whose sheet is not named "News"
      (or is missing a required header such as Body / BodyArabic)
  Then the request returns HTTP 400 with the bilingual worksheet/header message
  And no News article is created
```

---

### E2E-NWS-024 — the list shows the article's image thumbnail (D-357)

```gherkin
Scenario: the Title column renders a thumbnail when the article has an image
  Given an Administrator is on /admin/news
  And article "A" has a NewsImage asset and article "B" has none
  When the grid loads a page
  Then A's title cell shows the image thumbnail beside the title
  And B's title cell shows a tinted initials tile (never a broken image)
  And sorting / filtering by the Title column still works (column key unchanged)
```

**Covered (lower layer):** the flag-population path is proven by
`tests/SIMF.Api.Tests/ContactsTests.cs` →
`Admin_list_flips_HasLogo_once_a_CompanyLogo_asset_is_attached`; News uses the
identical owner=row.Id `WhichOwnersHaveActiveAssetAsync(NewsImage, ...)` restructure.
Confirm the render visually in the Chrome DevTools MCP smoke.

---

## Implementation notes

- **API integration tests** at `tests/SIMF.Api.Tests/NewsTests.cs` cover the same
  surface at a lower layer (no browser): create/list/get/update/deactivate, the
  `NEWS_TITLE_DUPLICATE` 409 on duplicate English title (create + title-change on
  update), the `NEWS_INVALID` 400 length/required guards, the `NEWS_NOT_FOUND` 404,
  and the per-action `News.*` policy gates. `AdminNewsEndpoints.cs` carries
  `// Tests: SIMF.Api.Tests/NewsTests.cs` and `AdminNewsService.cs` re-checks the
  same bounds so the contract holds even for non-HTTP callers.
- **Manual smoke as canonical-source-of-truth today.** Until Playwright is adopted,
  the canonical "run" is a Chrome DevTools MCP session: sign in via the Auth setup,
  walk each scenario, capture screenshots into `docs/screenshots/cp-admin-news-*.png`.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin scenario
  into a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created) +
  step-definition class. The Gherkin shape is already runner-agnostic.
- **Admin grid lists every row.** Unlike most CP lists, the News admin grid shows
  rows regardless of `IsActive` / publish window (drafts + soft-deleted), so "Delete"
  flips the Active column to "—" rather than removing the row — recovery is the
  reactivate-via-Edit path in E2E-NWS-011.

### E2E-NWS-022 — Image via the unified media-asset pipeline (D-357)

```gherkin
Scenario: Upload image, then switch it to an external link
  Given an Administrator is editing an article
  When they open the "Image" control, choose "Upload file", pick a PNG and click Upload
  Then a success message shows and the preview thumbnail refreshes
  And GET /account/api/admin/assets/NewsImage/{ownerId}/image returns the bytes (200)
  And /admin/media-library lists it as NewsImage - this entity - Image - Uploaded file - active
  When they switch to "External link", enter https://cdn.example/x.jpg and click Save link
  Then the asset Source becomes "External link" and GET /app/assets/NewsImage/{ownerId}/image 302s to that URL
  And the same-origin /content/assets/NewsImage/{ownerId}/image proxy serves it for any public page that renders this article
```

**Evidence:** the Asset DB row + the out-of-row file (or stored link); the Media Library row;
0 console errors; audit `AssetUploaded` then `AssetLinked`. Validation: a non-image / over-5 MB /
video upload is 400; deactivate->restore round-trips; restoring when a live (category,owner) asset
already exists is 409 (covered by `tests/SIMF.Api.Tests/AssetEndpointsTests.cs`).

### E2E-NWS-023 — Excel round-trip of BodyArabic + ExcerptArabic (D-506)

```gherkin
Scenario: The Arabic body and excerpt survive both export and import
  Given the administrator is on /admin/news with an article whose
      Body (Arabic)="نص الخبر بالعربية." and Excerpt (Arabic)="مقتطف عربي."
  When they click the toolbar "Export" action
  Then a POST /account/api/admin/news/export returns 200 with an .xlsx
  And the "News" sheet header row now includes "BodyArabic" and "ExcerptArabic"
      (appended after IsActive — earlier columns unmoved, D-506)
  And that article's row carries "نص الخبر بالعربية." under BodyArabic
      and "مقتطف عربي." under ExcerptArabic (the export no longer drops them)

  When they import a workbook whose "News" sheet supplies Title, TitleArabic,
      Body, BodyArabic, Category, CategoryArabic and an ExcerptArabic cell
  Then a POST /account/api/admin/news/import returns 200 and creates the row
  And GET /account/api/admin/news/{id} (and the grid summary) report the same
      BodyArabic + ExcerptArabic values that were imported
```

**Evidence:** the exported workbook's header row + the article's BodyArabic/ExcerptArabic
cells; the import result modal (1 created, 0 errors); the round-tripped list summary.
Covered by `tests/SIMF.Api.Tests/NewsExcelTests.cs`
(`Export_includes_the_body_arabic_and_excerpt_arabic_columns` +
`Import_round_trips_the_body_arabic_and_excerpt_arabic`).

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle): added the
presentation-toggle, full-page round-trip, CrudShell+SimfConfirm delete gate, and
Excel export/import/rejection scenarios; corrected the now-stale native-confirm()
delete copy in E2E-NWS-001 and E2E-NWS-006 to the shipped CrudShell + SimfConfirm flow.
Prior: 2026-06-03 (E2E catalogue rebuild) (D-256/D-257 grid affordances reconciled).
