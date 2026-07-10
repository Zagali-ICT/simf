# E2E test catalogue — Pending Others approval queue (`/admin/others/pending`)

| | |
|--|--|
| **Page** | [`cp/admin-others-pending.md`](../../pages/cp/admin-others-pending.md) |
| **Route** | `/admin/others/pending` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-03 |

> **Page permission:** `@attribute [RequirePermission(PermissionCatalog.Others.View)]`.
> The `CpNavigation` item `Module.AdminOthersPending` sets the same `RequiredPermission: PermissionCatalog.Others.View`.
> Approve / reject actions are enforced **server-side** on their own codes — see the
> permission map at the bottom of §Coverage matrix. The page does **not** wrap the
> Approve/Reject buttons in `<AuthorizedAction>`; the gate is the API policy, so the
> cross-permission scenario below asserts at the network layer.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-OPN-001 | Golden path — load queue → View → Approve-with-review → row vanishes + QR minted | happy | P0 | _to author_ |
| E2E-OPN-002 | View modal (read-only) renders the full pending profile + ID-document image | happy | P1 | _to author_ |
| E2E-OPN-003 | Single Reject with a 10–500 char reason → audited + row vanishes | happy | P0 | _to author_ |
| E2E-OPN-004 | Bulk **Approve selected** (multiselect → `/others/bulk-approve`) | happy | P1 | _to author_ |
| E2E-OPN-005 | Bulk **Reject selected** (shared-reason modal → `/others/bulk-reject`) | happy | P1 | _to author_ |
| E2E-OPN-006 | Pager + filter (first/prev/next/last/page-size, search box) | happy | P2 | _to author_ |
| E2E-OPN-007 | Empty queue renders `SimfEmptyState` ("No accounts are waiting for approval.") | happy | P1 | _to author_ |
| E2E-OPN-008 | Auth gate — signed-in admin lacking `Others.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-OPN-009 | Reject validation — reason < 10 chars keeps Submit disabled, no POST fires | error | P1 | _to author_ |
| E2E-OPN-010 | Conflict — Approve an account already approved in another tab → error toast | error | P1 | _to author_ |
| E2E-OPN-011 | Cross-kind 404 — a Visitor id on `/others/{id}/profile-for-approval` → 404 + fallback | error | P1 | _to author_ |
| E2E-OPN-012 | Server 500 on `/others/pending/list` → empty grid, no crash | resilience | P2 | _to author_ |
| E2E-OPN-013 | Bulk partial — bulk-approve over a mix of valid + stale ids → "Approved N. Skipped M." warning | resilience | P2 | _to author_ |
| E2E-OPN-014 | RTL / Arabic render — page + reject modal mirror to RTL | i18n | P1 | _to author_ |
| E2E-OPN-015 | Per-column filter narrows the grid (`Filters["email"]` / `Filters["displayName"]`, Skip reset, debounced) | happy | P1 | _to author_ |
| E2E-OPN-016 | Column sort toggles (`Sort`/`SortDescending` on email + displayName; Created not sortable) | happy | P2 | _to author_ |

**Server-side permission map (asserted by E2E-OPN-008 / the `PermissionEnforcementTests`):**

| Action | BFF route | API policy code |
|--------|-----------|-----------------|
| Load queue | `POST /account/api/admin/others/pending/list` | `Others.View` (page gate) |
| View profile | `GET /account/api/admin/others/{id}/profile-for-approval` | `Others.View` |
| Approve | `POST /account/api/admin/others/{id}/approve` | `Admins.Approve` |
| Reject | `POST /account/api/admin/others/{id}/reject` | `Admins.Reject` |
| Bulk approve | `POST /account/api/admin/others/bulk-approve` | `Others.Approve` |
| Bulk reject | `POST /account/api/admin/others/bulk-reject` | `Others.Reject` |

## Scenarios

### E2E-OPN-001 — Golden path (View → Approve-with-review)

```gherkin
Feature: Pending Others approval queue — approve flow
  As an Administrator on the partner-approvals desk
  I want to review an Other-typed application and approve it
  So that the partner rep can sign in and a QR badge is minted

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp using the Get-Totp helper
  And at least one Other-typed account sits in PendingApproval
  And they have landed on /admin/others/pending

