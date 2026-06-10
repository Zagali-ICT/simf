# E2E test catalogue — Sessions CRUD + broadcast lifecycle (`/admin/sessions`)

| | |
|--|--|
| **Page** | [`cp/admin-sessions.md`](../../pages/cp/admin-sessions.md) — source `SessionsList.razor` + `SessionsAddEdit.razor` + `SessionsViewDelete.razor` (D-353 CrudShell) |
| **Route** | `/admin/sessions` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **Permissions.** The page is gated `@attribute [RequirePermission(PermissionCatalog.Sessions.View)]`
> (`"Sessions.View"`). CRUD actions sit behind distinct codes:
> `Sessions.Create`, `Sessions.Edit`, `Sessions.Delete`. The **broadcast
> lifecycle** transition buttons AND the **recording** upload/remove block are
> gated by a separate code `Sessions.Publish` (`<AuthorizedAction
> Permission="PermissionCatalog.Sessions.Publish">`) — held by the Scientific
> Committee role — so an admin with View/Edit but not Publish sees the Details
> modal **without** the lifecycle footer or the recording uploader. The
> superadmin wildcard (`Administrator = "*"`) satisfies all of these.

> **API surface (BFF passthrough → API).** Every call goes through the CP BFF
> (`/account/api/...`) which forwards to the API:
> - `POST /account/api/admin/sessions/list` → `POST /admin/sessions/list` (`Sessions.View`)
> - `GET  /account/api/admin/sessions/{id}` → `GET  /admin/sessions/{id}` (`Sessions.View`)
> - `POST /account/api/admin/sessions` → `POST /admin/sessions` (`Sessions.Create`)
> - `PUT  /account/api/admin/sessions/{id}` → `PUT  /admin/sessions/{id}` (`Sessions.Edit`)
> - `DELETE /account/api/admin/sessions/{id}` → `DELETE /admin/sessions/{id}` (`Sessions.Delete`, soft-delete)
> - `PUT  /account/api/admin/sessions/{id}/status` → `PUT  /admin/sessions/{id}/status` (`Sessions.Publish`)
> - `POST /account/api/admin/sessions/{id}/recording` → multipart upload (`Sessions.Publish`)
> - `DELETE /account/api/admin/sessions/{id}/recording` (`Sessions.Publish`)
>
> The form also lazy-loads pickers: `POST .../halls/list`, `.../speakers/list`,
> `.../themes/list`, `.../session-categories/list` (all `Top=500`, `isActive=true`).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-SES-001 | Full CRUD round-trip — Add → Edit → Details → Deactivate | happy | P0 | _to author_ |
| E2E-SES-002 | Add form: Hall + Speakers (with role + reorder) + Themes + Category pickers | happy | P1 | _to author_ |
| E2E-SES-003 | Broadcast lifecycle — Scheduled → Held → Recorded → Published (+ reverse) | happy | P0 | _to author_ |
| E2E-SES-004 | Recording — upload video → Details shows file + size → remove | happy | P1 | _to author_ |
| E2E-SES-005 | Capacity override blank → "Inherits from hall"; numeric → effective capacity | happy | P2 | _to author_ |
| E2E-SES-006 | Grid: filter by Title, sort by Start, paginate | happy | P2 | _to author_ |
| E2E-SES-007 | Empty list renders `SimfEmptyState` ("No sessions yet.") | happy | P1 | _to author_ |
| E2E-SES-008 | Auth: signed-in user lacking `Sessions.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-SES-009 | Auth: View/Edit but not `Sessions.Publish` → no lifecycle/recording controls | auth | P0 | _to author_ |
| E2E-SES-010 | Validation: blank Code / 1-char Code → bilingual modal error | error | P1 | _to author_ |
| E2E-SES-011 | Validation: End ≤ Start → bilingual time-window error | error | P1 | _to author_ |
| E2E-SES-012 | Conflict: duplicate Code → 409 `SESSION_CODE_DUPLICATE` | error | P1 | _to author_ |
| E2E-SES-013 | Conflict: illegal lifecycle move → 400 `SESSION_STATUS_TRANSITION_INVALID` | error | P1 | _to author_ |
| E2E-SES-014 | Validation: inactive/unknown Hall → 400 `SESSION_HALL_NOT_FOUND` | error | P2 | _to author_ |
| E2E-SES-015 | Recording: non-video upload → 400 `SESSION_RECORDING_INVALID` | error | P2 | _to author_ |
| E2E-SES-016 | Server 500 on `/list` → bilingual fallback toast | resilience | P2 | _to author_ |
| E2E-SES-017 | RTL/Arabic render mirrors grid + Add modal + lifecycle footer | i18n | P1 | _to author_ |
| E2E-SES-018 | Live-URL validation (D-349) — YouTube/HLS accepted; other URL → bilingual error (client + 400 `SESSION_INVALID`) | error | P1 | _to author_ |
| E2E-SES-019 | Presentation toggle: switch to full-page + persists across reload (D-353) | happy | P1 | _to author_ |
| E2E-SES-020 | Full-page mode: Add/Edit/View take over the content area, Save returns to grid (D-353) | happy | P1 | _to author_ |
| E2E-SES-021 | Delete confirmation: Deactivate opens View/Delete → SimfConfirm gates the call (D-353) | error | P0 | _to author_ |
| E2E-SES-022 | Excel export: toolbar Export downloads an .xlsx of the filtered grid / selected rows (D-356) | happy | P1 | _to author_ |
| E2E-SES-023 | Excel import: upload a workbook → rows created + result modal with per-row outcome (D-356) | happy | P1 | _to author_ |
| E2E-SES-024 | Excel import: a non-workbook / wrong-sheet upload → bilingual rejection, nothing created (D-356) | error | P1 | _to author_ |

## Scenarios

### E2E-SES-001 — Full CRUD round-trip

```gherkin
Feature: Sessions CRUD round-trip
  As an Administrator
  I want to manage the programme sessions catalogue
  So that the public agenda and the Flutter app show the correct line-up

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And the Website is reachable on http://localhost:5115
  And an Administrator has signed in via /login + /login/totp using the Get-Totp helper
  And they have landed on /admin/sessions
  And at least one active Hall named "Auditorium A" (code "AUD-A") exists

