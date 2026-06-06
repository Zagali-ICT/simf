# Page 038 — إمكانية الوصول · Accessibility

Per-page documentation folder (App screen 38).

## Identity
| | |
|---|---|
| Mockup page | **38** (`Mockup.html`) |
| Route | `RouteNames.accessibility` → `/settings/accessibility` (**public, anonymous**) |
| Titles | AR **إمكانية الوصول** · EN **Accessibility** |
| Section | 5 — Settings |
| Nature | **Client-local accessibility settings** — text size, high contrast, reduce motion |
| App privilege | **Public (anonymous).** No API; nothing is read or written server-side. |
| Status | **No API** (client-local only); **Flutter screen BUILT** — choices **persisted to prefs + applied app-wide** (text scale / high-contrast theme / reduce-motion), D-327 |

## API (authoritative contract)
**None.** This screen has no backend contract — it is a client-local settings
panel. The selections live only in widget state.

## Behaviour
A settings panel with:
- An intro line explaining the panel.
- A **text-size** choice (three `ChoiceChip`s — Small / Default / Large).
- A **high-contrast** `SwitchListTile`.
- A **reduce-motion** `SwitchListTile`.

**Persisted + applied (D-327).** Each control reads/writes the prefs-backed
`AccessibilityController`, so the choice survives a restart, and the settings are
applied **app-wide** from `app/app.dart`: a `MaterialApp.router` `builder:`
injects `MediaQuery.textScaler` (0.85 / 1.0 / 1.15) and `disableAnimations`, and
high-contrast swaps the theme to `SimfTheme.highContrastLight()` /
`highContrastDark()`. No API, nothing server-side. UI is interim (final visuals
from SIMF-VID-001).

## Tests
- Widget: `src/Mobile/simf_app/test/features/accessibility/accessibility_screen_test.dart`
  (renders the three controls; toggling high-contrast flips + persists; picking a
  text size persists the choice).
- Unit: `src/Mobile/simf_app/test/features/accessibility/accessibility_controller_test.dart`
  (defaults; reads persisted values; invalid-index fallback; each setter persists;
  scaleFactor map).
- Integration: `src/Mobile/simf_app/integration_test/app_flows_test.dart`
  (changing text size + high-contrast rebuilds the assembled app cleanly).
- API: none (no backend contract).
- E2E: [`docs/tests/e2e/mobile-accessibility.md`](../../tests/e2e/mobile-accessibility.md).
