# Operation log viewer — `/admin/operation-log`

| | |
|--|--|
| **Route** | `/admin/operation-log` |
| **Layout** | `CpShellLayout` |
| **Audience** | Administrator |
| **Auth** | `[Authorize(Roles = "Administrator")]` + `RequireApprovedAccount` |
| **Pattern** | D-117 + D-132 canonical read-only grid (no toolbar mutations). |
| **Status** | ✅ Real (D-134 Sprint A) |
| **Backend endpoints** | `POST /account/api/admin/operation-log/list`, `GET /account/api/admin/operation-log/{id}` |
| **Source** | [`OperationLogViewer.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/OperationLogViewer.razor), [`AdminOperationLogService`](../../../src/Backend/SIMF.Infrastructure/Identity/AdminOperationLogService.cs) |
| **Backed by** | the existing `OperationLogEntry` table — **no schema change**. |
| **Tests** | [`docs/tests/e2e/cp-admin-operation-log.md`](../../tests/e2e/cp-admin-operation-log.md) |
| **Last reviewed** | 2026-05-29 |

## 1. Purpose

Read-only browse + filter over SIMF's **business + security audit
trail** (the `OperationLogEntry` table, populated by `IAuditLog` from
every sign-in / registration / approval / password / 2FA event). Distinct
from `/admin/logs` (D-117 §11.1) — that page tails the raw Serilog text
files; this one queries the structured durable audit table.

## 4. UI affordances

- **Banner.** `<SimfBanner Title="@L[\"Admin.OperationLog.Title\"]" />`
  → EN "Operation log" / AR "سجل العمليات".
- **Filter row.** Three inputs — Event type contains, Subject email
  contains, Outcome (Any / Success / Failure) — plus Apply + Clear
  buttons. The grid posts the filters via `GridQuery.Filters` so the
  server can index-seek (`IX_OperationLog_EventType_TimestampUtc`).
- **Grid columns.** Timestamp (local), Event, Outcome (pill — green
  Success / grey Failure), Subject email, Source IP. Sortable on
  Timestamp / Event / Outcome.
- **Per-row Details modal.** Full record including SubjectUserId,
  ActorUserId, UserAgent, CorrelationId, ErrorCode, Detail. No edit /
  delete — the audit log is append-only.
- **Pager + RTL** match the canonical pattern.

## 5. Data flow

```
Page init → POST /admin/operation-log/list (GridQuery with empty Filters)
  → AdminOperationLogService.ListAsync — filter, sort, page, AsNoTracking
  → ApiResult<GridPage<AdminOperationLogSummary>>
Per-row Details click → GET /admin/operation-log/{id}
  → AdminOperationLogService.GetAsync → AdminOperationLogDetail
```

## 6. Validation + error handling

- All inputs are best-effort filters; bad values are simply ignored
  (server uses `Enum.TryParse` / `Guid.TryParse` / `DateTimeOffset.TryParse`).
- Unknown id on Details → 404 `NotFound` → bilingual fallback.
- The audit log is append-only — no toast/error path for delete or edit
  because there's no such surface.

## 7. Edge cases + known limitations

- **No bulk operations** — read-only by design.
- **Export to XLSX deferred** — the master plan §3.1.2 mentioned it;
  ships in a follow-up so this slice stays minimum-viable.
- **Filter by date range deferred** — the server accepts `from`+`to`
  but the page doesn't yet expose date pickers. Filterable via direct
  API call meanwhile.
- **No audit row mints** when an admin reads this page — D-109 fires on
  writes only. Browsing the audit log is itself unaudited; if that's
  ever needed, a dedicated `Admin.AuditLogViewed` event would have to
  land in `AdminOperationLogService.ListAsync`.

## 8–9. i18n + accessibility

Identical canonical pattern. `Admin.OperationLog.*` keys cover every
visible string; EN ↔ AR parity preserved.

## 10. Use cases (UCS-001)

- **UC-OPL-LIST-001** — Filter + browse audit entries
  _(pending UCS detail entry)_.
- **UC-OPL-DETAILS-001** — Open one audit entry detail
  _(pending UCS detail entry)_.

## 11. E2E test scenarios

See [`docs/tests/e2e/cp-admin-operation-log.md`](../../tests/e2e/cp-admin-operation-log.md):

- E2E-OPL-001 — Default render (newest-first, sign-in events visible).
- E2E-OPL-002 — Filter by event type → grid narrows.
- E2E-OPL-003 — Filter by Outcome=Failure → only failures.
- E2E-OPL-004 — Details modal shows correlation id + user agent +
  detail.
- E2E-OPL-005 — Auth: non-admin → `/not-permitted`.
- E2E-OPL-006 — RTL.

## 12. Related docs

- Admin Manual: `Admin-Manual.md § 10.12 Operation log viewer`.
- D-134 plan §3.1.2 (Path 2 / no schema).
- Decisions: D-134-A1 covers Roles; Operation log lands under the
  Sprint-A continuation entry.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-29 | D-134 Sprint A | Original — list + filter + Details modal over existing OperationLog table. |

_Last reviewed:_ 2026-05-29 by Claude (D-134 Sprint A).
