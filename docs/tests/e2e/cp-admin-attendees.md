# E2E test catalogue — Attendees roster (`/admin/attendees`)

| | |
|--|--|
| **Page** | [`cp/admin-attendees.md`](../../pages/cp/admin-attendees.md) |
| **Route** | `/admin/attendees` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@simrsnf.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-08-18 |

> **Page nature.** This is a **read-only** roster grid (D-134 Sprint A): a
> combined view over `SimfUser` + `UserProfile` + `ProfileType` of every
> non-admin attendee, audience and partner accounts alike. Both carry
> `UserType.Visitor`; the audience-vs-partner split lives on
> `ProfileType.IsVisitor`, not on the user type. There is **no Add / Edit / Delete /
> Details modal** here — creation/editing lives on `/admin/visitors` and
> `/admin/others`. The "golden path" is therefore the **filter → page → sort →
> export** round-trip, not a CRUD cycle. Multiselect checkboxes render (D-132
> mandate) but **no bulk callbacks are wired** — selection has no action.
>
> **Permissions.** View gate = `PermissionCatalog.Attendees.View`
> (`[RequirePermission(PermissionCatalog.Attendees.View)]` on the page, and
> `Policies(PolicyFor(Attendees.View), RequireApprovedAccount)` on the API
> `/admin/attendees/list`). The **Export** button is independently gated by
> `PermissionCatalog.Attendees.Export` (wrapped in `<AuthorizedAction>`) and the
> `/admin/attendees/export` endpoint enforces the same `Attendees.Export` policy.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-ATT-001 | Golden path — load roster → filter → search → sort → page → export XLSX | happy | P0 | _to author_ |
| E2E-ATT-002 | UserType (Kind) filter: Visitors only / All | function | P1 | _to author_ |
| E2E-ATT-003 | AccountState filter — Approved / Pending / Rejected / Any | function | P1 | _to author_ |
| E2E-ATT-004 | Search by email or display name (`Like %term%`) | function | P1 | _to author_ |
| E2E-ATT-005 | Registration date-range filter (From / To, inclusive of the To day) | function | P1 | _to author_ |
| E2E-ATT-006 | Clear filters resets every control and reloads the full roster | function | P1 | _to author_ |
| E2E-ATT-007 | Column sort — Email / Display name / Kind / Registered (asc + desc) | function | P2 | _to author_ |
| E2E-ATT-008 | Pager — page size + First/Prev/Next/Last + summary text | function | P2 | _to author_ |
| E2E-ATT-009 | Profile-type / QR-id rendering — bilingual name, "—" fallbacks, pill colours | function | P2 | _to author_ |
| E2E-ATT-010 | Export action — XLSX download + `Admin.AttendeesExported` audit row | function | P1 | _to author_ |
| E2E-ATT-011 | Empty / no-match state renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-ATT-012 | Auth gate — signed-in user lacking `Attendees.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-ATT-013 | Export auth gate — user lacking `Attendees.Export` sees no Export button; API 403 | auth | P1 | _to author_ |
| E2E-ATT-014 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-ATT-015 | Admins are excluded from the roster | function | P1 | _to author_ |
| E2E-ATT-016 | RTL / Arabic render mirrors page + grid + filter row | i18n | P1 | _to author_ |
| E2E-ATT-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-ATT-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-ATT-001 — Golden path (filter → search → sort → page → export)

```gherkin
Feature: Attendees roster end-to-end
  As an Administrator
  I want to filter, search, sort, page and export the attendee roster
  So that I can answer "who is registered" and hand a list to the team

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator with the Attendees.View and Attendees.Export permissions
      has signed in via /login + /login/totp using the Get-Totp helper
  And they have navigated to /admin/attendees
  And the database contains at least 30 non-admin attendees
      (a mix of audience and partner profile types, Approved +
      PendingApproval + Rejected)

