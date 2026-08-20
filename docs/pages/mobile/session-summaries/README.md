# Session summaries (ملخص الجلسات) — mobile `/session-summaries`

| Field | Value |
|---|---|
| Route | `/session-summaries` (`RouteNames.sessionSummaryList`, page #111) · **public** — not auth-gated and not role-gated, so a guest browses the list; the جلساتي / المفضلة tabs need an approved account to fill |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/ai_summary/session_summary_list_screen.dart` (`SessionSummaryListScreen`, 232 lines, `ConsumerStatefulWidget`) |
| Widgets | `features/ai_summary/widgets/session_summary_list_card.dart` (`SessionSummaryCard`) + `category_pill.dart` (`CategoryPill`); shared `SimfFilterSearchField`, `SessionFilterTabs`, `FavouriteHeartButton`, `SessionIconLine` / `SessionMetaGroup`, `SessionStateChipRow` |
| Figma node | `1388:8392` (day header `1388:8428`, card `1388:8430`, time line `1388:8439`, meta glyphs `1388:8441 / 8449 / 8457`, chip row `1388:8462`) |
| Shell | `SimfPageShell` (title ملخص الجلسات) |
| API | `GET /app/programme/sessions` (the whole programme, cached) · `GET /app/sessions/favourites` + `POST` / `DELETE /app/sessions/{id}/favourite` (the heart) · `GET /app/account/sessions` (the جلساتي set) |
| Providers | `programmeSessionsProvider` (`features/sessions/data/sessions_repository.dart`) · `sessionFavouritesProvider` (`session_favourites.dart`) · `mySessionsProvider` (`features/myarea/data/my_sessions_repository.dart`) |
| Tests | `test/features/ai_summary/session_summary_list_screen_test.dart` (7); golden `test/golden/session_summary_list_golden_test.dart` (`goldens/session_summary_list_1388-8392.png`). E2E [`mobile-session-summaries.md`](../../../tests/e2e/mobile-session-summaries.md) |
| Status | ✅ Real — Figma `1388:8392`; **clean-code frozen (D-613)** — 596 → 249 lines plus the card widget, and the search field was lifted to the shared `SimfFilterSearchField` (DRY with الوفود) |

## 1. Purpose

The searchable, day-grouped index of sessions that already have a published
محضر. Tapping a card opens that session's summary detail
([ai-summary](../ai-summary/README.md), #34).

## 2. Audience & access

Public. The programme read is anonymous, so the الكل tab works signed out. The
جلساتي and المفضلة tabs read per-user endpoints that need an approved account —
signed out they resolve empty, so those tabs show their own empty message rather
than an auth wall.

## 3. Entry point

Home → "الميزات الذكية" section
(`features/home/widgets/home_smart_features_section.dart:67`) →
`pushNamed(RouteNames.sessionSummaryList)`.

## 4. UI & behaviour

A `Column`: search field, tab row, then the list in an `Expanded`.

- **Search** — `SimfFilterSearchField` (`showFilterIcon: false`, hint
  `sessionSummarySearchHint`). Local UI state only; no server query. The needle is
  lower-cased and matched against the session title (both languages) and every
  speaker's name (both languages), joined into one haystack.
- **Tabs** — `SessionFilterTabs` with `equalWidth: true`; the frame has exactly
  three: الكل · جلساتي · المفضلة.
- **The summary filter is unconditional.** `_filter` drops any session where
  `hasPublishedSummary` is false *before* the tab and search filters run (owner
  2026-07-14) — a future or not-yet-summarised session must never appear here.
- **Day grouping** — `sessionDays(filtered)` yields the distinct local days;
  `ListView.builder` builds one day block per day, each headed
  `l10n.eventDayLabel(n)` in `SimfTokens.labelWhiteMediumLg` (Inter Medium w500,
  per frame `1388:8428` — deliberately *not* w600).
- **Card** (`SessionSummaryCard`) — title over a clock line (`hh:mm a` on the
  Saudi wall clock via `formatSaudiTime12`, plus the duration), the favourite
  heart on the trailing edge, then the primary speaker (`name · title`) and the
  hall, then a bottom row with the category pill and the state chips. The
  **summary chip is deliberately suppressed** (`hasPublishedSummary: false` is
  passed to `sessionStateChips`) because the entire list is summarised, so the
  chip would be noise — only مباشر الآن / مسجّل can appear.

`_buildList` watches `sessionFavouritesProvider` and `mySessionsProvider` purely
to re-run the filter when those per-user sets resolve, keeping the two right-hand
tabs and the hearts live.

## 5. Actions

| Control | Handler | Effect |
|---|---|---|
| Back | `backOrHome(context)` | Pops, or Home |
| Search field | `setState(_query = value)` | Local filter, no fetch |
| الكل / جلساتي / المفضلة | `setState(_tab = ...)` | Local filter |
| Heart | `FavouriteHeartButton` → `SessionFavouritesController.toggle` | Optimistic flip, then `POST` / `DELETE /app/sessions/{id}/favourite`; reverts on failure so the heart never lies |
| Card tap | `pushNamed(RouteNames.aiSummary, {sessionId})` | Opens the summary detail (#34) |
| Pull-to-refresh | `refreshAsync(ref, programmeSessionsProvider.future)` | Re-reads the programme |
| Retry (error state) | `ref.invalidate(programmeSessionsProvider)` | Re-fetches |

## 6. States

| State | Render |
|---|---|
| Loading | `SimfLoadingState` |
| Error | `SimfRefreshableMessage` + `SimfErrorState` (`l10n.aiSummaryError` + retry) |
| Empty | `SimfRefreshableMessage` + `SimfEmptyState` (`Icons.summarize_outlined`), message chosen by `_emptyMessage`: no programme at all → `aiSummaryNoSessions`; a live search → `sessionsNoMatch`; جلساتي → `sessionsNoMine`; المفضلة → `sessionsNoFavourites`; otherwise (programme exists, nothing summarised) → `sessionSummariesEmpty` |
| Data | The day-grouped list above |

**Do not wrap the error branch in `SimfPullableHost`.** The code carries an
explicit comment about this: `SimfRefreshableMessage` already wraps its child in
one, and nesting two nests a `SingleChildScrollView` inside a
`SingleChildScrollView`, which hands the inner `LayoutBuilder`
`maxHeight: infinity` and throws "BoxConstraints forces an infinite height" —
taking the whole screen down whenever the sessions provider errored.

## 7. i18n / RTL

`AppL10n`: `sessionSummariesTitle` (ملخص الجلسات — deliberately distinct from
`aiSummaryTitle`, the singular detail header) · `sessionSummarySearchHint` ·
`sessionsTabAll` / `sessionsTabMine` / `sessionsTabFavourites` ·
`eventDayLabel(n)` · `sessionDurationMinutes(n)` · the five empty messages ·
`aiSummaryError` · `retryLabel`. Titles, speakers, halls and categories are all
bilingual pairs picked by `l10n.isArabic`. The tab row is RTL-ordered by the
directionality, and the frame's three equal-width tabs are pinned by
`equalWidth: true`.
