# Accessibility — إمكانية الوصول (Page 038, `#38`)

- **Route:** `/settings/accessibility` (`RouteNames.accessibility`). Access:
  **Guest+**. State is prefs-backed and applied app-wide; for a **signed-in**
  account it is also synced to the server (`accessibility-server-sync`, below).
- **API:** `GET` / `PUT /api/v1/app/account/preferences` — **live since
  2026-07-31**; `RequireApprovedAccount`, so a guest or a merely-verified account
  is 401/403 and the screen silently stays local-only. Best-effort in both
  directions — the screen never blocks on either call. Endpoint reference:
  [`api/account-preferences.md`](../../api/account-preferences.md).
- **Figma:** **1116:16630** (built D-314; persisted + applied app-wide D-327;
  screen-reader/captions wired D-465; server-synced 2026-07-30).
  **Clean-code freeze:** D-640 (2026-07-04).

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

## Server sync (`accessibility-server-sync`, 2026-07-30)

The five flags were device prefs **only**, so they did not follow the user to a
second device and did not survive a reinstall. They are now account settings:

- **Write-through.** Every setter pushes the whole `AccessibilitySettings` to
  `PUT /app/account/preferences` (`AccessibilityPreferencesRepository`).
  `textSize` travels as the stable enum **name**, never an index.
- **Hydrate at sign-in.** `AccessibilitySync.hydrate()` runs from
  `routeAfterAuth` — the one seam every sign-in path (password, 2FA completion,
  badge password) already goes through — and replays the account copy onto the
  device, writing prefs too so the next cold start reads it instantly.
- **Prefs stay the offline cache and the only READ path**, so the app renders
  the right scale on the first frame, offline, before any network call.
- **Both directions swallow their failures by contract.** A sync failure must
  never disturb the choice the user just made, and must never fail a sign-in;
  the same rule `OrgProfileController.warm()` already follows. Against an older
  API — or on a guest / not-yet-approved account — the screen degrades to exactly
  the pre-change behaviour.
- **The server half shipped 2026-07-31**, closing the `accessibility-server-sync`
  register item. The five values live on **additive `UserProfile` columns**
  (`AccessibilityTextSize` `nvarchar(16)` + four flags), so they sit in `SIMF_App`
  beside the bare `UserId` and never cross into `SIMF_Identity` (D-157). Two
  `HasSentinel` calls (`normal`, `true`) are load-bearing: without them a first
  save that turns **captions off** would be omitted from the `INSERT` and come
  back **on** from the column default. An unknown `textSize` is a bilingual 400
  `VALIDATION_FAILED` on field `textSize` rather than a silent coercion; a
  never-saved account reads the defaults rather than a 404.

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `data/accessibility_preferences_repository.dart` | `AccessibilityPreferencesRepository` — the `GET`/`PUT /app/account/preferences` pair + the tolerant `decode` (every field optional, defaults to the shipped value). |
| `data/accessibility_controller.dart` | `AccessibilityController` (prefs read/write + write-through) + `AccessibilitySync` (hydrate at sign-in). |
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

Every control is applied locally **and** written through to
`PUT /app/account/preferences` for a signed-in approved account (see *Server
sync* above). Nothing on the screen blocks on the network.

## Tests

`test/golden/accessibility_golden_test.dart` (frame 1116:16630, @375×812, ar) +
`test/features/accessibility/accessibility_screen_test.dart` (renders + each
toggle persists) + `accessibility_controller_test.dart` +
`accessibility_server_sync_test.dart` (write-through, hydrate, the two
swallow-the-failure cases, wire decode). Server half:
`tests/SIMF.Api.Tests/AccountPreferencesTests.cs` (8 facts). E2E:
[`mobile-accessibility.md`](../../../tests/e2e/mobile-accessibility.md)
(E2E-MOB038-001..012) + [`api-account-preferences.md`](../../../tests/e2e/api-account-preferences.md)
(E2E-ACP-001..013).

## Related decisions

- **D-640** (this clean-code freeze — 4-widget extraction + `labelWhiteMedium`
  token + first golden).
- **D-314** (built), **D-327** (persisted + applied app-wide), **D-465** (Figma
  1116:16630 + screen-reader/captions wired).
