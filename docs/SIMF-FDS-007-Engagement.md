# Feature Design Specification — Engagement

| Field | Value |
|-------|-------|
| Document ID | SIMF-FDS-007 |
| Title | Feature Design Specification — Engagement |
| Version | 1.0 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Product Owner |
| Approver | Product Owner |
| Date issued | 2026-05-20 |
| Related documents | SIMF-FDS-003, SIMF-FDS-004, SIMF-SRS-001, SIMF-UCS-001, SIMF-DAT-001, SIMF-RDR-001, SIMF-SAD-001 |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. The engagement feature, build-ready. |

---

## 1. Purpose

This is the build-ready specification for engagement during a session — the
live broadcast, the questions an attendee puts to the moderator, and the
comments, with their moderation. It is how the audience takes part in a
session.

## 2. Scope

The feature covers:

- the live broadcast of a session, with AI translation or captions and a
  language choice,
- the geographic restriction on the live stream,
- session questions — composing a question, the recipient, and the rule that
  gates when questions are open,
- the moderator's handling of questions for the sessions assigned to them,
- comments — composing a comment and the two-stage moderation, AI then admin.

It does **not** define sessions — that is the Forum Programme feature
(SIMF-FDS-004). It does not produce the hall-arrival record it depends on —
that is Badge & Access Control (SIMF-FDS-003). The **AI session summary** is
the Networking & Cognitive AI feature (SIMF-FDS-008), not this one.

The standalone mockup screens for "send question", "request interview" and
"audience comments" (26–28) were removed as screens; the question and comment
**features remain**, integrated into the live and session experience, and the
interview-request feature is dropped (SIMF-CON-001 section 14).

## 3. Requirements and use cases covered

| From SIMF-SRS-001 | From SIMF-UCS-001 |
|-------------------|-------------------|
| FR-701 live broadcast, AI translation, language | UC-13 Watch a live session |
| FR-702 the Riyadh-region restriction | UC-13 |
| FR-703 ask a question, recipient | UC-14 Ask a question in a session |
| FR-704 questions gated by hall arrival, closed at session end | UC-14 |
| FR-705 the moderator handles questions | UC-36 Manage the questions of an assigned session |
| FR-706, FR-707 comments and two-stage moderation | UC-15 Comment, UC-26 Moderate comments |

Decision **D5** governs the question timing and the comment moderation.

## 4. Feature overview

```
Session marked live (FDS-004)
        │
        ▼
  Live broadcast ── AI translation/captions ── Riyadh-region restriction
        │
        ├─▶ Questions  ─▶ gated by hall arrival ─▶ Moderator handles
        │
        └─▶ Comments   ─▶ AI filter ─▶ Admin review ─▶ shown in the feed
```

Real-time updates — the question stream, the comment feed, the moderation queue
— run over SignalR (SIMF-SAD-001 section 6.4).

## 5. Detailed behaviour

### 5.1 Live broadcast

- A session marked **live** (SIMF-FDS-004) can be broadcast. A user holding the
  Live Sessions page with the Manage broadcast action — the Scientific team in
  the suggested configuration (decision D11) — starts and stops the broadcast.
- The `LiveSessionState` records whether the session is broadcasting, when it
  started, and the caption language.
- The attendee watches the stream on the live screen (mockup Screen 25): the
  video, a red live indicator, and the live session's title and speakers.
- **AI translation / captions.** A caption track of the spoken content is shown
  over the video, with a **language picker** so the attendee chooses the caption
  language. The translation is produced through the cognitive-AI abstraction
  (SIMF-SAD-001 section 9.2); the provider is deferred (decision D7).
- **Geographic restriction.** The live stream is available only within the
  Riyadh region (FR-702). An attendee outside the region sees the restriction
  notice instead of the stream.
- The video stream itself is embedded from the live-broadcast platform; the
  platform is deferred and reached through an abstraction (decision D7).

### 5.2 Session questions

- An attendee can put a **question** to the moderator during a session
  (`UC-14`). The attendee chooses the recipient — the **speaker** or the
  **host** — and writes the question.
- **When questions are open.** A session's questions open for an attendee only
  **after that attendee is verified as arrived at the hall** — they have a
  `HallAttendance` enter record for the session (SIMF-FDS-003) — and they
  **close at session end** (decision D5). Until the attendee has arrived, the
  question composer is not offered.
- A submitted question is recorded as `Pending` and the attendee is told it
  will be reviewed before it goes on air.
- A question moves through the states: `Pending` → `OnAir`, `Answered`, or
  `Hidden`, set by the moderator (section 5.3).

### 5.3 The moderator and the question queue

- A **Moderator** is a mobile-app role assigned to specific sessions (decision
  D3, SIMF-RPM-001). A moderator handles questions only for their assigned
  sessions (`UC-36`).
- For an assigned session the moderator can: view the incoming questions,
  **order** them, **hide** an unsuitable one, put a question **on air** to the
  speaker, and mark a question **answered**.
- The question stream updates live for the moderator over SignalR.

### 5.4 Comments and two-stage moderation

