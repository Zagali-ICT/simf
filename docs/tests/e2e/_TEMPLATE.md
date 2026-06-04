# E2E test catalogue — `{Page Title}` (`{route}`)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7).
> Copy this file to `docs/tests/e2e/{cp|web|mobile}-{slug}.md` and fill
> every section. The catalogue is the **source of truth** for what E2E
> coverage exists; the actual implementation lives next to the test
> runner (Playwright / xUnit + WebApplicationFactory / etc.).

| | |
|--|--|
| **Page** | [`{slug}.md`](../../pages/{cp|web|mobile}/{slug}.md) |
| **Route** | `{e.g. /admin/interests}` |
| **Surface** | Control Panel / Website / Mobile |
| **Test runner** | Chrome DevTools MCP + PowerShell driver _(or: Playwright when adopted)_ |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via `Get-Totp` helper |
| **Last reviewed** | `YYYY-MM-DD` |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-XXX-001 | Golden path | happy | P0 | _to author_ |
| E2E-XXX-002 | Empty state | happy | P1 | _to author_ |
| E2E-XXX-003 | Auth gate (non-admin → /not-permitted) | auth | P0 | _to author_ |
| E2E-XXX-004 | Validation failure | error | P1 | _to author_ |
| E2E-XXX-005 | Conflict / duplicate | error | P1 | _to author_ |
| E2E-XXX-006 | Server 500 | resilience | P2 | _to author_ |
| E2E-XXX-007 | RTL render | i18n | P1 | _to author_ |

## Scenarios

### E2E-XXX-001 — Golden path

```gherkin
Feature: {Page Title} golden path
  As a {role}
  I want to {do the most common action}
  So that {value}

Background:
  Given an Administrator is signed in
  And ...

Scenario: ...
  Given ...
  When ...
  Then ...
  And ...
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/{slug}-{scenario}-before.png`
- Screenshot after: `docs/screenshots/{slug}-{scenario}-after.png`
- Console errors: 0 expected
- Network failures: 0 expected
- Audit row: `OperationLog` row with `Event = '{event-key}'` and the
  actor's id.

### E2E-XXX-002 — Empty state

```gherkin
Scenario: Empty state renders SimfEmptyState
  Given the database has no {row type}
  When the administrator opens the page
  Then the grid shows the SimfEmptyState
  And no error toast appears
```

### E2E-XXX-003 — Auth gate

```gherkin
Scenario: Non-administrator user is denied
  Given a signed-in user with no Administrator role
  When they navigate to {route}
  Then they are redirected to /not-permitted with HTTP 200
```

### (further scenarios per the §Coverage matrix)

---

_Last reviewed:_ `YYYY-MM-DD` by `{author}`.
