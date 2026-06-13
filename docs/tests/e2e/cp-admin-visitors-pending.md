# E2E test catalogue — Pending visitor approvals (`/admin/visitors/pending`)

| | |
|--|--|
| **Page** | [`cp/admin-visitors-pending.md`](../../pages/cp/admin-visitors-pending.md) |
| **Route** | `/admin/visitors/pending` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-13 |

> **Page permission:** the page itself is gated by
> `[RequirePermission(PermissionCatalog.Visitors.View)]` (`"Visitors.View"`).
> The **Approve** action API is gated by `Visitors.Approve`, **Reject** (single +
> bulk) by `Visitors.Reject`. A holder of `Visitors.View` alone can *open* the page
> and read profiles but the per-row / bulk approve & reject calls return 403 — this
> is its own scenario (E2E-VPN-010). The approve-time **profile-type (tier)
> picker** (D-386) additionally needs **`ProfileTypes.View`** to populate its
> options — an admin without it can still approve (the tier is left unchanged).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-VPN-001 | Golden round-trip — list → View → Approve-with-review → row vanishes + QR minted + audit | happy | P0 | _to author_ |
| E2E-VPN-002 | View (read-only) modal renders the full profile `<dl>` and closes | happy | P1 | _to author_ |
| E2E-VPN-003 | View modal renders the ID-document image inline when `HasIdImage` | happy | P1 | _to author_ |
| E2E-VPN-004 | View modal on a profile with no form data → `Admin.Pending.View.Empty` info alert | happy | P2 | _to author_ |
| E2E-VPN-005 | Reject one visitor with a 50-char reason → Rejected + reason audited | happy | P0 | _to author_ |
| E2E-VPN-006 | Reject validation — Submit disabled below 10 / above 500 chars | error | P1 | _to author_ |
| E2E-VPN-007 | Bulk approve — select rows + "Approve selected" → `Approved {n}. Skipped {m}.` | happy | P1 | _to author_ |
| E2E-VPN-008 | Bulk reject — select rows + "Reject selected" → shared-reason modal → `Rejected {n}. Skipped {m}.` | happy | P1 | _to author_ |
| E2E-VPN-009 | Empty queue renders `SimfEmptyState` (`Admin.Pending.None`) | happy | P1 | _to author_ |
| E2E-VPN-010 | Auth gate — user lacking `Visitors.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-VPN-011 | Stale row — sibling admin already actioned → 409 `AdminUserNotPending` toast | error | P1 | _to author_ |
| E2E-VPN-012 | Scope guard — approving a partner/admin id via the visitors URL → 404 `AdminUserNotFound` | error | P2 | _to author_ |
| E2E-VPN-013 | Server 500 on `/pending/list` → empty grid, no crash | resilience | P2 | _to author_ |
| E2E-VPN-014 | RTL / Arabic render mirrors page + Reject modal | i18n | P1 | _to author_ |
| E2E-VPN-015 | Per-column filter (Email / Display name) narrows the grid | happy | P1 | _to author_ |
| E2E-VPN-016 | Column sort toggles on Email / Display name | happy | P2 | _to author_ |
| E2E-VPN-017 | View/Approve modal shows ALL captured profile data (Job title, Gender, Organisation, Plate number, Reference number) for a fully-populated visitor (D-385) | happy | P0 | _to author_ |
| E2E-VPN-018 | Selected interests render as NAMES, not a bare count (D-385) | happy | P1 | _to author_ |
| E2E-VPN-019 | Approve WITH a profile-type (tier) → visitor's `ProfileTypeId` is set (D-386) | happy | P0 | _to author_ |
| E2E-VPN-020 | Approve with a partner / inactive tier → 400 `ADMIN_PROFILE_TYPE_INVALID` bilingual toast (D-386) | error | P1 | _to author_ |
| E2E-VPN-021 | Approve with "Keep current" (null `ProfileTypeId`) → tier unchanged (D-386) | happy | P1 | _to author_ |
| E2E-VPN-022 | Face photo thumbnail opens full / original-size in the stacked lightbox (D-387) | happy | P1 | _to author_ |
| E2E-VPN-023 | Face photo downloads via the `<a download>` link (thumbnail + lightbox footer) (D-387) | happy | P2 | _to author_ |
| E2E-VPN-024 | RTL / Arabic render of the View / Approve modal — all-data `<dl>`, tier picker, photo lightbox (D-385/386/387) | i18n | P1 | _to author_ |

## Scenarios

### E2E-VPN-001 — Golden round-trip (View → Approve-with-review)

```gherkin
Feature: Pending visitor approval golden path
  As an Administrator on the approval queue
  I want to review a pending visitor's profile and approve it
  So that their QR badge is minted and event entry is unlocked

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp (Get-Totp helper)
  And at least one Visitor account is in AccountState=PendingApproval
  And the administrator has landed on /admin/visitors/pending

