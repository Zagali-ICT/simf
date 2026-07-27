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
| **Role/gate** | Exhibitor (approved) with a current booth membership — DEF-EXH-001: the server authorises on `ProfileType.MobileAppRole == Exhibitor` (D-519), so Staff / Moderator / Media / Sponsor / plain Visitor callers all get 403 → a toast. DEF-EXH-006: an active `ExhibitorMembership` of an active `Exhibitor` is required alongside the role. D-781: an exhibitor-typed account created through the Others pipeline is attached to a booth from the CP (`POST /admin/exhibitors/{id}/accounts/link`) |
| **Subject** | D-780 (owner decision 2026-07-27, "can scan all badges") — ANY ACTIVE badge holder is capturable: media, sponsor, staff and fellow-exhibitor badges included. Only a deactivated account is refused (same 404 as an unknown code) |
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

### E2E-MOBSCANVIS-008 — ALL badges are scannable; only a dead account is refused (D-780)

```gherkin
Scenario Outline: Every ACTIVE badge holder is a capturable lead
  Given a signed-in Approved exhibitor with a current booth membership
  When it scans <badge>
  Then the API answers 200 with that person's full card
  And the row appears in My Visitors

  Examples:
    | badge                                   |
    | a Media account's badge                 |
    | a Sponsor account's badge               |
    | a Staff account's badge                 |
    | another exhibitor's badge               |
    | an ordinary visitor's badge             |

Scenario Outline: A badge that resolves to nothing usable is indistinguishable
  Given a signed-in Approved exhibitor
  When it scans <badge>
  Then the API answers 404 "No visitor badge matches this code."
  And nothing is added to My Visitors

  Examples:
    | badge                                   |
    | a deactivated (soft-deleted) profile    |
    | an unknown code                         |
```

> **Owner decision, 2026-07-27 (D-780) — "can scan all badges".** The owner was
> asked directly whether a booth may capture a MEDIA or SPONSOR attendee's badge,
> given the rule then admitted only audience-side (`IsForVisitor`) profile types
> and answered 404 for everything else, and ruled that ALL badges are scannable.
> That **reverses the premise of DEF-EXH-003**, which had introduced the
> audience-side narrowing. The `IsActive` half is deliberately KEPT: a deactivated
> account is not a valid attendee, and it answers the SAME 404 as an unknown code
> so the scan never leaks whether a badge exists. The **scanner-side** controls
> are untouched — the caller must still be exhibitor-typed AND hold a current
> booth membership (E2E-MOBSCANVIS-007 / -011).

**Evidence:**
`ExhibitorVisitorScanTests.Every_active_badge_is_capturable_whatever_its_profile_type`
(media + sponsor + staff + rival-exhibitor badges all 200 and all listed),
`ExhibitorVisitorScanTests.Deactivated_badge_subject_returns_404`,
`ExhibitorVisitorScanTests.Unknown_badge_returns_404`.

### E2E-MOBSCANVIS-009 — The visitor is told their card was shared (DEF-EXH-002)

```gherkin
Scenario: A new capture notifies the visitor, naming the exhibitor
  Given a signed-in Approved officer of the exhibitor "Acme Marine / أكمي البحرية"
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

### E2E-MOBSCANVIS-011 — Leaving the booth revokes the scan (DEF-EXH-006)

```gherkin
Scenario: A revoked booth membership ends the officer's scan authority
  Given an administrator provisioned a booth officer under an exhibitor
  And the officer is signed in and can scan an eligible visitor badge (200)
  When the officer's ExhibitorMembership is deactivated
  Then POST /api/v1/app/exhibitor/visitors/scan answers 403
       "Only exhibitor accounts with a current booth membership can scan
        visitor badges." /
       "مسح بطاقات الزوار متاح فقط لحسابات العارضين المرتبطة بجناح فعّال."
  And GET /api/v1/app/exhibitor/visitors also answers 403
  And no visitor PII (email, Saudi mobile, international mobile) is returned

