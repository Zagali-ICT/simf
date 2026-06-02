# E2E test catalogue — System logs viewer (`/admin/logs`)

| | |
|--|--|
| **Page** | [`cp/admin-logs.md`](../../pages/cp/admin-logs.md) |
| **Route** | `/admin/logs` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Permission gate:** the page carries
> `@attribute [RequirePermission(PermissionCatalog.Logs.View)]` (code
> `"Logs.View"`). All three BFF endpoints forward to API endpoints gated by
> `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Logs.View), nameof(AuthorizationPolicies.RequireApprovedAccount))`.
> A signed-in admin lacking `Logs.View` lands on `/not-permitted`.

> **Read-only page — no CRUD.** This viewer creates / edits / deletes nothing.
> The "golden round-trip" is the **pick → tail → live-poll → download** path. The
> only writes it triggers are two audit rows: `Admin.LogViewed` (every tail) and
> `Admin.LogDownloaded` (every download).

> **Endpoint reality check (razor is authoritative):** the page calls
> `GET /account/api/admin/logs/list`, `GET /account/api/admin/logs/tail?project={p}&file={f}&lines={n}`,
> and `GET /account/api/admin/logs/download?project={p}&file={f}`. (The older
> `cp/admin-logs.md` reference doc lists `/projects` + `/files` split endpoints —
> that is stale; the shipped page uses the single `/list` envelope.)

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-LOG-001 | Golden path — open → auto-select first project+file → tail renders → live poll → Download streams | happy | P0 | _to author_ |
| E2E-LOG-002 | Project select switches file list + re-tails first file of new project | function | P1 | _to author_ |
| E2E-LOG-003 | File (Day) select re-tails the chosen file | function | P1 | _to author_ |
| E2E-LOG-004 | Lines select (100/500/1000/5000) re-tails with new line count | function | P1 | _to author_ |
| E2E-LOG-005 | Auto-refresh checkbox OFF stops the 5 s poll; ON restarts it | function | P1 | _to author_ |
| E2E-LOG-006 | Refresh button reloads list + re-tails current file | function | P2 | _to author_ |
| E2E-LOG-007 | Download button → `text/plain` attachment with the bare file name | function | P0 | _to author_ |
| E2E-LOG-008 | Empty state — no log files → `SimfEmptyState`, no filter rows | happy | P1 | _to author_ |
| E2E-LOG-009 | Auth gate — signed-in admin lacking `Logs.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-LOG-010 | Not-found tail — unknown project/file → API 404, body stays empty | error | P1 | _to author_ |
| E2E-LOG-011 | Not-found download — unknown file → API 404, no file saved | error | P1 | _to author_ |
| E2E-LOG-012 | Server 500 on `/list` → empty state / no rows, no unhandled console error | resilience | P2 | _to author_ |
| E2E-LOG-013 | RTL / Arabic render — labels mirror, two-row layout reverses | i18n | P1 | _to author_ |

## Scenarios

### E2E-LOG-001 — Golden path (pick → tail → live poll → download)

```gherkin
Feature: System logs viewer golden path
  As an Administrator with the Logs.View permission
  I want to tail and download per-project Serilog files from the browser
  So that I can diagnose production issues without RDP/SSH onto the box

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And at least one log file exists under {Storage:LogDirectory} (start the API once so Serilog writes one)
  And an Administrator with the Logs.View permission has signed in via /login + /login/totp (TOTP from Get-Totp)
  And they have landed on /admin/logs

