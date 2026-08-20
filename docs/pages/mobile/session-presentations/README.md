# Sessions (الجلسات) — mobile `/session-presentations`

| Field | Value |
|---|---|
| Route | `/session-presentations` (`RouteNames.sessionPresentations`, page #202) · **public**. It is deliberately *not* in `_routeRoles` (owner 2026-07-22): a guest opens it from the Home "الجلسات" tile, and both of its reads are `AllowAnonymous`. |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/sessions/session_presentations_screen.dart` (`SessionPresentationsScreen`, 61 lines, `ConsumerStatefulWidget` — it owns only the day tab) |
| Widgets | `features/sessions/widgets/presentations_body.dart` (`PresentationsBody`) · `presentation_card.dart` (`PresentationCard`) · `file_icon.dart` (`FileIcon`) · `session_summry_button.dart` (`SessionSummryButton`); shared `SessionFilterTabs`, `SimfCard`, `SimfPullToRefresh`, `SimfRefreshableMessage` |
| Pure helper | `features/sessions/data/presentation_summary_gate.dart` — `presentationSummaryReady(item, session, nowUtc)` |
| Figma node | `1388:7621` (card `1388:7640`); the title is **"الجلسات"**, matching the Home tile |
| Shell | `SimfPageShell` (title الجلسات) |
| API | `GET /app/presentations` → `PublicPresentations` · `GET /app/programme/sessions` for the summary-ready gate. `GET /app/presentations/{id}/file` is **retained on the backend but unused by this screen** (see §6). |
| Providers | `presentationsProvider` over `presentationRepositoryProvider` (`features/sessions/data/presentation_repository.dart`) · `programmeSessionsByIdProvider` |
| Tests | `test/features/sessions/session_presentations_screen_test.dart` (9) + `presentation_models_test.dart` (2); golden `test/golden/session_presentations_golden_test.dart` (`goldens/presentations_1388-7621.png`). E2E [`mobile-session-presentations.md`](../../../tests/e2e/mobile-session-presentations.md) |
| Status | ✅ Real — built from a ComingSoon stub (D-464) against Figma `1388:7621`; **D-704** widened the list to *every* active session, not only those with an uploaded deck |

## 1. Purpose

The day-tabbed list of the forum's sessions, backed by the D-228
`SpeakerPresentation` records. Despite the feature name, this is not a download
screen: a card opens the session detail, and the gold button opens the session's
summary.

## 2. Audience & access

Public. Both reads are anonymous; the shared client simply omits the bearer when
there is no session.

## 3. Entry points

| From | Code |
|---|---|
| Home (signed in) → "عن الملتقى" tiles | `features/home/widgets/home_about_section.dart:72` |
| Home (guest) | `features/home/widgets/guest_home.dart:62` |

## 4. UI & behaviour

The screen resolves two providers and delegates the whole body to
`PresentationsBody`:

- **Day tabs** — `distinctLocalDays(items, (p) => p.sessionStartLocal)` yields the
  event days; the tab labels are `الكل` followed by `l10n.eventDayLabel(n)` for
  each. Tab 0 shows everything; tab *n* filters on `sameLocalDay`.
  `activeTab` guards against a stale selection when the day set **shrinks** on
  refresh (`dayTab < tabLabels.length ? dayTab : 0`).
- **Card** (`PresentationCard`) — the session title over the presenting speaker,
  a `FileIcon` on the trailing edge, then a bottom row with the day label and the
  gold summary button.

### The summary-button gate

`presentationSummaryReady(item, sessionsById[item.sessionId], nowUtc)` decides
whether the gold button is live (owner 2026-07-14):

- when the programme row is loaded → **`session.hasPublishedSummary`**, the same
  signal the summaries list filters on;
- when the programme is not loaded yet (the map is empty) → it falls back to the
  presentation's own start: a not-yet-started session cannot have a summary so
  the button stays inactive; a started or past one keeps it, and a real 404 shows
  the summary screen's own empty note.

A disabled button is greyed and drops its tap.

## 5. Actions

| Control | Handler | Effect |
|---|---|---|
| Back | `backOrHome(context)` | Pops, or Home |
| الكل / اليوم *n* | `setState(_dayTab = i)` | Local filter, no fetch |
| Card tap | `pushNamed(RouteNames.sessionDetail, {sessionId})` | Session detail (#17) |
| ملخص الجلسة (gold) | `pushNamed(RouteNames.aiSummary, {sessionId})` | Session summary (#34) |
| Pull-to-refresh | `refreshAsync(ref, presentationsProvider.future)` | Re-reads the list |
| Retry (error state) | `ref.invalidate(presentationsProvider)` | Re-fetches |

## 6. The button does not download (owner 2026-07-03)

The gold button carries `l10n.sessionSummary` — which reads **"ملخص الجلسة"**,
not "تحميل". The Figma frame and `PAGE-INDEX.md` still call it "the gold تحميل
button"; the label was re-pointed with the behaviour and the older name stuck in
the prose. It opens the **summary**, not the deck. `PresentationRepository.downloadFile` and
`SessionsEndpoints.presentationFile` (`GET /app/presentations/{id}/file`) still
exist and still work — the repository fetches the bytes through the shared client
so the self-signed-TLS handling and the bearer are inherited, which a bare URL
open could not — but **no widget on this screen calls them**. Treat the
download path as retained-but-unwired, not as dead code to delete.

## 7. Data contract (`PresentationItem`, `GET /app/presentations`)

The envelope is `{ items: [...] }` (`PresentationsPage.fromData`). Per item
(D-219 frozen keys), mirroring `SIMF.Contracts.Programme.PublicPresentationItem`:
`id` · `sessionId` · `sessionTitle` · `sessionTitleArabic` · `sessionStart` ·
`speakerName` · `speakerNameArabic` · `fileName` · `contentType` · `sizeBytes`.

`sessionStart` is zone-free on the wire; `sessionStartLocal` reads it on the
Saudi wall clock via `saudiOf`.

## 8. States

| State | Render |
|---|---|
| Loading | `SimfLoadingState` |
| Error | `SimfRefreshableMessage` + `SimfErrorState` (`l10n.presentationsError` + retry) |
| Empty | `SimfRefreshableMessage` + `SimfEmptyState` (`Icons.description_outlined`, `l10n.presentationsEmpty`) — checked before the tabs are built, so an empty list shows no tab row at all |
| Data | Tab row + `ListView.separated` inside `SimfPullToRefresh` |

## 9. i18n / RTL

`AppL10n`: `sessionPresentationsTitle` (الجلسات) · `sessionsTabAll` ·
`eventDayLabel(n)` · `sessionSummary` · `presentationsEmpty` ·
`presentationsError` · `retryLabel`. Titles and speaker names are bilingual pairs
picked by `l10n.isArabic`; the card uses `CrossAxisAlignment.stretch` with
`TextAlign.start`, so it mirrors without directional overrides.

## 10. Findings (recorded, not changed)

1. **`fileName`, `contentType` and `sizeBytes` are decoded and never rendered.**
   The card shows a generic `FileIcon` rather than a per-type glyph or a size, so
   the three file-metadata fields the API sends are invisible. They are the
   natural inputs for the retired download affordance.
2. **The title collides conceptually with the agenda (#16, "الجلسات" as well).**
   Both are reachable from Home; only the entry tile distinguishes them.
