# E2E test catalogue — Audience comments moderation (`/admin/comments-moderation`)

| | |
|--|--|
| **Page** | [`cp/admin-comments-moderation.md`](../../pages/cp/admin-comments-moderation.md) _(reference doc not yet authored)_ |
| **Route** | `/admin/comments-moderation` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (canonical SIMF browser smoke). Convertible to Playwright later — keep scenario steps tool-agnostic. |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **What this page is.** A session-scoped moderation desk (D-199, Mockup page 28
> — "Audience comments" / تعليقات الجمهور). The admin picks one live session from
> a `<select>` dropdown (which sits **above** the grid — it is not a grid column),
> then sees every active comment (Pending / Approved / Hidden) for that session in
> the canonical **`SimfDataGrid`** (D-256 raw-table→grid conversion), each row
> showing Author (+ email), Comment body, Status, the AI filter verdict, and the
> submitted time. The grid pages at **`Top = 20`**, exposes a **per-column filter
> on the Comment column** (the only `Filterable` column) and a **sort on the
> Submitted column** (the only `Sortable` column). Three per-row actions, rendered
> as quiet **icon** affordances in the grid's `RowActions` / `OnDeleteOne` (no
> filled text buttons): **Approve** (check-circle icon), **Hide** (eye-off icon),
> and **Delete** (trash, soft-delete). The grid carries select-all + per-row
> checkboxes (`Multiselect="true"`) but there is **no bulk-action toolbar** — the
> checkboxes are cosmetic here. There is no Add/Create — comments originate from
> the public app. There are no modals: status changes are inline icon actions;
> Delete fires a native `confirm()` dialog.

> **Permission split — read this before authoring the auth-gate.** The page is
> gated by `[RequirePermission(PermissionCatalog.Comments.View)]` (`Comments.View`),
> and the list BFF passthrough `POST /account/api/admin/sessions/{id}/comments/list`
> forwards to an API endpoint gated on `Comments.View`. But **both mutating
> endpoints** (`PUT .../comments/{id}/status` and `DELETE .../comments/{id}`)
> are gated on `Comments.Moderate` — a *distinct* permission. So a user holding
> only `Comments.View` can open the page and read comments but every Approve / Hide
> / Delete is forbidden at the API. `Administrator = "*"` (wildcard) holds both.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-CMT-001 | Golden path — pick session → Approve a Pending comment → Hide it → Delete it | happy | P0 | _to author_ |
| E2E-CMT-002 | Session picker round-trip — switch session A → B reloads the grid + clears stale toast | happy | P1 | _to author_ |
| E2E-CMT-003 | No sessions → `SimfEmptyState` ("No sessions available.") | happy | P1 | _to author_ |
| E2E-CMT-004 | Session with no comments → `SimfEmptyState` ("No comments for this session.") | happy | P1 | _to author_ |
| E2E-CMT-005 | Approve action — Pending/Hidden → Approved, button hides, status pill updates | happy | P0 | _to author_ |
| E2E-CMT-006 | Hide action — Pending/Approved → Hidden, comment drops from public feed | happy | P0 | _to author_ |
| E2E-CMT-007 | Delete (soft-delete) — `confirm()` accepted → row gone after reload | happy | P0 | _to author_ |
| E2E-CMT-008 | Delete cancelled — `confirm()` dismissed → no DELETE fires, row stays | happy | P1 | _to author_ |
| E2E-CMT-009 | Idempotent status — re-Approve an Approved row is a no-op success (button is hidden, so via API) | happy | P2 | _to author_ |
| E2E-CMT-010 | Auth gate — user without `Comments.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-CMT-011 | Permission split — `Comments.View` only: page loads, Moderate actions 403 | auth | P0 | _to author_ |
| E2E-CMT-012 | Not-found — moderate a comment deleted in another tab → 404 `SESSION_COMMENT_NOT_FOUND` | error | P1 | _to author_ |
| E2E-CMT-013 | Server 500 on `/comments/list` → bilingual fallback toast | resilience | P2 | _to author_ |
| E2E-CMT-014 | Server 500 / sessions load failure → bilingual sessions toast | resilience | P2 | _to author_ |
| E2E-CMT-015 | RTL render — Arabic toggle mirrors banner, picker, grid, icon actions | i18n | P1 | _to author_ |
| E2E-CMT-016 | Per-column filter — typing in the Comment filter narrows the grid (maps to `Search`, Skip→0) | happy | P1 | _to author_ |
| E2E-CMT-017 | Column sort — clicking the Submitted header toggles `Sort="created"` asc↔desc | happy | P2 | _to author_ |
| E2E-CMT-018 | Excel export (D-356) — toolbar Export downloads an .xlsx of the picked session's comments (whole set vs selected rows) | happy | P1 | _to author_ |

## Scenarios

### E2E-CMT-001 — Golden path (pick session → Approve → Hide → Delete)

```gherkin
Feature: Audience comments moderation desk golden path
  As an Administrator
  I want to approve, hide and delete a session's audience comments
  So that the public feed only shows comments the forum has cleared

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp (TOTP from Get-Totp)
  And at least one active session exists with code "S-01" titled "Opening Keynote"
  And that session has a Pending comment from "Sara Al-Otaibi" with body "Great session!"
  And they have landed on /admin/comments-moderation

