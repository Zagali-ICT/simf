# E2E — Mobile · Join-a-session hub (D-485)

| Field | Value |
|-------|-------|
| **Route** | `joinSessionHub` — `/sessions/join` (approved Visitor) |
| **Source** | [`join_session_hub_screen.dart`](../../../src/Mobile/simf_app/lib/features/sessions/join_session_hub_screen.dart) |
| **API** | `GET /app/programme/sessions` (reused — no new endpoint) |
| **Reached from** | My Area → **Book a seat** row (the standalone "both" entry; the other is the Join CTA on each session page) |

The hub lists the programme sessions; tapping one opens its detail page, where the
**Select my seat / Join** CTA lives. It is the discoverable entry into the join
flow for an attendee who has not yet opened a specific session.

## Coverage matrix

| ID | Scenario | Type | Pri | Status |
|----|----------|------|-----|--------|
| E2E-MOBHUB-001 | The hub lists every session (title + time · hall) under the "Choose a session to join" hint | happy | P1 | authored ✓ (widget — list renders Opening/Closing) |
| E2E-MOBHUB-002 | Tapping a session row opens its detail page (`sessionDetail`), where the Join CTA lives | happy | P0 | authored ✓ (widget — tap → DETAIL s1) |
| E2E-MOBHUB-003 | Empty programme → the "No sessions" empty state | edge | P2 | authored ✓ (widget — empty list) |
| E2E-MOBHUB-004 | A load failure → error + Retry, which re-fetches | error | P2 | authored ✓ (provider error → KsaErrorState) |
| E2E-MOBHUB-005 | Approved-only — reached from My Area; the route auth gate (110) sends a signed-out user to sign-in | auth | P2 | covered (router gate 110) |
| E2E-MOBHUB-006 | **Pull-to-refresh** (list / empty / error) re-fetches the programme (D-601 — the gesture works on every state) | happy | P2 | covered (screen — `SimfPullToRefresh` on all three states) |
| E2E-MOBHUB-007 | Under **RTL** each row's forward chevron points **left** (the stroked `ic_back.svg` glyph; D-601 fixed the Material-icon double-mirror) | visual | P2 | covered (golden `join_session_hub.png`, crop-verified) |

## Scenarios

### E2E-MOBHUB-002 — Pick a session to join

```gherkin
Scenario: Browsing to a session from the hub
  Given an approved visitor on the Join-a-session hub
  And the programme has sessions "Opening" and "Closing"
  Then both are listed with their time · hall
  When they tap "Opening"
  Then its session detail page opens (where the Select-my-seat / Join CTA lives)
```

---

_Last reviewed:_ `2026-07-03` by `SIMF Team` (D-601 — pull-to-refresh + RTL chevron).
