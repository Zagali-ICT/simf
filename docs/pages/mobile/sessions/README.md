# Sessions / Programme (برنامج الملتقى) — mobile `/sessions`

| Field | Value |
|---|---|
| Route | `/sessions` (`RouteNames.sessions`, page #16) · **signed-in Visitor+** (D-576 router gate; a guest is sent to sign-in) |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/sessions/sessions_screen.dart` (`SessionsScreen`) |
| Widgets | `lib/features/sessions/widgets/` — `sessions_search_field` · `programme_day_strip` · `programme_day_banner` · `session_type_tabs` · `session_timeline_row` |
| Figma node | `883:2308` (KSA-Project, file `PSXHhY0UVTAPSaIOf9uNKd`); sub-nodes 883:2316 search · 883:2327 day strip · 1064:13240 day banner · 883:2320 type tabs · 1310:3213/3232 timeline rows |
| Shell | `SimfPageShell` (`SimfTab.sessions`, sweep on; header برنامج الملتقى — the nav tab label is the shared "الجلسات", component 206:1732) |
| API | `GET /app/programme/days` (fetch-once; all filtering client-side) |
| Providers | `sessionsRepositoryProvider` · `simfDataConfigProvider` (day-image base URL) |
| Tests | `test/features/sessions/sessions_screen_test.dart` (9); golden `test/golden/sessions_golden_test.dart` (`goldens/sessions_883-2308.png`); E2E [`mobile-agenda.md`](../../../tests/e2e/mobile-agenda.md) |
| Legacy detail | `docs/App/Page_016/` (Function / Logic / API / Design) — historical spec; its "الأجندة nav label / 4 type tabs" text predates D-597/D-598 |
| Status | ✅ Real — D-299 (built) → D-452 (LIVE-frame relayout) → D-569 (parity refinement) → **clean-code frozen (D-598)** |

## 1. Purpose
The day-grouped forum programme: search, the full-range calendar day strip, the
selected day's title + logo banner, type filter tabs, and the المواعيد timeline
(first session featured with the day banner). Rows open the session detail (#17).

## 2. Audience & access
Signed-in Visitor+ (D-576). The endpoint itself stays anonymous; the gate is
app-UX at the router.

## 3. UI & behaviour
- Loading spinner → content; error → `SimfErrorState` + retry; empty →
  `SimfEmptyState`; the short states sit in the shared `SimfPullableHost` so
  **pull-to-refresh** fires everywhere (adopted D-598 — replaced two hand-rolled
  LayoutBuilder wrappers).
- Search (883:2316): white 18px magnifier at the inline-start, white hint, live
  client-side filter over title/description/code (both languages). Deliberately
  NOT the shared `SimfSearchField` — different frame design (recorded on the
  widget).
- Day strip (883:2327): WHITE band spanning the full first→last date range,
  **pinned LTR** (dates ascend left→right exactly as the frame renders — D-598
  fix; the app previously laid it RTL). Selected day = navy pill; session days =
  navy text; empty in-between days = muted, not tappable; weekend labels red.
  The frame's "WEN" label is a designer typo — the app keeps WED (deliberate).
- Day title + banner (1064:13240): the day's OWN title (not a static label) over
  the 85px logo banner with the gold anchor badge; navy anchor-glyph fallback.
- Type tabs (883:2320): **three** tabs الكل / جلسات / ورش العمل — the old
  fourth احداث tab was dropped to match the frame (owner 2026-07-03, D-598);
  event-type sessions remain visible under الكل.
- المواعيد timeline rows (1310:3213/3232): navy card, LTR time rail
  (start→connector→end) at the inline-start with the faint divider, gold title +
  trailing gold calendar glyph, 2-line description; the day's first session is
  featured with the banner image. Row tap → `/sessions/:id`.

## 4. Button / action audit (Level F, 2026-07-03)
| Control | Handler | Backend |
|---|---|---|
| Back (circled) | `backOrHome` | — |
| Search field | client-side filter (setState) | — |
| Day cell (has sessions) | select day (setState) | — |
| Type tab | filter (setState) | — |
| Timeline row | push `/sessions/:sessionId` #17 | detail GET on that screen |
| Pull-to-refresh / retry | `_load()` re-fetch | `GET /app/programme/days` |

All data repo-backed; filtering is deliberately client-side over the one fetch.

## 5. Clean-code freeze (D-598)
845 → 231-line screen + 5 widget files (all <400, stateless, data+callbacks).
Figma pixel pass vs 883:2308 fixed: day-strip direction (RTL→pinned LTR) and
the type-tab count (4→3, owner call). Adopted the shared `SimfPullableHost`.
Golden re-locked at the frame; module tests 9/9, full suite green.