Scenario: Approve, then hide, then delete one comment
  Given the page shows the hint "Select a session to moderate its audience comments."
  And the "Session" <select> lists "S-01 — Opening Keynote"
  When the administrator selects "S-01 — Opening Keynote"
  Then a POST /account/api/admin/sessions/{sessionId}/comments/list fires and returns 200
  And the SimfDataGrid shows a row with Author "Sara Al-Otaibi", Comment "Great session!",
      Status "Pending", and a "Submitted" timestamp
  And that row shows quiet icon actions: Approve (check-circle), Hide (eye-off) and Delete (trash)
  And the grid summary reads "Showing 1–1 of 1"

  When the administrator clicks the row's Approve (check-circle) icon action
  Then a PUT /account/api/admin/sessions/{sessionId}/comments/{commentId}/status fires
      with body { "status": 1 } and returns 200
  And a green toast reads "Comment status updated."
  And the grid reloads and the row's Status reads "Approved"
  And the Approve icon action is no longer rendered on that row (only Hide + Delete remain)

  When the administrator clicks the row's Hide (eye-off) icon action
  Then a PUT .../status fires with body { "status": 2 } and returns 200
  And a green toast reads "Comment status updated."
  And the row's Status reads "Hidden"
  And the Hide icon action is no longer rendered (only Approve + Delete remain)
  And the comment no longer appears on the public app feed for that session

  When the administrator clicks the row's Delete (trash) icon action
  Then a native confirm() dialog appears reading
      "Delete this comment? It will be removed from the public feed immediately."
  When the administrator accepts the dialog
  Then a DELETE /account/api/admin/sessions/{sessionId}/comments/{commentId} fires and returns 200
  And a green toast reads "Comment deleted."
  And the grid reloads and the comment row is gone
  And the empty state "No comments for this session." renders (was the only row)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-comments-moderation-golden-before.png` (session picked, Pending row)
- Screenshot after-approve: `docs/screenshots/cp-admin-comments-moderation-golden-approved.png`
- Screenshot after-delete: `docs/screenshots/cp-admin-comments-moderation-golden-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/sessions/.../comments/...` call returns 200
- Audit rows: `OperationLog` rows with `Event = 'SessionComment.Approved'`, then
  `'SessionComment.Hidden'`, then `'SessionComment.Deactivated'`, each carrying the
  actor's id and `Detail` like `sessionId=...; commentId=...; status=...`.

### E2E-CMT-002 — Session picker round-trip

```gherkin
Scenario: Switching session reloads the grid and clears the stale toast
  Given two active sessions "S-01 — Opening Keynote" and "S-02 — Naval Logistics" exist
  And the administrator has selected "S-01 — Opening Keynote"
  And they have just performed an action that left a green "Comment status updated." toast
  When they change the "Session" <select> to "S-02 — Naval Logistics"
  Then the stale toast is cleared (the message from S-01 does not follow to S-02)
  And a POST .../sessions/{S-02 id}/comments/list fires and returns 200
  And the SimfDataGrid shows S-02's comments only
  When they change the <select> back to "— Select a session —" (the empty option)
  Then the grid is cleared and neither the table nor an empty-state for comments renders
```

### E2E-CMT-003 — No sessions empty state

```gherkin
Scenario: No active sessions renders SimfEmptyState
  Given the database has no active Session rows
  When the administrator opens /admin/comments-moderation
  Then the session <select> is NOT rendered
  And the SimfEmptyState renders with the bilingual title
      "No sessions available." / "لا توجد جلسات متاحة."
  And no /comments/list request fires (there is no session to pick)
