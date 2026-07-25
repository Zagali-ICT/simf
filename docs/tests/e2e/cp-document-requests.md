# E2E test catalogue — Participation document requests desk (`/admin/document-requests`)

| | |
|--|--|
| **Page** | [`cp/document-requests.md`](../../pages/cp/document-requests.md) |
| **Route** | `/admin/document-requests` (`DocumentRequestsList.razor`) |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-26 (D-500 Wave 5) |

> **Decision:** D-500 — the **participation document request**. An approved
> attendee on the **Requests** screen (app `/requests`, الطلبات, Figma `1408:9726`)
> submits a request for a participation document (`POST /app/document-requests`,
> `documentType` = AttendanceCertificate / ParticipationLetter / InvitationLetter
> + optional note). Those requests land here as `Pending` rows; the administrator
> reviews each and **responds** (Accept or Reject) with an optional note. This is a
> **NEW additive `ParticipationDocumentRequests` table** (migration `D500`).

> **Page permission:** `@attribute [RequirePermission(PermissionCatalog.ParticipationDocumentRequests.View)]`
> (`"ParticipationDocumentRequests.View"`). The **list** + **GetById** API endpoints enforce
> `ParticipationDocumentRequests.View`; the **respond** endpoint enforces
> `ParticipationDocumentRequests.Manage`. Both are baselined `AdminOnly` and seeded
> idempotently. The per-row **Respond** action is a quiet reply (↩) icon inside the
> grid's `<RowActions>`, wrapped in `<AuthorizedAction Permission="ParticipationDocumentRequests.Manage">`
> — a `View`-only admin therefore does **not** see the icon at all. The API still
> independently enforces `Manage` on the PUT `/respond` as defence-in-depth. All three
> endpoints also require `RequireApprovedAccount`. This desk **mirrors**
> `/admin/speaker-meeting-requests`.

> **CP is response-only — no create / edit / delete.** Document requests are
> *submitted* by the authenticated audience from the Requests screen
> (`POST /api/v1/app/document-requests`). This CP page is a **review queue**
> rendered with the owner-mandated **SimfDataGrid** (server-paged, per-column
> filter + sort, full pager). Filter by status, then on a **Pending** row click the
> **Respond** (reply icon) action to Accept or Reject with an optional note (≤ 2000).
> Resolved (Accepted / Rejected) rows show no action icon. The golden path is
> **list → Respond (Accept) → row flips to Accepted**.

> **PII / audit note.** The list response carries the **requester name + document
> type + subject** but **not** the requester email; the email is resolved on read
> and revealed only when the respond/detail modal opens, via
> `GET /admin/document-requests/{id}` (PII on detail only).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-CPDR-001 | Golden path — list queue → Respond (Accept) + note → row flips to Accepted | happy | P0 | authored ✓ (`ParticipationDocumentRequestsTests`, API) |
| E2E-CPDR-002 | Respond (Reject) with a response note → row flips to Rejected | happy | P1 | authored ✓ (`ParticipationDocumentRequestsTests`, API) |
| E2E-CPDR-003 | Pending → Pending respond → 400 `PARTICIPATION_DOCUMENT_REQUEST_STATUS_INVALID` | error | P1 | authored ✓ (`ParticipationDocumentRequestsTests`, API) |
| E2E-CPDR-004 | List omits requester email; detail/respond modal reveals it (resolved on read) on open | happy | P0 | authored ✓ (`ParticipationDocumentRequestsTests`, API) |
| E2E-CPDR-005 | Filter by status narrows the grid; only Pending rows show the Respond icon | happy | P1 | _to author_ |
| E2E-CPDR-006 | Permission gate — a non-Admin role can't open the page (→ `/not-permitted`); `View`-only sees no Respond icon; PUT `/respond` → 403 | auth | P0 | authored ✓ (`ParticipationDocumentRequestsTests`, API — admin-role gate) |
| E2E-CPDR-007 | Empty state renders `SimfEmptyState` ("No document requests yet.") | empty | P1 | _to author_ |
| E2E-CPDR-008 | RTL render: Arabic toggle mirrors page + Respond modal | i18n | P1 | _to author_ |
| E2E-CPDR-009 | Decision notifies the requester (R-2) — Accept or Reject dispatches a ParticipationDocumentDecided in-app notification | happy | P1 | authored ✓ |
| E2E-CPDR-010 | Duplicate-pending guard (R-4) — a second Pending request for the same document type is 409 APP_REQUEST_DUPLICATE_PENDING; a different type is allowed | conflict | P1 | authored ✓ |