Scenario: Closing the exhibitor revokes every officer under it
  Given an administrator provisioned a booth officer under an exhibitor
  When the administrator closes the exhibitor with
    DELETE /api/v1/admin/exhibitors/{id}
  Then the officer's scan and list both answer 403
```

> DEF-EXH-006: `MobileAppRole.Exhibitor` is granted at provisioning time and then
> lives on the PERSON's `UserProfile`, so it outlived the booth — removing
> someone from an exhibitor did not stop them scanning visitor badges or reading
> the contact cards they already held. Authority now requires an **active**
> `ExhibitorMembership` of an **active** `Exhibitor` alongside the role. The test
> runs on an already-issued token: the JWT still carries the exhibitor app role,
> so only the server-side membership check can revoke it. `UserProfile`,
> `ExhibitorMembership` and `Exhibitor` are all on the App DB, so this is still a
> single-database check (D-157: no cross-DB join).

**Evidence:**
`ExhibitorVisitorScanTests.Booth_officer_is_refused_once_the_membership_is_revoked`,
`ExhibitorVisitorScanTests.Closing_the_exhibitor_revokes_its_officers_scan_authority`.

### E2E-MOBSCANVIS-013 — An Others-pipeline account can be attached to a booth (D-781)

```gherkin
Scenario: The Others-pipeline lockout is fixed from the Control Panel
  Given an administrator creates an account with POST /api/v1/admin/others
    carrying the "Exhibitor" profile type (MobileAppRole = Exhibitor)
  And the account completes the invite (password) and is approved
  When it signs in to the app and scans an eligible badge
  Then the API answers 403 (it has the role but NO ExhibitorMembership)
  And GET /api/v1/app/exhibitor/visitors also answers 403

  When the administrator opens /admin/exhibitors → the row's "Accounts" modal
  And fills "Account email" with that account's email under "Link an existing
    account" and clicks "Link account"
  Then POST /api/v1/admin/exhibitors/{id}/accounts/link returns HTTP 200
    (permission Exhibitors.LinkAccount; audit Exhibitor.AccountLinked)
  And a green toast reads "Account linked to this exhibitor." /
    "تم ربط الحساب بهذا العارض."
  And the account now appears in the exhibitor's Accounts table
  And the same scan now answers 200 and My Visitors lists the capture

Scenario Outline: Linking is not a back door
  When the administrator links <case>
  Then the API answers <status> with error code <code>

  Examples:
    | case                                             | status | code                             |
    | an email no account is registered under          | 404    | EXHIBITOR_ACCOUNT_NOT_FOUND      |
    | an account with a Media (non-exhibitor) type     | 409    | EXHIBITOR_ACCOUNT_NOT_ELIGIBLE   |
    | an account that already belongs to an exhibitor  | 409    | EXHIBITOR_ACCOUNT_ALREADY_LINKED |
    | any account under a deactivated exhibitor        | 409    | EXHIBITOR_INACTIVE               |
