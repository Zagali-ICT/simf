# E2E test catalogue — News CRUD (`/admin/news`)

| | |
|--|--|
| **Page** | [`cp/admin-news.md`](../../pages/cp/admin-news.md) |
| **Route** | `/admin/news` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (canonical SIMF browser smoke). Convertible to Playwright later — keep scenario steps tool-agnostic. |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

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
| E2E-NWS-006 | Delete — `confirm()` dialog gates the soft-delete; Cancel aborts | crud | P1 | _to author_ |
| E2E-NWS-007 | Client validation: blank required field → bilingual modal error, no POST | error | P1 | _to author_ |
| E2E-NWS-008 | Server validation: over-length title (>200) → 400 `NEWS_INVALID` | error | P2 | _to author_ |
| E2E-NWS-009 | Conflict: duplicate English title → 409 `NEWS_TITLE_DUPLICATE` | error | P1 | _to author_ |
| E2E-NWS-010 | Publish date + Display order round-trip through `<input type="date/number">` | crud | P2 | _to author_ |
| E2E-NWS-011 | Reactivate a soft-deleted article via Edit → Active checkbox | crud | P2 | _to author_ |
| E2E-NWS-012 | Server 500 on `/admin/news/list` → bilingual fallback toast | resilience | P2 | _to author_ |
| E2E-NWS-013 | RTL render: Arabic toggle mirrors page + Add modal | i18n | P1 | _to author_ |

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

  When the administrator clicks "Edit" on that row
  Then a GET /account/api/admin/news/{id} fires (the grid summary omits body/excerpt/image)
  And the modal titled "Edit news article" opens with every field pre-filled
  And an "Active" checkbox is now visible and ticked
  When they change Display order to "0"
  And they click "Save"
  Then a PUT /account/api/admin/news/{id} fires and returns HTTP 200
  And the modal closes and the toast reads "News article saved." / "تم حفظ الخبر."
  And the row's Order column now reads "0"

  When the administrator clicks "Delete" on that row
  And the browser confirm() dialog reads "Delete this news article? It will be removed from the public feed immediately."
  And they accept the dialog
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
  When the administrator clicks "Edit" on its grid row
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
  When the administrator clicks "Delete" on that row
  Then a browser confirm() dialog appears reading
      "Delete this news article? It will be removed from the public feed immediately."
      / "هل تريد حذف هذا الخبر؟ ستتم إزالته من الواجهة العامة فورًا."
  When they dismiss the dialog (Cancel)
  Then no DELETE /account/api/admin/news/{id} request fires
  And the row is unchanged (Active still "✓")
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
  When they re-open the row via "Edit"
  Then Publish date reads "2026-12-01" and Display order reads "42"
      (PublishedAt is parsed AssumeUniversal/AdjustToUniversal; the date text mirror holds)
```

### E2E-NWS-011 — Reactivate a soft-deleted article

```gherkin
Scenario: A deleted article is recovered by re-ticking Active in Edit
  Given a News article that was soft-deleted (its Active column reads "—")
  When the administrator clicks "Edit" on that row
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

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
