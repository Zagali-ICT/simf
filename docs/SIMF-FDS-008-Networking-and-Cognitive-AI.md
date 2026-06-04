# Feature Design Specification — Networking and Cognitive AI

| Field | Value |
|-------|-------|
| Document ID | SIMF-FDS-008 |
| Title | Feature Design Specification — Networking and Cognitive AI |
| Version | 1.0 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Product Owner |
| Approver | Product Owner |
| Date issued | 2026-05-20 |
| Related documents | SIMF-FDS-002, SIMF-FDS-004, SIMF-FDS-007, SIMF-SRS-001, SIMF-UCS-001, SIMF-DAT-001, SIMF-RDR-001, SIMF-SAD-001 |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. The networking and cognitive-AI feature, build-ready. |

---

## 1. Purpose

This is the build-ready specification for networking and the cognitive AI — how
attendees connect with one another, and how the AI assistant, the session
summaries and the accessibility aids work.

## 2. Scope

The feature covers:

- interests,
- the "meet people like you" matchmaking,
- one-to-one meeting requests and their approval,
- the AI assistant, backed by the two-level FAQ knowledge,
- the AI session summary,
- the accessibility AI — sign-language and speech conversion, captions,
- the AI provider abstraction and the AI settings.

It does **not** define the cognitive-AI provider — that is deferred (decision
D7) and reached through an abstraction. It does not cover live-session captions
in their broadcast context — that is the Engagement feature (SIMF-FDS-007),
which uses the same translation abstraction.

## 3. Requirements and use cases covered

| From SIMF-SRS-001 | From SIMF-UCS-001 |
|-------------------|-------------------|
| FR-801 interests | UC-17 Use the AI assistant |
| FR-802 matchmaking and the match score | (meet people like you) |
| FR-803 the 80% recommendation and push | (meet people like you) |
| FR-804 one-to-one meeting requests | UC-16 Request a one-to-one meeting |
| FR-805 the AI assistant | UC-17 |
| FR-806 the two-level FAQ knowledge | UC-17 |
| FR-807 accessibility AI | (accessibility) |
| FR-808 the AI provider abstraction | (architecture) |
| FR-708 the AI session summary | (smart features) |

Decision **D5** defines the two-level FAQ; decision **D7** defers the AI
provider.

## 4. Feature overview

```
Interests ─┐
Sessions  ─┼─▶ Matchmaking ─▶ match score ─▶ ≥80% ─▶ recommendation + push
           │
Meeting requests ─▶ PR approval
           │
FAQ (groups → entries) ─▶ AI assistant
Sessions ─▶ AI session summary
Spoken/written content ─▶ accessibility AI
```

Every AI capability runs through the cognitive-AI abstraction (SIMF-SAD-001
section 9.2); the provider is configuration, not code (decision D7).

## 5. Detailed behaviour

### 5.1 Interests

- An attendee chooses **interests** — topics such as cybersecurity, IT,
  investment and entrepreneurship, maritime navigation. The interest list is a
  dynamic `Category` of kind Interest, managed from the Control Panel.
- Interests are picked during registration and can be revisited from the
  attendee's profile.
- Interests feed the matchmaking (section 5.2).

### 5.2 Meet people like you

- The matchmaking suggests other attendees to a user, based on **shared
  interests** and **shared sessions** (FR-802).
- Each suggestion carries a **match score** and a short reason — for example,
  "two sessions in common, three shared interests" (mockup Screen 35).
- When a match score reaches **80% or more**, the system sends the user a
  **session recommendation** and a **push notification** (FR-803). The
  notification is raised as an event for the Notifications feature to deliver.
- A suggestion is acted on by opening the other attendee's profile and, from
  there, sending a one-to-one meeting request (section 5.3).

### 5.3 One-to-one meetings

- An attendee can send a **one-to-one meeting request** to another attendee
  (`UC-16`): the request carries the other attendee and a topic.
- The request is routed to the **PR team** for approval (FR-804); a user
  holding the One-to-one Meetings page with the Approve and Reject actions
  decides it.
- A request moves through `Pending` → `Approved` or `Declined`. On approval the
  requester is notified; the confirmed meeting appears in both attendees' areas.

### 5.4 The AI assistant

- The AI assistant is a conversational helper that answers attendee questions
  about the forum — the agenda, the venue, sessions and the like (FR-805,
  mockup Screen 36).
- It is backed by a **FAQ knowledge base organised in two levels** (decision
  D5, FR-806): **FAQ groups** at level one — for example, questions about a
  booth, about events, about the launch — and **FAQ entries** within each group
  at level two. A user holding the FAQ & AI Assistant page (the Technical team
  in the suggested configuration) manages both levels from the Control Panel.
- The grouping lets the assistant search and answer more accurately: it narrows
  to the relevant group, then to the entry.
- The assistant offers quick-reply suggestions and can deep-link into other
  parts of the app — the map, a session, a booth.
- The assistant runs through the cognitive-AI abstraction; the provider is
  deferred (decision D7).

### 5.5 The AI session summary

- For a session, the system produces an **AI-generated summary** (FR-708,
  mockup Screen 34): the key points, the recommendations, and a link to the
  full transcript.
- The summary is generated through the cognitive-AI abstraction and stored as a
  `SessionSummary` against the session.
- The attendee can read, save and share a summary, and pick a different session
  to summarise.

### 5.6 Accessibility AI

