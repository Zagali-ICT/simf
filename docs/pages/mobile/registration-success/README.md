# Registration success — تم التسجيل (Page 010, `#10`)

- **Route:** `/registration/success` (`RouteNames.registrationSuccess`). Reached as a **replacement** after the sign-up profile save (D-291), so the multi-step form is off the back stack. In the auth gate; offline-safe (owns **no write API**).
- **Figma:** **505:1451** (D-366). **Clean-code freeze:** D-625 (2026-07-04).

## Purpose

The terminal confirmation of sign-up: a green-ringed success mark, the "تم التسجيل
بنجاح" headline over the CP-editable welcome copy (D-461, site-settings with an
offline fallback), the reference-number card (the real DB-issued
`SIMF-YYYY-NNNNNNNN` when carried from the save, else the `SIMF-2026-xxxx` mask —
D-373), the gold **حالة التسجيل** action (→ Page 011 status) and the outlined
**الانتقال للرئيسية** (→ home), and the visual **تواصل معنا** block (call + mail
tiles that open the OS dialer / mail app when a `BuildConfig` contact value is
supplied, inert otherwise — D-369).

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `registration_success_screen.dart` (89) | The `ConsumerWidget` — reads `siteSettingsProvider` for the welcome message (offline fallback), `_back`, and the Scaffold: the decorative sweep + header + body. |
| `widgets/registration_success_header.dart` | `RegistrationSuccessHeader` — the back-chevron + centred-title band (title 24/w500; kept local, not `SimfPageShell`). |
| `widgets/registration_success_body.dart` | `RegistrationSuccessBody` — the scrollable 400-capped column composing the pieces below; owns the l10n reads + the status/home nav + the `_maskedReference` fallback. |
| `widgets/registration_success_mark.dart` | `RegistrationSuccessMark` — the 104px navy-deep circle + green ring + check. |
| `widgets/reference_number_card.dart` | `ReferenceNumberCard` — the beige label over the gold LTR reference on the navy-80% fill. |
| `widgets/registration_success_actions.dart` | `RegistrationSuccessActions` — the gold FilledButton (status) + the outlined button (home). |
| `widgets/contact_us_section.dart` | `ContactUsSection` (+ `_ContactTile`) — the تواصل معنا title, the call + mail tiles (best-effort `tel:`/`mailto:` via the D-369 helper, inert on an empty `BuildConfig` value), and the social footer. |

Single-use leaves colocated with their parent (`_ContactTile` inside the contact
section) per the booths/venue_map precedent (D-615/D-618); every file ≤400 lines.

## Tokenisation (this freeze)

The four module-level raw `Color(0x..)` consts were removed: `_green =
Color(0xFF22C55E)` → the existing **`SimfTokens.statusAccepted`** (byte-identical);
`_sweepTint = Color(0x0AFFFFFF)` → the existing **`SimfTokens.surfaceTint`**
(byte-identical); `_refCardFill = Color(0xCC01132D)` → new **`SimfTokens.navyFill80`**;
`_tileBorder = Color(0xFF253660)` → new **`SimfTokens.tileBorderNavy`**. Every new
token carries the exact same ARGB value → render-preserving.

## L4 Figma parity (frame 505:1451)

The screen had no golden. Captured `registration_success_505-1451.png` (@375×900,
ar) and **read it** — it renders the full frame (sweep, header, green mark,
headline + welcome, the reference card with the gold `SIMF-2026-xxxx` mask, both
actions, the contact block + social footer) in correct RTL with no tofu. The
decomposition is render-preserving (verbatim moves + byte-identical token swaps),
so this new golden locks the D-366/D-373 parity going forward.

## Level-F

Wired: back (`pop`, else home); **حالة التسجيل** → registration-status;
**الانتقال للرئيسية** → home; the call / mail tiles open the OS dialer / mail app
via the D-369 best-effort helper when `BuildConfig.supportPhone` /
`supportEmail` is set, and stay **inert** (owner decision: visual-only until
official contact details exist) otherwise. Reads `siteSettingsProvider` (public
site-settings, offline fallback). No write API (offline-safe by contract).

## Tests

`test/golden/registration_success_golden_test.dart` (frame 505:1451, @375×900, ar)
+ `test/features/registration/registration_success_screen_test.dart` (5 — renders
+ both nav actions + the D-461 welcome message + its fallback). E2E:
`docs/tests/e2e/mobile-registration-success.md`.

## Related decisions

- **D-625** (this clean-code freeze — decomposition + tokens + first golden).
- **D-366** (rebuilt to the KSA-Project frame 505:1451; masked reference + visual-only contact tiles), **D-373** (the reference card renders the real DB-issued reference; mask only as the no-data fallback), **D-461** (CP-editable welcome message), **D-369** (best-effort contact-launch helper), **D-291** (screen built + wired into the flow).