Scenario: Review then approve a single pending visitor
  Given the grid shows {N} pending rows with columns Email, Display name, Submitted
  And a row exists for visitor.e2e@example.com
  When the administrator clicks "Approve" on that row
  Then the review modal opens titled "Review and approve — visitor.e2e@example.com"
  And it fires GET /account/api/admin/visitors/{id}/profile-for-approval
  And while loading it shows "Loading the profile…"
  And on load it renders the profile description list:
    | Email          | visitor.e2e@example.com |
    | Display name   | (the row value)         |
    | Account type   | Visitor                 |
    | Profile type   | (the assigned type or —)|
  And the form fields (Full name Arabic/English, Nationality, Date of birth, Place
      of birth, Identity type, Identity number, Saudi mobile, International mobile,
      ID image uploaded, Selected interests) render when present
  And the footer shows "Cancel" and "Confirm approval"
  When the administrator clicks "Confirm approval"
  Then the review modal closes
  And POST /account/api/admin/visitors/{id}/approve returns 200 with ApiResult.Success=true
  And a green toast reads "Approved visitor.e2e@example.com."
  And the grid reloads and the row no longer appears (the account is now Approved)
  And the grid count drops to {N - 1}
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-visitors-pending-golden-before.png`
- Screenshot after (review modal): `docs/screenshots/cp-admin-visitors-pending-golden-review.png`
- Screenshot after (toast + reloaded grid): `docs/screenshots/cp-admin-visitors-pending-golden-after.png`
- Console errors: 0 expected
- Network: `GET /account/api/admin/visitors/{id}/profile-for-approval` → 200, then
  `POST /account/api/admin/visitors/{id}/approve` → 200, then
  `POST /account/api/admin/visitors/pending/list` → 200
- Side effects: subject `AccountState=Approved`, `UserProfile.QrId` minted (non-null),
  every refresh token for the subject revoked, `NotificationKind.AccountApproved`
  dispatched + email queued
- Audit row: `Admin.VisitorApproved` (`AuditEvents.AdminVisitorApproved`) with the
  actor's id, the subject id/email, and `Detail = profile.QrId`

### E2E-VPN-002 — View (read-only) modal

```gherkin
Scenario: View opens a read-only profile preview and closes
  Given the queue shows a pending visitor row
  When the administrator clicks "View" on that row
  Then a modal opens titled "Application details — {email}"
  And it fires GET /account/api/admin/visitors/{id}/profile-for-approval (200)
  And the profile renders in description lists (no Confirm button — read-only mode)
  And the footer shows a single "Close" button
  When the administrator clicks "Close"
  Then the modal closes and no approve/reject call fires
  And the grid row is unchanged
```

### E2E-VPN-003 — ID image inline in View modal

```gherkin
Scenario: View modal shows the uploaded ID document inline
  Given a pending visitor whose profile has HasIdImage=true
  When the administrator clicks "View" on that row
  Then the "ID image uploaded" field reads "Yes"
  And an <img> renders under the "ID document" heading
  And its src is /account/api/admin/visitors/{id}/id-document?v={cache-buster}
  And that GET returns 200 with an image content-type
```

### E2E-VPN-004 — Profile with no form data

```gherkin
Scenario: View modal on an empty profile shows the info alert
  Given a pending visitor whose profile has no form data
      (no Arabic/English name, nationality, DOB, identity, mobile, ID image, interests)
  When the administrator clicks "View" on that row
  Then the base identity description list still renders (Email, Display name, etc.)
  And a SimfAlert info reads "This account has not filled out the profile form yet."
  And no profile-form description list is shown
```

### E2E-VPN-005 — Reject one visitor with a reason

```gherkin
Scenario: Reject a single pending visitor with a valid reason
  Given the queue shows a pending visitor row for visitor.reject@example.com
  When the administrator clicks "Reject" on that row
  Then the reject modal opens titled "Reject account"
  And the body reads "Reject visitor.reject@example.com? This sets the account to
      Rejected and writes an audit row."
  And it shows a "Reason" textarea with hint "Between 10 and 500 characters. Shown to
      operators in the audit log." (MaxLength 500, 3 rows)
  When the administrator types "Documents do not match the registered identity."
  And the "Reject" submit button becomes enabled (reason length is 10..500)
  And they click "Reject"
  Then POST /account/api/admin/visitors/{id}/reject returns 200
      with body { "reason": "Documents do not match the registered identity." }
  And the modal closes
  And a green toast reads "Rejected visitor.reject@example.com."
  And the grid reloads and the row no longer appears
