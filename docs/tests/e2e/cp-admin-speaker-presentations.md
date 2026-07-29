# E2E test catalogue — Speaker presentations (`/admin/speaker-presentations`)

| | |
|--|--|
| **Page** | [`cp/admin-speaker-presentations.md`](../../pages/cp/admin-speaker-presentations.md) |
| **Route** | `/admin/speaker-presentations` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **Page shape (read from `SpeakerPresentationsList.razor`, D-228 / FR-407, SIMF-FDS-004 §5.3;
> converted raw table → `SimfDataGrid` by D-256).**
> This is a master-detail upload page, not a grid-with-modals CRUD page. It has:
> a **Speaker** `<select>` (always visible); and — once a speaker is picked and only
> for a user holding `Speakers.Edit` — a **Session** `<select>`, a **Presentation file**
> `<input type="file" id="presentation-file-input">`, and an **Upload** button. Below
> that sits a **`SimfDataGrid`** of the speaker's presentations with columns
> **File** (`fileName`, sortable + per-column filter), **Session** (`sessionTitle`,
> sortable + per-column filter) and **Size** (`sizeBytes`, sortable only). Each row
> exposes **quiet icon actions** in the grid's `RowActions`: a **Download** (download
> icon) shown for `Speakers.View` that `window.open(...)`s the file endpoint, and —
> behind `Speakers.Edit` — a **Delete** (trash icon, `Danger`) that fires a native
> `confirm()` before calling the API.
>
> **Grid behaviour is client-side.** The list endpoint
> (`GET /admin/speakers/{id}/presentations`) returns the speaker's full, non-paged
> set in one call; `_allRows` holds it and the grid's filter/sort/page are applied
> **in memory** by `BuildPage()` (no server round-trip on filter, sort or paging).
> The grid is `Multiselect="true"` (select-all + per-row checkbox) but there is **no**
> `CustomToolbar` and **no** bulk action wired — selection is cosmetic here. Page
> size is **`Top = 20`** (`new() { Top = 20 }`), reset to 20 whenever the speaker
> changes. Only the honoured filter keys are `fileName` and `sessionTitle`.
>
> **Permissions:** the page itself is gated by `PermissionCatalog.Speakers.View`
> (`@attribute [RequirePermission(PermissionCatalog.Speakers.View)]`). Upload, the
> session/file fields, and Delete are wrapped in `<AuthorizedAction Permission="Speakers.Edit">`.
> There is **no** `Add` modal, no edit, no display-order, no soft-delete toggle in the UI —
> Delete is a hard soft-delete (`Deactivate()`) on the API side and the row disappears.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-SPP-001 | Golden round-trip — pick speaker → pick session → upload file → row appears → download → delete | happy | P0 | _to author_ |
| E2E-SPP-002 | Speaker `<select>` populated from `/admin/speakers/list`; Session `<select>` populated from active sessions only | happy | P1 | _to author_ |
| E2E-SPP-003 | Empty state — speaker with no presentations renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-SPP-004 | Auth gate — signed-in user lacking `Speakers.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-SPP-005 | View-only gate — user with `Speakers.View` but not `Speakers.Edit` sees no upload form, no Delete | auth | P0 | _to author_ |
| E2E-SPP-006 | Upload button disabled until a session is chosen | error | P1 | _to author_ |
| E2E-SPP-007 | Upload with no file selected → 400 `SPEAKER_PRESENTATION_INVALID` bilingual toast | error | P1 | _to author_ |
| E2E-SPP-008 | Upload over 50 MB → 400 `SPEAKER_PRESENTATION_INVALID` size message | error | P1 | _to author_ |
| E2E-SPP-009 | Upload against a deactivated / unknown session → 404 `SESSION_NOT_FOUND` | error | P1 | _to author_ |
| E2E-SPP-010 | Download link opens the stored file in a new tab (attachment, original filename) | happy | P1 | _to author_ |
| E2E-SPP-011 | Delete confirm — cancelling the native `confirm()` makes no API call | error | P1 | _to author_ |
| E2E-SPP-012 | Delete is idempotent — second delete of the same id still returns success | resilience | P2 | _to author_ |
| E2E-SPP-013 | Server 500 on `/presentations` list → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-SPP-014 | RTL render — Arabic toggle mirrors page, labels, table, buttons | i18n | P1 | _to author_ |
| E2E-SPP-015 | Per-column filter — typing in the File / Session column filter narrows the grid (client-side, Skip→0) | happy | P1 | _to author_ |
| E2E-SPP-016 | Column sort toggles — clicking the File column header sorts asc then desc (client-side) | happy | P2 | _to author_ |
| E2E-SPP-017 | Excel export (D-356) — toolbar Export downloads an .xlsx of the selected speaker's presentations (whole set vs selected rows) | happy | P1 | _to author_ |
| E2E-SPP-018 | Regression (D-794) — a speaker with no photo renders the placeholder and issues NO asset request | regression | P1 | 2026-07-29 PASS |
| E2E-SPP-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-SPP-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-SPP-018 — Regression: no asset request for a photoless speaker (D-794)

> **Why this scenario exists.** The speaker picker built the photo URL for every
> speaker regardless of whether one exists, so each of the 9 photoless speakers
> (of 32) produced a 404. `SimfImageThumb` degrades so gracefully that nothing was
> visible on screen — the only symptom was 9 failed requests per page load.
> `CpAssetUrls.AdminImage`'s own summary states callers must render it "only when
> they know an asset exists (a `Has…` flag)"; `/admin/speakers` obeys that and
> this page did not.

```gherkin
Feature: The speaker picker only asks for photos that exist
  As an administrator opening the presentations page
  I want speakers without a headshot to show the placeholder
  So that the page issues no request that is known in advance to 404

