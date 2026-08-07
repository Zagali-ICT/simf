# E2E test catalogue — AI invocations log (`/admin/ai/invocations`)

| | |
|--|--|
| **Page** | [`cp/admin-ai-invocations.md`](../../pages/cp/admin-ai-invocations.md) _(reference doc not yet authored)_ |
| **Route** | `/admin/ai/invocations` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@simrsnf.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-03 |

> **Page shape (read this first).** This page is the **D-176 / G12
> append-only AI invocations log** — a **read-only** SOC + product
> regression view. There is **NO create / edit / delete / details
> modal** on this page, and no per-row actions (the grid has no
> `<RowActions>`). As of **D-256** the page was migrated from the raw
> `simf-table` to the canonical **`SimfDataGrid`** (owner-mandated). The
> complete interactive surface is now:
>
> 1. one **"Errors only"** filter checkbox (`SimfCheckbox`), kept as a
>    `<CustomToolbar>` control on the grid, and
> 2. an 8-column `SimfDataGrid` (Time, Prompt key, Feature, Provider,
>    Caller, Latency, Tokens (in/out), Error) with **per-column sort**
>    on Time/Prompt key/Feature/Provider/Caller/Latency, **per-column
>    filter** on **Prompt key** (`promptKey`) and **Caller** (`callerKind`),
>    a full numbered **pager**, a `SimfEmptyState` when empty (via
>    `<EmptyTemplate>`) and a one-line summary footer.
>
> The page loads on init via a single BFF call —
> `POST /account/api/admin/ai/invocations/list` (forwarded by the CP
> BFF to the API `POST /admin/ai/invocations/list`) — with a page size
> of `Top = 20` (`new GridQuery { Top = 20 }`). The grid **does** have a
> numbered pager, so `GridQuery.Skip` advances per page; the default
> order is newest-first (`ORDER BY CreatedAt DESC`) until the user sorts
> a column. The error column renders a
> red `SimfPill Variant="off"` carrying the row's `ErrorCode`.
> A per-row SOC drill-down endpoint (`GET /admin/ai/invocations/{id}`,
> audit event `AiInvocation.Viewed`) exists at the API layer but is
> **not wired to any UI element on this page** — do not author a
> "click row → detail" scenario; it would test UI that isn't there.
> Gate permission: `PermissionCatalog.AiInvocations.View`
> (`"AiInvocations.View"`, baseline `AdminOnly`).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-AIV-001 | Golden path — load log, read newest-first rows, summary footer | happy | P0 | _to author_ |
| E2E-AIV-002 | "Errors only" filter ON → only error rows; OFF → full list | happy | P0 | _to author_ |
| E2E-AIV-003 | Error rows render the red `SimfPill` with the `ErrorCode` | happy | P1 | _to author_ |
| E2E-AIV-004 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-AIV-005 | Filter result empty → empty state, no error toast | happy | P1 | _to author_ |
| E2E-AIV-006 | Auth gate — admin lacking `AiInvocations.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-AIV-007 | Page size — 20 rows per page, pager advances Skip, summary `1–20 of {Total}` | boundary | P2 | _to author_ |
| E2E-AIV-008 | Read-only surface — no Add/Edit/Delete controls present | happy | P1 | _to author_ |
| E2E-AIV-009 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-AIV-010 | RTL / Arabic render — banner, columns, checkbox mirror | i18n | P1 | _to author_ |
| E2E-AIV-011 | Per-column grid filter (Prompt key / Caller) narrows the grid | happy | P1 | _to author_ |
| E2E-AIV-012 | Column sort toggles (Time / Prompt key) ascending ↔ descending | happy | P2 | _to author_ |
| E2E-AIV-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-AIV-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-AIV-001 — Golden path (load log)

```gherkin
Feature: AI invocations log golden path
  As an Administrator (SOC / product)
  I want to read the append-only log of every AI call the platform made
  So that I can spot provider regressions, latency spikes and errors

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp
  And the SIMF_App database has at least one row in the AiInvocations table
  And they have navigated to /admin/ai/invocations