```

### E2E-CMT-004 — Session with no comments empty state

```gherkin
Scenario: Selected session with no comments renders SimfEmptyState
  Given an active session "S-03 — Cyber at Sea" exists with zero active comments
  When the administrator selects "S-03 — Cyber at Sea"
  Then a POST .../comments/list fires and returns 200 with an empty Items array
  And the SimfDataGrid's EmptyTemplate renders the SimfEmptyState with the bilingual title
      "No comments for this session." / "لا توجد تعليقات لهذه الجلسة."
  And no comment rows are rendered
```

### E2E-CMT-005 — Approve action

```gherkin
Scenario: Approving a Pending comment promotes it and hides the Approve button
  Given the administrator has selected a session with a "Pending" comment
  When they click the row's Approve (check-circle) icon action
  Then a PUT .../comments/{commentId}/status fires with { "status": 1 } and returns 200
  And the green toast "Comment status updated." appears
  And after the reload the row's Status reads "Approved"
  And the Approve icon action is no longer rendered on that row
  And the comment now appears on the public app feed for that session
```

### E2E-CMT-006 — Hide action

```gherkin
Scenario: Hiding an Approved comment removes it from the public feed
  Given the administrator has selected a session with an "Approved" comment visible on the public feed
  When they click the row's Hide (eye-off) icon action
  Then a PUT .../comments/{commentId}/status fires with { "status": 2 } and returns 200
  And the green toast "Comment status updated." appears
  And after the reload the row's Status reads "Hidden"
  And the Hide icon action is no longer rendered on that row
  And the comment is gone from the public app feed (the feed returns only Approved + active)
```

### E2E-CMT-007 — Delete (soft-delete), confirmed

```gherkin
Scenario: Deleting a comment after confirming removes it from the desk
  Given the administrator has selected a session with at least one comment row
  When they click the row's Delete (trash) icon action
  Then a native confirm() dialog appears reading
      "Delete this comment? It will be removed from the public feed immediately."
  When they accept the dialog
  Then a DELETE .../comments/{commentId} fires and returns 200 (ApiResult.Data = true)
  And the green toast "Comment deleted." appears
  And after the reload the comment row is gone (it is soft-deleted: IsActive = false,
      retained in the table for audit)
```

### E2E-CMT-008 — Delete cancelled

```gherkin
Scenario: Dismissing the confirm dialog leaves the comment untouched
  Given the administrator has selected a session with a comment row
  When they click the row's Delete (trash) icon action
  And they dismiss the native confirm() dialog (Cancel)
  Then NO DELETE request fires
  And the row remains in the SimfDataGrid with its status unchanged
  And no toast appears
```

### E2E-CMT-009 — Idempotent status set

```gherkin
Scenario: Re-setting the same status is a no-op success
  Given a comment is already "Approved"
  When a PUT .../comments/{commentId}/status fires with { "status": 1 } (Approved)
  Then the API returns 200 with the unchanged SessionCommentModerationRow
  And no new audit row is written (the service returns early when status == current)
  # Note: the UI hides the Approve button on an already-Approved row, so this path
  # is exercised at the API / SIMF.Api.Tests layer rather than via a button click.
```

### E2E-CMT-010 — Auth gate (no Comments.View)

```gherkin
Scenario: A signed-in user without Comments.View is denied the page
  Given a signed-in admin user whose role does NOT grant "Comments.View"
      (and is not the wildcard Administrator)
  When they navigate to /admin/comments-moderation
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/sessions/list request fires
  And the "Audience comments" item is hidden in the CP nav rail
      (CpNavigation item RequiredPermission = PermissionCatalog.Comments.View)
```

### E2E-CMT-011 — Permission split (View without Moderate)

```gherkin
Scenario: Comments.View without Comments.Moderate can read but not act
  Given a signed-in user whose role grants "Comments.View" but NOT "Comments.Moderate"
  When they open /admin/comments-moderation and select a session
  Then the page loads and the comment grid renders (the list endpoint requires only Comments.View)
  # Note: the Approve / Hide icon actions are wrapped in <AuthorizedAction
  #   Permission="Comments.Moderate">, so a View-only admin may not even SEE them;
  #   if a stale JWT still renders them, the API gate below is the backstop.
  When they click the row's Approve (or Hide) icon action, or accept the Delete confirm
  Then the BFF forwards to the API and the API returns HTTP 403 Forbidden
      (the status + delete endpoints require Comments.Moderate)
  And a red error toast surfaces the bilingual error message
  And the comment's status does NOT change