## Scenarios

### E2E-CPDR-001 — Golden path (list → Respond Accept)

```gherkin
Feature: Participation document requests desk — respond happy path
  As an Administrator with ParticipationDocumentRequests.Manage
  I want to accept a pending document request
  So that the requester gets their participation document decision

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp using the Get-Totp helper
  And an approved Visitor has submitted a document request from the app
      (POST /api/v1/app/document-requests with documentType = ParticipationLetter (1)), so one row is Pending
  And they have landed on /admin/document-requests

Scenario: Accept a pending document request
  Given the grid is showing rows of every status (no filter applied)
  Then POST /account/api/admin/document-requests/list fires with Skip=0
  And each row shows the columns: Requester, Document type, Status, Submitted (Saudi time), and a quiet Actions column
  And the Pending row shows the amber "Pending" pill

  When the administrator clicks the row's Respond (reply ↩ icon) action on the first Pending row
  Then GET /account/api/admin/document-requests/{id} fires (the detail fetch)
  And the "Respond to document request" modal opens
  And it shows the requester (with "Loading contact details…" then the requester email), the document type, and the note
  And the "Decision" select defaults to "Accept"
  And the "Response note (optional)" textarea is empty

  When the administrator leaves the Decision on "Accept"
  And types "Certificate issued — collect at the registration desk." (≤ 2000 chars) into the Response note
  And clicks "Send response"
  Then PUT /account/api/admin/document-requests/{id}/respond fires with Status=Accepted and the note text
  And the API returns HTTP 200
  And the modal closes
  And a green toast reads "Response sent." / "تم إرسال الردّ."
  And the list reloads
  When the administrator changes the "Status" column filter to "Accepted"
  Then the row appears with the green "Accepted" pill and no Respond icon
```

**Evidence:** `ParticipationDocumentRequestsTests` (green) — covers the public submit → Pending row and the respond-Accepted transition at the API layer.

### E2E-CPDR-002 — Respond (Reject) with a note

```gherkin
Scenario: Reject a pending document request with a response note
  Given a Pending document request is visible
  When the administrator clicks the row's Respond (reply ↩ icon) action on that row
  And the modal opens
  And they change the "Decision" select to "Reject"
  And they type "We cannot issue this letter for the requested role." into the Response note
  And they click "Send response"
  Then PUT /admin/document-requests/{id}/respond fires with Status=Rejected
  And the API returns HTTP 200
  And a green toast reads "Response sent." / "تم إرسال الردّ."
  And under the "Status" column filter set to "Rejected" the row shows the grey "Rejected" pill
```

**Evidence:** `ParticipationDocumentRequestsTests` (green) — the respond-Rejected path.

### E2E-CPDR-003 — Pending → Pending respond returns 400

```gherkin
Scenario: A respond that does not move the request out of Pending is rejected
  Given a Pending document request {id}
  When a respond is issued for {id} with a target status of Pending
  Then PUT /admin/document-requests/{id}/respond returns HTTP 400
  And ApiResult.Error.Code = "PARTICIPATION_DOCUMENT_REQUEST_STATUS_INVALID"
  And the request stays Pending
  # The CP modal only offers Accept / Reject, so STATUS_INVALID is reachable only via a
  # scripted client or a malformed request — assert at the API layer.
```

**Evidence:** `ParticipationDocumentRequestsTests` (green) — respond with status = Pending returns 400 `PARTICIPATION_DOCUMENT_REQUEST_STATUS_INVALID`.

### E2E-CPDR-004 — List omits email; modal reveals it on open

