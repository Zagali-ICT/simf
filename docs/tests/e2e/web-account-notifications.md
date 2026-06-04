# E2E test catalogue — Notifications inbox (Web) (`/account/notifications`)

| | |
|--|--|
| **Page** | [`web/account-notifications.md`](../../pages/web/account-notifications.md) |
| **Route** | `/account/notifications` |
| **Surface** | Website |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Auth model note.** This is a Website page guarded by `@attribute [Authorize]`
> only — **any** signed-in account reaches it; there is no `PermissionCatalog`
> code and no `/not-permitted` gate (that is a Control-Panel concept). The
> auth-gate scenario here is therefore an **unauthenticated** visitor hitting
> the route and being bounced to the Website sign-in flow. The page shows the
> signed-in user's **own** notifications only — the backing API derives the
> owner from the `sub` claim, never from a request field.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WNT-001 | Golden round-trip — open inbox → "New" pill on unread → delete one row → "Mark all as read" → pills clear | happy | P0 | _to author_ |
| E2E-WNT-002 | Empty inbox renders the grid empty state (`Account.Notifications.Empty`) | happy | P1 | _to author_ |
| E2E-WNT-003 | Delete one notification (per-row trash action) | happy | P0 | _to author_ |
| E2E-WNT-004 | Mark all as read (footer button) → success toast + pills clear | happy | P0 | _to author_ |
| E2E-WNT-005 | Per-column filter (Title) narrows the grid via a fresh `/list` POST | happy | P1 | _to author_ |
| E2E-WNT-006 | Pager — Next / page-size select fetch the next page | happy | P1 | _to author_ |
| E2E-WNT-007 | Column sort header re-queries newest/oldest | happy | P2 | _to author_ |
| E2E-WNT-008 | Auth gate — unauthenticated visitor → Website sign-in redirect | auth | P0 | _to author_ |
| E2E-WNT-009 | Isolation — a user never sees another user's notifications | auth | P0 | _to author_ |
| E2E-WNT-010 | Idempotent delete — deleting an already-gone row still returns 200 | error | P1 | _to author_ |
| E2E-WNT-011 | Server 500 on `/list` → grid stays empty, no unhandled toast | resilience | P2 | _to author_ |
| E2E-WNT-012 | RTL / Arabic render — title, columns, pills, pager mirror; Arabic body text shows | i18n | P1 | _to author_ |

## Scenarios

### E2E-WNT-001 — Golden round-trip

```gherkin
Feature: Visitor notification inbox round-trip
  As a signed-in SIMF user
  I want to read, dismiss, and clear my notifications
  So that my inbox reflects what I have actually seen

Background:
  Given the API is reachable on http://localhost:5175
  And the Website is reachable on http://localhost:5115
  And the test account has signed in via /login + /login/totp
    (superadmin@zagali-ict.com + TOTP from the Get-Totp helper)
  And at least 2 notifications exist for this account, of which at least 1 is unread
  And they have landed on /account/notifications

Scenario: Read, delete one, then mark all read
  Given the page title reads "Notifications" ("الإشعارات" in Arabic)
  And a POST /account/api/notifications/list request fired on load and returned 200
  And the grid renders one row per notification, newest first
  And each unread row (ReadAt is null) shows the blue "New" pill ("جديد")
  And the pager summary reads "Showing 1–{taken} of {total}"

  When the user reads the Title (English column "Title", Arabic "العنوان")
  And the Message column ("Message" / "الرسالة")
  And the Type column shows the Severity text
  And the Received column shows CreatedAt as "yyyy-MM-dd HH:mm" (local time)

  When the user clicks the trash (Delete) action on the first row
  Then a DELETE /account/api/notifications/{id} request fires and returns 200
  And the grid reloads (a fresh POST /list) with one fewer row
  And the pager total decrements by 1

  When the user clicks "Mark all as read" ("تعليم الكل كمقروء")
  Then a POST /account/api/notifications/read-all request fires and returns 200
  And a green SimfAlert appears reading "All notifications marked as read."
    ("تم تعليم جميع الإشعارات كمقروءة.")
  And the grid reloads and no remaining row shows the "New" pill
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/web-account-notifications-golden-before.png` (inbox with "New" pills)
- Screenshot after: `docs/screenshots/web-account-notifications-golden-after.png` (after mark-all-read, no pills + green toast)
- Console errors: 0 expected
- Network: every `/account/api/notifications/...` call returns 200 (`list`, `{id}` DELETE, `read-all`)
- Audit row: notification read/delete are user-self actions on `SimfAppDbContext` — no admin `OperationLog`/`RowAudit` event is expected for self-inbox actions (confirm at the DB layer against `NotificationLifecycleTests.cs` if asserting)

