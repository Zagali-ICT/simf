# E2E test catalogue — Control Panel assistant (floating help chat)

| | |
|--|--|
| **Page** | [`cp/assistant.md`](../../pages/cp/assistant.md) |
| **Route** | _(no route — floating widget on every signed-in CP page)_ |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-07-22 (new feature) |

> **Permission:** the widget renders only for a user holding `Assistant.Use`
> (Administrator via the `*` wildcard, plus GateOperator / PublicRelations /
> SecurityTeam / ScientificCommittee). The backend endpoint
> `POST /api/v1/admin/ai/assistant` is gated `Assistant.Use` + `RequireApprovedAccount`.
> The `cp-assistant` prompt is seeded on `Echo`; on Echo the answer echoes the
> grounded directory, so scenarios assert the **route is cited**, not exact prose.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-CPA-001 | Launcher visible on any CP page; clicking it opens the panel with the greeting | happy | P0 | _to author_ |
| E2E-CPA-002 | Golden path — ask "where do I add a session?" → the answer cites `/admin/sessions` | happy | P0 | _to author_ |
| E2E-CPA-003 | Empty / whitespace question — Send is disabled; no request is sent | validation | P1 | _to author_ |
| E2E-CPA-004 | Permission gate (UI) — a signed-in user without `Assistant.Use` sees no launcher | auth | P0 | _to author_ |
| E2E-CPA-005 | Grounding is permission-scoped — a ScientificCommittee operator asking about a gate page is told they may not have access; no forbidden route is cited | auth | P0 | _to author_ |
| E2E-CPA-006 | Provider/transport failure → the bilingual error bubble, no crash, circuit stays alive | resilience | P1 | _to author_ |
| E2E-CPA-007 | RTL — Arabic locale renders the panel RTL; the greeting is Arabic | i18n | P1 | _to author_ |
| E2E-CPA-008 | Auth gate (API) — `POST /admin/ai/assistant` without `Assistant.Use` → HTTP 403 | auth | P0 | _to author_ |
| E2E-CPA-009 | Close — the X button and Esc both close the panel; the launcher stays | happy | P2 | _to author_ |

## Scenarios

```gherkin
Scenario: E2E-CPA-002 Golden path — the assistant cites the right route
  Given an Administrator is signed in and on any Control Panel page
  When I click the assistant launcher
  And I type "where do I add a session?" and press Send
  Then a user bubble shows my question
  And an assistant bubble appears whose text contains "/admin/sessions"
  And a redacted AiInvocation row is written for prompt key "cp-assistant"

Scenario: E2E-CPA-003 Empty question is a no-op
  Given the assistant panel is open
  When the input is empty or only whitespace
  Then the Send button is disabled
  And no request is sent to /account/api/admin/ai/assistant

Scenario: E2E-CPA-004 No launcher without the permission
  Given a signed-in CP user whose role does not grant "Assistant.Use"
  When any Control Panel page renders
  Then the assistant launcher is not present in the DOM

Scenario: E2E-CPA-005 Grounding respects the caller's permissions
  Given a ScientificCommittee operator (no Gates permissions) is signed in
  When I ask the assistant "where do I operate the gates?"
  Then the answer does NOT contain "/admin/gates/operator"
  And the answer says the page may not be available to me / to ask an administrator

Scenario: E2E-CPA-006 A provider failure degrades gracefully
  Given the cp-assistant prompt is routed to a provider that returns an error
  When I ask the assistant a question
  Then an assistant bubble shows the localized "couldn't answer just now" message
  And the Control Panel circuit is still alive (no Blazor error UI)

Scenario: E2E-CPA-008 API auth gate
  Given an approved admin whose role grants no permissions
  When they POST /api/v1/admin/ai/assistant with a question
  Then the response is HTTP 403 Forbidden
```

**Evidence captured:**
- Screenshot: the open panel with a question + an answer bubble citing a `/admin/...` path.
- Console errors: 0 expected.
- Network: `POST /account/api/admin/ai/assistant` returns 200 (or the mocked failure for E2E-CPA-006); no failed assets.
- Audit row: an `AiInvocation` with `PromptKey = 'cp-assistant'` and `CallerKind = 'Admin'`.

---

_Last reviewed:_ `2026-07-22` by the CP assistant feature change.