- An attendee can post a **comment** on a session (`UC-15`).
- Every comment passes **two gates**, in order (decision D5):
  1. **AI filter.** The comment is checked by the cognitive-AI filter; the
     result — `Passed` or `Flagged` — is recorded with the comment. A flagged
     comment is **not discarded**; it still goes to the queue, marked flagged.
  2. **Admin review.** The comment waits in the **Comment Moderation queue** in
     the Control Panel. A user holding the Comment Moderation page with the
     Moderate action — the Scientific team, the single owner (decision D11) —
     approves or discards it.
- The AI never approves a comment on its own; every comment reaches an admin.
- An **approved** comment appears in the session's comment feed; a **discarded**
  comment does not. The moderation queue and the comment feed update live over
  SignalR (`UC-26`, SIMF-CPD-001 section 13.5).

## 6. Data

The feature uses these entities from SIMF-DAT-001 section 5.6: `LiveSessionState`,
`SessionQuestion`, `Comment`. It reads `Session`, `User` and `HallAttendance`.
A `Comment` always carries both an `AiResult` and an `AdminDecision`.

## 7. User interface

| Surface | Screens |
|---------|---------|
| Mobile app | Screen 25 the live broadcast; the question composer and the comment feed, integrated into the live/session experience |
| Mobile app (Moderator) | The moderator's question queue for an assigned session |
| Control Panel | Live Sessions (start/stop the broadcast); the Comment Moderation queue, per SIMF-CPD-001 section 13.5 |

Mobile visuals are the external designer's; Control Panel screens follow
SIMF-CPD-001. Captions and all text are localised; loading, empty and error
states are present; the live screens reflect the connection state rather than
showing stale data.

## 8. Validation rules

| Item | Rule |
|------|------|
| Start broadcast | The session is marked live; the user holds Manage broadcast |
| Question recipient | Required; speaker or host |
| Question text | Required; non-empty |
| Question availability | The attendee has a hall-arrival record for the session; the session has not ended |
| Comment text | Required; non-empty |
| Comment moderation | A comment is shown only after an admin approves it |

## 9. Security and privacy considerations

- The live stream enforces the Riyadh-region restriction server-side; the
  client does not decide eligibility.
- Question and comment submission is tied to the signed-in, Approved attendee.
- A question is gated on a real hall-arrival record; the client cannot bypass
  the gate.
- Comment text is user content in one language; it is stored with its language
  and screened by the AI filter before any display.
- Moderator and admin actions on questions and comments — hide, put on air,
  approve, discard — are written to the operation log.
- The cognitive-AI filter and translation run through an abstraction; the
  provider is given only the text it needs (decision D7, SIMF-SAD-001 section
  9.2).

## 10. Acceptance criteria

1. A live session can be started and stopped by a user with the Manage
   broadcast permission; the attendee sees the stream.
2. Captions are shown with a language picker; the attendee can change the
   caption language.
3. An attendee outside the Riyadh region sees the restriction notice, not the
   stream.
4. The question composer is offered only after the attendee has a hall-arrival
   record for the session, and not after the session ends.
5. A submitted question is `Pending` and the attendee is told it will be
   reviewed.
6. A moderator sees, orders, hides, puts on air and answers questions for their
   assigned sessions only, with live updates.
7. Every comment is screened by the AI filter, then waits in the moderation
   queue; a flagged comment is queued, not discarded.
8. An admin approves or discards a queued comment; the AI never approves on its
   own; an approved comment appears in the feed.
9. The moderation queue and the comment feed update live.
10. All screens render in Arabic (RTL) and English (LTR); no hardcoded text.
11. The build is clean and the feature has unit, integration and end-to-end
    tests that pass.

## 11. Test scenarios

| # | Scenario | Expected |
|---|----------|----------|
| T-01 | Start a live broadcast | `LiveSessionState` broadcasting; attendees see the stream |
| T-02 | Change the caption language | captions switch language |
| T-03 | Open the live stream from outside the Riyadh region | restriction notice shown; no stream |
| T-04 | Ask a question with a hall-arrival record | question recorded `Pending` |
| T-05 | Try to ask a question without a hall-arrival record | the composer is not offered |
| T-06 | Try to ask a question after the session ended | questions closed |
| T-07 | Moderator orders, hides and puts a question on air | states update; only assigned sessions |
| T-08 | Moderator opens a session not assigned to them | no access to its question queue |
| T-09 | Post a clean comment | AI result `Passed`; queued for admin |
| T-10 | Post a comment the AI flags | AI result `Flagged`; queued, not discarded |
| T-11 | Admin approves a queued comment | comment appears in the feed |
| T-12 | Admin discards a queued comment | comment does not appear; action logged |
| T-13 | Two attendees comment while a moderator watches | the queue updates live for the moderator |
| T-14 | Render the engagement screens in Arabic and English | correct layout and direction; no hardcoded text |

## 12. Open items

| ID | Item | Affects |
|----|------|---------|
| OI-1 | Confirm the live-broadcast platform and the AI translation provider as decision D7 closes | Section 5.1 |
| OI-2 | Confirm how the Riyadh-region restriction is determined — device location, IP region, or both | Section 5.1 |
| OI-3 | Confirm the AI comment-filter rules — what it screens for — with the client | Section 5.4 |
| OI-4 | Confirm whether a moderator may also act from the Control Panel, or only the app | Section 5.3 |
| OI-5 | Confirm document classification with the owner | Control block |

---

End of document.
