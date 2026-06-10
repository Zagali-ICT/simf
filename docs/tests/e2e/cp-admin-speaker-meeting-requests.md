# E2E test catalogue — Speaker meeting requests queue (`/admin/speaker-meeting-requests`)

| | |
|--|--|
| **Page** | [`cp/admin-speaker-meeting-requests.md`](../../pages/cp/admin-speaker-meeting-requests.md) _(reference doc not yet authored)_ |
| **Route** | `/admin/speaker-meeting-requests` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **Decision:** D-269 — the **speaker-scoped** meeting request. A signed-in,
> approved visitor on the **Speaker profile** (app screen #20, `/speakers/:speakerId`)
> may ask a speaker for a one-to-one meeting **only when** the speaker's
> `allowsMeetingRequests` flag is `true`. The mockup home tile shows Speakers
> **unlocked** (anonymous reads, D-199) and "طلب مقابلة" **locked 🔒** (login-only).
> This is a **NEW dedicated `SpeakerMeetingRequest` entity/table**, separate from the
> session-scoped `MeetingRequest` (mockup screen 27 "Request interview",
> [`cp-admin-meeting-requests.md`](./cp-admin-meeting-requests.md)). The two queues do
> not share rows.

> **Page permission:** `@attribute [RequirePermission(PermissionCatalog.SpeakerMeetingRequests.View)]`
> (`"SpeakerMeetingRequests.View"`). The **list** + **GetById** API endpoints enforce
> `SpeakerMeetingRequests.View`; the **respond** endpoint enforces
> `SpeakerMeetingRequests.Manage` (`"SpeakerMeetingRequests.Manage"`). Both are
> baselined `AdminOnly` and seeded idempotently. The per-row **Respond** action is a
> quiet reply (↩) icon inside the grid's `<RowActions>`, wrapped in
> `<AuthorizedAction Permission="SpeakerMeetingRequests.Manage">` — a `View`-only admin
> therefore does **not** see the icon at all (the button is hidden, not just rejected on
> submit). The API still independently enforces `Manage` on the PUT `/respond` as
> defence-in-depth (covered by E2E-SMR-009). All three endpoints also require
> `RequireApprovedAccount`.

> **CP is response-only — no create / edit / delete.** Speaker meeting requests are
> *submitted* by the authenticated audience from the Speaker profile screen
> (`POST /api/v1/app/speakers/{speakerId}/meeting-requests`, "طلب مقابلة", D-269).
> This CP page is a **review queue** rendered with the owner-mandated **SimfDataGrid**
> (server-paged, per-column filter + sort, full pager). Filter by status, then on a
> **Pending** row click the **Respond** (reply icon) action to Accept or Reject with an
> optional note. Resolved (Accepted / Rejected) rows show no action icon (the modal
> opens only for a `Pending` request). The classic "Add → Edit → Delete" round-trip
> therefore does **not** apply; the golden path is **list → Respond (Accept) → row flips
> to Accepted**.

> **No uniqueness / duplicate surface.** There is no name or code to collide on. The real
> conflict surface is a **stale Pending row** that another admin (or the same admin in a
> second tab) already responded to — a Pending → Pending decision is rejected server-side
> with `SPEAKER_MEETING_REQUEST_STATUS_INVALID` (400). E2E-SMR-005 covers it.

> **PII / audit note.** The list response carries the **requester name + subject + speaker
> name** but **not** the requester email; the email is fetched only when the respond modal
> opens, via `GET /admin/speaker-meeting-requests/{id}` (PII on detail only). Every list
> call is audited `Admin.SpeakerMeetingRequestsListed`; every detail fetch is audited
> `Admin.SpeakerMeetingRequestViewed`; every public submit is audited
> `SpeakerMeetingRequest.Submitted`; every response is audited
> `SpeakerMeetingRequest.Responded`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-SMR-001 | Golden path — list queue → Respond (Accept) → row flips to Accepted | happy | P0 | authored ✓ (`Admin_lists_then_responds_with_Accepted`) |
| E2E-SMR-002 | Respond (Reject) with a response note → row flips to Rejected | happy | P1 | _to author_ |
| E2E-SMR-003 | Pending → Pending respond → `SPEAKER_MEETING_REQUEST_STATUS_INVALID` (400) | error | P1 | authored ✓ (`Respond_with_Pending_status_returns_400`) |
| E2E-SMR-004 | List omits requester email; modal fetches detail (email + speaker name) on open | happy | P0 | authored ✓ (`List_response_does_not_contain_requester_email` + `Get_returns_detail_with_email_and_speaker_name`) |
| E2E-SMR-005 | Conflict: stale Pending row already responded → status-invalid race | error | P1 | _to author_ |
| E2E-SMR-006 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-SMR-007 | RTL render: Arabic toggle mirrors page + Respond modal | i18n | P1 | _to author_ |
| E2E-SMR-008 | Empty state renders `SimfEmptyState` ("No speaker meeting requests yet.") | happy | P1 | _to author_ |
| E2E-SMR-009 | Auth gate — `View`-only admin sees no Respond icon; PUT `/respond` → 403 | auth | P0 | authored ✓ (`Get_requires_administrator_role`) |
| E2E-SMR-010 | Auth gate — admin lacking `SpeakerMeetingRequests.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-SMR-011 | Only Pending rows show the Respond icon; resolved rows show none | happy | P2 | _to author_ |
| E2E-SMR-012 | Per-column grid filter (Status / Requester / Subject) narrows the grid, resets paging | happy | P1 | _to author_ |
| E2E-SMR-013 | Column sort (Requester / Status / Submitted) toggles `Sort` + `SortDescending` | happy | P2 | _to author_ |
| E2E-SMR-014 | List write is audited `Admin.SpeakerMeetingRequestsListed` | audit | P1 | authored ✓ (`List_writes_audit_event`) |
| E2E-SMR-015 | Excel export — toolbar Export downloads an .xlsx of the filtered grid; selected rows export just those (D-356) | happy | P1 | _to author_ |

## Scenarios

### E2E-SMR-001 — Golden path (list → Respond Accept)

```gherkin
Feature: Speaker meeting requests queue — respond happy path
  As an Administrator with SpeakerMeetingRequests.Manage
  I want to accept a pending speaker meeting request
  So that the requester gets a decision and the queue clears

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp using the Get-Totp helper
  And a speaker exists with allowsMeetingRequests = true
  And an approved Visitor has submitted a SpeakerMeetingRequest for that speaker
      (POST /api/v1/app/speakers/{speakerId}/meeting-requests), so one row is Pending
  And they have landed on /admin/speaker-meeting-requests

Scenario: Accept a pending speaker meeting request
  Given the grid is showing rows of every status (no filter applied)
  Then POST /account/api/admin/speaker-meeting-requests/list fires with Skip=0
  And each row shows the columns: Speaker (name), Requester, Subject, Status, Submitted (UTC), and a quiet Actions column
  And the Pending row shows the amber "Pending" pill
  And the summary line reads "Showing 1–{n} of {total}"

  When the administrator clicks the row's Respond (reply ↩ icon) action on the first Pending row
  Then GET /account/api/admin/speaker-meeting-requests/{id} fires (the detail fetch)
  And the "Respond to speaker meeting request" modal opens
  And the modal shows a description list: Speaker, Requester (with "Loading contact details…" then the requester email), Subject
  And the "Decision" select defaults to "Accept"
  And the "Response note (optional)" textarea is empty

  When the administrator leaves the Decision on "Accept"
  And types "Confirmed — meet at the speakers' lounge after the keynote." into the Response note
  And clicks "Send response"
  Then PUT /account/api/admin/speaker-meeting-requests/{id}/respond fires with Status=Accepted and the note text
  And the API returns HTTP 200
  And the modal closes
  And a green toast reads "Response sent." / "تم إرسال الردّ."
  And the list reloads
  When the administrator changes the "Status" column filter to "Accepted"
  Then the row appears with the green "Accepted" pill and no Respond icon
```

**Evidence:** `SpeakerMeetingRequestsTests.Admin_lists_then_responds_with_Accepted` (green) — covers the list returning the row (speaker name + requester + subject + Pending status) and the respond-Accepted transition at the API layer.

**Evidence captured (browser run):**
- Screenshot before: `docs/screenshots/cp-admin-speaker-meeting-requests-golden-before.png` (Pending grid)
- Screenshot (modal): `docs/screenshots/cp-admin-speaker-meeting-requests-respond-modal.png`
- Screenshot after: `docs/screenshots/cp-admin-speaker-meeting-requests-golden-after.png` (Accepted filter)
- Console errors: 0 expected
- Network: `/account/api/admin/speaker-meeting-requests/list`, `/{id}`, and `/{id}/respond` all return 200
- Audit rows: `Admin.SpeakerMeetingRequestsListed` (per list call), `Admin.SpeakerMeetingRequestViewed` (modal open), `SpeakerMeetingRequest.Responded` with `Detail.status = "Accepted"` and the actor id

### E2E-SMR-002 — Respond (Reject) with a note

```gherkin
Scenario: Reject a pending speaker meeting request with a response note
  Given a Pending speaker meeting request is visible
  When the administrator clicks the row's Respond (reply ↩ icon) action on that row
  And the modal opens
  And they change the "Decision" select to "Reject"
  And they type "Speaker is not taking 1:1 meetings this edition." into the Response note
  And they click "Send response"
  Then PUT /admin/speaker-meeting-requests/{id}/respond fires with Status=Rejected
  And the API returns HTTP 200
  And a green toast reads "Response sent." / "تم إرسال الردّ."
  And under the "Status" column filter set to "Rejected" the row shows the grey "Rejected" pill
```

### E2E-SMR-003 — Pending → Pending respond returns 400

```gherkin
Scenario: A respond that does not move the request out of Pending is rejected
  Given a Pending speaker meeting request {id}
  When a respond is issued for {id} with a target status of Pending
  Then PUT /admin/speaker-meeting-requests/{id}/respond returns HTTP 400
  And ApiResult.Error.Code = "SPEAKER_MEETING_REQUEST_STATUS_INVALID"
  And the request stays Pending
  # The CP modal only offers Accept / Reject, so STATUS_INVALID is reachable only via a
  # scripted client or a malformed request — assert at the API layer.
```

**Evidence:** `SpeakerMeetingRequestsTests.Respond_with_Pending_status_returns_400` (green).

### E2E-SMR-004 — List omits email; modal fetches detail on open

```gherkin
Scenario: The list contract carries no requester email; the modal lazily fetches it
  Given a Pending speaker meeting request whose requester has a known email
  When the administrator opens /admin/speaker-meeting-requests
  Then POST /account/api/admin/speaker-meeting-requests/list returns rows with
       Speaker name, Requester name, Subject and Status — but NO requester email field
  When the administrator clicks the row's Respond (reply ↩ icon) action
  Then the modal opens immediately showing the requester name and "Loading contact details…"
  And GET /account/api/admin/speaker-meeting-requests/{id} fires
  When the detail response (HTTP 200) arrives
  Then it carries the requester email (PII on detail only) and the speaker name
  And the requester email renders under the requester name
  When the administrator clicks "Cancel"
  Then the modal closes
  And NO PUT /respond request fires
  And the row remains Pending
```

**Evidence:** `SpeakerMeetingRequestsTests.List_response_does_not_contain_requester_email` (green) and `SpeakerMeetingRequestsTests.Get_returns_detail_with_email_and_speaker_name` (green).

### E2E-SMR-005 — Conflict: stale Pending row already responded

```gherkin
Scenario: Two admins race the same Pending row
  Given Admin A and Admin B both see the same Pending speaker meeting request {id}
  And Admin A accepts {id} (it is now Accepted server-side)
  When Admin B clicks the Respond (reply ↩ icon) action on their still-stale row and submits a decision
  Then PUT /admin/speaker-meeting-requests/{id}/respond returns a 4xx
  And the modal stays open
  And a red error toast surfaces the bilingual MessageForCurrentCulture()
  # The respond endpoint re-loads the row by id and re-validates status before writing.
```

### E2E-SMR-006 — Server 500 on `/list`

```gherkin
Scenario: API 500 on /list shows fallback bilingual toast
  Given the API is configured to return 500 on /admin/speaker-meeting-requests/list (e.g. DB down)
  When the administrator opens /admin/speaker-meeting-requests
  Then the page first shows "Loading…" / "جارٍ التحميل…"
  And then a red toast appears reading "Could not load speaker meeting requests." / "تعذّر تحميل طلبات مقابلة المتحدثين."
  And no table rows render
  And no SimfEmptyState renders (the load failed rather than returning an empty page)
```

### E2E-SMR-007 — RTL render

```gherkin
Scenario: Arabic toggle mirrors page + Respond modal
  Given the administrator is on /admin/speaker-meeting-requests in English
  When they switch the language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "طلبات مقابلة المتحدثين"
  And the SimfDataGrid column headers read المتحدث / مقدّم الطلب / الموضوع / الحالة / تاريخ الطلب / الإجراءات, mirrored for RTL
  And the per-column filter inputs (الحالة / مقدّم الطلب / الموضوع) sit under their headers and accept Arabic input
  And the status pills read قيد الانتظار / مقبول / مرفوض

  When they click the Respond (reply ↩ icon, title "الردّ") action on a Pending row
  Then the modal title reads "الردّ على طلب مقابلة المتحدث"
  And the Decision label reads "القرار" with options قبول / رفض
  And the note label reads "ملاحظة الردّ (اختيارية)"
  And the footer buttons read إرسال الردّ and إلغاء, mirrored for RTL
```

### E2E-SMR-008 — Empty state

```gherkin
Scenario: Empty queue renders SimfEmptyState
  Given the database has no SpeakerMeetingRequest rows (or none match the active filter)
  When the administrator opens /admin/speaker-meeting-requests
  Then the grid body renders the SimfEmptyState component (the grid's EmptyTemplate)
  And it shows the bilingual copy "No speaker meeting requests yet." / "لا توجد طلبات مقابلة متحدثين حتى الآن."
  And the per-column filter inputs (Status / Requester / Subject) remain usable
  And no error toast appears
```

### E2E-SMR-009 — Auth gate (View-only admin sees no Respond icon; API 403 as backstop)

```gherkin
Scenario: A View-only admin can read the queue but cannot respond
  # The Respond icon is wrapped in
  # <AuthorizedAction Permission="SpeakerMeetingRequests.Manage">, so a View-only
  # admin never sees it — the gate is UI-side as well as API-side.
  Given a signed-in admin whose role grants SpeakerMeetingRequests.View but NOT SpeakerMeetingRequests.Manage
  When they open /admin/speaker-meeting-requests
  Then the page loads and the grid renders (GET /list returns 200 — View is enough)
  And NO Respond (reply ↩ icon) action is shown on any Pending row (AuthorizedAction hides it)
  And no other write affordance is present
  # Defence-in-depth: even if the PUT is replayed directly (scripted client),
  # the API independently enforces Manage / administrator role:
  When the PUT /account/api/admin/speaker-meeting-requests/{id}/respond is issued directly
  Then the API returns HTTP 403
  And the request stays Pending
```

**Evidence:** `SpeakerMeetingRequestsTests.Get_requires_administrator_role` (green) — the admin desk endpoints reject a non-administrator caller with 403.

### E2E-SMR-010 — Auth gate (no View permission → /not-permitted)

```gherkin
Scenario: An admin lacking SpeakerMeetingRequests.View is denied the page
  Given a signed-in admin whose role does NOT grant SpeakerMeetingRequests.View
  When they navigate to /admin/speaker-meeting-requests
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/speaker-meeting-requests/list request fires
  # The nav item carries RequiredPermission = PermissionCatalog.SpeakerMeetingRequests.View,
  # so it is also hidden from the rail.
```

### E2E-SMR-011 — Only Pending rows expose Respond

```gherkin
Scenario: Resolved rows show no action icon
  Given no status filter is applied
  And the queue contains a mix of Pending, Accepted, and Rejected rows
  When the grid renders
  Then each Pending row shows the Respond (reply ↩ icon) action in the grid's RowActions
  And each Accepted row shows an empty RowActions cell (no icon)
  And each Rejected row shows an empty RowActions cell (no icon)
```

### E2E-SMR-012 — Per-column grid filter narrows the grid

```gherkin
Scenario: Typing into a column filter sends Filters[key] and resets paging
  # The SimfDataGrid exposes per-column filter inputs for the Filterable columns —
  # Status, Requester (requesterName) and Subject. Status is an enum-parse filter;
  # requesterName / subject are Contains filters.
  Given the administrator is on /admin/speaker-meeting-requests with multiple rows
  And the grid is on page 2 (Skip>0)
  When they enter "Pending" into the "Status" column filter
  Then POST /account/api/admin/speaker-meeting-requests/list fires with Filters["status"]="Pending" and Skip=0
  And only Pending rows render
  When they additionally type "Khalid" into the "Requester" column filter
  Then a list call fires carrying BOTH Filters["status"]="Pending" and Filters["requesterName"]="Khalid"
  And the grid narrows to rows matching both
  When they additionally type "keynote" into the "Subject" column filter
  Then a list call fires also carrying Filters["subject"]="keynote"
  When they clear all column filters
  Then a list call fires with an empty Filters dictionary and the full queue returns
```

### E2E-SMR-013 — Column sort toggles

```gherkin
Scenario: Clicking a sortable header toggles Sort and SortDescending
  Given the administrator is on /admin/speaker-meeting-requests with the default order (newest first)
  When they click the "Requester" column header
  Then POST /account/api/admin/speaker-meeting-requests/list fires with Sort="requesterName" and SortDescending=false
  And the rows render in ascending Requester order
  When they click the "Requester" header again
  Then a list call fires with Sort="requesterName" and SortDescending=true
  And the rows render in descending Requester order
  When they click the "Status" column header
  Then a list call fires with Sort="status" and SortDescending=false
  When they click the "Submitted" column header
  Then a list call fires with Sort="createdAt"
```

### E2E-SMR-014 — List write is audited

```gherkin
Scenario: Loading the queue writes an audit event
  Given an Administrator with SpeakerMeetingRequests.View
  When they open /admin/speaker-meeting-requests and the list call fires
  Then an audit event Admin.SpeakerMeetingRequestsListed is written with the actor id
```

**Evidence:** `SpeakerMeetingRequestsTests.List_writes_audit_event` (green).

### E2E-SMR-015 — Excel export (D-356)

```gherkin
Scenario: Export the speaker meeting requests queue to an XLSX workbook
  # D-356 added grid Excel EXPORT to this queue (export-only — there is no import
  # path because requests are created from the app and responded to from the CP
  # modal). The CP grid wires OnExport only; the toolbar shows an "Export" action
  # (no "Import" affordance). The download goes through the generic BFF proxy via
  # simfAccount.downloadXlsx → POST /account/api/admin/speaker-meeting-requests/export,
  # posting AdminGridExportRequest { Ids, Query }.
  Given the administrator is on /admin/speaker-meeting-requests with at least three rows
  And no rows are selected
  When they click the toolbar "Export" / "تصدير" action
  Then POST /account/api/admin/speaker-meeting-requests/export fires
  And the body carries an empty Ids list and the current Query (so the WHOLE filtered grid is exported)
  And the API returns HTTP 200 with an .xlsx body
  And the browser saves a file named simf-speaker-meeting-requests-{timestamp}.xlsx
  And the workbook's "SpeakerMeetingRequests" sheet header row reads Speaker | Requester | Subject | Status | CreatedAt | RespondedAt
  And the requester email column is NOT present (PII is detail-only, the D-185 pattern)

  When the administrator first sets the "Status" column filter to "Pending"
  And then clicks "Export" with no rows selected
  Then the export body carries Filters["status"]="Pending" in Query (export honours the active grid filters/sort)
  And only the Pending rows appear in the workbook

  When the administrator instead ticks the checkboxes on exactly two rows
  And clicks "Export"
  Then the export body carries those two row Ids and a null Query
  And the workbook contains exactly those two rows
  # The API caps the export at 5000 rows.
```

**Evidence:** `SpeakerMeetingRequestsExcelTests` (`tests/SIMF.Api.Tests/SpeakerMeetingRequestsExcelTests.cs`) covers the export endpoint at the API layer — the `SpeakerMeetingRequests` sheet, the six-column header (no requester email), filter/selection honoured, and the `SpeakerMeetingRequests.Export` permission gate.

**Evidence captured (browser run):**
- Screenshot (toolbar): `docs/screenshots/cp-admin-speaker-meeting-requests-export-toolbar.png` (Export action present, no Import)
- Network: `POST /account/api/admin/speaker-meeting-requests/export` returns 200 with the .xlsx body
- Downloaded file: `simf-speaker-meeting-requests-{timestamp}.xlsx`, sheet `SpeakerMeetingRequests`, header `Speaker | Requester | Subject | Status | CreatedAt | RespondedAt`
- Console errors: 0 expected

---

## Implementation notes

- **API integration tests** at [`tests/SIMF.Api.Tests/SpeakerMeetingRequestsTests.cs`](../../../tests/SIMF.Api.Tests/SpeakerMeetingRequestsTests.cs)
  cover the same surface at a lower layer (no browser): the public submit + validation
  (`SPEAKER_MEETING_REQUEST_INVALID`, `SPEAKER_NOT_FOUND`,
  `SPEAKER_MEETING_REQUESTS_NOT_ALLOWED`), the admin list + audit
  (`Admin_lists_then_responds_with_Accepted`, `List_writes_audit_event`), the
  list-omits-email contract (`List_response_does_not_contain_requester_email`), the
  GetById detail with email + speaker name (`Get_returns_detail_with_email_and_speaker_name`),
  the respond Accept/Reject path, the Pending → Pending guard
  (`Respond_with_Pending_status_returns_400` → `SPEAKER_MEETING_REQUEST_STATUS_INVALID`),
  and the administrator-role gate (`Get_requires_administrator_role`). During the
  Playwright transition keep both layers; the browser E2E adds the modal/lazy-detail-fetch,
  filter-resets-paging, toast text, and RTL coverage that the API tests cannot assert.
- **Backing surface:**
  - Public submit — `POST /api/v1/app/speakers/{speakerId}/meeting-requests`
    (body `SubmitSpeakerMeetingRequestRequest` = `requesterName`, `subject`;
    `RequireApprovedAccount` + rate-limited) → `ApiResult<SpeakerMeetingRequestSubmitted>`
    (`id`, `speakerId`, `status` = Pending, `createdAt`)
  - Admin desk — `POST /admin/speaker-meeting-requests/list`,
    `GET /admin/speaker-meeting-requests/{id}` (adds `requesterEmail`, PII on detail only),
    `PUT /admin/speaker-meeting-requests/{id}/respond` (Accepted / Rejected + optional note)
  - Permissions — `PermissionCatalog.SpeakerMeetingRequests.View` (page + list + GetById) /
    `.Manage` (respond), both baselined `AdminOnly`
  - Error codes — `SPEAKER_NOT_FOUND` (404), `SPEAKER_MEETING_REQUESTS_NOT_ALLOWED` (409),
    `SPEAKER_MEETING_REQUEST_INVALID` (400), `SPEAKER_MEETING_REQUEST_STATUS_INVALID` (400)
  - Audit events — `SpeakerMeetingRequest.Submitted`, `SpeakerMeetingRequest.Responded`,
    `Admin.SpeakerMeetingRequestsListed`, `Admin.SpeakerMeetingRequestViewed`
- **Entity separation.** `SpeakerMeetingRequest` is a NEW dedicated entity/table, distinct
  from the session-scoped `MeetingRequest` (mockup screen 27); the two admin queues
  ([`cp-admin-meeting-requests.md`](./cp-admin-meeting-requests.md) vs this file) never
  share rows.
- **Seeding a Pending row for the E2E run.** The CP cannot create a speaker meeting
  request — submit one via the public endpoint
  `POST /api/v1/app/speakers/{speakerId}/meeting-requests` (body: `requesterName`,
  `subject`) as an approved visitor against a speaker whose `allowsMeetingRequests` is
  `true`, or insert directly into the `SpeakerMeetingRequest` table (Status=Pending) for
  fixture setup.

---

_Last reviewed:_ `2026-06-10` by `SIMF Team` (D-356 Phase 5 — Excel + toggle; added E2E-SMR-015 grid Excel export. Original D-269 authoring 2026-06-03).
