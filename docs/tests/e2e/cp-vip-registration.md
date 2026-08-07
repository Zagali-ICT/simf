# E2E test catalogue — `VIP registration` (`/admin/visitors/vip`)

> **Authority:** SIMF E2E test catalogue (D-133 / D-245). VVIP/VIP feature D-429 (V-2).

| | |
|--|--|
| **Page** | [`vip-registration.md`](../../pages/cp/vip-registration.md) |
| **Route** | `/admin/visitors/vip` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell driver _(or: Playwright when adopted)_ |
| **Auth setup** | `superadmin@simrsnf.com` + TOTP via `Get-Totp` helper |
| **Last reviewed** | 2026-07-21 (VIP edit — page is now the VIP/VVIP list) |

> **What this page is (2026-07-21).** `/admin/visitors/vip` is now a **VIP/VVIP
> list page** — a copy of the visitor page scoped to the VIP guests (the
> `/admin/vips/list` subset: `ProfileType.Name` in {VVIP, VIP, Gold}). The grid
> lists name / job title / profile type / email. The toolbar **New VIP** button
> (gated by `Visitors.RegisterOnsite`) opens the VVIP/VIP **registration wizard**
> as a full section (the D-429 flow below); the per-row **Edit** (gated by
> `Visitors.Edit`) opens the shared `EditAccountForm` with `ShowVipPhoto=true` to
> change name / email / tier / profile photo / ID image / VIP welcome photo. It
> reuses the existing VIP list + account-id-keyed admin endpoints — no new
> permission, endpoint, or migration. Grounding:
> `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/VipRegistration.razor`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-VIPR-001 | Golden path — New VIP → register a VVIP with Mawj data + photo | happy | P0 | _to author_ |
| E2E-VIPR-002 | Picker restricted to VVIP / VIP only | happy | P0 | _to author_ |
| E2E-VIPR-003 | Auth gate (no RegisterOnsite permission → /not-permitted) | auth | P0 | _to author_ |
| E2E-VIPR-004 | Validation — Mawj/honorific over max length | error | P1 | _to author_ |
| E2E-VIPR-005 | Created account lands PendingApproval, no QR | happy | P0 | _to author_ |
| E2E-VIPR-006 | Approval reuses the existing pending-visitors queue | happy | P0 | _to author_ |
| E2E-VIPR-007 | RTL render (Arabic) | i18n | P1 | _to author_ |
| E2E-VIPR-008 | List shows the VIP/VVIP guests (name / job title / tier / email) | happy | P0 | _to author_ |
| E2E-VIPR-009 | New VIP opens the registration wizard section; Cancel returns to the list | happy | P1 | _to author_ |
| E2E-VIPR-010 | Per-row Edit changes name / email / tier / photo / ID / welcome photo | happy | P0 | _to author_ |
| E2E-VIPR-011 | Add/Edit affordances gated: no `RegisterOnsite`/`Edit` → buttons hidden, Details still shown (D-835) | auth | P0 | _to author_ |
| E2E-VIPR-012 | Details opens one VIP for a read-only admin, revealing the Arabic name and Arabic job title no column shows (D-835) | auth | P0 | _to author_ |
| E2E-VIPR-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-VIPR-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-VIPR-001 — Golden path: register a VVIP with Mawj data + photo

```gherkin
Feature: VIP registration golden path
  As an Administrator with on-site registration permission
  I want to register a VVIP guest with the Mawj welcome data and a clear photo
  So that the technical teams can compose the welcome message

Background:
  Given an Administrator is signed in
  And the "VVIP" and "VIP" audience profile-types are seeded

Scenario: Register a VVIP guest
  Given I am on "/admin/visitors/vip"
  When I select the "VVIP" tier
  And I enter English name "Khalid Al Otaibi" and Arabic name "خالد العتيبي"
  And I enter Mawj ID "MAWJ-10293", honorific "Minister", preferred language "Arabic"
  And I pick a Saudi nationality with national id "1101798278"
  And I pick an organisation and enter mobile "+966500000001"
  And I attach a clear VIP welcome photo (JPEG, < 2 MB)
  And I submit
  Then the success modal shows the new account with no QR (pending)
  And the UserProfile row carries MawjId, Honorific, PreferredLanguage
  And UserProfile.VipPhotoRelativePath is set
  And the account state is PendingApproval

Scenario: The desk enforces the Saudi-ID Luhn checksum and the plate set (D-459)
  Given I am on "/admin/visitors/vip"
  When I enter a Saudi national id "1234567890" (correct prefix/length but fails Luhn)
  And I submit
  Then the request is rejected (400) with the bilingual "national id is not valid" error
  And a national id "1101798278" (Luhn-valid) is accepted
  And the optional plate is entered via three 17-letter dropdowns (Arabic · Latin)
    + a 1–4 digit field (D-460) that assemble into the canonical code
    (e.g. ا/ب/ح + 1234 → "ABJ1234"); the server rejects out-of-set picks,
    matching the self-service profile rule
```

**Evidence captured:** screenshot before/after; console 0 errors; network 0 failed; `OperationLog` rows `UserProfile.VipPhotoUploaded` + walk-in register. Server validation backed by `WalkInRegistrationTests` + `SaudiPlateTests` (D-459).

### E2E-VIPR-002 — Picker restricted to VVIP / VIP only

```gherkin
Scenario: Only the VVIP and VIP tiers are offered
  Given I am on "/admin/visitors/vip"
  Then the profile-type picker shows only "VVIP" and "VIP"
  And the "Normal" / "Staff" / "Media" / "Sponsor" tiers are NOT offered
```

