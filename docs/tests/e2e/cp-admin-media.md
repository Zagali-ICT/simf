# E2E test catalogue — Media Center (`/admin/media`)

| | |
|--|--|
| **Page** | [`cp/admin-media.md`](../../pages/cp/admin-media.md) |
| **Route** | `/admin/media` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **Page permission:** `PermissionCatalog.Media.View` gates the page
> (`@attribute [RequirePermission(PermissionCatalog.Media.View)]`). The mutating
> API endpoints are gated separately: create → `Media.Create`, edit + image
> upload → `Media.Edit`, delete → `Media.Delete`. A media item is either an
> **Image** (no URL; its bitmap is uploaded out-of-row via
> `POST /admin/media/{id}/image`, only possible once the row exists) or a
> **Video** (an external playback `Url`, required). There is **no unique
> business key**, so there is no 409/duplicate path — the negative paths are
> video-URL-required, oversized/empty image upload, and the 404 missing-item.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MED-001 | Golden round-trip — Add Image → save → attach bitmap → Edit → Delete | happy | P0 | _to author_ |
| E2E-MED-002 | Add Video item (URL required, modal closes on create) | happy | P0 | _to author_ |
| E2E-MED-003 | Edit existing item — change metadata + DisplayOrder + toggle Active | happy | P1 | _to author_ |
| E2E-MED-004 | Delete item — CrudShell ViewDelete + SimfConfirm → soft-delete → row leaves active list | happy | P1 | _to author_ |
| E2E-MED-005 | Image upload — pick file, Upload, `HasImage` flips to ✓ | happy | P1 | _to author_ |
| E2E-MED-006 | Kind toggle — Image⇄Video shows/hides Video URL + image controls | happy | P2 | _to author_ |
| E2E-MED-007 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-MED-008 | Auth gate — admin lacking `Media.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-MED-009 | Validation — Video with blank URL → bilingual error, no POST | error | P1 | _to author_ |
| E2E-MED-010 | Image upload rejected — empty / >10 MB file → bilingual 400 | error | P1 | _to author_ |
| E2E-MED-011 | Missing item — Edit/Delete a deleted id → 404 `MediaNotFound` | error | P2 | _to author_ |
| E2E-MED-012 | Server 500 on `/list` → bilingual fallback toast | resilience | P2 | _to author_ |
| E2E-MED-013 | Delete cancelled — Cancel the SimfConfirm dialog → no DELETE fires | edge | P2 | _to author_ |
| E2E-MED-014 | RTL render — Arabic toggle mirrors page + modal | i18n | P1 | _to author_ |
| E2E-MED-015 | Per-column filter narrows the grid (Title (English) + Album (English)) | grid | P1 | _to author_ |
| E2E-MED-016 | Column sort toggles (Title (English) / Display order) | grid | P2 | _to author_ |
| E2E-MED-017 | Presentation toggle: switch to full-page + persists across reload (D-353) | happy | P1 | _to author_ |
| E2E-MED-018 | Full-page mode: Add/Edit/View take over the content area, Save returns to grid (D-353) | happy | P1 | _to author_ |
| E2E-MED-019 | Delete confirmation gate: Deactivate opens CrudShell ViewDelete → SimfConfirm gates the DELETE (D-353) | error | P0 | _to author_ |
| E2E-MED-020 | Excel export: toolbar Export downloads an .xlsx of the filtered grid / selected rows (D-356) | happy | P1 | _to author_ |
| E2E-MED-021 | Excel import: upload a workbook → rows created + result modal with per-row outcome (D-356) | happy | P1 | _to author_ |
| E2E-MED-022 | Excel import: a non-workbook / wrong-sheet upload → bilingual rejection, nothing created (D-356) | error | P1 | _to author_ |

## Scenarios

### E2E-MED-001 — Golden round-trip (Image)

```gherkin
Feature: Media Center CRUD round-trip for an Image item
  As an Administrator
  I want to add an image media item, attach its bitmap, edit it, then delete it
  So that the public gallery (Mockup page 30) stays accurate

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator (superadmin@zagali-ict.com) has signed in via /login + /login/totp
  And they have landed on /admin/media

