# E2E test catalogue — Website home (`/account`)

| | |
|--|--|
| **Page** | [`web/home.md`](../../pages/web/home.md) |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-WEB-HM-001 | Signed-in visitor lands on /account | P0 |
| E2E-WEB-HM-002 | State-banner redirects fire (Pending → /account/pending; Rejected → /account/rejected) | P0 |

## Scenarios

### E2E-WEB-HM-001 — Landing

```gherkin
Scenario: Signed-in Approved visitor lands on /account
  Given a Visitor V1 is Approved
  When V1 signs in via /login + /login/verify (Website)
  Then they land on /account
  And the page renders (MainLayout, no nav menu per D-064)
```

### E2E-WEB-HM-002 — State-banner redirects

```gherkin
Scenario: Pending or Rejected visitors are redirected
  Given V1 is in PendingApproval
  When V1 signs in
  Then they redirect to /account/pending

  Given V1 is in Rejected
  When V1 signs in
  Then they redirect to /account/rejected with the bilingual reason
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