Scenario: Review then approve one pending Other
  Given the SimfBanner reads "Pending Other approvals"
  And the grid lists Other-typed pending rows with columns Email, Display name, Created
  And the grid currently shows {N} rows
  When the administrator clicks "Approve" on the row for "press@maritime-news.example"
  Then a "Review and approve — press@maritime-news.example" modal opens
  And a GET /account/api/admin/others/{id}/profile-for-approval returns 200
  And the description list shows Email, Display name, Account type, Profile type, Submitted
  And (if the applicant filled the form) Identity type / Identity number / Saudi mobile / Selected interests appear
  When they click "Confirm approval"
  Then the modal closes
  And a POST /account/api/admin/others/{id}/approve returns 200 with ApiResult.Success = true
  And a green toast reads "Approved press@maritime-news.example."
  And the grid reloads and the row is gone (now {N - 1} rows)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-others-pending-golden-before.png`
- Screenshot after: `docs/screenshots/cp-admin-others-pending-golden-after.png`
- Console errors: 0 expected
- Network: `/account/api/admin/others/pending/list`, `.../profile-for-approval`, `.../{id}/approve` all return 200
- Audit row: `OperationLog` row for the approve event with the actor's id + a minted QR id on the approved account

### E2E-OPN-002 — View modal (read-only)

```gherkin
Scenario: View renders the full pending profile without approving
  Given the administrator is on /admin/others/pending
  When they click "View" on a pending row whose applicant has filled the profile form
  Then an "Application details — {email}" modal opens (no "Confirm approval" button — only "Close")
  And a GET /account/api/admin/others/{id}/profile-for-approval returns 200
  And the first description list shows Email, Display name, Account type, Profile type, Submitted
  And a second description list shows Full name (Arabic), Full name (English), Nationality,
      Date of birth, Place of birth, Identity type, Identity number, Saudi mobile,
      International mobile, ID image uploaded (Yes/No), Selected interests
  And when "ID image uploaded" = Yes an <img> loads from /account/api/admin/others/{id}/id-document
  And when the applicant has a profile photo (HasAvatar = true) a "Profile photo" block
      renders an <img> from /account/api/admin/others/{id}/avatar (D-727, owner item 5) —
      so the reviewer sees the staff member's face before approving
  When they click "Close"
  Then the modal closes and no approve/reject request fires

Scenario: View of an account that has NOT filled the form
  Given a pending Other created by an admin who has not yet completed the profile form
  When the administrator clicks "View" on that row
  Then the modal shows the core description list
  And an info SimfAlert reads "This account has not filled out the profile form yet."
```

### E2E-OPN-003 — Single Reject with reason

```gherkin
Scenario: Reject a pending Other with a mandatory reason
  Given the administrator is on /admin/others/pending
  When they click "Reject" on the row for "rep@exhibitor.example"
  Then a "Reject account" modal opens
  And it reads "Reject rep@exhibitor.example? This sets the account to Rejected and writes an audit row."
  And a "Reason" textarea is shown with helper "Between 10 and 500 characters. Shown to operators in the audit log."
  And the "Reject" submit button is disabled while the reason is shorter than 10 characters
  When they type "Duplicate registration — already approved under another email."
  And they click "Reject"
  Then a POST /account/api/admin/others/{id}/reject returns 200 with body { "Reason": "<that text>" }
  And the modal closes
  And a green toast reads "Rejected rep@exhibitor.example."
  And the grid reloads and the row is gone
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-others-pending-reject-modal.png`
- Network: `/account/api/admin/others/{id}/reject` returns 200
- Audit row: `OperationLog` row carrying the rejection reason + the actor id

### E2E-OPN-004 — Bulk approve selected

```gherkin
Scenario: Select multiple pending rows and approve them in one batch
  Given the grid shows at least 3 pending Other rows
  When the administrator ticks the select-row checkbox on 3 rows
  And clicks "Approve selected"
  Then a POST /account/api/admin/others/bulk-approve fires with body { "Ids": [<3 guids>] }
  And the API returns 200 with ApiResult.Data = { Approved: 3, Skipped: 0, Failures: [] }
  And a green toast reads "Approved 3 user(s). Skipped 0."
  And the grid reloads with the 3 rows gone