Scenario: Filter, search, sort, page, then export the filtered roster
  When the administrator opens /admin/attendees
  Then a POST /account/api/admin/attendees/list fires and returns HTTP 200
  And the grid renders the seven columns
      "Email", "Display name", "Kind", "Profile type", "State", "QR id", "Registered"
  And rows are ordered newest-first (CreatedAt descending) by default
  And the summary reads "Showing 1–25 of {total}"

  When they choose Kind = "Visitors only"
  And they choose State = "Approved"
  And they type "ahmed" into the "Email or display name contains" field
  And they click "Apply filters"
  Then a POST /account/api/admin/attendees/list fires whose body Filters carry
      userType="Visitor" and accountState="Approved" and Search="ahmed"
  And Skip resets to 0
  And every visible row has the green "Approved" State pill, Kind = "Visitor",
      and "ahmed" appears in either the Email or Display name column

  When they click the "Registered" column header
  Then a list request fires with Sort="createdAt" SortDescending=false
  And the rows re-order oldest-first

  When they set the page size to 50 and click "Last page"
  Then the corresponding list requests fire and the summary reflects the last page

  When they click "Export"
  Then a POST /account/api/admin/attendees/export fires with the SAME Filters + Search
  And the browser downloads a file named "simf-attendees-<UTC timestamp>.xlsx"
  And an OperationLog row is written with Event = "Admin.AttendeesExported"
      and Detail = "count=<exported row count>" and the actor's id
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-attendees-golden-before.png` (full unfiltered roster)
- Screenshot after: `docs/screenshots/cp-admin-attendees-golden-after.png` (filtered + sorted result)
- Console errors: 0 expected
- Network: every `POST /account/api/admin/attendees/list` returns 200; the
  `POST /account/api/admin/attendees/export` returns 200 with content-type
  `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- Audit row: `OperationLog` row with `Event = 'Admin.AttendeesExported'`,
  `Outcome = Success`, `Detail = 'count=N'`, and the actor's id.

### E2E-ATT-002 — UserType (Kind) filter

```gherkin
Scenario: Kind filter offers exactly All and Visitors only
  Given the roster contains audience and partner attendees, all of them
      UserType.Visitor
  When the administrator opens the Kind select
  Then it offers exactly two options, "All" (value "") and
      "Visitors only" (value "Visitor"), and no third option

  When they select Kind = "Visitors only" and click "Apply filters"
  Then the list request body carries Filters["userType"]="Visitor"
  And every visible row's Kind column reads "Visitor"

  When they select Kind = "All" (the empty option) and click "Apply filters"
  Then the list request body carries NO userType filter key
  And the same rows appear, because Admin is the only other UserType and the
      server excludes it outright
  And no admin row ever appears (UserType.Admin is always excluded server-side)
```

> **Known redundancy, not a defect.** `UserType` holds only `(Visitor, Admin)`
> and the roster excludes admins server-side, so "Visitors only" and "All"
> return the identical set and the Kind column is constantly "Visitor". The
> dead third option ("Other") was removed when this scenario was rewritten.
> Whether the control and the column stay at all is an open owner decision.

### E2E-ATT-003 — AccountState filter

```gherkin
Scenario: State filter narrows by account state
  When the administrator selects State = "Approved" and clicks "Apply filters"
  Then the list request body carries Filters["accountState"]="Approved"
  And every visible row shows the green "Approved" pill (SimfPill Variant="on")

  When they select State = "Pending" and click "Apply filters"
  Then the list request body carries Filters["accountState"]="PendingApproval"
  And every visible row shows the "Pending" pill (SimfPill Variant="admin")

  When they select State = "Rejected" and click "Apply filters"
  Then the list request body carries Filters["accountState"]="Rejected"
  And every visible row shows the red "Rejected" pill (SimfPill Variant="off")

  When they select State = "Any" (the empty option) and click "Apply filters"
  Then the list request body carries NO accountState filter key
  And rows of every state appear
```

### E2E-ATT-004 — Search by email or display name

```gherkin
Scenario: Search matches email OR display name (case-insensitive Like)
  Given an attendee exists with Email="fatima.alharbi@example.com"
      and DisplayName="Fatima Al-Harbi"
  When the administrator types "alharbi" into the search field and clicks "Apply filters"
  Then the list request body carries Search="alharbi" (trimmed)
  And the row for fatima.alharbi@example.com is visible

  When they clear the field, type "Fatima" and click "Apply filters"
  Then the same row is matched on DisplayName
  And the search is case-insensitive (server uses EF.Functions.Like '%term%')

  When they type a term that matches nothing and click "Apply filters"
  Then the grid renders the SimfEmptyState (see E2E-ATT-011)
```

### E2E-ATT-005 — Registration date-range filter

```gherkin
Scenario: From / To dates filter on CreatedAt, To inclusive of the whole day
  Given attendees registered across several days
  When the administrator sets "From date" = "2026-05-01"
  And sets "To date" = "2026-05-31"
  And clicks "Apply filters"
  Then the list request body carries Filters["from"]="2026-05-01"
  And Filters["to"]="2026-05-31T23:59:59"   # the page appends T23:59:59 so the To day is inclusive
  And only rows whose Registered date falls within 2026-05-01..2026-05-31 inclusive appear

  When they set only "From date" = "2026-05-15" and clear "To date" then click "Apply filters"
  Then the request carries a "from" filter and NO "to" filter
  And only rows registered on/after 2026-05-15 appear
```

