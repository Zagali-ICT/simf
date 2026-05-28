# E2E test catalogue — Attendees (`/admin/attendees`)

| | |
|--|--|
| **Page** | [`cp/admin-attendees.md`](../../pages/cp/admin-attendees.md) |
| **Authored** | D-134 Sprint A (2026-05-29) |
| **Last reviewed** | 2026-05-29 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-ATT-001 | Default render — newest-first; mix of Visitors + Others | P0 |
| E2E-ATT-002 | Filter by Kind=Visitors only | P0 |
| E2E-ATT-003 | Filter by State=Pending | P0 |
| E2E-ATT-004 | Search by email substring | P0 |
| E2E-ATT-005 | Auth: non-admin → /not-permitted | P0 |
| E2E-ATT-006 | RTL render | P1 |

## Scenarios

### E2E-ATT-001 — Default render

```gherkin
Scenario: Attendees page renders newest-first with mixed kinds
  Given the database has both Visitor and Other accounts (Approved + Pending mix)
  And admins are excluded by design
  When the admin navigates to /admin/attendees
  Then the grid renders rows newest-first
  And each row shows Email, DisplayName, Kind (Visitor or Other),
      ProfileType (or "—"), State pill, QR id (or "—"), Registered date
  And no admin row appears
```

### E2E-ATT-002 — Filter by kind

```gherkin
Scenario: Kind=Visitors only narrows the result
  Given attendees of both kinds exist
  When the admin picks Kind="Visitors only" + Apply filters
  Then every visible row's Kind column reads "Visitor"
```

### E2E-ATT-003 — Filter by state

```gherkin
Scenario: State=Pending filter narrows the result
  Given some attendees are PendingApproval
  When the admin picks State="Pending" + Apply filters
  Then every visible row's State pill is "Pending"
```

### E2E-ATT-004 — Search

```gherkin
Scenario: Search by email substring
  Given an attendee with email "ahmed.smoke@simf.test" exists
  When the admin types "ahmed" into the search field and Apply filters
  Then the grid shows that row
  And rows whose email + displayName don't contain "ahmed" are hidden
```

### E2E-ATT-005 — Auth gate

```gherkin
Scenario: Non-admin user is denied
  Given a Visitor account is signed in
  When they navigate to /admin/attendees
  Then they land on /not-permitted with HTTP 200
```

### E2E-ATT-006 — RTL

```gherkin
Scenario: Arabic toggle mirrors page
  Given the admin is on /admin/attendees
  When they click "العربية"
  Then <html dir="rtl" lang="ar"> is set
  And the banner reads "الحضور"
  And the filter labels + column headers are Arabic
  And the ProfileType column shows the Arabic name when available
```

_Last reviewed:_ 2026-05-29 by Claude (D-134 Sprint A).
