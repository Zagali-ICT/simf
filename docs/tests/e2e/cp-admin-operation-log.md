# E2E test catalogue — Operation log viewer (`/admin/operation-log`)

| | |
|--|--|
| **Page** | [`cp/admin-operation-log.md`](../../pages/cp/admin-operation-log.md) |
| **Route** | `/admin/operation-log` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Page contract (grounded in source).** Read-only viewer over the durable
> `OperationLogEntry` audit table (D-134 Sprint A). No create / edit / delete —
> the audit log is append-only. Surface affordances are exactly:
> a **filter row** (Event type contains, Subject email contains, Outcome
> Any/Success/Failure, From date, To date) with **Apply filters** + **Clear**
> buttons; an **Export** button (gated by `OperationLog.Export`, separate from
> View); a sortable, paged, multiselect **grid** (Timestamp / Event / Outcome
> pill / Subject email / Source IP); and a per-row **Details** modal.
> - **RequiredPermission (page + list/detail API):** `OperationLog.View`
>   (`@attribute [RequirePermission(PermissionCatalog.OperationLog.View)]`;
>   API `Policies(PolicyFor(OperationLog.View), RequireApprovedAccount)`).
> - **Export permission (button + export API):** `OperationLog.Export`.
> - **BFF routes:** `POST /account/api/admin/operation-log/list`,
>   `GET /account/api/admin/operation-log/{id}`,
>   `POST /account/api/admin/operation-log/export` (binary XLSX, no envelope).
> - **API routes:** `POST /admin/operation-log/list`,
>   `GET /admin/operation-log/{id}`, `POST /admin/operation-log/export`.
> - **Filter keys posted in `GridQuery.Filters`:** `eventType`,
>   `subjectEmail`, `outcome` (`Success`/`Failure`), `from`, `to`
>   (the page appends `T23:59:59` to the `to` date so the whole day is
>   inclusive). All filters are best-effort: bad values are silently ignored
>   server-side (`Enum.TryParse` / `DateTimeOffset.TryParse`).
> - **Export audit event:** an export mints one `OperationLog` row with
>   `EventType = "Admin.OperationLogExported"`, `Outcome = Success`,
>   `ActorUserId = <admin>`, `Detail = "count=<n>"`. A plain **read/browse
>   does NOT mint an audit row** (D-109 fires on writes only).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-OPL-001 | Golden round-trip — default render (newest-first) → filter → open Details → Clear | happy | P0 | _to author_ |
| E2E-OPL-002 | Filter by Event type contains → grid narrows | happy | P1 | _to author_ |
| E2E-OPL-003 | Filter by Outcome = Failure → only failure rows | happy | P1 | _to author_ |
| E2E-OPL-004 | Filter by date range (From / To, inclusive of To day) | happy | P1 | _to author_ |
| E2E-OPL-005 | Subject email contains filter narrows by subject | happy | P2 | _to author_ |
| E2E-OPL-006 | Clear filters resets every input and reloads full list | happy | P1 | _to author_ |
| E2E-OPL-007 | Sort by Event / Outcome / Timestamp columns | happy | P2 | _to author_ |
| E2E-OPL-008 | Pager — page size, Next / Prev / First / Last | happy | P2 | _to author_ |
| E2E-OPL-009 | Details modal renders the full record (correlation id, user agent, detail) | happy | P0 | _to author_ |
| E2E-OPL-010 | Export — XLSX downloads + audit row minted | happy | P0 | _to author_ |
| E2E-OPL-011 | Empty state — no rows match → `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-OPL-012 | Auth gate — signed-in admin lacking `OperationLog.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-OPL-013 | Export gate — admin with View but not `OperationLog.Export` sees no Export button | auth | P1 | _to author_ |
| E2E-OPL-014 | Bad filter values are ignored (no validation error) | error | P2 | _to author_ |
| E2E-OPL-015 | Details on a deleted / unknown id → 404 NotFound, bilingual fallback | error | P2 | _to author_ |
| E2E-OPL-016 | Server 500 on `/list` → bilingual load-failed toast | resilience | P2 | _to author_ |
| E2E-OPL-017 | RTL / Arabic render mirrors page + Details modal | i18n | P1 | _to author_ |

## Scenarios

### E2E-OPL-001 — Golden round-trip

