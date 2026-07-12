# Scan a contact — مسح رمز QR (`scanContact`, FDS-014)

- **Route:** `/contacts/scan` (`RouteNames.scanContact`). Access:
  **Visitor (approved)** — auth-gated. Also opened full-screen from the badge
  screen's visitor action (`badge/widgets/badge_actions.dart` imports
  `ScanContactScreen` — **keep the name/path**).
- **API:** `POST /app/contacts/resolve` (code → live card) +
  `POST /app/contacts/save` (idempotent; saving yourself is a 400).
- **Figma:** owner-supplied **1701:7080** (FDS-014 §5.5–5.6, built D-286/D-324).
  **Clean-code freeze:** D-646 (2026-07-04).

## Purpose

Scan (or type) another visitor's share code → resolve it to a live `VisitorCard`
shown in a preview bottom sheet → optionally add a note → save to *My Contacts*.
A 404 resolves to a not-found toast; a 400 on save (self-save) surfaces inline. A
scanned **plain vCard with no SIMF share token** (a foreign phone's contact, or an
old QR) can't resolve to a live card, so instead of dead-ending the app offers to
add it straight to the phone's own contacts via the OS "add contact" flow (D-744).

## Structure

| File | Holds |
|------|-------|
| `contacts/scan_contact_screen.dart` (214) | `ScanContactScreen` (`ConsumerStatefulWidget`) — delegates the scan surface to the shared `QrScanView`; `_handleToken` (resolve → preview), `_showPreview`, `_leave`; plus the private `_ContactPreviewSheet` (the resolved-card + note + save sheet, shared `ContactCard`). |

## Clean-code freeze (D-646)

The screen was **already clean** — 214 lines, delegates its surface to the shared
**`QrScanView`** (D-430) and renders the resolved card via the shared
**`ContactCard`**, fully tokenised (no raw `Color(0x..)`). The `_ContactPreviewSheet`
is a cohesive private (the resolve→save half of the flow), kept in the screen.
So this freeze is the **render-lock golden only** (no code change).

`ScanContactScreen`'s name + path are **kept** — `badge/widgets/badge_actions.dart`
imports it for the visitor's full-screen "scan to add someone" action.

## L4 parity (frame 1701:7080)

Captured `scan_contact_1701-7080.png` (@375×812, ar, `enableCamera:false`) and
**read it** — the مسح رمز QR header (forced-LTR bar), the "أو أدخل الرمز يدويًا"
hint, the رمز المشاركة manual-entry field, the gold بحث button and the رجوع link.
RTL, no tofu. The camera is off in the harness and the preview sheet only opens
after a scan, so the golden locks the resting scan surface.

## Level-F

- **Camera / manual entry** — feed `_handleToken` (shared `QrScanView`).
- **Resolve** — code → live `VisitorCard` in the preview sheet (404 → toast).
- **Save** — with an optional note → `POST /app/contacts/save` (self-save 400 →
  toast); success toasts + closes so the caller reloads.
- **Foreign vCard → phone contacts** — a scanned vCard with no `X-SIMF-TOKEN`
  can't resolve; `_saveVcardToPhone` confirms, then hands the raw vCard to the OS
  add-contact flow via the shared `shareTextContent` (D-744). No new permission.
- **Back** — `_leave`.

## Tests

`test/golden/scan_contact_golden_test.dart` (frame 1701:7080, @375×812, ar,
`enableCamera:false`) + `test/features/contacts/scan_contact_screen_test.dart`
(resolve / not-found / self-save / unavailable-hides-save / foreign-vCard →
save-to-phone). E2E: `docs/tests/e2e/mobile-my-contacts.md` (E2E-MMC-013).

## Related decisions

- **D-744** (a token-less foreign vCard is offered to the phone's own contacts via
  the OS add-contact flow — `shareTextContent` — instead of dead-ending).
- **D-646** (this clean-code freeze — render-lock golden; no code change).
- **D-286 / D-324** (built, FDS-014), **D-430** (shared `QrScanView`),
  **D-426** (bounded opt-in camera).
