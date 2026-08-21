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
| `contacts/my_contacts_screen.dart` (46) | `MyContactsScreen` (`ConsumerWidget`) — the Scaffold + AppBar with the QR-scan action, and nothing else; returning from the scanner invalidates `savedContactsProvider` so a save there shows up. |
| `contacts/widgets/my_contacts_body.dart` (`MyContactsBody`) | The `savedContactsProvider` loading / error / empty / list dispatch, the `SimfPullToRefresh` + `ListView.builder`, and the open-detail glue — a sheet that pops `true` toasts and invalidates the list. |
| `contacts/widgets/saved_contact_tile.dart` (`SavedContactTile`) | One saved-contact row (name / org·title subtitle / chevron). Named `Tile` to avoid clashing with the `SavedContactRow` data model it renders. |
| `contacts/widgets/saved_contact_sheet.dart` (`SavedContactSheet`) | The detail sheet — the shared `ContactCard` + Export-vCard / Remove, with the export + confirm-delete logic. |
| `contacts/widgets/contacts_empty_state.dart` (`ContactsEmptyState`) | The title + hint + scan-action empty state. |
| `contacts/widgets/error_state.dart` (`ErrorState`) | The message + retry error state, in the theme-default text colour. |

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
  share_my_contact D-645). Both are the screen's standard state surfaces. They no
  longer live in the screen file — they are `ContactsEmptyState` and `ErrorState`
  under `widgets/`, since no `_Private` widget class may live in a screen
  (`tool/conventions` SIMF-C3) — but the call itself stands: they are
  feature-local, not the shared `SimfEmptyState` / `SimfErrorState`.
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

### The sheet's buttons can no longer strand (fixed 2026-08-20)

Both of `SavedContactSheet`'s actions cleared `_busy` on the success path and
again inside `on ApiFailure`, with no `finally` — so anything thrown that is
**not** an `ApiFailure` left Export and Remove disabled for good, with no toast
and no way out but dismissing the sheet. The escape is real: `SimfApiClient`
converts only the **first** call's errors to `ApiFailure`, and the 401
token-refresh branch sits outside that guard, so a keystore/keychain
`PlatformException` (an OS keystore reset, a restored backup) surfaces raw
mid-action. Both now clear in a `finally`.

Remove adds one condition: the `finally` is skipped once the sheet has popped.
`mounted` does **not** stand in for "already gone" — `pop()` only reverses the
route's exit animation and the State outlives it — so re-enabling there would
flick the spinner back to the icon on a sheet the user can still see sliding
away.

## Tests

`test/golden/my_contacts_golden_test.dart` (render-lock, @375×812, ar) +
`test/features/contacts/my_contacts_screen_test.dart` (list / empty / error /
open-row-then-remove) +
`test/features/contacts/saved_contact_sheet_test.dart` (2 — a confirmed removal
leaves Remove disabled as the sheet exits; a failed removal re-enables it on the
sheet that stays). E2E: `docs/tests/e2e/mobile-my-contacts.md`.

## Related decisions

- **D-647** (this clean-code freeze — `SavedContactTile` + `SavedContactSheet`
  extraction, `SimfPullToRefresh` swap, render-lock golden).
- **D-286 / D-324** (built, FDS-014).
