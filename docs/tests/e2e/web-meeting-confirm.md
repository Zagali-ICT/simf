# E2E test catalogue — Speaker meeting confirm (`/meeting/confirm`)

| | |
|--|--|
| **Route** | `/meeting/confirm?token=…` |
| **Surface** | Public Website (SIMF.Web) — **anonymous** |
| **Auth setup** | None — the opaque single-use token in the query is the only credential |
| **Last reviewed** | 2026-07-09 (D-717 — item 7, FDS-013 §15.7 GAP-3) |

> **What this page does (grounded in `MeetingConfirm.razor`, D-717).**
> The public landing page for the speaker double-opt-in email links. After an admin
> accepts a speaker meeting request **and binds a hall slot** (Slice B → `AwaitingSpeaker`),
> the speaker is emailed **Approve** and **Reject** links, each pointing here with a
> distinct single-use, action-bound token. Opening the link **GET-previews** the pending
> decision (speaker sees who wants to meet, the topic, the time, the hall) **without
> consuming** the token — safe against email-scanner prefetch. Clicking **Confirm**
> **POSTs** and applies the decision (Approve → `Accepted` + the requester is notified
> "confirmed"; Reject → `Rejected`). A used / expired / unknown token, or a request that
> already left `AwaitingSpeaker`, shows the neutral **"This link is no longer valid"**
> card — never leaking which reason. Backed by
> `GET`/`POST /api/v1/app/meeting-actions/{token}` (`AllowAnonymous`, rate-limited) and
> covered by `tests/SIMF.Api.Tests/MeetingActionTokenTests.cs`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MAC-001 | Open an Approve link → preview shows the meeting; Confirm → "confirmed" + request Accepted | happy | P0 | authored ✓ (`Approve_confirms_the_meeting_and_marks_the_token_used`, API) |
| E2E-MAC-002 | Open a Reject link → Confirm → "declined" + request Rejected | happy | P0 | authored ✓ (`Reject_declines_the_meeting`, API) |
| E2E-MAC-003 | Opening the link (GET) does not consume the token; a second open still previews | edge | P0 | authored ✓ (`Preview_is_GET_safe_and_does_not_consume_the_token`, API) |
| E2E-MAC-004 | A used token (and its sibling) → neutral "no longer valid" | error | P0 | authored ✓ (`A_used_token_and_its_sibling_are_neutral_404s`, API) |
| E2E-MAC-005 | An unknown / malformed token → neutral "no longer valid" | error | P1 | authored ✓ (`An_unknown_token_is_a_neutral_404`, API) |
| E2E-MAC-006 | An expired token (>72h) → neutral "no longer valid" | error | P1 | authored ✓ (`An_expired_token_is_a_neutral_404`, API) |
| E2E-MAC-007 | RTL / Arabic render — the preview + confirm mirror | i18n | P1 | authored ✓ (browser) |
| E2E-MAC-008 | No `?token=` at all → neutral state, no API call | edge | P2 | authored ✓ (browser) |

## Scenarios

### E2E-MAC-001/002 — Approve / Reject a meeting from the email link

```gherkin
Feature: Speaker confirms a meeting over email
Background:
  Given an admin accepted a speaker meeting request and bound it to a hall slot
  And the request is AwaitingSpeaker
  And the speaker received the Approve/Reject email (two /meeting/confirm?token= links)

Scenario: Approve
  When the speaker opens the Approve link
  Then GET /api/v1/app/meeting-actions/{token} returns 200 with a preview
       (action=Approve, requester name, topic, slot, hall) — and the token is NOT consumed
  When the speaker clicks "Approve the meeting"
  Then POST /api/v1/app/meeting-actions/{token} returns 200 (outcome=Approve)
  And the page shows "Thank you — the meeting is confirmed."
  And the request status is Accepted, SpeakerDecisionAt is set, the token is used
  And the requester receives a MeetingRequestConfirmed notification

Scenario: Reject
  When the speaker opens the Reject link and clicks "Decline the meeting"
  Then the request status is Rejected and the page shows "Thank you — the meeting was declined."
```

### E2E-MAC-003/004/005/006 — Single-use + neutral errors

```gherkin
Scenario: A prefetch/preview does not burn the token
  When the Approve link is opened twice (as a mail scanner would prefetch)
  Then both GETs return 200 and the token stays unused

Scenario: Reuse / sibling / unknown / expired all show the same neutral page
  Given a token was already confirmed
  When it (or its sibling, or a random string, or an expired token) is opened/confirmed
  Then the API returns 404 MEETING_ACTION_TOKEN_INVALID
  And the page shows "This link is no longer valid." with no hint of the exact reason
```

### E2E-MAC-007 — RTL / Arabic render

```gherkin
Scenario: The preview + confirm mirror in Arabic
  Given a valid Approve token and the language switched to العربية
  When the speaker opens /meeting/confirm?token=…
  Then the card direction is RTL, the intro and the Requester/Topic/When (and Where, if bound)
       labels are Arabic, and the confirm button label is Arabic
  And no element overflows horizontally (scrollWidth == clientWidth)
```

### E2E-MAC-008 — No ?token= → neutral state, no API call

```gherkin
Scenario: Opening the page with no token
  When /meeting/confirm is opened with no ?token= (or a blank / whitespace token)
  Then the page makes NO request to /api/v1/app/meeting-actions/…
  And it shows the neutral "This link is no longer valid." card (Meeting.Confirm.Invalid)
  # grounded in MeetingConfirm.razor.cs — OnInitializedAsync skips the GET when Token is blank,
  # so _preview stays null and the razor renders the neutral error card (MeetingConfirm.razor:34-38)
```

---

_Last reviewed:_ 2026-07-09 by Claude — D-720 (item 7 DoD close — E2E-MAC-007/008 authored). Earlier: D-717 (item 7 Slice C, FDS-013 §15.7 GAP-3) new public token landing page.