### E2E-WNT-002 — Empty inbox

```gherkin
Scenario: Empty inbox renders the grid empty state
  Given the signed-in account has zero notification rows
  When they open /account/notifications
  Then the POST /account/api/notifications/list returns 200 with Total = 0
  And the grid body renders the empty cell with copy "No notifications."
    ("لا توجد إشعارات.")  -- Account.Notifications.Empty / SimfDataGrid EmptyLabel
  And the pager summary reads "Showing 0–0 of 0"
  And the "Mark all as read" button is still visible
  And no error SimfAlert appears
```

### E2E-WNT-003 — Delete one (per-row trash action)

```gherkin
Scenario: Per-row delete removes a single notification
  Given the inbox shows {N} rows
  When the user clicks the red trash icon on a specific row
    (the SimfDataGrid OnDeleteOne row-end action, title "Delete" / "حذف")
  Then a DELETE /account/api/notifications/{that-id} request fires
  And the API returns ApiResult<bool>.Ok(true) with HTTP 200
  And the page calls LoadAsync() and re-POSTs /account/api/notifications/list
  And the grid now shows {N - 1} rows
  And the deleted row's Title no longer appears
```

### E2E-WNT-004 — Mark all as read

```gherkin
Scenario: Mark all as read clears every "New" pill
  Given the inbox has at least 2 unread rows showing the "New" pill
  When the user clicks "Mark all as read" in the footer actions
  Then a POST /account/api/notifications/read-all (body: null) request fires
  And the API returns ApiResult<bool>.Ok(true) with HTTP 200
  And a green SimfAlert (Variant="success") appears reading
    "All notifications marked as read." / "تم تعليم جميع الإشعارات كمقروءة."
  And LoadAsync() re-POSTs /list
  And no remaining row shows the "New" pill (every ReadAt is now non-null)

Scenario: Mark all as read is idempotent on an already-read inbox
  Given every notification is already read (no "New" pills)
  When the user clicks "Mark all as read"
  Then the POST /account/api/notifications/read-all still returns 200
  And the same green success SimfAlert appears
  And the grid is unchanged
```

### E2E-WNT-005 — Per-column filter

```gherkin
Scenario: Filtering the Title column narrows the grid
  Given the inbox shows several notifications with different titles
  When the user types a term into the Title column's filter input
    (placeholder "Search" / "بحث", aria-label "Filter column Title")
  And waits for the 300 ms debounce
  Then a fresh POST /account/api/notifications/list fires with
    Filters["title"] set and Skip reset to 0
  And the grid shows only rows whose Title matches the term
  And the pager total reflects the filtered count
  When the user clears the filter input
  Then a fresh /list fires without the title filter and the full set returns
```

### E2E-WNT-006 — Pager

```gherkin
Scenario: Paging through the inbox
  Given the account has more than 25 notifications (default Top = 25)
  And the pager summary reads "Showing 1–25 of {total}"
  When the user clicks the Next (chevron-right) control
  Then a POST /list fires with Skip = 25, Top = 25
  And the summary reads "Showing 26–{to} of {total}"
  When the user changes the page-size select to 50
  Then a POST /list fires with Top = 50, Skip reset to 0
  And the summary reads "Showing 1–50 of {total}"
  When the user clicks the Last (chevron-last) control
  Then a POST /list fires landing on the final page
  And Next + Last become disabled on the last page
```

### E2E-WNT-007 — Column sort

```gherkin
Scenario: Sorting by the Received column re-queries the server
  Given the inbox renders newest-first by default
  When the user clicks the sortable "Received" (createdAt) column header
  Then a POST /list fires with Sort = "createdAt" and Skip reset to 0
  And the aria-sort on that header toggles ascending / descending
  And the rows reorder accordingly (oldest first when ascending)
```

