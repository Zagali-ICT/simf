# Terms & conditions — الشروط والأحكام (Page 009, `#9`)

- **Route:** `/terms` (`RouteNames.terms`). Access: **Guest+ (public)**.
- **API:** anonymous `GET /app/content/terms` (read-only). A 404 is the empty
  state; transport/5xx is the error state (both with retry).
- **Figma:** **505:1553** (built D-367, fidelity pass D-375). **Clean-code
  freeze:** D-639 (2026-07-04).

## Purpose

A read-only terms view over the server content block. Two modes:

- **Standalone read** — reached from the more menu / footer; موافق simply leaves
  the page (same as the back chevron).
- **In-flow consent** (`requireConsent: true`) — the explicit **موافق** tap *is*
  the consent (client-side only, D8); it returns `true` to the caller via
  `pop(true)`, and the back chevron declines via `pop(false)`.

Each non-empty line of the server body renders as one gold-hairline bullet card
under the معلومات هامة لزوار الملتقى heading; the gold موافق button is always
shown (505:1684 — no last-updated line, no interim checkbox).

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `terms_screen.dart` (~230) | `TermsScreen` (`ConsumerStatefulWidget`) — load → state glue (loading / empty / error / loaded), the header band, the accept/decline handlers, and the content `Column`. |
| `widgets/terms_bullet_card.dart` (`TermsBulletCard`) | One gold-hairline bullet card (gold • at the inline start + the term text). |

## Clean-code freeze (D-639)

- The top-right sweep tint was a page-local `const Color(0x0AFFFFFF)` — swapped
  to the exact-match token `SimfTokens.surfaceTint` (byte-identical), and the
  local const deleted.
- `_buildMessage` was a page-local twin of the shared `SimfErrorState`
  (`Center → Column → [message, FilledButton(retry)]`). Both the empty and the
  error branches now use `SimfErrorState`, which carries the retry the design
  requires — `SimfEmptyState` is intentionally **not** used (it is icon-only,
  no retry). The message text moves from `txtSecondary` to the widget's white,
  bringing terms into line with the app-wide error surface on the navy scaffold.
- `_BulletCard` → `widgets/terms_bullet_card.dart` (`TermsBulletCard`), verbatim.
- Fully tokenised (no raw `Color(0x..)`); every file ≤400 lines.

## L4 Figma parity (frame 505:1553)

Captured `terms_505-1553.png` (@375×900, ar) and **read it** — the navy surface
+ diagonal sweep, the centred الشروط والأحكام title with a left chevron, the
معلومات هامة لزوار الملتقى heading, three gold-hairline bullet cards (gold • at
the RTL inline-start), and the full-width gold موافق button pinned at the bottom.
RTL, no tofu. The bullet-card extraction is verbatim, so this golden locks the
D-375 parity going forward.

## Level-F

- **موافق** — `_accept()`: standalone leaves the page (`pop(null)` / `go('/')`);
  consent mode returns `true`.
- **Back chevron** — `_back()`: standalone leaves; consent mode returns `false`.
- **Retry** (empty / error) — `_load()` re-fetches the content block.
- **Body text** — `SelectableText` (the terms are selectable/copyable).

## Tests

`test/golden/terms_golden_test.dart` (frame 505:1553, @375×900, ar) +
`test/features/content/terms_screen_test.dart` (loaded / empty+retry / 404 /
transport-error+reload / consent). E2E: `docs/tests/e2e/mobile-terms.md`.

## Related decisions

- **D-639** (this clean-code freeze — token swap + shared `SimfErrorState` +
  bullet-card extraction + first golden).
- **D-367** (built to Figma 505:1553), **D-375** (fidelity pass).