Scenario: Open the viewer, watch it tail, and download the active file
  Given the page title reads "System logs · SIMF"
  And the SimfBanner title reads "System logs"
  And the supporting text reads "Per-project log files. The view auto-refreshes; pick a project and a day, then tail or download."
  When the page finishes loading
  Then exactly one GET /account/api/admin/logs/list fires and returns HTTP 200 with Success=true
  And the "Project" select is populated with one <option> per project, each label "{Name} ({FileCount})" (e.g. "SIMF.Api (3)")
  And the first project is auto-selected
  And the "Day" (File) select is populated with that project's files, each label "{FileName} — {Size} ({yyyy-MM-dd HH:mm})" (e.g. "log-20260602.log — 12.4 KB (2026-06-02 09:31)")
  And the first (newest) file is auto-selected
  And the "Lines" select shows 100 / 500 / 1000 / 5000 with 500 pre-selected
  And the "Live tail (5 s)" checkbox is ticked
  And exactly one GET /account/api/admin/logs/tail?project={p}&file={f}&lines=500 fires and returns HTTP 200
  And a muted summary line reads "{FileName} · {Size} · {LineCount} lines"
  And the <pre class="simf-logs-viewer"> block shows the last 500 lines of the file

  When 5 seconds elapse with the file still selected
  Then a second GET /account/api/admin/logs/tail?...&lines=500 fires automatically (the live poll)
  And the <pre> body refreshes with no full-page reload and no flicker of the dropdowns

  When the administrator clicks "Download"
  Then the browser navigates to /account/api/admin/logs/download?project={p}&file={f} (forceLoad)
  And the response is HTTP 200 with Content-Type text/plain and Content-Disposition: attachment; filename="{FileName}"
  And the file saves to disk with the bare file name (no path segments)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-logs-golden-before.png` (page loaded, first project + file auto-selected, tail rendered)
- Screenshot after: `docs/screenshots/cp-admin-logs-golden-after.png` (download dialog / saved file confirmation)
- Console errors: 0 expected
- Network: `GET /account/api/admin/logs/list` → 200, every `GET /account/api/admin/logs/tail` → 200, `GET /account/api/admin/logs/download` → 200 with `text/plain` + `Content-Disposition: attachment`
- Audit rows: one `AuditEntry` row with `EventType = 'Admin.LogViewed'` and `Detail = '{project}/{file}'` per tail; one row with `EventType = 'Admin.LogDownloaded'` and `Detail = '{project}/{file}'` for the download — both with the actor's id and `Outcome = Success`

### E2E-LOG-002 — Project select switches file list

```gherkin
Scenario: Changing the Project select repopulates the Day list and tails its first file
  Given the page has loaded with at least two projects (e.g. "SIMF.Api" and "SIMF.ControlPanel")
  And "SIMF.Api" is selected
  When the administrator selects "SIMF.ControlPanel" in the Project select
  Then the Day (File) select repopulates with SIMF.ControlPanel's files only
  And the first (newest) SIMF.ControlPanel file is auto-selected
  And a GET /account/api/admin/logs/tail?project=SIMF.ControlPanel&file={firstFile}&lines={current} fires and returns 200
  And the <pre> body shows the new file's tail
  And the muted summary line updates to the new file's name / size / line count
```

### E2E-LOG-003 — File (Day) select re-tails

```gherkin
Scenario: Changing the Day select tails the chosen file
  Given the selected project has more than one file
  When the administrator selects an older file in the Day select (e.g. "log-20260531.log")
  Then a GET /account/api/admin/logs/tail?project={p}&file=log-20260531.log&lines={current} fires and returns 200
  And the <pre> body shows that file's tail
  And the muted summary line reads the chosen file's name / size / line count
```

### E2E-LOG-004 — Lines select changes the tail size

```gherkin
Scenario: Changing the Lines select re-tails with the new count
  Given a file is selected and tailed at 500 lines
  When the administrator selects "1000" in the Lines select
  Then a GET /account/api/admin/logs/tail?project={p}&file={f}&lines=1000 fires and returns 200
  And the muted summary line's "{LineCount} lines" reflects up to 1000 lines (or the file's total if smaller)
  When the administrator selects "5000"
  Then a GET ...&lines=5000 fires and returns 200