Scenario: Create an Image item, attach its bitmap, edit it, then delete it
  Given the grid currently shows {N} rows
  When the administrator clicks "Add media item"
  Then the Add modal opens titled "Add media item"
  And the Type select defaults to "Image"
  And the fields Title (English), Title (Arabic), Album (English), Album (Arabic), Display order, Active are visible
  And the "Video URL" field is NOT visible (Image kind)
  And a hint reads "Save the item first, then attach its image from the Edit screen."

  When they fill Title (English)="Opening Ceremony"
  And they fill Title (Arabic)="حفل الافتتاح"
  And they fill Album (English)="Day 1"
  And they fill Album (Arabic)="اليوم الأول"
  And they fill Display order="10"
  And they leave Active ticked
  And they click "Save"
  Then POST /account/api/admin/media returns 200 with ApiResult.Success=true
  And the modal stays open and switches into Edit mode (title "Edit media item")
  And a green toast reads "Saved. You can now attach the image below." / "تم الحفظ. يمكنك الآن إرفاق الصورة أدناه."
  And the grid now shows {N + 1} rows including a row with Type="Image", Title (English)="Opening Ceremony", Order=10, Image="—", Active="✓"

  When they choose a 200 KB PNG in the "Image file" picker
  Then the picked file name appears under the picker
  And the "Upload image" button enables
  When they click "Upload image"
  Then POST /account/api/admin/media/{id}/image returns 200
  And a green toast reads "Image uploaded." / "تم رفع الصورة."
  And the hint changes to "An image is attached to this item."
  And after the background reload the grid row shows Image="✓"

  When they click the row's Edit (pencil) action
  Then GET /account/api/admin/media/{id} returns 200 and the modal pre-fills the saved values
  When they change Display order to "0"
  And they click "Save"
  Then PUT /account/api/admin/media/{id} returns 200
  And a green toast reads "Media item saved." / "تم حفظ عنصر الوسائط."
  And the grid row's Order column reads "0"

  When they click the row's Delete (trash) action
  Then the View/Delete form opens (CrudShell) showing the read-only details and a red "Deactivate" button
  When they click "Deactivate" and confirm in the SimfConfirm dialog
  Then DELETE /account/api/admin/media/{id} returns 200
  And a green toast reads "Media item deleted." / "تم حذف عنصر الوسائط."
  And the grid returns to {N} active rows (soft-delete; IsActive=false)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-media-001-before.png` (grid baseline)
- Screenshots after: `docs/screenshots/cp-admin-media-001-add-modal.png`, `docs/screenshots/cp-admin-media-001-image-attached.png`, `docs/screenshots/cp-admin-media-001-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/media/...` call returns 200; the create POST, the `/image` POST, the GET-by-id, the PUT, and the DELETE all succeed
- Audit rows: `admin.media.created`, `admin.media.image.set`, `admin.media.updated`, `admin.media.deactivated` written with the actor's id

### E2E-MED-002 — Add Video item

```gherkin
Scenario: Add a Video item with a playback URL — modal closes on create
  Given the Add modal is open
  When the administrator changes Type to "Video"
  Then the "Video URL" field appears
  And the "Image file" picker / "Upload image" controls are NOT shown
  When they fill Title (English)="Highlights Reel"
  And they fill Video URL="https://www.youtube.com/watch?v=abc123"
  And they fill Display order="5"
  And they click "Save"
  Then POST /account/api/admin/media returns 200
  And the modal closes (Video items are complete on create, no bitmap step)
  And a green toast reads "Media item saved." / "تم حفظ عنصر الوسائط."
  And a grid row exists with Type="Video", Title (English)="Highlights Reel", Image="—", Order=5, Active="✓"
```

### E2E-MED-003 — Edit existing item

```gherkin
Scenario: Edit metadata, DisplayOrder and the Active flag of an existing item
  Given a media item "Opening Ceremony" exists in the grid
  When the administrator clicks the row's Edit (pencil) action
  Then GET /account/api/admin/media/{id} returns 200
  And the Edit modal pre-fills Type, both titles, both albums, Display order and the Active checkbox
  When they change Title (Arabic)="حفل الختام"
  And they change Display order="3"
  And they untick "Active"
  And they click "Save"
  Then PUT /account/api/admin/media/{id} returns 200
  And a green toast reads "Media item saved." / "تم حفظ عنصر الوسائط."
  And the row reflects the new Title (Arabic) and Order=3 and Active="—"
