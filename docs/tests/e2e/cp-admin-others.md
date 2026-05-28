# E2E test catalogue — Others CRUD (`/admin/others`)

| | |
|--|--|
| **Page** | [`cp/admin-others.md`](../../pages/cp/admin-others.md) |
| **Surface** | Control Panel |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-OTH-001 | Walk-in Other (Kind=Other) → Approved + QR minted | P0 |
| E2E-OTH-002 | Cross-kind ProfileTypeId rejected (Visitor type on Other route) | P0 |
| E2E-OTH-003 | Cross-kind id on /admin/others/{visitorId}/profile → 404 | P0 |
| E2E-OTH-004 | Bulk-delete Others with reason | P1 |

## Scenarios

### E2E-OTH-001 — Walk-in Other

```gherkin
Scenario: Register a walk-in Other-typed attendee
  Given an admin is signed in on /admin/others
  And at least one Other profile-type "Sponsor staff" exists
  When they click "+ Add"
  Then the walk-in wizard opens with Kind="Other"
  And the Interests section is hidden (Other-typed have no interests)
  When they pick "Sponsor staff" tile, fill identity + Saudi ID + mobile
  And click "Register"
  Then a SimfUser is created with UserType=Other + AccountState=Approved
  And the QR is minted
  And audit Admin.WalkInRegistered(kind=Other) is written
  And the success modal shows the badge with the "Sponsor staff" color stripe
```

### E2E-OTH-002 — Cross-kind ProfileTypeId

```gherkin
Scenario: A Visitor profile-type id on the Others endpoint is rejected
  Given a Visitor profile-type "General" with id PT-V1 exists
  When the admin POSTs to /admin/others/register-onsite with ProfileTypeId=PT-V1
  Then the API returns HTTP 400 with ApiResult.Error.Code="AdminProfileTypeInvalid"
  And no SimfUser is created
```

### E2E-OTH-003 — Cross-kind id 404

```gherkin
Scenario: A Visitor id on /admin/others/{id}/profile returns 404
  Given a Visitor with id V1 exists
  When the admin GETs /account/api/admin/others/V1/profile
  Then HTTP 404 + ApiResult.Error.Code="NotFound"
  And the response is byte-identical to an unknown-id 404
```

### E2E-OTH-004 — Bulk-delete

```gherkin
Scenario: Bulk-delete Others with required reason
  Same shape as E2E-USR-002 / E2E-VIS-006 but on /admin/others.
  Audit rows are Admin.OtherDeleted.
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
