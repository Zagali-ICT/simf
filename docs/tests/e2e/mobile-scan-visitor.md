# E2E test catalogue — `Scan visitor badge` (`scanVisitor`)

> **Authority:** SIMF E2E test catalogue (D-133). Mobile catalogue — the
> exhibitor lead-capture scan "مسح بطاقة زائر / scan a visitor's badge" (D-426).
> Reached from the badge screen's exhibitor action. Backend:
> `ExhibitorRepository.scanByBadge(qrId)` (the exhibitor scan endpoint) —
> captures the visitor server-side, then the app routes to
> [`myVisitors`](mobile-my-visitors.md). The screen delegates its surface to the
> shared `QrScanView` (D-430): the manual-entry path always works and the bounded
> opt-in camera can never trap the user on EMUI. **D-724 (owner item 10):**
> `QrScanView` was re-skinned to the navy/gold KSA-Project language (Figma
> 1701:7080) — circular back header, beige field chrome, gold "or" divider, and
> the `SimfScannerFrame` gold-bracket viewfinder (node 758:4735) for the
> camera-on state — so it matches the badge "Share my QR" page. Presentation
> only; the manual-first / bounded-camera / two-exit contract is unchanged, and
> both goldens (`scan_visitor.png` + `scan_contact_1701-7080.png`) were re-locked.
> **D-737 (unified scanner):** `QrScanView` now hosts the shared
> `SimfScannerBody` (`lib/app/widgets/simf_scanner_body.dart`) with the single
> `ScanGate` dedupe policy and a visible camera-permission-denied error card; the
> manual-first / bounded-camera / two-exit contract and the golden are unchanged.
> App tests:
> `test/features/exhibitor/scan_visitor_screen_test.dart` (widget, 4 cases — the
> `_onCode` capture/route + 404/403/5xx toast branches) + the render-lock golden
> `test/golden/scan_visitor_golden_test.dart` (`goldens/scan_visitor.png`
> @375×812, `enableCamera:false`). Clean-code reviewed + frozen (D-643,
> 2026-07-04); per-page doc
> [`docs/pages/mobile/scan-visitor/`](../../pages/mobile/scan-visitor/README.md).

| | |
|--|--|
| **Page** | mobile exhibitor lead-capture scan (no Figma frame — functional page) |
| **Route** | app screen `/exhibitor/scan` (`RouteNames.scanVisitor`) |
| **Surface** | Mobile (Flutter); shared `QrScanView` (camera + manual entry) |
| **Role/gate** | Exhibitor (approved, non-visitor). A visitor-tier caller → server 403 → a toast |
| **Test runner** | Flutter widget/golden test + device manual (camera path is device-only) |

> **Notes:** the entry `QrId` scanned here is the visitor's badge QR; on success
> the visitor is captured and the app navigates to زوار جناحي so the exhibitor sees
> the updated list. The camera is off in the harness (`enableCamera:false`); the
> manual-entry field drives the flow in tests.

---

### E2E-MOBSCANVIS-001 — Golden path (scan → capture → My Booth Visitors)

```gherkin
Scenario: An exhibitor scans a visitor badge
  Given a signed-in approved exhibitor opens "مسح بطاقة زائر"
  When they scan (or type) a valid visitor badge QR and continue
  Then scanByBadge is sent with the trimmed code
  And it returns HTTP 200 (the visitor is captured server-side)
  And a "تم تسجيل الزائر / Visitor captured" toast shows
  And the app routes to زوار جناحي (myVisitors) showing the newly-captured visitor
  And exactly one lead email is dispatched to the exhibitor (see E2E-MOBSCANVIS-007)
```

### E2E-MOBSCANVIS-002 — Unknown code (404 not found)

```gherkin
Scenario: An unknown / expired badge code
  Given the exhibitor enters a code that resolves to nothing
  When scanByBadge returns 404
  Then the "not found" toast ("لم يتم العثور على الزائر") shows
  And the exhibitor stays on the scan screen (no navigation)
```

### E2E-MOBSCANVIS-003 — Auth gate (visitor-tier → 403)

```gherkin
Scenario: A non-exhibitor account is refused
  Given a signed-in visitor-tier account reaches the scan screen
  When scanByBadge returns 403
  Then the forbidden toast ("يمكن لحسابات العارضين فقط…") shows
  And no capture happens; the screen stays put
```

### E2E-MOBSCANVIS-004 — Manual-entry path + generic failure

