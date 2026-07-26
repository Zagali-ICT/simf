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
| **Role/gate** | Exhibitor (approved) — DEF-EXH-001: the server authorises on `ProfileType.MobileAppRole == Exhibitor` (D-519), so Staff / Moderator / Media / Sponsor / plain Visitor callers all get 403 → a toast |
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

### E2E-MOBSCANVIS-003 — Auth gate (any non-exhibitor → 403)

```gherkin
Scenario: A non-exhibitor account is refused
  Given a signed-in visitor-tier account reaches the scan screen
  When scanByBadge returns 403
  Then the forbidden toast ("يمكن لحسابات العارضين فقط…") shows
  And no capture happens; the screen stays put
```

### E2E-MOBSCANVIS-007 — Only a real exhibitor may scan (DEF-EXH-001)

```gherkin
Scenario Outline: Every non-exhibitor caller is refused, not just visitors
  Given a signed-in Approved account whose profile type is <type>
  When it calls POST /api/v1/app/exhibitor/visitors/scan with any badge code
  Then the API answers 403 "Only exhibitor accounts can scan visitor badges."
  And GET /api/v1/app/exhibitor/visitors also answers 403
  And no visitor PII (email, Saudi mobile, international mobile) is returned

  Examples:
    | type      |
    | Normal    |
    | Staff     |
    | Moderator |

Scenario: A genuine exhibitor may scan
  Given a signed-in Approved account whose profile type is "Exhibitor"
    (ProfileType.MobileAppRole = Exhibitor, D-519)
  When it scans an eligible visitor badge
  Then the API answers 200 with the visitor's full card
```

> The old rule authorised on "the profile type is NOT a visitor type", which
> admitted every partner type — Staff, Moderator, Media and Sponsor tokens could
> call both endpoints and harvest visitor PII (login email + both mobile
> numbers). `ProfileType` lives on the App DB beside `UserProfile`, so the
> `MobileAppRole` test is a single-database query (D-157: no cross-DB join).

**Evidence:** `tests/SIMF.Api.Tests/ExhibitorVisitorScanTests.cs` —
`Exhibitor_scans_visitor_badge_captures_and_returns_full_card`,
`Visitor_caller_cannot_scan_badges_403`, `Staff_caller_cannot_scan_badges_403`,
`Moderator_caller_cannot_scan_badges_403`.

### E2E-MOBSCANVIS-008 — Only a visitor may be scanned (DEF-EXH-003)

```gherkin
Scenario Outline: An ineligible badge subject is indistinguishable from unknown
  Given a signed-in Approved exhibitor
  When it scans <badge>
  Then the API answers 404 "No visitor badge matches this code."
  And nothing is added to My Visitors

  Examples:
    | badge                                   |
    | a Staff account's badge                 |
    | another exhibitor's badge               |
    | a deactivated (soft-deleted) profile    |
    | an unknown code                         |
```

> The scan previously resolved a `QrId` with no `IsActive` and no audience-side
> filter, so a staff badge or a rival exhibitor's badge was capturable as a
> "lead". The four cases share ONE error shape so the scan never leaks whether a
> badge exists but is ineligible. A visitor with no tier assigned stays eligible
> — the approve-time tier is optional (CS-D / D-386), so a null `ProfileType` is
> an ordinary audience account, not a partner.

**Evidence:** `ExhibitorVisitorScanTests.Ineligible_badge_subject_returns_404`,
`ExhibitorVisitorScanTests.Unknown_badge_returns_404`.

### E2E-MOBSCANVIS-009 — The visitor is told their card was shared (DEF-EXH-002)

```gherkin
Scenario: A new capture notifies the visitor, naming the exhibitor
  Given a signed-in Approved exhibitor named "Acme Marine / أكمي البحرية"
  And an eligible visitor badge that this exhibitor has not captured before
  When the exhibitor scans it
  Then the capture succeeds (200)
  And exactly ONE in-app notification of kind "ExhibitorLeadCaptured" is
    written for the VISITOR, in the "Account" group
  And its body names "Acme Marine" (Arabic body names "أكمي البحرية")
  And no email is queued

Scenario: An idempotent re-scan does not re-notify
  When the same exhibitor scans the same badge again (refreshing the note)
  Then the capture still succeeds (200)
  And the visitor still has exactly ONE ExhibitorLeadCaptured notification
```

> DEF-EXH-002 (privacy): the scan reads the visitor's ENTRY badge and returns a
> full contact card, so the visitor is at minimum told who now holds it. The
> notification is best-effort (`TryDispatchAsync`) — a dispatch failure never
> undoes the committed capture. **Not implemented, deliberately:** an opt-out
> flag / consent gate on the scan itself. Whether an exhibitor may scan at all is
> a product decision affecting live event operations; the recommended design is
> written up for the owner rather than shipped.

**Evidence:**
`ExhibitorVisitorScanTests.New_capture_notifies_the_visitor_once_and_a_rescan_is_silent`.

### E2E-MOBSCANVIS-010 — A CP-provisioned booth officer can scan (DEF-EXH-005)

```gherkin
Scenario: The CP's own provisioning path produces a working exhibitor
  Given an administrator provisions a booth account with
    POST /api/v1/admin/exhibitors/{id}/accounts
  And the officer completes the invite (password) and is approved
  When the officer signs in to the app and scans an eligible visitor badge
  Then the API answers 200 with the visitor's full card
  And GET /api/v1/app/exhibitor/visitors lists the capture
  And the provisioned account's profile type carries MobileAppRole = Exhibitor
```

> The provisioning path created the officer with **no** profile type ("a
> least-privilege Visitor account"), which the DEF-EXH-001 rule can never admit —
> the CP produced exhibitors that could not use the tools they were provisioned
> for. The account is now created through the partner-side `CreateOtherAsync`
> pipeline with the exhibitor profile type, resolved by its `MobileAppRole`
> (never by a name literal — the row is admin-curated and renameable). With no
> active exhibitor-mapped profile type at all, provisioning answers a clean 409
> `ADMIN_PROFILE_TYPE_INVALID` instead of minting an unusable account.

**Evidence:** `ExhibitorVisitorScanTests.Cp_provisioned_booth_officer_can_scan_and_list`.

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

---

_Last reviewed:_ `2026-07-27` by `SIMF Team` — DEF-EXH-005: the CP provisioning
path now assigns the exhibitor profile type, so a booth officer created from the
CP can actually scan (E2E-MOBSCANVIS-010); the backing class
`ExhibitorVisitorScanTests` now holds 10 tests. Earlier: `2026-07-26` —
security/privacy fixes DEF-EXH-001 (only `MobileAppRole.Exhibitor` may scan),
DEF-EXH-003 (the scanned subject must be an active audience account),
DEF-EXH-002 (a new capture notifies the visitor once, naming the exhibitor) —
E2E-MOBSCANVIS-007..009. Earlier:
`2026-07-11` — D-737 unified scanner (QrScanView now hosts SimfScannerBody +
camera-error state); `2026-07-04`.
