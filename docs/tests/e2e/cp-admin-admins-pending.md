# E2E test catalogue — Pending admins (`/admin/admins/pending`)

| | |
|--|--|
| **Page** | [`cp/admin-admins-pending.md`](../../pages/cp/admin-admins-pending.md) |
| **Route** | `/admin/admins/pending` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-03 |

> **Page-required permission:** `PermissionCatalog.Admins.View`
> (the page carries `@attribute [RequirePermission(PermissionCatalog.Admins.View)]`).
> The row Approve action is gated server-side by `Admins.Approve`; the row
> Reject action and the bulk-reject by `Admins.Reject`; bulk-approve by
> `Admins.Approve`. The list endpoint is gated by `Admins.View`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-APN-001 | Golden path — approve one pending admin → row vanishes, green toast, account Approved + QR minted | happy | P0 | _to author_ |
| E2E-APN-002 | Reject one pending admin with a reason → row vanishes, audited | happy | P0 | _to author_ |
| E2E-APN-003 | Reject reason < 10 chars → Submit stays disabled, no POST fires | error | P1 | _to author_ |
| E2E-APN-004 | Reject reason > 500 chars → textarea caps at MaxLength, Submit stays disabled | error | P2 | _to author_ |
| E2E-APN-005 | Bulk approve — multiselect → "Approve selected" → counts toast | happy | P1 | _to author_ |
| E2E-APN-006 | Bulk reject — multiselect → shared-reason modal → counts toast | happy | P1 | _to author_ |
| E2E-APN-007 | Bulk action with nothing selected → no request fires | error | P2 | _to author_ |
| E2E-APN-008 | Pager — First / Last / Prev / Next / page-size round-trip | happy | P2 | _to author_ |
| E2E-APN-009 | Empty queue renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-APN-010 | Auth gate — admin lacking `Admins.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-APN-011 | Conflict — approve an already-approved row → 409 `AdminUserNotPending` | error | P1 | _to author_ |
| E2E-APN-012 | Server 500 on `/pending/list` → degrades to empty state, no rows | resilience | P2 | _to author_ |
| E2E-APN-013 | RTL / Arabic render mirrors page + reject modal | i18n | P1 | _to author_ |
| E2E-APN-014 | Per-column filter narrows the grid (email / Display name) | happy | P1 | _to author_ |
| E2E-APN-015 | Column sort toggles (Email / Display name) | happy | P2 | _to author_ |
| E2E-APN-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-APN-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-APN-001 — Golden path (approve one pending admin)

```gherkin
Feature: Pending-admin approval golden path
  As an Administrator
  I want to approve a self-registered admin candidate
  So that the new admin can sign in to the Control Panel with their RBAC roles

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in as superadmin@zagali-ict.com via /login + /login/totp
    using the TOTP value produced by the PowerShell Get-Totp helper
  And at least one admin account exists in AccountState=PendingApproval
    (e.g. seed candidate "pending.admin@zagali-ict.com" / "Pending Admin")
  And they have landed on /admin/admins/pending

Scenario: Approve a pending admin
  Given the grid shows {N} pending rows
  And the SimfBanner title reads "Pending staff approvals"
  And the supporting line reads "Approval mints the QR badge and unlocks sign-in (CP for staff, event entry for visitors). Rejection records a reason for audit."
  And the grid columns are Email, Display name, Created
  When the administrator clicks "Approve" on the row for "pending.admin@zagali-ict.com"
  Then a POST /account/api/admin/admins/{id}/approve fires and returns 200 with ApiResult.Success=true
  And a green toast (SimfAlert variant="success") reads "Approved pending.admin@zagali-ict.com."
  And the list reloads (POST /account/api/admin/admins/pending/list returns 200)
  And the grid shows {N - 1} rows
  And the approved row no longer appears in the pending queue
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-admins-pending-001-before.png`
- Screenshot after: `docs/screenshots/cp-admin-admins-pending-001-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/admins/...` call returns 200
- Audit row: `OperationLog` row with `Event = 'Admin.StaffApproved'`, `Outcome = Success`,
  the actor's id (superadmin), the subject's id/email, and `Detail` = the minted QR id.
- Side effects: subject `AccountState` flips to `Approved`, a QR id is minted on its
  `UserProfile`, the subject's refresh tokens are revoked, and an `AccountApproved`
  notification + email is dispatched to the subject.

### E2E-APN-002 — Reject one pending admin with a reason

```gherkin
Scenario: Reject a pending admin via the reason modal
  Given the administrator is on /admin/admins/pending
  And a pending row exists for "reject.me@zagali-ict.com"
  When the administrator clicks "Reject" on that row
  Then the reject modal opens titled "Reject account"
  And the body reads "Reject reject.me@zagali-ict.com? This sets the account to Rejected and writes an audit row."
  And a "Reason" textarea is shown with the helper "Between 10 and 500 characters. Shown to operators in the audit log."
  And the modal "Reject" submit button is disabled (reason is empty)
  When the administrator types Reason="Duplicate of an existing staff account; please use the original."
  Then the "Reject" submit button becomes enabled
  When they click "Reject"
  Then a POST /account/api/admin/admins/{id}/reject fires with body { Reason: "Duplicate of an existing staff account; please use the original." } and returns 200
  And the modal closes
  And a green toast reads "Rejected reject.me@zagali-ict.com."
  And the list reloads and the row no longer appears
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-admins-pending-002-reject-modal.png`
- Audit row: `OperationLog` row with `Event = 'Admin.StaffRejected'`, `Outcome = Success`,
  actor + subject ids, and `Detail` = the reason text.
- Side effects: subject `AccountState` → `Rejected`, `UserProfile.RejectionReason`
  (+ Arabic mirror) set, refresh tokens revoked, `AccountRejected` notification + email sent.

### E2E-APN-003 — Reject reason too short

```gherkin
Scenario: Reason shorter than 10 chars keeps Submit disabled
  Given the reject modal is open for a pending admin
  When the administrator types Reason="too short"
  Then the modal "Reject" submit button stays disabled (Value.Length is < 10)
  And no /account/api/admin/admins/{id}/reject request fires
  When they extend Reason to "valid ten plus chars reason"
  Then the "Reject" submit button becomes enabled
