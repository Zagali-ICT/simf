# E2E test catalogue — Notifications inbox (`/account/notifications`)

| | |
|--|--|
| **Page** | [`cp/account-notifications.md`](../../pages/cp/account-notifications.md) |
| **Route** | `/account/notifications` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Page shape note.** This is a **per-user notification inbox**, not a CRUD-of-records
> admin page. It carries no Add / Create / Edit affordance, so there are no
> field-validation or duplicate-name flows to test. Its destructive ops (Delete /
> Bulk-delete) are **idempotent** server-side and actor-scoped — the API resolves the
> owner from the `sub` claim, so a user only ever sees and mutates their own rows. The
> template's "validation / conflict" rows are therefore reframed below onto the page's
> real surface: filter no-match (empty result), idempotent re-delete, and server-500.
>
> **Auth note.** The page is gated by `@attribute [Authorize]` only — there is **no**
> `RequirePermission` / `PermissionCatalog` code on it. Any signed-in CP user reaches
> it; the auth gate to test is the **unauthenticated** redirect to `/login`, not a
> `/not-permitted` permission denial.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-NTF-001 | Golden round-trip — render mix → Details → per-row Delete → Mark all read | happy | P0 | _to author_ |
| E2E-NTF-002 | Per-row Details modal shows Title / Message / Type / Received | happy | P1 | _to author_ |
| E2E-NTF-003 | Per-row Delete removes the row and refreshes the grid | happy | P1 | _to author_ |
| E2E-NTF-004 | Multiselect → bulk Delete dismisses N rows + bilingual toast | happy | P1 | _to author_ |
| E2E-NTF-005 | Mark all as read — every "New" pill vanishes + bilingual toast | happy | P0 | _to author_ |
| E2E-NTF-006 | Empty inbox renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-NTF-007 | Filter / search no-match → empty grid body, no error | edge | P2 | _to author_ |
| E2E-NTF-008 | Pager — page size + First/Prev/Next/Last navigation | happy | P2 | _to author_ |
| E2E-NTF-009 | Auth gate — unauthenticated visit → `/login` redirect | auth | P0 | _to author_ |
| E2E-NTF-010 | Idempotent re-delete — already-dismissed row → 200, no error | resilience | P2 | _to author_ |
| E2E-NTF-011 | Server 500 on `/list` → no rows render, grid leaves loading state | resilience | P2 | _to author_ |
| E2E-NTF-012 | RTL / Arabic render mirrors page, grid, pager + Details modal | i18n | P1 | _to author_ |

## Scenarios

### E2E-NTF-001 — Golden round-trip

```gherkin
Feature: Notifications inbox round-trip
  As a signed-in Control Panel user
  I want to read, open, delete and clear my notifications
  So that my inbox reflects what I have already handled

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp (TOTP from Get-Totp)
  And the signed-in user has at least 2 unread and 1 read notification seeded
  And they have landed on /account/notifications

Scenario: View a notification, delete one, then mark all read
  Given the grid shows {N} rows newest-first
  And every unread row carries the blue "New" / "جديد" pill in the Title column
  And the columns are Title, Message, Type, Received, Actions
  And the "Mark all as read" button is visible below the grid

  When the user clicks the per-row "Details" (ⓘ) action on the first unread row
  Then a "Notification details" modal opens
  And it shows a description list with Title, Message, Type and Received
  And the Received value matches the row's "yyyy-MM-dd HH:mm" timestamp
  When they click "Close"
  Then the modal closes

  When the user clicks the per-row "Delete" (🗑) action on that same row
  Then a DELETE /account/api/notifications/{id} request fires and returns 200
  And the grid reloads via POST /account/api/notifications/list (200)
  And the grid now shows {N - 1} rows
  And the deleted row is gone

  When the user clicks "Mark all as read"
  Then a POST /account/api/notifications/read-all request fires and returns 200
  And the grid reloads
  And no row shows the "New" pill any more
  And a green SimfAlert toast reads "All notifications marked as read." / "تم تعليم جميع الإشعارات كمقروءة."
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-account-notifications-golden-before.png`
- Screenshot after (post mark-all-read, no pills, toast visible): `docs/screenshots/cp-account-notifications-golden-after.png`
- Details modal: `docs/screenshots/cp-account-notifications-details-modal.png`
- Console errors: 0 expected
- Network: every `/account/api/notifications/*` call returns 200 (`list`, `{id}` DELETE, `read-all`)
- Data check: the deleted row is gone from `SIMF_App.Notifications` for the actor; the remaining rows for the actor all have a non-null `ReadAt`