```

> **Owner decision, 2026-07-27 (D-781).** DEF-EXH-006 made a CURRENT
> `ExhibitorMembership` half the lead-capture authorisation, and
> `AdminExhibitorService.ProvisionAccountAsync` was the ONLY writer of that row —
> so an exhibitor-typed account created through the generic Others pipeline
> (`POST /admin/others`) or the Others walk-in desk got 403 on scan AND on My
> Visitors with no Control-Panel path to attach it to a booth. The link action is
> that path. It deliberately does NOT mutate the account's profile type (that
> would silently change an app role another admin assigned): the account must
> already carry an active exhibitor-mapped type, else a distinct 409 tells the
> administrator to set it on the Others page first. The **scanner-side** controls
> are unchanged — removing an officer from a booth still revokes their tools
> (E2E-MOBSCANVIS-011).

**Evidence:**
`ExhibitorVisitorScanTests.Others_pipeline_account_can_scan_once_it_is_linked_to_an_exhibitor`
(403 before the link → 200 after),
`ExhibitorVisitorScanTests.Linking_refuses_an_account_that_is_not_exhibitor_typed`,
`ExhibitorVisitorScanTests.Linking_refuses_an_unknown_email_and_an_already_linked_account`.

### E2E-MOBSCANVIS-012 — The notice names the exhibitor, not the account (DEF-EXH-007)

```gherkin
Scenario: A CP-provisioned officer's capture still names who received the data
  Given an exhibitor named "Northern Shipyards / أحواض الشمال"
  And an administrator provisioned a booth officer under it
    (the officer's UserProfile is a stub with NO Name / NameArabic)
  When the officer scans an eligible visitor badge for the first time
  Then the capture succeeds (200)
  And the visitor's ExhibitorLeadCaptured notice names "Northern Shipyards"
  And the Arabic body names "أحواض الشمال"
```

> DEF-EXH-007: the notice used to take its name from the SCANNING ACCOUNT's
> `UserProfile.Name` / `NameArabic`. The CP provisioning pipeline creates a stub
> profile with neither set (`AdminAccountService.CreateAccountAsync`), so for
> exactly the accounts DEF-EXH-005 enabled the notice degraded to the generic
> "An exhibitor" / "أحد العارضين" — the one thing the notice exists to say was
> missing. The name is now resolved from the `Exhibitor` the officer represents,
> falling back to the officer's own profile name and only then to the generic
> wording.

**Evidence:**
`ExhibitorVisitorScanTests.Capture_notice_names_the_exhibitor_a_cp_officer_represents`.

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

_Last reviewed:_ `2026-07-27` by `SIMF Team` — **owner decisions D-780 + D-781**:
D-780 "can scan all badges" widened the SUBJECT rule to any ACTIVE badge holder
(media / sponsor / staff / fellow exhibitor now capturable; only a deactivated
account is refused), reversing the premise of DEF-EXH-003
(E2E-MOBSCANVIS-008 rewritten); D-781 added
`POST /admin/exhibitors/{id}/accounts/link` so an exhibitor-typed account created
through the Others pipeline can be attached to a booth from the CP instead of
being locked out by the DEF-EXH-006 membership rule (E2E-MOBSCANVIS-013). The
backing class `ExhibitorVisitorScanTests` now holds 17 tests. Earlier:
`2026-07-27` — DEF-EXH-006: scan authority now
requires a CURRENT booth membership, so revoking a membership or closing the
exhibitor revokes the tools (E2E-MOBSCANVIS-011); DEF-EXH-007: the capture
notice names the EXHIBITOR the officer represents, which a CP-provisioned stub
profile could not (E2E-MOBSCANVIS-012). The backing class
`ExhibitorVisitorScanTests` now holds 13 tests. Earlier: `2026-07-27` —
DEF-EXH-005: the CP provisioning path now assigns the exhibitor profile type, so
a booth officer created from the CP can actually scan (E2E-MOBSCANVIS-010).
Earlier: `2026-07-26` —
security/privacy fixes DEF-EXH-001 (only `MobileAppRole.Exhibitor` may scan),
DEF-EXH-003 (the scanned subject must be an active audience account),
DEF-EXH-002 (a new capture notifies the visitor once, naming the exhibitor) —
E2E-MOBSCANVIS-007..009. Earlier:
`2026-07-11` — D-737 unified scanner (QrScanView now hosts SimfScannerBody +
camera-error state); `2026-07-04`.
_Last reviewed:_ 2026-07-26 by Claude — BUG-024: a new booth capture now emails
the lead card to the exhibitor (E2E-MOBSCANVIS-007). Earlier: `2026-07-11` by
`SIMF Team` (D-737 unified scanner) and `2026-07-04`.