```gherkin
Scenario: The list contract carries no requester email; the modal reveals it (resolved on read)
  Given a Pending document request whose requester has a known email
  When the administrator opens /admin/document-requests
  Then POST /account/api/admin/document-requests/list returns rows with
       Requester name, Document type and Status — but NO requester email field
  When the administrator clicks the row's Respond (reply ↩ icon) action
  Then the modal opens immediately showing the requester name and "Loading contact details…"
  And GET /account/api/admin/document-requests/{id} fires
  When the detail response (HTTP 200) arrives
  Then it carries the requester email (resolved on read, PII on detail only)
  And the requester email renders under the requester name
  When the administrator clicks "Cancel"
  Then the modal closes
  And NO PUT /respond request fires
  And the row remains Pending
```

**Evidence:** `ParticipationDocumentRequestsTests` (green) — the list row omits the email and GetById returns the resolved-on-read email.

### E2E-CPDR-005 — Status filter; only Pending rows expose Respond

```gherkin
Scenario: Filter by status, and only Pending rows are actionable
  Given the queue contains a mix of Pending, Accepted, and Rejected rows
  When the administrator enters "Pending" into the "Status" column filter
  Then POST /account/api/admin/document-requests/list fires with Filters["status"]="Pending" and Skip=0
  And only Pending rows render, each showing the Respond (reply ↩ icon) action
  When the administrator clears the filter
  Then resolved (Accepted / Rejected) rows render with an empty RowActions cell (no icon)
```

### E2E-CPDR-006 — Permission gate (non-Admin can't open; View-only sees no Respond; API 403)

```gherkin
Scenario: A non-Admin role cannot open the page and cannot respond
  Given a signed-in admin whose role does NOT grant ParticipationDocumentRequests.View
  When they navigate to /admin/document-requests
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/document-requests/list request fires
  # The nav item carries RequiredPermission = ParticipationDocumentRequests.View, so it is hidden from the rail.

Scenario: A View-only admin can read the queue but cannot respond
  Given a signed-in admin whose role grants ParticipationDocumentRequests.View but NOT .Manage
  When they open /admin/document-requests
  Then the page loads and the grid renders (GET /list returns 200 — View is enough)
  And NO Respond (reply ↩ icon) action is shown on any Pending row (AuthorizedAction hides it)
  # Defence-in-depth: even if the PUT is replayed directly (scripted client):
  When the PUT /account/api/admin/document-requests/{id}/respond is issued directly
  Then the API returns HTTP 403
  And the request stays Pending
```

**Evidence:** `ParticipationDocumentRequestsTests` (green) — the admin desk endpoints reject a non-administrator caller with 403.

### E2E-CPDR-007 — Empty state

```gherkin
Scenario: Empty queue renders SimfEmptyState
  Given the database has no ParticipationDocumentRequest rows (or none match the active filter)
  When the administrator opens /admin/document-requests
  Then the grid body renders the SimfEmptyState component (the grid's EmptyTemplate)
  And it shows the bilingual copy "No document requests yet." / "لا توجد طلبات وثائق حتى الآن."
  And the per-column filter inputs remain usable
  And no error toast appears
```

### E2E-CPDR-008 — RTL render

```gherkin
Scenario: Arabic toggle mirrors page + Respond modal
  Given the administrator is on /admin/document-requests in English
  When they switch the language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfDataGrid column headers (Requester / Document type / Status / Submitted / Actions) mirror for RTL
  And the status pills read قيد المراجعة / مقبول / مرفوض
  When they click the Respond (reply ↩ icon) action on a Pending row
  Then the modal title reads the Arabic "الردّ على طلب الوثيقة"
  And the Decision label reads "القرار" with options قبول / رفض
  And the note label reads "ملاحظة الردّ (اختيارية)"
  And the footer buttons read إرسال الردّ and إلغاء, mirrored for RTL
```

---

### E2E-CPDR-009 — Decision notifies the requester (R-2)

```gherkin
Scenario: Accepting or rejecting a document request lands an in-app notification
  Given a visitor has a Pending participation-document request
  When an administrator Accepts (or Rejects) it
  Then the response is HTTP 200
  And a ParticipationDocumentDecided in-app notification is dispatched to the
    requester (best-effort; a dispatch failure never undoes the committed decision)
```

