# E2E test catalogue — Pending visitors (`/admin/visitors/pending`)

| | |
|--|--|
| **Page** | [`cp/admin-visitors-pending.md`](../../pages/cp/admin-visitors-pending.md) |
| **Surface** | Control Panel |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-VPN-001 | D-128 approve-with-review: View modal → Confirm → row vanishes | P0 |
| E2E-VPN-002 | Reject with 50-char reason → AccountState=Rejected + reason audited | P0 |
| E2E-VPN-003 | Reject reason < 10 chars → Submit disabled | P1 |
| E2E-VPN-004 | View shows ID image inline when HasIdImage | P1 |
| E2E-VPN-005 | Stale row (sibling admin already approved) → 404 toast | P1 |

## Scenarios

### E2E-VPN-001 — Approve-with-review

```gherkin
Scenario: Approve a pending visitor only after reviewing the full profile
  Given a visitor V1 is in AccountState=PendingApproval
  And V1 has a complete profile (HasIdImage=true)
  When the admin opens /admin/visitors/pending
  And clicks "Approve" on V1's row
  Then a modal opens preloaded via GET /admin/visitors/V1/profile-for-approval
  And shows every profile field + the inline ID image
  And the footer reads "Cancel" + "Confirm and Approve"
  When the admin clicks "Confirm and Approve"
  Then POST /admin/visitors/V1/approve fires
  And V1.AccountState becomes Approved
  And a fresh QR id is minted on V1
  And the toast reads "Approved {email}"
  And the row vanishes from the queue
  And the audit log records Admin.UserApproved with the actor
```

### E2E-VPN-002 — Reject with reason

```gherkin
Scenario: Reject with a 50-character reason
  Given V1 is in PendingApproval
  When the admin clicks "Reject" on V1's row
  Then the reject reason modal opens with a SimfTextarea
  When they type "Email domain not approved for the 2026 forum."
  And click "Reject"
  Then POST /admin/visitors/V1/reject fires with the reason
  And V1.AccountState becomes Rejected
  And V1.RejectionReason holds the verbatim reason
  And the audit log records Admin.UserRejected
  And the toast reads "Rejected {email}"
```

### E2E-VPN-003 — Reason length gate

```gherkin
Scenario: Reject reason shorter than 10 chars keeps Submit disabled
  Given the reject modal is open
  When the admin types "Too short"
  Then the Submit button is disabled
  When they extend to "Too short — adding context to pass the 10-char gate"
  Then Submit enables
```

### E2E-VPN-004 — ID image inline

```gherkin
Scenario: View shows ID document image
  Given V1 has HasIdImage=true
  When the admin clicks "View" on V1's row
  Then the modal renders an <img> with src "/account/api/admin/visitors/V1/id-document?v={ticks}"
  And the decrypted image displays
```

### E2E-VPN-005 — Stale row

```gherkin
Scenario: Approving a row that another admin just handled returns 404
  Given Admin A and Admin B both have /admin/visitors/pending open
  And both rows include V1
  When Admin A successfully approves V1
  And Admin B then clicks "Approve" on V1's stale row
  Then the server returns HTTP 404 + ApiResult.Error.Code="NotFound"
  And Admin B's modal closes
  And the toast surfaces the bilingual fallback "The visitor was not found or is no longer pending."
  And Admin B's grid reloads (V1 is gone)
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
