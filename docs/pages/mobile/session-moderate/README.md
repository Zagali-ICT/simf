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
  it can then **restore**.
- Every action updates the row **optimistically** and rolls it back on failure.

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `session_moderate_screen.dart` (264) | State — queue load, action handlers, filter counts, build + `_body` |
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
the server requires); answered → `setAnswered(true|false)`; retry; pull-to-refresh.
Every write is optimistic with rollback. Reads `getQueue()` (working desk) plus
`getQueue(status: hidden)` (rejected tab). No missing API.

## Tests

`test/features/moderation/session_moderate_screen_test.dart` +
`test/features/moderation/moderation_models_test.dart`. API twin:
`tests/SIMF.Api.Tests/ModeratorDeskStateTests.cs` (DEF-MOD-001/002). E2E:
`docs/tests/e2e/mobile-session-moderate.md` (MOBMOD-006/007).