```

### E2E-MED-004 — Delete (soft-delete) with confirm

```gherkin
Scenario: Delete an item via the CrudShell ViewDelete form + SimfConfirm gate
  Given a media item exists in the active grid
  When the administrator clicks the row's Delete (trash) action
  Then the View/Delete form opens (CrudShell, dialog by default) showing the read-only details and a red "Deactivate" button
  When they click "Deactivate"
  Then a SimfConfirm dialog appears naming the item
  When they click the confirm "Deactivate" button
  Then DELETE /account/api/admin/media/{id} returns 200 with ApiResult.Data=true
  And a green toast reads "Media item deleted." / "تم حذف عنصر الوسائط."
  And the row is removed from the active list (IsActive set false server-side, not a hard delete)
  And the deactivation is idempotent — re-deleting the same id is still 200 with no second audit row
```

### E2E-MED-005 — Image upload

```gherkin
Scenario: Attach a bitmap to an Image item via the Edit modal
  Given an Image item with no bitmap (grid shows Image="—") is open in Edit mode
  Then the hint reads "No image attached yet."
  And the "Upload image" button is disabled (no file picked)
  When the administrator picks a JPEG/PNG/WEBP under 10 MB in the "Image file" input
  Then the file name renders and "Upload image" enables
  When they click "Upload image"
  Then POST /account/api/admin/media/{id}/image (multipart, antiforgery disabled) returns 200
  And the button shows its loading state while in flight
  And a green toast reads "Image uploaded." / "تم رفع الصورة."
  And the hint becomes "An image is attached to this item."
  And the grid row Image column shows "✓" after the reload
```

### E2E-MED-006 — Kind toggle reveals the right controls

```gherkin
Scenario: Switching Type between Image and Video shows the correct fields
  Given the Add modal is open with Type="Image"
  Then no "Video URL" field is shown
  And (in Add/new mode) the hint "Save the item first..." is shown instead of an image picker
  When the administrator changes Type to "Video"
  Then the "Video URL" field appears
  And no image picker / "Save first" hint is shown
  When they change Type back to "Image"
  Then the "Video URL" field disappears
  And the create-time "Save the item first..." hint returns
```

### E2E-MED-007 — Empty state

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no MediaItem rows (or none match the active list)
  When the administrator opens /admin/media
  And POST /account/api/admin/media/list returns 200 with Total=0
  Then the grid body renders the SimfEmptyState component titled "No media items yet" / "لا توجد عناصر وسائط بعد"
  And the toolbar still shows the "Add media item" button
  And no error toast appears
```

### E2E-MED-008 — Auth gate

```gherkin
Scenario: Signed-in admin lacking Media.View is denied
  Given a signed-in administrator whose role does NOT include the Media.View permission
  When they navigate to /admin/media
  Then they land on /not-permitted with HTTP 200 (the [RequirePermission(PermissionCatalog.Media.View)] gate)
  And no /account/api/admin/media/list request fires
  And the "Media" item is hidden from the CP nav rail (CpNavigation RequiredPermission = Media.View)
```

### E2E-MED-009 — Validation: Video without URL

```gherkin
Scenario: Saving a Video item with a blank URL shows a bilingual error and never POSTs
  Given the Add modal is open
  When the administrator changes Type to "Video"
  And leaves Video URL blank
  And clicks "Save"
  Then a SimfAlert error toast appears reading "A video item requires a playback URL." / "يتطلب عنصر الفيديو رابط تشغيل."
  And the modal stays open
  And no POST /account/api/admin/media request fires (client-side guard in SaveAsync)

Scenario: Server also rejects a blank Video URL (defence in depth)
  Given the client guard were bypassed
  When POST /account/api/admin/media is sent with Kind=Video and Url=null
  Then the API returns HTTP 400 with ApiResult.Error.Code = "MEDIA_INVALID"
  And the bilingual message is "A video media item requires a URL." / "يتطلّب عنصر الفيديو رابطاً."
```

### E2E-MED-010 — Image upload rejected

```gherkin
Scenario: Empty or oversized image upload returns a bilingual 400
  Given an Image item is open in Edit mode
  When the administrator uploads a 0-byte file
  Then POST /account/api/admin/media/{id}/image returns HTTP 400 with Error.Code = "VALIDATION_FAILED"
  And the message is "No file was uploaded." / "لم يتم رفع أي ملف."
  And a red toast reads "The image could not be uploaded." / "تعذّر رفع الصورة." (page fallback) or the surfaced server message

  When instead they upload a file larger than 10 MB
  Then the API returns HTTP 400 with Error.Code = "VALIDATION_FAILED"
  And the message is "Image must be 10 MB or smaller." / "يجب أن تكون الصورة 10 ميجابايت أو أقل."
  And the modal stays open and the grid Image column is unchanged
```

