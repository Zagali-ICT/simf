# E2E test catalogue — Speaker presentations (`/admin/speaker-presentations`)

| | |
|--|--|
| **Page** | [`cp/admin-speaker-presentations.md`](../../pages/cp/admin-speaker-presentations.md) |
| **Route** | `/admin/speaker-presentations` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Page shape (read from `SpeakerPresentationsList.razor`, D-228 / FR-407, SIMF-FDS-004 §5.3).**
> This is a master-detail upload page, not a grid-with-modals CRUD page. It has:
> a **Speaker** `<select>` (always visible); and — once a speaker is picked and only
> for a user holding `Speakers.Edit` — a **Session** `<select>`, a **Presentation file**
> `<input type="file" id="presentation-file-input">`, and an **Upload** button. Below
> that sits the presentation table (`File`, `Session`, `Size`, actions) with a per-row
> **Download** link (`target="_blank"`) and, again behind `Speakers.Edit`, a **Delete**
> button that fires a native `confirm()` before calling the API.
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

## Scenarios

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

  When the administrator clicks the "Download" link on that row
  Then a new tab opens GET /account/api/admin/speaker-presentations/{rowId}/file
  And the response is HTTP 200 with Content-Disposition: attachment; filename="deck.pdf"
  And the downloaded bytes equal the uploaded payload

  When the administrator clicks "Delete" on that row
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
  And the table is not rendered
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
  Then the presentation table and Download links render
  But the Session dropdown, the "Presentation file" input and the "Upload" button are NOT rendered
  And the per-row "Delete" button is NOT rendered
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
Scenario: Download link streams the original file as an attachment
  Given a presentation row "deck.pdf" exists for the selected speaker
  When the administrator clicks the row's "Download" link (target="_blank")
  Then a GET /account/api/admin/speaker-presentations/{rowId}/file is made
  And the API returns HTTP 200
  And the Content-Disposition header is attachment; filename="deck.pdf"
  And the body bytes equal the originally uploaded payload
```

### E2E-SPP-011 — Cancel the delete confirm

```gherkin
Scenario: Cancelling the native confirm() makes no delete call
  Given a presentation row exists for the selected speaker
  When the administrator clicks "Delete"
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
  And the table headers read "الملف", "الجلسة", "الحجم"
  And the per-row actions read "تنزيل" (Download) and "حذف" (Delete)
  And the nav rail mirrors and the controls appear in reverse order
```

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

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
