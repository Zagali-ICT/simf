# AI assistant — المساعد الذكي (Page 036, `#36`)

- **Route:** `/chatbot` (`RouteNames.chatbot`). Access: **Guest+ (public)**. Makes **no API call**.
- **Figma:** **1064:13066** (D-448 parity). **Clean-code freeze:** D-631 (2026-07-04).

## Purpose

An interim AI-assistant shell. There is **no backend chatbot endpoint** (verified,
Page_036 / DECISIONS_LOG). It opens on the scripted Figma transcript, then a new
prompt (typed or a quick-reply chip) is echoed as a user bubble and answered by
the overridable `chatbotResponderProvider` seam — whose default
`CannedChatbotResponder` returns a fixed bilingual "coming soon" notice. When a
real provider is procured server-side, only the seam swaps; the screen stays.

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `chatbot_screen.dart` (142) | `ChatbotScreen` + State — the input / scroll controllers, the added-messages list, `_send` (echo + responder reply + scroll), the l10n-built `_seed` transcript, and the transcript + chips + composer layout. Re-exports the responder seam. |
| `data/chatbot_responder.dart` | `ChatbotResponder` (abstract) + `CannedChatbotResponder` (the interim canned reply) + `chatbotResponderProvider` (the overridable seam). |
| `data/chat_message.dart` | `ChatAuthor` enum + `ChatMessage` (one transcript line). |
| `widgets/chat_bubble.dart` (`ChatBubble` + `_AiBadge`) | One bubble — assistant left (navy + gold "AI" badge) / user right (gold), the 2px inner tail. |
| `widgets/quick_replies.dart` (`QuickReplies` + `_QuickReplyChip`) | The horizontal beige-hairline chip strip; a tap sends the chip as the next prompt. |
| `widgets/chat_composer.dart` (`ChatComposer`) | The bottom input bar — text field + the gold send square (spinner while sending). |

The responder seam + the message model moved to `data/` (D-545), the responder
re-exported (the test injects a fake `ChatbotResponder`). Screen was already fully
tokenised (no raw `Color(0x..)`). Every file ≤400 lines.

## L4 Figma parity (frame 1064:13066)

Captured `chatbot_1064-13066.png` (@375×812, ar, seed transcript) as the
**baseline before** the refactor, then **held it WITHOUT `--update`** after —
proving the data/model move + the 3-widget extraction byte-identical. Golden read:
المساعد الذكي header, the scripted transcript (assistant bubbles + gold "AI" badge
left / user bubbles gold right), the quick-reply chip strip, the composer with the
gold send square, RTL, no tofu.

## Level-F

Wired: typed prompt + chip → echo user bubble + `chatbotResponderProvider` reply
(default = canned notice); empty prompt is a no-op; auto-scroll to the latest;
AR↔EN re-seeds the transcript. **No backend endpoint** (interim by design — the
seam is ready for a real provider).

## Tests

`test/golden/chatbot_golden_test.dart` (frame 1064:13066, @375×812, ar) +
`test/features/chatbot/chatbot_screen_test.dart` (seed transcript, typed send,
chip send, empty no-op, the default canned responder, Arabic RTL bubble order).
E2E: `docs/tests/e2e/mobile-chatbot.md`.

## Related decisions

- **D-631** (this clean-code freeze — responder/model → `data/` + 3 widgets + first golden).
- **D-322** (screen built), **D-448** (1064:13066 parity). No backend chatbot endpoint (verified).
