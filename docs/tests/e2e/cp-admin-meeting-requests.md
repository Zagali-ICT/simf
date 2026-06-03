# E2E test catalogue — Meeting requests queue (`/admin/meeting-requests`)

| | |
|--|--|
| **Page** | [`cp/admin-meeting-requests.md`](../../pages/cp/admin-meeting-requests.md) _(reference doc not yet authored)_ |
| **Route** | `/admin/meeting-requests` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-03 |

> **Page permission:** `@attribute [RequirePermission(PermissionCatalog.MeetingRequests.View)]`
> (`"MeetingRequests.View"`). The **list** + **GetById** API endpoints enforce
> `MeetingRequests.View`; the **respond** endpoint enforces `MeetingRequests.Manage`
> (`"MeetingRequests.Manage"`). Both are baselined `AdminOnly` and seeded idempotently.
> **D-256:** the per-row **Respond** action is now a quiet reply (↩) icon inside the
> grid's `<RowActions>`, wrapped in `<AuthorizedAction Permission="MeetingRequests.Manage">`
> — a `View`-only admin therefore does **not** see the icon at all (the button is hidden,
> not just rejected on submit). The API still independently enforces `Manage` on the PUT
> `/respond` as defence-in-depth (covered by E2E-MTR-009).

> **CP is response-only — no create / edit / delete.** Meeting requests are
> *submitted* by the authenticated audience from the live-session screen
> (`POST /api/v1/sessions/{sessionId}/meeting-requests`, "طلب مقابلة" pill, D-174).
> This CP page is a **review queue** rendered with the owner-mandated **SimfDataGrid**
> (server-paged, per-column filter + sort, full pager; **page size Top = 20**). Filter by
> status (now a filterable grid column, not a custom select), then on a **Pending** row
> click the **Respond** (reply icon) action to Accept or Reject with an optional note.
> Accepted/Rejected rows show no action icon (the modal opens only for
> `MeetingRequestStatus.Pending`). The classic "Add → Edit → Delete" round-trip therefore
> does **not** apply; the golden path is **filter → Respond (Accept) → row flips to Accepted**.

> **No uniqueness / duplicate surface.** There is no name or code to collide on. The real
> conflict surface is a **stale Pending row** that another admin (or the same admin in a
> second tab) already responded to, or a row deleted out from under the modal —
> server returns `MEETING_REQUEST_NOT_FOUND` (404) or `MEETING_REQUEST_STATUS_INVALID`
> (400). E2E-MTR-005 covers the status-invalid race.

> **PII / audit note.** The list response carries requester *names* but **not** emails
> (D-185 moved email off the list contract). The email is fetched only when the modal
> opens, via `GET /admin/meeting-requests/{id}` (audited `Admin.MeetingRequestViewed`).
> Every list call is audited `Admin.MeetingRequestsListed`; every response is audited
> `MeetingRequest.Responded` **plus** a second `Admin.MeetingRequestViewed` (the respond
> path also discloses email, so SOC sees a Viewed event regardless of which endpoint
> disclosed it).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MTR-001 | Golden path — filter Pending → Respond (Accept) → row flips to Accepted | happy | P0 | _to author_ |
| E2E-MTR-002 | Respond (Reject) with a response note → row flips to Rejected | happy | P1 | _to author_ |
| E2E-MTR-003 | Status column filter cycles Pending / Accepted / Rejected / cleared, resets paging | happy | P1 | _to author_ |
| E2E-MTR-004 | Respond modal fetches detail (requester email) on open; Cancel discards | happy | P1 | _to author_ |
| E2E-MTR-005 | Conflict: stale Pending row already responded → `MEETING_REQUEST_STATUS_INVALID` / `NOT_FOUND` | error | P1 | _to author_ |
| E2E-MTR-006 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-MTR-007 | RTL render: Arabic toggle mirrors page + Respond modal | i18n | P1 | _to author_ |
| E2E-MTR-008 | Empty state renders `SimfEmptyState` ("No meeting requests yet.") | happy | P1 | _to author_ |
| E2E-MTR-009 | Auth gate — `View`-only admin sees no Respond icon; PUT `/respond` → 403 | auth | P0 | _to author_ |
| E2E-MTR-010 | Auth gate — admin lacking `MeetingRequests.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-MTR-011 | Only Pending rows show the Respond icon; resolved rows show none | happy | P2 | _to author_ |
| E2E-MTR-012 | Per-column grid filter (Requester / Subject) narrows the grid, resets paging | happy | P1 | _to author_ |
| E2E-MTR-013 | Column sort (Requester / Status) toggles `Sort` + `SortDescending` | happy | P2 | _to author_ |

## Scenarios

### E2E-MTR-001 — Golden path (filter Pending → Accept)

```gherkin
Feature: Meeting requests queue — respond happy path
  As an Administrator with MeetingRequests.Manage
  I want to accept a pending in-session meeting request
  So that the requester gets a decision and the queue clears

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp using the Get-Totp helper
  And at least one MeetingRequest exists in the Pending state on an active Session
  And they have landed on /admin/meeting-requests

