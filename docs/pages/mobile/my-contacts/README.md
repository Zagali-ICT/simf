# My contacts — جهات اتصالي (`myContacts`, FDS-014)

- **Route:** `/contacts` (`RouteNames.myContacts`). Access: **Visitor (approved)**
  — auth-gated.
- **API:** `GET /app/contacts` (resolved on read — no PII snapshot) +
  `DELETE /app/contacts/{id}` (soft-delete) + `GET /app/contacts/{id}/vcard`.
- **Figma:** none — interim UI (final visuals from SIMF-VID-001).
  **Clean-code freeze:** D-647 (2026-07-04).

## Purpose

Lists the cards the visitor saved. A row opens a detail sheet to **export** the
saved card as a vCard or **remove** it (confirm → soft-delete → reload + toast).
The app-bar scan action opens the scanner to add more.

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `contacts/my_contacts_screen.dart` (~200) | `MyContactsScreen` (`ConsumerStatefulWidget`) — the load / scanner / open-detail glue, the AppBar + scan action, the loading/error/empty/list dispatch, and the small local `_EmptyState` / `_ErrorState`. |
| `contacts/widgets/saved_contact_tile.dart` (`SavedContactTile`) | One saved-contact row (name / org·title subtitle / chevron). Named `Tile` to avoid clashing with the `SavedContactRow` data model it renders. |
| `contacts/widgets/saved_contact_sheet.dart` (`SavedContactSheet`) | The detail sheet — the shared `ContactCard` + Export-vCard / Remove, with the export + confirm-delete logic. |

## Clean-code freeze (D-647)

- `_SavedRow` → **`SavedContactTile`** and `_SavedContactSheet` → **`SavedContactSheet`**
  moved to `widgets/` verbatim (the sheet takes the export/remove logic with it),
  dropping the screen's now-unused `contact_card` / `simf_confirm_dialog` /
  `content_sharer` imports.
- The raw `RefreshIndicator` → the app-wide branded **`SimfPullToRefresh`**
  (D-520/D-532); the resting render is unchanged (the spinner colour only shows
  during a pull).
- **`_EmptyState` kept local, NOT `SimfEmptyState`** — it carries a **title + hint
  + a scan action button**, richer than the shared icon+message empty state.
  **`_ErrorState` kept local, NOT `SimfErrorState`** — it renders its message in
  the theme-default text colour, and swapping to the shared white-text state
  can't be proven identical from the loaded-state golden (same call as
  share_my_contact D-645). Both are the screen's standard state surfaces.
- Already fully tokenised; every file ≤400 lines.

## L4 render-lock (no Figma frame)

Captured `my_contacts.png` (@375×812, ar, two saved contacts) and **read it** —
the جهات اتصالي AppBar + QR-scan action, the two `SavedContactTile` rows (gold
account icon + name + org·title subtitle + chevron), on navy. RTL, no tofu. No
Figma frame is bound (interim UI), so this is a structural render-lock.

## Level-F

- **Row → detail sheet** — export vCard / remove (confirm → delete → reload +
  toast).
- **Scan action** — opens the scanner; a save there reloads the list.
- **Pull-to-refresh / Retry** — re-fetch `listSaved`.
- **Empty** — title + hint + scan action.
- **Back** — AppBar back.

## Tests

`test/golden/my_contacts_golden_test.dart` (render-lock, @375×812, ar) +
`test/features/contacts/my_contacts_screen_test.dart` (list / empty / error /
open-row-then-remove). E2E: `docs/tests/e2e/mobile-my-contacts.md`.

## Related decisions

- **D-647** (this clean-code freeze — `SavedContactTile` + `SavedContactSheet`
  extraction, `SimfPullToRefresh` swap, render-lock golden).
- **D-286 / D-324** (built, FDS-014).
