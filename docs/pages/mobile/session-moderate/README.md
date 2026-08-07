# Session-moderate desk — أسئلة الجلسة (`sessionModerate`)

- **Route:** `/sessions/{id}/moderate` (`RouteNames.sessionModerate`). Access: per-session `SessionModerator` grant (or Administrator) — **not** the mobile `AppRole.moderator` (a moderator without the grant gets a 403).
- **Figma:** **1461:12227** (tablet; authoritative per the class doc-comment). _Note: the codebase also carries `758:5307` (test), `805:1876` (filter bar), `1461:12565` (header) for the same screen — flagged, D-622._
- **Clean-code freeze:** D-622 (2026-07-04). Built D-405/D-509.

## Purpose

The per-session moderator Q&A desk. Lists the question queue with five
count-badged filter chips (الكل/جديد/الأسئلة المقبولة/تمت الإجابة/مرفوض) and three
per-question actions, **all five backed by the persisted `QuestionStatus`**.

**DEF-MOD-001 / DEF-MOD-002 (2026-07-26).** Previously **answered** and the
**rejected list** were moderator-session-local Dart state: leaving the screen (or an
app restart, or a co-moderator on another device) lost the answered marks, and a
rejected question was unrecoverable from the app. Now:

- `QuestionStatus` carries an additive **`Answered = 3`** (int column, no check
  constraint → **no migration**) written by
  `PUT /app/sessions/{id}/questions/{qid}/answered {isAnswered}` — same per-session
  gate as `hide` / `push`, idempotent, and only reachable from `Approved`.
- The desk list takes an optional **`?status=`**: omitted returns the working desk
  (Approved + Answered); `?status=Hidden` returns the desk's own rejected rows, which
  it can then **restore**. The tab is an allow-list — `Pending` is a **400**, because
  those questions are still inside the Scientific Committee's stage-2 gate (D-212).
- **D-772** — `?status=Hidden` returns only rows hidden **from the desk**
  (`StatusBeforeHidden` = Approved or Answered). A question the Committee rejected
  while it was still Pending stays in the Committee queue and its `questionText` is
  never shipped to the desk. Rows hidden before `StatusBeforeHidden` existed (null)
  have unknown provenance and are treated as Committee rows — not exposed.
- Every action updates the row **optimistically** and rolls it back on failure.

## Finding the desk at all (FR-MOD-001, 2026-07-27)

The desk is authorised **per session**, but nothing told the app WHICH sessions
carry the grant: the forum action rendered on **every** session in the programme
for anyone with `AppRole.moderator`, and the missing grant was only discoverable
as a **403 after the tap**. An icon that 403s is worse than no icon.

- **`GET /app/sessions/moderated`** (`ListMyModeratedSessionsEndpoint`, beside
  `SessionQuestionEndpoints` and gated the same way the desk is) returns the
  caller's own grants on ACTIVE sessions, soonest first, projecting the bilingual
  title + hall + start/end so the app needs no second fetch per session. It is
  scoped to the caller — there is no "list another user's grants" shape here; the
  admin surface for that stays `IAdminSessionModeratorService`.
- The app caches it in `myModeratedSessionsProvider` (`autoDispose`, so a fresh
  grant is picked up on the next visit). **Session detail** now offers the desk
  action only for a session in that set; while the call is in flight, or if it
  failed, no action is offered.
- The **moderator's operational home** grew a **جلساتي / My sessions** section
  listing those sessions (shared `SimfListRow` via `ModeratedSessionTile`, hall +
  Saudi 12-hour start, never a UTC instant), each tapping straight through to its
  desk. Empty and error states are the shared `SimfPageNote` / `SimfErrorState`,
  and the list is pull-to-refresh like every other data page.
- An **Administrator** may moderate any session without a grant, so an empty list
  is not a statement that they cannot open a desk.

## Ordering the queue (FR-MOD-003, 2026-07-27)