```

### E2E-APN-004 — Reject reason too long

```gherkin
Scenario: Reason longer than 500 chars is capped and blocks Submit
  Given the reject modal is open for a pending admin
  When the administrator attempts to paste a 600-character reason
  Then the SimfTextarea MaxLength="500" caps the input at 500 characters
  And the Submit button is enabled at exactly 500 chars
  # The client guard is Disabled="@(_rejectReason.Length is < 10 or > 500)"; the
  # API validator (AdminBulkRejectRequestValidator pattern, 10-500) is the
  # server-side backstop for any request that bypasses the UI cap.
```

### E2E-APN-005 — Bulk approve selected

```gherkin
Scenario: Multiselect then "Approve selected"
  Given the administrator is on /admin/admins/pending
  And the grid shows at least 3 pending rows
  When they tick the "Select all" header checkbox (or two row checkboxes)
  And they click "Approve selected"
  Then a POST /account/api/admin/admins/bulk-approve fires with body { Ids: [<selected guids>] } and returns 200
  And the response carries AdminBulkApprovalResponse { Approved, Skipped, Failures }
  And a toast reads "Approved 2 user(s). Skipped 0." with variant="success" (green) when Skipped == 0
  And the toast variant is "warning" (amber) when Skipped > 0
  And the list reloads and the approved rows disappear
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-admins-pending-005-bulk-approve.png`
- Audit rows: one `OperationLog` row with `Event = 'Admin.StaffApproved'` **per approved subject**
  (per-subject auditing — a batch action still has per-user visibility).
- Network: `/account/api/admin/admins/bulk-approve` returns 200 (max 500 ids per request).

### E2E-APN-006 — Bulk reject selected (shared reason)

```gherkin
Scenario: Multiselect then "Reject selected" via shared-reason modal
  Given the administrator is on /admin/admins/pending
  And two pending rows are selected
  When they click "Reject selected"
  Then the bulk-reject modal opens titled "Reject selected accounts"
  And the body reads "You are about to reject 2 pending account(s). The reason below is recorded and shown to each user."
  And the modal "Reject selected" submit button is disabled while the reason is < 10 chars
  When they type Reason="Open registration was closed; re-apply through the official channel."
  And they click "Reject selected"
  Then a POST /account/api/admin/admins/bulk-reject fires with body { Ids: [<guids>], Reason: "<reason>" } and returns 200
  And the response carries AdminBulkRejectResponse { Rejected, Skipped, Failures }
  And a toast reads "Rejected 2 user(s). Skipped 0." (green when Skipped == 0, amber otherwise)
  And the modal closes and the list reloads
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-admins-pending-006-bulk-reject-modal.png`
- Audit rows: one `Admin.StaffRejected` row per rejected subject, `Detail` = the shared reason.

### E2E-APN-007 — Bulk action with nothing selected

```gherkin
Scenario: A bulk action with an empty selection is a no-op
  Given the administrator is on /admin/admins/pending
  And no rows are selected
  When they trigger "Approve selected"
  Then the OnBulkApproveAsync guard (selected.Count == 0) returns early
  And no /account/api/admin/admins/bulk-approve request fires
  When they trigger "Reject selected"
  Then the OnBulkRejectAsync guard (selected.Count == 0) returns early
  And the bulk-reject modal does not open
  # Server backstop: an empty Ids array is rejected with HTTP 400,
  # ApiResult.Error.Code = "AdminBulkActionInvalid",
  # "At least one user id is required." / "يجب تحديد مستخدم واحد على الأقل."
