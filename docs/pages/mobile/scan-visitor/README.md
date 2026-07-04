# Scan visitor badge — مسح بطاقة زائر (`scanVisitor`, D-426)

- **Route:** `/exhibitor/scan` (`RouteNames.scanVisitor`). Access:
  **Exhibitor (approved, non-visitor)** — a visitor-tier caller gets 403 → a
  toast. Reached from the badge screen (exhibitor action).
- **API:** `ExhibitorRepository.scanByBadge(qrId)` — captures the visitor
  server-side; on success routes to `myVisitors`.
- **Figma:** none — a D-426 functional page, not a KSA design frame.
  **Clean-code freeze:** D-643 (2026-07-04).

## Purpose

Exhibitor lead-capture: scan a visitor's entry-badge QR (or type the code). On a
successful scan the visitor is captured and the screen shows a confirmation
toast, then routes to زواري (My Visitors). A 404 / 403 / other failure each
surface a distinct toast.

## Structure

| File | Holds |
|------|-------|
| `exhibitor/scan_visitor_screen.dart` (85) | `ScanVisitorScreen` (`ConsumerStatefulWidget`) — delegates the whole surface to the shared `QrScanView`; owns only `_onCode` (scan → capture → route + toast), `_failureText`, and `_leave`. |

## Clean-code freeze (D-643)

The screen was **already clean** — 85 lines, no widgets of its own; it delegates
entirely to the shared **`QrScanView`** (D-430), which guarantees the
manual-entry path and a camera that can never trap the user on EMUI. So this
freeze is the **render-lock golden only** (no code change) — locking that shared
scan surface as it renders for this screen.

## L4 render-lock (no Figma frame)

Captured `scan_visitor.png` (@375×812, ar, `enableCamera:false`) and **read it**
— the مسح بطاقة زائر header (forced-LTR bar), the "أو أدخل الرمز يدويًا" hint, the
رمز المشاركة manual-entry field, the gold بحث button and the رجوع link. RTL, no
tofu. The camera is off in the harness, so the golden locks the manual-entry
surface. No Figma frame is bound, so this is a structural render-lock.

## Level-F

- **Camera / manual entry** — both feed `_onCode` (shared `QrScanView`).
- **Successful scan** — capture server-side → toast → route to `myVisitors`.
- **404 / 403 / error** — distinct failure toasts.
- **Back** — `_leave` (pop, else → badge).

## Tests

`test/golden/scan_visitor_golden_test.dart` (render-lock, @375×812, ar,
`enableCamera:false`). The scan flow itself is exercised through the shared
`QrScanView` tests + the exhibitor repository tests. E2E:
[`docs/tests/e2e/mobile-scan-visitor.md`](../../../tests/e2e/mobile-scan-visitor.md)
(E2E-MOBSCANVIS-001..005, authored D-648 — closed the earlier pre-existing gap).

> **Remaining gap (owner):** this D-426 screen still has no dedicated *widget*
> test (the render is covered by the golden + the shared `QrScanView` tests) —
> a small unit test for `_onCode`'s toast/route branches is tracked for the owner.

## Related decisions

- **D-643** (this clean-code freeze — render-lock golden + first PAGE-INDEX row,
  no code change).
- **D-426** (exhibitor scan + my-visitors built), **D-430** (shared `QrScanView`).
