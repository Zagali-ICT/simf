# AI service detail — `/admin/ai/services/{feature}`

| | |
|--|--|
| **Route** | `/admin/ai/services/{feature}` (the `AiFeature` name, e.g. `SessionSummary`) |
| **Audience** | Administrator |
| **Auth** | `[RequirePermission(PermissionCatalog.AiPrompts.View)]` (page). The Routing tab's editor is additionally wrapped in `AuthorizedAction(AiPrompts.Edit)` — the only write. Analytics reads `/ai/dashboard` (best-effort; a 403 degrades to the empty state). |
| **Pattern** | CP Phase-2 "AI Control Center" per-service drill-down. `SimfTabs` primitive + the shared `AiRoutingEditor`. Reached from the services console "Open" action; not a nav item (parameterized route). |
| **Status** | ✅ Real (CP Phase-2) |
| **Backend endpoints** | None of its own. Reads `POST /account/api/admin/ai/prompts/list` (filtered CP-side to the feature), `GET /account/api/admin/ai/prompts/{id}` + `PUT …/{id}` (the routing editor), and `GET /account/api/admin/ai/dashboard` (analytics). |
| **Source** | [`AiServiceDetail.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/AiServiceDetail.razor), [`AiRoutingEditor.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/AiRoutingEditor.razor), [`SimfTabs.razor`](../../../src/Shared/SIMF.Components/Layout/SimfTabs.razor) |
| **Tests** | [`docs/tests/e2e/cp-admin-ai-service-detail.md`](../../tests/e2e/cp-admin-ai-service-detail.md), [`AiServiceDetailTests.cs`](../../../tests/SIMF.ControlPanel.Tests/AiServiceDetailTests.cs), [`AiRoutingEditorTests.cs`](../../../tests/SIMF.ControlPanel.Tests/AiRoutingEditorTests.cs), [`SimfTabsTests.cs`](../../../tests/SIMF.ControlPanel.Tests/SimfTabsTests.cs) |
| **Last reviewed** | 2026-06-22 |

## 1. Purpose

Everything about one AI service in one place, behind three tabs:

- **Routing** — the shared `AiRoutingEditor`: set the active prompt's Provider /
  Model / Temperature / Max-tokens and save (PUTs the whole prompt back with only
  the routing changed). The same editor the services console uses in its modal —
  one implementation, two hosts. Hidden when the admin lacks `AiPrompts.Edit`, or
  when the service has no active prompt.
- **Prompts** — this service's prompts (key / provider / model / version / active),
  filtered CP-side from the catalogue. Edits live on `/admin/ai/prompts`.
- **Analytics** — the service's 24h health (calls / error rate / avg latency /
  tokens) lifted from the dashboard aggregate's per-service row. Lazy-loaded on
  first open; a missing `AiDashboard.View` or no activity shows the empty state.

## 2. Notes

`{feature}` is parsed from the `AiFeature` enum name (case-insensitive); an
unrecognised value renders the "Unknown AI service" state. The page is reached
from the [AI services console](admin-ai-services.md) "Open" row action, not the
side menu. The `SimfTabs` primitive and `AiRoutingEditor` introduced here are
reusable beyond this page.