```

### E2E-APN-008 — Pager round-trip

```gherkin
Scenario: Pager controls page through the queue
  Given the queue has more than one page of pending admins (default page size 20)
  When the administrator changes the page-size control ("Show")
  Then a POST /account/api/admin/admins/pending/list fires with the new Top and returns 200
  When they click "Next page" / "Last page" / "First page" / "Previous"
  Then each click fires a /pending/list POST with the right Skip and returns 200
  And the summary reads "{from}-{to} of {total}"
  And the page indicator reads "Page {current} of {total}"
```

### E2E-APN-009 — Empty queue

```gherkin
Scenario: Empty queue renders SimfEmptyState
  Given there are no admin accounts in AccountState=PendingApproval
  When the administrator opens /admin/admins/pending
  Then POST /account/api/admin/admins/pending/list returns 200 with an empty page
  And the grid body renders the SimfEmptyState component
  And it shows the bilingual copy "No accounts are waiting for approval." / "لا توجد حسابات بانتظار الموافقة."
  And no error toast appears
```

### E2E-APN-010 — Auth gate (admin lacking the permission)

```gherkin
Scenario: A signed-in admin without Admins.View is denied
  Given a signed-in admin whose roles do NOT include the Admins.View permission
    (and who is not the wildcard Administrator "*")
  When they navigate to /admin/admins/pending
  Then the [RequirePermission(PermissionCatalog.Admins.View)] attribute denies the page
  And they land on /not-permitted with HTTP 200
  And no /account/api/admin/admins/pending/list request fires
  # Defense in depth: even if the BFF proxy were reached, the API list endpoint is
  # itself policy-gated on Admins.View and would return 403.
```

### E2E-APN-011 — Conflict (approve an already-approved row)

```gherkin
Scenario: Approving a non-pending account returns 409
  Given a row that was Approved in another tab is still visible in this stale grid
  When the administrator clicks "Approve" on that stale row
  Then POST /account/api/admin/admins/{id}/approve returns HTTP 409
  And ApiResult.Success=false with Error.Code = "AdminUserNotPending"
  And a red toast (variant="error") surfaces the bilingual MessageForCurrentCulture():
    "The target account is not pending approval." / "الحساب المستهدف ليس في انتظار الموافقة."
  And the list reload (the next /pending/list) drops the stale row
  # A wrong-type id (e.g. a visitor id sent to the admins approve URL) returns
  # HTTP 404 with Error.Code = "AdminUserNotFound".
```

### E2E-APN-012 — Server 500 on the list

```gherkin
Scenario: API 500 on /pending/list degrades to the empty state
  Given the API is configured to return 500 on /admin/admins/pending/list (e.g. DB down)
  When the administrator opens /admin/admins/pending
  Then the grid shows the loading indicator first
  And because the envelope is not { Success: true, Data: not null }, the page falls back
    to an empty GridPage (GridPage.Of(empty, 0, query))
  And the SimfEmptyState renders with no rows
  # The list path has no dedicated error toast — it degrades to the empty state. A failed
  # approve/reject action, by contrast, shows the red fallback toast (Admin.Users.Fallback)
  # when MessageForCurrentCulture() is unavailable.
```

### E2E-APN-013 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the reject modal
  Given the administrator is on /admin/admins/pending in English
  When they switch the UI language to العربية from the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "موافقات الفريق المعلّقة"
  And the supporting line, columns, and row buttons render Arabic ("موافقة" / "رفض")
  And the nav rail and pager arrows mirror
  When they click "رفض" on a pending row
  Then the reject modal opens RTL titled "رفض الحساب"
  And the reason label reads "السبب" with helper "بين 10 و500 حرف. يظهر للمشغّلين في سجل التدقيق."
  And the footer actions appear in reverse order
```

### E2E-APN-014 — Per-column filter narrows the grid

