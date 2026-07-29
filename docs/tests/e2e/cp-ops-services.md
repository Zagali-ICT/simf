# E2E test catalogue — `Background services` (`/admin/ops/services`)

| | |
|--|--|
| **Page** | [`ops-services.md`](../../pages/cp/ops-services.md) |
| **Route** | `/admin/ops/services` |
| **Surface** | Control Panel |
| **Permission** | `ServicesMonitor.View` |
| **Backend** | `GET /account/api/admin/ops/workers` (BFF) which proxies `GET /api/v1/admin/ops/workers` |
| **Test runner** | Chrome DevTools MCP + PowerShell driver |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via `Get-Totp` helper |
| **Last reviewed** | 2026-07-18 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-SVCM-001 | Golden path: roll-up + per-worker grid renders | happy | P0 | authored |
| E2E-SVCM-002 | Auto-refresh (15s) updates without a manual click | happy | P1 | authored |
| E2E-SVCM-003 | Auth gate (admin without `ServicesMonitor.View` → /not-permitted) | auth | P0 | authored |
| E2E-SVCM-004 | Stale worker shows the Stale pill + increments the roll-up | state | P1 | authored |
| E2E-SVCM-005 | Faulted worker shows Faulted + error, then a success clears it | state | P1 | authored |
| E2E-SVCM-006 | `/health` `workers` check reflects the tier | health | P1 | authored |
| E2E-SVCM-007 | Worker logs land in the separate `SIMF.Workers` log project | logs | P2 | authored |
| E2E-SVCM-008 | RTL (Arabic) render | i18n | P1 | authored |
| E2E-SVCM-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-SVCM-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-SVCM-001 — Golden path

```gherkin
Feature: Background-services monitor golden path
  As an administrator holding ServicesMonitor.View
  I want to see every background worker's live state
  So that I can confirm the scheduled jobs are all up

Background:
  Given an Administrator is signed in
  And the API has been running long enough for the workers to register

Scenario: The monitor lists every worker with a status
  When the administrator opens /admin/ops/services
  Then a roll-up shows the Up, Stale and Faulted counts
  And a grid lists each registered worker (e.g. SessionReminderWorker,
      EmailBackgroundService, RetentionSweepWorker)
  And each row shows a status pill, last run, last success, run count,
      failure count and last error
  And a worker whose last tick succeeded shows the "Up" pill
  And a worker still inside its first cycle shows the "Starting" pill
```

**Evidence captured:**
- Screenshot: `docs/screenshots/ops-services-golden-after.png`
- Console errors: 0 expected
- Network failures: 0 expected (the BFF `GET /account/api/admin/ops/workers` returns 200)

### E2E-SVCM-002 — Auto-refresh

```gherkin
Scenario: The grid refreshes on its own
  Given the administrator is on /admin/ops/services
  When 15 seconds pass without any interaction
  Then the "Last refreshed at {time}" line updates
  And a worker whose state changed in that window shows its new pill
  And no full page reload occurs
```

### E2E-SVCM-003 — Auth gate

```gherkin
Scenario: An admin without the permission is denied
  Given a signed-in admin whose role does not grant ServicesMonitor.View
  When they navigate to /admin/ops/services
  Then they are redirected to /not-permitted with HTTP 200
  And the "Background services" item is absent from their side menu
```

### E2E-SVCM-004 — Stale worker

```gherkin
Scenario: A worker that stops ticking is flagged Stale
  Given a periodic worker has not completed a successful tick for longer than
      twice its interval plus the grace window
  When the administrator refreshes /admin/ops/services
  Then that worker's row shows the "Stale" pill (warn)
  And the roll-up Stale count is at least 1
```

### E2E-SVCM-005 — Faulted worker then recovery

```gherkin
Scenario: A failing tick surfaces the error, and a later success clears it
  Given a worker's most recent tick threw "boom"
  When the administrator opens /admin/ops/services
  Then that worker's row shows the "Faulted" pill (danger)
  And its Last error column shows "boom"
  And its failure count is at least 1
  When the worker next completes a successful tick
  And the administrator refreshes
  Then that worker shows the "Up" pill
  And its Last error column is empty (the stale error is cleared)
```

### E2E-SVCM-006 — /health reflects the tier

```gherkin
Scenario: The workers health check drives readiness
  Given every worker is up
  When a monitor calls GET /health
  Then the response is Healthy and the "workers" check reports the worker count
  Given at least one worker is Stale
  When a monitor calls GET /health
  Then the "workers" check reports Degraded and names the stale worker
  Given at least one worker is Faulted
  Then the "workers" check reports Unhealthy and names the faulted worker
```

### E2E-SVCM-007 — Separate worker logs

```gherkin
Scenario: Worker logs are filed under their own project
  Given the workers have emitted log lines
  When the administrator opens /admin/logs
  Then a "SIMF.Workers" project is listed alongside "SIMF.Api"
  And the SIMF.Workers files contain the worker log lines
  And the SIMF.Api files do not contain those worker lines
```

### E2E-SVCM-008 — RTL render

```gherkin
Scenario: Arabic layout mirrors correctly
  Given the administrator's language is Arabic
  When they open /admin/ops/services
  Then the page renders right-to-left
  And the column headers, pills and roll-up read in Arabic
  And scrollWidth equals clientWidth (no horizontal overflow)
```

---

_Last reviewed:_ 2026-07-18 by Claude (background-services monitor).
