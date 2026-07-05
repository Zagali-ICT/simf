# Accessibility — إمكانية الوصول (Page 038, `#38`)

- **Route:** `/settings/accessibility` (`RouteNames.accessibility`). Access:
  **Guest+**. No API — all state is prefs-backed and applied app-wide.
- **Figma:** **1116:16630** (built D-314; persisted + applied app-wide D-327;
  screen-reader/captions wired D-465). **Clean-code freeze:** D-640 (2026-07-04).

## Purpose

Two grouped sections on the navy `SimfPageShell`:

- **العرض** — the حجم الخط card (four pill chips: صغير / متوسط / كبير / أكبر),
  the high-contrast switch, the reduce-motion switch.
- **الصوت والقراءة** — the screen-reader switch, the session-captions switch.

Every choice is **persisted** (`AccessibilityController`, prefs-backed) and
**applied app-wide**: the text scaler + reduce-motion ride the root MediaQuery,
high-contrast swaps the theme (`app/app.dart`), the screen-reader switch drives
the navigation announcer (`router.dart`) and fires an immediate
`SemanticsService` announcement on enable, and the captions switch gates the
live-broadcast caption strip.

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `accessibility_screen.dart` (89) | `AccessibilityScreen` (`ConsumerWidget`) — the shell + the two sections composing the widgets; the screen-reader `SemanticsService` announcement stays here (it needs the `BuildContext`). |
| `widgets/accessibility_font_size_card.dart` (`AccessibilityFontSizeCard` + `_SizeChip`) | The حجم الخط card + one gold/outline pill. |
| `widgets/accessibility_toggle_row.dart` (`AccessibilityToggleRow`) | One navy-deep labelled switch row (title + gold `Switch`, `hint` as a semantics hint). |
| `widgets/accessibility_section_heading.dart` (`AccessibilitySectionHeading`) | A section heading (`textLg`/`w600`). |

## Clean-code freeze (D-640)

- The four inline widget classes (`_FontSizeCard`/`_SizeChip`, `_ToggleRow`,
  `_SectionHeading`) moved to `widgets/` verbatim, leaving an 89-line screen.
- The font-size card label and the toggle-row title were the **same** raw
  `TextStyle` (white / `textMd` / `w500`). Per the tokens rule (*"a widget never
  constructs a raw TextStyle"*) that twin became **`SimfTokens.labelWhiteMedium`**
  — the white sibling of the existing `labelBeigeMedium`, byte-identical to the
  two inline copies — and both widgets now reference it.
- `AccessibilitySectionHeading` is **kept screen-local, not `SimfSectionHeader`**
  — the section heading is `textLg`/`w600`, heavier than the shared header's
  `w500`, so swapping would change the render.
- Already fully tokenised (no raw `Color(0x..)`); every file ≤400 lines.

## L4 Figma parity (frame 1116:16630)

Captured `accessibility_1116-16630.png` (@375×812, ar, default prefs) and **read
it** — the إمكانية الوصول header, the العرض heading, the حجم الخط card with four
RTL pills (the default متوسط filled gold), the تباين عالي / تقليل الحركة toggles
(off), the الصوت والقراءة heading, the قارئ الشاشة toggle (off) and the
الترجمة النصية captions toggle (on/gold by default). RTL, no tofu. The widget
extraction + token swap are byte-identical, so this golden locks the D-465 parity.

## Level-F

- **Font-size chips** — set the app text scale (persisted).
- **High-contrast / reduce-motion / screen-reader / captions** switches — each
  persists + applies app-wide (theme / MediaQuery / announcer / caption strip).
- **Back** — `backOrHome`.

No SIMF API — the screen is settings-only.

## Tests

`test/golden/accessibility_golden_test.dart` (frame 1116:16630, @375×812, ar) +
`test/features/accessibility/accessibility_screen_test.dart` (renders + each
toggle persists) + `accessibility_controller_test.dart`. E2E:
`docs/tests/e2e/mobile-accessibility.md`.

## Related decisions

- **D-640** (this clean-code freeze — 4-widget extraction + `labelWhiteMedium`
  token + first golden).
- **D-314** (built), **D-327** (persisted + applied app-wide), **D-465** (Figma
  1116:16630 + screen-reader/captions wired).