```gherkin
Feature: Operation log viewer golden round-trip
  As an Administrator
  I want to browse, filter and inspect the durable audit trail
  So that I can investigate sign-in / approval / 2FA / password events

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp using
      superadmin@zagali-ict.com and a TOTP from the Get-Totp helper
  And they have landed on /admin/operation-log

Scenario: Default render, filter, open one entry, then clear
  Given at least one SignIn audit event exists (this session just produced one)
  When the page finishes loading
  Then a POST /account/api/admin/operation-log/list fires with empty Filters and returns 200
  And the SimfBanner title reads "Operation log"
  And the grid shows rows ordered newest-first (When (local) descending)
  And each row shows the columns: When (local), Event, Outcome, Subject email, Source IP
  And the most recent row's Event is a sign-in event with a green "Success" pill
  And the pager summary reads "Showing 1–{taken} of {total}"

  When the administrator types "SignIn" into "Event type contains"
  And clicks "Apply filters"
  Then a new POST .../operation-log/list fires with Filters.eventType="SignIn"
  And the grid shows only rows whose Event contains "SignIn"
  And the pager total drops to the filtered count

  When the administrator clicks the "Details" action on the top row
  Then a GET /account/api/admin/operation-log/{id} fires and returns 200
  And a read-only "Audit entry" modal opens
  And it lists When (local), Event, Outcome, Subject email, Subject user id,
      Actor user id, Source IP, User agent, Correlation id, Error code, Detail
  When they click "Close"
  Then the modal closes

  When the administrator clicks "Clear"
  Then every filter input is emptied
  And a POST .../operation-log/list fires with empty Filters
  And the grid shows the full (unfiltered) newest-first list again
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-operation-log-golden-before.png`
- Screenshot after (filtered + Details modal): `docs/screenshots/cp-admin-operation-log-golden-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/operation-log/...` call returns 200
- Audit row: **none** — browsing/filtering the log is itself unaudited
  (D-109 fires on writes only).

### E2E-OPL-002 — Filter by Event type contains

```gherkin
Scenario: Event type contains filter narrows the grid
  Given the grid shows mixed event types
  When the administrator types "Password" into "Event type contains"
  And clicks "Apply filters"
  Then a POST .../operation-log/list fires with Filters.eventType="Password"
  And only rows whose Event contains "Password" (e.g. PasswordReset) render
  And the server matches case-insensitively via LIKE '%Password%'
  And the placeholder hint had read "e.g. SignIn, PasswordReset, Role.Created"
```

### E2E-OPL-003 — Filter by Outcome = Failure

```gherkin
Scenario: Outcome = Failure shows only failure rows
  Given the log contains both Success and Failure entries
  When the administrator selects "Failure" in the Outcome dropdown
  And clicks "Apply filters"
  Then a POST .../operation-log/list fires with Filters.outcome="Failure"
  And every visible row shows the grey "Failure" pill
  And no row shows the green "Success" pill
```

### E2E-OPL-004 — Filter by date range (inclusive To)

```gherkin
Scenario: From / To date range filters by TimestampUtc, To-day inclusive
  Given audit entries exist across several days
  When the administrator picks From date = "2026-05-01"
  And picks To date = "2026-05-31"
  And clicks "Apply filters"
  Then a POST .../operation-log/list fires with Filters.from="2026-05-01"
      and Filters.to="2026-05-31T23:59:59"
  And only rows with TimestampUtc between 2026-05-01 00:00:00
      and 2026-05-31 23:59:59 (inclusive of the whole 31st) render
```

### E2E-OPL-005 — Subject email contains

```gherkin
Scenario: Subject email contains filter narrows by subject
  Given entries exist for several subject emails
  When the administrator types "superadmin@zagali-ict.com" into "Subject email contains"
  And clicks "Apply filters"
  Then a POST .../operation-log/list fires with Filters.subjectEmail="superadmin@zagali-ict.com"
  And only rows whose Subject email contains that string render
  And rows with a null Subject email (shown as "—") are excluded
```

### E2E-OPL-006 — Clear filters

```gherkin
Scenario: Clear resets every filter input and reloads the full list
  Given the administrator has applied an Event type + Outcome + date-range filter
  When they click "Clear"
  Then "Event type contains", "Subject email contains" are emptied
  And the Outcome dropdown returns to "Any"
  And both From date and To date are emptied
  And a POST .../operation-log/list fires with empty Filters and skip reset to 0
  And the grid shows the full newest-first list
```

### E2E-OPL-007 — Column sort

