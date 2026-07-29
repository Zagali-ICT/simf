# E2E test catalogue — `Send a question` (`send-question`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> submission endpoint is already built (D-169 / D-174); API tests in
> `tests/SIMF.Api.Tests/SessionQuestionsTests.cs`. The **Flutter screen is built**
> and widget-tested in
> `src/Mobile/simf_app/test/features/questions/send_question_screen_test.dart`
> (no-id empty state, empty-question inline prompt, submit success + clear,
> 400 not-open toast, generic error toast). It reuses the shipped wire contract
> (no new API).
>
> **Figma re-skin (frame `934:3636` "معلومات عن الجلسة"):** the screen was rebuilt
> to the KSA-Project frame on the shared `KsaPage` shell. The page is now titled
> **"معلومات عن الجلسة" / "Session information"** and shows a **"بيانات الجلسة" /
> "Session details" block** (`1049:12590`) — the session description rendered as a
> right-aligned **numbered list** (one entry per non-blank description line) — above
> the question composer: the "الاسئلة" / Questions section label (`945:3756`), a
> tinted **borderless** navy multiline question box (`934:3668`, `اكتب سؤالك هنا…`
> placeholder, `maxLength=500`), a gold full-width submit (`942:3746`), and a centred
> gold-bulleted "ملاحظة" / Note note (`943:3750`). The session-data block
> reads the **anonymous** detail (`GET /app/programme/sessions/{id}` — the shipped
> endpoint, **no new API**) and is **non-blocking context**: a failed read just hides
> the block and the composer still works.
>
> **B7 (2026-07-27) — the "إلى من؟" recipient choice is back and wired.** The screen
> had hardcoded `QuestionRecipient.speaker`, so `recipient` was **always 0**: the Host
> half of `SessionQuestionRecipient` could not be produced by any client, and the three
> "إلى من؟" / المتحدث / المضيف strings were unreferenced. The composer now shows two
> pills (shared `SimfRadioPill`) above the question box. **Speaker stays the default**,
> so a user who never taps sends exactly what the shipped app sent.
>
> **A17 (2026-07-27) — the ملاحظة note no longer promises a review that does not
> happen.** It read "تتم مراجعة الأسئلة قبل عرضها على الهواء" / "Questions are reviewed
> before going on air", which is false for a **live** question: once the session has
> started the question skips the AI filter and the Scientific Committee and lands
> **Approved** (E2E-MOB026-014). The note now names the gate that is always real — the
> moderator: "يختار مشرف الجلسة الأسئلة التي تُعرض على الهواء" / "The session moderator
> picks which questions go on air."

