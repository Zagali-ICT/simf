# Page 026 — Function (إرسال سؤال · Send a question)

What the user does on this screen. Grounded in `Mockup.html` screen 26 and the
Screen Guide SCREEN26 ("Composer for sending a question to the speaker or host
during the live broadcast … Moderation note: questions are reviewed before going
on air").

## Privilege / auth gate
**Visitor (approved) — login-only.** The submit endpoint
`POST /api/v1/app/sessions/{id}/questions` requires an approved account
(`RequireApprovedAccount`). A guest / pending account cannot submit — it is
prompted to sign in.

## Elements (top → bottom, from the mockup)
1. **Live video frame** — the live broadcast at the top (same look as screen 25).
2. **Live-mode area** — in the mockup the live frame carries a live-mode tab
   strip. In the app the live-mode functions are **separate auth-gated routes**:
   **`إرسال سؤال`** (26, this screen) and **audience comments** (28,
   [Page_028](../Page_028/README.md), built D-319). The `طلب مقابلة`
   request-interview tab (27) was **removed** (D-278).
3. **Recipient picker** — two pills: **المتحدث (Speaker)** / **المضيف (Host)** —
   chooses the question's addressee (`Recipient`).
4. **Question text area** — the question body (`QuestionText`, 1–1000 chars).
5. **Brass primary button** — `إرسال السؤال` (submit).
6. **Moderation note** — "questions are reviewed before going on air".

## What the user does
1. **Pick a recipient** — tap `المتحدث` (Speaker) or `المضيف` (Host). The default
   is **Speaker** (the wire default for clients that omit the field, D-174).
2. **Write the question** — type into the text area (trimmed; empty/whitespace is
   rejected as `SESSION_QUESTION_INVALID`).
3. **Submit** → `إرسال السؤال` → `POST …/sessions/{id}/questions` with
   `{ QuestionText, Recipient, IsAtVenue }`. On success the app shows a
   **confirmation** (the response carries the submitter's own queue position
   `Order`, not the whole queue).
4. **Understand the review** — the question does **not** go on air immediately. It
   lands **Pending**, is screened by an **advisory AI filter** (which does not
   block), then is **approved by the Scientific Committee**; only then can a
   session moderator **push** it live (Page_026_Logic L-3).

## The three submit rules (must all pass)
A submission is only accepted when **all three** hold (Page_026_Logic L-2):
1. **Arrival at the hall** — for a hall with a geofence the user must have a
   `HallAttendance` arrival record; otherwise the `IsAtVenue` self-assert toggle
   must be **true** (D-242 / D-171). A remote user (`IsAtVenue = false`, no
   arrival) is rejected `403 NOT_AT_VENUE`.
2. **Opens 5 minutes before start** — questions open only within `StartUtc − 5min`
   (`PreStartWindow = 5min`). Earlier than that → `400 SESSION_NOT_LIVE_FOR_QUESTIONS`.
3. **Closes at the end** — questions close at `EndUtc` (`PostEndWindow = 0`). After
   the session ends → `400 SESSION_NOT_LIVE_FOR_QUESTIONS`.

## Acceptance criteria
- Only an **approved, signed-in** account can submit; a guest / pending account is
  gated out.
- The **recipient** defaults to **Speaker** and round-trips as **Host** when the
  user picks `المضيف`.
- An **empty / whitespace** question is rejected (`SESSION_QUESTION_INVALID`).
- Submitting **more than 5 minutes before** the start, or **after** the end, is
  rejected (`SESSION_NOT_LIVE_FOR_QUESTIONS`); submitting **within 5 minutes
  before** start, or during the session, is accepted.
- A submission **without** an arrival record **and** with `IsAtVenue = false` is
  rejected (`NOT_AT_VENUE`).
- A successful submission lands **Pending** (not yet on air) and returns the
  submitter's own queue position; it reaches the moderator desk only **after**
  Committee approval.

## Where it fits in the journey
**Journey D — conference-day live mode.** The live-mode **question composer**,
reached over the live session screen (25). The other live-mode functions are
separate screens: **audience comments** (28, [Page_028](../Page_028/README.md),
built D-319). The `طلب مقابلة` request-interview screen (27) was **removed**
(D-278).