```gherkin
Scenario: Manual entry drives the flow (no camera)
  Given the camera is unavailable / disabled
  When the exhibitor types the badge code into the manual field and taps "بحث"
  Then scanByBadge is sent with the typed code (same path as a camera scan)

Scenario: A transport / 5xx failure
  When scanByBadge fails with a non-404/403 error
  Then the generic error toast ("تعذّر تسجيل الزائر") shows and the screen stays put
```

### E2E-MOBSCANVIS-005 — Back / leave + RTL

```gherkin
Scenario: Leaving the scanner
  When the exhibitor taps back ("رجوع")
  Then the scanner pops (or routes to the badge screen if it cannot pop)

Scenario: RTL
  Given the app language is Arabic
  Then the header (forced-LTR bar), the manual-entry hint + field, the gold
    "بحث" button and the "رجوع" link render right-to-left, no tofu
```

### E2E-MOBSCANVIS-006 — Unified scanner: camera-first + camera-denied (D-737)

```gherkin
Scenario: The camera stage is the shared SimfScannerBody
  Given a signed-in approved exhibitor opens "مسح بطاقة زائر" and starts the camera
  Then the gold-bracket viewfinder opens over the bounded live camera
  And the manual field + gold "بحث" button stay usable below it

Scenario: A denied / missing camera shows the error card, not a black box
  When the OS denies the camera permission (or the device has no camera)
  Then the shared error card shows
       "تعذّر تشغيل الكاميرا. فعّل إذن الكاميرا من إعدادات النظام، أو أدخل الرمز يدويًا بالأسفل." /
       "Camera unavailable. Enable camera permission in system settings, or type the code below."
  And a "إعادة المحاولة / Try again" retry control is offered
  And the exhibitor can still type the badge code and run scanByBadge (same capture flow)
```

**Evidence:** source-verified — `simf_scanner_body.dart` `_CameraErrorCard` on a
controller error / the 8 s watchdog (device-only render); `simf_scanner_body_test`
covers the always-mounted manual field with the camera off; `scan_gate_test`
(single-flight + dedupe). The capture / 404 / 403 / 5xx branches remain in
`scan_visitor_screen_test`.

### E2E-MOBSCANVIS-007 — The lead is emailed to the exhibitor (BUG-024, 2026-07-26)

```gherkin
Scenario: A new capture emails the lead card to the exhibitor's own address
  Given a signed-in approved exhibitor scans a valid visitor badge at their booth
  When POST /app/exhibitor/visitors/scan returns 200
  Then exactly ONE email is dispatched, addressed to the exhibitor's own account email
  And its subject is "SIMF visitor captured at your booth: {VisitorName}"
  And the body is bilingual (English block, rule, RTL Arabic block) and carries
      the visitor's name, job title, organisation, the scan time on the SAUDI
      wall clock in 12-hour form (D-219, never UTC) and the operator's note
  And it carries NEITHER the visitor's national ID NOR the raw badge QR id

Scenario: A duplicate scan does not email again
  When the SAME exhibitor re-scans the SAME visitor's badge
  Then the response is still 200 and My Booth Visitors still holds ONE row
  And NO second email is dispatched

Scenario: A failed scan emails nothing
  When the badge resolves to nothing (404) or the caller is visitor-tier (403)
  Then no lead email is dispatched

Scenario: A mail failure never breaks the scan
  Given the email queue throws on enqueue
  When a valid badge is scanned
  Then the response is still 200 and the capture row is still written
  And an Email.EnqueueFailed audit row is recorded (purpose "ExhibitorLeadCapture")
```

**Evidence:** `tests/SIMF.Api.Tests/ExhibitorLeadEmailTests.cs` (exactly-one /
duplicate-none / failed-scan-none, plus the field + Saudi-time + no-QR-id
assertions); `EmailTemplateRendererTests.Catalog_default_exhibitor_lead_capture_*`
for the template shape. The mail-failure path is the shared
`EmailQueueExtensions.TryEnqueueAsync` contract already covered by
`EmailEnqueueFailureTests`. The template copy is admin-editable in the Control
Panel (`/admin/email/templates` → `ExhibitorLeadCapture`).

---

_Last reviewed:_ 2026-07-26 by Claude — BUG-024: a new booth capture now emails
the lead card to the exhibitor (E2E-MOBSCANVIS-007). Earlier: `2026-07-11` by
`SIMF Team` (D-737 unified scanner) and `2026-07-04`.