```

**Evidence captured:**
- Screenshots: `docs/screenshots/cp-admin-visitors-pending-reject-modal.png`,
  `docs/screenshots/cp-admin-visitors-pending-reject-after.png`
- Console errors: 0 expected
- Network: `POST /account/api/admin/visitors/{id}/reject` → 200, then
  `POST /account/api/admin/visitors/pending/list` → 200
- Side effects: subject `AccountState=Rejected`, `UserProfile.RejectionReason` +
  `RejectionReasonArabic` set to the reason, refresh tokens revoked,
  `NotificationKind.AccountRejected` dispatched + email queued
- Audit row: `Admin.VisitorRejected` (`AuditEvents.AdminVisitorRejected`) with the
  actor id, subject id/email, and `Detail = reason`

### E2E-VPN-006 — Reject reason length validation (client-side gate)

```gherkin
Scenario Outline: Reject submit stays disabled outside 10..500 chars
  Given the reject modal is open for a pending visitor
  When the administrator types a reason of <length> characters
  Then the "Reject" submit button is <state>
  And no POST /account/api/admin/visitors/{id}/reject fires while disabled

  Examples:
    | length | state    |
    | 0      | disabled |
    | 5      | disabled |
    | 9      | disabled |
    | 10     | enabled  |
    | 500    | enabled  |
    | 501    | disabled |
```

### E2E-VPN-007 — Bulk approve

```gherkin
Scenario: Approve several pending visitors in one batch
  Given the queue shows at least 3 pending visitor rows
  When the administrator ticks the "Select all" checkbox (or several per-row checkboxes)
  And clicks "Approve selected"
  Then POST /account/api/admin/visitors/bulk-approve fires with body
      { "ids": [ ...selected guids... ] } and returns 200
  And a toast reads "Approved {Approved} user(s). Skipped {Skipped}."
  And the toast is green when Skipped=0, amber (warning) when Skipped>0
  And the grid reloads with the approved rows removed
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-visitors-pending-bulk-approve.png`
- Network: `POST /account/api/admin/visitors/bulk-approve` → 200 (one audit +
  one operation-log row per approved subject), then `/pending/list` → 200

### E2E-VPN-008 — Bulk reject (shared reason)

```gherkin
Scenario: Reject several pending visitors with one shared reason
  Given the queue shows at least 2 pending visitor rows
  When the administrator ticks the per-row checkboxes for those rows
  And clicks "Reject selected"
  Then a shared-reason modal opens titled "Reject selected"
  And the body reads the count of selected rows ("Reject {n} accounts? ...")
  And it shows the same "Reason" textarea (10..500 chars, MaxLength 500)
  When the administrator types "Batch rejected — duplicate registrations."
  And clicks "Reject"
  Then POST /account/api/admin/visitors/bulk-reject fires with body
      { "ids": [...], "reason": "Batch rejected — duplicate registrations." } → 200
  And a toast reads "Rejected {Rejected} user(s). Skipped {Skipped}."
  And the toast is green when Skipped=0, amber (warning) when Skipped>0
  And the grid reloads with the rejected rows removed
```

### E2E-VPN-009 — Empty queue

```gherkin
Scenario: Empty queue renders SimfEmptyState
  Given no Visitor accounts are in PendingApproval
  When the administrator opens /admin/visitors/pending
  Then POST /account/api/admin/visitors/pending/list returns 200 with 0 rows
  And the grid body renders the SimfEmptyState component
  And it shows the bilingual copy "No accounts are waiting for approval." /
      "لا توجد حسابات بانتظار الموافقة."
  And no error toast appears
```

### E2E-VPN-010 — Auth gate

```gherkin
Scenario: A signed-in user without Visitors.View is denied
  Given a signed-in admin whose roles grant no "Visitors.View" permission
      (and are not Administrator="*")
  When they navigate to /admin/visitors/pending
  Then they land on /not-permitted with HTTP 200
  And no POST /account/api/admin/visitors/pending/list request fires
  And the page is not rendered (RequirePermission attribute blocks it)