Scenario: Accept a pending meeting request
  Given the grid is showing rows of every status (no filter applied)
  When the administrator types "Pending" into the "Status" column filter input
  Then POST /account/api/admin/meeting-requests/list fires with Filters["status"]="Pending" and Skip=0
  And the grid shows only rows with the amber "Pending" pill
  And each row shows the columns: Session ("{Code} — {Title}"), Requester, Subject, Status, Submitted (UTC), Responded ("—"), and a quiet Actions column
  And the summary line reads "Showing 1–{n} of {total}" (page size Top=20)

  When the administrator clicks the row's Respond (reply ↩ icon) action on the first Pending row
  Then GET /account/api/admin/meeting-requests/{id} fires (the detail fetch)
  And the "Respond to meeting request" modal opens
  And the modal shows a description list: Session, Requester (with "Loading contact details…" then the requester email), Subject
  And the "Decision" select defaults to "Accept"
  And the "Response note (optional, ≤2000 chars)" textarea is empty

  When the administrator leaves the Decision on "Accept"
  And types "Confirmed — meet at booth A3 after the session." into the Response note
  And clicks "Send response"
  Then PUT /account/api/admin/meeting-requests/{id}/respond fires with Status=Accepted and the note text
  And the API returns HTTP 200
  And the modal closes
  And a green toast reads "Response sent." / "تم إرسال الردّ."
  And the list reloads
  And (with the "Status" column filter still "Pending") the responded row no longer appears
  When the administrator changes the "Status" column filter to "Accepted"
  Then the row appears with the green "Accepted" pill, a populated "Responded" UTC timestamp, and no Respond icon
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-meeting-requests-golden-before.png` (Pending grid)
- Screenshot (modal): `docs/screenshots/cp-admin-meeting-requests-respond-modal.png`
- Screenshot after: `docs/screenshots/cp-admin-meeting-requests-golden-after.png` (Accepted filter)
- Console errors: 0 expected
- Network: `/account/api/admin/meeting-requests/list`, `/{id}`, and `/{id}/respond` all return 200
- Audit rows (Identity/App audit log): `Admin.MeetingRequestsListed` (per list call), `Admin.MeetingRequestViewed` (modal open + a second on respond), and `MeetingRequest.Responded` with `Detail.status = "Accepted"` and the actor id

### E2E-MTR-002 — Respond (Reject) with a note

```gherkin
Scenario: Reject a pending meeting request with a response note
  Given a Pending meeting request is visible
  When the administrator clicks the row's Respond (reply ↩ icon) action on that row
  And the modal opens
  And they change the "Decision" select to "Reject"
  And they type "Speaker is unavailable for 1:1s this edition." into the Response note
  And they click "Send response"
  Then PUT /admin/meeting-requests/{id}/respond fires with Status=Rejected
  And the API returns HTTP 200
  And a green toast reads "Response sent." / "تم إرسال الردّ."
  And under the "Status" column filter set to "Rejected" the row shows the grey "Rejected" pill and the Responded UTC timestamp
