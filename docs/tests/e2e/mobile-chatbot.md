# E2E test catalogue — `AI assistant` (`chatbot`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue. The
> AI assistant is wired to the **centralised AI** (`POST /app/ai/assistance` — the
> `assistance` prompt, grounded server-side on the live event context: programme
> sessions, FAQ, booths). Pixel-parity to KSA Figma frame `1064:13066` (D-448): the
> navy shell, an assistant/user bubble transcript (assistant left + gold "AI" badge,
> user right + gold fill), the **quick-reply chips** (frame `1070:13389`) and the
> bottom **input bar** (frame `1070:13398`). The screen opens with the greeting; each
> prompt (typed or a chip) is answered by the overridable `ChatbotResponder` seam
> (default = `ApiChatbotResponder` → the API). A wire error shows a localized error
> bubble. Widget-tested in
> `src/Mobile/simf_app/test/features/chatbot/chatbot_screen_test.dart`.

| | |
|--|--|
| **Page** | [`chatbot`](../../pages/mobile/chatbot/README.md) |
| **Route** | app screen #36 `/chatbot` — `POST /app/ai/assistance` (centralised AI, grounded) |
| **Surface** | Mobile (Flutter) only |
| **Auth setup** | **Signed-in / approved account** (the endpoint requires an approved account; reached from the visitor home). |
| **Last reviewed** | 2026-07-22 (wired to the real grounded `/app/ai/assistance`) |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB036-001 | Open → the assistant greeting + the four quick-reply chips (no scripted transcript) | happy | P0 | authored ✓ (screen `opens with only the greeting — no scripted transcript`) |
| E2E-MOB036-002 | Type a prompt + send → user bubble then the AI reply | happy | P0 | authored ✓ (screen `typing + send appends the user message and the AI reply`) |
| E2E-MOB036-003 | Tap a quick-reply chip → sends it as the next prompt | happy | P1 | authored ✓ (screen `tapping a quick-reply chip sends it as the next prompt`) |
| E2E-MOB036-004 | Empty / whitespace prompt is ignored (no bubble) | edge | P1 | authored ✓ (screen `an empty prompt does not append a bubble`) |
| E2E-MOB036-005 | A wire error (offline / provider failure) shows the localized error bubble | error | P0 | authored ✓ (screen `a wire error shows the localized error bubble`) |
| E2E-MOB036-006 | Arabic: greets in Arabic; the sent user bubble sits right of assistant (RTL) | rtl | P1 | authored ✓ (screen `Arabic: greets in Arabic and pins the sent bubble RTL`) |
| E2E-MOB036-007 | Grounded answer cites the real agenda (live, requires a configured provider) | happy | P1 | manual (live) — see below |
| E2E-MOB036-008 | Re-open → the saved transcript reloads (conversation persists, D-756) | happy | P1 | authored ✓ (screen `renders the saved history under the greeting`) + backend `Assistance_persists_the_turn_and_history_returns_it` |
| E2E-MOB036-009 | The assistant remembers earlier turns (memory across messages, D-756) | happy | P1 | backend `Assistance_second_call_includes_prior_turns_as_memory` |
| E2E-MOB036-010 | **The message input has an accessible name (BUG-012)** — `ChatComposer`'s text box exposes its placeholder as its own semantics label (the placeholder itself vanishes once the user types); the gold send square already had one | a11y | P2 | authored ✓ (same `Semantics(label:, textField: true)` wrap covered by `simf_search_field_semantics_test`) |

## Scenarios

### E2E-MOB036-001 — Open the assistant (greeting + chips)

```gherkin
Feature: AI assistant (centralised AI, grounded on the live event)
  As a signed-in visitor
  I want to ask the assistant about the event
  So that I get answers grounded in the real agenda / FAQ / booths

Scenario: The screen opens with only the greeting and the quick-reply chips
  When the user opens /chatbot
  Then the transcript shows only the assistant greeting
  And no scripted demo conversation is shown
  And four quick-reply chips are shown (Request a meeting · Upcoming sessions · SAMI booth location · Today's sessions)
```

