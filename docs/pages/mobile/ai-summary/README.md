# AI session summary (ملخص الجلسة) — mobile `/ai-summary?sessionId=`

| Field | Value |
|---|---|
| Route | `/ai-summary?sessionId=` (`RouteNames.aiSummary`, page #34) · **public** — no auth gate, no role gate; the `sessionId` query parameter is optional |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/ai_summary/session_summary_screen.dart` (`AiSummaryScreen`, 271 lines, `ConsumerStatefulWidget`) — note the class name and the file name differ; the file is named for the feature, the class for the route |
| Widgets | `features/ai_summary/widgets/summary_session_card.dart` (`SummarySessionCard`) · `summary_video_card.dart` (`SummaryVideoCard`) · `summary_content_card.dart` (`SummaryTabContentCard`, `SummaryBullet`) · `summary_generate_card.dart` (`SummaryGenerateCard`); shared `SessionFilterTabs` |
| Figma node | `1072:13518`; the tab row is `1072:14647` |
| Shell | `SimfPageShell` (title ملخص الجلسة) |
| API | `GET /app/programme/sessions/{id}/summary` → `PublicSessionSummary` (`AllowAnonymous`) · `GET /app/programme/sessions` for the session card + the same-day agenda |
| Providers | `sessionSummaryProvider(sessionId)` over `sessionSummaryRepositoryProvider` (`features/ai_summary/data/session_summary_repository.dart`) · `programmeSessionsProvider` |
| Tests | `test/features/ai_summary/session_summary_screen_test.dart` (7) + `session_summary_models_test.dart` (6); golden `test/golden/session_summary_golden_test.dart` (`goldens/session_summary_1072-13518.png`). E2E [`mobile-ai-summary.md`](../../../tests/e2e/mobile-ai-summary.md) |
| Legacy detail | `docs/App/Page_034/` — superseded by this file; the old copy described the D-465 stacked-card re-skin, which the tabbed layout replaced |
| Status | ✅ Real — built D-317; **clean-code frozen (D-612)** — 715 → 291 lines plus three widget files and three DRY wins (shared `gregorianWeekdayName` / `SimfEmptyState` / `SimfErrorState`) |

## 1. Purpose

The published محضر for one session: the session card with its same-day agenda,
the recording and summary-video players, three tabs of extracted content
(أبرز النقاط · التوصيات · المتحدثون), and the expandable full text.

> **Contract.** The summary is **Committee-generated in the Control Panel**
> (D-237 / D-472). This screen is a read-only consumer — the gold button reveals
> already-published text, it does **not** trigger generation. The button's l10n
> key is still `aiSummaryGenerateButton`, which reads like a generator; it is an
> expand/collapse toggle.

## 2. Audience & access

Anonymous. Both reads are public, so a guest opening a summary from the
presentations list or the summaries list gets the full page.

## 3. Entry points

| From | Code |
|---|---|
| Session summaries list (#111) — card tap | `features/ai_summary/widgets/session_summary_list_card.dart:54` |
| Session presentations (#202) — the gold تحميل button | `features/sessions/widgets/presentation_card.dart:40` |
| Session detail (#17) | `features/sessions/session_detail_screen.dart:216` |

## 4. UI & behaviour

The body is a `ListView` inside `SimfPullToRefresh`:

1. **`SummarySessionCard`** — the selected session plus `_dayAgenda(list)`: every
   *other* session on the same local day, sorted by start. Carries the duration
   label computed from `endLocal - startLocal`.
2. **Video players** (`_videoPlayers`, item #35) — up to two `SummaryVideoCard`s,
   each added only when its URL is non-empty: the session's **full live
   recording** (`recordingUrl`, sourced server-side from `Session.LiveStreamUrl`)
   then the team's **short summary video** (`summaryVideoUrl`). A session with
   neither contributes no widgets, so there is no layout shift.
3. **`SessionFilterTabs`** (`equalWidth: true`) — أبرز النقاط · التوصيات ·
   المتحدثون, in that declaration order, which renders right-to-left in Arabic.
   أبرز النقاط is the default.
4. **`SummaryTabContentCard`** — the active tab's block, split on `\n` into
   trimmed non-empty lines and rendered as `SummaryBullet`s.
5. **`SummaryGenerateCard`** — the expandable full-text paragraph, open by
   default (`_summaryExpanded = true`).

### Selection

`_ensureSelection` runs after the programme resolves, in a post-frame callback:

- no `sessionId` passed → selects the **first** programme session;
- a `sessionId` passed but no metadata yet → matches it in the list, falling back
  to the first when the id is unknown.

`_summaryAsync` watches `sessionSummaryProvider(id)` only once an id exists;
until then it is `AsyncValue.data(null)` — **data-null, not loading** — so the
tabs render their empty note instead of a spinner.

## 5. Actions

| Control | Handler | Effect |
|---|---|---|
| Back | `backOrHome(context)` | Pops, or Home |
| Tab (×3) | `setState(_tab = ...)` | Swaps the content card's block |
| Full-text card header | `setState(_summaryExpanded = !...)` | Expand / collapse |
| Pull-to-refresh | `refreshAsync(ref, programmeSessionsProvider.future)` | Re-reads the programme (not the summary itself) |
| Retry (tab error) | `ref.invalidate(sessionSummaryProvider(_selectedId!))` | Re-fetches the summary |

## 6. Data contract (`SessionSummary`, `GET /app/programme/sessions/{id}/summary`)

Wire keys (D-219 frozen): `keyPoints` / `keyPointsArabic` · `recommendations` /
`recommendationsArabic` · `speakers` / `speakersArabic` · `fullText` /
`fullTextArabic` · `generatedByAi` · `publishedAt` · `recordingUrl` ·
`summaryVideoUrl`.

The four content pairs are **newline-delimited** — one bullet per non-empty line.
`publishedAt` is kept as the raw string and parsed lazily by `parseWireOrNull`.

**A 404 is folded to `null`, not to an error.** `sessionSummaryProvider` catches
`ApiFailure` and returns `null` when `httpStatus == 404`, because an unpublished
summary is not a failure; anything else rethrows to the retry surface.

## 7. States

| State | Render |
|---|---|
| Programme loading | `SimfLoadingState` |
| Programme error **or** empty | `SimfPullableHost` + `SimfEmptyState` (`Icons.event_busy_outlined`, `l10n.aiSummaryNoSessions`) — an error and an empty programme deliberately share one surface here |
| Summary loading | Gold spinner *inside* the tab content card; the session card and tabs stay on screen |
| Summary error | `SimfErrorState` inside the content card (`l10n.aiSummaryError` + retry) |
| No published summary (404 → null) | Tabs render `l10n.aiSummaryNone`; the full-text card shows the same note; both video players are absent |

## 8. i18n / RTL

`AppL10n`: `aiSummaryTitle` (ملخص الجلسة) · `aiSummaryKeyPointsHeading` /
`aiSummaryRecommendationsHeading` / `aiSummarySpeakersHeading` ·
`aiSummaryGenerateButton` · `aiSummaryRecordingLabel` / `aiSummaryVideoLabel` ·
`aiSummaryNone` · `aiSummaryNoSessions` · `aiSummaryError` ·
`sessionDurationMinutes(n)` · `aiSummarySessionLabel` · `retryLabel`. Every
content block is a bilingual pair picked by `l10n.isArabic`. Times run on the
Saudi wall clock (`saudiNow`, `sameLocalDay`).

## 9. Findings (recorded, not changed)

1. **`generatedByAi` is decoded and never rendered.** The model comment says it
   "drives the 'generated by AI' banner", and the legacy Page_034 doc described
   that banner — but no widget in the app reads the field (verified by grep: the
   only hits are the model's own declaration, factory and doc comment). Either the
   banner was dropped in the D-612 rework or it never reached the tabbed layout.
2. **`publishedAt` is decoded and never rendered** on this screen either.
3. **Pull-to-refresh re-reads the programme, not the summary.** A محضر edited in
   the CP while the screen is open is not picked up by the pull; only the
   in-card retry (which requires an error) invalidates `sessionSummaryProvider`.
