# E2E test catalogue — `AI assistant` (`chatbot`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue —
> there is **no backend chatbot endpoint** (verified). Pixel-parity to KSA Figma
> frame `1064:13066` (D-448): the navy `KsaPage` shell, a transcript **seeded
> with the scripted Figma conversation** (assistant bubbles left + gold "AI"
> badge, user bubbles right + gold fill), the horizontal **quick-reply chips**
> (frame `1070:13389`) and the bottom **input bar** (frame `1070:13398`). A new
> prompt (typed or a chip) is echoed as a user bubble and answered by the
> overridable `ChatbotResponder` seam (default = canned bilingual notice); the
> screen makes **no network call**. Widget-tested in
> `src/Mobile/simf_app/test/features/chatbot/chatbot_screen_test.dart`.

| | |
|--|--|
| **Page** | [`Page_036`](../../App/Page_036/README.md) |
| **Route** | app screen #36 `/chatbot` — **no API** (no server-side chatbot) |
| **Surface** | Mobile (Flutter) only |
| **Auth setup** | **None** — public/anonymous; the screen makes no network call. |
| **Last reviewed** | 2026-06-19 (D-448 — Figma `1064:13066` parity) |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB036-001 | Open → the scripted Figma transcript (greeting + 2 Q&A) + the four quick-reply chips | happy | P0 | authored ✓ (screen `opens with the scripted Figma transcript`) |
| E2E-MOB036-002 | Type a prompt + send → user bubble then assistant reply | happy | P0 | authored ✓ (screen `typing + send appends the user message and the reply`) |
| E2E-MOB036-003 | Tap a quick-reply chip → sends it as the next prompt | happy | P1 | authored ✓ (screen `tapping a quick-reply chip sends it as the next prompt`) |
| E2E-MOB036-004 | Empty / whitespace prompt is ignored (no bubble) | edge | P1 | authored ✓ (screen `an empty prompt does not append a bubble`) |
| E2E-MOB036-005 | Default responder returns the canned interim notice (EN) | edge | P1 | authored ✓ (screen `the default responder returns the canned interim notice`) |
| E2E-MOB036-006 | Arabic: the transcript seeds in Arabic; user bubble sits right of assistant (RTL) | rtl | P1 | authored ✓ (screen `Arabic: seeds the Arabic transcript and replies RTL`) |

## Scenarios

### E2E-MOB036-001 — Open the assistant (scripted transcript + chips)

```gherkin
Feature: AI assistant (Figma-parity shell)
  As a guest
  I want a chat surface seeded with the example conversation
  So that I can see how the assistant works and ask follow-ups

Scenario: The screen opens with the scripted transcript and quick-reply chips
  When the user opens /chatbot
  Then the transcript shows the assistant greeting and the two scripted Q&A pairs
  And four quick-reply chips are shown (Request a meeting · Upcoming sessions · SAMI booth location · Today's sessions)
  And no network request is made
```

**Evidence:** screen test `opens with the scripted Figma transcript`.

### E2E-MOB036-002 / 003 — Send a prompt / tap a chip

```gherkin
Scenario: Sending appends the user message then the assistant reply
  Given the user typed "Custom question?"
  When they press the gold send button
  Then a right-aligned user bubble shows "Custom question?"
  And a left-aligned assistant bubble (with the gold "AI" badge) shows the responder's reply

Scenario: Tapping a quick-reply chip sends it
  When the user taps the "Request a meeting" chip
  Then "Request a meeting" is appended as a user bubble and answered by the responder
```

**Evidence:** screen tests `typing + send appends the user message and the reply`,
`tapping a quick-reply chip sends it as the next prompt`.

### E2E-MOB036-004 / 005 / 006 — Empty prompt, canned notice, RTL

```gherkin
Scenario: A whitespace-only prompt is ignored
  When the user sends "   "
  Then no bubble is appended

Scenario: The default responder gives the interim notice (EN)
  Given no responder override (the real CannedChatbotResponder)
  When the user sends any prompt in English
  Then the assistant replies "The AI assistant is being connected — automatic replies are coming soon."

Scenario: Arabic transcript + RTL bubble pinning
  Given the app locale is Arabic
  Then the greeting reads "مرحباً 🤝 أنا مساعدك الذكي. كيف يمكنني المساعدة اليوم؟"
  And the user bubble "متى تبدأ جلسة الافتتاح؟" sits to the right of the assistant greeting
```

**Evidence:** screen tests `an empty prompt does not append a bubble`,
`the default responder returns the canned interim notice`,
`Arabic: seeds the Arabic transcript and replies RTL`.

---

_Last reviewed:_ `2026-06-19` by `SIMF Team`.
