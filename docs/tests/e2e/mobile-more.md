# E2E test catalogue — `More` (`more`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> More screen is a **navigation hub with no API** (a list of tiles routing to the
> already-built secondary screens, plus a static app-version line). The **Flutter
> screen is built** and widget-tested in
> `src/Mobile/simf_app/test/features/more/more_screen_test.dart` (renders the
> tiles, version line, tap About → navigates).

| | |
|--|--|
| **Page** | [`Page_041`](../../App/Page_041/README.md) |
| **Route** | app screen #41 `/more` (no API) |
| **Surface** | Mobile (Flutter) |
| **Auth setup** | **None** — the hub is anonymous; the destination routes keep their own auth gate. |
| **Last reviewed** | 2026-06-06 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB041-001 | Guest opens More → sees the six tiles + version line | happy | P0 | authored ✓ (screen `renders the navigation tiles`, `shows the app-version line`) |
| E2E-MOB041-002 | Tapping About routes to the About screen | happy | P0 | authored ✓ (screen `tapping About navigates to the About route`) |
| E2E-MOB041-003 | Tapping a gated tile (Notifications) while signed-out bounces to sign-in | edge | P1 | covered (router auth gate, destination #33; `redirectDecision` test) |

## Scenarios

### E2E-MOB041-001 — More hub renders

```gherkin
Feature: More (navigation hub)
  As a guest (signed out)
  I want a hub of secondary screens
  So that I can reach About, Accessibility, Terms, Rate, Notifications and Media partners

Scenario: The More screen lists every tile and the version
  When the guest opens /more
  Then a tile is shown for About, Accessibility, Terms, Rate, Notifications and Media partners
  And each tile shows a leading icon and a trailing chevron
  And a static "SIMF v0.1.0" line is shown at the bottom
```

**Evidence:** screen tests `renders the navigation tiles`, `shows the app-version line`.

### E2E-MOB041-002 — Tile navigation

```gherkin
Scenario: Tapping a tile routes to its screen
  When the guest taps the "About the forum" tile
  Then the app navigates to the About route (/about)
```

**Evidence:** screen test `tapping About navigates to the About route`.

### E2E-MOB041-003 — Gated destination

```gherkin
Scenario: A gated destination still enforces auth
  Given the guest is signed out
  When the guest taps the "Notifications" tile
  Then the router redirects to /sign-in
```

**Evidence:** the More tile only navigates; the auth gate lives on the
destination route (#33 Notifications, #40 Rate) — covered by the router
`redirectDecision` / `routePathRequiresAuth` tests.

---

_Last reviewed:_ `2026-06-06` by `SIMF Team`.
