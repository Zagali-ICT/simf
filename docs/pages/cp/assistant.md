# Control Panel assistant — floating help chat

| | |
|--|--|
| **Route** | _(no route — a floating widget rendered by the shell on every signed-in CP page)_ |
| **Audience** | Any CP operator holding `Assistant.Use` (Administrator via the `*` wildcard, plus the GateOperator / PublicRelations / SecurityTeam / ScientificCommittee teams) |
| **Auth** | The widget renders only for a user holding `PermissionCatalog.Assistant.Use` (or the wildcard). The backend endpoint is gated `Policies(PolicyFor(Assistant.Use), RequireApprovedAccount)` + the `auth` rate limiter. |
| **Pattern** | Floating launcher + chat panel mounted once in `CpShellLayout`. Answers go through the **one** centralized AI (`IAiService`, the `cp-assistant` prompt). Grounded on the CP page catalogue, so it can only cite a real route the operator may open. |
| **Status** | ✅ Real |
| **Backend endpoints** | BFF `POST /account/api/admin/ai/assistant` → API `POST /api/v1/admin/ai/assistant` (`CpAssistantEndpoint`, gated `Assistant.Use`). The BFF handler builds the grounding directory server-side and forwards `{ question, pages, locale }` to the `cp-assistant` prompt. |
| **Source** | [`CpAssistantWidget.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Assistant/CpAssistantWidget.razor), [`CpAssistantDirectory.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Assistant/CpAssistantDirectory.cs), [`CpAssistantEndpoint.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/CpAssistantEndpoint.cs) |
| **Tests** | [`docs/tests/e2e/cp-assistant.md`](../../tests/e2e/cp-assistant.md), [`CpAssistantEndpointTests.cs`](../../../tests/SIMF.Api.Tests/CpAssistantEndpointTests.cs), [`CpAssistantDirectoryTests.cs`](../../../tests/SIMF.ControlPanel.Tests/CpAssistantDirectoryTests.cs) |
| **Last reviewed** | 2026-07-22 |

## 1. Purpose

A help assistant that lets a Control Panel operator ask, in plain language,
"where is the screen for X?" or "how do I configure Y?" and get an answer that
**names the exact Control Panel page and its path** so they can open it. It lowers
the learning curve for the ~90-page admin surface without a static manual hunt.

## 2. How it works

1. The operator types a question in the floating panel (bottom corner of every
   signed-in CP page).
2. The browser posts only the **question** to the BFF (`/account/api/admin/ai/assistant`).
3. The BFF handler builds a **grounding directory** from `CpNavigation`, filtered
   to the pages **this** operator may open (their permission claims) and localized
   to their language, then forwards `{ question, pages, locale }` to the API.
4. The API routes it through the centralized `IAiService` (`cp-assistant` prompt),
   which is instructed to answer only from the supplied directory and to cite the
   exact route. The call is logged as an `AiInvocation` (redacted) like every other
   AI call.
5. The answer appears as a chat bubble, quoting the page path (for example
   `/admin/rating-config`).

Because the directory is **permission-filtered server-side**, the assistant can
never point an operator at a page they are not allowed to open — it is safe by
construction.

## 3. Provider (going live)

Like every SIMF AI feature, the `cp-assistant` prompt is **seeded on the offline
`Echo` provider** so development and tests run without egress. In production it
becomes a real conversational assistant the moment an operator sets a real
provider — either by setting `Ai:DefaultProvider` (e.g. `Anthropic` / `Gemini`,
the D-484 redirect), or by editing the `cp-assistant` prompt's provider on
`/admin/ai/prompts`. A real API key (`Ai:<Provider>:ApiKey`) and approved network
egress are prerequisites — the key stays server-side and is never sent to the
browser. On `Echo`, the assistant still returns the grounded page match.

## 4. Out of scope (later)

Deep step-by-step instructions drawn from the full `Admin-Manual.md` would need a
retrieval step (the manual does not fit the AI input cap in one shot); v1 grounds
on the page directory (name + route) only. Adding a short per-page "purpose" line
and manual-snippet retrieval is a possible phase-2 enhancement.