### E2E-MED-011 — Missing item (404)

```gherkin
Scenario: Editing or deleting an already-removed item returns 404
  Given a media item id that no longer exists (hard-removed out of band)
  When the administrator clicks the Edit (pencil) action on a stale row
  Then GET /account/api/admin/media/{id} returns HTTP 404 with Error.Code = "MEDIA_NOT_FOUND"
  And a red error toast surfaces "The media item was not found." / "لم يتم العثور على عنصر الوسائط."
  And the modal does not open

  When instead they PUT or DELETE that id
  Then the API returns HTTP 404 with the same Error.Code = "MEDIA_NOT_FOUND"
```

### E2E-MED-012 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the fallback bilingual toast
  Given the API is configured to return 500 on /admin/media/list (e.g. DB down)
  When the administrator opens /admin/media
  Then the page shows the "Loading media…" indicator briefly
  And then a red toast appears reading "The media list could not be loaded." / "تعذّر تحميل قائمة الوسائط."
  And no rows render and the SimfEmptyState is not shown
```

### E2E-MED-013 — Delete cancelled

```gherkin
Scenario: Cancelling the SimfConfirm dialog cancels the delete
  Given a media item exists in the grid
  When the administrator clicks the row's Delete (trash) action
  And the View/Delete form opens, they click "Deactivate"
  And in the SimfConfirm dialog they click "Cancel"
  Then no DELETE /account/api/admin/media/{id} request fires
  And the row stays in the grid unchanged
  And no toast appears
```

### E2E-MED-014 — RTL render

```gherkin
Scenario: Arabic toggle mirrors the page and the modal
  Given the administrator is on /admin/media in English
  When they switch the UI language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "مركز الوسائط"
  And the grid headers are Arabic (النوع / العنوان / الألبوم / الصورة / الترتيب / نشط)
  And the toolbar shows "إضافة عنصر وسائط"

  When they click "إضافة عنصر وسائط"
  Then the Add modal opens in RTL
  And the field labels are Arabic (النوع، العنوان، الألبوم، ترتيب العرض)
  And the Type options read "صورة" / "فيديو"
  And the footer actions (حفظ / إلغاء) appear in reverse order
```

### E2E-MED-015 — Per-column filter narrows the grid

```gherkin
Scenario: Typing into a per-column grid filter re-queries /list and narrows the rows
  Given the grid is showing the first page of 20 media items (GridQuery.Top=20, Skip=0)
  And several items have Title (English) starting with "Opening"
  When the administrator clicks the column-filter control on the "Title (English)" column
  And they type "Opening" into the per-column filter input for "Title (English)"
  Then POST /account/api/admin/media/list fires with GridQuery.Filters["titleEn"]="Opening"
  And GridQuery.Skip resets to 0 (back to the first page)
  And the grid re-renders showing only rows whose Title (English) contains "Opening"
  And the pager summary updates to the filtered Total (e.g. "Showing 1–3 of 3")

  When they also type "Day 1" into the per-column filter input for "Album (English)"
  Then the next POST /account/api/admin/media/list carries both filters
       GridQuery.Filters["titleEn"]="Opening" and GridQuery.Filters["albumEn"]="Day 1"
  And the grid narrows further to rows matching BOTH (server-side Contains, case-insensitive)

  When they clear both per-column filter inputs
  Then POST /account/api/admin/media/list fires with an empty GridQuery.Filters
  And the grid returns to the full active list
```

**Notes:** the four filterable columns are `titleEn`, `titleAr`, `albumEn`,
`albumAr` (each `Filterable="true"` on `MediaList.razor`). `AdminMediaService`
honours these keys (plus `isActive`) and ignores any unknown column key, so a
filter on a non-filterable column (`kind`, `hasImage`, `displayOrder`) is a
no-op server-side.

### E2E-MED-016 — Column sort toggles

```gherkin
Scenario: Clicking a sortable column header cycles ascending → descending
  Given the grid is showing media items ordered by the default DisplayOrder
  When the administrator clicks the "Title (English)" column header
  Then POST /account/api/admin/media/list fires with GridQuery.Sort="titleEn" and SortDescending=false
  And the grid re-orders A→Z by Title (English)
  When they click the "Title (English)" header again
  Then the next POST carries GridQuery.Sort="titleEn" and SortDescending=true
  And the grid re-orders Z→A

  When instead they click the "Display order" header
  Then POST /account/api/admin/media/list fires with GridQuery.Sort="displayOrder"
  And the grid orders by ascending DisplayOrder, then descending on a second click
