# E2E test catalogue — AI service detail (`/admin/ai/services/{feature}`)

| | |
|--|--|
| **Page** | [`cp/admin-ai-service-detail.md`](../../pages/cp/admin-ai-service-detail.md) |
| **Route** | `/admin/ai/services/{feature}` (e.g. `/admin/ai/services/SessionSummary`) |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@simrsnf.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-22 (CP Phase-2 — new page) |

> **Page permission:** `@attribute [RequirePermission(PermissionCatalog.AiPrompts.View)]`.
> A per-service tabbed drill-down (`SimfTabs`), reached from the services console
> "Open" action. **Routing** tab = the shared `AiRoutingEditor` (wrapped in
> `AuthorizedAction(AiPrompts.Edit)` — the only write). **Prompts** tab = this
> service's prompts (read, filtered CP-side from `/ai/prompts/list`). **Analytics**
> tab = the service's row from the `/ai/dashboard` aggregate (best-effort — a
> 403/failure degrades to the empty state). `{feature}` is the `AiFeature` name.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-AISD-001 | Open from console — the "Open" row action navigates to `/admin/ai/services/{feature}` | happy | P0 | _to author_ |
| E2E-AISD-002 | Tabs — Routing / Prompts / Analytics render; clicking switches the active panel | happy | P0 | _to author_ |
| E2E-AISD-003 | Routing — change Provider + Model, Save → "Routing updated." and the active prompt is retargeted | happy | P0 | _to author_ |
| E2E-AISD-004 | Routing without edit permission — the Routing tab shows no editor (AuthorizedAction hidden) | auth | P1 | _to author_ |
| E2E-AISD-005 | Routing — a service with no active prompt shows "no active prompt to configure" | edge | P2 | _to author_ |
| E2E-AISD-006 | Prompts — the table lists only THIS feature's prompts (others filtered out) | happy | P1 | _to author_ |
| E2E-AISD-007 | Analytics — the tab shows this service's 24h calls / error rate / latency / tokens | happy | P1 | _to author_ |
| E2E-AISD-008 | Analytics with no `AiDashboard.View` (or no calls) → the empty "no activity" state, no error | edge | P2 | _to author_ |
| E2E-AISD-009 | Unknown feature — `/admin/ai/services/NotAFeature` shows the "Unknown AI service" state | error | P1 | _to author_ |
| E2E-AISD-010 | Auth gate — an admin without `AiPrompts.View` cannot reach the route | auth | P0 | _to author_ |
| E2E-AISD-011 | RTL — Arabic locale renders the tabs + panels RTL | rtl | P2 | _to author_ |
| E2E-AISD-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-AISD-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

```gherkin
Scenario: E2E-AISD-002 Tabs switch the active panel
  Given I open "/admin/ai/services/SessionSummary"
  Then I see the Routing, Prompts, and Analytics tabs with Routing active
  When I click the "Prompts" tab
  Then the Prompts panel (this service's prompts) is shown and Routing is hidden

Scenario: E2E-AISD-003 Configure routing from the detail page
  Given the "Session summary" service routes to "Echo"
  And I am signed in with "AiPrompts.Edit"
  When I open "/admin/ai/services/SessionSummary" on the Routing tab
  And I change Provider to "Gemini" and Model to "gemini-2.5-flash" and Save
  Then a "Routing updated." toast is shown
  And re-opening the page shows the active prompt on "Gemini"

Scenario: E2E-AISD-006 Prompts tab is filtered to the service
  Given the catalogue has a "session-summary" prompt (SessionSummary) and a
        "faq-answer" prompt (FAQ)
  When I open "/admin/ai/services/SessionSummary" and click the Prompts tab
  Then only "session-summary" is listed
  And "faq-answer" is not listed

Scenario: E2E-AISD-009 Unknown feature
  When I open "/admin/ai/services/NotAFeature"
  Then the page shows "Unknown AI service." and no tabs
```
