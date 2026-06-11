# Page 036 — المساعد الذكي · AI assistant

Per-page documentation folder (App screen 36).

## Identity
| | |
|---|---|
| Mockup page | **36** (`Mockup.html`) |
| Route | `RouteNames.chatbot` → `/chatbot` (**guest+, anonymous**) |
| Titles | AR **المساعد الذكي** · EN **AI assistant** |
| Section | 4 — Engagement |
| Nature | **Interim chat shell** — scrolling transcript (user bubbles right, assistant bubbles left) + a bottom input row |
| App privilege | **Guest+ (anonymous).** No auth, no API call. |
| Status | **No backend chatbot endpoint (verified).** Flutter screen **BUILT** as an honest interim shell |

## API (authoritative contract)
**None.** There is **no server-side chatbot / AI-assistant endpoint** in the App
API — this was verified against the shipped surface. The screen makes **no
network call**: it appends the typed message, then appends an assistant reply
produced locally by the `ChatbotResponder` seam. No new package was added.

## Behaviour
- A one-time, dismissible **preview banner** at the top notes the assistant is in
  preview / interim.
- The transcript scrolls; **user** lines align to the trailing edge (accent
  bubble), **assistant** lines to the leading edge (field bubble). RTL-aware
  via `AlignmentDirectional`.
- On **send** the trimmed prompt is appended as a user bubble, then the
  `ChatbotResponder.reply(prompt, isArabic:)` result is appended as an assistant
  bubble. An empty/whitespace prompt is ignored; a send is locked (spinner)
  while a reply is in flight.
- The **default** responder (`CannedChatbotResponder`) returns a fixed bilingual
  notice — AR `المساعد الذكي قيد التفعيل — سيتوفر الرد التلقائي قريباً.` /
  EN `The AI assistant is being connected — automatic replies are coming soon.`
- The responder is exposed via the overridable `chatbotResponderProvider`, so
  tests inject a fake and a future real provider swaps in **without touching the
  screen**.

> **Interim, by design.** The chatbot backend / AI provider is **not implemented
> server-side**. This page is a UI shell that is honest about that until a real
> provider is procured (no scope creep, no fabricated endpoint). Final visuals
> come from SIMF-VID-001.

## Tests
- Widget: `src/Mobile/simf_app/test/features/chatbot/chatbot_screen_test.dart`
  (empty + banner, type→send appends user + reply, default canned notice EN/AR,
  empty prompt ignored, banner dismiss).
- API: **none** (no endpoint exists).
- E2E: [`docs/tests/e2e/mobile-chatbot.md`](../../tests/e2e/mobile-chatbot.md).
