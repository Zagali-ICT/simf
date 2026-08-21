# Notifications — الإشعارات (Page 033, `#33`)

- **Route:** `/notifications` (`RouteNames.notifications`). Access: approved account (`RequireApprovedAccount`).
- **Figma:** **223:4264** (authoritative, per the class doc-comment). _Note: the extracted widgets' inline comments still cite `758:2491` — a stale secondary node id for the same screen (flagged, D-621)._
- **Clean-code freeze:** D-621 (2026-07-04). Built D-312.

## Purpose

The notification inbox. One read (`POST /app/account/notifications/list`) fills the
list; opening the inbox auto-marks everything read (clears the Home bell badge).
Search + الكل/جلسات/VIP chips filter client-side; the list is grouped by day
(اليوم/أمس/date). An unread row taps to mark-read and deep-links actionable kinds
— see [Deep-linking a tile](#deep-linking-a-tile) for the allow-list that decides
where a tap lands.

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `notifications_screen.dart` (313) | State — load/auto-mark, per-item mark + deep-link, mark-all, the search/chip filter, `_buildBody` / `_buildInbox`; the `_allowedClickPaths` set |
| `notification_filters.dart` | `NotificationFilter` (all/sessions/vip), `sessionsChipGroups` / `vipChipGroups`, and `groupForItem` — the server `group` with a kind fallback for pre-migration rows |
| `widgets/notifications_filter_bar.dart` | `NotificationsFilterBar` — the three chips plus the "Mark all read" action |
| `widgets/notification_filter_chip.dart` | `NotificationFilterChip` |
| `widgets/notification_category_icon.dart` | `NotificationCategoryIcon` — the per-kind colour/glyph table |
| `widgets/notification_card.dart` | `NotificationCard` — title/body/"{time} · {day}" stamp via `formatSaudiTime12` (`core/utils/saudi_time.dart`) |
| `widgets/unread_dot.dart` | `UnreadDot` — the 14-px **red** (`SimfTokens.danger`) unread marker the card positions at its top inline-end corner |
| `widgets/notification_grouped_list.dart` | `NotificationGroupedList` (+ `_dayLabel`, which resolves اليوم/أمس/`{day} {month}` off Saudi time) |

The no-matches state uses the shared `SimfPullableHost` (D-621 replaced the local
`_PullableState` copy); the error and empty states use `SimfRefreshableMessage`,
which is that same host already paired with the pull-to-refresh wrapper.

**The day feed is lazy.** `NotificationGroupedList` used to be a
`ListView(children: …)` built from nested `for`s over the day runs, so every card
in the whole history was constructed on first paint. It is now a
`ListView.builder` over a flattened row list — a row is either a day header or a
card, and each row carries the day label the card stamps its time with. Cards get
a `ValueKey(item.id)` so a mark-read flip re-uses the right element instead of
shifting state down the list. The grouping is unchanged: runs, not buckets (the
API returns newest-first, so "اليوم" heads the list once rather than collecting
every today-row from the whole history), and the `AlwaysScrollableScrollPhysics`
that makes the pull-to-refresh fire on a short list is preserved.

## L4 Figma parity (frame 223:4264)

`notifications_223-4264` golden held without `--update` — layout matches: header,
search+tune, chips (الكل gold), day groups, cards (category icon inline-**start**,
as the first child of the card Row; unread dot inline-**end**, positioned), bottom
nav, per-kind icon colours. _(This line read "icon inline-end, unread dot
inline-start" until 2026-08-20 — the two are the other way round in
`notification_card.dart`, whose own class doc-comment still carries the same
inverted claim — and calls the dot gold, where `UnreadDot` paints
`SimfTokens.danger`. The golden is the parity evidence; only the prose was
wrong.)_ **Deliberate deviation:** the VIP
icon is a **star**, not the mockup's ✕ close-circle (a ✕ on a positive invite reads
as an error).

## Deep-linking a tile

A tap runs `_maybeDeepLink` **before** the mark-read call, so an actionable tile
still navigates even if the best-effort write leaves the screen unmounted.

1. **The server `clickUrl` wins.** `NotificationKindCatalog.ClickUrlFor` stamps an
   app-internal location on the row; the app pushes it verbatim — but only after
   checking it against `_allowedClickPaths`, because the router has no error page
   and an unknown route would be worse than doing nothing. Only the **path** is
   compared; the query string is ignored (D-678), so `/rate?code=Session&targetId=…`
   and `/rate?code=Day&targetId=…` both reduce to the single `/rate` entry. The set
   is `/rate`, `/badge`, `/meeting-confirm`, `/meetings` and `/meet`.
2. **Otherwise the kind decides**, for rows created before the `clickUrl` column:
   `SessionRatingRequest` (with a related id) → the Session rating form;
   `BookingConfirmed` and `AccountApproved` → the personal entry badge `/badge`.
   Every other kind only marks itself read.

**`MatchRecommended` tiles were inert until 2026-08-20.** FR-803's recommendation
push stamps `clickUrl = /meet` (the Meet-people partner directory,
`RouteNames.meetPeople`) — but `/meet` was never added to the allow-list, and this
guard fails **silently**: the path misses, `_maybeDeepLink` returns, the tile is
marked read, and nothing else happens. No log, no toast, no crash. FR-803's only
notification entry point had been dead since it shipped. This is the **second**
recurrence of the same failure — QA A27 did it to the four meeting-lifecycle kinds
(`/meetings`), and the comment recording that fix was sitting directly above an
allow-list that had already drifted again.

The two sets are no longer kept in step by whoever remembers to edit both. They
are pinned against each other by
`tests/SIMF.Domain.Tests/NotificationClickUrlContractTests.cs`, which reads the
catalogue and this screen as text and fails the build on any clickUrl the server
can emit that the app would refuse. The pin is deliberately **one-directional**: a
server url the app rejects is a defect, while an app-allowed path the server never
emits is harmless (the app may allow a path for a kind the backend has not started
sending yet, and asserting equality would red the build on a half-landed feature).

## Level-F

Wired: search + 3 chips (client-side), row tap → mark-read + the clickUrl/kind
deep-link above, mark-all, pull-to-refresh, retry. Reads the notifications repo
(`list` / `read` / `read-all` + unread-count invalidate). No missing API.

## Tests

`test/features/notifications/notifications_screen_test.dart` + `notifications_repository_test.dart`
+ `test/golden/notifications_golden_test.dart` (23 total). E2E: `docs/tests/e2e/mobile-notifications.md`.