**Evidence captured:**
- API integration test: `ParticipationDocumentRequestsTests.Responding_accept_or_reject_dispatches_a_ParticipationDocumentDecided_notification_to_the_requester`
- Notification kind `ParticipationDocumentDecided` (additive value 52) groups under "Account"

### E2E-CPDR-010 — Duplicate-pending guard (R-4)

```gherkin
Scenario: A second pending request for the same document type is blocked
  Given a visitor already has a Pending request for document type 0
  When they submit another request for document type 0
  Then the API returns HTTP 409 with ErrorCodes.AppRequestDuplicatePending

Scenario: A pending request for a different document type is allowed
  Given a visitor already has a Pending request for document type 0
  When they submit a request for document type 1
  Then the API returns HTTP 200
```

**Evidence captured:**
- API integration tests: `ParticipationDocumentRequestsTests.Submitting_a_second_pending_request_for_the_same_document_type_is_409`, `ParticipationDocumentRequestsTests.Submitting_a_pending_request_for_a_different_document_type_is_allowed`

---

## Implementation notes

- **API integration tests** at
  [`tests/SIMF.Api.Tests/ParticipationDocumentRequestsTests.cs`](../../../tests/SIMF.Api.Tests/ParticipationDocumentRequestsTests.cs)
  cover the same surface at a lower layer: the public submit + validation
  (`PARTICIPATION_DOCUMENT_REQUEST_INVALID`), the admin list (email omitted), the
  GetById detail with the resolved-on-read requester email, the respond
  Accept/Reject path, the Pending → Pending guard
  (`PARTICIPATION_DOCUMENT_REQUEST_STATUS_INVALID` 400), the not-found guard
  (`PARTICIPATION_DOCUMENT_REQUEST_NOT_FOUND`), and the administrator-role gate.
  During the Playwright transition keep both layers; the browser E2E adds the
  modal, filter, toast text, and RTL coverage the API tests cannot assert.
- **Backing surface:**
  - Public submit — `POST /api/v1/app/document-requests`
    (`{ documentType: int (0 = AttendanceCertificate, 1 = ParticipationLetter,
    2 = InvitationLetter), note?: string ≤ 1000 }`; approved-only).
  - Admin desk — `POST /admin/document-requests/list`,
    `GET /admin/document-requests/{id}` (adds the resolved-on-read requester email),
    `PUT /admin/document-requests/{id}/respond` (Accepted / Rejected + optional note
    ≤ 2000).
  - Permissions — `PermissionCatalog.ParticipationDocumentRequests.View` (page +
    list + GetById) / `.Manage` (respond), both baselined `AdminOnly`.
  - Error codes — `PARTICIPATION_DOCUMENT_REQUEST_INVALID` (400),
    `PARTICIPATION_DOCUMENT_REQUEST_NOT_FOUND` (404),
    `PARTICIPATION_DOCUMENT_REQUEST_STATUS_INVALID` (400, respond with status =
    Pending).
  - Status — `MeetingRequestStatus` = Pending / Accepted / Rejected / Cancelled
    (Cancelled added in D-500). A respond only sets Accepted / Rejected.
- **Mirror.** This desk is modelled on `/admin/speaker-meeting-requests`
  ([`cp-admin-speaker-meeting-requests.md`](cp-admin-speaker-meeting-requests.md)) —
  same SimfDataGrid + Respond-modal pattern, same list-omits-email PII rule. The
  sibling new desk is [`cp-badge-requests.md`](cp-badge-requests.md); the app side
  is [`mobile-requests.md`](mobile-requests.md).
- **Seeding a Pending row for the E2E run.** The CP cannot create a document
  request — submit one via the public endpoint
  `POST /api/v1/app/document-requests` as an approved visitor, or insert directly
  into the `ParticipationDocumentRequests` table (Status = Pending) for fixture
  setup.

---

_Last reviewed:_ `2026-06-26` by `SIMF Team` — D-500 Wave 5 (الطلبات): participation
document requests review desk (mirrors `/admin/speaker-meeting-requests`).
