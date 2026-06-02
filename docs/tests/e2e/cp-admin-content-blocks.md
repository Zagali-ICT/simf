# E2E test catalogue — Content blocks (`/admin/content-blocks`)

| | |
|--|--|
| **Page** | [`cp/admin-content-blocks.md`](../../pages/cp/admin-content-blocks.md) |
| **Route** | `/admin/content-blocks` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Page summary.** The Content blocks page (D-173, gap doc G8, PDF §1, §2.1)
> is the dynamic-CMS admin surface: editable key/value text blocks (welcome
> message, page copy, labels, the `cyber.*` policy text the Flutter app reads)
> surfaced on the public Website + mobile app. The page is a **single grid**
> (columns: Key, English preview, Last updated, Active) with **one toolbar
> button "New block"** and **one shared Add/Edit modal** carrying four fields:
> `Key`, `Content (English)`, `Content (Arabic)` and an `Active` checkbox.
> Each grid row has two row-action buttons: **Edit content block** and
> **Delete**.
>
> There is **no search box, no filter control, no pager control, no bulk
> action and no separate read-only "Details" view** on the page — "Edit"
> re-opens the same modal pre-filled and is the read-back path. The grid is
> capped at the first 25 rows (`GridQuery { Top = 25 }`, no page-2 control).
>
> **Upsert is keyed, not id-based.** The same `PUT /admin/content-blocks`
> serves both create and edit; the server normalises the key (`Trim()` +
> `ToLowerInvariant()`) and creates the row if absent or **updates it in
> place** if present. In the modal the **`Key` field is disabled while
> editing** (`Disabled="_busy || _isEdit"`), so a duplicate-key collision
> can only be reached from the **New block** path — and it does **not** error,
> it silently upserts onto the existing row (see E2E-CNT-005).
>
> `RequiredPermission` = `PermissionCatalog.ContentBlocks.View`. The upsert is
> additionally gated by `ContentBlocks.Edit` and delete by
> `ContentBlocks.Delete` at the API layer.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-CNT-001 | Golden path — New block → grid → Edit (read-back + change) → Delete | happy | P0 | _to author_ |
| E2E-CNT-002 | New block: create one block, toast + grid row + audit | happy | P0 | _to author_ |
| E2E-CNT-003 | Edit content block: Key field disabled, content updated in place | happy | P1 | _to author_ |
| E2E-CNT-004 | Delete (idempotent deactivate): row Active flips to "—" | happy | P1 | _to author_ |
| E2E-CNT-005 | Re-using an existing key from "New block" upserts in place (no duplicate) | happy | P1 | _to author_ |
| E2E-CNT-006 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-CNT-007 | Auth gate: signed-in admin lacking `ContentBlocks.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-CNT-008 | Validation: too-short key (`< 2` chars) → `CONTENT_BLOCK_INVALID` 400 | error | P1 | _to author_ |
| E2E-CNT-009 | Validation: content over 8000 chars → `CONTENT_BLOCK_INVALID` 400 | error | P1 | _to author_ |
| E2E-CNT-010 | Delete a missing/already-removed key → `CONTENT_BLOCK_NOT_FOUND` / idempotent | error | P2 | _to author_ |
| E2E-CNT-011 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-CNT-012 | RTL / Arabic render: page + Add modal mirror | i18n | P1 | _to author_ |

## Scenarios

### E2E-CNT-001 — Golden path (New → grid → Edit → Delete)

```gherkin
Feature: Content blocks CRUD round-trip
  As an Administrator
  I want to manage the dynamic CMS content blocks
  So that the Website + Flutter copy stays editable without a redeploy

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator (superadmin@zagali-ict.com) has signed in via /login + /login/totp
    using a fresh code from the Get-Totp helper
  And they have landed on /admin/content-blocks
  And the grid has finished loading (no "Loading content blocks…" text)