### E2E-NTF-002 — Per-row Details modal

```gherkin
Scenario: Details modal renders all four read-only fields
  Given the grid shows at least one notification
  When the user clicks the "Details" (ⓘ) action on a row whose Type = "Info"
  Then the "Notification details" modal opens (SimfModal)
  And the description list shows:
    | Field    | Value source        |
    | Title    | TitleFor(row)       |
    | Message  | BodyFor(row)        |
    | Type     | row.Severity        |
    | Received | row.CreatedAt local |
  And there are no editable inputs (read-only modal)
  When they click "Close"
  Then the modal closes and the grid is unchanged
  And no /account/api/notifications request fires for opening Details (client-side only)
```

### E2E-NTF-003 — Per-row Delete

```gherkin
Scenario: Single Delete removes the row
  Given the grid shows {N} rows
  When the user clicks the "Delete" (🗑) action on a target row
  Then DELETE /account/api/notifications/{id} fires and returns 200 with ApiResult.Data = true
  And the grid reloads (POST /list, 200)
  And the grid shows {N - 1} rows
  And the target row no longer appears
  And no error toast appears
```

### E2E-NTF-004 — Multiselect + bulk Delete

```gherkin
Scenario: Select multiple rows and bulk-dismiss
  Given the grid shows at least 3 rows
  When the user ticks the row checkbox on 3 distinct rows
  Then the grid toolbar surfaces a "Delete" bulk action
  When they click the toolbar "Delete"
  Then 3 sequential DELETE /account/api/notifications/{id} requests fire (one per selected row), each 200
  And the grid reloads (POST /list, 200)
  And the 3 selected rows are gone
  And a green toast reads "Dismissed 3 notifications." / "تم تجاهل 3 إشعارات."
```

> Note: there is **no** bulk-dismiss endpoint — `OnBulkDeleteAsync` loops the per-row
> DELETE. Selection caps at the visible page (`Top = 25`), so the loop count is bounded.

### E2E-NTF-005 — Mark all as read

```gherkin
Scenario: Mark all read flips every unread row
  Given the grid shows a mix of read and unread rows
  And at least one row carries the "New" / "جديد" pill
  When the user clicks "Mark all as read"
  Then POST /account/api/notifications/read-all fires and returns 200
  And the grid reloads
  And no row shows the "New" pill
  And a green toast reads "All notifications marked as read." / "تم تعليم جميع الإشعارات كمقروءة."
  And (cross-check) the header bell unread badge drops to 0 on its next poll
```

### E2E-NTF-006 — Empty inbox

```gherkin
Scenario: Empty inbox renders SimfEmptyState
  Given the signed-in user has zero notification rows
  When they open /account/notifications
  Then POST /account/api/notifications/list returns 200 with an empty page (Total = 0)
  And the grid body renders the SimfEmptyState component
  And the empty state reads "No notifications." / "لا توجد إشعارات."
  And the "Mark all as read" button is still present (a no-op click stays 200)
  And no error toast appears
```

### E2E-NTF-007 — Filter / search no-match

```gherkin
Scenario: Filtering to a term that matches nothing shows an empty grid
  Given the grid shows several notifications
  When the user types a non-matching term (e.g. "zzz-no-such-notification") into the grid filter
  Then POST /account/api/notifications/list fires with the filter in the GridQuery
  And it returns 200 with Total = 0
  And the grid body renders the SimfEmptyState ("No notifications." / "لا توجد إشعارات.")
  And no error toast appears
  When the user clears the filter
  Then the full row set returns
```

### E2E-NTF-008 — Pager

```gherkin
Scenario: Page size and navigation controls work
  Given the signed-in user has more than 25 notifications
  When they open /account/notifications
  Then the grid shows 25 rows (default Top = 25)
  And the summary reads "Showing 1–25 of {Total}" (Arabic: "عرض ...")
  When they click "Next"
  Then POST /list fires with the next Skip offset and returns 200
  And the summary advances (e.g. "Showing 26–50 of {Total}")
  When they change the page-size selector ("Show") to a larger value
  Then /list re-fires with the new Top and the row count matches
  When they click "Last page" then "First page"
  Then the pager jumps to the final page then back to page 1, each via a 200 /list call
```