`PUT …/questions/reorder` shipped **implemented and permission-gated but with no
interface**, so a moderator could not order the queue they were about to read on
stage. The desk list is now a `ReorderableListView.builder` with a per-card drag
handle (`buildDefaultDragHandles: false` — the handle is built per row, so a
**rejected** row simply has none: it is off the desk and has no place in the
running order).

- The handle carries its own accessible name
  (**إعادة ترتيب السؤال / Reorder question**) as a `Semantics` **container**, so a
  screen reader announces the control rather than merging its name into the
  question text.
- **RTL:** the handle sits at the row's leading edge and mirrors with the card. A
  vertical reorder list has no left/right semantics to invert — "up" is earlier in
  the running order in both languages — and a test pins that the same gesture
  produces the same order in `ar` and `en`.
- The endpoint replaces the whole order and requires **every** working-desk
  question exactly once, so the call always ships the full desk even when a chip
  is showing a subset; the rows the filter hides keep their slots. Optimistic like
  every other desk action, with rollback + toast on failure.

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `session_moderate_screen.dart` | State — queue load, action handlers, `_reorder`, filter counts, build + `_body` |
| `widgets/moderated_session_tile.dart` | `ModeratedSessionTile` — one جلساتي row on the moderator's home (FR-MOD-001) |
| `widgets/moderator_header.dart` | `ModeratorDeskHeader` (+ `_RolePill`) — custom navy header with the role pill (kept local; SimfPageShell has no trailing slot) |
| `widgets/moderator_filter_bar.dart` | `ModeratorFilterBar` (+ `_Chip`) — five equal-width count chips |
| `widgets/moderator_question_card.dart` | `ModeratorQuestionCard` (+ `_ActionButton`, `_hm`, `_initials`) |

The forbidden/empty/error states use `SimfEmptyState`/`SimfErrorState`; the list uses
the shared `SimfPullToRefresh` (D-622 replaced the raw `RefreshIndicator`).

## L4 Figma parity (frame 1461:12227)

Overlay-verified — header (back + أسئلة الجلسة + gold محاضر pill), five count chips
(RTL order), question cards (gold top border, time/name/avatar, gold-bordered question
box, 3 action buttons: reject red-outline / answered green-outline / on-stage gold-solid).
No golden (16 widget tests are the render baseline).

**Divergence flagged (pre-existing D-405, not changed):** the frame's card subtitle
shows the submitter's **country** (green) on every card; the app renders a gold
**"to-host"** label only for host-directed questions.

## Level-F

Wired: 5 chips filter; reject → `setHidden(true)`; restore → `setHidden(false)`;
on-stage → `push` (returns a rejected / answered row to Approved first, which is what
the server requires); answered → `setAnswered(true|false)`; **drag-to-reorder →
`reorder`** (FR-MOD-003); retry; pull-to-refresh. Every write is optimistic with
rollback. Reads `getQueue()` (working desk), `getQueue(status: hidden)` (rejected
tab) and `getMySessions()` (FR-MOD-001 discovery). No missing API — the last
implemented-but-unreachable endpoint (`reorder`) now has its affordance.

## Tests

`test/features/moderation/session_moderate_screen_test.dart` (14 cases, incl. the
four FR-MOD-003 reorder cases) +
`test/features/moderation/moderation_models_test.dart` +
`test/features/home/moderator_home_test.dart` (5 cases — the FR-MOD-001 جلساتي
list, tap-through, empty, error, RTL) + the two FR-MOD-001 gate cases in
`test/features/sessions/session_detail_screen_test.dart`. API twins:
`tests/SIMF.Api.Tests/ModeratorDeskStateTests.cs` (DEF-MOD-001/002) and
`tests/SIMF.Api.Tests/ModeratedSessionsTests.cs` (FR-MOD-001 — own grants only,
soonest first, soft-deleted session dropped, empty list, 401). E2E:
`docs/tests/e2e/mobile-session-moderate.md` (MOBMOD-006/007, MOBMOD-009/010).