### E2E-ATT-006 — Clear filters

```gherkin
Scenario: Clear resets every control and reloads the full roster
  Given the administrator has applied Kind="Visitors only", State="Approved",
      search="ahmed" and a date range, and the grid is filtered
  When they click "Clear"
  Then the Kind select returns to "All"
  And the State select returns to "Any"
  And the search field is emptied
  And both date inputs are emptied
  And Skip resets to 0 and the Filters/Search on the query are cleared
  And a list request fires with no Filters and null Search
  And the full newest-first roster reappears
```

### E2E-ATT-007 — Column sort

```gherkin
Scenario: Sortable columns toggle ascending / descending
  When the administrator clicks the "Email" header
  Then a list request fires with Sort="email" SortDescending=false and rows sort A→Z by email
  When they click "Email" again
  Then Sort="email" SortDescending=true and rows sort Z→A

  When they click "Display name"
  Then Sort="displayName" and rows sort by display name
  When they click "Kind"
  Then Sort="userType" and rows sort by UserType
  When they click "Registered"
  Then Sort="createdAt" and rows sort by CreatedAt

  # Profile type, State and QR id columns are NOT sortable (Sortable not set) — no sort fires on them.
```

### E2E-ATT-008 — Pager

```gherkin
Scenario: Pager controls page through the roster
  Given the roster has more than 25 rows and the default page size is 25 (GridQuery.Top = 25)
  Then the summary reads "Showing 1–25 of {total}" and the pager reads "Page 1 of {pages}"
  When the administrator clicks "Next"
  Then a list request fires with Skip=25 and the summary reads "Showing 26–50 of {total}"
  When they click "Last page"
  Then the final page loads and the summary's last index equals {total}
  When they change the page size dropdown to 50
  Then a list request fires with Top=50 (server clamps Top to 1..200) and 50 rows render
  When they click "First page"
  Then Skip resets to 0 and the first page reloads
```

### E2E-ATT-009 — Profile-type / QR-id / pill rendering

```gherkin
Scenario: Cells render bilingual profile type, dash fallbacks and coloured pills
  Given an attendee has a ProfileType (e.g. "VIP" / "كبار الشخصيات") and a minted QrId
  Then their "Profile type" cell shows "VIP" in English (ProfileTypeName)
  And shows "كبار الشخصيات" when the UI culture is Arabic (ProfileTypeNameArabic)
  And their "QR id" cell shows the 12-char id

  Given an attendee has no profile type and no minted QR id
  Then their "Profile type" cell shows "—"
  And their "QR id" cell shows "—"

  And Approved rows show the green pill, Pending rows the admin pill,
      Rejected rows the red pill, and any other state renders a plain pill with the raw value
```

### E2E-ATT-010 — Export action + audit

```gherkin
Scenario: Export streams an XLSX of the filtered roster and writes an audit row
  Given the administrator has the Attendees.Export permission
  And they have applied Kind="Visitors only" + State="Approved"
  When they click "Export"
  Then simfAccount.downloadXlsx posts the current query (Filters + Search) to
      /account/api/admin/attendees/export
  And the API returns HTTP 200 with content-type
      application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
  And a Content-Disposition attachment filename "simf-attendees-<UTC timestamp>.xlsx"
  And the workbook contains only the filtered rows (server caps export at 5000 rows — ExportRowCap)
  And an OperationLog row is written with Event="Admin.AttendeesExported",
      Outcome=Success, Detail="count=<rows>" and the actor's id
  And no ApiResult envelope is used (the response is raw binary bytes)
```

### E2E-ATT-011 — Empty / no-match state

```gherkin
Scenario: No matching attendees renders SimfEmptyState
  Given a filter combination that matches zero attendees
      (e.g. State="Rejected" + a search term that no row contains)
  When the administrator clicks "Apply filters"
  Then the list request returns HTTP 200 with an empty page
  And the grid body renders the SimfEmptyState component
  And the empty state shows the bilingual copy
      "No attendees match the current filters." / "لا يوجد حضور يطابق عوامل التصفية الحالية."
  And no error toast appears
  And the filter row and Export button remain visible
```

### E2E-ATT-012 — Auth gate (missing Attendees.View)

