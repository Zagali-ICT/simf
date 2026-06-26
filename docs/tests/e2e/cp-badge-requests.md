# E2E test catalogue — Badge update requests desk (`/admin/badge-requests`)

| | |
|--|--|
| **Page** | [`cp/badge-requests.md`](../../pages/cp/badge-requests.md) |
| **Route** | `/admin/badge-requests` (`BadgeRequestsList.razor`) |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-26 (D-500 Wave 5) |

> **Decision:** D-500 — the **badge update request**. An approved attendee on the
> **Requests** screen (app `/requests`, الطلبات, Figma `1408:9726`) asks for a
> corrected job title on their badge (`POST /app/badge-requests`, `requestedJobTitle`
> 1–128 required + optional note). Those requests land here as `Pending` rows; the
> administrator reviews each and **responds** (Accept or Reject) with an optional
> note. **On Accept the requested title is applied to the user's profile `JobTitle`.**
> This is a **NEW additive `BadgeUpdateRequests` table** (migration `D500`).

> **Page permission:** `@attribute [RequirePermission(PermissionCatalog.BadgeUpdateRequests.View)]`
> (`"BadgeUpdateRequests.View"`). The **list** + **GetById** API endpoints enforce
> `BadgeUpdateRequests.View`; the **respond** endpoint enforces
> `BadgeUpdateRequests.Manage`. Both are baselined `AdminOnly` and seeded idempotently.
> The per-row **Respond** action is a quiet reply (↩) icon inside the grid's
> `<RowActions>`, wrapped in `<AuthorizedAction Permission="BadgeUpdateRequests.Manage">`
> — a `View`-only admin therefore does **not** see the icon at all. The API still
> independently enforces `Manage` on the PUT `/respond` as defence-in-depth. All three
> endpoints also require `RequireApprovedAccount`. This desk **mirrors**
> `/admin/speaker-meeting-requests`.

> **CP is response-only — no create / edit / delete.** Badge update requests are
> *submitted* by the authenticated audience from the Requests screen
> (`POST /api/v1/app/badge-requests`). This CP page is a **review queue** rendered
> with the owner-mandated **SimfDataGrid** (server-paged, per-column filter + sort,
> full pager). Filter by status, then on a **Pending** row click the **Respond**
> (reply icon) action to Accept or Reject with an optional note (≤ 2000). On
> **Accept** the `requestedJobTitle` is written to the user's profile `JobTitle`.
> Resolved rows show no action icon. The golden path is **list → Respond (Accept) →
> row flips to Accepted + the user's JobTitle updates**.

> **PII / audit note.** The list response carries the **requester name + requested
> job title** but **not** the requester email; the email is resolved on read and
> revealed only when the respond/detail modal opens, via
> `GET /admin/badge-requests/{id}` (PII on detail only).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-CPBR-001 | Golden path — list queue → Respond (Accept) + note → row flips to Accepted + user's profile JobTitle updates | happy | P0 | authored ✓ (`BadgeUpdateRequestsTests`, API) |
| E2E-CPBR-002 | Respond (Reject) with a response note → row flips to Rejected; profile JobTitle unchanged | happy | P1 | authored ✓ (`BadgeUpdateRequestsTests`, API) |
| E2E-CPBR-003 | Pending → Pending respond → 400 `BADGE_UPDATE_REQUEST_STATUS_INVALID` | error | P1 | authored ✓ (`BadgeUpdateRequestsTests`, API) |
| E2E-CPBR-004 | List omits requester email; detail/respond modal reveals it (resolved on read) on open | happy | P0 | authored ✓ (`BadgeUpdateRequestsTests`, API) |
| E2E-CPBR-005 | Filter by status narrows the grid; only Pending rows show the Respond icon | happy | P1 | _to author_ |
| E2E-CPBR-006 | Permission gate — a non-Admin role can't open the page (→ `/not-permitted`); `View`-only sees no Respond icon; PUT `/respond` → 403 | auth | P0 | authored ✓ (`BadgeUpdateRequestsTests`, API — admin-role gate) |
| E2E-CPBR-007 | Empty state renders `SimfEmptyState` ("No badge requests yet.") | empty | P1 | _to author_ |
| E2E-CPBR-008 | RTL render: Arabic toggle mirrors page + Respond modal | i18n | P1 | _to author_ |

## Scenarios

### E2E-CPBR-001 — Golden path (list → Respond Accept → JobTitle applied)