```

### E2E-MTR-003 — Status column filter cycles and resets paging

```gherkin
Scenario: The Status column filter drives the list query and resets Skip to 0
  # D-256: status is now a filterable SimfDataGrid column (Key="status"),
  # not a standalone <select>. Typing a status value into the column filter
  # input sends GridQuery.Filters["status"] and the grid resets paging.
  Given the administrator is on /admin/meeting-requests with no filter applied
  When they enter "Pending" into the "Status" column filter
  Then a list call fires with Filters["status"]="Pending" and Skip=0
  And only Pending rows render
  When they change the "Status" column filter to "Accepted"
  Then a list call fires with Filters["status"]="Accepted" and Skip=0
  And only Accepted rows render
  When they change the "Status" column filter to "Rejected"
  Then only Rejected rows render
  When they clear the "Status" column filter
  Then the filter clears (empty Filters dictionary) and rows of every status render
  And any prior error toast is cleared on each list reload
```

### E2E-MTR-004 — Detail fetch on modal open + Cancel discards

```gherkin
Scenario: Modal lazily fetches the requester email, Cancel makes no write
  Given a Pending meeting request whose requester has a known email
  When the administrator clicks the row's Respond (reply ↩ icon) action
  Then the modal opens immediately showing the requester name and "Loading contact details…"
  And GET /account/api/admin/meeting-requests/{id} fires
  When the detail response (HTTP 200, with RequesterEmail) arrives
  Then the requester email renders under the requester name
  When the administrator clicks "Cancel"
  Then the modal closes
  And NO PUT /respond request fires
  And the row remains Pending with no Responded timestamp
```

### E2E-MTR-005 — Conflict: stale Pending row already responded

```gherkin
Scenario: Two admins race the same Pending row
  Given Admin A and Admin B both see the same Pending meeting request {id}
  And Admin A accepts {id} (it is now Accepted server-side)
  When Admin B clicks the Respond (reply ↩ icon) action on their still-stale row and submits a decision
  Then PUT /admin/meeting-requests/{id}/respond returns a 4xx
  And the modal stays open
  And a red error toast surfaces the bilingual MessageForCurrentCulture()
  # The respond endpoint re-loads the row by id and re-validates status:
  #  - a deleted row → ApiResult.Error.Code = "MEETING_REQUEST_NOT_FOUND" (404)
  #  - a Pending-target decision → "MEETING_REQUEST_STATUS_INVALID" (400),
  #    "Response status must be Accepted or Rejected." /
  #    "يجب أن تكون حالة الردّ مقبولة أو مرفوضة."
  # (The CP modal only offers Accept/Reject, so STATUS_INVALID is reachable
  #  only via a scripted client or a malformed request — assert at the API layer.)
```

### E2E-MTR-006 — Server 500 on `/list`

```gherkin
Scenario: API 500 on /list shows fallback bilingual toast
  Given the API is configured to return 500 on /admin/meeting-requests/list (e.g. DB down)
  When the administrator opens /admin/meeting-requests
  Then the page first shows "Loading…" / "جارٍ التحميل…"
  And then a red toast appears reading "Could not load meeting requests." / "تعذّر تحميل طلبات المقابلات."
  And no table rows render
  And no SimfEmptyState renders (the load failed rather than returning an empty page)
```

### E2E-MTR-007 — RTL render

```gherkin
Scenario: Arabic toggle mirrors page + Respond modal
  Given the administrator is on /admin/meeting-requests in English
  When they switch the language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "طلبات المقابلات"
  And the SimfDataGrid column headers read الجلسة / مقدّم الطلب / الموضوع / الحالة / تاريخ الطلب / تاريخ الردّ / الإجراءات, mirrored for RTL
  And the per-column filter inputs (مقدّم الطلب / الموضوع / الحالة) sit under their headers and accept Arabic input
  And the status pills read قيد الانتظار / مقبول / مرفوض

  When they click the Respond (reply ↩ icon, title "الردّ") action on a Pending row
  Then the modal title reads "الردّ على طلب المقابلة"
  And the Decision label reads "القرار" with options قبول / رفض
  And the note label reads "ملاحظة الردّ (اختيارية، حتى 2000 محرف)"
  And the footer buttons read إرسال الردّ and إلغاء, mirrored for RTL
