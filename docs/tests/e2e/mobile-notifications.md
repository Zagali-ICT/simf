# E2E test catalogue — `Notifications` (`notifications`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> notification reads/writes are already built + signed-in
> (`RequireApprovedAccount`). The **Flutter screen is built** and tested in
> `src/Mobile/simf_app/test/features/notifications/notifications_screen_test.dart`
> (list, empty, error→retry, tap-unread→markRead+refresh, mark-all) plus the model
> decode in `notification_models_test.dart`. It reuses the shared
> `NotificationsRepository` (the same one that backs the Page_013 bell badge).

| | |
|--|--|
| **Page** | [`Page_033`](../../App/Page_033/README.md) |
| **Route** | `POST /api/v1/app/account/notifications/list` · `/{id}/read` · `/read-all` · app screen #33 `/notifications` |
| **Surface** | Mobile (Flutter) + App API |
| **Auth setup** | **Signed-in, approved account** (`RequireApprovedAccount`). A guest never reaches route 33 (it is in `_authenticatedRoutes`). |
| **Last reviewed** | 2026-06-06 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB033-001 | An approved user loads the inbox (severity icon · title · body · unread dot) | happy | P0 | authored ✓ (screen `renders the notification list`) |
| E2E-MOB033-002 | Tapping an unread row marks it read then refreshes | happy | P0 | authored ✓ (screen `tapping an unread row marks it read then refreshes`) |
| E2E-MOB033-003 | The app-bar mark-all-read action marks every notification read | happy | P0 | authored ✓ (screen `mark-all action calls the repo when there is unread`) |
| E2E-MOB033-004 | Empty inbox → empty state, no mark-all action | edge | P1 | authored ✓ (screen `empty list shows the empty state`, `no mark-all action when everything is read`) |
| E2E-MOB033-005 | A read failure → error + Retry that re-fetches | resilience | P0 | authored ✓ (screen `error shows retry, which re-fetches`) |
| E2E-MOB033-006 | String `kind`/`severity` decode tolerantly (unknown → Info) | contract | P1 | authored ✓ (models `decodes the string kind/severity…`, `an unknown or missing severity falls back to info`) |

## Scenarios

### E2E-MOB033-001 — Load the inbox

```gherkin
Feature: Notifications inbox
  As an approved, signed-in user
  I want my notifications
  So that I do not miss a reminder or a booking update

Scenario: The inbox renders the notifications
  Given I am signed in with an approved account
  When the app calls POST /api/v1/app/account/notifications/list with { skip: 0, top: 50 }
  Then it returns 200 with the GridPage items
  And each card shows the severity icon, the localized title (bold), the body
  And an unread notification shows an accent dot
```

**Evidence:** screen test `renders the notification list`.

### E2E-MOB033-002 — Tap an unread row / E2E-MOB033-003 — Mark all read

```gherkin
Scenario: Tapping an unread notification marks it read
  When I tap an unread notification card
  Then the app calls POST /api/v1/app/account/notifications/{id}/read
  And the list refreshes (the dot is gone on the next read)

Scenario: Mark all read
  Given at least one notification is unread
  When I tap the app-bar mark-all-read action
  Then the app calls POST /api/v1/app/account/notifications/read-all
  And the list refreshes
```

**Evidence:** screen tests `tapping an unread row marks it read then refreshes`,
`mark-all action calls the repo when there is unread`.

### E2E-MOB033-004 — Empty / E2E-MOB033-005 — Error+retry / E2E-MOB033-006 — Contract

```gherkin
Scenario: No notifications shows the empty state
  Given the list read returns no items
  Then the screen shows the "No notifications yet" placeholder
  And no mark-all-read action is shown

Scenario: A failed read offers a retry
  Given the notifications read fails
  Then an error + Retry are shown, and Retry re-runs the read

Scenario: The wire kind/severity are string names
  Given a notification with severity "Warning" and an unknown severity "Nope"
  Then "Warning" decodes to the warning band
  And the unknown value falls back to Info
```

**Evidence:** screen tests `empty list shows the empty state`,
`no mark-all action when everything is read`, `error shows retry, which re-fetches`;
model tests `decodes the string kind/severity…`,
`an unknown or missing severity falls back to info`.

---

_Last reviewed:_ `2026-06-06` by `SIMF Team`.