Scenario: Open the log and read the newest invocations
  When the page initialises
  Then exactly one POST /account/api/admin/ai/invocations/list call fires
  And the request body is { "skip": 0, "top": 20, "filters": {} }
  And the call returns HTTP 200 with ApiResult.success = true
  And the "Loading…" text is replaced by the SimfDataGrid
  And the grid header shows exactly these 8 columns in order:
    | Time | Prompt key | Feature | Provider | Caller | Latency | Tokens (in/out) | Error |
  And rows are ordered newest-first by Time (CreatedAt descending)
  And the first row's Time renders as "yyyy-MM-dd HH:mm:ss UTC"
  And the Prompt key cell renders inside a <code> element
  And the Feature cell shows an AiFeature name (e.g. "QuestionFilter", "Translate")
  And the Provider cell shows an AiProvider name (e.g. "Echo", "OpenAi")
  And the Latency cell renders the integer milliseconds followed by "ms"
  And the Tokens cell renders "{in} / {out}" using "—" where a token count is null
  And rows with no ErrorCode show an empty Error cell (no pill)
  And the summary footer reads "Showing 1–{shown} of {Total}"
```

**Evidence captured:**
- Screenshot before (loading): `docs/screenshots/cp-admin-ai-invocations-loading.png`
- Screenshot after (grid): `docs/screenshots/cp-admin-ai-invocations-golden.png`
- Console errors: 0 expected
- Network: the single `/account/api/admin/ai/invocations/list` call returns 200; no other admin API call fires on load
- Audit row: **none** — listing the grid is a read with no audit write. (The audit event `AiInvocation.Viewed` is emitted only by the per-row detail endpoint, which this page does not call.)

### E2E-AIV-002 — "Errors only" filter toggles the list

```gherkin
Scenario: Errors-only checkbox filters to failed invocations and back
  Given the log contains a mix of rows — some with ErrorCode = null and some with a non-null ErrorCode
  And the administrator is on /admin/ai/invocations with the full list shown
  When they tick the "Errors only" checkbox
  Then a new POST /account/api/admin/ai/invocations/list fires
  And the request body filters are { "errorOnly": "true" }
  And the call returns HTTP 200
  And every visible row shows a non-empty Error cell (a red pill with an ErrorCode)
  And the summary footer total reflects only the error rows

  When they untick the "Errors only" checkbox
  Then a new POST /account/api/admin/ai/invocations/list fires
  And the request body filters are {} (empty)
  And the full mixed list is shown again
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-ai-invocations-errors-only.png`
- Network: two `/list` calls — the filtered one carries `filters.errorOnly = "true"`, the cleared one carries an empty `filters` object
- Console errors: 0 expected

### E2E-AIV-003 — Error pill render

```gherkin
Scenario: A failed invocation renders its ErrorCode in a red pill
  Given the log contains a row whose ErrorCode is "AiProviderTimeout" (any non-null code)
  When the administrator opens /admin/ai/invocations
  Then that row's Error cell renders a SimfPill with Variant="off"
  And the pill text equals the row's ErrorCode verbatim
  And a row whose ErrorCode is null renders no pill in its Error cell