Background:
  Given an Administrator with the Speakers.View permission has signed in
  And the roster holds 32 speakers, of which 23 have a SpeakerPhoto asset

Scenario: No failed asset request on first render
  When I open /admin/speaker-presentations
  Then 23 requests to /account/api/admin/assets/SpeakerPhoto/{id}/image are issued
  And every one of them returns 200
  And no request is issued for any of the 9 speakers whose HasPhoto is false
  And the network log contains zero 404 responses

Scenario: A photoless speaker still renders a usable card
  When I open /admin/speaker-presentations
  Then each photoless speaker's card shows the SimfImageThumb placeholder icon
  And the card is still clickable and shows the speaker's name and country
```

**Automated by** the WS4 CP element sweep (`E2E-SPP-ELS-002`), which fails the
route on any broken image or any same-origin asset returning >= 400.

### E2E-SPP-001 — Golden round-trip

```gherkin
Feature: Speaker presentation file management round-trip
  As an Administrator with the Speakers.Edit permission
  I want to attach, download and remove a speaker's presentation file for a session
  So that session decks are available to the programme team

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator (superadmin@zagali-ict.com) has signed in via /login + /login/totp using the Get-Totp helper
  And at least one active Speaker ("Dr. Speaker") and one active Session ("Keynote") exist
  And the Administrator has landed on /admin/speaker-presentations
  And the page title reads "Speaker presentations"

