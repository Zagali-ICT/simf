# E2E test catalogue — `Sign up — type` (`signUpType`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). Mobile screen #4 — the
> account-type gate. Spec: [`Page_004`](../../App/Page_004/README.md). Runner-agnostic
> Gherkin. The screen glue is widget-tested in
> `src/Mobile/simf_app/test/features/auth/sign_up_type_screen_test.dart`.

| | |
|--|--|
| **Page** | [`Page_004`](../../App/Page_004/README.md) (App page docs) |
| **Route** | app screen #4 `signUpType` → `/sign-up/type` |
| **APIs** | **None** — client-only UI gate (no request on entry, selection, or Continue) |
| **Surface** | Mobile (Flutter) — Guest (reachable before any account exists) |
| **Auth setup** | None. No token, no `Authorization` header; behaves identically offline. |
| **Last reviewed** | 2026-06-03 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB004-001 | Visitor is the only selectable type; Continue disabled until it is selected | happy | P0 | authored ✓ (widget test) |
| E2E-MOB004-002 | Select Visitor → Continue → sign-up form with `type=visitor` | happy | P0 | authored ✓ (widget test) |
| E2E-MOB004-003 | Tapping Exhibitor/Sponsor shows the CP-only note and never selects / never enables Continue | edge | P0 | authored ✓ (widget test) |
| E2E-MOB004-004 | **No backend request** is issued by this screen (works offline) | resilience | P0 | authored (no API by construction) |
| E2E-MOB004-005 | "Have an account? Sign in" leaves the sign-up flow → sign-in | happy | P1 | authored ✓ (widget test) |
| E2E-MOB004-006 | Back returns to the previous screen; selection is discarded | edge | P1 | authored (AppBar back / pushed route) |
| E2E-MOB004-007 | RTL render (Arabic) — tiles, note, buttons mirror | i18n | P1 | authored (screen) |

## Scenarios

### E2E-MOB004-001 — Visitor-only gate

```gherkin
Feature: Account-type gate
Scenario: Only Visitor is selectable
  Given a guest opens the sign-up type screen
  Then "Visitor" is enabled with a helper line
  And "Exhibitor" and "Sponsor" are shown disabled with a Control-Panel-only note
  And the Continue button is disabled
```

**Evidence:** `sign_up_type_screen_test` — "initial: the three types render and Continue is disabled".

### E2E-MOB004-002 — Visitor → form carrying the type

```gherkin
Scenario: Continue forwards type=visitor
  Given the guest taps "Visitor"
  Then Continue becomes enabled
  When they tap Continue
  Then the app navigates to the sign-up form (Page 005)
  And the navigation carries type=visitor (no API call is made)
```

**Evidence:** `sign_up_type_screen_test` — "selecting Visitor enables Continue and routes to the form carrying type=visitor".

### E2E-MOB004-003 — Disabled types explain themselves

```gherkin
Scenario: Exhibitor/Sponsor are CP-only
  When the guest taps "Exhibitor" (or "Sponsor")
  Then a note explains exhibitor & sponsor accounts are managed from the Control Panel
  And nothing is selected and Continue stays disabled
```

**Evidence:** `sign_up_type_screen_test` — "tapping a disabled type shows the CP-only note and never enables Continue".

### E2E-MOB004-004 — No network

```gherkin
Scenario: The screen is fully offline-capable
  Given the device has no network
  When the guest selects Visitor and taps Continue
  Then the screen behaves identically — it issues no request at any point
```

> By construction: the type list is a static in-code constant; the only effect of
> Continue is in-app navigation carrying `type=visitor`. The first backend call of
> the sign-up flow happens on Page 005, not here (Page_004_API.md).

### E2E-MOB004-005 — Leave to sign-in

```gherkin
Scenario: Returning user bails out
  When the guest taps "Have an account? Sign in"
  Then the app navigates to the sign-in screen (Page 003)
```

**Evidence:** `sign_up_type_screen_test` — "the Sign in link leaves the sign-up flow".

### E2E-MOB004-006 — Back discards the selection

```gherkin
Scenario: Back is non-destructive
  Given the guest selected Visitor
  When they tap the app-bar Back
  Then they return to the previous screen (sign-in / welcome)
  And re-entering the type screen shows no selection (Continue disabled again)
```

---

_Last reviewed:_ `2026-06-03` by `SIMF Team`.