| | |
|--|--|
| **Page** | [`Page_026`](../../App/Page_026/README.md) |
| **Route** | `POST /api/v1/app/sessions/{sessionId}/questions` · app screen #26 `/live/question?sessionId={id}` |
| **Surface** | Mobile (Flutter) + App API |
| **Auth setup** | **Approved account** — the route is auth-gated and the endpoint is `RequireApprovedAccount`. Auth-setup via the `Get-Totp` helper for an admin, or a visitor email-OTP session. |
| **Last reviewed** | 2026-07-09 (D-714 — advisory AI filter wired config-gated, item 12 GAP-1) |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB026-001 | No `sessionId` → "open from a live session" empty state | edge | P1 | authored ✓ (screen `no session id shows the open-from-a-session empty state`) |
| E2E-MOB026-002 | Pick recipient (Host) + type + Submit → 200, sent toast, `recipient = 1` | happy | P0 | authored ✓ (screen `choosing Host submits recipient = 1`) |
| E2E-MOB026-003 | Empty question → inline prompt, no call | validation | P0 | authored ✓ (screen `empty question shows the inline prompt, no submit`) |
| E2E-MOB026-004 | 400 `SESSION_NOT_LIVE_FOR_QUESTIONS` (outside window) → not-open toast | edge | P0 | authored ✓ (screen `a 400 SESSION_NOT_LIVE_FOR_QUESTIONS shows the not-open toast`) |
| E2E-MOB026-005 | A server-500 / transport failure → generic error toast | resilience | P0 | authored ✓ (screen `a generic failure shows the generic error toast`) |
| E2E-MOB026-006 | RTL — the screen renders right-to-left in Arabic | i18n | P2 | covered (l10n AR/EN pairs; `Directionality` from the locale) |
| E2E-MOB026-007 | Figma form chrome — الاسئلة label + tinted box + gold submit + ملاحظة note all render | layout | P1 | authored ✓ (screen `golden render — label, question box, submit, note`) |
| E2E-MOB026-008 | Question box accepts multiline + `اكتب سؤالك هنا…` placeholder, caps at 500 chars | layout | P1 | _to author_ |
| E2E-MOB026-009 | **B7** — the "إلى من؟" picker renders both pills; a user who never taps still posts the default recipient (Speaker = 0), and switching Host → Speaker posts 0 again | edge | P0 | authored ✓ (screen `golden render — label, question box, submit, note` asserts Send to / Speaker / Host; `typing + submit sends to the default recipient + sent toast` → 0; `switching back to Speaker submits recipient = 0`) |
| E2E-MOB026-015 | **A17** — the ملاحظة note names the moderator, and the old "reviewed before going on air" copy is gone (it was false for a live question) | validation | P1 | authored ✓ (screen `golden render — label, question box, submit, note` asserts the moderator copy present + the review copy absent) |
| E2E-MOB026-010 | بيانات الجلسة block renders the session description as a numbered list | layout | P1 | authored ✓ (screen `renders the بيانات الجلسة block as a numbered list`) |
| E2E-MOB026-011 | Session-detail read fails → block hidden, composer still works | resilience | P1 | authored ✓ (screen `hides the بيانات الجلسة block when the detail read fails`) |
| E2E-MOB026-012 | **Advisory AI filter (D-714 GAP-1), PRE-questions only (two-path Q&A, 2026-07-19)** — for a question asked **before** the session goes live the server runs stage 1: default the offline stub (`stub-clean`), or the real `AiQuestionFilter` (via `IAiService` + the seeded `question-filter` prompt → `ai-clean`/`ai-flagged`, `ai-unavailable` fallback) when `SessionQuestions:AiFilterEnabled=true`. **Advisory only** — the verdict is recorded for the Committee and NEVER changes the question's Pending status. A **LIVE** question skips the AI filter entirely (verdict null) — see E2E-MOB026-014 | happy/resilience | P1 | authored ✓ (backend `QuestionAiFilterTests` — verdict map + all fallbacks; `SessionQuestionsTests.Pre_question_is_AI_screened_and_waits_for_the_committee`) |
| E2E-MOB026-013 | **Venue gate — no self-assert (S-5)** — the app always sends `isAtVenue: false`; the server is the authoritative LIVE gate. A **geofenced** hall requires a real `HallAttendance` arrival (else 403 `NOT_AT_VENUE`); a **non-geofenced** hall has no arrival mechanism, so the question is accepted (remote Q&A). Before start the venue gate is skipped entirely | validation | P0 | authored ✓ (app `questions_repository_test.dart` posts `isAtVenue: false`; backend `QuestionArrivalGatingTests` + `SessionQuestionsTests.Submit_without_at_venue_flag_accepts_remote_question_on_a_non_geofenced_hall`) |
| E2E-MOB026-014 | **Two-path routing (owner 2026-07-19)** — the server routes a submission by phase. A **LIVE** question (asked once the session has started) skips the AI filter **and** the Scientific Committee and lands **Approved**, straight on the per-session moderator desk for accept (push) / reject (hide). A **PRE** question (asked before start) runs the advisory AI filter and lands **Pending** for the Committee → then the desk. The composer screen is identical for both (the server decides) | happy | P0 | authored ✓ (backend `SessionQuestionsTests.Live_question_skips_AI_and_lands_directly_on_the_moderator_desk` + `Pre_question_is_AI_screened_and_waits_for_the_committee`) |
| E2E-MOB026-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOB026-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-MOB026-001 — No session → empty state