```

### E2E-OPN-005 — Bulk reject selected (shared reason)

```gherkin
Scenario: Select multiple pending rows and reject them with one shared reason
  Given the grid shows at least 2 pending Other rows
  When the administrator ticks 2 rows
  And clicks "Reject selected"
  Then a "Reject selected accounts" modal opens
  And it reads "You are about to reject 2 pending account(s)..." (count = 2)
  And the "Reject selected" submit button is disabled until the reason is 10–500 characters
  When they type "Bulk cleanup of stale partner applications before the event."
  And they click "Reject selected"
  Then a POST /account/api/admin/others/bulk-reject fires with body { "Ids": [<2 guids>], "Reason": "<text>" }
  And the API returns 200 with ApiResult.Data = { Rejected: 2, Skipped: 0, Failures: [] }
  And a green toast reads "Rejected 2 user(s). Skipped 0."
  And the grid reloads with the 2 rows gone
```

### E2E-OPN-006 — Pager + filter

```gherkin
Scenario: Page through the queue and filter by email
  Given the queue holds more than one page of pending Other rows (page size 20)
  When the administrator clicks "Next page"
  Then a POST /account/api/admin/others/pending/list fires with the advanced Skip
  And the pager summary updates (e.g. "21–40 of 57")
  When they click "First page"
  Then the grid returns to rows 1–20
  When they type "exhibitor" into the per-column "Filter column Email" input
  Then the list request carries GridQuery.Filters["email"] = "exhibitor" with Skip reset to 0
  And only rows whose Email matches remain
  # NB: the page has NO single free-text search box — filtering is per column
  #     (Email + Display name). The full filter behaviour is E2E-OPN-015.
```

### E2E-OPN-007 — Empty queue

```gherkin
Scenario: Empty queue renders SimfEmptyState
  Given there are no Other-typed accounts in PendingApproval
  When the administrator opens /admin/others/pending
  Then the grid body renders the SimfEmptyState component
  And the title reads "No accounts are waiting for approval." / "لا توجد حسابات بانتظار الموافقة."
  And no error toast appears
```

### E2E-OPN-008 — Auth gate

```gherkin
Scenario: Signed-in admin lacking Others.View is denied the page
  Given a signed-in admin whose role does NOT grant PermissionCatalog.Others.View
  When they navigate to /admin/others/pending
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/others/pending/list request fires

Scenario: An admin with Others.View but lacking approve rights cannot approve at the API
  Given a signed-in admin who holds Others.View but neither Admins.Approve nor Others.Approve
  When they open the page and click "Approve" then "Confirm approval"
  Then the BFF forwards POST /admin/others/{id}/approve
  And the API returns 403 (policy PermissionCatalog.PolicyFor(Admins.Approve) denies it)
  And a red error toast surfaces the bilingual server message
  And the row stays in the queue
```

### E2E-OPN-009 — Reject reason validation

```gherkin
Scenario: A too-short reason keeps the Reject submit disabled and fires no request
  Given the "Reject account" modal is open for one pending row
  When the administrator types "too short" (9 characters)
  Then the "Reject" submit button stays disabled
  And no /account/api/admin/others/{id}/reject request fires
  When they type a reason longer than 500 characters
  Then the textarea caps the input at MaxLength 500
  And the submit button is enabled at exactly 10–500 characters