### E2E-VIPR-003 — Auth gate

```gherkin
Scenario: An admin without RegisterOnsite cannot open the page
  Given an Administrator without the "Visitors.RegisterOnsite" permission is signed in
  When I navigate to "/admin/visitors/vip"
  Then I am redirected to "/not-permitted"
```

### E2E-VIPR-004 — Validation: over-length Mawj / honorific

```gherkin
Scenario: Server rejects over-length Mawj extras
  Given I am on "/admin/visitors/vip"
  When I enter a Mawj ID longer than 64 characters
  And I submit an otherwise valid form
  Then the request is rejected with a bilingual validation error
```

### E2E-VIPR-005 — Created account is PendingApproval with no QR

```gherkin
Scenario: VIP walk-in is created pending, no QR at the desk
  Given I register a valid VIP guest
  Then the new SimfUser.AccountState is PendingApproval
  And UserProfile.QrId is empty
```

### E2E-VIPR-006 — Approval reuses the existing pending-visitors queue

```gherkin
Scenario: The VIP guest appears in the standard pending-visitors queue
  Given I registered a VIP guest who is PendingApproval
  When I open "/admin/visitors/pending"
  Then the guest appears in the queue with the VVIP/VIP tier
  And approving from there mints the QR (existing flow, D-386)
```

### E2E-VIPR-007 — RTL render

```gherkin
Scenario: Arabic UI renders right-to-left with no overflow
  Given the UI culture is "ar"
  When I open "/admin/visitors/vip"
  Then the VIP details section labels render in Arabic
  And scrollWidth == clientWidth (no horizontal overflow)
```

### E2E-VIPR-008 — The list shows the VIP/VVIP guests

```gherkin
Scenario: The page lists the VIP guests in a grid
  Given an Administrator holding Visitors.RegisterOnsite is signed in
  When I open "/admin/visitors/vip"
  Then POST /account/api/admin/vips/list returns 200
  And the grid renders columns: Name, Job title, Profile type, Email
  And every row is a VVIP / VIP guest (the /admin/vips/list subset)
  And an empty result renders the SimfEmptyState ("No VIPs match the filter.")
```

### E2E-VIPR-009 — New VIP opens the registration wizard

```gherkin
Scenario: New VIP toggles the page to the registration wizard, Cancel returns
  Given I am on "/admin/visitors/vip" with the grid loaded
  And the toolbar shows a "New VIP" (plus-icon) button
  When I click "New VIP"
  Then the grid is replaced by the "Register a VVIP / VIP" section
  And it hosts the VVIP/VIP registration wizard (picker restricted to VVIP/VIP)
  When I click Cancel
  Then the grid is shown again with no account created
  # Completing the wizard (E2E-VIPR-001) shows the "VIP registered." toast and reloads the grid.
```

### E2E-VIPR-010 — Per-row Edit changes the VIP's data

```gherkin
Scenario: Edit a VIP's name / tier / photo / ID from the list
  Given an Administrator holding Visitors.Edit is on "/admin/visitors/vip"
  And a VIP row "HRH Faisal" is listed
  When I click the row Edit (pencil) icon
  Then an "Edit VIP" modal opens hosting EditAccountForm (Scope=visitors, ShowVipPhoto=true)
  And it is pre-filled with the VIP's email, display name and current tier
  And a "Photo & ID" section shows Profile photo, ID document and VIP welcome photo inputs
  When I change the Display name and the Profile type (e.g. VIP → VVIP)
  And I pick a new PNG (< 2 MB) for "Profile photo"
  And I click Save
  Then PUT /account/api/admin/visitors/{UserId} fires first (email + name + tier)
  And then POST /account/api/admin/visitors/{UserId}/avatar returns 200
  And the modal closes with the "VIP updated." toast and the grid reloads
  # An ID image with no human face returns 400 VISITOR_ID_IMAGE_NO_FACE and the modal stays open.
```

### E2E-VIPR-011 — Add/Edit affordances are permission-gated

```gherkin
Scenario: An admin lacking the visitor permissions sees no New VIP or Edit
  Given a signed-in admin who can reach the page but holds neither
        Visitors.RegisterOnsite nor Visitors.Edit
  When I open "/admin/visitors/vip"
  Then the grid loads normally
  And the toolbar does NOT show the "New VIP" button
  And the rows do NOT show a per-row Edit (pencil) icon
  And the rows DO still show the "Details" action (D-835)
  # SimfDataGrid renders Add/Edit only when the callback HasDelegate; the page
  # wires them only when Authz.AuthorizeAsync succeeds for the respective policy.
  # Details is wired unconditionally: reading the row is what the page gate bought.
  # Before D-835 this admin saw no actions column at all - an empty box - and had
  # no way to open a single VIP record.
```

### E2E-VIPR-012 — Details opens the VIP without any mutating permission (D-835)

```gherkin
Scenario: A read-only admin opens one VIP record
  Given a signed-in admin who can reach the page but holds neither
        Visitors.RegisterOnsite nor Visitors.Edit
  And the list holds a VIP "Fahad Al-Otaibi" whose Arabic name is "فهد العتيبي"
        and whose Arabic job title is "مدير عام"
  When I open "/admin/visitors/vip" and click "Details" on that row
  Then a read-only dialog opens titled with the VIP's display name
  And it shows Name, Name (AR), Job title, Job title (AR), Profile type and Email
  And the Arabic name and Arabic job title are visible here and in no grid column
  And NO request fires - the dialog renders from the row the grid already holds
  And there is no Save, Delete or any other committing control in the dialog
  When I close the dialog
  Then the grid is unchanged and still shows every row
```
