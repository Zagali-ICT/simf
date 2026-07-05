# Share my contact — شارك جهة اتصالي (`shareMyContact`, FDS-014)

- **Route:** `/contacts/share` (`RouteNames.shareMyContact`). Access:
  **Visitor (approved)** — auth-gated.
- **API:** `GET /app/account/share-token` (minted on first call) +
  `POST …/share-token` (rotate) + `GET /app/account/contact-card.vcf` (the vCard
  encoded in the QR + shared via the OS sheet).
- **Figma:** owner-supplied **1701:6062** (FDS-014 §5.4–5.5, built D-286/D-324).
  **Clean-code freeze:** D-645 (2026-07-04).

## Purpose

Renders the caller's dedicated share token as a QR another visitor can scan —
the QR encodes the vCard (Arabic name + phones, D-470) so any phone camera can
add the contact. The visitor can **rotate** the token (the old code stops
resolving) or hand off the vCard via the OS share sheet. The share token is
separate from the entry `QrId`, so scanning at the gate never harvests the card.

## Structure

| File | Holds |
|------|-------|
| `contacts/share_my_contact_screen.dart` (232) | `ShareMyContactScreen` (`ConsumerStatefulWidget`) — the load (token + vCard), rotate (confirm → swap → toast) and share-vCard glue, plus the QR card + hint + share/rotate actions; the small `_ErrorState` private. |

## Clean-code freeze (D-645)

The screen was **already clean** — 232 lines, cohesive, fully tokenised (no raw
`Color(0x..)`), using the shared `SimfBackButton` + `SimfConfirmDialog`. So this
freeze is the **render-lock golden only** (no code change).

**`_ErrorState` kept local (not `SimfErrorState`):** the local state renders its
message with the **theme-default** text colour, whereas `SimfErrorState`
hardcodes white. Swapping is a single-use dedup whose exact render can't be
proven identical without also capturing the error state, so — to preserve the
render — the local private stays. (The scaffold is the dark-theme navy default;
the QR sits on a light `SimfTokens.surface` card.)

## L4 parity (frame 1701:6062)

Captured `share_my_contact_1701-6062.png` (@375×812, ar, loaded state) and
**read it** — the شارك جهة اتصالي AppBar, the white vCard QR card on navy, the
muted scan hint, the gold مشاركة جهة اتصال share action, the تحوير الرمز rotate
action. RTL, no tofu. The QR renders (qr_flutter rasterises in goldens, unlike
`Image.asset`).

## Level-F

- **Share (.vcf)** — fetch the My-Area vCard → OS share sheet.
- **Rotate code** — confirm dialog → `rotateShareToken` → swap QR + toast.
- **Retry** — re-fetch on load failure.
- **Back** — shared `SimfBackButton`.

## Tests

`test/golden/share_my_contact_golden_test.dart` (frame 1701:6062, @375×812, ar)
+ `test/features/contacts/share_my_contact_screen_test.dart` (QR-from-vCard /
load-error+retry / rotate confirm+cancel). E2E: `docs/tests/e2e/mobile-my-contacts.md`.

## Related decisions

- **D-645** (this clean-code freeze — render-lock golden, `_ErrorState` kept
  local; no code change).
- **D-286 / D-324** (built, FDS-014), **D-470** (QR encodes the vCard).