```gherkin
Scenario: Typing in a per-column filter input refetches the page
  Given the administrator is on /admin/admins/pending
  And the queue has at least 12 pending admins so a filter visibly narrows the grid
  And the SimfDataGrid shows a filter row under the header with a search input
    under the "Email" column and the "Display name" column
    (only the email + displayName columns carry Filterable="true"; "Created" has no filter input)
  When the administrator types "pending.admin" into the input labelled "Filter column Email"
  Then after the 300 ms debounce a POST /account/api/admin/admins/pending/list fires
  And the request body carries GridQuery.Filters["email"]="pending.admin" with Skip reset to 0
  And the grid reloads showing only rows whose Email contains "pending.admin"
    (backend applies EF.Functions.Like(Email, "%pending.admin%"))
  When the administrator clears the Email input and types "Pending" into the input
    labelled "Filter column Display name"
  Then a POST /pending/list fires with GridQuery.Filters["displayName"]="Pending" and Skip=0
  And the grid reloads showing only rows whose Display name contains "Pending"
  And the pager summary recomputes to "{from}-{to} of {filtered-total}"
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-admins-pending-014-column-filter.png`
- Network: each keystroke after the debounce fires exactly one `/pending/list` POST;
  rapid typing collapses to a single request (the grid debounces 300 ms and cancels
  the prior token).
- Note: clearing the input removes the key from `Filters` (the grid drops a
  whitespace-only value), so the next `/pending/list` carries no `email`/`displayName`
  filter and the full queue returns.

### E2E-APN-015 — Column sort toggles

```gherkin
Scenario: Clicking a sortable header toggles ascending / descending
  Given the administrator is on /admin/admins/pending
  And the queue has more than one page of pending admins
  And the "Email" and "Display name" headers are sortable (render a sort button + arrow);
    the "Created" header is plain text (Created carries no Sortable flag)
  When the administrator clicks the "Email" column header
  Then a POST /account/api/admin/admins/pending/list fires with
    GridQuery.Sort="email", SortDescending=false and Skip reset to 0
  And the grid reorders ascending by Email and the header arrow shows ▲ (aria-sort="ascending")
  When the administrator clicks the "Email" header again
  Then a POST /pending/list fires with Sort="email", SortDescending=true
  And the grid reorders descending and the arrow shows ▼ (aria-sort="descending")
  When the administrator clicks the "Display name" header
  Then a POST /pending/list fires with Sort="displayName", SortDescending=false
  And sorting switches to Display name ascending (Email returns to the neutral ↕ arrow)
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-admins-pending-015-column-sort.png`
- Note: the backend honours `email` / `displayName` sort keys (case-insensitive);
  any other key falls back to the natural newest-first order. A sort change also
  resets `Skip` to 0 and clears the current selection.

---

## Implementation notes

- **One-click Approve (no review modal).** Unlike the Pending Visitors / Pending
  Others queues (which carry the D-128 review-before-approve modal), `PendingStaff.razor`
  approves in a single click — there is **no** View / profile-preview / confirm step on
  this page. This is a known parity gap logged in the page reference doc; do not author a
  "confirm approval" scenario for this page.
- **Two reject modals, one reason rule.** The single-row reject and the bulk reject share
  the 10–500 char reason rule. The client disables Submit via
  `Disabled="@(_rejectReason.Length is < 10 or > 500)"`; the API backstop is
  `AdminBulkRejectRequestValidator` (Ids 1–500, Reason 10–500). The single reject body is
  `AdminRejectRequest { Reason }`; the bulk bodies are `AdminBulkApprovalRequest { Ids }`
  and `AdminBulkRejectRequest { Ids, Reason }`.
- **Manual smoke is canonical today.** Until a Playwright project exists, the canonical
  "run" is a Chrome DevTools MCP session driven per the steps above, capturing screenshots
  into `docs/screenshots/cp-admin-admins-pending-*.png`. The Gherkin shape is runner-agnostic
  and copies straight into a `.feature` file under `tests/SIMF.E2E.Tests/` when adopted.
- **API integration tests at a lower layer** cover the same surface without a browser:
  - `tests/SIMF.Api.Tests/AdminApprovalTests.cs` — single approve / reject
    (`Admin.StaffApproved` / `Admin.StaffRejected` audit + the `AdminUserNotPending` 409 /
    `AdminUserNotFound` 404 paths).
  - `tests/SIMF.Api.Tests/AdminBulkAdminTests.cs` — `bulk-approve` / `bulk-reject` for the
    admin queue (per-subject counts + the `AdminBulkActionInvalid` empty-ids 400).
  - `tests/SIMF.Api.Tests/AdminBulkApprovalTests.cs` + `AdminBulkRejectTests.cs` — the shared
    bulk worker behaviour (Approved/Skipped/Failures, reason validation).
  - `tests/SIMF.Api.Tests/AdminGatesTests.cs` — the permission gates on the admin endpoints.
- **Permission gating reference.** Page: `Admins.View`. Actions — approve: `Admins.Approve`;
  reject + bulk-reject: `Admins.Reject`; bulk-approve: `Admins.Approve`. All endpoints also
  require the `RequireApprovedAccount` policy.

---

_Last reviewed:_ 2026-06-03 by Claude (E2E catalogue rebuild) (D-256/D-257 grid affordances reconciled).