```

### E2E-OPN-010 — Conflict (already approved)

```gherkin
Scenario: Approving an account already approved elsewhere surfaces a server error
  Given two admin tabs are open on /admin/others/pending showing the same pending row
  When tab A approves the row (row vanishes, toast "Approved ...")
  And tab B then clicks "Approve" + "Confirm approval" on the now-stale row
  Then the BFF forwards POST /admin/others/{id}/approve
  And the API rejects the illegal state transition for the already-approved account
  And a red toast surfaces envelope.Error.MessageForCurrentCulture() (fallback "Something went wrong...")
  And tab B reloads and the stale row is gone
```

### E2E-OPN-011 — Cross-kind 404

```gherkin
Scenario: A Visitor id on the Others profile-for-approval route returns 404
  Given a pending VISITOR account id (UserType = Visitor, not Other)
  When that id is requested at GET /account/api/admin/others/{visitorId}/profile-for-approval
  Then the API returns HTTP 404 with ApiResult.Error.Code = ErrorCodes.NotFound
  And the response is byte-identical to an unknown-id 404 (404-collapses-all-mismatch policy)
  And the View modal shows the error SimfAlert with the fallback "The profile could not be loaded."
  And the English server message is "No pending Other account was found for this id."
  And the Arabic server message is "لم يتم العثور على حساب آخر بانتظار الموافقة بهذا المعرّف."
```

### E2E-OPN-012 — Server 500 on list

```gherkin
Scenario: API 500 on /others/pending/list degrades gracefully
  Given the API is configured to return 500 on /admin/others/pending/list (e.g. DB down)
  When the administrator opens /admin/others/pending
  Then the grid shows the loading indicator then resolves to an empty grid
  And the page does not throw (it coalesces a failed envelope to GridPage.Of(empty))
  And no rows render and no unhandled console error appears
```

### E2E-OPN-013 — Bulk partial (skipped count)

```gherkin
Scenario: Bulk approve over a mix of valid and stale ids reports the skipped count
  Given the administrator selects 5 rows, one of which was approved/rejected in another session
  When they click "Approve selected"
  Then POST /account/api/admin/others/bulk-approve returns 200
  And ApiResult.Data = { Approved: 4, Skipped: 1, Failures: [ { UserId, Email, ReasonCode, ... } ] }
  And an amber/warning toast reads "Approved 4 user(s). Skipped 1."
  And the 4 approved rows are gone while the failed one is reported
```

### E2E-OPN-014 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the reject modal
  Given the administrator is on /admin/others/pending in English
  When they switch the language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "موافقات المستخدمين الآخرين المعلّقة"
  And the supporting line and the row action buttons (موافقة / رفض / عرض) render in Arabic
  And the nav rail + pager arrows mirror
  When they click "رفض" on a row
  Then the "رفض الحساب" modal opens in RTL with the Arabic reason label and helper
  When they reject with a valid reason
  Then the toast reads "تمّ رفض {email}."
```

### E2E-OPN-015 — Per-column filter narrows the grid (D-257)

```gherkin
Scenario: Typing in the Email column filter input narrows the queue
  Given the administrator is on /admin/others/pending with more than one page of pending Other rows
  And the grid header carries a per-column filter row under the sortable headers
  And the Email and Display name columns each render a "search" filter input
      (aria-label "Filter column Email" / "Filter column Display name", placeholder "Search")
  When they type "exhibitor" into the "Filter column Email" input
  Then after the 300 ms debounce a POST /account/api/admin/others/pending/list fires
  And the request body carries GridQuery.Filters["email"] = "exhibitor" with Skip reset to 0
  And only rows whose Email matches (server-side EF Like %exhibitor%) remain
  And the pager summary recomputes for the filtered total
  When they additionally type "Maritime" into the "Filter column Display name" input
  Then the next list request carries BOTH Filters["email"] = "exhibitor"
      and Filters["displayName"] = "Maritime" (the filters compose)
  When they clear the Email filter input
  Then the list request drops the "email" key and keeps Filters["displayName"] = "Maritime"
```