```gherkin
Feature: Badge update requests desk — respond happy path
  As an Administrator with BadgeUpdateRequests.Manage
  I want to accept a pending badge update request
  So that the requester's badge job title is corrected

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp using the Get-Totp helper
  And an approved Visitor whose profile JobTitle is "Engineer" has submitted a badge update request from the app
      (POST /api/v1/app/badge-requests with requestedJobTitle "Lead Naval Architect"), so one row is Pending
  And they have landed on /admin/badge-requests

Scenario: Accept a pending badge update request
  Given the grid is showing rows of every status (no filter applied)
  Then POST /account/api/admin/badge-requests/list fires with Skip=0
  And each row shows the columns: Requester, Requested job title, Status, Submitted (UTC), and a quiet Actions column
  And the Pending row shows the amber "Pending" pill and the requested title "Lead Naval Architect"

  When the administrator clicks the row's Respond (reply ↩ icon) action on the first Pending row
  Then GET /account/api/admin/badge-requests/{id} fires (the detail fetch)
  And the "Respond to badge request" modal opens
  And it shows the requester (with "Loading contact details…" then the requester email), the requested job title, and the note
  And the "Decision" select defaults to "Accept"

  When the administrator leaves the Decision on "Accept"
  And types "Approved — title corrected on the badge." (≤ 2000 chars) into the Response note
  And clicks "Send response"
  Then PUT /account/api/admin/badge-requests/{id}/respond fires with Status=Accepted and the note text
  And the API returns HTTP 200
  And the modal closes
  And a green toast reads "Response sent." / "تم إرسال الردّ."
  And the requester's profile JobTitle is now "Lead Naval Architect"
  When the administrator changes the "Status" column filter to "Accepted"
  Then the row appears with the green "Accepted" pill and no Respond icon
```

**Evidence:** `BadgeUpdateRequestsTests` (green) — covers the public submit → Pending row, the respond-Accepted transition, and the applied profile `JobTitle` at the API layer.

### E2E-CPBR-002 — Respond (Reject) with a note

```gherkin
Scenario: Reject a pending badge update request with a response note
  Given a Pending badge update request is visible, and the requester's JobTitle is "Engineer"
  When the administrator clicks the row's Respond (reply ↩ icon) action on that row
  And the modal opens
  And they change the "Decision" select to "Reject"
  And they type "The requested title does not match our records." into the Response note
  And they click "Send response"
  Then PUT /admin/badge-requests/{id}/respond fires with Status=Rejected
  And the API returns HTTP 200
  And a green toast reads "Response sent." / "تم إرسال الردّ."
  And the requester's profile JobTitle is still "Engineer" (Reject does not apply the title)
  And under the "Status" column filter set to "Rejected" the row shows the grey "Rejected" pill
```

**Evidence:** `BadgeUpdateRequestsTests` (green) — the respond-Rejected path leaves the profile unchanged.

### E2E-CPBR-003 — Pending → Pending respond returns 400

```gherkin
Scenario: A respond that does not move the request out of Pending is rejected
  Given a Pending badge update request {id}
  When a respond is issued for {id} with a target status of Pending
  Then PUT /admin/badge-requests/{id}/respond returns HTTP 400
  And ApiResult.Error.Code = "BADGE_UPDATE_REQUEST_STATUS_INVALID"
  And the request stays Pending
  # The CP modal only offers Accept / Reject, so STATUS_INVALID is reachable only via a
  # scripted client or a malformed request — assert at the API layer.
```

**Evidence:** `BadgeUpdateRequestsTests` (green) — respond with status = Pending returns 400 `BADGE_UPDATE_REQUEST_STATUS_INVALID`.

### E2E-CPBR-004 — List omits email; modal reveals it on open

```gherkin
Scenario: The list contract carries no requester email; the modal reveals it (resolved on read)
  Given a Pending badge update request whose requester has a known email
  When the administrator opens /admin/badge-requests
  Then POST /account/api/admin/badge-requests/list returns rows with
       Requester name, Requested job title and Status — but NO requester email field
  When the administrator clicks the row's Respond (reply ↩ icon) action
  Then the modal opens immediately showing the requester name and "Loading contact details…"
  And GET /account/api/admin/badge-requests/{id} fires
  When the detail response (HTTP 200) arrives
  Then it carries the requester email (resolved on read, PII on detail only)
  And the requester email renders under the requester name
  When the administrator clicks "Cancel"
  Then the modal closes
  And NO PUT /respond request fires
  And the row remains Pending
```

**Evidence:** `BadgeUpdateRequestsTests` (green) — the list row omits the email and GetById returns the resolved-on-read email.

### E2E-CPBR-005 — Status filter; only Pending rows expose Respond

```gherkin
Scenario: Filter by status, and only Pending rows are actionable
  Given the queue contains a mix of Pending, Accepted, and Rejected rows
  When the administrator enters "Pending" into the "Status" column filter
  Then POST /account/api/admin/badge-requests/list fires with Filters["status"]="Pending" and Skip=0
  And only Pending rows render, each showing the Respond (reply ↩ icon) action
  When the administrator clears the filter
  Then resolved (Accepted / Rejected) rows render with an empty RowActions cell (no icon)
```

### E2E-CPBR-006 — Permission gate (non-Admin can't open; View-only sees no Respond; API 403)