```

### E2E-CMT-012 — Not-found (concurrent delete)

```gherkin
Scenario: Acting on a comment already removed elsewhere returns 404
  Given the administrator has the moderation grid open with a comment row
  And that comment was soft-deleted in another session (IsActive = false)
  When the administrator clicks the row's Approve / Hide / Delete icon action on the stale row
  Then the PUT/DELETE fires and the API returns HTTP 404
      with ApiResult.Error.Code = "SESSION_COMMENT_NOT_FOUND"
  And a red error toast surfaces the bilingual message
      "The comment was not found on this session." / "لم يتم العثور على التعليق على هذه الجلسة."
  And the grid reflects the comment's absence after the next reload
```

### E2E-CMT-013 — Server 500 on comments list

```gherkin
Scenario: API 500 on /comments/list shows the fallback bilingual toast
  Given the API is configured to return 500 on .../comments/list (e.g. DB down)
  When the administrator selects a session
  Then the loading text "Loading…" / "جارٍ التحميل…" shows briefly
  And then a red toast appears reading
      "Could not load comments. Please try again." / "تعذر تحميل التعليقات. حاول مرة أخرى."
  And no SimfDataGrid rows render
```

### E2E-CMT-014 — Sessions load failure

```gherkin
Scenario: API 500 on the sessions list shows the sessions fallback toast
  Given the API is configured to return 500 on /account/api/admin/sessions/list
  When the administrator opens /admin/comments-moderation
  Then a red toast appears reading
      "Could not load sessions. Please try again." / "تعذر تحميل الجلسات. حاول مرة أخرى."
  And the session <select> is empty (no options beyond the placeholder)
```

### E2E-CMT-015 — RTL render

```gherkin
Scenario: Arabic toggle mirrors the page
  Given the administrator is on /admin/comments-moderation in English
  When they switch the UI language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "تعليقات الجمهور"
  And the hint reads "اختر جلسة لإدارة تعليقات الجمهور الخاصة بها."
  And the session field label reads "الجلسة" with placeholder "— اختر جلسة —"
  When a session with comments is selected
  Then the SimfDataGrid column headers read "الكاتب", "التعليق", "الحالة",
      "تقييم الذكاء الاصطناعي", "تاريخ الإرسال", "الإجراءات" (mirrored right-to-left)
  And the "التعليق" (Comment) column shows a per-column filter input placeholder "بحث"
  And the "تاريخ الإرسال" (Submitted) header is a sortable button
  And the per-row icon actions carry the tooltips "اعتماد" (Approve), "إخفاء" (Hide), "حذف" (Delete)
  And the status pills render as "قيد الانتظار" / "معتمد" / "مخفي"
  And the grid summary reads "عرض 1–1 من 1"
```

### E2E-CMT-016 — Per-column filter narrows the grid

```gherkin
Scenario: Typing in the Comment column filter narrows the grid (maps to the Search field)
  Given the administrator has selected session "S-01 — Opening Keynote"
  And that session has three active comments, one of whose body contains "logistics"
  And the SimfDataGrid renders all three rows (summary "Showing 1–3 of 3")
  # "Comment" is the only Filterable column on this grid; its filter input carries
  #   aria-label "Filter column Comment". There is no per-column filter on Author,
  #   Status, AI verdict or Submitted.
  When the administrator types "logistics" into the "Filter column Comment" input
  Then after the input debounce a POST .../sessions/{S-01 id}/comments/list fires and returns 200
  And the request body carries Skip = 0 (the page resets) with the typed text mapped to
      the backend "Search" field (the page maps GridQuery.Filters["body"] → ListSessionCommentsBody.Search)
  And the grid narrows to just the row whose Comment contains "logistics" (summary "Showing 1–1 of 1")
  And clearing the filter input fires another list call with Search = null and restores all three rows
```

### E2E-CMT-017 — Column sort toggles

```gherkin
Scenario: Clicking the Submitted header toggles ascending/descending sort
  Given the administrator has selected a session with several comments
  # "Submitted" (Key "created") is the only Sortable column — and the only
  #   server-honoured sort key. Author / Comment / Status / AI verdict are not sortable.
  When the administrator clicks the "Submitted" column header (sortable button)
  Then a POST .../comments/list fires with Sort = "created" and SortDescending = false (ascending)
  And the rows re-order oldest-submitted first and the header shows the ascending arrow (▲)
  And Skip resets to 0 and any row selection is cleared
  When the administrator clicks the "Submitted" header again
  Then a POST .../comments/list fires with Sort = "created" and SortDescending = true (descending)
  And the rows re-order newest-submitted first and the header shows the descending arrow (▼)
