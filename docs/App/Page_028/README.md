# Page 028 — تعليقات الجمهور · Audience comments

Per-page documentation folder (App screen 28).

## Identity
| | |
|---|---|
| Mockup page | **28** (`Mockup.html`) |
| Route | `RouteNames.audienceComments` → `/live/comments?sessionId={id}` (**auth-gated**) |
| Titles | AR **تعليقات الجمهور** · EN **Audience comments** |
| Section | 4 — Live & Q&A |
| Nature | **Per-session audience comment feed** — author + body + a like toggle; a bottom box submits a new comment (held for moderation) |
| App privilege | **Approved account** (`RequireApprovedAccount`). Route 28 is in `_authenticatedRoutes`. |
| Status | API **BUILT** (reuse — `GET/POST /app/sessions/{id}/comments`, like via `POST`/`DELETE .../like`, D-223); **Flutter screen BUILT** |

## API (authoritative contract)
All `RequireApprovedAccount`. The screen takes an optional `sessionId` query
param; with none it shows an "open from a live session" empty state (L-2).
- `GET /api/v1/app/sessions/{id}/comments` → `ApiResult<List<SessionCommentFeedRow>>`
  — `id`, `sessionId`, `userId`, `authorDisplayName`, `body`, `createdAt`,
  `likeCount`, `likedByMe`.
- `POST /api/v1/app/sessions/{id}/comments` body `{ body: <1..1000> }` → the
  submitted comment with a moderation `status` (`Pending = 0` / `Approved = 1` /
  `Hidden = 2`). A fresh comment may be **Pending** and is held back from the
  public feed until a moderator approves it (L-3).
- `POST /api/v1/app/sessions/{id}/comments/{commentId}/like` and
  `DELETE .../like` → `SessionCommentLikeResult` (`commentId`, `likeCount`,
  `likedByMe`).

## Behaviour
A scrollable comment feed — each card shows the author display name, the body,
and a like button showing `likeCount` (filled when `likedByMe`); tapping toggles
like / unlike and updates that one row from the server result (L-4; a like
failure leaves the row untouched). A bottom submit box (multi-line `TextField`,
max 1000, + send) posts a new comment, then shows the "awaiting moderation"
toast and refreshes the feed (L-3). Loading / empty / read-error+retry states.
UI is interim (final visuals from SIMF-VID-001).

## Tests
- Models: `src/Mobile/simf_app/test/features/comments/comment_models_test.dart`
  (feed decode, UTC createdAt, like-result, submit-status, list envelope).
- Widget: `src/Mobile/simf_app/test/features/comments/audience_comments_screen_test.dart`
  (no-id empty, feed renders, empty, error→retry, submit calls repo + toast,
  like toggles like→unlike).
- E2E: [`docs/tests/e2e/mobile-audience-comments.md`](../../tests/e2e/mobile-audience-comments.md).