```gherkin
Feature: Send a question (live Q&A)
  As an approved attendee
  I want to send a question to the speaker or host
  So that it can be answered on air

Scenario: Opened without a live session
  Given the screen is opened with no sessionId
  Then it shows the "open from a live session" empty state
  And no recipient picker or question field is shown
```

**Evidence:** screen test `no session id shows the open-from-a-session empty state`.

### E2E-MOB026-002 — Submit a question

```gherkin
Scenario: Send a question to the host
  Given the screen is opened with a live session id
  And the attendee taps the "المضيف" / Host pill in the "إلى من؟" picker
  And the question text is "Who chairs the panel?"
  When the attendee taps Send question
  Then the app calls POST /api/v1/app/sessions/{id}/questions
  And the body has questionText, isAtVenue=false, recipient=1
  And a "question sent" toast is shown and the field is cleared

Scenario: Send a question to the speaker (the default — B7)
  Given the screen is opened with a live session id
  And the attendee does NOT tap the recipient picker
  And the question text is "How deep is the reef?"
  When the attendee taps Send question
  Then the body carries recipient=0, exactly as the shipped app sent
```

**Evidence:** screen tests `choosing Host submits recipient = 1`,
`switching back to Speaker submits recipient = 0`, and
`typing + submit sends to the default recipient + sent toast`;
API `SessionQuestionsTests`. (`isAtVenue` is always `false` from the app —
the server is the authoritative venue gate, E2E-MOB026-013.)

### E2E-MOB026-003 — Empty question / E2E-MOB026-004 — Not open / E2E-MOB026-005 — Generic error

```gherkin
Scenario: An empty question is blocked client-side
  Given the question field is empty
  When the attendee taps Send question
  Then an inline "type your question first" prompt is shown
  And no request is sent

Scenario: The session is over (#7 — phase-based window)
  # A FUTURE session (before start) is now OPEN to any approved user with no
  # venue gate; a LIVE session is venue-gated; only a session past its End
  # returns SESSION_NOT_LIVE_FOR_QUESTIONS (the after-view is a recording).
  Given the submit returns 400 SESSION_NOT_LIVE_FOR_QUESTIONS
  When the attendee submits a question
  Then the not-open toast reads "الأسئلة مغلقة لهذه الجلسة." /
       "Questions are closed for this session."
  # DEF-MOD-006 — the old copy promised a 5-minute pre-start window that
  # SessionQuestionService has never enforced (there is no lower bound at all;
  # questions simply close at the session End). The string now describes the
  # real behaviour — the server rule is intentional and was NOT changed.

Scenario: A server / transport failure
  Given the submit fails with a 500 (or transport error)
  When the attendee submits a question
  Then a generic "could not send your question" toast is shown
```

**Evidence:** screen tests `empty question shows the inline prompt, no submit`,
`a 400 SESSION_NOT_LIVE_FOR_QUESTIONS shows the not-open toast`,
`a generic failure shows the generic error toast`. A 404 maps to the same
not-open toast as the 400 (the screen treats both as "not currently open").

### E2E-MOB026-006 — RTL

```gherkin
Scenario: Arabic renders right-to-left
  Given the app locale is Arabic
  Then the title shows "معلومات عن الجلسة"
  And the "بيانات الجلسة" section header + the "الاسئلة" composer lay out right-to-left
  And the "إلى من؟" picker shows المتحدث / المضيف, laid out right-to-left
```

**Evidence:** the l10n getters pair AR/EN (`sendQuestion*`); `Directionality`
follows the active locale, as on every shipped mobile screen.

### E2E-MOB026-007 — Figma form chrome (frame `934:3636`)