```

**Notes:** the sortable columns are `kind`, `titleEn`, `displayOrder` and
`isActive` (each `Sortable="true"`). `titleAr`, `albumEn`, `albumAr` and
`hasImage` are NOT sortable. The default order (no Sort) is DisplayOrder
ascending then CreatedAt descending.

### E2E-MED-017 — Presentation toggle persists (D-353)

```gherkin
Scenario: Switch to full-page mode and it persists across reload
  Given the administrator is on /admin/media with the default "dialog" presentation
  And the grid toolbar shows the CrudPresentationToggle "Open as full page" control (maximize icon)
  When they click the toggle
  Then the toggle label changes to "Open as dialog" (window icon)
  And localStorage key "simf.cp.prefs.media" holds {"v":1,"presentation":"page"}
  When they reload /admin/media
  Then OnInitializedAsync re-reads CpPreferences.GetPresentationAsync("media")
  And the toggle still reads "Open as dialog"
  And opening "Add media item" now renders the full-page frame (not a popup)
```

### E2E-MED-018 — Full-page mode round-trip (D-353)

```gherkin
Scenario: Add/Edit/View take over the content area; Save returns to the grid
  Given the presentation is set to "full page" (GridHidden hides the grid while a form is open)
  When the administrator clicks "Add media item"
  Then the grid + SimfBanner are replaced by the CrudShell full-page frame (title "Add media item" + close header + the MediaAddEdit form)
  And there is no modal backdrop
  When they fill Type="Video" + Title (English)="Closing Reel" + Video URL="https://www.youtube.com/watch?v=zzz999" + Display order="2"
  And they click "Save"
  Then the page frame closes
  And the grid re-appears with the new Video row and the success toast "Media item saved." / "تم حفظ عنصر الوسائط."
  When they click the row's Edit (pencil) action and then the frame's Close button
  Then the form closes and the grid re-appears unchanged (no PUT fired)
```

**Note:** for an **Image** create the shell does NOT close on the first Save —
`MediaAddEdit` flips into Edit mode in place (full page) so the bitmap can be
attached; the shell only closes via OnSuccess on the Video create or any
Edit-mode save. Drive the Video branch above to see the page-frame round-trip.

### E2E-MED-019 — Delete confirmation gate (CrudShell + SimfConfirm) (D-353)

```gherkin
Scenario: Deactivate requires explicit confirmation through SimfConfirm
  Given the administrator is on /admin/media
  When they click the "Delete" (trash) icon on a row
  Then GET /account/api/admin/media/{id} returns 200
  And the View/Delete form (MediaViewDelete) opens in the CrudShell showing the read-only
      details (Type, both titles, both albums, Video URL or Has image, Display order, Active)
      and a red "Deactivate" button
  When they click "Deactivate"
  Then a SimfConfirm dialog appears whose message names the item via RowName (English title,
      else Arabic title, else the kind label) — "Deactivate this media item \"{name}\"?…"
  When they click "Cancel" in the SimfConfirm
  Then no DELETE request fires and the row is unchanged
  When they re-open, click "Deactivate" and click the confirm "Deactivate" button
  Then exactly one DELETE /account/api/admin/media/{id} fires and returns 200
  And the form closes and a green toast reads "Media item deleted." / "تم حذف عنصر الوسائط."
  And the row leaves the active list (soft-delete; IsActive=false)
```

**Note:** the delete no longer uses the native `confirm()` (D-353). On a server
failure the SimfConfirm closes first so the bilingual error lands on the visible
form body (`MediaViewDelete.ConfirmDeleteAsync`), not behind the overlay.

### E2E-MED-020 — Excel export (D-356)

```gherkin
Scenario: Export the filtered grid (and a selected subset) to an XLSX workbook
  Given the administrator is on /admin/media with at least two media items
  When they click the toolbar "Export" action with no rows selected
  Then OnExportAsync calls CrudGridExcel.ExportAsync(empty Ids, current _query)
  And a POST /account/api/admin/media/export fires carrying AdminGridExportRequest
      { Ids: [], Query: the current GridQuery } (Query is sent only when no rows are selected)
  And the browser saves an .xlsx workbook of the whole filtered grid
  And the workbook's media sheet has a header row of the exported media columns
  When they instead tick two rows then click "Export"
  Then the POST carries those two Ids and an empty Query
  And the workbook contains exactly those two rows
  And the API caps the export at 5000 rows
