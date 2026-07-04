# Notifications — الإشعارات (Page 033, `#33`)

- **Route:** `/notifications` (`RouteNames.notifications`). Access: approved account (`RequireApprovedAccount`).
- **Figma:** **223:4264** (authoritative, per the class doc-comment). _Note: the extracted widgets' inline comments still cite `758:2491` — a stale secondary node id for the same screen (flagged, D-621)._
- **Clean-code freeze:** D-621 (2026-07-04). Built D-312.

## Purpose

The notification inbox. One read (`POST /app/account/notifications/list`) fills the
list; opening the inbox auto-marks everything read (clears the Home bell badge).
Search + الكل/جلسات/VIP chips filter client-side; the list is grouped by day
(اليوم/أمس/date). An unread row taps to mark-read and deep-links actionable kinds
(`SessionRatingRequest` → the Session rating form).

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `notifications_screen.dart` (318) | State — load/auto-mark, per-item mark + deep-link, mark-all, filters, `_buildBody`; the filter-kind consts |
| `widgets/notification_filter_chip.dart` | `NotificationFilterChip` |
| `widgets/notification_category_icon.dart` | `NotificationCategoryIcon` — the per-kind colour/glyph table |
| `widgets/notification_card.dart` | `NotificationCard` (+ `_UnreadDot`, `_timeFormat`) |
| `widgets/notification_grouped_list.dart` | `NotificationGroupedList` (+ `_dayLabel`, `_dateFormat`) |

The error / empty / no-matches states use the shared `SimfPullableHost` (D-621
replaced the local `_PullableState` copy).

## L4 Figma parity (frame 223:4264)

`notifications_223-4264` golden held without `--update` — layout matches: header,
search+tune, chips (الكل gold), day groups, cards (icon inline-end, unread dot
inline-start), bottom nav, per-kind icon colours. **Deliberate deviation:** the VIP
icon is a **star**, not the mockup's ✕ close-circle (a ✕ on a positive invite reads
as an error).

## Level-F

Wired: search + 3 chips (client-side), row tap → mark-read + `SessionRatingRequest`
deep-link, mark-all, pull-to-refresh, retry. Reads the notifications repo
(`list` / `read` / `read-all` + unread-count invalidate). No missing API.

## Tests

`test/features/notifications/notifications_screen_test.dart` + `notifications_repository_test.dart`
+ `test/golden/notifications_golden_test.dart` (23 total). E2E: `docs/tests/e2e/mobile-notifications.md`.