```gherkin
Scenario: The re-skinned form renders every Figma section
  Given the screen is opened with a live session id
  Then an "إلى من؟" (EN "Send to") label sits above two pills, المتحدث / المضيف
  And the section label reads "الاسئلة" (EN "Questions"), inline-end aligned
  And a tinted navy multiline question box is shown below it
  And a gold full-width button reads "إرسال السؤال" (EN "Send question")
  And a centred note reads "ملاحظة …" — "ملاحظة" (EN "Note") gold/bold, then
      "يختار مشرف الجلسة الأسئلة التي تُعرض على الهواء."
      (EN "The session moderator picks which questions go on air.") in beige
  And the old "تتم مراجعة الأسئلة قبل عرضها على الهواء." copy is NOT shown
```

**Evidence:** screen test `golden render — label, question box, submit, note`
asserts the moderator copy present and the old review copy absent, plus the three
picker strings. The form renders `sendQuestionSectionLabel`, the navy box,
`sendQuestionSubmit`, the `SendQuestionRecipientPicker`, and the `ReviewNote` with
`sendQuestionNoteLabel` + `sendQuestionWindowHint`. Matches Figma frame `934:3636`
(sub-frames `945:3756`, `934:3668`, `942:3746`, `943:3750`).

### E2E-MOB026-008 — Question box (multiline, placeholder, 500 cap)

```gherkin
Scenario: The tinted question box accepts a multiline question
  Given the screen is opened with a live session id
  And the empty box shows the placeholder "اكتب سؤالك هنا…"
      (EN "Type your question here…")
  When the attendee types a two-line question
  Then both lines are kept (the field grows from 4 up to 6 lines)
  And typing is capped at 500 characters with no visible counter
  And the gold "إرسال السؤال" submit becomes active
```

**Evidence:** `TextField` `hintText = sendQuestionHint`, `minLines: 4`,
`maxLines: 6`, `maxLength: 500`, `counterText: ''`.

### E2E-MOB026-009 — Recipient picker wired, Speaker still the default (B7)

```gherkin
Scenario: The picker renders and defaults to Speaker
  Given the screen is opened with a live session id
  Then an "إلى من؟" picker shows the المتحدث and المضيف pills
  And المتحدث (Speaker) is pre-selected
  And the question text is "ما عمق الشعاب المرجانية؟"
  When the attendee taps "إرسال السؤال" without touching the picker
  Then the app calls POST /api/v1/app/sessions/{id}/questions
  And the body has recipient=0 (Speaker, the default)
  And the "تم إرسال سؤالك" (EN "Your question was sent") toast is shown
  And the question box is cleared

Scenario: Switching Host then back to Speaker posts 0 again
  Given the screen is opened with a live session id
  When the attendee taps المضيف and then المتحدث
  And types a question and taps "إرسال السؤال"
  Then the body has recipient=0
```

**Evidence:** the static `_recipient = QuestionRecipient.speaker` (`wireIndex == 0`)
is passed as `recipientIndex`; no `المتحدث`/`المضيف` pills render. Preserves the
shipped wire contract (D-169/D-174). Note: scenario E2E-MOB026-002 above still
describes the old Host-pill path for the pre-re-skin contract; the picker is now
gone, so the live path is recipient=0.

### E2E-MOB026-010 — بيانات الجلسة block (numbered session data)

```gherkin
Scenario: The session-data block renders the description as a numbered list
  Given the screen is opened with a live session id
  And GET /api/v1/app/programme/sessions/{id} returns a description with
    two non-blank lines ("First point", "Second point")
  Then a "بيانات الجلسة" (EN "Session details") header is shown above the composer
  And the lines render as a right-aligned numbered list: "1." First point, "2." Second point
  And the "الاسئلة" composer renders below the block
```

**Evidence:** screen test `renders the بيانات الجلسة block as a numbered list`
(`_SessionDataBlock` + `_NumberedLine`, fed by `SessionDetail.localizedDescription`).

### E2E-MOB026-011 — Session-detail read fails → block hidden, composer works

```gherkin
Scenario: A failed session-detail read hides the block, not the composer
  Given the screen is opened with a live session id
  And GET /api/v1/app/programme/sessions/{id} fails (e.g. 500 / transport)
  Then no "بيانات الجلسة" block is shown
  And the question composer (field + gold submit + note) still renders and works
```

