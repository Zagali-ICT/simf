# E2E test catalogue — AI services console (`/admin/ai/services`)

| | |
|--|--|
| **Page** | [`cp/admin-ai-services.md`](../../pages/cp/admin-ai-services.md) |
| **Route** | `/admin/ai/services` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-22 (CP Phase-1 — new page) |

> **Page permission:** `@attribute [RequirePermission(PermissionCatalog.AiPrompts.View)]`.
> The page is **read-only**: it issues one `POST /account/api/admin/ai/prompts/list`
> (the same endpoint + `AiPrompts.View` gate the prompt catalogue uses) and
> aggregates the result **CP-side** into one row per AI service (`AiFeature`).
> There are no write actions and no new endpoint. Routing/prompt edits stay on the
> AI prompts catalogue (`/admin/ai/prompts`).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-AIS-001 | Golden path — page loads, one row per service (feature) with active prompt + provider + model + hosting | happy | P0 | _to author_ |
| E2E-AIS-002 | Aggregation — a feature with 2 prompts shows the **active** prompt's routing and a prompt count of 2 | happy | P0 | _to author_ |
| E2E-AIS-003 | Residency warning — a sensitive feature (Session summary) on a cloud provider (Gemini) shows the gold **Residency risk** pill | happy | P0 | _to author_ |
| E2E-AIS-004 | Hosting pills — Echo → "Offline", OpenAI → "OpenAI API", Gemini/Anthropic → "Cloud" | happy | P1 | _to author_ |
| E2E-AIS-005 | No active prompt — a feature whose only prompt is inactive shows the "None active" pill, no active key | edge | P1 | _to author_ |
| E2E-AIS-006 | Filter — typing a service name / feature filters the rows client-side | happy | P1 | _to author_ |
| E2E-AIS-007 | Sort — sorting by Provider / Prompts reorders the rows | happy | P2 | _to author_ |
| E2E-AIS-008 | Empty — no prompts in the catalogue → `SimfEmptyState` "No AI services configured." | empty | P1 | _to author_ |
| E2E-AIS-009 | Auth gate — a signed-in admin **without** `AiPrompts.View` cannot see the nav item or reach the route | auth | P0 | _to author_ |
| E2E-AIS-010 | Server 500 — the list endpoint fails → error `SimfAlert`, no rows | error | P2 | _to author_ |
| E2E-AIS-011 | RTL — Arabic locale renders the page RTL with localized service names (prompt `DisplayNameArabic`) | rtl | P2 | _to author_ |
| E2E-AIS-012 | Configure routing — open the modal on a service, change provider + model, Save → "Routing updated." and the grid shows the new provider | happy | P0 | _to author_ |
| E2E-AIS-013 | Routing auth — an admin without `AiPrompts.Edit` does not see the "Configure routing" action | auth | P1 | _to author_ |
| E2E-AIS-014 | Routing absent — a service whose only prompt is inactive offers no "Configure routing" action (nothing to retarget) | edge | P2 | _to author_ |
| E2E-AIS-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-AIS-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

```gherkin
Scenario: E2E-AIS-001 Golden path — one row per AI service
  Given I am signed in to the Control Panel as an Administrator
  And the AI prompt catalogue has one active prompt per seeded feature
  When I open "/admin/ai/services"
  Then the grid shows one row per AI service (feature)
  And each row shows the active prompt key with a "v{version}" badge
  And each row shows the active prompt's Provider and Model
  And each row shows a hosting pill and a prompt count

Scenario: E2E-AIS-002 Aggregation picks the active prompt
  Given the "Session summary" feature has two prompts:
    | key                  | provider | active | version |
    | session-summary      | Gemini   | true   | 2       |
    | session-summary-old  | Echo     | false  | 1       |
  When I open "/admin/ai/services"
  Then the Session-summary row shows the active key "session-summary" and "v2"
  And it shows Provider "Gemini"
  And its Prompts count is 2

Scenario: E2E-AIS-003 Residency warning for sensitive content on a cloud provider
  Given the "Session summary" service routes to the "Gemini" provider
  When I open "/admin/ai/services"
  Then the Session-summary row shows the gold "Residency risk" pill
  And a non-sensitive service on the same provider does NOT show that pill

Scenario: E2E-AIS-005 A feature with only an inactive prompt
  Given the "FAQ" feature has a single prompt that is inactive
  When I open "/admin/ai/services"
  Then the FAQ row shows the "None active" pill
  And it shows no active prompt key

Scenario: E2E-AIS-008 Empty catalogue
  Given the AI prompt catalogue is empty
  When I open "/admin/ai/services"
  Then the grid shows the empty state "No AI services configured."

Scenario: E2E-AIS-009 Auth gate
  Given I am signed in as an admin whose roles grant no "AiPrompts.View" permission
  Then the "AI services" item is absent from the side menu
  And navigating directly to "/admin/ai/services" is refused by the page guard

Scenario: E2E-AIS-012 Configure a service's routing from the console
  Given I am signed in with "AiPrompts.Edit"
  And the "Session summary" service's active prompt routes to "Echo"
  When I click "Configure routing" on the Session-summary row
  And the routing modal opens showing the active prompt's provider/model
  And I change Provider to "Gemini" and Model to "gemini-2.5-flash"
  And I click "Save routing"
  Then a "Routing updated." toast is shown
  And the Session-summary row now shows Provider "Gemini" and Model "gemini-2.5-flash"
  And only the routing fields changed — the prompt's text/feature/active state are unchanged

Scenario: E2E-AIS-013 Routing action hidden without edit permission
  Given I am signed in with "AiPrompts.View" but NOT "AiPrompts.Edit"
  When I open "/admin/ai/services"
  Then no row shows the "Configure routing" action
```