Scenario: Create, read back via Edit, change in place, then delete one block
  Given the grid currently shows {N} rows
  When the administrator clicks "New block"
  Then the Edit modal opens titled "Edit content block"
  And it shows four inputs: "Key (e.g. home.welcome.title)", "Content (English)",
      "Content (Arabic)", and an "Active" checkbox (ticked)
  And the "Key" field is enabled (this is the create path)
  When they fill Key="home.welcome.title"
  And they fill Content (English)="Welcome to SIMF 2027"
  And they fill Content (Arabic)="مرحباً بكم في سيمف 2027"
  And the Active checkbox stays ticked
  And they click "Save"
  Then the modal closes
  And a green SimfAlert reads "Content block saved." at the top of the surface
  And the grid shows {N + 1} rows
  And a row exists with Key="home.welcome.title", an English preview starting "Welcome to SIMF 2027",
      a "Last updated" timestamp of "now" in "yyyy-MM-dd HH:mm UTC" format, and Active = "✓"

  When the administrator clicks "Edit content block" on that row
  Then the Edit modal re-opens with the row's values pre-filled
  And the "Key" field is DISABLED and reads "home.welcome.title"
  And "Content (English)" reads "Welcome to SIMF 2027"
  And "Content (Arabic)" reads "مرحباً بكم في سيمف 2027"
  And the "Active" checkbox is ticked
  When they change Content (English) to "Welcome back to SIMF 2027"
  And they click "Save"
  Then the modal closes
  And a green SimfAlert reads "Content block saved."
  And the same row (no new row added) now previews "Welcome back to SIMF 2027"
  And the row id is unchanged (in-place update, not a second row)

  When the administrator clicks "Delete" on that row
  Then a green SimfAlert reads "Content block deleted."
  And the row's Active column flips from "✓" to "—"
  And the row remains visible (delete is a soft deactivate, not a hard remove)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-content-blocks-golden-before.png`
- Screenshots: `docs/screenshots/cp-admin-content-blocks-{add-modal,edit-modal,after-delete}.png`
- Console errors: 0 expected
- Network: `POST /account/api/admin/content-blocks/list` → 200; `PUT /account/api/admin/content-blocks` (create) → 200; `PUT /account/api/admin/content-blocks` (edit) → 200; `DELETE /account/api/admin/content-blocks/home.welcome.title` → 200
- Audit rows: two `OperationLog`/audit rows with `Event = 'ContentBlock.Upserted'` (create + edit, `Detail = "key=home.welcome.title"`) and one with `Event = 'ContentBlock.Deactivated'`, all carrying the actor's user id

### E2E-CNT-002 — New block (single create)

```gherkin
Scenario: Create one content block from the New block button
  Given the administrator is on /admin/content-blocks
  When they click "New block"
  And they fill Key="footer.copyright"
  And they fill Content (English)="© 2027 Royal Saudi Naval Forces"
  And they fill Content (Arabic)="© 2027 القوات البحرية الملكية السعودية"
  And they click "Save"
  Then the BFF forwards PUT /account/api/admin/content-blocks with the UpsertContentBlockRequest
  And the API returns HTTP 200 with ApiResult.Data.Key="footer.copyright"
  And the modal closes
  And a green SimfAlert reads "Content block saved."
  And a new grid row appears with Key="footer.copyright" and Active="✓"
```

### E2E-CNT-003 — Edit content block (key locked, content updated)

```gherkin
Scenario: Edit pre-fills the row and disables the key
  Given a content block with Key="home.welcome.title" exists
  When the administrator clicks "Edit content block" on that row
  Then the Edit modal opens with Key, Content (English), Content (Arabic) and Active pre-filled
  And the "Key" input is rendered disabled (cannot be changed on the edit path)
  When they untick the "Active" checkbox
  And they click "Save"
  Then the API upserts the row in place (same id) with IsActive=false
  And a green SimfAlert reads "Content block saved."
  And the row's Active column reads "—"
