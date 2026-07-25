# Sessions — `/admin/sessions`

| | |
|--|--|
| **Route** | `/admin/sessions` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel |
| **Audience** | Administrator / Scientific Committee |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Sessions.View)]` (`"Sessions.View"`) + `RequireApprovedAccount` + JWT bearer; mutations also `RequireRateLimiting("auth")` |
| **Pattern** | D-353 CrudShell (dialog/full-page toggle) + reusable Add/Edit + View/Delete forms; D-356 Uniform-CRUD Excel export + import via `CrudGridExcel` |
| **Status** | ✅ Real (D-165; D-231 lifecycle; D-232 recording; D-225 speaker roles; D-226 category; D-349 live URLs; D-353 framing; D-356 Excel) — 2026-06-10 |
| **Implements use case(s)** | UC-SES-CREATE-001, UC-SES-EDIT-001, UC-SES-DEACTIVATE-001, UC-SES-LIFECYCLE-001 (per SIMF-FDS-004 §5.3 + PDF §2.9) |
| **Backend endpoints** | `POST /account/api/admin/sessions/list`, `GET /account/api/admin/sessions/{id}`, `POST /account/api/admin/sessions`, `PUT /account/api/admin/sessions/{id}`, `DELETE /account/api/admin/sessions/{id}`, `PUT /account/api/admin/sessions/{id}/status`, `POST`/`DELETE /account/api/admin/sessions/{id}/recording`, `POST /account/api/admin/sessions/export`, `POST /account/api/admin/sessions/import` (BFF → API `/api/v1/admin/sessions/*`) |
| **Source file** | [`SessionsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionsList.razor), [`SessionsAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionsAddEdit.razor), [`SessionsViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionsViewDelete.razor) |
| **Tests** | [`docs/tests/e2e/cp-admin-sessions.md`](../../tests/e2e/cp-admin-sessions.md); `tests/SIMF.Api.Tests/AdminSessionsTests.cs`, `SessionLifecycleTests.cs`, `SessionRecordingTests.cs`, `SessionsExcelTests.cs` |
| **Last reviewed** | 2026-06-10 |

---

## 1. Purpose

This page is the admin catalogue for the event's programme **sessions** — the
agenda line-up that drives both the public Website agenda and the Flutter app.
An administrator walks in to create, edit, view and deactivate sessions, each
carrying a stable **Code**, bilingual title/description, a **Hall**, an optional
**Category**, a start/end window, a capacity override, a reorderable
**speaker/host** roster, a **theme** set and the live-broadcast feed URLs. The
Scientific Committee additionally drives a session through its broadcast
**lifecycle** (Scheduled → Held → Recorded → Published) and attaches/removes the
session **recording** — both gated by a distinct `Sessions.Publish` permission.
As of D-356 the grid also bulk-exports to and imports from Excel.

## 2. Audience + permissions

- **Who can reach it:** any signed-in admin whose role grants `Sessions.View`
  (superadmin wildcard `Administrator = "*"` satisfies all codes).
- **Who can edit/write on it:** CRUD is split across distinct codes —
  `Sessions.Create`, `Sessions.Edit`, `Sessions.Delete`. The broadcast lifecycle
  transitions **and** the recording upload/remove block sit behind
  `Sessions.Publish` (the Scientific Committee role). Excel export is gated by
  `Sessions.Export`, import by `Sessions.Import`.
- **Authorisation gates:**
  - Page: `@attribute [RequirePermission(PermissionCatalog.Sessions.View)]`.
  - Every API endpoint: `Policies(PermissionCatalog.PolicyFor(<code>), nameof(AuthorizationPolicies.RequireApprovedAccount))`.
  - Action buttons in the View/Delete form are wrapped in
    `<AuthorizedAction Permission="@PermissionCatalog.Sessions.Publish">` so an
    admin with View/Edit but not Publish sees the read-only details **without**
    the lifecycle footer or the recording uploader.
  - The grid **Moderate** row action (gavel icon → the per-session live Q&A desk
    at `/sessions/{id}/moderate`) is wrapped in
    `<AuthorizedAction Permission="@PermissionCatalog.Questions.Moderate">`, so it
    appears only for a moderator / Administrator (D-646). The desk page and its API
    enforce the same `Questions.Moderate` code, so hiding the action is UX-only.
- **What an unauthenticated / under-privileged user sees:** an admin lacking
  `Sessions.View` lands on `/not-permitted` (HTTP 200) and the "Sessions" nav
  item is not rendered (its `RequiredPermission = Sessions.View`).

## 3. Screenshots

| State | File | Captured |
|-------|------|----------|
| Default (with rows) | `docs/screenshots/cp-admin-sessions-default.png` | _pending_ |
| Empty state | `docs/screenshots/cp-admin-sessions-empty.png` | _pending_ |
| Add (dialog) | `docs/screenshots/cp-admin-sessions-add-modal.png` | _pending_ |
| Add (full page, D-353) | `docs/screenshots/cp-admin-sessions-add-fullpage.png` | _pending_ |
| Details / View | `docs/screenshots/cp-admin-sessions-details-modal.png` | _pending_ |
| Delete confirm (SimfConfirm) | `docs/screenshots/cp-admin-sessions-delete-confirm.png` | _pending_ |
| Import result modal | `docs/screenshots/cp-admin-sessions-import-result.png` | _pending_ |
| RTL (Arabic) | `docs/screenshots/cp-admin-sessions-rtl.png` | _pending_ |

## 4. UI affordances

### 4.1 Banner / page header

`SimfBanner` with the title `Admin.Sessions.Title`. The banner + grid are hidden
(`GridHidden`) when a form is open in **full-page** presentation; in dialog
presentation the grid stays behind the modal.

### 4.2 Toolbar

The grid is the shared `SimfDataGrid` (`Multiselect="true"`, per-row checkbox +
select-all, quiet per-row icon actions). The `CustomToolbar` slot renders the
**D-353 presentation toggle**:

| Button | Wired callback | Calls | Notes |
|--------|----------------|-------|-------|
| Select all / Select row | grid built-in | — | `Multiselect="true"` |
| Add | `OnAddAsync` | opens `SessionsAddEdit` (Create) | |
| Edit | `OnEditAsync` | `GET /…/{id}` then `SessionsAddEdit` (Edit) | loads full detail first |
| Details | `OnDetailsAsync` | `GET /…/{id}` then `SessionsViewDelete` (IsDelete=false) | |
| Deactivate | `OnDeleteAsync` | `GET /…/{id}` then `SessionsViewDelete` (IsDelete=true) | SimfConfirm gates the DELETE |
| Export | `OnExportAsync` | `POST /…/export` via `_excel.ExportAsync(ids, _query)` | D-356; `Sessions.Export` |
| Import | `OnImportAsync` | `_excel.TriggerImportAsync()` → `POST /…/import` | D-356; `Sessions.Import` |
| Presentation toggle | `CrudPresentationToggle` `@bind-Value="_presentation"` | localStorage `simf.cp.prefs.sessions` | D-353; Page ↔ Popup |

`<CrudGridExcel @ref="_excel" Resource="sessions" OnImported="OnImportedAsync" OnError="OnExcelError" />`
is rendered below the grid; on a successful import it raises a success toast
(`Grid.Import.Done`) and reloads the grid.

### 4.3 Grid columns

| Column | Source field | Sortable | Filterable | Notes |
|--------|--------------|----------|------------|-------|
| Code | `r.Code` | yes | no | |
| Title | `r.Title` | yes | yes | filter column |
| Title (Arabic) | `r.TitleArabic` | no | no | |
| Hall | `HallLabel(r)` | no | no | culture-aware (`HallName`/`HallNameArabic`) |
| Start (Saudi time) | `r.Start` | yes | no | `yyyy-MM-dd HH:mm` |
| End (Saudi time) | `r.End` | yes | no | `yyyy-MM-dd HH:mm` |
| Capacity | `r.Capacity` | no | no | effective capacity |
| Active | `r.IsActive` | no | no | `SimfPill` on/off |
| Status | `r.Status` | no | no | lifecycle pill (Published=on, Scheduled=neutral, else admin) |

### 4.4 Pager

First / Prev / numbered / Next / Last; default `Top = 20`; summary
`Admin.Sessions.Summary` ("Showing X–Y of Z").

### 4.5 Form fields (`SessionsAddEdit`)

| Field | Type | Required | MaxLength | Validation (client guard in `HandleSubmitAsync`) |
|-------|------|----------|-----------|--------------------------------------------------|
| Code | text | yes | 16 | 2–16 chars; trimmed + `ToUpperInvariant` before POST |
| Title (English) | text | yes | 256 | 1–256 chars |
| Title (Arabic) | text | yes | 256 | 1–256 chars |
| Description (English) | textarea | no | 2048 | optional; `null` if blank |
| Description (Arabic) | textarea | no | 2048 | optional; `null` if blank |
| Live stream URL | text | no | 1024 | `LiveStreamUrlPolicy.IsAllowed` (YouTube / HLS / MP4 https) |
| Live sign-language URL | text | no | 1024 | same policy |
| Hall | select | yes | — | must parse to a Guid; loaded from `…/halls/list` (Top=500, active) |
| Category | select | no | — | optional; loaded from `…/session-categories/list` |
| Type | select | yes* | — | Workshop / Session / Event — **required** on create (#3); *grandfathered: a legacy untyped row may stay untyped on edit, but a set type can't be cleared |
| Start (Saudi time) | datetime-local | yes | — | parses; treated as Saudi local time |
| End (Saudi time) | datetime-local | yes | — | parses; must be `> Start` |
| Capacity override | number | no | — | blank = inherit hall; else integer ≥ 0 |
| Seat selection (override) | select | no | — | blank = inherit the hall; else Assigned seat / Open seating (general admission) — D-485 |
| Add speaker | select | yes* | — | reorderable roster with per-speaker role (Speaker/Host); **≥1 required unless Type = Event** (#4); *grandfathered on edit |
| Add theme | select | no | — | multi-pick theme chips |
| Active | checkbox | (Edit only) | — | shows in the public agenda |

The View/Delete form (`SessionsViewDelete`) renders a read-only `<dl>` of every
field plus Effective capacity, Speakers, Published-at (when present) and the
recording row; in delete mode it adds a red **Deactivate** button gated by a
`SimfConfirm` dialog. The `Sessions.Publish`-gated blocks add the recording
file-input (`session-recording-input`, `accept="video/*"`) + Upload/Remove
buttons and the lifecycle transition buttons.

## 5. Data flow

```
User action → SessionsList/AddEdit/ViewDelete handler
            → JS interop (simfAccount.postJson/getJson/putJson/deleteJson/uploadFile)
            → CP BFF /account/api/admin/sessions/...
            → API /api/v1/admin/sessions/... → IAdminSessionService → SimfAppDbContext
            → ApiResult<T> envelope → UI update + bilingual toast
```

| When | Method + path (BFF → API) | Request body | Response shape |
|------|---------------------------|--------------|----------------|
| OnInit / query change | `POST /account/api/admin/sessions/list` | `GridQuery` | `ApiResult<GridPage<AdminSessionSummary>>` |
| Edit / Details / Delete open | `GET /account/api/admin/sessions/{id}` | — | `ApiResult<AdminSessionDetail>` |
| Create | `POST /account/api/admin/sessions` | `AdminCreateSessionRequest` | `ApiResult<AdminSessionDetail>` |
| Update | `PUT /account/api/admin/sessions/{id}` | `AdminUpdateSessionRequest` | `ApiResult<AdminSessionDetail>` |
| Deactivate (after SimfConfirm) | `DELETE /account/api/admin/sessions/{id}` | — | `ApiResult<bool>` (soft-delete) |
| Lifecycle transition | `PUT /account/api/admin/sessions/{id}/status` | `SetSessionStatusRequest` | `ApiResult<AdminSessionDetail>` |
| Recording upload / remove | `POST` / `DELETE /account/api/admin/sessions/{id}/recording` | multipart / — | `ApiResult<AdminSessionDetail>` |
| Export (D-356) | `POST /account/api/admin/sessions/export` | `AdminGridExportRequest { Ids, Query }` | `.xlsx` download |
| Import (D-356) | `POST /account/api/admin/sessions/import` | multipart `file` | `ApiResult<AdminGridImportResult>` |

The Add/Edit form also lazy-loads its pickers on first render:
`POST …/halls/list`, `…/speakers/list`, `…/themes/list`,
`…/session-categories/list` (all `Top=500`, `isActive=true`).

## 6. Validation + error handling

- **Client-side guards** (`SessionsAddEdit.HandleSubmitAsync`): Code 2–16, Title
  1–256, Title (Arabic) 1–256, a parseable Hall Guid, parseable Start/End with
  `End > Start`, non-negative integer capacity, and each non-blank live URL
  passing `LiveStreamUrlPolicy.IsAllowed`. It also enforces the **required Type**
  (#3) and the **≥1-speaker-unless-Event** rule (#4), each mirroring the API with
  the same no-regression grandfather via `Initial` (a legacy violating row stays
  saveable; a compliant one cannot be regressed). A failed guard sets `_error` (a
  `SimfAlert`) and **no** request fires.
- **Server-side:** `IAdminSessionService` (CreateAsync/UpdateAsync/SetStatusAsync).
  Relevant `ErrorCodes`:
  - `SESSION_NOT_FOUND` (404) — unknown id.
  - `SESSION_CODE_DUPLICATE` (409) — duplicate code.
  - `SESSION_INVALID_TIME_WINDOW` (400) — End ≤ Start.
  - `SESSION_HALL_NOT_FOUND` (400) — inactive/unknown hall.
  - `SESSION_SPEAKER_NOT_FOUND` / `SESSION_THEME_NOT_FOUND` (400) — bad M-to-M link.
  - `SESSION_STATUS_TRANSITION_INVALID` (400) — illegal lifecycle move.
  - `SESSION_RECORDING_INVALID` (400) — non-video upload.
  - `SESSION_TYPE_REQUIRED` (400, #3) — no type on create; or clearing a set type on edit.
  - `SESSION_SPEAKER_REQUIRED` (400, #4) — a non-Event session with no speaker on create; or dropping the last speaker of a compliant non-Event on edit.
  - `SESSION_INVALID` (400) — live URL fails the shared `LiveStreamUrlPolicy`.
- **Excel import** (`ImportSessionsEndpoint` over `AdminGridImportEndpoint`):
  insert-only, dedup/row key = **Code**. Required headers: Code, Title,
  TitleArabic, Hall, Start, End. An optional **Speakers** column holds
  comma-separated speaker **codes** (resolved case-insensitive, active-only;
  position sets the display order, role defaults to Speaker) so an imported
  non-Event row can meet the #4 min-1-speaker rule. Because the create rules run
  per row, a **blank Type** (#3), a non-Event row with **no speakers** or an
  **unknown/duplicate speaker code** (#4), plus the existing bad
  code/title/time-window/capacity, unresolved Hall code or unknown Category all
  raise a per-row `DataValidationException` collected into the result's error
  list — one bad row never aborts the batch. Hall resolves from its **code**
  (case-insensitive, active-only); Category from its English **name** (blank =
  unset); the **export still omits the roster** (insert-only import ⇒ no
  round-trip). Upload defence: ZIP-magic gate → 400 "The file is not a valid Excel
  workbook."; >5 MB → 413; cap 5000 rows; a missing/mis-named "Sessions" sheet or
  missing required header is rejected at parse.
- **Toast strategy:** success toasts `Admin.Sessions.Created` /
  `Admin.Sessions.Updated` / `Admin.Sessions.Deactivated` (each `string.Format`
  with the title); import success `Grid.Import.Done`; load failure
  `Admin.Sessions.LoadFailed`; form fallback `Admin.Sessions.Fallback`. Server
  errors surface `Error.MessageForCurrentCulture()` (bilingual).

## 7. Edge cases + known limitations

- **Edit/View/Delete fetch the full detail first** (`LoadDetailAsync`) — the grid
  summary omits speakers/themes/recording/live URLs, so editing from a
  summary-only form would wipe them. A failed fetch surfaces a toast and aborts.
- **Capacity override blank = inherit the hall** — the View form shows
  "Inherits from hall" (`Admin.Sessions.Field.CapacityInherits`) and the
  Effective capacity row reflects the resolved value.
- **Lifecycle is a state machine** — `NextTransitions` only offers legal moves
  (e.g. Scheduled → Held only); a stale modal posting an illegal move gets a
  400 `SESSION_STATUS_TRANSITION_INVALID`.
- **Excel omits the speaker roster + theme set** — both are M-to-M collections
  that cannot be expressed safely as a single text cell; export leaves them out
  and import never sets them. An admin manages them afterwards via Edit.
- **Import is insert-only** — a duplicate Code is a per-row error, not an update.
- **Delete is a soft-delete** — `IsActive=false`; the row stays visible with the
  grey "Inactive" pill.

## 8. i18n + RTL

- All strings come from `Strings.resx` (EN) + `Strings.ar.resx` (AR) via
  `IStringLocalizer<Strings> L` under the `Admin.Sessions.*` key family; status
  labels derive `Admin.Sessions.Status.{enumName}` 1:1 with `SessionStatus`.
- Hall / speaker / theme / category labels are culture-aware
  (`CultureInfo.CurrentUICulture` `ar` → the Arabic name).
- RTL: `<html dir="rtl" lang="ar">` mirrors the nav rail, grid headers, pills,
  pager arrows and the speaker chip Up/Down/Remove buttons; the lifecycle footer
  renders in Arabic.

## 9. Accessibility

- Keyboard: the grid actions are buttons; the form is an `EditForm`; SimfConfirm
  and CrudShell trap focus while open and restore it on close.
- Screen reader: `SimfDataGrid` exposes `Caption` (`Admin.Sessions.Title`) +
  per-row `RowLabel` (the session title).
- Colour contrast / focus: WCAG AA via `theme.tokens.css`; `--focus-ring` on
  every focusable element.

## 10. Related use cases (UCS-001)

| UC ID | Title | Notes |
|-------|-------|-------|
| UC-SES-CREATE-001 | Create a session | Code/Title/Hall/window + pickers |
| UC-SES-EDIT-001 | Edit a session | full detail incl. roster/themes |
| UC-SES-DEACTIVATE-001 | Deactivate a session | soft-delete via SimfConfirm |
| UC-SES-LIFECYCLE-001 | Drive broadcast lifecycle | `Sessions.Publish` only |

## 11. Related E2E test scenarios

| Scenario | File | Coverage |
|----------|------|----------|
| Golden CRUD round-trip | [`cp-admin-sessions.md`](../../tests/e2e/cp-admin-sessions.md) | E2E-SES-001 |
| Pickers / lifecycle / recording | same | E2E-SES-002..004, 009 |
| Validation + conflicts | same | E2E-SES-010..015, 018 |
| Presentation toggle persists (D-353) | same | E2E-SES-019 |
| Full-page mode round-trip (D-353) | same | E2E-SES-020 |
| Delete confirmation gate (D-353) | same | E2E-SES-021 |
| Excel export (D-356) | same | E2E-SES-022 |
| Excel import + rejection (D-356) | same | E2E-SES-023, 024 |
| Moderate row action → live Q&A desk (D-646) | same | E2E-SES-031 |

## 12. Related docs

- Per-page CP documentation set (4-aspect + README, D-380):
  [`../../CP/admin-sessions/README.md`](../../CP/admin-sessions/README.md)
  (Function / Logic / API / Design).
- Pattern doc: [`SIMF_TABLE_PATTERN.md`](../../dev/SIMF_TABLE_PATTERN.md).
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) — admin Sessions group + `ApiResult<T>`.
- Architecture: [`SIMF-SAD-001`](../../SIMF-SAD-001-Software-Architecture-Document.md).
- Authority spec: SIMF-FDS-004 §5.3 (+ PDF §2.9).
- Decisions: D-165 (CRUD), D-225 (speaker roles), D-226 (category), D-231
  (lifecycle), D-232 (recording), D-349 (live URLs), D-353 (CrudShell framing),
  D-356 (Uniform-CRUD Excel).
- Source: [`SessionsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionsList.razor),
  [`SessionsAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionsAddEdit.razor),
  [`SessionsViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionsViewDelete.razor),
  [`SessionEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SessionEndpoints.cs),
  [`SessionsExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SessionsExcelEndpoints.cs).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-07-01 | D-578 | `SessionsAddEdit` "get subtitle" tools below the caption fields: import an `.srt`/`.vtt`/`.txt` file (parsed server-side to text) or **Fetch subtitle from video** (`POST /admin/sessions/subtitle/fetch-from-video`, gated `Sessions.Edit`), both filling `LiveCaptions`/`LiveCaptionsArabic` which feed the AI session-summary. Fetch degrades to `SUBTITLE_FETCH_FAILED` where the server can't reach YouTube (on-prem NCA network) → paste/upload instead. E2E SES-027..030. |
| 2026-06-10 | D-356 | Uniform-CRUD Excel export (`Sessions.Export`) + import (`Sessions.Import`) via `CrudGridExcel`; reference doc created. |
| 2026-06-09 | D-353 | CrudShell dialog/full-page toggle; inline SimfModal forms replaced by reusable `SessionsAddEdit` + `SessionsViewDelete`; delete now gated by SimfConfirm. |
| earlier | D-165/231/232/225/226/349 | Sessions CRUD, broadcast lifecycle, recording, speaker roles, category, live-stream URLs. |

---

_Last reviewed:_ 2026-07-01 by Claude (D-578 — subtitle import/fetch tools).