```gherkin
Scenario: A non-Admin role cannot open the page and cannot respond
  Given a signed-in admin whose role does NOT grant BadgeUpdateRequests.View
  When they navigate to /admin/badge-requests
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/badge-requests/list request fires
  # The nav item carries RequiredPermission = BadgeUpdateRequests.View, so it is hidden from the rail.

Scenario: A View-only admin can read the queue but cannot respond
  Given a signed-in admin whose role grants BadgeUpdateRequests.View but NOT .Manage
  When they open /admin/badge-requests
  Then the page loads and the grid renders (GET /list returns 200 — View is enough)
  And NO Respond (reply ↩ icon) action is shown on any Pending row (AuthorizedAction hides it)
  # Defence-in-depth: even if the PUT is replayed directly (scripted client):
  When the PUT /account/api/admin/badge-requests/{id}/respond is issued directly
  Then the API returns HTTP 403
  And the request stays Pending
  And the requester's profile JobTitle is unchanged
```

**Evidence:** `BadgeUpdateRequestsTests` (green) — the admin desk endpoints reject a non-administrator caller with 403.

### E2E-CPBR-007 — Empty state

```gherkin
Scenario: Empty queue renders SimfEmptyState
  Given the database has no BadgeUpdateRequest rows (or none match the active filter)
  When the administrator opens /admin/badge-requests
  Then the grid body renders the SimfEmptyState component (the grid's EmptyTemplate)
  And it shows the bilingual copy "No badge requests yet." / "لا توجد طلبات تعديل الشارة حتى الآن."
  And the per-column filter inputs remain usable
  And no error toast appears
```

### E2E-CPBR-008 — RTL render

```gherkin
Scenario: Arabic toggle mirrors page + Respond modal
  Given the administrator is on /admin/badge-requests in English
  When they switch the language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfDataGrid column headers (Requester / Requested job title / Status / Submitted / Actions) mirror for RTL
  And the status pills read قيد المراجعة / مقبول / مرفوض
  When they click the Respond (reply ↩ icon) action on a Pending row
  Then the modal title reads the Arabic "الردّ على طلب تعديل الشارة"
  And the Decision label reads "القرار" with options قبول / رفض
  And the note label reads "ملاحظة الردّ (اختيارية)"
  And the footer buttons read إرسال الردّ and إلغاء, mirrored for RTL
```

---

## Implementation notes

- **API integration tests** at
  [`tests/SIMF.Api.Tests/BadgeUpdateRequestsTests.cs`](../../../tests/SIMF.Api.Tests/BadgeUpdateRequestsTests.cs)
  cover the same surface at a lower layer: the public submit + validation
  (`BADGE_UPDATE_REQUEST_INVALID`, including the required `requestedJobTitle`
  1–128), the admin list (email omitted), the GetById detail with the
  resolved-on-read requester email, the respond Accept (which applies the title to
  the user's profile `JobTitle`) / Reject path, the Pending → Pending guard
  (`BADGE_UPDATE_REQUEST_STATUS_INVALID` 400), the not-found guard
  (`BADGE_UPDATE_REQUEST_NOT_FOUND`), and the administrator-role gate. During the
  Playwright transition keep both layers; the browser E2E adds the modal, filter,
  toast text, and RTL coverage the API tests cannot assert.
- **Backing surface:**
  - Public submit — `POST /api/v1/app/badge-requests`
    (`{ requestedJobTitle: string 1–128 (required), note?: string ≤ 1000 }`;
    approved-only). On admin **Accept** the title is applied to the user's profile
    `JobTitle`.
  - Admin desk — `POST /admin/badge-requests/list`,
    `GET /admin/badge-requests/{id}` (adds the resolved-on-read requester email),
    `PUT /admin/badge-requests/{id}/respond` (Accepted / Rejected + optional note
    ≤ 2000).
  - Permissions — `PermissionCatalog.BadgeUpdateRequests.View` (page + list +
    GetById) / `.Manage` (respond), both baselined `AdminOnly`.
  - Error codes — `BADGE_UPDATE_REQUEST_INVALID` (400),
    `BADGE_UPDATE_REQUEST_NOT_FOUND` (404),
    `BADGE_UPDATE_REQUEST_STATUS_INVALID` (400, respond with status = Pending).
  - Status — `MeetingRequestStatus` = Pending / Accepted / Rejected / Cancelled
    (Cancelled added in D-500). A respond only sets Accepted / Rejected.
- **Mirror.** This desk is modelled on `/admin/speaker-meeting-requests`
  ([`cp-admin-speaker-meeting-requests.md`](cp-admin-speaker-meeting-requests.md)) —
  same SimfDataGrid + Respond-modal pattern, same list-omits-email PII rule. The
  sibling new desk is [`cp-document-requests.md`](cp-document-requests.md); the app
  side is [`mobile-requests.md`](mobile-requests.md).
- **Seeding a Pending row for the E2E run.** The CP cannot create a badge update
  request — submit one via the public endpoint `POST /api/v1/app/badge-requests`
  as an approved visitor, or insert directly into the `BadgeUpdateRequests` table
  (Status = Pending) for fixture setup.

---

_Last reviewed:_ `2026-06-26` by `SIMF Team` — D-500 Wave 5 (الطلبات): badge update
requests review desk (mirrors `/admin/speaker-meeting-requests`; Accept applies the
requested title to the user's profile JobTitle).