```

### E2E-LOG-005 — Auto-refresh toggle controls the 5 s poll

```gherkin
Scenario: Unticking "Live tail (5 s)" stops the poll; re-ticking restarts it
  Given a file is selected and the "Live tail (5 s)" checkbox is ticked
  And the page is auto-polling /tail every 5 seconds
  When the administrator unticks "Live tail (5 s)"
  Then no further automatic GET /account/api/admin/logs/tail requests fire while the checkbox stays unticked
  And the <pre> body stays static (no auto-refresh)
  When the administrator re-ticks "Live tail (5 s)"
  Then automatic GET /account/api/admin/logs/tail requests resume on the 5 s cadence (ResetTimer re-arms the System.Timers.Timer)
```

### E2E-LOG-006 — Refresh button

```gherkin
Scenario: Refresh reloads the project list and re-tails the current file
  Given a project + file are selected and tailed
  When the administrator clicks "Refresh"
  Then one GET /account/api/admin/logs/list fires (re-reading the project/file inventory) and returns 200
  And one GET /account/api/admin/logs/tail for the still-selected file fires and returns 200
  And the Refresh button is disabled while a tail is in flight (Disabled="_loadingTail")
  And any newly-created file appears in the relevant project's count and Day list
```

### E2E-LOG-007 — Download streams the full file

```gherkin
Scenario: Download streams the whole file as a text/plain attachment
  Given a project + file are selected (Download is enabled because _selectedFile is non-empty)
  When the administrator clicks "Download"
  Then the browser force-navigates to /account/api/admin/logs/download?project={p}&file={f}
  And the BFF forwards to the API and returns HTTP 200
  And the response Content-Type is text/plain
  And the response Content-Disposition is: attachment; filename="{bare file name}"
  And the saved file is the FULL file, not just the tailed N lines
  And an audit row EventType='Admin.LogDownloaded', Detail='{project}/{file}' is written
```

### E2E-LOG-008 — Empty state

```gherkin
Scenario: No log files renders SimfEmptyState with the bilingual copy
  Given {Storage:LogDirectory} contains no project folders / no log files
  When the administrator opens /admin/logs
  Then GET /account/api/admin/logs/list returns 200 with Projects.Count == 0
  And the page renders the SimfEmptyState component (no filter row, no actions row, no <pre>)
  And the empty-state title reads "No log files yet — start the API, CP or Web so Serilog can create one." (EN)
  And in Arabic it reads "لا توجد ملفات سجل بعد — شغّل الـ API أو لوحة التحكّم أو الموقع حتى ينشئها Serilog."
  And no /tail or /download request fires
  And no error toast appears
```

### E2E-LOG-009 — Auth gate

```gherkin
Scenario: Signed-in admin lacking Logs.View is denied
  Given a signed-in admin whose role does NOT include the Logs.View permission (and is not Administrator "*")
  When they navigate to /admin/logs
  Then the RequirePermission(PermissionCatalog.Logs.View) attribute denies access
  And they land on /not-permitted with HTTP 200
  And no GET /account/api/admin/logs/list request fires
  And the "System logs" nav item is hidden for them (CpNavigation Module.AdminLogs RequiredPermission = Logs.View)
```

### E2E-LOG-010 — Not-found tail

```gherkin
Scenario: Tailing an unknown project/file returns 404 and leaves the body empty
  Given the administrator is on /admin/logs
  When a GET /account/api/admin/logs/tail?project=Bogus&file=does-not-exist.log&lines=500 is issued
  Then the API ILogFileService.TailAsync returns null
  And the API responds HTTP 404 (Send.NotFoundAsync)
  And the BFF Forward surfaces the non-success envelope
  And the page leaves _tail null so the <pre> block does not render
  And no Admin.LogViewed audit row is written (the audit write only runs on a non-null tail)
