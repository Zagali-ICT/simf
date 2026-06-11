# Page 033 — الإشعارات · Notifications

Per-page documentation folder (App screen 33).

## Identity
| | |
|---|---|
| Mockup page | **33** (`Mockup.html`) |
| Route | `RouteNames.notifications` → `/notifications` (**signed-in only**) |
| Titles | AR **الإشعارات** · EN **Notifications** |
| Section | 6 — Badge & notifications |
| Nature | **The notification inbox** — a severity-coded card per notification, an unread dot, tap-to-read, and a mark-all-read action |
| App privilege | **Approved account only.** The endpoints require `RequireApprovedAccount`; route 33 is in `_authenticatedRoutes`. |
| Status | API **BUILT** (reuse — `/app/account/notifications/*`); **Flutter screen BUILT** |

## API (authoritative contract)
All three are signed-in (`RequireApprovedAccount`):
- `POST /api/v1/app/account/notifications/list` — body `{ skip, top }` →
  `GridPage<NotificationDto>` (`items`, `pageNumber`, `pageSize`, `total`).
- `POST /api/v1/app/account/notifications/{id}/read` → `true`.
- `POST /api/v1/app/account/notifications/read-all` → `true`.

`NotificationDto`: `id`, `kind` (**string** name), `title`/`titleArabic`,
`body`/`bodyArabic`, `severity` (**string** — `Info`/`Success`/`Warning`/`Error`),
`readAt` (string?), `isRead` (bool), `createdAt` (string). The `kind` + `severity`
enums serialize as their **string** names (D-110); the Flutter layer parses
`severity` tolerantly (unknown → `Info`).

The repository (`NotificationsRepository`, shared with the Page_013 bell
unread-count) gains `getNotifications()` / `markRead()` / `markAllRead()`; no new
endpoint.

## Behaviour
One read returns the first page (interim — no paging UI). Each notification is a
card: a severity icon/colour (success = green check, warning = accent triangle,
error = danger circle, info = navy info), the localized title in bold, the
localized body, and an accent **unread dot** while `!isRead`. Tapping an unread
row marks it read (`POST …/{id}/read`) then refreshes; a read row is inert. The
app-bar **mark-all-read** action (shown only while something is unread) calls
`POST …/read-all` then refreshes. Loading / empty / error+retry states. UI is
interim (final visuals from SIMF-VID-001).

## Tests
- Models: `src/Mobile/simf_app/test/features/notifications/notification_models_test.dart`
  (decode incl. string kind/severity, `isRead`, localized fallback, `GridPage` envelope).
- Widget: `src/Mobile/simf_app/test/features/notifications/notifications_screen_test.dart`
  (list, empty, error→retry, tap-unread→markRead+refresh, mark-all calls the repo).
- E2E: [`docs/tests/e2e/mobile-notifications.md`](../../tests/e2e/mobile-notifications.md).
