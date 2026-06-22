# AI services — `/admin/ai/services`

| | |
|--|--|
| **Route** | `/admin/ai/services` |
| **Audience** | Administrator |
| **Auth** | `[RequirePermission(PermissionCatalog.AiPrompts.View)]` (page) + `RequireApprovedAccount` on the underlying list endpoint. Read-only — no per-action permissions, because there are no write actions. |
| **Pattern** | CP Phase-1 "AI Control Center" — a per-service view that aggregates the D-176 prompt catalogue by `AiFeature`. `SimfDataGrid`-based, client-side aggregation (no new endpoint). |
| **Status** | ✅ Real (CP Phase-1) |
| **Backend endpoints** | None of its own. Reads BFF `POST /account/api/admin/ai/prompts/list` → API `POST /api/v1/admin/ai/prompts/list` (the same endpoint the prompt catalogue uses, gated by `AiPrompts.View`). |
| **Source** | [`AiServicesConsole.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/AiServicesConsole.razor), [`AiHosting.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/AiHosting.cs) |
| **Tests** | [`docs/tests/e2e/cp-admin-ai-services.md`](../../tests/e2e/cp-admin-ai-services.md), [`AiServicesConsoleTests.cs`](../../../tests/SIMF.ControlPanel.Tests/AiServicesConsoleTests.cs) |
| **Last reviewed** | 2026-06-22 |

## 1. Purpose

One centralized place to answer "what runs each AI service, and where?" without
reading the prompt catalogue row by row. The owner's direction (per the AI-Tools
deck) is a centralized AI module where **each service has its own provider +
model**, set in the CP; this page is the per-service overview of that routing.

The page reads the whole prompt catalogue once and groups it by `AiFeature` into
one row per service. For each service it shows:

- **Service** — the active prompt's bilingual `DisplayName` (Arabic in the AR
  locale), with the `AiFeature` enum as a technical sub-label.
- **Active prompt** — the key + `v{version}` of the prompt that is currently
  `IsActive` (the routing the service actually uses), or a "None active" pill
  when the feature has only inactive prompts.
- **Provider / Model** — the active prompt's routing.
- **Hosting** — a derived pill: **Cloud** (Gemini / Anthropic / Azure OpenAI),
  **Offline** (Echo), or **OpenAI API** (OpenAI — on-prem-vs-cloud depends on the
  configured `BaseUrl`, so it is shown as endpoint-dependent). A sensitive feature
  (session summary, assistant, live translation/sign) on a cloud provider also
  raises a **Residency risk** pill — the hybrid NCA/DoD data-residency signal.
  The classification is shared with the prompt catalogue via `AiHosting`.
- **Prompts** — how many prompts the service has (1 normally; more for A/B).

## 2. Out of scope (later phases)

Editing a service's provider/model inline (the "Routing" tab), per-service health
analytics (calls / error-rate / latency / token spend), and the dashboard +
tabbed service detail are deferred to CP Phase-1/2 (they need a `SimfTabs`
primitive and an invocation-aggregate endpoint). This page delivers the read-only
"one row per service" overview; edits stay on `/admin/ai/prompts`.