```gherkin
Scenario: A signed-in admin lacking Attendees.View is denied
  Given a signed-in Control Panel user whose role does NOT grant Attendees.View
      (and is not the Administrator wildcard "*")
  When they navigate to /admin/attendees
  Then the [RequirePermission(PermissionCatalog.Attendees.View)] attribute denies them
  And they land on /not-permitted with HTTP 200
  And no POST /account/api/admin/attendees/list request fires
  And the "Attendees" nav item (Module.Attendees, RequiredPermission=Attendees.View)
      is hidden from their nav rail
```

### E2E-ATT-013 — Export auth gate (missing Attendees.Export)

```gherkin
Scenario: View-only admin can read the roster but cannot export
  Given a signed-in admin who has Attendees.View but NOT Attendees.Export
  When they open /admin/attendees
  Then the roster loads normally (list request returns 200)
  And the "Export" button is NOT rendered (the AuthorizedAction wrapper hides it)

  When the export endpoint is invoked directly (e.g. a forged request) without the permission
  Then the API /admin/attendees/export returns HTTP 403 (PolicyFor(Attendees.Export) fails)
```

### E2E-ATT-014 — Server 500 on /list

```gherkin
Scenario: API 500 on /list surfaces the bilingual fallback toast
  Given the API is made to return HTTP 500 on /admin/attendees/list (e.g. DB down)
  When the administrator opens /admin/attendees
  Then the grid first shows the loading indicator "Loading attendees…" / "جارٍ تحميل الحضور…"
  And then a red SimfAlert toast appears reading
      "The attendees could not be loaded." / "تعذّر تحميل قائمة الحضور."
      (or the server's MessageForCurrentCulture() when the envelope carries one)
  And no rows render
```

### E2E-ATT-015 — Admins excluded from the roster

```gherkin
Scenario: Admin accounts never appear in the attendee roster
  Given at least one Administrator account exists (e.g. superadmin@simrsnf.com)
  When the administrator opens /admin/attendees with no filters
  Then no row for an Administrator account appears in the grid
  And the total count excludes admins
      (the server filters user.UserType != UserType.Admin)
  And selecting Kind = "All" still excludes admins
```

### E2E-ATT-016 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors page, filter row and grid
  Given the administrator is on /admin/attendees in English
  When they switch the language to "العربية" in the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "الحضور"
  And the Kind filter label reads "النوع" with exactly two options, "الكل" / "الزوار فقط"
  And the State filter label reads "الحالة" with options "الكل" / "مُعتمد" / "معلّق" / "مرفوض"
  And the search label reads "البريد أو الاسم الظاهر يحتوي"
  And the date labels read "من تاريخ" / "إلى تاريخ"
  And the Apply / Clear / Export buttons read "تطبيق التصفية" / "مسح" / "تصدير"
  And the grid headers read "البريد الإلكتروني" / "الاسم الظاهر" / "النوع" /
      "نوع الملف الشخصي" / "الحالة" / "معرف QR" / "تاريخ التسجيل"
  And every row's Kind reads "زائر"
  And the profile-type cell shows the Arabic profile-type name (ProfileTypeNameArabic)
  And the toolbar and pager arrows reverse direction
```

---

## Implementation notes

- **Manual smoke is the canonical "run" today.** Until a Playwright project is
  adopted, drive each scenario through a Chrome DevTools MCP session: sign in
  per the Background, walk each row, capture screenshots into
  `docs/screenshots/cp-admin-attendees-{scenario}.png`. The Gherkin is written
  runner-agnostic so it ports to a `.feature` file later.
- **API integration tests at a lower layer.** The same surface is covered
  without a browser by:
  - `tests/SIMF.Api.Tests/AdminAttendeesTests.cs` — `/admin/attendees/list`
    (filters: userType / accountState / from / to, search, sort, paging, admins
    excluded). Referenced from the `// Tests:` headers on
    `AttendeeEndpoints.cs` and `AdminAttendeeService.cs`.
  - `tests/SIMF.Api.Tests/AdminAttendeesExportTests.cs` — `/admin/attendees/export`
    (XLSX bytes, filter parity with list, the 5000-row cap, and the
    `Admin.AttendeesExported` audit row).
  When an E2E scenario reliably covers one of these, the matching `Api.Tests`
  case can be retired — but keep both during the transition.
- **Permission gate tests.** `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs`
  asserts the `Module.Attendees` nav item carries `RequiredPermission =
  Attendees.View`, and `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs`
  asserts both endpoints are gated (View on `/list`, Export on `/export`) — a
  missing gate fails the build.
- **No CRUD here.** Do not author Add/Edit/Delete/Details scenarios — those
  belong to `/admin/visitors` and `/admin/others`. This page is read-only;
  multiselect renders but has no wired bulk action.

---

_Last reviewed:_ 2026-08-18 by Claude (dead UserType option removed).