```

### E2E-MTR-008 — Empty state

```gherkin
Scenario: Empty queue renders SimfEmptyState
  Given the database has no MeetingRequest rows (or none match the active filter)
  When the administrator opens /admin/meeting-requests
  Then the grid body renders the SimfEmptyState component (the grid's EmptyTemplate)
  And it shows the bilingual copy "No meeting requests yet." / "لا توجد طلبات مقابلات حتى الآن."
  And the per-column filter inputs (Requester / Subject / Status) remain usable
  And no error toast appears
```

### E2E-MTR-009 — Auth gate (View-only admin sees no Respond icon; API 403 as backstop)

```gherkin
Scenario: A View-only admin can read the queue but cannot respond
  # D-256: the Respond icon is wrapped in
  # <AuthorizedAction Permission="MeetingRequests.Manage">, so a View-only
  # admin never sees it — the gate is now UI-side as well as API-side.
  Given a signed-in admin whose role grants MeetingRequests.View but NOT MeetingRequests.Manage
  When they open /admin/meeting-requests
  Then the page loads and the grid renders (GET /list returns 200 — View is enough)
  And NO Respond (reply ↩ icon) action is shown on any Pending row (AuthorizedAction hides it)
  And no other write affordance is present
  # Defence-in-depth: even if the PUT is replayed directly (scripted client),
  # the API independently enforces Manage:
  When the PUT /account/api/admin/meeting-requests/{id}/respond is issued directly
  Then the API returns HTTP 403
  And the row stays Pending
```

### E2E-MTR-010 — Auth gate (no View permission → /not-permitted)

```gherkin
Scenario: An admin lacking MeetingRequests.View is denied the page
  Given a signed-in admin whose role does NOT grant MeetingRequests.View
  When they navigate to /admin/meeting-requests
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/meeting-requests/list request fires
  # The Module.MeetingRequests nav item carries RequiredPermission =
  # PermissionCatalog.MeetingRequests.View, so it is also hidden from the rail.
```

### E2E-MTR-011 — Only Pending rows expose Respond

```gherkin
Scenario: Resolved rows show no action icon
  Given no status filter is applied
  And the queue contains a mix of Pending, Accepted, and Rejected rows
  When the grid renders
  Then each Pending row shows the Respond (reply ↩ icon) action in the grid's RowActions
  And each Accepted row shows an empty RowActions cell (no icon)
  And each Rejected row shows an empty RowActions cell (no icon)
  And Accepted / Rejected rows show a populated "Responded" UTC timestamp while Pending rows show "—"
```

### E2E-MTR-012 — Per-column grid filter narrows the grid

```gherkin
Scenario: Typing into a column filter sends Filters[key] and resets paging
  # D-256: the SimfDataGrid exposes per-column filter inputs for the
  # Filterable columns — Requester (Key="requesterName"),
  # Subject (Key="subject"), and Status (Key="status"). The backend
  # honours requesterName/subject as Contains filters and status as an
  # enum-parse filter (MeetingRequestService.ListAllAsync). The Session
  # column is NOT filterable (code/title are joined after paging).
  Given the administrator is on /admin/meeting-requests with multiple Pending rows
  And the grid is on page 2 (Skip=20)
  When they type "Khalid" into the "Requester" column filter
  Then POST /account/api/admin/meeting-requests/list fires with Filters["requesterName"]="Khalid" and Skip=0
  And the grid narrows to rows whose Requester contains "Khalid"
  And the summary line re-counts as "Showing 1–{n} of {total}"

  When they additionally type "interview" into the "Subject" column filter
  Then a list call fires carrying BOTH Filters["requesterName"]="Khalid" and Filters["subject"]="interview"
  And the grid narrows further to rows matching both
  When they clear both column filters
  Then a list call fires with an empty Filters dictionary and the full queue returns
```

### E2E-MTR-013 — Column sort toggles

```gherkin
Scenario: Clicking a sortable header toggles Sort and SortDescending
  # D-256: Sortable columns are requesterName, subject, status, createdAt,
  # respondedAt (Session is not sortable). Default order is CreatedAt
  # descending (newest first) when no Sort is set.
  Given the administrator is on /admin/meeting-requests with the default order (newest first)
  When they click the "Requester" column header
  Then POST /account/api/admin/meeting-requests/list fires with Sort="requesterName" and SortDescending=false
  And the rows render in ascending Requester order
  When they click the "Requester" header again
  Then a list call fires with Sort="requesterName" and SortDescending=true
  And the rows render in descending Requester order
  When they click the "Status" column header
  Then a list call fires with Sort="status" and SortDescending=false
  And the rows group by status order (Pending, Accepted, Rejected by enum value)
```

---

## Implementation notes

- **API integration tests** at `tests/SIMF.Api.Tests/MeetingRequestsTests.cs` cover the
  same surface at a lower layer (no browser): the public submit + validation
  (`MEETING_REQUEST_INVALID`, `MEETING_REQUEST_SESSION_NOT_FOUND`), the admin list +
  status filter, the GetById detail (email disclosure), the respond Accept/Reject path,
  the Pending → Pending guard (`MEETING_REQUEST_STATUS_INVALID`), the not-found guard
  (`MEETING_REQUEST_NOT_FOUND`), and the per-permission gates (`MeetingRequests.View`
  on list/GetById, `MeetingRequests.Manage` on respond). During the Playwright
  transition keep both layers; the browser E2E adds the modal/lazy-detail-fetch,
  filter-resets-paging, toast text, and RTL coverage that the API tests cannot assert.
- **Backing surface (verified this session):**
  - CP page — `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/MeetingRequestsList.razor`
  - BFF routes — `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs`
    (`/account/api/admin/meeting-requests/list` POST, `/{id}` GET, `/{id}/respond` PUT)
  - Admin client — `src/Shared/SIMF.ApiClient/SimfAdminClient.cs`
    (`ListAdminMeetingRequestsAsync`, `GetAdminMeetingRequestAsync`, `RespondToAdminMeetingRequestAsync`)
  - API endpoints — `src/Backend/SIMF.Api/Endpoints/Sessions/MeetingRequestEndpoints.cs`
  - Service — `src/Backend/SIMF.Infrastructure/MeetingRequests/MeetingRequestService.cs`
  - Contracts — `src/Shared/SIMF.Contracts/Sessions/MeetingRequests.cs`
    (`AdminMeetingRequestRow`, `AdminMeetingRequestDetail`, `RespondToMeetingRequestRequest`)
  - Permissions — `PermissionCatalog.MeetingRequests.View` / `.Manage`
  - Nav — `Module.MeetingRequests` → `/admin/meeting-requests`, `RequiredPermission = MeetingRequests.View`
  - Strings — `Admin.MeetingRequests.*` in `Resources/Strings.resx` + `Strings.ar.resx`
  - Error codes — `ErrorCodes.MeetingRequest{Invalid,NotFound,SessionNotFound,StatusInvalid}`
  - Audit events — `Admin.MeetingRequestsListed`, `Admin.MeetingRequestViewed`,
    `MeetingRequest.Submitted`, `MeetingRequest.Responded`
- **Manual smoke as canonical-source-of-truth today.** Until Playwright is adopted, the
  canonical run is a Chrome DevTools MCP session: sign in via the Auth setup, walk each
  scenario, capture screenshots into `docs/screenshots/cp-admin-meeting-requests-*.png`.
- **Seeding a Pending row for the E2E run.** The CP cannot create a meeting request —
  submit one via the public endpoint `POST /api/v1/sessions/{sessionId}/meeting-requests`
  (body: `RequesterName`, `Subject`) as an approved visitor against an **active** session,
  or insert directly into `MeetingRequests` (Status=Pending) for fixture setup.

---

_Last reviewed:_ 2026-06-03 by Claude (E2E catalogue rebuild) (D-256/D-257 grid affordances reconciled).
