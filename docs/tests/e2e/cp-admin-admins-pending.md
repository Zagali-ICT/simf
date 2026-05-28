# E2E test catalogue — Pending admins (`/admin/admins/pending`)

| | |
|--|--|
| **Page** | [`cp/admin-admins-pending.md`](../../pages/cp/admin-admins-pending.md) |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-APN-001 | Approve (one-click — no review modal yet) → row vanishes + role granted | P0 |
| E2E-APN-002 | Reject with reason → audited + bilingual reason stored | P0 |
| E2E-APN-003 | Reason < 10 chars → Submit disabled | P1 |

## Scenarios

### E2E-APN-001 — Approve (one-click)

```gherkin
Scenario: Approve a pending admin (no review modal — parity gap)
  Given a self-registered admin candidate A1 is in PendingApproval
  When the admin clicks "Approve" on A1's row
  Then POST /admin/admins/A1/approve fires immediately (no preview modal)
  And A1.AccountState becomes Approved
  And the Administrator role is granted
  And the toast reads "Approved {email}"
  And the row vanishes
  And the audit log records Admin.UserApproved
```

> **Known parity gap with PendingVisitors/PendingOthers:** there is no
> review-before-approve modal here yet (D-128 applied only to V/O).
> Tracked separately. Until then, admins MUST verify the candidate
> off-channel before clicking Approve.

### E2E-APN-002 — Reject

```gherkin
Scenario: Reject pending admin with reason
  Same shape as E2E-VPN-002 but on /admin/admins/pending.
```

### E2E-APN-003 — Reason gate

```gherkin
Scenario: Short reason disables Submit
  Same shape as E2E-VPN-003.
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
