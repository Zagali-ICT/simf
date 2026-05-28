# E2E test catalogue — Operation log viewer (`/admin/operation-log`)

| | |
|--|--|
| **Page** | [`cp/admin-operation-log.md`](../../pages/cp/admin-operation-log.md) |
| **Authored** | D-134 Sprint A (2026-05-29) |
| **Last reviewed** | 2026-05-29 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-OPL-001 | Default render — newest-first; sign-in event visible | P0 |
| E2E-OPL-002 | Filter by event type → grid narrows | P1 |
| E2E-OPL-003 | Filter by Outcome=Failure → only failures | P1 |
| E2E-OPL-004 | Details modal shows correlation id + user agent + detail | P1 |
| E2E-OPL-005 | Auth: non-admin → /not-permitted | P0 |
| E2E-OPL-006 | RTL render | P2 |

## Scenarios

### E2E-OPL-001 — Default render

```gherkin
Scenario: Operation log lists newest-first
  Given the admin has signed in (which adds SignIn.Succeeded rows to the audit log)
  When they navigate to /admin/operation-log
  Then GET /admin/operation-log/list returns >= 1 row
  And the first row is the most recent SignIn.Succeeded
  And the Outcome pill is "Success"
```

### E2E-OPL-002 — Filter by event type

```gherkin
Scenario: Event type filter narrows the result set
  Given the audit log contains a mix of SignIn.* and PasswordReset.* events
  When the admin types "SignIn" into the Event-type filter
  And clicks Apply filters
  Then only rows whose EventType contains "SignIn" remain
  And the pager total reflects the filtered count
```

### E2E-OPL-003 — Filter by outcome

```gherkin
Scenario: Outcome=Failure filter shows only failures
  Given the audit log contains both Success and Failure rows
  When the admin picks Outcome=Failure
  And clicks Apply filters
  Then every visible row's Outcome pill is "Failure"
```

### E2E-OPL-004 — Details modal

```gherkin
Scenario: Details modal renders the full audit record
  Given an audit row R1 exists with non-null CorrelationId and UserAgent
  When the admin clicks the Details icon on R1
  Then GET /admin/operation-log/{R1.Id} returns 200
  And the modal shows Timestamp + Event + Outcome + Subject email +
      Subject user id + Actor user id + Source IP + User agent +
      Correlation id + Error code (if any) + Detail (if any)
```

### E2E-OPL-005 — Auth gate

```gherkin
Scenario: Non-admin user is denied
  Given a Visitor account is signed in
  When they navigate to /admin/operation-log
  Then they land on /not-permitted with HTTP 200
  And no /admin/operation-log/list request fires
```

### E2E-OPL-006 — RTL

```gherkin
Scenario: Arabic toggle mirrors page
  Given the admin is on /admin/operation-log
  When they click "العربية"
  Then <html dir="rtl" lang="ar"> is set
  And the banner reads "سجل العمليات"
  And the filter labels are Arabic
  And the Outcome pill texts flip
```

_Last reviewed:_ 2026-05-29 by Claude (D-134 Sprint A).
