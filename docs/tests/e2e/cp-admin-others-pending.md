# E2E test catalogue — Pending others (`/admin/others/pending`)

| | |
|--|--|
| **Page** | [`cp/admin-others-pending.md`](../../pages/cp/admin-others-pending.md) |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-OPN-001 | Approve-with-review → row vanishes + Approved + QR minted | P0 |
| E2E-OPN-002 | Reject with reason → audited + bilingual reason stored | P0 |
| E2E-OPN-003 | Cross-kind id on /admin/others/{visitorId}/profile-for-approval → 404 | P0 |

## Scenarios

### E2E-OPN-001 — Approve

```gherkin
Scenario: Approve a pending Other with review
  Same shape as E2E-VPN-001 but on /admin/others/pending.
  Loads via GET /admin/others/{id}/profile-for-approval.
  POST /admin/others/{id}/approve mints QR + sets AccountState=Approved.
```

### E2E-OPN-002 — Reject

```gherkin
Scenario: Reject pending Other with reason
  Same shape as E2E-VPN-002 but on /admin/others/pending.
  Audit row Admin.UserRejected(kind=Other).
```

### E2E-OPN-003 — Cross-kind 404

```gherkin
Scenario: A Visitor id on the Others pending-profile endpoint returns 404
  Given a Visitor V1 in PendingApproval
  When the admin GETs /account/api/admin/others/V1/profile-for-approval
  Then HTTP 404 + ApiResult.Error.Code="NotFound"
  And the response is byte-identical to an unknown-id 404
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
