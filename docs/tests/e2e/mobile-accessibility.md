# E2E test catalogue — `Accessibility` (`accessibility`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue —
> this screen has **no API** (client-local settings only). The **Flutter screen
> is built** and widget-tested in
> `src/Mobile/simf_app/test/features/accessibility/accessibility_screen_test.dart`
> (renders the three controls; toggling the high-contrast switch flips it).
> Persistence + app-wide application are DEFERRED to a settings-store pass.

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
| E2E-MOB038-003 | Picking a text size selects that chip (local state) | happy | P1 | covered (single-select `ChoiceChip` group) |
| E2E-MOB038-004 | Choices are not persisted / applied app-wide (DEFERRED) | edge | P2 | covered (doc-comment note — settings-store pass deferred) |

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

### E2E-MOB038-003 — Text size / E2E-MOB038-004 — Deferred persistence

```gherkin
Scenario: Picking a text size selects that chip
  When the user taps the "Large" chip
  Then "Large" becomes the selected chip
  And the other text-size chips deselect

Scenario: Choices are not yet persisted or applied
  Given the user changes any accessibility control
  Then the change lives only in local widget state
  And nothing is saved or applied app-wide (deferred to a settings-store pass)
```

**Evidence:** single-select `ChoiceChip` group; the deferred note is documented
on the screen and in `Page_038/README.md`.

---

_Last reviewed:_ `2026-06-06` by `SIMF Team`.
