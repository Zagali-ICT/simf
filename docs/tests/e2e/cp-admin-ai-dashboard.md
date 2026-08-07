# E2E test catalogue — AI dashboard (`/admin/ai`)

| | |
|--|--|
| **Page** | [`cp/admin-ai-dashboard.md`](../../pages/cp/admin-ai-dashboard.md) |
| **Route** | `/admin/ai` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@simrsnf.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-22 (CP Phase-1 — new page) |

> **Page permission:** `@attribute [RequirePermission(PermissionCatalog.AiDashboard.View)]` —
> the **same** `AiDashboard.View` that gates `GET /api/v1/admin/ai/dashboard`.
> The page is **read-only**: one `GET /account/api/admin/ai/dashboard` returns a
> 24h roll-up of `AiInvocation` telemetry (overall + per-service) plus the
> configured active/total service counts. No write actions.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-AID-001 | Golden path — the five health tiles render (calls, error rate, avg latency, tokens, active/total services) | happy | P0 | _to author_ |
| E2E-AID-002 | Per-service breakdown — one table row per service with calls / errors (rate) / avg latency / tokens | happy | P0 | _to author_ |
| E2E-AID-003 | 24h window — a call older than 24h is excluded from the totals + per-service counts | edge | P1 | _to author_ |
| E2E-AID-004 | No activity — no calls in the window → tiles show 0 and the breakdown shows the empty state | empty | P1 | _to author_ |
| E2E-AID-005 | Auth gate — an admin without `AiDashboard.View` cannot see the nav item or reach `/admin/ai` | auth | P0 | _to author_ |
| E2E-AID-006 | Server 500 — the dashboard endpoint fails → error `SimfAlert`, no tiles | error | P2 | _to author_ |
| E2E-AID-007 | RTL — Arabic locale renders the page + tiles RTL | rtl | P2 | _to author_ |
| E2E-AID-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-AID-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

```gherkin
Scenario: E2E-AID-001 Golden path — health tiles
  Given I am signed in to the Control Panel with "AiDashboard.View"
  And there have been AI calls in the last 24 hours
  When I open "/admin/ai"
  Then I see five tiles: Calls, Error rate, Avg latency, Tokens, Active services
  And each tile shows its computed value (e.g. error rate as a percentage,
      latency in ms, active services as "{active} / {total}")

Scenario: E2E-AID-002 Per-service breakdown
  Given the "Session summary" service had 40 calls (2 errors) in the window
  When I open "/admin/ai"
  Then the breakdown table has a Session-summary row
  And it shows 40 calls, "2 (5.0%)" errors, the avg latency in ms, and the token total

Scenario: E2E-AID-003 The 24h window excludes older calls
  Given a service had one call 1 hour ago and one call 48 hours ago
  When I open "/admin/ai"
  Then that service's row counts only the 1-hour-ago call
  And the 48-hour-ago call is not in the totals

Scenario: E2E-AID-004 No activity in the window
  Given there have been no AI calls in the last 24 hours
  When I open "/admin/ai"
  Then the Calls tile shows 0
  And the per-service breakdown shows "No AI calls in this window yet."

Scenario: E2E-AID-005 Auth gate
  Given I am signed in as an admin whose roles grant no "AiDashboard.View"
  Then the "AI dashboard" item is absent from the side menu
  And navigating directly to "/admin/ai" is refused by the page guard
```
