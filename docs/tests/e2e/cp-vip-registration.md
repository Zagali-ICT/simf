# E2E test catalogue — `VIP registration` (`/admin/visitors/vip`)

> **Authority:** SIMF E2E test catalogue (D-133 / D-245). VVIP/VIP feature D-429 (V-2).

| | |
|--|--|
| **Page** | [`vip-registration.md`](../../pages/cp/vip-registration.md) |
| **Route** | `/admin/visitors/vip` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell driver _(or: Playwright when adopted)_ |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via `Get-Totp` helper |
| **Last reviewed** | 2026-06-15 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-VIPR-001 | Golden path — register a VVIP with Mawj data + photo | happy | P0 | _to author_ |
| E2E-VIPR-002 | Picker restricted to VVIP / VIP only | happy | P0 | _to author_ |
| E2E-VIPR-003 | Auth gate (no RegisterOnsite permission → /not-permitted) | auth | P0 | _to author_ |
| E2E-VIPR-004 | Validation — Mawj/honorific over max length | error | P1 | _to author_ |
| E2E-VIPR-005 | Created account lands PendingApproval, no QR | happy | P0 | _to author_ |
| E2E-VIPR-006 | Approval reuses the existing pending-visitors queue | happy | P0 | _to author_ |
| E2E-VIPR-007 | RTL render (Arabic) | i18n | P1 | _to author_ |

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
  And I pick a Saudi nationality with national id "1234567890"
  And I pick an organisation and enter mobile "+966500000001"
  And I attach a clear VIP welcome photo (JPEG, < 2 MB)
  And I submit
  Then the success modal shows the new account with no QR (pending)
  And the UserProfile row carries MawjId, Honorific, PreferredLanguage
  And UserProfile.VipPhotoRelativePath is set
  And the account state is PendingApproval
```

**Evidence captured:** screenshot before/after; console 0 errors; network 0 failed; `OperationLog` rows `UserProfile.VipPhotoUploaded` + walk-in register.

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
