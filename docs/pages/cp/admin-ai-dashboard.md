# AI dashboard — `/admin/ai`

| | |
|--|--|
| **Route** | `/admin/ai` |
| **Audience** | Administrator |
| **Auth** | `[RequirePermission(PermissionCatalog.AiDashboard.View)]` (page) + `RequireApprovedAccount` on the endpoint. Read-only — no per-action permissions. |
| **Pattern** | CP Phase-1 "AI Control Center" landing dashboard. `SimfStatCard` health tiles + a per-service breakdown table, fed by a single aggregate endpoint. |
| **Status** | ✅ Real (CP Phase-1) |
| **Backend endpoints** | BFF `GET /account/api/admin/ai/dashboard` → API `GET /api/v1/admin/ai/dashboard` (gated by `AiDashboard.View`). One on-demand aggregate over `AiInvocation` (last 24h) + the `AiPrompt` catalogue — no new schema. |
| **Source** | [`AiDashboard.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/AiDashboard.razor), [`AiPromptAdminEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/AiPromptAdminEndpoints.cs) (`GetAiDashboardEndpoint`), [`AdminAiPromptService.cs`](../../../src/Backend/SIMF.Infrastructure/Ai/AdminAiPromptService.cs) (`GetDashboardAsync`) |
| **Tests** | [`docs/tests/e2e/cp-admin-ai-dashboard.md`](../../tests/e2e/cp-admin-ai-dashboard.md), [`AiDashboardTests.cs`](../../../tests/SIMF.ControlPanel.Tests/AiDashboardTests.cs), [`AiModuleTests.cs`](../../../tests/SIMF.Api.Tests/AiModuleTests.cs) (endpoint aggregation + auth) |
| **Last reviewed** | 2026-06-22 |

## 1. Purpose

The AI Control Center landing page — the at-a-glance health of the whole AI
module over the **last 24 hours**, so an admin can spot a regression (a spike in
errors or latency, a runaway token spend) without trawling the invocation log.

### Top-line tiles (`SimfStatCard`)

- **Calls** — total AI invocations in the window.
- **Error rate** — failed calls ÷ total, as a percentage.
- **Avg latency** — call-weighted average latency in ms (not an average of
  per-service averages).
- **Tokens** — total input + output tokens.
- **Active services** — `{active} / {total}` configured services (a service is
  "active" when it has an active prompt; independent of whether it was called).

### Per-service breakdown

One row per `AiFeature` that had calls in the window: calls, errors (+ rate),
avg latency, token total. Empty-state ("No AI calls in this window yet.") when
nothing was called.

## 2. Out of scope (later phases)

A configurable window, time-series charts, p95/p99 latency, and the tabbed
service-detail drill-down (Routing / Prompts / Logs / Analytics — needs a
`SimfTabs` primitive) are deferred. This page is the read-only 24h roll-up;
per-service routing edits live on the [AI services console](admin-ai-services.md)
and the invocation log on `/admin/ai/invocations`.