**Notes:** the only Filterable columns are `email` and `displayName`; the `created`
column has no filter input. Filter keys are case-sensitive (`email` / `displayName`)
and honoured server-side in `GetPendingPageAsync` (`AdminAccountService.cs` — the
"Per-column filters (CP grid Filterable columns: email, displayName)" block).

### E2E-OPN-016 — Column sort toggles (D-256)

```gherkin
Scenario: Clicking a sortable header cycles ascending then descending
  Given the administrator is on /admin/others/pending
  And the queue lists rows in the natural order (newest first by Created)
  When they click the "Email" column header
  Then a POST /account/api/admin/others/pending/list fires with Sort = "email",
      SortDescending = false, Skip reset to 0
  And the rows reorder ascending by Email and the header shows the ascending (▲) arrow
  When they click the "Email" header again
  Then the next list request carries Sort = "email", SortDescending = true
  And the rows reorder descending and the header shows the descending (▼) arrow
  When they click the "Display name" header instead
  Then the request carries Sort = "displayName", SortDescending = false
      (switching column resets to ascending)
  And the "Created" column header is NOT sortable (no sort button, no arrow)
```

---

## Implementation notes

- **Lower-layer API coverage already exists.** The same surface is covered without
  a browser by these xUnit + `WebApplicationFactory` suites under
  `tests/SIMF.Api.Tests/`:
  - `AdminApprovalTests.cs` — single approve/reject of Other accounts (`ApproveOther` /
    `RejectOther`), QR mint on approve, mandatory 10–500 char reason.
  - `AdminBulkApprovalTests.cs` — `/admin/others/bulk-approve`, the `Approved`/`Skipped`/
    `Failures` shape, empty-id 400 (`ErrorCodes.AdminBulkActionInvalid`), and the
    `Others.Approve` gate (D-214 security fix).
  - `AdminBulkRejectTests.cs` — `/admin/others/bulk-reject`, shared-reason validation,
    the `Rejected`/`Skipped`/`Failures` shape, `Others.Reject` gate.
  - `PendingProfileReadTests.cs` — `/admin/others/{id}/profile-for-approval`, the
    404-collapses-all-mismatch policy (unknown / approved / wrong-type ids, including
    the cross-kind Visitor-id case in E2E-OPN-011).
  - `PermissionEnforcementTests.cs` (`SIMF.Api.Tests`) + `CpNavigationPermissionTests.cs`
    (`SIMF.ControlPanel.Tests`) — fail the build if a route/page gate is missing; these
    back E2E-OPN-008.
- **Manual smoke is the canonical run today.** Until Playwright is adopted, walk each
  scenario in a Chrome DevTools MCP session per the
  [SIMF table/smoke pattern](../../dev/SIMF_TABLE_PATTERN.md), signing in with
  `superadmin@zagali-ict.com` + the `Get-Totp` helper, and capture screenshots into
  `docs/screenshots/cp-admin-others-pending-*.png`.
- **Convert to Playwright** when the runner lands: copy each Gherkin block into a
  `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created) plus a
  step-definition class. The steps are already runner-agnostic.
- **Permission caveat worth a regression note:** the single Approve/Reject endpoints
  gate on `Admins.Approve` / `Admins.Reject` while the bulk endpoints gate on
  `Others.Approve` / `Others.Reject` and the page gate is `Others.View`. E2E-OPN-008's
  second scenario exercises this asymmetry; flag any future code that aligns these so
  the catalogue is updated in the same changeset.

---

_Last reviewed:_ 2026-06-03 by Claude (E2E catalogue rebuild) (D-256/D-257 grid affordances reconciled).