Scenario: Pick speaker, pick session, upload, download, delete
  Given the Speaker dropdown shows the placeholder "— Select a speaker —"
  And no Session dropdown, file input, Upload button or table are visible yet
  When the administrator selects Speaker "Dr. Speaker"
  Then a GET /account/api/admin/speakers/{speakerId}/presentations call returns 200
  And the Session dropdown, the "Presentation file" input and the "Upload" button become visible
  And the Session dropdown shows the placeholder "— Select a session —"

  When the administrator selects Session "Keynote"
  And they choose a local file "deck.pdf" (application/pdf, ~8 bytes) in the "Presentation file" input
  And they click "Upload"
  Then the BFF forwards POST /account/api/admin/speakers/{speakerId}/presentations?sessionId={sessionId} with the file as multipart "file"
  And the API returns HTTP 200 with ApiResult.Data.FileName = "deck.pdf"
  And a green toast reads "Presentation uploaded." / "تم رفع العرض."
  And the table refreshes and shows a row with File="deck.pdf", Session="Keynote", Size formatted (e.g. "8 B")

  When the administrator clicks the row's Download (download icon) action
  Then window.open fires GET /account/api/admin/speaker-presentations/{rowId}/file in a new tab
  And the response is HTTP 200 with Content-Disposition: attachment; filename="deck.pdf"
  And the downloaded bytes equal the uploaded payload

  When the administrator clicks the row's Delete (trash icon) action
  And confirms the native dialog "Remove this presentation file?"
  Then the BFF forwards DELETE /account/api/admin/speaker-presentations/{rowId}
  And the API returns HTTP 200 with ApiResult.Data = true
  And a green toast reads "Presentation removed." / "تمت إزالة العرض."
  And the table refreshes and the "deck.pdf" row is gone
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-speaker-presentations-001-before.png` (speaker picked, empty table)
- Screenshot after upload: `docs/screenshots/cp-admin-speaker-presentations-001-uploaded.png`
- Screenshot after delete: `docs/screenshots/cp-admin-speaker-presentations-001-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/speakers/*` and `/account/api/admin/speaker-presentations/*` call returns 200
- Audit rows: an `OperationLog` row with `Event = 'SpeakerPresentation.Uploaded'` and `Detail` containing `presentationId=…; speakerId=…; sessionId=…; file=deck.pdf; bytes=8`, then a row with `Event = 'SpeakerPresentation.Deleted'`, both carrying the actor's id.

### E2E-SPP-002 — Dropdowns populated correctly

```gherkin
Scenario: Speaker list comes from /admin/speakers/list, session list is active-only
  Given the page has loaded on /admin/speaker-presentations
  Then a POST /account/api/admin/speakers/list with { Top: 500 } returned 200
  And the Speaker dropdown lists every returned speaker by Name plus the leading placeholder
  And a POST /account/api/admin/sessions/list with { Top: 500 } returned 200
  When the administrator selects any speaker
  Then the Session dropdown lists only sessions where IsActive = true
  And a deactivated session is NOT offered as an upload target
```

### E2E-SPP-003 — Empty state

```gherkin
Scenario: Speaker with no presentations renders SimfEmptyState
  Given a speaker "Dr. Speaker" exists with zero active presentation files
  When the administrator selects that speaker
  Then GET /account/api/admin/speakers/{speakerId}/presentations returns 200 with an empty list
  And the grid renders its EmptyTemplate (no data rows)
  And the SimfEmptyState shows the bilingual copy
    "This speaker has no presentation files yet." / "لا توجد ملفات عروض لهذا المتحدث بعد."
  And no error toast appears
```

### E2E-SPP-004 — Auth gate (no Speakers.View)

```gherkin
Scenario: User without Speakers.View is denied the page
  Given a signed-in admin whose role does NOT grant PermissionCatalog.Speakers.View
  When they navigate to /admin/speaker-presentations
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/speakers/list request fires
  And the "Module.SpeakerPresentations" nav item is hidden from their rail
```

### E2E-SPP-005 — View-only gate (Speakers.View without Speakers.Edit)

```gherkin
Scenario: Read-only admin can browse but cannot upload or delete
  Given a signed-in admin whose role grants Speakers.View but NOT Speakers.Edit
  When they navigate to /admin/speaker-presentations and select a speaker
  Then the presentation grid and per-row Download (download icon) actions render
  But the Session dropdown, the "Presentation file" input and the "Upload" button are NOT rendered
  And the per-row Delete (trash icon) action is NOT rendered
  And the GET /account/api/admin/speakers/{speakerId}/presentations call still returns 200
```

### E2E-SPP-006 — Upload disabled without a session

```gherkin
Scenario: Upload button is disabled until a session is selected
  Given the administrator has selected a speaker
  And the Session dropdown still shows "— Select a session —"
  Then the "Upload" button is disabled
  When they select a session
  Then the "Upload" button becomes enabled
```

### E2E-SPP-007 — Upload with no file

```gherkin
Scenario: Clicking Upload with no file chosen returns a bilingual validation error
  Given the administrator has selected a speaker and a session
  And no file is chosen in the "Presentation file" input
  When they click "Upload"
  Then the BFF rejects the empty form with 400 ApiResult.Error.Code = "SPEAKER_PRESENTATION_INVALID"
  And a red toast surfaces the MessageForCurrentCulture():
    "A presentation file is required." / "ملف العرض مطلوب."
  And the table is not refreshed with a new row
```

### E2E-SPP-008 — Upload over the 50 MB cap

```gherkin
Scenario: Uploading a file larger than 50 MB is rejected
  Given the administrator has selected a speaker and a session
  And they choose a file larger than 50 MB in the "Presentation file" input
  When they click "Upload"
  Then the API returns HTTP 400 with ApiResult.Error.Code = "SPEAKER_PRESENTATION_INVALID"
  And the message reads "The presentation file must be 50 MB or smaller." / "يجب ألا يتجاوز حجم ملف العرض 50 ميجابايت."
  And a red error toast is shown
  And no new row appears in the table
```

### E2E-SPP-009 — Upload against an unknown / inactive session

```gherkin
Scenario: Upload referencing a session that does not exist (or is deactivated) returns 404
  Given the administrator has selected a valid speaker
  And the upload posts ?sessionId={a GUID with no active Session}
  When the upload is submitted
  Then the API returns HTTP 404 with ApiResult.Error.Code = "SESSION_NOT_FOUND"
  And the message reads "The session was not found." / "لم يتم العثور على الجلسة."
  And a red error toast is shown
  And no SpeakerPresentation row is created
```

### E2E-SPP-010 — Download opens the stored file

```gherkin
Scenario: Download action streams the original file as an attachment
  Given a presentation row "deck.pdf" exists for the selected speaker
  When the administrator clicks the row's Download (download icon) action
  Then window.open opens GET /account/api/admin/speaker-presentations/{rowId}/file in a new tab
  And the API returns HTTP 200
  And the Content-Disposition header is attachment; filename="deck.pdf"
  And the body bytes equal the originally uploaded payload
```

### E2E-SPP-011 — Cancel the delete confirm

```gherkin
Scenario: Cancelling the native confirm() makes no delete call
  Given a presentation row exists for the selected speaker
  When the administrator clicks the row's Delete (trash icon) action
  And dismisses the native dialog "Remove this presentation file?" / "هل تريد إزالة ملف العرض هذا؟"
  Then NO DELETE /account/api/admin/speaker-presentations/{rowId} request fires
  And the row remains in the table
  And no toast appears
```

### E2E-SPP-012 — Delete is idempotent

```gherkin
Scenario: Deleting an already-removed presentation still returns success
  Given a presentation row was just deleted (its API id is known)
  When a second DELETE /account/api/admin/speaker-presentations/{rowId} is issued
  Then the API returns HTTP 200 with ApiResult.Data = true (the service short-circuits an inactive row)
  And no second SpeakerPresentation.Deleted audit row is written
```

### E2E-SPP-013 — Server 500 on the list call

```gherkin
Scenario: API 500 on the presentations list shows the bilingual fallback toast
  Given the API is configured to return 500 on /admin/speakers/{speakerId}/presentations (e.g. DB down)
  When the administrator selects a speaker
  Then the page shows "Loading presentations…" / "جارٍ تحميل العروض…" briefly
  And then a red toast appears reading
    "The action could not be completed. Please try again." / "تعذّر إكمال العملية. يُرجى المحاولة مرة أخرى."
  And no rows render
```

### E2E-SPP-014 — RTL render

```gherkin
Scenario: Arabic toggle mirrors the whole page
  Given the administrator is on /admin/speaker-presentations in English
  When they switch the UI to Arabic ("العربية")
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "عروض المتحدثين"
  And the Speaker label reads "المتحدث" with placeholder "— اختر متحدثاً —"
  And after picking a speaker the Session label reads "الجلسة", the file label "ملف العرض", the button "رفع"
  And the grid column headers read "الملف", "الجلسة", "الحجم"
  And the per-row icon actions carry the titles "تنزيل" (Download) and "حذف" (Delete)
  And the nav rail mirrors and the grid + controls appear in reverse order
```

### E2E-SPP-015 — Per-column filter narrows the grid

```gherkin
Scenario: Typing a value in a column filter input filters the grid in memory
  Given the administrator has selected a speaker with several presentation rows
  And the grid lists rows spanning more than one File name and Session
  And the grid shows the per-column "Filter column" inputs (placeholder "Search")
  When the administrator types "deck" into the File column filter input
  Then OnQueryChanged fires with GridQuery.Filters["fileName"] = "deck"
  And GridQuery.Skip is reset to 0 (first page)
  And NO /account/api/admin/speakers/{speakerId}/presentations request fires
    (BuildPage filters the already-loaded _allRows in memory, case-insensitive Contains)
  And the grid shows only rows whose File contains "deck"
  And the summary updates to "Showing 1–{n} of {n}" for the narrowed count
  When the administrator clears the File filter and types "Keynote" into the Session column filter
  Then OnQueryChanged fires with GridQuery.Filters["sessionTitle"] = "Keynote"
  And the grid shows only rows whose Session contains "Keynote"
  And still no list call is made (the Size column has no filter input — sizeBytes is sort-only)
```

### E2E-SPP-016 — Column sort toggles

```gherkin
Scenario: Clicking a sortable column header toggles ascending / descending in memory
  Given the administrator has selected a speaker with several presentation rows
  And no explicit sort is applied (rows default to newest-first by CreatedAt)
  When the administrator clicks the "File" column header
  Then OnQueryChanged fires with GridQuery.Sort = "fileName" and SortDescending = false
  And NO list request fires (BuildPage re-orders _allRows in memory)
  And the rows are ordered A→Z by File name
  When the administrator clicks the "File" column header again
  Then OnQueryChanged fires with GridQuery.Sort = "fileName" and SortDescending = true
  And the rows are ordered Z→A by File name
  And sorting on "Size" instead applies Sort = "sizeBytes" (smallest → largest, then toggled)
```

### E2E-SPP-017 — Excel export (D-356)

```gherkin
Scenario: Export the selected speaker's presentations to an XLSX workbook
  Given the administrator is on /admin/speaker-presentations
  And they have selected Speaker "Dr. Speaker" who has at least two presentation rows
  And the grid toolbar shows the "Export" action (this page is export-only — there is no Import button)
  When the administrator clicks "Export" with no rows selected
  Then simfAccount.downloadXlsx fires a POST /account/api/admin/speaker-presentations/export
  And the body is an AdminGridExportRequest with an empty Ids list and Query.Filters["speakerId"] = the selected speaker's id
  And the API authorises against PermissionCatalog.Speakers.Export
  And the endpoint lists that speaker's files via ListForSpeakerAsync (master-detail; no GridQuery list)
  And the browser saves a file named simf-speaker-presentations-{timestamp}.xlsx
  And the workbook's "SpeakerPresentations" sheet has the header row
    FileName | Session | SessionArabic | ContentType | SizeBytes | CreatedAt
  And the sheet contains one data row per active presentation of that speaker

  When the administrator instead selects exactly two rows then clicks "Export"
  Then the POST body carries those two row Ids in Ids (plus the same speakerId filter)
  And the workbook contains exactly those two rows

  When no speaker is selected (the placeholder "— Select a speaker —" is showing)
  Then there is no grid and therefore no Export action to invoke
    (with no speakerId filter the endpoint would return an empty workbook — the grid's
     "one speaker at a time" contract is preserved)
```

**Evidence captured:**
- Export with no selection → network shows POST `/account/api/admin/speaker-presentations/export` returning 200 with `Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- The saved workbook's first row matches `FileName | Session | SessionArabic | ContentType | SizeBytes | CreatedAt`
- Console errors: 0 expected
- The API caps the export at 5000 rows

---

## Implementation notes

- **API integration tests already cover this surface at a lower layer:**
  `tests/SIMF.Api.Tests/SpeakerPresentationsTests.cs` covers the upload → list →
  download round-trip (`Upload_then_list_then_download_round_trips_the_file`),
  the delete-removes-from-list path (`Delete_removes_the_presentation_from_the_list`),
  the unknown-session 404 (`Upload_for_an_unknown_session_is_404` →
  `ErrorCodes.SessionNotFound`), and the non-admin 403
  (`Non_admin_cannot_upload_a_presentation`). These map to E2E-SPP-001/010,
  E2E-SPP-001 (delete), E2E-SPP-009, and E2E-SPP-004/005 respectively. The
  empty-file 400, the 50 MB cap, the cancel-confirm UI path, the empty state,
  the 500 fallback, and the RTL render are **browser-only** and have no
  lower-layer twin yet.
- **Manual smoke is canonical today.** Until a Playwright project exists, the
  canonical run is a Chrome DevTools MCP session: sign in per the Auth setup,
  walk each scenario, and capture screenshots into
  `docs/screenshots/cp-admin-speaker-presentations-{scenario}.png`. The Gherkin
  steps are runner-agnostic and convert 1:1 to `.feature` files under
  `tests/SIMF.E2E.Tests/` when that runner is adopted.
- **Surface facts** are grounded in: the page
  `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SpeakerPresentationsList.razor`,
  the BFF routes in `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs`
  (lines ~1947-1995), the API endpoints in
  `src/Backend/SIMF.Api/Endpoints/Admin/SpeakerPresentationEndpoints.cs`, the
  service `src/Backend/SIMF.Infrastructure/Programme/AdminSpeakerPresentationService.cs`
  (50 MB cap, audit events), the error codes in `src/Shared/SIMF.Common/ErrorCodes.cs`
  (`SPEAKER_PRESENTATION_INVALID`, `SPEAKER_PRESENTATION_NOT_FOUND`, `SESSION_NOT_FOUND`,
  `SPEAKER_NOT_FOUND`), the audit keys in
  `src/Backend/SIMF.Application/Auditing/AuditEvents.cs`
  (`SpeakerPresentation.Uploaded` / `SpeakerPresentation.Deleted`), the nav entry
  `Module.SpeakerPresentations` in `src/ControlPanel/SIMF.ControlPanel/CpNavigation.cs`
  (`RequiredPermission: PermissionCatalog.Speakers.View`), and the bilingual strings
  in `Resources/Strings.resx` + `Resources/Strings.ar.resx`.

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle). Added E2E-SPP-017
(Excel export). This page is **export-only** (no import — the export endpoint explicitly
adds no generic import) and has **no presentation toggle**; Delete still uses the native
`confirm()` (no SimfConfirm/CrudShell on this master-detail upload page), so no toggle,
import, or SimfConfirm-delete scenarios were added.
Prior: 2026-06-03 (E2E catalogue rebuild) (D-256/D-257 grid affordances reconciled).
