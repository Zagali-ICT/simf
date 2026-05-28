# E2E test catalogue — Visitor notifications (Web) (`/account/notifications`)

| | |
|--|--|
| **Page** | [`web/account-notifications.md`](../../pages/web/account-notifications.md) |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-WEB-NTF-001 | Visitor lands from profile header link (D-132 orphan fix) | P0 |
| E2E-WEB-NTF-002 | Empty inbox shows friendly empty state | P1 |
| E2E-WEB-NTF-003 | Dismiss a notification per-row | P1 |

## Scenarios

### E2E-WEB-NTF-001 — Link from profile

```gherkin
Scenario: Visitor reaches inbox from the profile header
  Given V1 is on /account/profile
  When V1 clicks the Notifications anchor in the header
  Then they land on /account/notifications
  And the inbox renders with their notifications
```

### E2E-WEB-NTF-002 — Empty inbox

```gherkin
Scenario: Empty inbox renders SimfEmptyState
  Given V1 has no notifications
  When they open /account/notifications
  Then the empty state reads "No notifications." (bilingual)
```

### E2E-WEB-NTF-003 — Dismiss

```gherkin
Scenario: Per-row dismiss
  Given V1 has a notification N1
  When V1 clicks the trash icon
  Then DELETE /account/api/notifications/N1 returns 200
  And the row vanishes
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