```

### E2E-CNT-004 — Delete (idempotent deactivate)

```gherkin
Scenario: Delete deactivates the row rather than hard-deleting it
  Given an active content block with Key="promo.banner" exists (Active = "✓")
  When the administrator clicks "Delete" on that row
  Then the BFF forwards DELETE /account/api/admin/content-blocks/promo.banner
  And the API returns HTTP 200 with ApiResult.Data = true
  And a green SimfAlert reads "Content block deleted."
  And the row stays in the grid with its Active column now "—"
  And a subsequent public read GET /api/v1/content/promo.banner returns 404 (inactive blocks are hidden publicly)
```

### E2E-CNT-005 — Re-using an existing key upserts in place (no duplicate)

```gherkin
Scenario: New block with a key that already exists updates the existing row
  Given a content block with Key="home.welcome.title" and Content (English)="Welcome to SIMF 2027" exists
  When the administrator clicks "New block"
  And they fill Key="HOME.WELCOME.TITLE"   # different case — the server normalises to lower-case
  And they fill Content (English)="Overwritten copy"
  And they fill Content (Arabic)="نسخة محدثة"
  And they click "Save"
  Then the API normalises the key to "home.welcome.title" and updates the EXISTING row in place
  And NO second row is created (the grid row count is unchanged)
  And a green SimfAlert reads "Content block saved." (there is no duplicate/conflict error for this page)
  And the existing row's English preview now reads "Overwritten copy"
```

### E2E-CNT-006 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no ContentBlock rows
  When the administrator opens /admin/content-blocks
  Then once loading finishes the grid body is replaced by the SimfEmptyState component
  And the empty state title reads "No content blocks yet." / "لا توجد كتل بعد."
  And the toolbar still shows the "New block" button
  And no error SimfAlert appears
```

### E2E-CNT-007 — Auth gate (missing ContentBlocks.View permission)

```gherkin
Scenario: A signed-in admin lacking the ContentBlocks.View permission is denied
  Given a signed-in Control-Panel user whose role does NOT include "ContentBlocks.View"
    and who is not the Administrator wildcard ("*")
  When they navigate to /admin/content-blocks
  Then the [RequirePermission(PermissionCatalog.ContentBlocks.View)] gate redirects them to /not-permitted with HTTP 200
  And no POST /account/api/admin/content-blocks/list request fires
  And the "Content blocks" item is hidden in the nav rail (CpNavigation RequiredPermission = ContentBlocks.View)
```

### E2E-CNT-008 — Validation: too-short key

```gherkin
Scenario: A key shorter than 2 characters is rejected by the API
  Given the Add modal is open from "New block"
  When the administrator fills Key="a"   # 1 character, below the 2-128 bound
  And fills Content (English)="x"
  And clicks "Save"
  Then the BFF forwards PUT /account/api/admin/content-blocks
  And the API returns HTTP 400 with ApiResult.Error.Code = "CONTENT_BLOCK_INVALID"
  And the modal STAYS open (env.Success is false)
  And a red SimfAlert surfaces the bilingual MessageForCurrentCulture():
      "Content block key must be between 2 and 128 characters." /
      "يجب أن يتراوح طول مفتاح المحتوى بين 2 و 128 حرفاً."
```

### E2E-CNT-009 — Validation: content over 8000 characters

```gherkin
Scenario: Content longer than 8000 characters is rejected
  Given the Add modal is open from "New block"
  When the administrator fills Key="long.block"
  And fills Content (English) with a string of 8001 characters
  And clicks "Save"
  Then the API returns HTTP 400 with ApiResult.Error.Code = "CONTENT_BLOCK_INVALID"
  And the modal stays open
  And a red SimfAlert reads "Content cannot exceed 8000 characters." /
      "لا يمكن أن يتجاوز المحتوى 8000 حرف."
```

### E2E-CNT-010 — Delete a missing / already-removed key