**Evidence:** screen test `opens with only the greeting — no scripted transcript`.

### E2E-MOB036-002 / 003 — Send a prompt / tap a chip

```gherkin
Scenario: Sending appends the user message then the AI reply
  Given the user typed "When is the opening?"
  When they press the gold send button
  Then a right-aligned user bubble shows "When is the opening?"
  And POST /app/ai/assistance is called with { message, locale }
  And a left-aligned assistant bubble (with the gold "AI" badge) shows the returned answer

Scenario: Tapping a quick-reply chip sends it
  When the user taps the "Request a meeting" chip
  Then "Request a meeting" is appended as a user bubble and answered by the assistant
```

**Evidence:** screen tests `typing + send appends the user message and the AI reply`,
`tapping a quick-reply chip sends it as the next prompt`.

### E2E-MOB036-004 / 005 / 006 — Empty prompt, wire error, RTL

```gherkin
Scenario: A whitespace-only prompt is ignored
  When the user sends "   "
  Then no bubble is appended

Scenario: A wire error shows the localized error bubble
  Given the assistance call fails (offline, or the provider returns an error)
  When the user sends a prompt
  Then the user's message stays in the transcript
  And the assistant bubble shows "Couldn't get a reply right now. Please try again." (EN) / "تعذّر الحصول على رد الآن. حاول مرة أخرى." (AR)

Scenario: Arabic greeting + RTL bubble pinning
  Given the app locale is Arabic
  Then the greeting reads "مرحباً 🤝 أنا مساعدك الذكي. كيف يمكنني المساعدة اليوم؟"
  And after sending "متى تبدأ جلسة الافتتاح؟" the user bubble sits to the right of the assistant greeting
```

**Evidence:** screen tests `an empty prompt does not append a bubble`,
`a wire error shows the localized error bubble`,
`Arabic: greets in Arabic and pins the sent bubble RTL`.

### E2E-MOB036-007 — Grounded answer (live)

```gherkin
Scenario: The assistant answers from the real event data
  Given a real AI provider is configured (see the AI go-live runbook) and the programme has a session "Opening Session" at 08:00 in the Main Hall
  When the user asks "When does the opening session start?"
  Then the answer states 08:00 / the Main Hall (from the live agenda, not model priors)
  And a question about a non-existent session is answered "I don't have that information" (grounding refusal)
```

**Evidence:** manual live check on a provider-configured environment (echo returns the
echoed prompt, so this scenario is asserted only against a real provider). The grounding
plumbing is unit-tested in `AssistanceContextBuilderTests` and
`AiModuleTests.Assistance_prompt_is_grounded_with_the_event_context_block`.

### E2E-MOB036-008 / 009 — Persisted history + memory (D-756)

```gherkin
Scenario: The conversation survives navigation / app-restart
  Given the visitor previously asked "Where is hall H1?" and got an answer
  When they re-open /chatbot
  Then GET /app/ai/assistance/history is called
  And the transcript shows the greeting followed by the saved turns (oldest-first)

Scenario: The assistant remembers earlier turns
  Given the visitor said "My name is Sam." earlier in the conversation
  When they later ask "What did I just say?"
  Then the assistance call carries the recent turns as a {history} context block
  And the answer reflects the earlier turn (not a fresh, memory-less reply)
```

**Evidence:** screen test `renders the saved history under the greeting (persisted)`;
backend `AiModuleTests.Assistance_persists_the_turn_and_history_returns_it` +
`Assistance_second_call_includes_prior_turns_as_memory`.

---

_Last reviewed:_ `2026-07-26` by `SIMF Team` — BUG-012: the composer's message box
now carries an accessible name (E2E-MOB036-010). _Prior:_ `2026-07-22` by `SIMF Team`.
