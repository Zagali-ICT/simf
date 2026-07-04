# E2E test catalogue — `Scan visitor badge` (`scanVisitor`)

> **Authority:** SIMF E2E test catalogue (D-133). Mobile catalogue — the
> exhibitor lead-capture scan "مسح بطاقة زائر / scan a visitor's badge" (D-426).
> Reached from the badge screen's exhibitor action. Backend:
> `ExhibitorRepository.scanByBadge(qrId)` (the exhibitor scan endpoint) —
> captures the visitor server-side, then the app routes to
> [`myVisitors`](mobile-my-visitors.md). The screen delegates its surface to the
> shared `QrScanView` (D-430): the manual-entry path always works and the bounded
> opt-in camera can never trap the user on EMUI. App test: the render-lock golden
> `test/golden/scan_contact_golden_test.dart` pattern applies
> (`test/golden/scan_visitor_golden_test.dart`, `goldens/scan_visitor.png`
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
> the visitor is captured and the app navigates to زواري so the exhibitor sees
> the updated list. The camera is off in the harness (`enableCamera:false`); the
> manual-entry field drives the flow in tests.

---

### E2E-MOBSCANVIS-001 — Golden path (scan → capture → My Visitors)

```gherkin
Scenario: An exhibitor scans a visitor badge
  Given a signed-in approved exhibitor opens "مسح بطاقة زائر"
  When they scan (or type) a valid visitor badge QR and continue
  Then scanByBadge is sent with the trimmed code
  And it returns HTTP 200 (the visitor is captured server-side)
  And a "تم تسجيل الزائر / Visitor captured" toast shows
  And the app routes to زواري (myVisitors) showing the newly-captured visitor
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

---

_Last reviewed:_ `2026-07-04` by `SIMF Team`.