```gherkin
Scenario: Deleting a key that no longer exists returns CONTENT_BLOCK_NOT_FOUND
  Given a content block with Key="stale.key" was already hard-removed from the DB out of band
  When the administrator clicks "Delete" on a row whose Key="stale.key"
  Then the BFF forwards DELETE /account/api/admin/content-blocks/stale.key
  And the API returns HTTP 404 with ApiResult.Error.Code = "CONTENT_BLOCK_NOT_FOUND"
  And a red SimfAlert surfaces "Content block not found." / "لم يتم العثور على المحتوى."

Scenario: Deleting an already-inactive block is idempotent (no error)
  Given a content block with Key="promo.banner" exists but is already inactive (Active = "—")
  When the administrator clicks "Delete" on that row
  Then the API returns HTTP 200 with ApiResult.Data = true (the deactivate is a no-op)
  And a green SimfAlert reads "Content block deleted."
```

### E2E-CNT-011 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the bilingual fallback toast
  Given the API is configured to return 500 on POST /admin/content-blocks/list (e.g. DB down)
  When the administrator opens /admin/content-blocks
  Then the page first shows "Loading content blocks…"
  And then a red SimfAlert appears reading
      "The content blocks could not be loaded." / "تعذّر تحميل كتل المحتوى."
  And no grid rows render
  And the "New block" button is still present
```

### E2E-CNT-012 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add modal
  Given the administrator is on /admin/content-blocks in English
  When they switch the culture to Arabic (العربية)
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "كتل المحتوى"
  And the toolbar button reads "كتلة جديدة"
  And the grid headers read "المفتاح", "الإنجليزية", "آخر تحديث", "مفعّل"
  And the nav rail mirrors with Arabic labels

  When they click "كتلة جديدة"
  Then the Edit modal opens in RTL titled "تعديل كتلة المحتوى"
  And the field labels read "المفتاح (مثلاً home.welcome.title)", "المحتوى (الإنجليزية)",
      "المحتوى (العربية)" and the checkbox label "مفعّل"
  And the footer actions read "إلغاء" and "حفظ" in reverse order
```

---

## Implementation notes

- **API integration tests** at `tests/SIMF.Api.Tests/CmsTests.cs` cover the
  same surface at a lower layer (no browser) and are the lower-tier safety net
  for these scenarios:
  - `Admin_upsert_creates_then_updates_in_place` — backs E2E-CNT-002 / -003 /
    -005 (proves the same id is reused on a second upsert of the same key).
  - `Delete_content_block_makes_subsequent_public_read_404` — backs
    E2E-CNT-004 / -010 (delete = deactivate; public read then 404s).
  - `Non_admin_caller_is_forbidden_on_content_block_upsert` — backs the API
    half of the auth gate E2E-CNT-007 (a Visitor token → HTTP 403 on upsert).
  - `Public_read_returns_active_block` / `Public_read_of_inactive_block_returns_404`
    / `If_modified_since_returns_304_when_unchanged` / `Public_batch_returns_only_existing_active_keys`
    — the public read side that consumes what this admin page writes.
  - `Cybersecurity_policy_blocks_are_seeded_by_IdentitySeeder` — guards the
    well-known `cyber.*` keys (a Flutter wire contract) that an admin must not
    break from this page.
  Note these tests hit the API directly at `/api/v1/admin/content-blocks`; the
  CP page reaches the same endpoints through the BFF passthrough at
  `/account/api/admin/content-blocks*` (see
  `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs`).

- **No client-side validation.** The razor performs no length checks before
  the PUT — every validation path (E2E-CNT-008 / -009) is enforced server-side
  in `AdminCmsService.UpsertContentBlockAsync` and surfaces back through
  `env.Error.MessageForCurrentCulture()`. Drive these scenarios by actually
  submitting the out-of-bound value, not by expecting an inline field error.

- **Manual smoke is the canonical run today.** Until Playwright is adopted,
  walk each scenario in a Chrome DevTools MCP session (sign in per the Auth
  setup, capture screenshots into `docs/screenshots/cp-admin-content-blocks-*.png`).

- **Convert to Playwright** when the runner lands: copy each Gherkin scenario
  into a `.feature` file under `tests/SIMF.E2E.Tests/` plus a step-definition
  class. The Gherkin is already runner-agnostic.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