```gherkin
Scenario: Sorting toggles on Event, Outcome and Timestamp columns
  Given the grid is showing the default newest-first order
  When the administrator clicks the "Event" column header
  Then a POST .../operation-log/list fires with Sort="eventType"
  And rows are ordered by Event ascending (then newest-first within an event)
  When they click "Event" again
  Then SortDescending flips to true and the order reverses

  When the administrator clicks the "Outcome" column header
  Then a POST .../operation-log/list fires with Sort="outcome"
  And rows group by Outcome

  When the administrator clicks the "When (local)" header
  Then sorting toggles between oldest-first and the default newest-first
```

### E2E-OPL-008 — Pager

```gherkin
Scenario: Pager page-size and navigation
  Given the log has more than 25 entries (default page size Top=25)
  When the administrator changes the "Show" page size to a larger value
  Then a POST .../operation-log/list fires with the new Top
  And the pager summary "Showing 1–{taken} of {total}" updates
  When they click "Next"
  Then Skip advances by the page size and the next slice loads
  When they click "Last page"
  Then the final slice loads and "Next" is disabled
  When they click "First page"
  Then Skip returns to 0
```

### E2E-OPL-009 — Details modal full record

```gherkin
Scenario: Details modal renders the complete audit record
  Given a failed sign-in entry exists (it carries an ErrorCode and Detail)
  When the administrator clicks "Details" on that row
  Then a GET /account/api/admin/operation-log/{id} fires and returns 200
  And the "Audit entry" modal shows a description list with:
    | Field            | Source                         |
    | When (local)     | TimestampUtc (local)           |
    | Event            | EventType                      |
    | Outcome          | Outcome                        |
    | Subject email    | SubjectEmail or "—"            |
    | Subject user id  | SubjectUserId or "—"           |
    | Actor user id    | ActorUserId or "—"             |
    | Source IP        | SourceIp or "—"                |
    | User agent       | UserAgent or "—"               |
    | Correlation id   | CorrelationId or "—"           |
    | Error code       | ErrorCode or "—"               |
    | Detail           | Detail or "—"                  |
  And there is NO edit or delete control (append-only audit log)
  When they click "Close"
  Then the modal closes and the grid is unchanged
```

### E2E-OPL-010 — Export to XLSX

```gherkin
Scenario: Export downloads the filtered set and mints an audit row
  Given the administrator holds both OperationLog.View and OperationLog.Export
  And they have applied an Event type = "SignIn" filter
  When they click "Export"
  Then a POST /account/api/admin/operation-log/export fires with the same Filters
  And the response is a binary XLSX (content-type
      application/vnd.openxmlformats-officedocument.spreadsheetml.sheet)
  And the browser saves a file named simf-operation-log-<yyyyMMddHHmmss>.xlsx
  And the workbook contains only the filtered rows (capped at 5000)
  And exactly one new audit row is minted with
      EventType="Admin.OperationLogExported", Outcome=Success,
      ActorUserId=<admin>, Detail="count=<n>"
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-operation-log-export.png`
- Downloaded file present: `simf-operation-log-*.xlsx`
- Network: the export POST returns 200 with a non-empty body
- Audit row: `OperationLog` row with `EventType = 'Admin.OperationLogExported'`
  and the actor's id (the export is the only audited action on this page).

### E2E-OPL-011 — Empty state

```gherkin
Scenario: No rows match → SimfEmptyState
  Given a filter combination that matches no entries
      (e.g. Event type contains = "NoSuchEventXYZ")
  When the administrator clicks "Apply filters"
  Then a POST .../operation-log/list returns 200 with an empty page (total 0)
  And the grid body renders the SimfEmptyState component
  And the empty state reads "No audit entries match the current filters."
      / "لا توجد سجلات تطابق عوامل التصفية الحالية."
  And no error toast appears
```

### E2E-OPL-012 — Auth gate (page + list/detail)

```gherkin
Scenario: Admin lacking OperationLog.View is denied
  Given a signed-in admin user whose role does NOT include OperationLog.View
      (its permission claim has no "OperationLog.View" and is not Administrator "*")
  When they navigate to /admin/operation-log
  Then they land on /not-permitted with HTTP 200
  And the page never fires POST /account/api/admin/operation-log/list
  And if the list/detail API is called directly with that token it returns 403 Forbidden
  And the "Module.OperationLog" nav item is hidden for this user
      (CpNavigation RequiredPermission = OperationLog.View)
```

### E2E-OPL-013 — Export-permission gate

