# Speaker meeting requests — `/admin/speaker-meeting-requests`

| | |
|--|--|
| **Route** | `/admin/speaker-meeting-requests` |
| **Audience** | Administrator |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.SpeakerMeetingRequests.View)]` (page); API endpoints gated `SpeakerMeetingRequests.View` (list + GetById) / `.Manage` (respond) / `.Export` (export), all `+ RequireApprovedAccount` |
| **Pattern** | D-269 review/approval queue (NOT full CRUD) — SimfDataGrid (server-paged, sortable + filterable) + a per-row Respond modal. Mirrors `MeetingRequestsList.razor`. |
| **Status** | ✅ Real (D-269) |
| **Backend endpoints** | BFF passthroughs `/account/api/admin/speaker-meeting-requests/*` → API: `POST /admin/speaker-meeting-requests/list`, `GET /admin/speaker-meeting-requests/{id}`, `PUT /admin/speaker-meeting-requests/{id}/respond`, `POST /admin/speaker-meeting-requests/export` (D-356). Public submit (app, not this page): `POST /api/v1/app/speakers/{speakerId}/meeting-requests`. |
| **Source** | [`SpeakerMeetingRequestsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SpeakerMeetingRequestsList.razor), [`SpeakerMeetingRequestEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Programme/SpeakerMeetingRequestEndpoints.cs), [`SpeakerMeetingRequestsExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SpeakerMeetingRequestsExcelEndpoints.cs), [`SpeakerMeetingRequestService.cs`](../../../src/Backend/SIMF.Infrastructure/MeetingRequests/SpeakerMeetingRequestService.cs), [`SpeakerMeetingRequest.cs`](../../../src/Backend/SIMF.Domain/BusinessMeetings/SpeakerMeetingRequest.cs) |
| **Backed by** | `dbo.SpeakerMeetingRequests` table (migration `D269_AddSpeakerMeetingRequests`, 2026-06-03). Distinct from the session-scoped `MeetingRequest`. |
| **Tests** | [`docs/tests/e2e/cp-admin-speaker-meeting-requests.md`](../../tests/e2e/cp-admin-speaker-meeting-requests.md) |
| **Last reviewed** | 2026-06-11 |

## 1. Purpose

The admin review queue for **meeting requests submitted to a speaker** by the
audience (D-269, Mockup page 20 "Speaker profile"). A signed-in, approved
visitor on the Speaker profile may ask a speaker for a one-to-one meeting **only
when** that speaker's `AllowsMeetingRequests` flag is `true`. Those requests land
here as `Pending` rows; the administrator reviews each one and **responds**
(Accept or Reject) with an optional note.

This is a **response-only queue, not a CRUD page** — there is no Add / Edit /
Delete from the Control Panel. Requests are *created* from the app (the public
submit endpoint) and *responded to* from the CP modal. The golden path is
therefore **list → Respond (Accept) → row flips to Accepted**, not the classic
"Add → Edit → Delete" round-trip.

It is distinct from the session-scoped `MeetingRequest` queue (Mockup screen 27
"Request interview", [`cp-admin-meeting-requests.md` E2E](../../tests/e2e/cp-admin-meeting-requests.md)).
`SpeakerMeetingRequest` is a separate dedicated entity / table; the two queues
never share rows.

## 4. UI

- Banner (`SimfBanner`) + a single owner-mandated **SimfDataGrid** (server-paged,
  per-column filter + sort, full pager, multiselect, Export action). No Add
  toolbar button — this page does not create rows.
- Grid columns (see §4.5): Speaker, Requester, Subject, Status (pill), Submitted
  (CreatedAt, UTC), Responded (RespondedAt, UTC or "—").
- Status pill (`SimfPill`): Pending = amber (`warn`), Accepted = green (`on`),
  Rejected = grey (`off`).
- Per-row **Respond** action — a quiet reply (↩) icon (`SimfToolbarButton
  Icon="reply"`) shown **only on `Pending` rows**, wrapped in
  `<AuthorizedAction Permission="SpeakerMeetingRequests.Manage">`. Resolved
  (Accepted / Rejected) rows show no action icon.
- Per-row **Resend speaker confirmation** action (R-1) — a quiet send (➤) icon
  (`SimfToolbarButton Icon="send"`) shown **only on `AwaitingSpeaker` rows**,
  wrapped in `<AuthorizedAction Permission="SpeakerMeetingRequests.Manage">` (the
  same gate as Respond). It POSTs
  `/account/api/admin/speaker-meeting-requests/{id}/resend-confirmation` (no modal):
  the prior 72h Approve/Reject token pair is invalidated and a fresh pair is minted
  + re-emailed to the speaker; the row stays `AwaitingSpeaker`. Success toast
  "The speaker confirmation email was re-sent."; a non-`AwaitingSpeaker` row is a
  409 (`SPEAKER_MEETING_REQUEST_STATUS_INVALID`). A stuck `AwaitingSpeaker` row
  whose links all expire is also auto-reverted to `Pending` by the
  `MeetingAwaitingSpeakerExpiryWorker` (R-1a), returning it to the Respond queue.
- **Respond modal** (`SimfModal`): a description list (Speaker, Requester, Subject)
  + a **Decision** select (Accept / Reject) + an optional **Response note**
  textarea + Send / Cancel footer buttons. The requester email is loaded into the
  modal on open (see §5 / §7).
- **Excel export only (D-356):** the grid toolbar carries an **Export** action
  (there is **no Import** affordance — see §7). Export posts
  `AdminGridExportRequest { Ids, Query }` to
  `/account/api/admin/speaker-meeting-requests/export` via
  `simfAccount.downloadXlsx`. With no rows selected it sends the current `Query`
  (the whole filtered grid); with rows selected it sends their `Ids` and a null
  `Query` (just those rows). The download is named
  `simf-speaker-meeting-requests-{timestamp}.xlsx`, sheet **"SpeakerMeetingRequests"**,
  header row `Speaker | Requester | Subject | Status | CreatedAt | RespondedAt`.
  The export is capped at **5000 rows** (`AdminGridExportEndpoint.MaxExportRows`)
  and honours the active filters/sort. The requester email is **not** exported
  (PII is detail-only — the D-185 pattern). There is no import / no toggle.
- Empty state: `SimfEmptyState` ("No speaker meeting requests yet.") via the
  grid's `EmptyTemplate`.
- Sortable columns: Requester, Subject, Status, Submitted, Responded. Filterable
  columns: Requester (Contains), Subject (Contains), Status (enum-parse).

### 4.5 Queue columns + actions

| Column | Sortable | Filterable | Notes |
|--------|----------|-----------|-------|
| Speaker | no | no | Speaker display name (`SpeakerName`) |
| Requester | yes | yes (Contains) | Requester display name; the email is **not** in the list row (PII detail-only) |
| Subject | yes | yes (Contains) | Free-text subject of the request |
| Status | yes | yes (enum) | Pill: Pending (amber) / Accepted (green) / Rejected (grey) |
| Submitted | yes | no | `CreatedAt`, rendered `yyyy-MM-dd HH:mm 'UTC'` |
| Responded | yes | no | `RespondedAt`, same UTC format, or "—" when unresolved |
| Actions | — | — | Quiet **Respond** (↩) icon on `Pending` rows only (gated `SpeakerMeetingRequests.Manage`) |

**Actions on this page (no create / edit / delete):**

- **Respond → Accept** — `PUT /admin/speaker-meeting-requests/{id}/respond` with
  `Status = Accepted` + optional note. Row flips to Accepted; success toast
  "Response sent." / "تم إرسال الردّ."
- **Respond → Reject** — same endpoint with `Status = Rejected` + optional note.
  Row flips to Rejected.
- **Export** — `POST /admin/speaker-meeting-requests/export` (D-356, export only).

## 5. Data flow + endpoints

1. **List** — the page posts `GridQuery` to
   `/account/api/admin/speaker-meeting-requests/list` (BFF) →
   `POST /admin/speaker-meeting-requests/list` (API,
   `ListAdminSpeakerMeetingRequestsEndpoint`) → `service.ListAllAsync(actorId,
   query)` → `ApiResult<GridPage<AdminSpeakerMeetingRequestRow>>`. Rows carry
   speaker name + requester name + subject + status + timestamps, **no email**.
2. **Detail (on Respond)** — opening the modal fires
   `GET /admin/speaker-meeting-requests/{id}`
   (`GetAdminSpeakerMeetingRequestEndpoint`) → `service.GetAsync` →
   `AdminSpeakerMeetingRequestDetail` which **adds the requester email** (PII on
   detail only). Each fetch is one audited Viewed event (D-185 pattern).
3. **Respond** — Send fires
   `PUT /admin/speaker-meeting-requests/{id}/respond`
   (`RespondToSpeakerMeetingRequestEndpoint`, body
   `RespondToSpeakerMeetingRequestRequest { Status, ResponseNote }`) →
   `service.RespondAsync`. The list reloads on success.
4. **Export (D-356)** —
   `POST /admin/speaker-meeting-requests/export`
   (`ExportSpeakerMeetingRequestsEndpoint : AdminGridExportEndpoint<AdminSpeakerMeetingRequestRow>`).
   The base resets `Skip=0`, sets `Top=MaxExportRows=5000`, calls the **same**
   `service.ListAllAsync` the list endpoint uses (so filters/sort behave
   identically), filters by selected `Ids` when supplied, then renders the
   workbook. Sheet `SpeakerMeetingRequests`; file prefix
   `simf-speaker-meeting-requests`; columns Speaker, Requester, Subject, Status,
   CreatedAt, RespondedAt (no email).

The actor id is resolved from the access-token `sub` claim on every endpoint for
the service's audit-log entry.

## 6. Validation + error handling

- **Pending → Pending is rejected.** A respond that does not move the request out
  of `Pending` returns **400 `SPEAKER_MEETING_REQUEST_STATUS_INVALID`**. The CP
  modal only offers Accept / Reject, so this is reachable only via a scripted /
  malformed client; the API re-loads the row and re-validates status before
  writing.
- **Public submit guards** (app side, surfaced for completeness):
  `SPEAKER_NOT_FOUND` (404), `SPEAKER_MEETING_REQUESTS_NOT_ALLOWED` (409, speaker
  has not opted in), `SPEAKER_MEETING_REQUEST_INVALID` (400).
- **Load failure / 500 on `/list`** — a red fallback toast
  "Could not load speaker meeting requests." /
  "تعذّر تحميل طلبات مقابلة المتحدثين."; no rows render and no empty-state shows
  (the load failed rather than returning an empty page).
- **Race / stale row** — if another admin already responded, the respond returns a
  4xx and the modal surfaces the bilingual `MessageForCurrentCulture()` error.

## 7. Edge cases + known limitations

- **Export only — no import, no toggle (D-356).** Speaker meeting requests are
  created from the app and responded to from the CP modal, so there is no generic
  import path. The grid wires `OnExport` only; the toolbar shows **Export** and no
  **Import** affordance. (This differs from CRUD pages such as
  [`admin-themes.md`](admin-themes.md) which carry both Export and Import.)
- **Requester email is detail-only PII.** The list (and the export) deliberately
  omit the requester email; it is fetched only when the Respond modal opens, via
  the audited GetById endpoint (the D-185 pattern). A non-`.xlsx` is N/A here —
  there is no upload.
- **Only Pending rows are actionable.** Resolved (Accepted / Rejected) rows show
  no Respond icon; the modal opens only for a `Pending` request.
- **A `View`-only admin cannot respond.** The Respond icon is wrapped in
  `<AuthorizedAction Permission="SpeakerMeetingRequests.Manage">`, so a View-only
  admin never sees it; the PUT `/respond` independently enforces `Manage` as
  defence-in-depth.
- **Export cap.** The whole-grid export is capped at 5000 rows; beyond that the
  admin must narrow with a column filter first.

## 8. i18n + RTL

`Admin.SpeakerMeetingRequests.*` keys (title, columns, status filters, the Respond
modal labels/buttons, loading + empty + load-failed copy) with full EN ↔ AR
parity. RTL mirrors the page, grid headers, per-column filter inputs, status
pills, and the Respond modal (title, Decision label/options, note label, footer
buttons). Grid chrome reuses the shared `Grid.*` keys (Export, Filter, pager,
select-all).

## 10. Use cases

- Review the pending queue, respond Accept/Reject to a request, export the
  filtered queue to XLSX. (No create/edit/delete use case — submission is an app
  use case against the public endpoint.)

## 11. E2E

See [`docs/tests/e2e/cp-admin-speaker-meeting-requests.md`](../../tests/e2e/cp-admin-speaker-meeting-requests.md):
E2E-SMR-001 golden (list → Respond Accept), 002 Respond Reject + note, 003
Pending→Pending 400 status-invalid, 004 list-omits-email / modal fetches detail,
005 stale-row race, 006 server-500 fallback toast, 007 RTL, 008 empty state, 009
auth (View-only sees no Respond icon; PUT 403), 010 no-View → `/not-permitted`,
011 only Pending rows expose Respond, 012 per-column filter, 013 column sort, 014
list write audited, 015 Excel export (D-356).

## 12. Related docs

- Sibling session-scoped queue E2E: [`cp-admin-meeting-requests.md`](../../tests/e2e/cp-admin-meeting-requests.md).
- Permissions: `PermissionCatalog.SpeakerMeetingRequests.{View, Manage, Export}`
  (all baselined `AdminOnly`); guide `docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`.
- Decisions: D-269 (the speaker-scoped meeting request + queue), D-356 (grid Excel
  export wave).
- Authority spec: SIMF-FDS-004 §5.4 (Speakers); Mockup page 20 "Speaker profile".

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-03 | D-269 | Original — `SpeakerMeetingRequest` entity + EF migration `D269_AddSpeakerMeetingRequests` + CP review queue (SimfDataGrid + Respond modal) + public submit endpoint. Response-only (no CRUD). |
| 2026-06-11 | D-356 | Grid Excel **export only** added (toolbar Export → `.xlsx`, sheet "SpeakerMeetingRequests", header `Speaker | Requester | Subject | Status | CreatedAt | RespondedAt`, capped 5000 rows, requester email excluded as detail-only PII). No import path, no toggle. E2E extended with E2E-SMR-015. |

_Last reviewed:_ 2026-06-11 by Claude (D-356 — reference doc authored; Excel export only).