### E2E-WNT-008 — Auth gate (unauthenticated)

```gherkin
Scenario: Unauthenticated visitor cannot reach the inbox
  Given there is no authenticated session (cookies cleared)
  When the browser navigates to /account/notifications
  Then the [Authorize] attribute denies the interactive render
  And the visitor is redirected to the Website sign-in flow (/login)
  And no POST /account/api/notifications/list request fires
  And separately: if the BFF /account/api/notifications/list is called without
    the access_token cookie, the endpoint returns HTTP 401 Unauthorized
```

### E2E-WNT-009 — Per-user isolation

```gherkin
Scenario: A user never sees another user's notifications
  Given user A has notifications "Alpha-1", "Alpha-2"
  And user B has a notification "Bravo-1"
  When user B signs in and opens /account/notifications
  Then the POST /list returns only B's rows ("Bravo-1")
  And "Alpha-1" / "Alpha-2" never appear
  And the API derived the owner from the sub claim, ignoring any client-supplied id
  And deleting "Bravo-1" cannot affect A's rows
```

### E2E-WNT-010 — Idempotent delete

```gherkin
Scenario: Deleting an already-removed notification still succeeds
  Given a notification with id {X} has already been deleted
  When a DELETE /account/api/notifications/{X} request fires again
  Then the API returns ApiResult<bool>.Ok(true) with HTTP 200 (DeleteMineAsync is idempotent)
  And the grid reload shows no error toast
  And the row count is unchanged
```

### E2E-WNT-011 — Server 500 on list

```gherkin
Scenario: API 500 on /list leaves the page graceful
  Given the API is forced to return 500 on /account/notifications/list (e.g. DB down)
  When the user opens /account/notifications
  Then the loading spinner ("Loading notifications…" / "جارٍ تحميل الإشعارات…") shows briefly
  And the envelope is not { Success: true, Data: not null }
  And the page keeps the previous (empty) _page — no rows render
  And no unhandled exception surfaces in the browser console
  And _loading returns to false (the finally block runs)
```

### E2E-WNT-012 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the inbox and shows Arabic text
  Given the user is on /account/notifications in English
  When they switch culture to "العربية"
  Then the page reloads with <html dir="rtl" lang="ar">
  And the page title reads "الإشعارات"
  And the column headers read "العنوان" / "الرسالة" / "النوع" / "التاريخ" / "الإجراءات"
  And each row's Title/Message uses the Arabic fields (TitleArabic / BodyArabic)
  And the "New" pill reads "جديد"
  And the "Mark all as read" button reads "تعليم الكل كمقروء"
  And the pager controls + summary mirror to RTL ("عرض {0}–{1} من {2}")
  And the per-row Delete action title reads "حذف"
```

---

## Implementation notes

- **Lower-layer coverage exists.** The same surface is covered at the API layer
  (no browser) by:
  - `tests/SIMF.Api.Tests/NotificationTests.cs` — list / unread-count / mark-read /
    mark-all-read / delete endpoints and the `sub`-claim owner derivation.
  - `tests/SIMF.Api.Tests/NotificationLifecycleTests.cs` — the create →
    surface → read → delete lifecycle and per-user isolation.
  When an E2E scenario here is implemented and green, the matching API.Tests
  case can stay as the fast lower-layer guard (keep both during the transition).
- **Manual smoke is canonical today.** Until Playwright is adopted, the
  canonical run is a Chrome DevTools MCP session: sign in per the Auth setup,
  walk each scenario, and capture screenshots into
  `docs/screenshots/web-account-notifications-{scenario}.png`.
- **Convert to Playwright** later: copy each Gherkin scenario into a `.feature`
  file under `tests/SIMF.E2E.Tests/` + step definitions. The steps are
  written runner-agnostic on purpose.
- **No permission code applies.** Unlike CP admin pages, this Website page is
  gated by `[Authorize]` only — there is nothing to seed in `PermissionCatalog`
  and no `/not-permitted` redirect to assert. The owner is always the signed-in
  user (`sub` claim), which is why E2E-WNT-009 (isolation) is a P0 security check.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
