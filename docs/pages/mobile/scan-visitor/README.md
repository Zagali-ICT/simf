# Scan visitor badge — مسح بطاقة زائر (`scanVisitor`, D-426)

- **Route:** `/exhibitor/scan` (`RouteNames.scanVisitor`). Access:
  **Exhibitor (approved)** — DEF-EXH-001: the server authorises on
  `ProfileType.MobileAppRole == Exhibitor` (D-519), so Staff / Moderator /
  Media / Sponsor / plain Visitor callers all get 403 → a toast. Reached from
  the badge screen (exhibitor action).
- **API:** `ExhibitorRepository.scanByBadge(qrId)` — captures the visitor
  server-side; on success routes to `myVisitors`. DEF-EXH-003: the scanned
  subject must itself be an ACTIVE audience-side account (a staff or rival
  exhibitor badge answers the same 404 as an unknown code). DEF-EXH-002: a NEW
  capture raises one `NotificationKind.ExhibitorLeadCaptured` in-app notice to
  the visitor naming the exhibitor; an idempotent re-scan raises none.
  DEF-EXH-005: a booth officer provisioned from the CP
  (`POST /admin/exhibitors/{id}/accounts`) now carries the exhibitor profile
  type, so the CP's own path produces an account that can actually scan.
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
`enableCamera:false`) + `test/features/exhibitor/scan_visitor_screen_test.dart`
(widget, 4 cases — the `_onCode` branches: valid badge → capture + route to My
Visitors with the code trimmed; 404 / 403 / 5xx → the distinct toasts, no
navigation). E2E:
[`docs/tests/e2e/mobile-scan-visitor.md`](../../../tests/e2e/mobile-scan-visitor.md)
(E2E-MOBSCANVIS-001..005). Both the widget-test and E2E gaps flagged at freeze
are now closed (D-648).

## Related decisions

- **D-643** (this clean-code freeze — render-lock golden + first PAGE-INDEX row,
  no code change).
- **D-426** (exhibitor scan + my-visitors built), **D-430** (shared `QrScanView`).
