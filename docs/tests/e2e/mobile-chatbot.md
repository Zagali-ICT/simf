# E2E test catalogue — `AI assistant` (`chatbot`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue —
> there is **no backend chatbot endpoint** (verified). The Flutter screen is an
> honest **interim chat shell** (no API call): a transcript + a bottom input row
> backed by an overridable `ChatbotResponder` seam whose default returns a fixed
> bilingual canned notice. Widget-tested in
> `src/Mobile/simf_app/test/features/chatbot/chatbot_screen_test.dart` (empty +
> banner, type→send, default canned notice EN/AR, empty prompt ignored, banner
> dismiss).

| | |
|--|--|
| **Page** | [`Page_036`](../../App/Page_036/README.md) |
| **Route** | app screen #36 `/chatbot` — **no API** (no server-side chatbot) |
| **Surface** | Mobile (Flutter) only |
| **Auth setup** | **None** — public/anonymous; the screen makes no network call. |
| **Last reviewed** | 2026-06-06 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB036-001 | Open → empty transcript + dismissible preview banner | happy | P0 | authored ✓ (screen `starts empty with the preview banner`) |
| E2E-MOB036-002 | Type a prompt + send → user bubble then assistant reply | happy | P0 | authored ✓ (screen `typing + send appends the user message and the reply`) |
| E2E-MOB036-003 | Default responder returns the canned interim notice (EN) | edge | P1 | authored ✓ (screen `the default responder returns the canned interim notice`) |
| E2E-MOB036-004 | Empty / whitespace prompt is ignored (no bubble) | edge | P1 | authored ✓ (screen `an empty prompt does not append a bubble`) |
| E2E-MOB036-005 | Arabic canned notice renders in Arabic | rtl | P2 | authored ✓ (screen `renders the Arabic canned notice in Arabic`) |
| E2E-MOB036-006 | Dismissing the preview banner hides it | edge | P2 | authored ✓ (screen `dismissing the preview banner hides it`) |

## Scenarios

### E2E-MOB036-001 — Open the assistant

```gherkin
Feature: AI assistant (interim shell)
  As a guest
  I want a chat surface
  So that I can ask the assistant questions once it is connected

Scenario: The screen opens empty with a preview notice
  When the user opens /chatbot
  Then the transcript is empty with an "Ask the assistant to get started." prompt
  And a dismissible preview banner notes the assistant is interim
  And no network request is made
```

**Evidence:** screen test `starts empty with the preview banner`.

### E2E-MOB036-002 — Send a prompt

```gherkin
Scenario: Sending appends the user message then the assistant reply
  Given the user typed "When does it start?"
  When they press Send
  Then a right-aligned user bubble shows "When does it start?"
  And a left-aligned assistant bubble shows the responder's reply
```

**Evidence:** screen test `typing + send appends the user message and the reply`.

### E2E-MOB036-003 / 004 / 005 / 006 — Canned notice, empty prompt, RTL, banner dismiss

```gherkin
Scenario: The default responder gives the interim notice (EN)
  Given no responder override (the real CannedChatbotResponder)
  When the user sends any prompt in English
  Then the assistant replies "The AI assistant is being connected — automatic replies are coming soon."

Scenario: A whitespace-only prompt is ignored
  When the user sends "   "
  Then no bubble is appended and the empty prompt remains

Scenario: The Arabic canned notice renders in Arabic
  Given the app locale is Arabic
  When the user sends a prompt
  Then the assistant replies "المساعد الذكي قيد التفعيل — سيتوفر الرد التلقائي قريباً."

Scenario: The preview banner can be dismissed
  When the user taps the banner close icon
  Then the preview banner is hidden
```

**Evidence:** screen tests `the default responder returns the canned interim notice`,
`an empty prompt does not append a bubble`, `renders the Arabic canned notice in Arabic`,
`dismissing the preview banner hides it`.

---

_Last reviewed:_ `2026-06-06` by `SIMF Team`.