```

### E2E-LOG-011 — Not-found download

```gherkin
Scenario: Downloading an unknown file returns 404 and saves nothing
  Given the administrator is on /admin/logs
  When a GET /account/api/admin/logs/download?project=Bogus&file=missing.log is issued
  Then the API OpenRead returns null and responds HTTP 404
  And the BFF returns Results.StatusCode(404) (status != 200 || bytes empty)
  And no file is saved to disk
  And no Admin.LogDownloaded audit row is written
```

### E2E-LOG-012 — Server 500 on list

```gherkin
Scenario: API 500 on /list degrades gracefully
  Given the API is configured to return 500 on /admin/logs/list (e.g. the log directory path is unreadable)
  When the administrator opens /admin/logs
  Then GET /account/api/admin/logs/list returns a non-success envelope (Success=false)
  And the page sets _list = null
  And the page renders the SimfEmptyState (the `_list is null` branch) rather than a half-built filter row
  And no /tail request fires
  And the browser console shows no unhandled JS exception
```

### E2E-LOG-013 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the two-row layout
  Given the administrator is on /admin/logs in English
  When they switch the UI language to Arabic ("العربية")
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "سجلات النظام"
  And the supporting text reads "ملفات السجل لكل مشروع. تتحدّث الصفحة تلقائياً؛ اختر مشروعاً ويوماً ثم تابع أو نزّل."
  And the filter labels read "المشروع" / "اليوم" / "الأسطر"
  And the "Live tail (5 s)" checkbox label reads "متابعة حيّة (5 ث)"
  And the action buttons read "تحديث" (Refresh) and "تنزيل" (Download)
  And the two strict rows (filters row 1 / actions row 2) mirror right-to-left
  And the <pre class="simf-logs-viewer"> body keeps the log content left-to-right (logs are technical/LTR) inside the RTL page
```

---

## Implementation notes

- **Read-only diagnostic surface.** The page never mutates domain data — the
  only persisted side effects are the two audit rows (`Admin.LogViewed` per
  tail, `Admin.LogDownloaded` per download), defined in
  `SIMF.Application/Auditing/AuditEvents.cs` (`AdminLogViewed = "Admin.LogViewed"`,
  `AdminLogDownloaded = "Admin.LogDownloaded"`) and written from
  `LogsEndpoints.cs` `TailLogEndpoint` / `DownloadLogEndpoint`. Assert these in
  E2E-LOG-001 / -007.
- **Live poll caveat for the runner.** The 5 s `System.Timers.Timer` keeps
  firing while a file is selected; when scripting E2E-LOG-001/-005, wait at
  least one full interval (≥ 5 s) and count `/tail` requests rather than
  asserting an exact-once. The timer is reset on the Auto-refresh toggle
  (`ResetTimer`) and disposed on `IDisposable.Dispose`.
- **No lower-layer API tests yet.** `LogsEndpoints.cs` carries
  `// Tests: SIMF.Api.Tests/LogsEndpointsTests.cs (todo).` — that file does
  **not** exist. The existing `tests/SIMF.Api.Tests/AuditLogTests.cs` and
  `AdminOperationLogExportTests.cs` cover the **Operation log** page
  (`/admin/operation-log`), a different surface — they do NOT cover this
  Serilog file viewer. Until `LogsEndpointsTests.cs` lands, this E2E catalogue
  is the only coverage of the `list` / `tail` / `download` endpoints; consider
  adding API integration tests for the 404 (`TailAsync` null / `OpenRead` null)
  and the `Logs.View` policy enforcement as the cheap lower layer behind
  E2E-LOG-009/-010/-011.
- **Manual smoke as canonical-source-of-truth today.** Until Playwright is
  adopted, the canonical "run" is a Chrome DevTools MCP session: sign in per
  the Auth setup, walk each scenario, capture screenshots into
  `docs/screenshots/cp-admin-logs-{scenario}.png`.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin
  scenario into a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be
  created) + step definitions. The Gherkin is already runner-agnostic.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