### E2E-NTF-009 — Auth gate (unauthenticated)

```gherkin
Scenario: An unauthenticated visitor is redirected to login
  Given there is no signed-in CP session (cookies cleared)
  When the browser navigates to /account/notifications
  Then the [Authorize] attribute redirects to /login (HTTP 200 on the login page)
  And no POST /account/api/notifications/list request fires
```

> The page has **no** `RequirePermission` gate, so there is no `/not-permitted`
> scenario — any *signed-in* CP user is allowed. The only gate is authentication.
> Server-side, the API additionally scopes every operation to the caller's `sub`
> claim, so one user can never read or delete another user's notifications.

### E2E-NTF-010 — Idempotent re-delete

```gherkin
Scenario: Deleting an already-dismissed notification is a safe no-op
  Given a notification row that has just been deleted
  When a second DELETE /account/api/notifications/{id} fires for the same id
  Then the API returns HTTP 200 with ApiResult.Data = true (idempotent)
  And no error toast appears
  And the grid stays consistent (the row remains absent)
```

### E2E-NTF-011 — Server 500 on `/list`

```gherkin
Scenario: A 500 on the list endpoint leaves the grid empty without crashing
  Given the API is forced to return 500 on /account/notifications/list (e.g. DB down)
  When the administrator opens /account/notifications
  Then the grid first shows the "Loading notifications…" indicator
  And then renders no rows (the page keeps _page empty when Success is false)
  And the page does not throw a JS exception (Console errors: 0 unhandled)
```

> Today the page has no explicit error toast on a failed `/list` (it just leaves the
> grid empty when `envelope.Success` is false). Flag as a hardening follow-up if a
> red fallback toast is desired here — match the Interests page pattern
> (`docs/tests/e2e/cp-admin-interests.md`, E2E-INT-006).

### E2E-NTF-012 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page, grid and Details modal
  Given the administrator is on /account/notifications in English
  When they switch the language to العربية in the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "الإشعارات"
  And the nav rail and grid mirror to RTL (columns right-to-left)
  And the "New" pill reads "جديد"
  And the "Mark all as read" button reads "تعليم الكل كمقروء"
  And the pager arrows reverse direction

  When the user opens a row's "Details"
  Then the modal opens in RTL
  And the description-list field labels are Arabic
  And the empty-state copy (if shown) reads "لا توجد إشعارات."
```

---

## Implementation notes

- **Manual smoke is the canonical run today.** Until Playwright is adopted, the
  canonical execution is a Chrome DevTools MCP session: sign in per the Auth setup,
  walk each scenario, and capture screenshots into
  `docs/screenshots/cp-account-notifications-{scenario}.png`. Steps are written
  tool-agnostic so they port to a Playwright `.feature` file unchanged.
- **API integration tests cover the same surface at a lower layer (no browser):**
  - `tests/SIMF.Api.Tests/NotificationTests.cs` — list / unread-count / mark-read /
    mark-all / delete, all asserted **actor-scoped** (a user only sees and mutates
    their own rows), plus `Delete_removes_the_row_and_is_idempotent` (covers the
    re-delete in E2E-NTF-010) and `MarkAllRead_clears_every_unread`
    (covers E2E-NTF-005).
  - `tests/SIMF.Api.Tests/NotificationLifecycleTests.cs` — end-to-end notification
    creation → delivery lifecycle.
- **Endpoints exercised** (BFF `simfAccount.*` → API, cookie→bearer forward):
  - `POST /account/api/notifications/list` → API `POST /api/v1/account/notifications/list`
  - `DELETE /account/api/notifications/{id}` → API `DELETE /api/v1/account/notifications/{id}` (idempotent)
  - `POST /account/api/notifications/read-all` → API `POST /api/v1/account/notifications/read-all`
  - (bell-only, not on this page: `GET .../unread-count`, `POST .../{id}/read`)
- **No PermissionCatalog gate** — confirmed: the page declares only
  `@attribute [Authorize]`. Do not author a `/not-permitted` scenario for it; the
  auth gate is the unauthenticated `/login` redirect (E2E-NTF-009).

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
