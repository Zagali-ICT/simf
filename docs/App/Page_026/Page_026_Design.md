# Page 026 — Design (إرسال سؤال · Send a question)

Flutter screen design. Grounded in `Mockup.html` screen 26 and the Screen Guide
SCREEN26. RTL, Arabic-primary.

## Layout (top → bottom, from the mockup)
1. **Live video frame** — the live broadcast pinned at the top (same look as
   screen 25). Driven by the session detail's `LiveStreamUrl`; the optional
   `لغة الإشارة` (sign-language) toggle is driven by `LiveSignLanguageUrl` (D-271
   live stub).
2. **Live-mode tab bar** — three tabs: `طلب مقابلة` (→ 27) · `تعليقات الجمهور`
   (live comments) · **`إرسال سؤال`** (this screen, active). The tabs switch
   between the live-mode views.
3. **Recipient picker** — two pills: **`المتحدث`** (Speaker) / **`المضيف`** (Host).
   One is selected; the selection is the `Recipient` field (default Speaker).
4. **Question text area** — a multi-line input for `QuestionText` (1–1000 chars).
5. **Submit button** — a filled **brass** primary `إرسال السؤال`.
6. **Moderation note** — a quiet caption "questions are reviewed before going on
   air".

## Data binding
- **Recipient pills** bind to `SessionQuestionRecipient`
  (`المتحدث` = `Speaker`/0, `المضيف` = `Host`/1); the default selection is
  **Speaker** (Page_026_API E1).
- **Submit** sends `POST …/sessions/{id}/questions` with
  `{ questionText, recipient, isAtVenue }` (Page_026_API E1). On a `200` it shows a
  **confirmation** built from `SessionQuestionSubmitted.order` (the submitter's own
  queue position) and clears the text area.
- **`isAtVenue`** is supplied from the arrival state — set true when the user holds
  a `HallAttendance` arrival record or has self-asserted "I am at the venue"
  (Page_026_Logic L-2 rule 1).
- The **live frame** + sign-language toggle bind to the session detail's live
  fields (D-271); this composer does not own them.

## States
- **Composing** — recipient pill selected, text entered, submit enabled.
- **Submitting** — submit button shows a spinner while `POST …/questions` runs.
- **Confirmed** — a success toast / banner ("question received, reviewed before
  going on air"); the text area clears.
- **Window closed** — `400 SESSION_NOT_LIVE_FOR_QUESTIONS` → an inline "questions
  open 5 minutes before the session and close when it ends" message (rules 2 / 3).
- **Not at venue** — `403 NOT_AT_VENUE` → a "you must be at the venue to ask a
  question" message (rule 1).
- **Invalid text** — `400 SESSION_QUESTION_INVALID` → an inline "please enter a
  question" error on the empty text area.
- **Gated / error** — `401` / `403` (not approved) → the composer is not reachable
  (login-only); `404` → "session removed".

## RTL / localization
- Whole screen mirrored RTL; the live-mode tab bar + recipient pills mirror RTL.
- The recipient pills (`المتحدث` / `المضيف`), the text-area placeholder, the brass
  `إرسال السؤال` button and the moderation note are bilingual per the active
  locale.
- The brass accent on the primary submit button + the selected recipient pill use
  theme tokens (no raw colours).

## Note — comments tab (28) folded into live
The third live-mode tab is **audience comments**, which per D-271 no longer has a
**standalone screen (28)** — the comments feed surfaces **inside** the session /
live screen. The comments pipeline (2-stage AI-filter on submit → admin
approve/hide via `/admin/comments-moderation`) is **already built**; this composer
does not render or own it (Page_026_Logic L-6).
