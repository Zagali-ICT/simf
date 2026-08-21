# Gate-operator console — فحص رمز QR (staff, `#32`)

- **Route:** `/gates/scan` (`RouteNames.gateScanner`, screen `#105`). Role-gated to `AppRole.staff`+; the server additionally requires the `Gates.Operate` grant + a gate assignment.
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
| `gate_scan_screen.dart` (386) | `GateScanScreen` + State: assignment load, scan call, idempotency, back/leave stage-walk, offline-backlog drain, `_body` dispatch |
| `widgets/gate_setup_view.dart` | `GateSetupView` — QR tile + picker + movement toggle + scan CTA |
| `widgets/gate_picker.dart` | `GatePicker` — assigned-gates dropdown (brand font sourced from theme constants) |
| `widgets/gate_direction_button.dart` | `GateDirectionButton` — one دخول/خروج pill |
| `widgets/gate_result_view.dart` | `GateResultView` — allowed/denied verdict card + detail rows |
| `widgets/gate_scan_app_bar.dart` | `GateScanAppBar` — the forced-LTR top bar + circular navy back button (Figma 758:4655). It used to be built inline in the screen |
| `widgets/gate_pending_banner.dart` | `GatePendingBanner` — the slim "N waiting to sync" strip above the scanning stages; renders nothing when the backlog is empty |
| `data/gates_repository.dart` | `GatesRepository` — assignments, the scan POST, the offline queue/flush, the D-820 on-device verdict |
| `data/gate_scan_queue.dart` | `GateScanQueue` + `PendingGateScan` — the persisted offline backlog (see below) |
| `data/gate_offline_config.dart`, `data/offline_badge.dart` | the cached offline rules + the badge decoder they judge with |

The forbidden / not-assigned / load-error states use the shared `SimfEmptyState`
/ `SimfErrorState` (D-616 replaced the local `_Centered` / `_Retry`).

## Actions (Level-F: all wired)

| Element | Handler |
|---------|---------|
| Gate dropdown | `_onGate` → `_applyGateDefaults` |
| دخول / خروج pill | sets `_direction` (disabled per gate mode) |
| سكان الرمز | opens the scanner (disabled until a direction is chosen) |
| ZXing camera / manual submit | `recordScanOrQueue` (`POST /app/gates/{id}/scans`, fresh UUIDv4 per scan; queues on-device when the server is unreachable) |
| سكان مرة أخرى | clears result, reopens scanner |
| retry (error state) | `_loadGates` |
| back | stage-walk result→setup→scanner→leave |

## L4 Figma parity

Setup golden `test/golden/goldens/gate_setup_758-4651.png` (@375×812) overlay-verified
against 758:4651 — back button, gold QR tile, dropdown, movement pills, hint all match.
Result frames 4819/4886 covered by the green allowed/denied tests (built D-509).

## Gate state + 403 handling (DEF-STF-005 / DEF-STF-006 / DEF-STF-008)

- **A 403 shows the SERVER's own reason.** "You do not hold `Gates.Operate`" and
  "you are not assigned to this gate" are both 403 and need different operator
  actions, so the console renders `ApiFailure.message` (already picked for the
  app locale by the envelope decoder) whenever the server sent one, and falls
  back to `gateForbidden` only for a bare policy 403 with no body. 429 keeps its
  own rate-limit copy.
- **An inactive assigned gate is marked.** `GatePicker` tags it
  `"<name> — غير نشطة / — inactive"` in a muted colour, and the setup card shows a
  warning under the picker. It is marked rather than excluded so an operator whose
  only assignment went inactive still sees it (and the reason) instead of an
  empty console.
- **An inactive gate denies at HTTP 200, not 503.** `GateScanResultKind.GateInactive`
  (503 `GATE_INACTIVE`) was dead code — nothing produced it. The engine denies at
  step 5 with `DenialReasonCode.GateInactiveAtScan`, which keeps the append-only
  `GateScan` audit row for the attempt and gives the operator the designed red
  denial card carrying "This gate is currently inactive." The 503 arm was removed
  and `SIMF-API-GATES-001` §7.2.4 / §8.1 / §8.2 carry the as-built note.

## Offline scan queue (G-4 / D-819)

A scan that never reached the server is held on-device and retried instead of
being lost. Only a call that returned **no response at all** (network down, a
timeout — `ApiFailure.httpStatus == null`) is queued; anything the server did
answer, including a 429 or a deterministic 5xx, is a decision a blind retry
cannot change, so it is rethrown and surfaced to the operator. While the backlog
is non-empty the `GatePendingBanner` shows "N waiting to sync"; opening the
console and each successful online scan drain it oldest-first with each entry's
**original** idempotency key, so a scan the server already recorded replays
rather than counting the person twice.

The backlog is a JSON array in the non-sensitive prefs store
(`StorageKeys.pendingGateScans`), capped at **5000** entries — raised from 500
by D-819, because a dropped entry is a person who walked through a gate with no
record of it and 500 is inside one busy gate's shift.

**Each entry holds `gateId`, `qr`, `idempotencyKey`, `direction` and
`queuedAtIso` — and `queuedAtIso` is the Saudi wall clock, never the device's.**
This is the field a reader would assume was UTC, so it is worth stating: it is
`formatWire(saudiNow())`, per the house rule D-219 / D-770. It used to be
`formatWire(DateTime.now())`, which was wrong in a way nothing downstream could
detect — `formatWire` drops the zone marker and keeps the **wall-clock
reading**, so a tablet set to any non-Saudi timezone wrote its own local reading
into the record, labelled as Saudi time. `saudiNow()` cancels the device's
`timeZoneOffset` and adds +03:00, so the same instant is read on the Riyadh
clock wherever the tablet is set. The clock is a constructor seam on
`GatesRepository` (defaulting to `saudiNow`) purely so a test can pin the
stamped string exactly; production never passes anything else.

**Where the stamp goes today: nowhere off the device.** The scan POST body is
`qr` / `idempotencyKey` / `source` / `direction` only — `flushPending` replays
through that same call — so `queuedAtIso` is written to prefs and read by
nothing. `SIMF-API-GATES-001` §"scans" does define an optional `clientScannedAt`
("device-asserted device-local scan time, recorded but never authoritative")
which this app does not send. The stamp is therefore correct-but-unused; it
matters because the moment it IS surfaced or uploaded, a wrong reading would be
indistinguishable from a right one.

When the link is down the console also gives an **advisory** on-device verdict
(D-820 / D-821) by decrypting the badge against the cached gate rules — see
`GatesRepository.judgeOffline`. It is advisory precisely because the queued scan
is still uploaded and re-decided by the server against live data.

## Tests

`test/features/gates/gate_scan_screen_test.dart` (19) + `gate_models_test.dart` (6)
+ `gates_repository_test.dart` (11) —
Both-gate movement gating, allowed/denied results, direction sent, scan-again,
not-assigned, 403 (server reason + generic fallback), inactive-gate marker + warning,
retry, 429 rate-limit, queue/flush behaviour. The queued-at stamp has two tests in
`gates_repository_test.dart`: one injects a clock and pins the serialized string
exactly, the other brackets the default between two `saudiNow()` readings — the
bracket alone cannot catch the old bug, since on a +03:00 machine `saudiNow()` and
`DateTime.now()` agree, which is why the seam exists.
Server: `tests/SIMF.Api.Tests/GateScanTests.cs`
(`Inactive_gate_records_a_GATE_INACTIVE_AT_SCAN_denial_at_200`). Golden:
`test/golden/gate_setup_golden_test.dart`. E2E: `docs/tests/e2e/mobile-gate-scan.md`.