```

### E2E-VPN-011 — Stale row (concurrent action)

```gherkin
Scenario: Approving a row a sibling admin already actioned shows the 409 toast
  Given the administrator loaded the queue showing visitor.stale@example.com
  And another admin approved (or rejected) that same account in the meantime
      (subject is no longer in AccountState=PendingApproval)
  When the administrator opens the review modal and clicks "Confirm approval"
  Then POST /account/api/admin/visitors/{id}/approve returns HTTP 409
      with ApiResult.Error.Code = "AdminUserNotPending"
  And a red toast surfaces the bilingual MessageForCurrentCulture()
      "The target account is not pending approval." /
      "الحساب المستهدف ليس في انتظار الموافقة."
  And the grid reloads (the stale row falls off)
```

### E2E-VPN-012 — Scope guard (wrong queue)

```gherkin
Scenario: Approving a partner/admin id via the visitors endpoint is rejected
  Given a pending account whose linked ProfileType.IsVisitor=false (a partner/Other)
      OR a UserType=Admin id
  When a request hits POST /account/api/admin/visitors/{thatId}/approve
  Then the API returns HTTP 404 with ApiResult.Error.Code = "AdminUserNotFound"
  And the bilingual message reads "The target account is not in the expected approval
      queue." / "الحساب المستهدف ليس في قائمة الاعتماد المتوقعة."
  And a SOC audit row "Admin.ApprovalScopeMismatch" is written (Outcome=Failure)
  (Note: such an id is not normally present on the visitors queue grid; this guards
   a hand-crafted / forged request.)
```

### E2E-VPN-013 — Server 500 on list

```gherkin
Scenario: API 500 on /pending/list leaves an empty grid, no crash
  Given the API is configured to return 500 on /admin/visitors/pending/list (DB down)
  When the administrator opens /admin/visitors/pending
  Then the grid shows the loading indicator while the call is in flight
  And on failure LoadAsync falls back to an empty GridPage (0 rows)
  And the grid body renders the SimfEmptyState (no unhandled exception, no console error)
```

### E2E-VPN-014 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the Reject modal
  Given the administrator is on /admin/visitors/pending in English
  When they switch the UI language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "موافقات الزوار المعلّقة"
  And the supporting text + grid headers render in Arabic, mirrored
  And the row action buttons (View / Approve / Reject) appear in reverse order

  When the administrator clicks "رفض" (Reject) on a row
  Then the reject modal opens RTL titled "رفض الحساب"
  And the "Reason" textarea label + hint are Arabic
  And the submit button reads "رفض" and footer actions are reverse-ordered
```

### E2E-VPN-015 — Per-column filter narrows the grid

```gherkin
Scenario: Filter the queue by Email and by Display name
  Given the grid shows several pending rows including visitor.e2e@example.com
  And the Email and Display name columns each expose a per-column filter input
      ("Filter column Email" / "Filter column Display name", placeholder "Search")
  When the administrator types "visitor.e2e" into the Email column filter
  Then POST /account/api/admin/visitors/pending/list fires with
      GridQuery.Filters["email"] = "visitor.e2e" and Skip reset to 0 (page 1)
  And the backend applies a case-insensitive LIKE %visitor.e2e% over Email
  And the grid narrows to the matching row(s) and the pager total updates
  When the administrator clears that input and types "Al-" into the Display name filter
  Then the next /pending/list fires with GridQuery.Filters["displayName"] = "Al-"
      (and no "email" key) and Skip = 0
  And the grid narrows to rows whose Display name contains "Al-"
  When the administrator clears the Display name filter
  Then /pending/list fires with empty Filters and the full queue returns
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-visitors-pending-filter.png`
- Network: `POST /account/api/admin/visitors/pending/list` → 200 with
  `Filters["email"]`, then with `Filters["displayName"]`, then with no filter keys
- Note: the `created` column is display-only — it exposes no filter input
  (only `email` + `displayName` are `Filterable`). The backend honours the same
  two keys in `ListPendingAsync` (`AdminAccountService.cs`).

### E2E-VPN-016 — Column sort toggles

```gherkin
Scenario: Sort the queue by Email then Display name, toggling direction
  Given the grid shows several pending rows
  When the administrator clicks the "Email" column header
  Then POST /account/api/admin/visitors/pending/list fires with
      GridQuery.Sort = "email" and SortDescending = false (ascending)
  And the rows reorder ascending by Email
  When the administrator clicks the "Email" header again
  Then /pending/list fires with Sort = "email" and SortDescending = true
  And the rows reorder descending by Email
  When the administrator clicks the "Display name" column header
  Then /pending/list fires with Sort = "displayName" and SortDescending = false
  And the rows reorder ascending by Display name
  And the "created" column header is not sortable (no sort call fires when clicked)
```

