# E2E test catalogue — `Accessibility` (`accessibility`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue —
> this screen has **no API** (client-local settings only). The **Flutter screen
> is built** and tested in
> `src/Mobile/simf_app/test/features/accessibility/accessibility_screen_test.dart`
> + `accessibility_controller_test.dart`. The choices are **persisted** (prefs)
> and **applied app-wide** — text scaler, reduce-motion, and a high-contrast
> theme swap (D-327).

| | |
|--|--|
| **Page** | [`Page_038`](../../App/Page_038/README.md) |
| **Route** | app screen #38 `/settings/accessibility` (no API) |
| **Surface** | Mobile (Flutter) |
| **Auth setup** | **None** — the screen is public (anonymous). |
| **Last reviewed** | 2026-06-06 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB038-001 | The panel renders intro + text-size chips + the two switches | happy | P0 | authored ✓ (screen `renders the three controls`) |
| E2E-MOB038-002 | Toggling the high-contrast switch flips its value | happy | P0 | authored ✓ (screen `toggling the high-contrast switch flips it`) |
| E2E-MOB038-003 | Picking a text size selects that chip and persists | happy | P1 | authored ✓ (screen `picking a text size persists the choice`) |
| E2E-MOB038-004 | Choices are persisted to prefs and applied app-wide (scale / contrast / motion) | happy | P1 | authored ✓ (controller test persists; integration test rebuilds the app) |

## Scenarios

### E2E-MOB038-001 — The panel renders

```gherkin
Feature: Accessibility settings (client-local)
  As any user (signed out or in)
  I want accessibility preferences
  So that I can tune text size, contrast and motion

Scenario: The settings panel renders its controls
  When the user opens /settings/accessibility
  Then an intro line is shown
  And a text-size choice with Small, Default and Large is shown
  And a High contrast switch and a Reduce motion switch are shown
```

**Evidence:** screen test `renders the three controls`.

### E2E-MOB038-002 — High-contrast toggle

```gherkin
Scenario: The high-contrast switch toggles
  Given the High contrast switch is off
  When the user taps it
  Then the switch flips to on
```

**Evidence:** screen test `toggling the high-contrast switch flips it`.

### E2E-MOB038-003 — Text size persists / E2E-MOB038-004 — Applied app-wide

```gherkin
Scenario: Picking a text size selects that chip and persists
  When the user taps the "Large" chip
  Then "Large" becomes the selected chip
  And the other text-size chips deselect
  And the choice is written to prefs (accessibility_font_scale)

Scenario: Accessibility choices are applied across the whole app
  Given the user sets Large text and turns High contrast on
  Then the root MediaQuery text scaler becomes 1.15
  And the app theme swaps to the high-contrast variant
  And the choices survive an app restart (read back from prefs on boot)
```

**Evidence:** screen test `picking a text size persists the choice`; controller
test (read-on-boot + each setter persists); integration test (text size +
high-contrast rebuild the assembled app without crashing).

---

_Last reviewed:_ `2026-06-06` by `SIMF Team`.