Scenario: Create, edit, view, deactivate one session
  Given the grid currently shows {N} rows
  When the administrator clicks "Add session"
  Then the Add modal opens titled "Add session"
  And it shows the fields: Code, Title (English), Title (Arabic), Description (English),
      Description (Arabic), Hall, Category, Start (UTC), End (UTC), Capacity override,
      Add speaker, Add theme
  When they fill Code="SES-001"
  And they fill Title (English)="Future of Naval Logistics"
  And they fill Title (Arabic)="مستقبل الإمداد البحري"
  And they fill Description (English)="A panel on supply-chain resilience."
  And they select Hall="Auditorium A (AUD-A)"
  And they fill Start (UTC)="2026-11-10T09:00"
  And they fill End (UTC)="2026-11-10T10:30"
  And they leave Capacity override blank
  And they click "Create session"
  Then the BFF POSTs /account/api/admin/sessions and the API returns 200
  And the modal closes
  And the grid shows {N + 1} rows
  And a green toast reads "Session \"Future of Naval Logistics\" was created."
  And a row exists with Code="SES-001", Title="Future of Naval Logistics",
      Hall="Auditorium A", Start "2026-11-10 09:00", a green "Active" pill,
      and a "Scheduled" lifecycle pill

  When the administrator clicks the "Edit" icon on that row
  Then the GET /account/api/admin/sessions/{id} returns 200
  And the Edit modal opens titled "Edit session" with every field pre-filled
  And an additional "Active — show in the public agenda" checkbox is visible (ticked)
  When they change End (UTC) to "2026-11-10T11:00"
  And they click "Save changes"
  Then the PUT /account/api/admin/sessions/{id} returns 200
  And the modal closes
  And a green toast reads "Session \"Future of Naval Logistics\" was updated."
  And the row's End (UTC) column reads "2026-11-10 11:00"

  When the administrator clicks the "Details" icon on that row
  Then a read-only modal opens titled "Session details"
  And it renders a description list with Code, Title, Title (Arabic), Description,
      Hall, Start, End, Capacity override ("Inherits from hall"), Effective capacity,
      Speakers ("—"), Status ("Active"), Lifecycle ("Scheduled"), Recording ("No recording")
  When they click "Close"
  Then the modal closes

  When the administrator clicks the "Deactivate" icon on that row
  Then the View/Delete form opens (CrudShell, dialog by default) showing the row's read-only details
      and a red "Deactivate" button
  When they click "Deactivate"
  Then a SimfConfirm dialog asks to confirm, naming the session
  When they click the confirm "Deactivate" button
  Then the DELETE /account/api/admin/sessions/{id} returns 200
  And a green toast reads "Session \"Future of Naval Logistics\" was deactivated."
  And the row's Status pill changes from the green "Active" to the grey "Inactive"
