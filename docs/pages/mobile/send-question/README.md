# Send a question (معلومات عن الجلسة) — mobile `/live/question?sessionId=`

| Field | Value |
|---|---|
| Route | `/live/question?sessionId=` (`RouteNames.sendQuestion`, page #26) · auth-gated (approved) |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/questions/send_question_screen.dart` (`SendQuestionScreen`, 319 lines) |
| Widgets | `lib/features/questions/widgets/send_question_content.dart` (`ReviewNote`, `SessionDataBlock` + `_NumberedLine`) |
| Figma node | `934:3636` (composer 934:3668, data block 1049:12590, note 943:3750) |
| Shell | `SimfPageShell` (title معلومات عن الجلسة) |
| API | `POST /app/sessions/{id}/questions` (`RequireApprovedAccount`, D-169/D-174) + `GET /app/programme/sessions/{id}` (the optional non-blocking data block) |
| Providers | `questionsRepositoryProvider` · `sessionDetailRepositoryProvider` |
| Tests | `test/features/questions/send_question_screen_test.dart` (8); golden `test/golden/send_question_golden_test.dart` (`goldens/send_question_934-3636.png`); E2E [`mobile-send-question.md`](../../../tests/e2e/mobile-send-question.md) |
| Legacy detail | `docs/App/Page_026/` — retained as the historical spec |
| Status | ✅ Real — D-318 (built) → 934:3636 parity → **clean-code frozen (D-604)** |

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
- The frame has no recipient selector, so the question submits to the default
  recipient (Speaker=0); the wire `recipient` field is preserved (D-169/D-174).
- Empty text → inline error; 400 `SESSION_NOT_LIVE_FOR_QUESTIONS` / 404 →
  "questions only open around the session" toast; other failure → generic toast;
  success → clear + confirmation toast.

## 4. Button / action audit (Level F, 2026-07-03)
| Control | Handler | Backend |
|---|---|---|
| Back | `backOrHome` | — |
| Question field | text input (inline empty-check) | — |
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
