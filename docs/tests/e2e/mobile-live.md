# E2E test catalogue — `Live broadcast` (`live`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> live read is already built + anonymous (D-271; additive `LiveStreamUrl` +
> `LiveSignLanguageUrl` on the session detail). The **Flutter screen is built**
> and widget-tested in
> `src/Mobile/simf_app/test/features/live/live_broadcast_screen_test.dart`
> (no-id empty, blank-id empty, not-live, recording note, sign-language note,
> 404, error→retry, Arabic). It reuses the public session detail wire contract
> via a small `LiveRepository` — no duplicate of the full detail model. The
> non-video paths are tested directly; real playback needs a device, so the
> video path is exercised manually only.

| | |
|--|--|
| **Page** | [`Page_025`](../../App/Page_025/README.md) |
| **Route** | `GET /api/v1/app/programme/sessions/{id}` · app screen #25 `/live?sessionId=` |
| **Surface** | Mobile (Flutter) + App API |
| **Auth setup** | **None** — the read is `AllowAnonymous` (a guest can watch). |
| **Last reviewed** | 2026-06-06 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB025-001 | No `sessionId` → "pick a session" empty state, no fetch | edge | P0 | authored ✓ (screen `no sessionId shows the pick-a-session empty state`) |
| E2E-MOB025-002 | A live stream URL → the player + LIVE badge | happy | P0 | manual (real playback needs a device — not widget-tested) |
| E2E-MOB025-003 | No stream + no recording → the not-live state | edge | P1 | authored ✓ (screen `no stream + no recording shows the not-live state`) |
| E2E-MOB025-004 | No stream but a recording → the recording note | edge | P1 | authored ✓ (screen `no stream but a recording shows the recording note`) |
| E2E-MOB025-005 | A sign-language URL → the sign-language note | edge | P2 | authored ✓ (screen `a sign-language url shows the sign-language note`) |
| E2E-MOB025-006 | Session 404 → not-found state | resilience | P1 | authored ✓ (screen `a 404 shows the not-found state`) |
| E2E-MOB025-007 | A read failure → error + Retry that re-fetches | resilience | P0 | authored ✓ (screen `a non-404 failure shows error + retry, which re-fetches`) |

## Scenarios

### E2E-MOB025-001 — No session selected

```gherkin
Feature: Live broadcast
  As a guest (signed out)
  I want a clear prompt when I open /live with no session
  So that I know to open a session first

Scenario: Opening /live without a sessionId
  Given the app opens /live with no sessionId query param
  Then the screen shows "No live session selected — open a session to watch."
  And no session detail read is issued
```

**Evidence:** screen test `no sessionId shows the pick-a-session empty state`
(+ `an empty sessionId is treated as no selection`).

### E2E-MOB025-002 — Live player

```gherkin
Scenario: A broadcasting session shows the player
  Given the session has a non-empty liveStreamUrl
  When the app calls GET /api/v1/app/programme/sessions/{id}
  Then a video player initialises at the stream's aspect ratio
  And a LIVE badge and a play/pause control are shown
```

**Evidence:** manual — real playback needs a platform; the widget tests cover
every non-video path instead.

### E2E-MOB025-003 — Not live / E2E-MOB025-004 — Recording / E2E-MOB025-005 — Sign language

```gherkin
Scenario: A session with no stream and no recording is off-air
  Given liveStreamUrl is null and hasRecording is false
  Then the screen shows "This session is not broadcasting right now."

Scenario: A session with a recording shows the recording note
  Given liveStreamUrl is null and hasRecording is true
  Then the screen shows "A recording of this session is available."

Scenario: A sign-language stream is announced
  Given liveSignLanguageUrl is non-empty
  Then the screen shows "Sign-language interpretation is available."
```

**Evidence:** screen tests `no stream + no recording shows the not-live state`,
`no stream but a recording shows the recording note`,
`a sign-language url shows the sign-language note`.

### E2E-MOB025-006 — Not found / E2E-MOB025-007 — Error+retry

```gherkin
Scenario: A missing session shows the not-found state
  Given GET /api/v1/app/programme/sessions/{id} returns 404
  Then the screen shows "This session was not found"

Scenario: A failed read offers a retry
  Given the session read fails (non-404)
  Then an error + Retry are shown, and Retry re-runs the read
```

**Evidence:** screen tests `a 404 shows the not-found state`,
`a non-404 failure shows error + retry, which re-fetches`.

---

_Last reviewed:_ `2026-06-06` by `SIMF Team`.