### E2E-VPN-017 — Modal shows ALL captured profile data (D-385)

```gherkin
Scenario: The View / Approve modal renders every captured profile field
  Given a fully-populated pending visitor visitor.full@example.com whose profile has
      a Job title, Gender, an Organisation, a Plate number and a Reference number
  When the administrator clicks "View" (or "Approve") on that row
  Then GET /account/api/admin/visitors/{id}/profile-for-approval returns 200
      with a PendingProfileResponse populated by AdminApprovalReadService
  And the profile description list additionally renders:
    | Job title       | (the captured job title)            |
    | Gender          | (Male / Female per the captured value) |
    | Organisation    | (the org name, bilingual)           |
    | Plate number    | (the captured plate)                |
    | Reference number| (the captured reference)            |
  And these render alongside the existing identity + form fields
  And the modal does NOT show QrId or RejectionReason (excluded from the pending preview)
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-visitors-pending-alldata.png`
- Network: `GET /account/api/admin/visitors/{id}/profile-for-approval` → 200
- Assert the `<dl>` contains Job title, Gender, Organisation, Plate number,
  Reference number and contains neither "QR" nor "Rejection reason"

### E2E-VPN-018 — Interests render as names, not a count (D-385)

```gherkin
Scenario: Selected interests are listed by name
  Given a pending visitor whose profile has 3 selected interests
      (e.g. "Maritime security", "Naval logistics", "Shipbuilding")
  When the administrator opens the View / Approve modal
  Then the "Selected interests" field lists the interest NAMES
      ("Maritime security, Naval logistics, Shipbuilding")
  And it does NOT show a bare count such as "3 interests"
```

### E2E-VPN-019 — Approve WITH a profile-type sets the tier (D-386)

```gherkin
Scenario: Approving and choosing a tier sets the visitor's profile-type
  Given the administrator (holding Visitors.Approve AND ProfileTypes.View) opens the
      Approve modal for visitor.tier@example.com
  And the approve modal shows a profile-type picker defaulting to "Normal" (D-392;
      "Keep current" remains a selectable option) populated with the active
      audience-side profile types (via ProfileTypes.View)
  When the administrator selects the tier "VIP" and clicks "Confirm approval"
  Then POST /account/api/admin/visitors/{id}/approve fires with body
      { "profileTypeId": "{VIP-guid}" } and returns 200
  And the visitor's UserProfile.ProfileTypeId is set to the VIP id
  And a green toast reads "Approved visitor.tier@example.com."
  And the grid reloads and the row no longer appears
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-visitors-pending-tier-picker.png`
- Network: `POST /account/api/admin/visitors/{id}/approve` with `profileTypeId` → 200
- Side effect: subject `UserProfile.ProfileTypeId` = the selected tier id

### E2E-VPN-020 — Approve with a partner / inactive tier is rejected (D-386)

```gherkin
Scenario: A partner-side or inactive tier on approve returns a 400 error
  Given the Approve modal is open for a pending visitor
  When a request hits POST /account/api/admin/visitors/{id}/approve with a
      profileTypeId that is partner-side, inactive, or unknown
  Then the API returns HTTP 400 with ApiResult.Error.Code = "ADMIN_PROFILE_TYPE_INVALID"
  And a red toast surfaces the bilingual MessageForCurrentCulture()
      (English / Arabic invalid-profile-type message)
  And the visitor stays in AccountState=PendingApproval (no approval, no QR minted)
  And the row remains on the queue
```

### E2E-VPN-021 — Approve with "Keep current" leaves the tier unchanged (D-386)

```gherkin
Scenario: Approving with the default "Keep current" does not change the tier
  Given a pending visitor whose UserProfile.ProfileTypeId is currently {existing} (or null)
  And the administrator opens the Approve modal and leaves the picker on "Keep current"
  When they click "Confirm approval"
  Then POST /account/api/admin/visitors/{id}/approve fires with profileTypeId = null
      (the key absent / null) and returns 200
  And the visitor's UserProfile.ProfileTypeId is unchanged ({existing} / still null)
  And the account is now Approved and the row leaves the queue
```

### E2E-VPN-022 — Photo opens full-size in the lightbox (D-387)