```

**Evidence captured:**
- Before / after screenshots → `docs/screenshots/cp-admin-sessions-{canonical,add-modal,edit-modal,details-modal,deactivated}.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/sessions/*` call returns 200 (the `/list`, the POST,
  the GET, the PUT, the DELETE) and each picker `.../halls|speakers|themes|session-categories/list` returns 200
- Audit rows: `RowAudit` Insert + Update + soft-delete (Update `IsActive=false`) rows for the
  session, each stamped with the actor id

### E2E-SES-002 — Add form pickers (Hall, Speakers with role + reorder, Themes, Category)

```gherkin
Scenario: Populate the relational pickers on the Add form
  Given two active Speakers "Capt. Lin" / "النقيب لين" and "Dr. Omar" / "د. عمر" exist
  And one active Theme "Logistics" / "الإمداد" exists
  And one active Session category "Plenary" / "عام" exists
  And the Add modal is open with Code, Title (EN/AR), Hall, Start and End already filled
  When they select Category="Plenary"
  And they pick "Capt. Lin" in the "Add speaker" select
  Then a chip "1. Capt. Lin" appears with a role dropdown defaulting to "Speaker",
      plus "Up", "Down" and "Remove" buttons
  When they pick "Dr. Omar" in the "Add speaker" select
  Then a chip "2. Dr. Omar" appears
  When they change Dr. Omar's role dropdown to "Host"
  And they click "Up" on Dr. Omar's chip
  Then the chips renumber to "1. Dr. Omar" (Host) and "2. Capt. Lin" (Speaker)
  When they pick "Logistics" in the "Add theme" select
  Then a chip "Logistics" appears with a "Remove" button
  And the same speaker can no longer be re-picked (it leaves the Add-speaker option list)
  When they click "Create session"
  Then the POST body carries Speakers=[{Dr.Omar,Host,order 0},{Capt.Lin,Speaker,order 1}]
      and ThemeIds=[Logistics] and CategoryId=Plenary
  And the API returns 200 and a green "…was created." toast shows
  When they re-open the row's Details modal
  Then the Speakers list renders "Dr. Omar (د. عمر)" then "Capt. Lin (النقيب لين)" in order
```

### E2E-SES-003 — Broadcast lifecycle transitions

```gherkin
Scenario: Walk a session through its broadcast lifecycle and back
  Given an active session "SES-001" exists in lifecycle "Scheduled"
  And the administrator has the Sessions.Publish permission (superadmin wildcard)
  When they open its Details modal
  Then the modal footer shows a single lifecycle button "Mark held"

  When they click "Mark held"
  Then the PUT /account/api/admin/sessions/{id}/status returns 200 with Status=Held
  And a green toast reads "Session lifecycle set to Held."
  And the modal footer now shows "Back to scheduled" and "Mark recorded"

  When they click "Mark recorded"
  Then the API returns 200 with Status=Recorded
  And the footer now shows "Back to held" and "Publish"

  When they click "Publish"
  Then the API returns 200 with Status=Published
  And a green toast reads "Session lifecycle set to Published."
  And the Details list now shows a "Published at" timestamp
  And the grid's Lifecycle pill for the row turns the green "on" variant reading "Published"
  And the footer now shows the single reverse button "Un-publish"

  When they click "Un-publish"
  Then the API returns 200 with Status=Recorded
```

**Evidence captured:**
- Screenshots → `docs/screenshots/cp-admin-sessions-lifecycle-{scheduled,held,recorded,published}.png`
- Network: four sequential `PUT .../status` calls each return 200
- Audit row: `RowAudit` Update rows recording the status column change with the actor id

### E2E-SES-004 — Recording upload / remove

```gherkin
Scenario: Attach then remove a session recording
  Given an active session "SES-001" exists
  And the administrator has the Sessions.Publish permission
  When they open its Details modal
  Then the recording row reads "No recording"
  And below the description list a "Recording file" file-input and an "Upload recording" button are visible
  When they choose a small valid file "talk.mp4" (video/mp4) in the file-input
  And they click "Upload recording"
  Then the BFF POSTs /account/api/admin/sessions/{id}/recording as multipart and the API returns 200
  And a green toast reads "Recording uploaded."
  And the recording row now reads "talk.mp4 (… MB/KB)"
  And a "Remove recording" button appears

  When they click "Remove recording"
  Then the DELETE /account/api/admin/sessions/{id}/recording returns 200
  And a green toast reads "Recording removed."
  And the recording row reverts to "No recording"
```

### E2E-SES-005 — Capacity override semantics

```gherkin
Scenario: Blank capacity inherits the hall; a number overrides it
  Given the Add modal is open with Hall="Auditorium A" (hall seat count 200)
  When they leave Capacity override blank and create the session
  And they open its Details modal
  Then "Capacity override" reads "Inherits from hall"
  And "Effective capacity" reads "200"
  When they Edit the session and set Capacity override="120" and save
  And they re-open Details
  Then "Capacity override" reads "120"
  And "Effective capacity" reads "120"
```

### E2E-SES-006 — Grid filter / sort / paginate

```gherkin
Scenario: Filter, sort and page the sessions grid
  Given more than 20 sessions exist
  When the administrator types "Naval" into the grid filter (Title column is Filterable)
  Then the POST /account/api/admin/sessions/list body carries the title filter
  And only rows whose Title contains "Naval" render
  When they click the "Start (UTC)" column header
  Then the list re-queries sorted by startUtc ascending
  When they click "Next"
  Then the pager advances and the summary reads "Showing 21–40 of {total}"
```

### E2E-SES-007 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no active Session rows
  When the administrator opens /admin/sessions
  Then the grid body renders the SimfEmptyState component
  And it shows the bilingual copy "No sessions yet." / "لا توجد جلسات بعد."
  And the toolbar still shows the "Add session" button
  And no error toast appears
```

### E2E-SES-008 — Auth gate (page permission)

```gherkin
Scenario: User lacking Sessions.View is denied the page
  Given a signed-in admin whose role does NOT grant "Sessions.View"
  When they navigate to /admin/sessions
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/sessions/list request fires
  And the "Sessions" nav item is not rendered in the rail (RequiredPermission gate)
```

### E2E-SES-009 — Action-level auth gate (Sessions.Publish)

```gherkin
Scenario: Edit-capable admin without Sessions.Publish sees no lifecycle or recording controls
  Given a signed-in admin whose role grants Sessions.View + Sessions.Edit but NOT Sessions.Publish
  When they open a session's Details modal
  Then the description list and the "Close" button render
  But the lifecycle transition buttons in the footer are NOT rendered
  And the "Recording file" uploader + "Upload recording"/"Remove recording" buttons are NOT rendered
  When they attempt PUT /account/api/admin/sessions/{id}/status directly (out of band)
  Then the API returns 403 (the policy PolicyFor(Sessions.Publish) denies it)
```

### E2E-SES-010 — Code validation

```gherkin
Scenario: Blank or too-short Code shows a bilingual error in the modal
  Given the Add modal is open with Title, Hall, Start and End filled
  When they leave Code blank (or enter a single character "S")
  And they click "Create session"
  Then a SimfAlert error appears at the top of the modal
  And reads "Code must be between 2 and 16 characters." / "يجب أن يتراوح طول رمز الجلسة بين 2 و 16 حرفاً."
  And the modal stays open
  And no POST /account/api/admin/sessions request fires (client-side guard in SessionForm)
```

### E2E-SES-011 — Time-window validation

```gherkin
Scenario: End at or before Start shows a bilingual time-window error
  Given the Add modal is open with Code, Title, Hall all valid
  When they set Start (UTC)="2026-11-10T10:00"
  And they set End (UTC)="2026-11-10T09:00"
  And they click "Create session"
  Then a SimfAlert error appears reading "End time must be after start time." / "يجب أن تكون نهاية الجلسة بعد بدايتها."
  And the modal stays open
  And no POST request fires (client guard); were it to reach the API it would 400 SESSION_INVALID_TIME_WINDOW
```

### E2E-SES-012 — Duplicate Code conflict

```gherkin
Scenario: Duplicate Code returns 409 with the bilingual server message
  Given an active session with Code="SES-001" already exists
  When the administrator opens the Add modal
  And fills Code="SES-001" plus a valid Title, Title (Arabic), Hall, Start and End
  And clicks "Create session"
  Then the BFF forwards POST /admin/sessions
  And the API returns HTTP 409 with ApiResult.Error.Code = "SESSION_CODE_DUPLICATE"
  And the modal stays open
  And the SimfAlert surfaces the bilingual MessageForCurrentCulture():
      "A session with code 'SES-001' already exists." / "توجد جلسة بالرمز 'SES-001' بالفعل."
```

### E2E-SES-013 — Illegal lifecycle transition

```gherkin
Scenario: An illegal lifecycle move is rejected with a bilingual server message
  Given a session "SES-001" in lifecycle "Scheduled"
  When a PUT /account/api/admin/sessions/{id}/status with Status=Published is sent
      (e.g. a stale modal whose only legal next move was "Mark held")
  Then the API returns HTTP 400 with ApiResult.Error.Code = "SESSION_STATUS_TRANSITION_INVALID"
  And the error toast reads "A session cannot move from Scheduled to Published."
      / "لا يمكن نقل الجلسة من Scheduled إلى Published."
  And the modal stays open with the unchanged status
```

### E2E-SES-014 — Inactive / unknown Hall

```gherkin
Scenario: Selecting (or posting) an inactive hall is rejected
  Given the Add modal is open with a valid Code, Title, Start and End
  And the chosen HallId no longer exists or has been deactivated
  When they click "Create session"
  Then the API returns HTTP 400 with ApiResult.Error.Code = "SESSION_HALL_NOT_FOUND"
  And the SimfAlert reads "Hall '…' does not exist or is inactive." / "القاعة '…' غير موجودة أو غير مفعّلة."
  And the modal stays open
```

### E2E-SES-015 — Non-video recording rejected

```gherkin
Scenario: Uploading a non-video file is rejected
  Given the Details modal is open for a session and the administrator has Sessions.Publish
  When they choose "agenda.pdf" in the recording file-input
  And they click "Upload recording"
  Then the API returns HTTP 400 with ApiResult.Error.Code = "SESSION_RECORDING_INVALID"
  And the error toast reads "The recording must be a video file (mp4, m4v, webm, ogg, mov)."
      / "يجب أن يكون التسجيل ملف فيديو (mp4، m4v، webm، ogg، mov)."
  And the recording row still reads "No recording"
```

### E2E-SES-016 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the fallback bilingual toast
  Given the API is configured to return 500 on /admin/sessions/list (e.g. DB down)
  When the administrator opens /admin/sessions
  Then the grid shows the loading indicator
  And then a red toast appears reading "The sessions could not be loaded." / "تعذّر تحميل الجلسات."
  And no rows render
```

### E2E-SES-017 — RTL render

```gherkin
Scenario: Arabic toggle mirrors the grid, the Add modal and the lifecycle footer
  Given the administrator is on /admin/sessions in English
  When they click the "العربية" link in the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "الجلسات"
  And the grid headers, the "Active"/"Lifecycle" pills and the pager arrows mirror

  When they click "إضافة جلسة"
  Then the Add modal opens in RTL with Arabic labels (الرمز، العنوان، القاعة، …)
  And the speaker chip "Up/Down/Remove" buttons appear in reverse order

  When they open a session's Details modal
  Then the Hall label shows the Arabic hall name
  And the lifecycle footer buttons render in Arabic ("وضع علامة منعقدة"… per resx) and mirror
```

### E2E-SES-018 — Live broadcast URL validation (D-349)

```gherkin
Scenario: A YouTube or HLS live URL is accepted; anything else is rejected
  Given the Add modal is open with a valid Code, Title, Hall, Start and End
  And the "Live stream URL (live broadcast)" field shows the hint
      "Paste a YouTube link (youtube.com / youtu.be) or a direct HLS/MP4 stream URL."
  When they set Live stream URL="https://www.youtube.com/watch?v=dQw4w9WgXcQ"
  And they click "Create session"
  Then the API returns 200 and the session is created with that live URL

  When they Edit the session and set Live stream URL="https://vimeo.com/12345"
  And they click "Save changes"
  Then a SimfAlert error reads "Enter a valid YouTube link or an HLS/MP4 stream URL (https)."
      / "أدخل رابط يوتيوب صالحاً أو رابط بث HLS/MP4 (https)."
  And the modal stays open and no PUT fires (client guard)
  And were it to reach the API it would 400 SESSION_INVALID (shared LiveStreamUrlPolicy)
```

### E2E-SES-019 — Presentation toggle persists (D-353)

```gherkin
Scenario: Switch to full-page mode and it persists across reload
  Given the administrator is on /admin/sessions with the default "dialog" presentation
  And the grid toolbar shows the "Open as full page" toggle (maximize icon) from CrudPresentationToggle
  When they click the toggle
  Then the toggle label changes to "Open as dialog" (window icon)
  And localStorage key "simf.cp.prefs.sessions" holds {"v":1,"presentation":"page"}
  When they reload /admin/sessions
  Then OnInitializedAsync re-reads Prefs.GetPresentationAsync("sessions")
  And the toggle still reads "Open as dialog"
  And opening "Add session" now renders the full-page frame (not a popup)
```

### E2E-SES-020 — Full-page mode round-trip (D-353)

```gherkin
Scenario: Add/Edit/View take over the content area; Save returns to the grid
  Given the presentation is set to "full page" (CrudPresentation.Page)
  When the administrator clicks "Add session"
  Then the grid + SimfBanner are hidden (GridHidden) and replaced by the CrudShell page frame
      (title "Add session" + close header + the SessionsAddEdit form)
  And there is no modal backdrop
  When they fill Code, Title (EN/AR), Hall, Start and End and click "Create session"
  Then the POST /account/api/admin/sessions returns 200
  And the page frame closes and the grid re-appears with the new row and the success toast
  When they click the "Edit" icon and then the frame's "Close" (X) header button
  Then the GET /account/api/admin/sessions/{id} loads the full detail into SessionsAddEdit
  And clicking Close re-shows the grid unchanged (no PUT fires)
  When they click the "Details" icon
  Then SessionsViewDelete renders read-only in the same full-page frame (IsDelete=false, no Deactivate button)
```

### E2E-SES-021 — Delete confirmation gate (D-353)

```gherkin
Scenario: Deactivate requires explicit confirmation through SimfConfirm
  Given the administrator is on /admin/sessions
  When they click the "Deactivate" icon on a row
  Then the GET /account/api/admin/sessions/{id} loads the detail
  And SessionsViewDelete opens (CrudShell) showing the read-only details and a red "Deactivate" button
      (the broadcast-lifecycle + recording blocks render too, gated by Sessions.Publish)
  When they click "Deactivate"
  Then a SimfConfirm dialog appears with the title "Deactivate session" and the message
      naming the session: "Deactivate the session \"Future of Naval Logistics\"? It will be hidden from the public agenda."
  When they click "Cancel"
  Then no DELETE request fires and the row is unchanged
  When they re-open and click "Deactivate" then the confirm "Deactivate" button
  Then exactly one DELETE /account/api/admin/sessions/{id} fires (soft-delete, Sessions.Delete)
  And the success toast reads "Session \"Future of Naval Logistics\" was deactivated."
  And the grid reloads with the row's pill turned grey "Inactive"
```

### E2E-SES-022 — Excel export (D-356)

```gherkin
Scenario: Export the sessions grid to an XLSX workbook
  Given the administrator is on /admin/sessions with at least two sessions
  When they click the toolbar "Export" action with no rows selected
  Then a POST /account/api/admin/sessions/export fires carrying AdminGridExportRequest
      with an empty Ids list and the current Query (whole filtered grid, capped at 5000 rows)
  And the browser saves a file named simf-sessions-{timestamp}.xlsx
  And the workbook's "Sessions" sheet header row reads
      Code | Title | TitleArabic | Hall | Category | StartUtc | EndUtc | Capacity | Status | IsActive
  And the Hall cell holds the hall *code*, the Category cell the category English name,
      and StartUtc/EndUtc are ISO-8601 UTC strings (e.g. 2026-11-10T09:00:00Z)
  And the speaker roster and theme set are NOT exported (M-to-M, omitted by design)
  When they instead select two rows then click "Export"
  Then the POST body carries those two Ids and the workbook contains exactly those two rows
```

### E2E-SES-023 — Excel import (D-356)

```gherkin
Scenario: Import sessions from a workbook and see the per-row outcome
  Given the administrator is on /admin/sessions
  And one active Hall with code "AUD-A" exists
  When they click the toolbar "Import" action (the file picker "sessions-import-input", accept=".xlsx", opens)
  And they choose an .xlsx whose "Sessions" sheet has the required headers
      Code | Title | TitleArabic | Hall | StartUtc | EndUtc
      with two new rows (Hall="AUD-A", valid ISO StartUtc < EndUtc)
  Then a POST /account/api/admin/sessions/import fires as multipart form data
  And the import-result modal shows "2 created, 0 updated, 0 skipped." (import is insert-only)
  And the grid reloads and a green toast reads the shared Grid.Import.Done text
  When they import a workbook where one row has a Hall code that no active hall matches
  Then that row appears in the per-row error list reading "No active hall with code '…' was found."
      and the others still import (one bad row never aborts the batch)
  When they import a row whose EndUtc is at/before StartUtc
  Then that row errors with "The end time must be after the start time."
```

### E2E-SES-024 — Excel import rejection (D-356)

```gherkin
Scenario: A bad upload is rejected without creating anything
  Given the administrator is on /admin/sessions
  When they import a file that is not a valid .xlsx (fails the ZIP-magic check)
  Then the API returns HTTP 400 and the page shows a bilingual error toast
      "The file is not a valid Excel workbook." / "الملف ليس مصنف Excel صالحًا."
  And no session is created
  When they import a file larger than 5 MB
  Then the API returns HTTP 413 "The Excel file is too large. The maximum is 5 MB."
  When they import a workbook whose sheet is not named "Sessions"
      (or is missing a required header from Code/Title/TitleArabic/Hall/StartUtc/EndUtc)
  Then the parse rejects it with a bilingual error and nothing is created
```

---

## Implementation notes

- **Lower-layer API coverage already exists.** These xUnit + WebApplicationFactory
  suites cover the same surface without a browser, and should be kept in sync /
  retired as E2E coverage lands:
  - `tests/SIMF.Api.Tests/AdminSessionsTests.cs` — CRUD, duplicate-code (409),
    time-window (400), hall-not-found (400), speaker/theme link validation,
    live-URL validation (D-349 — YouTube/HLS accepted, other → 400 SESSION_INVALID).
  - `tests/SIMF.Api.Tests/SessionLifecycleTests.cs` — the `Scheduled → Held →
    Recorded → Published` transition matrix + the `SESSION_STATUS_TRANSITION_INVALID`
    rejections (P3.2a / D-231).
  - `tests/SIMF.Api.Tests/SessionRecordingTests.cs` — recording upload/remove,
    the video allow-list and the `SESSION_RECORDING_INVALID` rejection (D-232).
  - Permission enforcement is asserted by `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs`
    and `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs` (the nav
    item's `RequiredPermission = Sessions.View`).
  - `tests/SIMF.Api.Tests/SessionsExcelTests.cs` — the D-356 Excel export/import
    surface (the `Sessions` sheet column layout, hall-code / category-name
    resolution, the insert-only import, the per-row error aggregation, the
    `Sessions.Export` / `Sessions.Import` permission gates and the
    ZIP-magic / 5 MB / 5000-row upload defence).
- **Manual smoke is canonical today.** Until Playwright is adopted, run these
  scenarios as a Chrome DevTools MCP session: sign in per the Auth setup, walk
  each scenario, and capture screenshots into `docs/screenshots/cp-admin-sessions-*.png`.
- **Convert to Playwright** when the runner is adopted: each Gherkin block maps to
  a `.feature` scenario under `tests/SIMF.E2E.Tests/` (project to be created) plus
  a step-definition class. The Gherkin is already runner-agnostic.
- **Two permissions, two gates.** Note the split: CRUD = `Sessions.Create/Edit/Delete`,
  broadcast lifecycle + recording = `Sessions.Publish`. E2E-SES-009 is the load-bearing
  proof that the `Sessions.Publish` UI controls are hidden AND the endpoint denies a
  caller without that code.

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle).
