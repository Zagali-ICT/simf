# Page 026 — Logic (إرسال سؤال · Send a question)

Business rules behind the live Q&A composer. Verified against the public
submission + moderator surface (D-169), the recipient pills (D-174), the
Scientific-Committee queue (D-212), the advisory AI filter (D-236) and the
arrival gate (D-242). **No new backend behaviour in this wave** — D-271 documents
the existing pipeline + the open/close window + the screen-28 comments removal.

## L-1 One call submits a question
The screen writes from a **single** call:
`POST /app/sessions/{sessionId}/questions` (body `SubmitSessionQuestionRequest`):

| Field | Drives |
|-------|--------|
| `QuestionText` (1–1000 chars, trimmed) | the question body |
| `Recipient` (`Speaker` = 0 / `Host` = 1) | the addressee pill (`المتحدث` / `المضيف`); defaults to **Speaker** |
| `IsAtVenue` (bool) | the "I am at the venue" self-assert (used when the hall has no geofence arrival record) |

On success it returns `SessionQuestionSubmitted { Id, SessionId, Order, CreatedAt }`
— `Order` is the **submitter's own queue position** only (moderators see the full
queue via the moderator surface).

## L-2 The THREE open/close rules (all must pass)
The submit is gated by **three** server-side rules. Failing any one rejects the
submission:

1. **Arrival at the hall (D-242).** When the session's hall has a **geofence**, the
   attendee must already hold a **`HallAttendance` arrival record** for that hall.
   When the hall has **no geofence**, the gate falls back to the **`IsAtVenue`
   self-assert** — the body flag must be `true`. A caller with **neither** (no
   arrival, `IsAtVenue = false`) is rejected **`403 NOT_AT_VENUE`**.
2. **Opens 5 minutes before start.** Questions open only inside the window
   `StartUtc − PreStartWindow`, where **`PreStartWindow = 5min`**. A session that
   starts in 10 minutes is still **closed**; one starting in 3 minutes is **open**.
3. **Closes at the end.** Questions close at **`EndUtc`** (**`PostEndWindow = 0`**).
   After the session ends the window is **closed**.

Outside the time window (rule 2 or 3) the submit returns
**`400 SESSION_NOT_LIVE_FOR_QUESTIONS`**. This is exactly the live window the
mockup wording captures — `قبل الجلسة بخمس دقائق` (open 5 min before) and
`تقفل بنهاية الجلسة` (close at the end).

## L-3 "Reviewed before going on air" = the three-stage pipeline
A submitted question does **not** appear live immediately. It moves through three
stages before a moderator can push it:

1. **Stage 1 — advisory AI filter (D-236).** On submit, an `IQuestionAiFilter`
   (stub) tags an **advisory** verdict (`AiFilterVerdict`, e.g. `stub-clean`). It
   is **advisory only** — it does **not** change the status; the question still
   lands `Pending`.
2. **Stage 2 — Scientific-Committee approval (D-212).** Every new question lands
   **`Status = Pending`** on the Committee's central queue. A live session
   (`StartUtc` already past) computes **`Phase = Live`**. The Committee approves
   (`PUT /api/v1/admin/questions/{id}/approve`) — only then does the question
   reach the per-session moderator desk.
3. **Stage 3 — session moderator (D-169).** On
   `…/sessions/{id}/questions/moderate` a moderator (an admin, or a granted
   per-session `SessionModerator`) can **hide / unhide**, **reorder** and **push**
   the approved question on air (`…/{qid}/push` stamps `PushedAt`, idempotent).

So the moderation note "questions are reviewed before going on air" maps to:
advisory AI screen → Committee approve → moderator push.

## L-4 Recipient = the المتحدث / المضيف pills (D-174)
The recipient picker maps 1:1 to `SessionQuestionRecipient`:
`Speaker = 0` (`المتحدث`) · `Host = 1` (`المضيف`). The default for clients on the
pre-D-174 wire shape is **Speaker**. The chosen recipient round-trips into the
moderator queue row (`SessionQuestionModeratorRow.Recipient`) so the moderator
knows whom the question is for.

## L-5 Validation + edge cases
- **Empty / whitespace text** → `400 SESSION_QUESTION_INVALID`.
- **More than 5 minutes before start** / **after the end** → `400
  SESSION_NOT_LIVE_FOR_QUESTIONS`.
- **No arrival + `IsAtVenue = false`** → `403 NOT_AT_VENUE`.
- **Unauthenticated / pending account** → `401` / `403` — the submit is
  login-only (`RequireApprovedAccount`).
- **Session missing / soft-deleted** → `404`.

## L-6 The standalone comments screen (28) is REMOVED — comments fold into live
Per the updated mockup (D-271) the **standalone audience-comments screen (28) is
removed**. Audience comments still exist and are **already built** — they surface
**inside** the session / live screen, not as a separate screen. The comments
pipeline is two stages:
1. **AI filter on submit** — an `ICommentAiFilter` (stub) screens each comment, so
   it lands either **Approved** or **Pending**.
2. **Admin approve / hide** — an admin moderates via the CP
   **`/admin/comments-moderation`** desk (`CommentsModerationList`).

No new build for comments in this wave — this section records the **pipeline** and
the **screen removal** only. The session-question pipeline (this page) and the
comments pipeline are **separate** surfaces.

## L-7 Localization
Arabic primary (RTL), English secondary. The recipient pills
(`المتحدث` / `المضيف`), the text-area placeholder, the brass `إرسال السؤال`
button and the moderation note are bilingual per the active locale; the live frame
+ the three live-mode tabs mirror RTL.