```gherkin
Scenario: Admin with View but not Export sees no Export button
  Given a signed-in admin holding OperationLog.View but NOT OperationLog.Export
  When they open /admin/operation-log
  Then the grid, filters, Apply and Clear render normally
  And the "Export" button is NOT rendered
      (it is wrapped in <AuthorizedAction Permission="OperationLog.Export">)
  And if the export API is called directly with that token it returns 403 Forbidden
```

### E2E-OPL-014 — Bad filter values ignored

```gherkin
Scenario: Best-effort filters never raise a validation error
  Given a crafted request carries an unparseable Outcome value
      (the dropdown only offers Any/Success/Failure, but the API is best-effort)
  When the list endpoint receives Filters.outcome="banana"
  Then Enum.TryParse fails and the outcome filter is simply skipped
  And the request still returns 200 with the unfiltered-by-outcome result
  And no error toast appears on the page for normal dropdown use
```

### E2E-OPL-015 — Details on unknown id → 404

```gherkin
Scenario: Detail for a non-existent id returns bilingual NotFound
  Given a row id that no longer exists (or a random Guid)
  When a GET /account/api/admin/operation-log/{id} is issued
  Then the API returns HTTP 404 with ApiResult.Error.Code = "NotFound"
  And the bilingual message is "The audit entry was not found."
      / "لم يتم العثور على سجل التدقيق."
  And the Details modal shows no detail body
```

### E2E-OPL-016 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the bilingual load-failed toast
  Given the API is made to return 500 on /admin/operation-log/list (e.g. DB down)
  When the administrator opens /admin/operation-log
  Then the grid shows the "Loading audit entries…" indicator
  And then a red SimfAlert appears reading
      "The audit entries could not be loaded." / "تعذّر تحميل سجلات التدقيق."
  And no rows render
```

### E2E-OPL-017 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the Details modal
  Given the administrator is on /admin/operation-log in English
  When they switch the UI language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "سجل العمليات"
  And the filter labels read "نوع الحدث يحتوي", "بريد الموضوع يحتوي", "النتيجة",
      "من تاريخ", "إلى تاريخ"
  And the Outcome dropdown options read "الكل" / "نجاح" / "فشل"
  And the buttons read "تطبيق التصفية", "مسح", "تصدير"
  And the grid headers read "الوقت (محلي)", "الحدث", "النتيجة", "بريد الموضوع",
      "عنوان IP المصدر"
  And the toolbar buttons and pager arrows appear in reverse order

  When they open a row's "تفاصيل" (Details)
  Then the modal title reads "سجل التدقيق"
  And the field labels are Arabic (e.g. "معرف الارتباط", "وكيل المستخدم",
      "رمز الخطأ", "التفاصيل")
  And the "إغلاق" (Close) button dismisses it
```

---

## Implementation notes

- **Read-only, append-only by design.** This page has no create / edit /
  delete / deactivate / bulk-mutation surface — every "action" row above is a
  filter, sort, pager, Details, or Export affordance only. Do not author a
  CRUD round-trip; the golden path (E2E-OPL-001) is a browse → filter →
  Details → Clear round-trip instead.
- **Two distinct permissions.** `OperationLog.View` gates the page + the
  list/detail API; `OperationLog.Export` separately gates the Export button +
  the export API. Both gate scenarios (E2E-OPL-012, -013) must be exercised.
- **Manual smoke is canonical today.** Until Playwright is adopted, the
  canonical run is a Chrome DevTools MCP session: sign in per the Background,
  walk each scenario, capture screenshots into
  `docs/screenshots/cp-admin-operation-log-*.png`. Keep the Gherkin
  runner-agnostic so it ports to `tests/SIMF.E2E.Tests/` `.feature` files
  later.
- **API integration tests (lower layer).** `tests/SIMF.Api.Tests/AdminOperationLogExportTests.cs`
  covers the export endpoint (XLSX bytes + the `Admin.OperationLogExported`
  audit row + the `OperationLog.Export` gate) at the API layer without a
  browser. The `// Tests:` headers on `OperationLogEndpoints.cs` and
  `AdminOperationLogService.cs` also reference `AdminOperationLogTests.cs` for
  the list / detail / filter / sort surface — verify that file's presence
  before relying on it (only `AdminOperationLogExportTests.cs` was found on
  disk at authoring time). When an E2E scenario fully covers a surface, the
  matching `Api.Tests` case may be retired — but keep both during the
  transition.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
