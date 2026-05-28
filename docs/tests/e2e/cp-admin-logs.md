# E2E test catalogue — Logs viewer (`/admin/logs`)

| | |
|--|--|
| **Page** | [`cp/admin-logs.md`](../../pages/cp/admin-logs.md) |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-LOG-001 | Pick project Api → file list populates | P1 |
| E2E-LOG-002 | Tail file → 5-second poll updates body | P1 |
| E2E-LOG-003 | Download streams the full file to disk | P1 |
| E2E-LOG-004 | Non-admin → /not-permitted | P0 |

## Scenarios

### E2E-LOG-001 — Pick project

```gherkin
Scenario: Picking a project populates the file list
  Given the admin is on /admin/logs
  When they pick Project=Api from the Project select
  Then GET /account/api/admin/logs/files?project=Api returns the file list
  And the File select populates with the per-day log files (newest first)
```

### E2E-LOG-002 — Tail with auto-refresh

```gherkin
Scenario: Tail with auto-refresh polls every 5 seconds
  Given Project=Api + File=2026-05-28.log + Lines=500 are picked
  And the Auto-refresh checkbox is ticked
  When the page tails the file
  Then GET /account/api/admin/logs/tail?project=Api&file=2026-05-28.log&lines=500 returns
  And the body renders in a monospace <pre>
  When 5 seconds elapse
  Then the tail call fires again
  And new log lines appended since the last poll appear at the bottom
  When the tab loses focus
  Then the poll pauses
```

### E2E-LOG-003 — Download

```gherkin
Scenario: Download streams the full file
  Given Project + File are picked
  When the admin clicks "Download"
  Then GET /account/api/admin/logs/download?project=...&file=... returns the file
  And the browser saves it with the original filename
```

### E2E-LOG-004 — Auth

```gherkin
Scenario: Non-admin user denied
  Given a Visitor signs in
  When they navigate to /admin/logs
  Then they land on /not-permitted with HTTP 200
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
