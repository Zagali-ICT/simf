# E2E test catalogue — `Guest mode` (`guest-mode`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — Page
> 012 is an **informational entry screen with no API**: a guest reads what they can
> browse vs. what needs an account, then either continues into the app or signs in.
> The **Flutter screen is built** and widget-tested in
> `src/Mobile/simf_app/test/features/guest/guest_mode_screen_test.dart` (renders
> the headline + both actions, primary → home, secondary → sign-in). No endpoint
> backs this page.

| | |
|--|--|
| **Page** | [`Page_012`](../../App/Page_012/README.md) |
| **Route** | app screen #12 `/guest` (no API) |
| **Surface** | Mobile (Flutter) |
| **Auth setup** | **None** — public/anonymous; the screen renders without a token. |
| **Last reviewed** | 2026-06-06 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB012-001 | Guest opens the screen → headline + browse/account body + both actions | happy | P0 | authored ✓ (screen `renders the headline + both actions`) |
| E2E-MOB012-002 | **Continue as guest** → home | happy | P0 | authored ✓ (screen `primary button continues to home`) |
| E2E-MOB012-003 | **Sign in** → the sign-in screen | happy | P0 | authored ✓ (screen `secondary button routes to sign-in`) |
| E2E-MOB012-004 | Arabic locale renders the RTL copy + titles | i18n | P1 | covered (bilingual `_t(ar,en)` getters; AR is the default locale) |
| E2E-MOB012-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOB012-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-MOB012-001 — Guest opens the screen

```gherkin
Feature: Guest mode (entry)
  As a guest (signed out)
  I want to understand guest browsing
  So that I can decide whether to continue or sign in

Scenario: The screen explains guest mode and offers both actions
  Given the app is on /guest with no token
  Then the headline "Browsing as guest" is shown
  And a body explains a guest can browse sessions, speakers, the map and media
  And a body explains the badge, personal notifications and booking need sign-in
  And a primary "Continue as guest" and a secondary "Sign in" action are shown
```

**Evidence:** screen test `renders the headline + both actions`.

### E2E-MOB012-002 — Continue as guest / E2E-MOB012-003 — Sign in

```gherkin
Scenario: Continue as guest enters the app
  When the guest taps "Continue as guest"
  Then the app navigates to home (/)

Scenario: Sign in opens the sign-in screen
  When the guest taps "Sign in"
  Then the app pushes the sign-in screen (Page_003)
```

**Evidence:** screen tests `primary button continues to home`,
`secondary button routes to sign-in`.

### E2E-MOB012-004 — Arabic / RTL

```gherkin
Scenario: Arabic locale renders the RTL copy
  Given the device locale is Arabic
  Then the title reads "وضع الضيف" and the actions read their Arabic labels
```

**Evidence:** bilingual `_t(ar,en)` getters (AR is the default locale).

---

_Last reviewed:_ `2026-06-06` by `SIMF Team`.