**Evidence:** screen test `hides the بيانات الجلسة block when the detail read fails`;
`_loadDetail` swallows `ApiFailure` (the block is optional context).

### E2E-MOB026-013 — Venue gate, no self-assert (S-5)

```gherkin
Scenario: The app never self-certifies venue presence
  Given the send-question composer submits a question
  Then the request body carries isAtVenue = false (the app does not self-assert)

Scenario: The server is the authoritative LIVE venue gate
  Given a LIVE session whose hall has a geofence
  And the visitor has NOT recorded a hall arrival
  When they submit a question
  Then the API returns 403 "NOT_AT_VENUE"
  When the visitor has recorded a hall arrival (HallAttendance)
  Then the submission is accepted (200)

Scenario: A non-geofenced hall accepts remote questions
  Given a LIVE session whose hall has no arrival mechanism
  When the visitor submits a question (isAtVenue false)
  Then it is accepted (200) — remote Q&A works
```

**Evidence:** app `questions_repository_test.dart` (body `isAtVenue == false`);
backend `QuestionArrivalGatingTests` (geofenced requires arrival; non-geofenced
accepts) + `SessionQuestionsTests.Submit_without_at_venue_flag_accepts_remote_question_on_a_non_geofenced_hall`.

### E2E-MOB026-014 — Two-path routing (owner 2026-07-19)

```gherkin
Scenario: A live question goes straight to the moderator desk (no AI, no committee)
  Given a session that is already live
  And the attendee submits a question
  Then the server stores it Approved in the Live phase with no AI verdict
  And it appears on that session's moderator desk immediately
  And it never enters the Scientific Committee queue

Scenario: A pre-question is screened by AI and waits for the committee
  Given a session that has not yet started
  And the attendee submits a question
  Then the server runs the advisory AI filter and stores it Pending in the Pre phase
  And it appears in the Scientific Committee queue
  And it is NOT on the moderator desk until the committee approves it
```

**Evidence:** backend `SessionQuestionsTests.Live_question_skips_AI_and_lands_directly_on_the_moderator_desk`
and `Pre_question_is_AI_screened_and_waits_for_the_committee`. The moderator desk +
committee mechanics are covered by `cp-session-moderate.md` / `cp-admin-question-queue.md`.

---

_Last reviewed:_ `2026-07-26` by `Claude` — **DEF-MOD-006: `sendQuestionNotOpen`
claimed a 5-minute pre-start window the server never enforced; the AR+EN string now
says "Questions are closed for this session." The server rule is unchanged (no lower
bound; closes at the session End). E2E-MOB026-004 reworded.**
_Prior:_ `2026-07-19` by `Claude` — **Two-path Q&A (owner): the server now
routes a submission by phase — a LIVE question skips the AI filter + the Scientific
Committee and lands Approved straight on the moderator desk; a PRE question runs the
advisory AI filter and lands Pending for the Committee. Composer screen unchanged
(server decides); E2E-MOB026-014, and 012 clarified as PRE-only.** _Prior:_ `2026-07-11` by `Claude` — **S-5: the app no longer self-certifies
venue presence (always sends `isAtVenue: false`); the server is the authoritative
LIVE gate (geofenced hall requires a real `HallAttendance` arrival, non-geofenced
hall accepts remote Q&A); E2E-MOB026-013.** _Prior:_ `2026-07-10` by `SIMF Team` — **#7 (D-733): the server question
window is now phase-based — a FUTURE session (before start) accepts questions from
any approved user with NO venue gate; a LIVE session keeps the check-in/venue
gate; a session past its End is closed (`SESSION_NOT_LIVE_FOR_QUESTIONS`). The
composer screen is unchanged (still maps the 400/404 to the not-open toast); the
ask ENTRY visibility is gated on the session-detail (future-only) and
live-broadcast (live-only) screens — see `mobile-session-detail.md` /
`mobile-live.md`.** _Prior:_ `2026-06-19`.