```

**Note:** the export Resource slug passed to `CrudGridExcel` is `media`
(`<CrudGridExcel @ref="_excel" Resource="media" … />`), so the BFF route is
`/account/api/admin/media/export` → API `/admin/media/export`.

### E2E-MED-021 — Excel import (D-356)

```gherkin
Scenario: Import media from a workbook and see the per-row outcome
  Given the administrator is on /admin/media
  When they click the toolbar "Import" action
  Then OnImportAsync calls CrudGridExcel.TriggerImportAsync() which clicks the hidden
      file <input id="media-import-input"> (accept=".xlsx")
  When they choose an .xlsx whose media sheet has rows for two new items
  Then a POST /account/api/admin/media/import fires as multipart form data
  And the import-result modal shows "2 created, 0 updated, 0 skipped." plus a (here empty) per-row error list
  And the page raises OnImported → the shared "Grid.Import.Done" success toast
  And the grid reloads (LoadAsync) and lists both new items
  When they import a workbook with one row that fails a row rule
  Then the modal shows that row under the per-row error list and counts it as skipped
  And the API caps the import at 5000 rows
```

### E2E-MED-022 — Excel import rejection (bad / wrong-sheet upload) (D-356)

```gherkin
Scenario: A bad upload is rejected without creating anything
  Given the administrator is on /admin/media
  When they import a file that is not a valid .xlsx (fails the ZIP-magic check) or exceeds the 5 MB gate
  Then the request returns HTTP 400
  And CrudGridExcel raises OnError → the page shows a bilingual error toast (OnExcelError)
  And no media item is created
  When they import a workbook whose sheet is not the expected media sheet
  Then the request returns HTTP 400 with the bilingual wrong-worksheet message
  And nothing is created
```

---

## Implementation notes

- **No 409/duplicate path by design.** `AdminMediaService` has no unique
  business key on a media item (unlike `Speaker.Code`), so the catalogue
  replaces the usual "duplicate" row with the video-URL-required guard
  (MED-009), the image-upload size/empty guard (MED-010) and the 404
  missing-item path (MED-011). This is documented in the service summary
  comment in `src/Backend/SIMF.Infrastructure/Media/AdminMediaService.cs`.
- **Two-step image flow.** Image bytes are written out-of-row via
  `IMediaImageStorage` and can only be attached after the row exists, so an
  Image create keeps the modal open in Edit mode (toast `SavedUploadNext`)
  whereas a Video create closes the modal. Drive both branches.
- **BFF passthrough.** Every page call goes through the CP BFF
  (`/account/api/admin/media...` in `AccountEndpoints.cs`), which forwards to
  the API `/admin/media...` endpoints with the bearer token. The image
  upload route is multipart and `.DisableAntiforgery()` (same stance as the
  visitor id-document upload, D-029).
- **API integration tests** at `tests/SIMF.Api.Tests/AdminMediaTests.cs`
  cover the same surface at a lower layer (no browser): `Create_then_get`,
  `Video_without_url_is_400_MEDIA_INVALID`, `Update_then_soft_delete_drops_from_public_list`,
  `Deactivate_is_idempotent`, `List_returns_a_page`,
  `Image_upload_sets_HasImage_and_public_image_streams`,
  `Anonymous_caller_is_unauthorized_on_create`,
  `Non_admin_caller_is_forbidden_on_create`, `Get_returns_404_for_unknown_id`.
  When E2E covers a scenario you can usually drop the matching `Api.Tests`
  case — but keep both during the transition.
- **Convert to Playwright** when adopted: copy each Gherkin scenario into a
  `.feature` file under `tests/SIMF.E2E.Tests/` + step definitions; the shape
  is already runner-agnostic.

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle): added E2E-MED-017..022 (toggle persist, full-page round-trip, CrudShell+SimfConfirm delete gate, Excel export, Excel import, Excel import rejection) and corrected the stale native-`confirm()` delete copy in MED-001/004/013 to the shipped CrudShell + SimfConfirm flow.
