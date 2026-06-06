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
| Status | **No API** (client-local only); **Flutter screen BUILT** |

## API (authoritative contract)
**None.** This screen has no backend contract — it is a client-local settings
panel. The selections live only in widget state.

## Behaviour
A settings panel with:
- An intro line explaining the panel.
- A **text-size** choice (three `ChoiceChip`s — Small / Default / Large), local state.
- A **high-contrast** `SwitchListTile`, local state.
- A **reduce-motion** `SwitchListTile`, local state.

**State is LOCAL only.** Persistence (saving the choices) and app-wide
application (actually scaling text / swapping the theme / disabling animations
across the app) are **DEFERRED** to a later settings-store pass — the controls
behave, but nothing is saved or applied yet. A short note on the screen states
this. UI is interim (final visuals from SIMF-VID-001).

## Tests
- Widget: `src/Mobile/simf_app/test/features/accessibility/accessibility_screen_test.dart`
  (renders the three controls; toggling the high-contrast switch flips it).
- API: none (no backend contract).
- E2E: [`docs/tests/e2e/mobile-accessibility.md`](../../tests/e2e/mobile-accessibility.md).
