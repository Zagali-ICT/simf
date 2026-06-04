# Visitor notifications inbox (Web) — `/account/notifications`

| | |
|--|--|
| **Route** | `/account/notifications` (Website) |
| **Audience** | Any signed-in visitor |
| **Auth** | `[Authorize]` |
| **Status** | ✅ Real |
| **Backend** | `POST /account/api/notifications/list`, `DELETE /account/api/notifications/{id}`, `POST /account/api/notifications/read-all` |
| **Source** | [`Notifications.razor`](../../../src/Website/SIMF.Web/Components/Pages/Account/Notifications.razor) |
| **Last reviewed** | 2026-05-28 |

## 1. Purpose

Visitor-side notification inbox. Reachable from the **Notifications** link
in the UserProfile header (added in D-132 to close the orphan-page audit
finding).

## 11. E2E

| Scenario | ID |
|----------|----|
| Visitor lands from profile link | E2E-WEB-NTF-001 |
| Empty inbox → friendly empty state | E2E-WEB-NTF-002 |
| Dismiss a notification | E2E-WEB-NTF-003 |

## 12. Related

- D-132 — orphan fix: UserProfile header now links here.
- CP equivalent: [`cp/account-notifications.md`](../cp/account-notifications.md).

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 5).
