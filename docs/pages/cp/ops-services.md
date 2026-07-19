# Background services — `/admin/ops/services`

| | |
|--|--|
| **Route** | `/admin/ops/services` |
| **Audience** | Administrator |
| **Auth** | `[RequirePermission(PermissionCatalog.ServicesMonitor.View)]` |
| **Pattern** | SimfBanner + SimfStatCard roll-up + SimfDataGrid (read-only, auto-refresh). |
| **Status** | ✅ Real |
| **Backend** | `GET /account/api/admin/ops/workers` (BFF) proxying `GET /api/v1/admin/ops/workers` |
| **Source** | [`ServicesMonitor.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ServicesMonitor.razor) |
| **Last reviewed** | 2026-07-18 |

## 1. Purpose

Live health of the in-process background workers (the scheduled jobs hosted in
the API application pool): session reminders, seat-hold expiry, the rating
prompts, the daily NCA sweeps and the e-mail queue drain. Each worker reports a
heartbeat to an in-process registry; this page reads the registry snapshot so an
operator can confirm at a glance that every worker is up.

## 4. UI

- **Roll-up:** three SimfStatCards — Up, Stale, Faulted counts.
- **Refresh line:** "Last refreshed at {time}" + a Refresh button; the grid also
  auto-refreshes every 15 seconds.
- **Grid (SimfDataGrid):** one row per worker with Service (name + description),
  Status pill, Last run, Last success, Runs, Failures and Last error.

## 5. States

| State | Pill | Meaning |
|-------|------|---------|
| Starting | neutral | Registered, still inside its first expected cycle. |
| Up (Healthy) | on | Ticked successfully within its expected window. |
| Stale | warn | No successful tick for longer than twice its interval plus grace. |
| Faulted | danger | Its most recent tick threw; `Last error` carries the message; clears to Up on the next success. |

An event-driven worker (the e-mail queue drain, no fixed interval) is Up once
registered and never goes Stale; a failing send marks it Faulted until the next
successful send.

## 6. Health + logs

- The same registry feeds a `workers` check on `/health`: Healthy when every
  worker is up, Degraded when one is Stale, Unhealthy when one is Faulted.
- Worker logs are written to a separate `SIMF.Workers` folder under
  `{Storage:LogDirectory}`, visible as its own project in `/admin/logs`.

## 7. Edge cases

- **No workers registered** → the grid shows the SimfEmptyState.
- **Transient fetch failure** → a bilingual error toast; the 15s poll keeps
  running and recovers on the next tick.
- **Read-only** → no create / edit / delete; the grid carries no row actions.

## 10. Use cases

UC-SVCM-VIEW, UC-SVCM-HEALTH _(pending UCS)_.

## 11. E2E

See [`e2e/cp-ops-services.md`](../../tests/e2e/cp-ops-services.md) — E2E-SVCM-001..008.

## 12. Related

- Permission: `PermissionCatalog.ServicesMonitor.View`.
- Backend: `WorkerStatusEndpoint.cs`, `WorkerHeartbeatRegistry.cs`, `WorkersHealthCheck.cs`.
- Ops: `deploy/ops.ps1` (start / stop / restart the worker-hosting API pool).

_Last reviewed:_ 2026-07-18 by Claude (background-services monitor).
