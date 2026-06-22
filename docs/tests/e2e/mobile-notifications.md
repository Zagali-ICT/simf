# E2E test catalogue — `Notifications` (`notifications`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> notification reads/writes are already built + signed-in
> (`RequireApprovedAccount`). The **Flutter screen is built** and tested in
> `src/Mobile/simf_app/test/features/notifications/notifications_screen_test.dart`
> (list, empty, error→retry, open-marks-all-read + Home-badge clear) plus the model
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
| E2E-MOB033-002 | Opening the inbox marks every unread notification read **and clears the Home bell badge** (#13/#14 — the backend has read/unread only, no "seen") | happy | P0 | authored ✓ (screen `opening marks every unread notification read`) |
| E2E-MOB033-003 | Opening an all-read inbox does not call mark-all; the explicit "Mark all read" button only shows for mid-session unread, and it + a per-item tap also clear the Home badge | edge | P1 | authored ✓ (screen `opening an all-read inbox does not call mark-all`) |
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

### E2E-MOB033-002 — Opening marks all read + clears the badge / E2E-MOB033-003 — Mark-all + per-item

```gherkin
Scenario: Opening the inbox marks every notification read (#13)
  Given at least one notification is unread
  When I open the notifications screen (it loads the list)
  Then the app calls POST /api/v1/app/account/notifications/read-all
  And the items show as read (the unread dots clear)
  And the Home bell badge count is refreshed to reflect zero unread (#14)
  # The backend models read/unread only (no separate "seen" state), so an
  # opened inbox is treated as read.

Scenario: An opened all-read inbox does nothing extra
  Given every notification is already read
  When I open the notifications screen
  Then no read-all call is made, and no "Mark all read" button is shown

Scenario: Mid-session mark-all / per-item read also clear the Home badge (#14)
  Given a notification arrives (or open's read-all failed) so an item is unread
  When I tap that card (or the "Mark all read" button)
  Then the app marks it/all read AND invalidates the Home bell count provider
  And the Home badge no longer shows the stale unread number
```

**Evidence:** screen tests `opening marks every unread notification read`,
`opening an all-read inbox does not call mark-all`; the per-item + mark-all paths
share `_markAllRead` / `_onTapItem`, both invalidating `unreadNotificationCountProvider`.

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

_Last reviewed:_ `2026-06-21` by `SIMF Team`.
