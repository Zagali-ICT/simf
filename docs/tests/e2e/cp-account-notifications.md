# E2E test catalogue — Notifications inbox (CP) (`/account/notifications`)

| | |
|--|--|
| **Page** | [`cp/account-notifications.md`](../../pages/cp/account-notifications.md) |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-NTF-001 | Default mix of read + unread; New pill on unread rows | P1 |
| E2E-NTF-002 | Details modal renders title/body/severity/createdAt | P1 |
| E2E-NTF-003 | Per-row delete removes the row | P1 |
| E2E-NTF-004 | Select 3 + bulk Delete → 3 rows dismiss + toast | P0 |
| E2E-NTF-005 | Mark all as read → New pills vanish | P0 |
| E2E-NTF-006 | Empty inbox → SimfEmptyState | P1 |
| E2E-NTF-007 | RTL render | P2 |

## Scenarios

### E2E-NTF-001 — Default render

```gherkin
Scenario: Mixed read/unread notifications render
  Given the user has 5 notifications, 2 of them unread
  When they open /account/notifications
  Then 5 rows render with title + body + severity + createdAt
  And the 2 unread rows show the New SimfPill in the Title column
  And the header bell badge reads "2"
```

### E2E-NTF-002 — Details modal

```gherkin
Scenario: Details modal shows the full record
  Given a notification with Title="System update", Body="Long body…", Severity=Info
  When the user clicks the Details icon
  Then a SimfModal opens with the four fields rendered in a simf-dl
  And Close closes the modal
```

### E2E-NTF-003 — Per-row delete

```gherkin
Scenario: Per-row delete dismisses the row
  Given a notification N1 exists in the inbox
  When the user clicks the trash icon on N1
  Then DELETE /account/api/notifications/N1 returns 200
  And the grid reloads without N1
```

### E2E-NTF-004 — Bulk-dismiss

```gherkin
Scenario: Bulk-dismiss 3 selected notifications
  Given the inbox has 5 rows
  When the user ticks 3 row checkboxes
  And clicks toolbar Delete
  Then 3 DELETE /account/api/notifications/{id} calls fire sequentially
  And the toast reads Account.Notifications.BulkDismissed with count=3
  And the grid reloads with 2 rows
```

### E2E-NTF-005 — Mark all read

```gherkin
Scenario: Mark all as read
  Given 4 of the 6 rows are unread
  When the user clicks "Mark all as read"
  Then POST /account/api/notifications/read-all fires
  And every row's New pill disappears
  And the header bell badge drops to 0
  And rows stay visible (read-state only, not dismissed)
```

### E2E-NTF-006 — Empty state

```gherkin
Scenario: Empty inbox renders SimfEmptyState
  Given the user has no notifications
  When they open /account/notifications
  Then SimfEmptyState renders with "No notifications." copy
  And the Mark-all-read button below the grid stays visible (no-op but valid)
```

### E2E-NTF-007 — RTL

```gherkin
Scenario: RTL renders correctly
  Given the user toggles "العربية"
  When they navigate to /account/notifications
  Then page mirrors; column headers flip; the Mark-all-read button stays below the grid
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