```

### E2E-CMT-018 — Excel export (D-356)

```gherkin
Scenario: Export the picked session's comments to an XLSX workbook
  Given the administrator is on /admin/comments-moderation
  And they have selected session "S-01 — Opening Keynote"
  And that session has three active comments (one Pending, one Approved, one Hidden)
  And the SimfDataGrid renders all three rows (summary "Showing 1–3 of 3")
  # Export is the only Excel affordance on this desk — there is NO Import:
  #   comments originate from the public app, so the grid wires OnExport only.
  When the administrator clicks the toolbar "Export" action with no rows selected
  Then the page calls simfAccount.downloadXlsx against
      POST /account/api/admin/comments-moderation/export
  And the request body is an AdminGridExportRequest with an empty Ids list and a Query
      whose Filters carry "sessionId" = {S-01 id} (plus the current Sort + the body filter
      mapped to Search), so the export covers the whole picked-session set across every status
  And the browser saves a file named simf-comments-{timestamp}.xlsx
  And the workbook's "Comments" sheet has the header row
      Author | Email | Body | Status | AiVerdict | Created
  And the sheet has three data rows whose Status cells read "Pending", "Approved" and "Hidden"

  When the administrator instead selects exactly the Approved and Hidden rows then clicks "Export"
  Then the request body carries those two comment ids in Ids (Query still pins the sessionId)
  And the workbook contains exactly those two rows (the unselected Pending row is excluded)

  # Guardrails (verified at the API / SIMF.Api.Tests layer, CommentsExcelTests.cs):
  # the export is capped at 5000 rows, and with no (or an unparseable) "sessionId"
  # filter the endpoint exports nothing rather than dumping every session's comments.
```

**Evidence captured:**
- Screenshot of the saved workbook's header + status rows →
  `docs/screenshots/cp-admin-comments-moderation-export.png`
- Network: the single POST /account/api/admin/comments-moderation/export returns 200
  with an `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` body
- Permission: the export endpoint is gated on `PermissionCatalog.Comments.Export`
  (a *third* permission distinct from `Comments.View` and `Comments.Moderate`)

---

## Implementation notes

- **Manual smoke is the canonical run today.** Until Playwright is adopted, the
  canonical execution of these scenarios is a Chrome DevTools MCP session: sign in
  per the Auth setup, walk each scenario, capture screenshots into
  `docs/screenshots/cp-admin-comments-moderation-{scenario}.png`.
- **No reference page doc yet.** `docs/pages/cp/admin-comments-moderation.md` does
  not exist as of this rebuild; the `Page` link above is forward-declared. Author
  the reference doc when the page-doc backfill reaches the Engagement module.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin scenario
  into a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created) plus
  a step-definition class. The Gherkin shape is already runner-agnostic.
- **API integration tests cover the same surface at a lower layer (no browser):**
  `tests/SIMF.Api.Tests/SessionCommentsTests.cs` —
  `Admin_moderation_list_can_filter_by_status` (CMT-005/006 backing list),
  `Admin_set_status_to_hidden_removes_from_public_feed_and_is_idempotent`
  (CMT-006 + CMT-009 idempotency),
  `Admin_soft_delete_removes_comment_from_feed_and_moderation_list` (CMT-007),
  `Set_status_on_missing_comment_is_SESSION_COMMENT_NOT_FOUND` (CMT-012), and
  `Non_admin_caller_is_forbidden_on_moderation_list` (CMT-010/011 at the API layer).
- **Status enum wire values** (`SessionCommentStatus`): the PUT body sends the
  integer — confirm the exact values in `src/Shared/SIMF.Common/Enums/`
  (Pending / Approved / Hidden). The Gherkin uses `1` = Approved and `2` = Hidden
  per the UI's button-to-status mapping; verify against the frozen enum before
  asserting the raw integer in a runner.
- **Permission split is the highest-value coverage here.** The page gate
  (`Comments.View`) and the action gates (`Comments.Moderate`) are *different*
  permissions — CMT-011 is the scenario that proves a View-only admin cannot
  moderate. Treat a regression there as a security defect.

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle; added the Excel-export scenario E2E-CMT-018).
