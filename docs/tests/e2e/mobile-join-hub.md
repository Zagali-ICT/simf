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
| E2E-MOBHUB-008 | **The hub is actually reachable (BUG-016):** `/sessions/join` resolves to the hub, not to session detail with `sessionId="join"`. The route table declared `/sessions/:sessionId` (#17) above the static `/sessions/join` (#110) and go_router matches in declaration order, so the only entry point ("Book a seat" in My Area) landed on a "session not found" screen (`GET /app/programme/sessions/join` → 404) | nav | P0 | authored ✓ (app `router_route_order_test` — `/sessions/join` → `joinSessionHub`, `/sessions/<id>` → `sessionDetail`, plus the no-parameterised-route-before-a-static-one invariant) |
| E2E-MOBHUB-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOBHUB-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

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

### E2E-MOBHUB-008 — "Book a seat" actually opens the hub

```gherkin
Scenario: The Profile "Book a seat" row reaches the hub
  Given an approved visitor on the Profile tab
  When they tap the "Book a seat" row
  Then the location is /sessions/join
  And the join-a-session hub is rendered (the programme list)
  And NOT the session-detail screen with sessionId "join"
  And no GET /app/programme/sessions/join request is made

Scenario: A real session id still opens the detail screen
  When the app navigates to /sessions/3f1c9a2e-8d64-4a51-9c7b-0e2f5a6b7c8d
  Then the session-detail screen is rendered
```

> go_router matches routes in **declaration order** and keeps the first hit. The
> table declared `/sessions/:sessionId` above `/sessions/join`, so the dynamic
> route swallowed the static one. `buildRoutes()` now emits every static path
> before the parameterised ones, so the shadowing cannot come back when a route
> is added anywhere in the table.

**Evidence:** `test/app/router_route_order_test.dart`.

---

_Last reviewed:_ `2026-07-26` by `SIMF Team` (BUG-016 — the hub was unreachable:
the parameterised session route shadowed `/sessions/join`; the flat route table
is now emitted static-first — E2E-MOBHUB-008). _Prior:_ `2026-07-03` (D-601 —
pull-to-refresh + RTL chevron).