```

### E2E-AIV-004 — Empty state

```gherkin
Scenario: No invocations recorded yet renders SimfEmptyState
  Given the AiInvocations table is empty (a fresh environment that has made no AI calls)
  When the administrator opens /admin/ai/invocations
  Then the POST /account/api/admin/ai/invocations/list call returns 200 with an empty Items array and Total = 0
  And the grid renders no data rows
  And the page shows the SimfEmptyState component (via the grid's EmptyTemplate)
  And the empty-state title reads "No invocations recorded yet." / "لا توجد استدعاءات حتى الآن."
  And no error toast (SimfAlert) appears
  And the "Errors only" checkbox is still present and interactive
```

### E2E-AIV-005 — Filter yields empty set

```gherkin
Scenario: Errors-only filter on a clean log shows the empty state
  Given the log contains rows but none of them have an ErrorCode
  And the administrator is on /admin/ai/invocations
  When they tick the "Errors only" checkbox
  Then the POST /list call returns 200 with an empty Items array and Total = 0
  And the SimfEmptyState renders in place of the table
  And no error toast appears
  And unticking "Errors only" restores the full list
```

### E2E-AIV-006 — Auth gate

```gherkin
Scenario: A signed-in admin lacking AiInvocations.View is denied
  Given a user is signed in to the Control Panel
  And their role does NOT grant PermissionCatalog.AiInvocations.View ("AiInvocations.View")
  And they are NOT an Administrator (Administrator holds the "*" wildcard)
  When they navigate to /admin/ai/invocations
  Then the [RequirePermission(PermissionCatalog.AiInvocations.View)] attribute blocks render
  And they land on /not-permitted with HTTP 200
  And no /account/api/admin/ai/invocations/list request fires
  And the "Module.AiInvocations" nav item is hidden for this user (RequiredPermission unmet)
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-ai-invocations-not-permitted.png`
- Network: zero `/account/api/admin/ai/invocations/*` calls
- Covered at the API layer by `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` (a request to `POST /admin/ai/invocations/list` without the policy returns 403).

### E2E-AIV-007 — Page-size cap (20 per page) + pager

```gherkin
Scenario: The page shows 20 rows per page and pages through the rest
  Given the AiInvocations table holds more than 20 rows (e.g. 137)
  When the administrator opens /admin/ai/invocations
  Then the request body sends "top": 20 and "skip": 0
  And the grid renders exactly 20 rows
  And those 20 rows are the most recent by CreatedAt (descending)
  And the summary footer reads "Showing 1–20 of 137"

  When they click the grid's Next page control
  Then a new POST /account/api/admin/ai/invocations/list fires with "skip": 20 and "top": 20
  And the grid renders the next 20 rows
  And the summary footer reads "Showing 21–40 of 137"
    # The SimfDataGrid carries a full numbered pager (Prev / Next / First / Last + page size);
    # older rows are reachable in-page, no longer only via the API directly.
```

### E2E-AIV-008 — Read-only surface guard

```gherkin
Scenario: The page exposes no mutation controls
  Given the administrator is on /admin/ai/invocations with rows shown
  Then there is NO "Add" / "New invocation" button anywhere on the page
  And the grid renders NO Actions column and NO per-row icon actions (no Edit pencil, no Delete trash, no Details)
  And the only mutation-free interactive controls are the "Errors only" checkbox, the per-column sort/filter affordances and the pager
  And no row is clickable (the row-level detail drill-down is an API-only endpoint, not wired here)
```

### E2E-AIV-009 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the bilingual fallback toast
  Given the API is made to return HTTP 500 on POST /admin/ai/invocations/list (e.g. SIMF_App DB unavailable)
  When the administrator opens /admin/ai/invocations
  Then the "Loading…" text shows first
  And then a SimfAlert with Variant="error" appears at the top of the surface
  And it reads either the server's MessageForCurrentCulture() or the fallback
      "Could not load invocations." / "تعذّر تحميل سجلّ الاستدعاءات."
  And the grid renders no data rows
  And no rows appear
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-ai-invocations-500.png`
- Network: the `/list` call returns 500 (or `success = false`); the page surfaces the toast rather than throwing.

### E2E-AIV-010 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and column headers
  Given the administrator is on /admin/ai/invocations in English
  When they switch the UI language to العربية from the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "سجلّ استدعاءات الذكاء الاصطناعي"
  And the column headers read, right-to-left:
    | الوقت | مفتاح المحفّز | الميزة | الموفّر | المستدعي | زمن الاستجابة | الرموز (داخل/خارج) | الخطأ |
  And the "Errors only" checkbox label reads "الأخطاء فقط"
  And the summary footer reads "عرض 1–{shown} من {Total}"
  And the nav rail mirrors with Arabic labels
  And (empty state, if applicable) the SimfEmptyState reads "لا توجد استدعاءات حتى الآن."
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-ai-invocations-rtl.png`
- Console errors: 0 expected

### E2E-AIV-011 — Per-column grid filter narrows the grid

```gherkin
Scenario: Typing in a column filter input narrows the grid and resets paging
  Given the administrator is on /admin/ai/invocations with the full list shown
  And the log contains rows for several Prompt keys (e.g. "session-summary", "question-filter")
  And the SimfDataGrid exposes per-column filter inputs only on "Prompt key" and "Caller"
  When they type "session-summary" into the "Filter column Prompt key" input
  Then a new POST /account/api/admin/ai/invocations/list fires
  And the request body filters carry "promptKey": "session-summary"
  And the request body sends "skip": 0 (paging resets to the first page)
  And the call returns HTTP 200
  And every visible row's Prompt key cell contains "session-summary"
  And the summary footer total reflects only the matching rows

  When they clear the "Prompt key" filter and type "Admin" into the "Filter column Caller" input
  Then a new POST .../list fires with filters carrying "callerKind": "Admin" and "skip": 0
  And every visible row's Caller cell contains "Admin"
  # Only Prompt key (promptKey) and Caller (callerKind) are Filterable; Time, Feature,
  # Provider, Latency, Tokens and Error expose NO per-column filter input.
```

```gherkin
Scenario: The per-column filter coexists with the "Errors only" toolbar toggle
  Given the administrator has ticked the "Errors only" checkbox so the grid shows only error rows
  When they additionally type "Admin" into the "Filter column Caller" input
  Then the POST .../list request body filters carry BOTH "errorOnly": "true" AND "callerKind": "Admin"
  And the "Errors only" toggle is re-applied on every grid query change (it is not lost by the filter)
  And the grid shows only error rows whose Caller contains "Admin"
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-ai-invocations-col-filter.png`
- Network: the filtered `/list` call carries `filters.promptKey` (or `filters.callerKind`) and `skip = 0`
- Console errors: 0 expected
- Note: the backend `AdminAiPromptService` honours `promptkey` / `callerkind` (case-insensitive substring) and `erroronly` together; unknown columns are ignored.

### E2E-AIV-012 — Column sort toggles

```gherkin
Scenario: Clicking a sortable column header toggles ascending and descending
  Given the administrator is on /admin/ai/invocations with rows shown
  And the grid defaults to newest-first (Sort unset → CreatedAt descending)
  When they click the "Prompt key" column header
  Then a new POST /account/api/admin/ai/invocations/list fires
  And the request body carries "sort": "promptKey" with "sortDescending": false
  And the rows render ordered by Prompt key A→Z

  When they click the "Prompt key" column header again
  Then a new POST .../list fires with "sort": "promptKey" and "sortDescending": true
  And the rows render ordered by Prompt key Z→A

  When they click the "Time" column header
  Then a new POST .../list fires with "sort": "createdAt"
  And the rows re-order by CreatedAt
  # Sortable columns: Time (createdAt), Prompt key (promptKey), Feature (feature),
  # Provider (provider), Caller (callerKind), Latency (latencyMs).
  # Tokens and Error are NOT sortable.
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-ai-invocations-sort.png`
- Network: each header click fires one `/list` call carrying the `sort` key and the toggled `sortDescending`
- Console errors: 0 expected

---

## Implementation notes

- **Read-only by design (D-176 / D-179), grid since D-256.** The page is
  deliberately an append-only viewer: no create/edit/delete, no per-row
  actions. Since the D-256 `SimfDataGrid` migration it paginates with
  `Top = 20` and a full numbered pager, defaulting to `CreatedAt DESC`
  until the user sorts. The UI now surfaces three server-side query knobs:
  the `errorOnly` toolbar toggle, plus per-column filters on `promptKey`
  and `callerKind`, and per-column sort on
  `createdAt`/`promptKey`/`feature`/`provider`/`callerKind`/`latencyMs`.
  The service also honours a `feature` filter, but no column surfaces it.
  Do not author CRUD scenarios — the surface has none.
- **No audit write on this page.** Listing the grid performs no audit
  write. The `AiInvocation.Viewed` audit event (`AuditEvents.AiInvocationViewed`)
  is emitted only by the per-row detail endpoint
  `GET /admin/ai/invocations/{id}` (`GetAiInvocationDetailEndpoint`), which
  is rate-limited (`"ai-test"`) and is **not** invoked from this page.
- **API integration tests** at `tests/SIMF.Api.Tests/AiModuleTests.cs` and
  `tests/SIMF.Api.Tests/AiHardeningTests.cs` cover the invocations list +
  detail + redaction surface at a lower layer (no browser). The permission
  gate is asserted by `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs`
  and the CP nav/permission wiring by
  `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs`.
- **BFF path note.** The CP page calls `/account/api/admin/ai/invocations/list`
  via `simfAccount.postJson`; the CP BFF (`AccountEndpoints.cs`) forwards it
  to the admin API through `SimfAdminClient.ListAiInvocationsAsync`
  (relative `ai/invocations/list` against the client's `/admin` base).
- **Convert to Playwright** when the runner is adopted: each Gherkin
  scenario maps to a `.feature` file under `tests/SIMF.E2E.Tests/`
  (project to be created) plus a step-definition class. The shape is
  already runner-agnostic.

---

_Last reviewed:_ 2026-06-03 by Claude (E2E catalogue rebuild) (D-256/D-257 grid affordances reconciled).