- The system provides accessibility aids (FR-807): **sign-language
  conversion**, **speech-to-text and live captions**, and speech conversion for
  attendees with hearing or vision needs.
- These run through the cognitive-AI abstraction and are turned on from the
  attendee's accessibility settings (mockup Screen 38).

### 5.7 The AI provider and settings

- All AI capabilities — the assistant, the session summary, translation and
  captions, the comment filter used by the Engagement feature — go through one
  cognitive-AI abstraction. The provider is **not yet chosen** (decision D7) and
  is set by configuration; choosing or changing it is one adapter, not a
  rewrite.
- A user holding the AI Settings page (the Technical team) configures the
  cognitive-AI behaviour from the Control Panel.

## 6. Data

The feature uses these entities from SIMF-DAT-001 section 5.7 and 5.10:
`Interest` (a `Category`), `UserInterest`, `MeetingRequest`, `MatchSuggestion`,
`FaqGroup`, `FaqEntry`, `AiSetting`, `SessionSummary`. It reads `User`,
`Session` and `Category`.

Whether `MatchSuggestion` is stored or computed on demand is open in
SIMF-DAT-001 (its OI-2); this feature treats a match as derived from interests
and sessions and does not depend on the storage choice.

## 7. User interface

| Surface | Screens |
|---------|---------|
| Mobile app | Screen 35 Meet people like you, Screen 34 AI session summary, Screen 36 AI assistant, Screen 38 Accessibility settings; the interests step in registration; the meeting-request action on a profile |
| Control Panel | FAQ & AI Assistant (the two-level FAQ), AI Settings, One-to-one Meetings (the approval queue), and Content & Categories for the interest list |

Mobile visuals are the external designer's; Control Panel screens follow
SIMF-CPD-001. All content is held in Arabic and English; loading and error
states are present; the assistant shows a clear state while it is answering.

## 8. Validation rules

| Item | Rule |
|------|------|
| Interests | At least one interest may be required at registration — to confirm |
| Meeting request topic | Required |
| Meeting request target | An existing, Approved attendee, not the requester |
| Meeting request decision | A reason is recorded on a decline |
| FAQ group | A title in Arabic and English |
| FAQ entry | A question and an answer in Arabic and English; belongs to a group |
| Match recommendation | Triggered at a score of 80% or above |

## 9. Security and privacy considerations

- Matchmaking uses an attendee's interests and sessions; a suggestion shows
  only what the other attendee's profile already exposes.
- A meeting request is between two consenting attendees, mediated by the PR
  team's approval; an attendee is not contacted without that approval.
- The cognitive-AI provider is given only the data a capability needs — the FAQ
  knowledge for the assistant, the session content for a summary, the text for
  a filter or translation — and never more (SIMF-SAD-001 section 9.2).
- The AI provider is deferred; an on-premises or sovereign option may be
  required for a Ministry of Defense system, which is part of decision D7.
- Meeting decisions and FAQ and AI-settings changes are written to the
  operation log.

## 10. Acceptance criteria

1. An attendee can choose interests; the interest list is managed as a dynamic
   category.
2. The matchmaking suggests attendees with a match score and a reason based on
   shared interests and sessions.
3. A match of 80% or more raises a session recommendation and a push
   notification.
4. An attendee can send a one-to-one meeting request; it routes to the PR team,
   which approves or declines it; the requester is notified.
5. The AI assistant answers forum questions using the two-level FAQ; the FAQ
   groups and entries are managed from the Control Panel.
6. An AI session summary is produced and an attendee can read, save and share
   it.
7. The accessibility AI aids can be turned on and work.
8. Every AI capability runs through the abstraction; the provider is a
   configuration value.
9. All screens render in Arabic (RTL) and English (LTR); no hardcoded text.
10. The build is clean and the feature has unit, integration and end-to-end
    tests that pass.

## 11. Test scenarios

| # | Scenario | Expected |
|---|----------|----------|
| T-01 | Choose interests at registration | interests saved against the user |
| T-02 | View match suggestions | attendees suggested with a score and a reason |
| T-03 | A match reaches 80% | a recommendation and a push event are raised |
| T-04 | Send a one-to-one meeting request | request `Pending`, routed to PR |
| T-05 | PR approves a meeting request | request `Approved`; requester notified; meeting shown to both |
| T-06 | PR declines a meeting request | request `Declined`; reason recorded |
| T-07 | Ask the AI assistant a forum question | an answer drawn from the relevant FAQ group/entry |
| T-08 | Manage the two-level FAQ in the Control Panel | groups and entries saved in both languages |
| T-09 | Generate an AI session summary | summary stored; attendee can read, save, share |
| T-10 | Turn on a captions / sign-language aid | the accessibility aid works |
| T-11 | Confirm the AI provider is a configuration value | no code change to switch the provider |
| T-12 | Render the networking and AI screens in Arabic and English | correct layout and direction; no hardcoded text |

## 12. Open items

| ID | Item | Affects |
|----|------|---------|
| OI-1 | Confirm the cognitive-AI provider, including any on-premises / sovereign requirement (decision D7) | Sections 5.4–5.7 |
| OI-2 | Confirm whether `MatchSuggestion` is stored or computed on demand (SIMF-DAT-001 OI-2) | Section 6 |
| OI-3 | Confirm whether choosing at least one interest is mandatory at registration | Section 8 |
| OI-4 | Confirm the matchmaking inputs and the exact score formula with the client | Section 5.2 |
| OI-5 | Confirm document classification with the owner | Control block |

---

End of document.
