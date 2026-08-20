# My sessions (عروض الجلسات) — mobile `/my-sessions`

| Field | Value |
|---|---|
| Route | `/my-sessions` (`RouteNames.myAreaSessions`, page #113) · role-gated to `_attendee` = `{AppRole.visitor, AppRole.exhibitor}`; the read is `RequireApprovedAccount` server-side |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/myarea/my_sessions_screen.dart` (`MySessionsScreen`, 113 lines, `ConsumerStatefulWidget` — it owns only the tab) |
| Widgets | `features/myarea/widgets/my_sessions_tabbed_list.dart` (`MySessionsTabbedList`, `MySessionCard`); shared `SessionFilterTabs`, `FavouriteHeartButton`, `SessionIconLine` / `SessionMetaGroup`, `SessionStateChipRow`, `SimfCard`, `SimfPullToRefresh` |
| Figma node | `1388:9067` (tab row `1388:9077`, card `1388:9115`) |
| Shell | `SimfPageShell` (title عروض الجلسات) |
| API | `GET /app/account/sessions` (`MyAreaEndpoints.sessions`, `RequireApprovedAccount`) · `GET/POST/DELETE /app/sessions/favourites` behind the heart |
| Providers | `mySessionsProvider` over `mySessionsRepositoryProvider` (`features/myarea/data/my_sessions_repository.dart`) · `sessionFavouritesProvider` (inside `FavouriteHeartButton`) |
| Tests | `test/features/myarea/my_sessions_screen_test.dart` (3) + `my_sessions_models_test.dart` (3); golden `test/golden/my_sessions_golden_test.dart` (`goldens/my_sessions_1388-9067.png`). E2E [`mobile-my-sessions.md`](../../../tests/e2e/mobile-my-sessions.md) |
| Status | ✅ Real — **restored by D-710 (2026-07-09)**, the owner having reversed the D-609 removal: the screen was recovered, re-routed, linked from the More menu, and its golden + tests re-locked |

## 1. Purpose

The signed-in attendee's own session list, split four ways: القادمة · حضرتها ·
فاتتني · الأرشيف.

> **Contract.** The four tabs are partitioned **client-side from the device
> clock** (`saudiNow()`). The wire carries no tab — the API returns one flat list
> and the screen decides which rows belong where.

## 2. Audience & access

Approved attendee (visitor or exhibitor). The More menu hides the entry entirely
for a role that cannot open it: `more_forum_info_section.dart:44` guards the tile
with `routeAllowsRole(RouteNames.myAreaSessions, role)` before rendering it.

## 3. Entry point

More (#31) → "معلومات الملتقى" section →
`pushNamed(RouteNames.myAreaSessions)` (`more_forum_info_section.dart:47`).

## 4. UI & behaviour

A `Column`: the tab row, then the list in an `Expanded`.

- **Tabs** — `SessionFilterTabs` with `equalWidth: true`, `gap: SimfTokens.space2`
  and a leading glyph on each, matching frame `1388:9077`'s four equal-width
  tabs: `Icons.upcoming_outlined` القادمة · `Icons.event_available_outlined`
  حضرتها · `Icons.event_busy_outlined` فاتتني · `Icons.archive_outlined` الأرشيف.
- **Partition** (`_filter`, against `nowUtc = saudiNow()`):

  | Tab | Predicate |
  |---|---|
  | القادمة | `item.isUpcoming(nowUtc)` — `start.isAfter(now)` |
  | حضرتها | `item.attended` (the server's per-user flag) |
  | فاتتني | `item.hasEnded(nowUtc) && !item.attended` |
  | الأرشيف | `item.isArchived` — `status` is `recorded` or `published`, i.e. there is a replayable recording |

  The four are **not** a strict partition: an attended session that is also
  `published` appears under both حضرتها and الأرشيف, which is intended.

- **Count header** — `MySessionsTabbedList` prepends one non-card row
  (`itemCount: items.length + 1`, index 0) carrying
  `l10n.mySessionsCount(items.length, tabLabel)`.
- **Card** (`MySessionCard`) — the title over a clock line reading
  `time · category` (or just the time when the session has no category), the
  favourite heart on the trailing edge, then the primary speaker
  (`name · rank`) and the hall, then the state chips.

  The chips come from `sessionStateChips(phase:, hasPublishedSummary: false,
  status:)` — **`false` is passed deliberately**: this list carries no summary
  flag on the wire, so only مباشر الآن / مسجّل can appear (owner 2026-07-14, the
  same rule the agenda follows).

## 5. Actions

| Control | Handler | Effect |
|---|---|---|
| Back | `backOrHome(context)` | Pops, or Home |
| Tab (×4) | `setState(_tab = ...)` | Local re-partition, no fetch |
| Heart | `FavouriteHeartButton` → `SessionFavouritesController.toggle` | Optimistic flip then `POST` / `DELETE /app/sessions/{id}/favourite`; reverts on failure |
| Card tap | `pushNamed(RouteNames.sessionDetail, {sessionId})` | Session detail (#17) |
| Pull-to-refresh | `refreshAsync(ref, mySessionsProvider.future)` | Re-reads the list |
| Retry (error state) | `ref.invalidate(mySessionsProvider)` | Re-fetches |

## 6. Data contract (`MyAreaSessionItem`, `GET /app/account/sessions`)

The envelope is `{ items: [...] }` (`MyAreaSessions.fromData`). Per item (D-219
frozen keys), mirroring `SIMF.Contracts.Account.MyAreaSessionItem`: `id` ·
`title` · `titleArabic` · `start` · `end` · `status` · `attended` ·
`isFavourite` · `hallNameEn` / `hallNameAr` · `categoryNameEn` /
`categoryNameAr` · `speakerNameEn` / `speakerNameAr` · `speakerTitle`.

`start` / `end` are zone-free on the wire; `startLocal` reads through `saudiOf`.
`durationMinutes` floors at 0 so a malformed pair cannot render a negative
length.

## 7. States

| State | Render |
|---|---|
| Loading | `SimfLoadingState` |
| Error | `SimfPullToRefresh` over a `ListView` holding `SimfErrorState` (`l10n.mySessionsError` + retry) — hand-nested here rather than using `SimfRefreshableMessage` |
| Empty (per tab) | `SimfEmptyState` (`Icons.event_note_outlined`, `l10n.mySessionsEmpty`) inside an always-scrollable `ListView`, so the pull still fires on the short body |
| Data | Count header + `ListView.separated` of cards |

## 8. i18n / RTL

`AppL10n`: `mySessionsTitle` (عروض الجلسات) · `mySessionsTabUpcoming` /
`mySessionsTabAttended` / `mySessionsTabMissed` / `mySessionsTabArchive` ·
`mySessionsCount(n, tab)` · `mySessionsEmpty` · `mySessionsError` ·
`retryLabel`. Titles, halls, categories and speaker names are bilingual pairs
picked by `l10n.isArabic`. The card's time uses
`TimeOfDay.fromDateTime(startLocal).format(context)`, so its 12/24-hour form and
its `ص` / `م` marker follow the platform locale.

## 9. Findings (recorded, not changed)

1. **`isFavourite` is decoded and never read.** The heart's state comes from the
   app-wide `sessionFavouritesProvider` set instead, so the per-row flag the API
   sends is redundant on this screen — and can disagree with the heart if the two
   sources drift.
2. **Two inline `TextStyle`s remain** in `my_sessions_tabbed_list.dart` (the count
   header and the card title) — they assemble token atoms
   (`SimfTokens.textLg` / `textMd`, `SimfTokens.surface`) rather than raw numbers,
   but §5.1 of the app CLAUDE.md counts both forms and wants a named token style.
3. **The time format is platform-locale-driven, unlike the summaries list**, which
   pins 12-hour Saudi wall-clock time through `formatSaudiTime12`. The two
   session lists therefore render the same start differently on a 24-hour device.