```gherkin
Scenario: Clicking the face-photo thumbnail opens the original-size lightbox
  Given a pending visitor whose profile has a captured face photo
  When the administrator opens the View / Approve modal
  Then a photo thumbnail renders whose src is
      /account/api/admin/visitors/{id}/id-document (same-origin cookie auth)
  When the administrator clicks the thumbnail
  Then a stacked SimfModal lightbox opens showing the full / original-size image
  And the lightbox footer shows a "Download" link
  When the administrator closes the lightbox
  Then the underlying View / Approve modal is still open (the lightbox is stacked on top)
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-visitors-pending-photo-lightbox.png`
- Network: `GET /account/api/admin/visitors/{id}/id-document` → 200 image content-type

### E2E-VPN-023 — Photo downloads (D-387)

```gherkin
Scenario: The Download link saves the original photo
  Given the View / Approve modal (and/or the photo lightbox) is open for a visitor
      whose profile has a face photo
  Then a "Download" link (<a download>) appears under the thumbnail and in the
      lightbox footer, href = /account/api/admin/visitors/{id}/id-document
  When the administrator clicks "Download"
  Then the browser downloads the original image file (same-origin admin cookie auth)
  And GET /account/api/admin/visitors/{id}/id-document returns 200 with an image content-type
```

### E2E-VPN-024 — RTL / Arabic render of the modal (D-385/386/387)

```gherkin
Scenario: Arabic toggle mirrors the all-data modal, tier picker and photo lightbox
  Given the administrator is on /admin/visitors/pending with the UI language set to العربية
  And a fully-populated pending visitor row is shown
  When the administrator clicks "عرض" (View) / "اعتماد" (Approve) on that row
  Then the modal opens RTL (<html dir="rtl" lang="ar">)
  And the description list labels (Job title, Gender, Organisation, Plate number,
      Reference number, Selected interests) render in Arabic, mirrored
  And the Organisation name shows its Arabic value
  And the approve-mode profile-type picker label + "Keep current" option render in Arabic
  And the photo thumbnail + "Download"/"تنزيل" link mirror correctly
  When the administrator clicks the photo thumbnail
  Then the stacked lightbox opens RTL with its footer Download link mirrored
```

---

## Implementation notes

- **Manual smoke is the canonical run today.** Until a Playwright project exists,
  the canonical execution is a Chrome DevTools MCP session: sign in per the Auth
  setup, walk each scenario, and capture screenshots into
  `docs/screenshots/cp-admin-visitors-pending-{scenario}.png`. Keep the Gherkin
  steps runner-agnostic so they port to `.feature` files later.
- **Permission split is load-bearing.** The page opens on `Visitors.View`, but the
  approve / reject / bulk calls are independently gated (`Visitors.Approve`,
  `Visitors.Reject`). E2E-VPN-010 covers the page gate; a view-only admin reaching
  the page but getting 403 on the action calls is a real path worth a dedicated
  probe when the test runner lands.
- **API integration tests cover the same surface at a lower layer (no browser):**
  - `tests/SIMF.Api.Tests/AdminApprovalTests.cs` — single approve/reject, the
    `Admin.VisitorApproved` / `Admin.VisitorRejected` audit rows, and the
    `AdminUserNotPending` (409) / `AdminUserNotFound` (404) guards.
  - `tests/SIMF.Api.Tests/AdminBulkApprovalTests.cs` — bulk-approve batch +
    per-subject skip reporting.
  - `tests/SIMF.Api.Tests/AdminBulkRejectTests.cs` — bulk-reject shared-reason
    batch + per-subject skip reporting.
  When E2E covers a scenario you can usually drop the matching `Api.Tests` case —
  but during the transition keep both.
- **Doc drift note:** `docs/pages/cp/admin-visitors-pending.md` (last reviewed
  2026-05-28) states bulk endpoints "do not exist yet" and that the stale-row case
  returns 404. As built, the bulk-approve (D-164) and bulk-reject (D-209) endpoints
  ship and are wired to the grid's Multiselect, and the stale-row case returns **409
  `AdminUserNotPending`** (404 `AdminUserNotFound` is the missing-id / wrong-scope
  case). This catalogue reflects the as-built code.

---

_Last reviewed:_ 2026-06-13 by Claude (D-385/386/387 — modal all-data display,
approve-time profile-type picker + `ADMIN_PROFILE_TYPE_INVALID`, photo lightbox +
download; added E2E-VPN-017..024).
_Earlier:_ 2026-06-03 by Claude (E2E catalogue rebuild) (D-256/D-257 grid affordances reconciled).
