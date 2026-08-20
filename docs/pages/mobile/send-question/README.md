# Send a question (معلومات عن الجلسة) — mobile `/live/question?sessionId=`

| Field | Value |
|---|---|
| Route | `/live/question?sessionId=` (`RouteNames.sendQuestion`, page #26) · auth-gated (approved) |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/questions/send_question_screen.dart` (`SendQuestionScreen`, 256 lines) |
| Widgets | `widgets/send_question_composer.dart`, `send_question_submit_button.dart`, `send_question_recipient_picker.dart`, `review_note.dart`, `session_data_block.dart`, `numbered_line.dart` — one public widget per file. They were a single `send_question_content.dart` until the 2026-08 clean-code round; that file held six unrelated widgets and its name described none of them. |
| Figma node | `934:3636` (composer 934:3668, data block 1049:12590, note 943:3750) |
| Shell | `SimfPageShell` (title معلومات عن الجلسة) |
| API | `POST /app/sessions/{id}/questions` (`RequireApprovedAccount`, D-169/D-174) + `GET /app/programme/sessions/{id}` (the optional non-blocking data block) |
| Providers | `questionsRepositoryProvider` · `sessionDetailRepositoryProvider` |
| Tests | `test/features/questions/send_question_screen_test.dart` (10); golden `test/golden/send_question_golden_test.dart` (`goldens/send_question_934-3636.png`); E2E [`mobile-send-question.md`](../../../tests/e2e/mobile-send-question.md) |
| Legacy detail | `docs/App/Page_026/` — retained as the historical spec |
| Status | ✅ Real — D-318 (built) → 934:3636 parity → **clean-code frozen (D-604)**; `_form` composer/submit further extracted (D-637) |

## 1. Purpose
Ask a question during a live session: the بيانات الجلسة session-data block (the
session description as a numbered list, non-blocking context) over the الاسئلة
composer (max-500 tinted box), a bottom-pinned gold submit, and the
reviewed-before-air note.

## 2. Audience & access
Auth-gated (route 26 is authenticated); the submit endpoint is
`RequireApprovedAccount`. Reached from a live session with its id in the query.

## 3. UI & behaviour
- No session id → "open from a live session" empty state.
- The data block reads the anonymous session detail; a fetch failure just hides
  the block (the composer still works — non-blocking context).
- The recipient picker sits above the composer (**B7**, the D-174 choice) under
  the إلى من؟ / "Send to" label: المتحدث / Speaker is the default, المضيف / Host
  the alternative, submitted as the wire `recipient` int (Speaker=0, Host=1,
  D-169/D-174). The Figma frame has no such selector — the picker is a
  deliberate addition to it, from the D-174 mockup's two pills.
- Empty text → inline error; 400 `SESSION_NOT_LIVE_FOR_QUESTIONS` / 404 →
  "questions only open around the session" toast; other failure → generic toast;
  success → clear + confirmation toast.
- **Two-path routing (server-side, owner 2026-07-19).** The screen is identical for
  both paths — the server decides by phase in `SessionQuestionService.SubmitAsync`.
  A **LIVE** question (asked once the session has started) skips the AI filter + the
  Scientific Committee and lands Approved straight on the per-session moderator desk.
  A **PRE** question (asked before start) runs the advisory AI filter and lands
  Pending for the committee → then the desk. The "reviewed before air" note holds
  for both (a human moderator still reviews before a question reaches the stage). See
  E2E `mobile-send-question.md` MOB026-014.

## 4. Button / action audit (Level F, 2026-07-03)
| Control | Handler | Backend |
|---|---|---|
| Back | `backOrHome` | — |
| Question field | text input (inline empty-check) | — |
| المتحدث / المضيف (recipient pills) | `SendQuestionRecipientPicker` → `_recipient` | — (sent as `recipient` on submit) |
| إرسال السؤال (submit) | `_submit` → validate → POST | `POST …/questions` |

All content repo-backed; no missing API.

## 5. Clean-code freeze (D-604)
**420 → 319-line screen** + one widget file (`ReviewNote` / `SessionDataBlock`).
**Bug the regenerated golden exposed + fixed:** the submit button set its label
size/weight via `FilledButton.styleFrom(textStyle:)`, dropping the brand font
so the Arabic "إرسال السؤال" rendered as **tofu** — and the *existing frozen
golden had locked that tofu in* (generated with `--update` at build time).
Moved the style onto the label `Text`; regenerated the golden (now correct
Arabic, crop-verified) and overlay-checked against 934:3636. Behaviour
byte-identical (8 tests green).

## 6. Further decomposition (D-637, 2026-07-04)
D-604 froze the screen but left a ~140-line `_form` method with the composer box +
submit inline. Extracted them to the widget file as **`SendQuestionComposer`** (the
الاسئلة label + the tinted max-500 question box) and **`SendQuestionSubmitButton`**
(the gold full-width submit, keeping the label-`Text` font fix). Screen **319 →
248**; the `send_question_934-3636` golden **held WITHOUT `--update`** (render
byte-identical) and the 8 tests pass. `QuestionRecipient` stays in the screen — it
is imported cross-feature by moderation.

## 7. The submit button can no longer strand (fixed 2026-08-20)

`_submitting` was cleared on the success path and again inside `on ApiFailure`,
with no `finally` — so anything thrown that is **not** an `ApiFailure` left
إرسال السؤال disabled for good, with no toast and no way out but leaving the
screen. The escape is real: `SimfApiClient` converts only the **first** call's
errors to `ApiFailure`, and the 401 token-refresh branch sits outside that
guard, so a keystore/keychain `PlatformException` (an OS keystore reset, a
restored backup) surfaces raw mid-submit. The flag now clears in a `finally`;
clearing the composer still happens only on success.
