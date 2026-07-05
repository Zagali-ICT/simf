# Gate-operator console — فحص رمز QR (staff, `#32`)

- **Route:** `/gate-scan` (`RouteNames.gateScanner`). Role-gated to `AppRole.staff`+; the server additionally requires the `Gates.Operate` grant + a gate assignment.
- **Figma:** setup **758:4651**, denied **758:4819**, allowed **758:4886**.
- **Clean-code freeze:** D-616 (2026-07-04). D-406 / D-509 built it.

## Flow (5-stage state machine)

load assignments (`GET /app/gates/my-assignments`) → **setup** (pick gate + دخول/خروج
movement) → **scanner** (ZXing camera or manual entry) → **result** (green مسموح /
red ممنوع via `POST /app/gates/{id}/scans`) → "سكان مرة أخرى". A fixed In/Out gate
locks the movement; a Both gate requires the operator to choose first.

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `gate_scan_screen.dart` (361) | `GateScanScreen` + State: assignment load, scan call, idempotency, back/leave stage-walk, forced-LTR AppBar, `_body` dispatch |
| `widgets/gate_setup_view.dart` | `GateSetupView` — QR tile + picker + movement toggle + scan CTA |
| `widgets/gate_picker.dart` | `GatePicker` — assigned-gates dropdown (brand font sourced from theme constants) |
| `widgets/gate_direction_button.dart` | `GateDirectionButton` — one دخول/خروج pill |
| `widgets/gate_scanner_view.dart` | `GateScannerView` — ZXing camera + manual-entry row |
| `widgets/gate_result_view.dart` | `GateResultView` — allowed/denied verdict card + detail rows |

The forbidden / not-assigned / load-error states use the shared `SimfEmptyState`
/ `SimfErrorState` (D-616 replaced the local `_Centered` / `_Retry`).

## Actions (Level-F: all wired)

| Element | Handler |
|---------|---------|
| Gate dropdown | `_onGate` → `_applyGateDefaults` |
| دخول / خروج pill | sets `_direction` (disabled per gate mode) |
| سكان الرمز | opens the scanner (disabled until a direction is chosen) |
| ZXing camera / manual submit | `recordScan` (`POST /app/gates/{id}/scans`, idempotency-keyed) |
| سكان مرة أخرى | clears result, reopens scanner |
| retry (error state) | `_loadGates` |
| back | stage-walk result→setup→scanner→leave |

## L4 Figma parity

Setup golden `test/golden/goldens/gate_setup_758-4651.png` (@375×812) overlay-verified
against 758:4651 — back button, gold QR tile, dropdown, movement pills, hint all match.
Result frames 4819/4886 covered by the green allowed/denied tests (built D-509).

## Tests

`test/features/gates/gate_scan_screen_test.dart` (9) + `gate_models_test.dart` (5) —
Both-gate movement gating, allowed/denied results, direction sent, scan-again,
not-assigned, 403 not-authorised, retry, 429 rate-limit. Golden:
`test/golden/gate_setup_golden_test.dart`. E2E: `docs/tests/e2e/mobile-gate-scan.md`.
