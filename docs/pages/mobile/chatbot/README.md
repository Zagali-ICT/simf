# AI assistant — المساعد الذكي (Page 036, `#36`)

- **Route:** `/chatbot` (`RouteNames.chatbot`). Access: **signed-in** (reached from the visitor home).
- **API:** `POST /app/ai/assistance` — the centralised AI (`assistance` prompt), **grounded server-side on the live event context**, with **persisted conversation + short-term memory**; `GET /app/ai/assistance/history` loads the saved transcript on open (D-756).
- **Figma:** **1064:13066** (D-448 parity).

## Purpose

The visitor AI assistant. It opens with the assistant greeting, then each prompt
(typed or a quick-reply chip) is answered by the **centralised AI** through the
overridable `chatbotResponderProvider` seam — whose default `ApiChatbotResponder`
calls `POST /app/ai/assistance`. The backend grounds the `assistance` prompt on the
live programme sessions, FAQ, and exhibition booths (built server-side by
`AssistanceContextBuilder`), so the assistant answers from the **real event data**,
not the model's general knowledge. A wire error surfaces as a localized error bubble.

> Superseded: the earlier interim shell had **no backend endpoint** — it opened on a
> scripted demo transcript and every prompt got a canned "coming soon" reply. Both the
> scripted "history" and the `CannedChatbotResponder` were removed (owner directive:
> no fake data) when the screen was wired to the real assistant.

## Structure

| File | Holds |
|------|-------|
| `chatbot_screen.dart` | `ChatbotScreen` + State — input / scroll controllers, the added-messages list, `_send` (append user bubble → responder reply / error bubble → scroll), the l10n greeting `_seed`, and the transcript + chips + composer layout. Re-exports the responder seam. |
| `data/chatbot_responder.dart` | `ChatbotResponder` (abstract) + **`ApiChatbotResponder`** (`POST /app/ai/assistance` via `SimfApiClient`, decodes `outputText`) + `chatbotResponderProvider` (the overridable seam, default = the API responder). |
| `data/ai_chat_history_repository.dart` | **`AiChatHistoryRepository`** (`GET /app/ai/assistance/history` → `List<ChatMessage>`) + `aiChatHistoryProvider` (auto-dispose future, loaded at open). The transcript = greeting + loaded history + this-session's added turns (D-756). |
| `data/chat_message.dart` | `ChatAuthor` enum + `ChatMessage` (one transcript line). |
| `widgets/chat_bubble.dart` (`ChatBubble` + `_AiBadge`) | One bubble — assistant left (navy + gold "AI" badge) / user right (gold). |
| `widgets/quick_replies.dart` (`QuickReplies` + `_QuickReplyChip`) | The horizontal chip strip; a tap sends the chip as the next prompt. |
| `widgets/chat_composer.dart` (`ChatComposer`) | The bottom input bar — text field + the gold send square (spinner while sending). |

## Data flow

`ChatbotScreen._send` → `chatbotResponderProvider` (`ApiChatbotResponder`) →
`SimfApiClient.post('/app/ai/assistance', { message, locale })` →
`AssistanceEndpoint` → `AssistanceContextBuilder.BuildAsync()` (reuses
`IProgrammeSessionService` / `IPublicFaqService` / `IPublicBoothService`) →
`IAiService.InvokeAsync("assistance", { message, context, locale })` → provider →
`AiCallResult.outputText`.

The endpoint requires an approved account and is rate-limited (`auth`). Backed by the
offline `echo` provider until an operator configures a real provider + key (see the AI
go-live runbook in `docs/SIMF-OPS-001-Deployment-and-Operations.md`).

**Persistence & memory (D-756).** On open, the screen loads the saved transcript via
`aiChatHistoryProvider` (`GET /app/ai/assistance/history`) — best-effort, so a load
failure never blanks the chat. Server-side, `AssistanceEndpoint` reads the caller's
recent turns (`IAiChatHistoryService.GetRecentContextAsync`, last 12, capped) into a
new `{history}` prompt slot so the assistant remembers earlier turns, then persists the
exchange (`AppendTurnAsync`) to the additive `AiChatMessages` table (`SIMF_App`, keyed
by a bare Guid user id per D-157). Existing DBs need
`docs/migrations/2026/SIMF_App_AssistancePromptHistory.sql` (the seeder is INSERT-only).

## Figma parity (frame 1064:13066)

Golden `goldens/chatbot_1064-13066.png` (@375×812, ar) — the greeting bubble +
gold "AI" badge, the quick-reply chip strip, the composer with the gold send square,
RTL, no tofu. The sample Q&A shown in the Figma frame was illustrative demo content
and is **not** seeded (removed with the fake data); the golden was re-locked to the
greeting-only open state in the same changeset.

## Tests

`test/golden/chatbot_golden_test.dart` (frame 1064:13066, @375×812, ar) +
`test/features/chatbot/chatbot_screen_test.dart` (greeting-only open — **no scripted
transcript**, **saved-history renders under the greeting**, typed send, chip send,
empty no-op, **wire-error → error bubble**, Arabic greeting + RTL bubble order).
Backend: `tests/SIMF.Api.Tests/AiModuleTests.cs` — the turn persists + the history
endpoint returns it, and a second call carries the prior turn as memory. E2E:
`docs/tests/e2e/mobile-chatbot.md`.

## Related decisions

- **D-322** (screen built), **D-448** (1064:13066 parity), **D-176** (centralised AI module).
- Wired to the real `/app/ai/assistance` + grounded on the live event context; scripted
  transcript + `CannedChatbotResponder` removed (owner directive: no fake data, 2026-07-22).
- **D-756** — persisted conversation + short-term memory (`AiChatMessages` table,
  `GET /app/ai/assistance/history`, `{history}` prompt slot); owner-approved D-110
  freeze-lift for the additive table; stores visitor message text (PII) in `SIMF_App`.
