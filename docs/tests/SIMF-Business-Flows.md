# SIMF Business-Flow E2E Journeys

| | |
|--|--|
| **Title** | SIMF Business-Flow E2E Journeys — cross-page production-readiness scenarios |
| **Status** | Living catalogue (not a controlled `SIMF-XXX-NNN` deliverable) |
| **Parent** | [`SIMF-Production-Readiness-Round1.md`](SIMF-Production-Readiness-Round1.md) (the round charter) · [`SIMF-TST-001-Test-Plan.md`](../SIMF-TST-001-Test-Plan.md) (strategy) |
| **Companions** | [`e2e/README.md`](e2e/README.md) (per-page catalogue) · [`SIMF-Production-Readiness-TestBook.xlsx`](SIMF-Production-Readiness-TestBook.xlsx) (tester workbook) |
| **Created** | 2026-07-11 |

> **What this is.** The per-page E2E catalogue under [`e2e/`](e2e/) proves each
> page works in isolation. These **15 business flows** prove the system
> works **end-to-end across pages and surfaces** — the real journeys a delegate,
> an operator, a moderator and an administrator take (bulk onboarding → badge
> activation → gate scan → attendance → Q&A → reminder → rating → close-the-year).
> Each flow is Gherkin, grounded in the real routes / endpoints / error-codes /
> rules, and was **adversarially verified** against the source (every route,
> endpoint, error code, enum value and audit event was checked in code; several
> first-draft fabrications were caught and corrected — see the `Verified` column).
> **No literal secret appears** — admin TOTP uses the `Get-Totp` helper and
> visitor/badge OTPs are read from `SIMF_Identity.AccountCodes` at run time.

## Master index

| Flow | Title | Scenarios | Range | Verified |
|------|-------|-----------|-------|----------|
| [BF-01](#bf-01-bulk-delegation-onboarding--badge-activation) | Bulk delegation onboarding → badge activation | 13 | `E2E-BF-01-001..013` | ✓ |
| [BF-02](#bf-02-vvip--vip--normal-single-registration--approve--vip-roster) | VVIP / VIP / Normal single registration → approve → VIP roster | 12 | `E2E-BF-02-001..012` | ✓ (corrected) |
| [BF-03](#bf-03-staff--moderator-other-accounts--profile-scoped-gates--scan) | Staff & Moderator (Other) accounts + profile-scoped gates + scan | 13 | `E2E-BF-03-001..013` | ✓ |
| [BF-04](#bf-04-halls-per-purpose--seat-layout) | Halls per purpose + seat layout | 14 | `E2E-BF-04-001..014` | ✓ (corrected) |
| [BF-05](#bf-05-session-booking--approve--hall-door-arrival--live-now) | Session booking → approve → hall-door arrival → live-now | 12 | `E2E-BF-05-001..012` | ✓ (corrected) |
| [BF-06](#bf-06-meeting-requests--cp-review-desks) | Meeting requests → CP review desks | 14 | `E2E-BF-06-001..014` | ✓ (corrected) |
| [BF-07](#bf-07-qa-pipeline--pre--live-timing-gates-ai-advisory--committee--moderator-desk) | Q&A pipeline — pre + live, timing gates, AI-advisory + committee + moderator desk | 13 | `E2E-BF-07-001..013` | ✓ |
| [BF-08](#bf-08-session-reminder--rating-triggers-leave-hall--end-of-session--end-of-day--end-of-programme) | Session reminder + rating triggers (leave-hall / end-of-session / end-of-day / end-of-programme) | 12 | `E2E-BF-08-001..012` | ✓ (corrected) |
| [BF-09](#bf-09-close-the-year--snapshot-to-archive-history-this-year) | Close the year / snapshot-to-archive ("history this year") | 12 | `E2E-BF-09-001..012` | ✓ |
| [BF-10](#bf-10-full-control-panel-smoke--no-dead-button-no-crash-permission-gate) | Full Control-Panel smoke — no dead button, no crash, permission gate | 13 | `E2E-BF-10-001..013` | ✓ |
| [BF-11](#bf-11-full-mobile-app-smoke--every-screen-every-role-no-crash) | Full mobile-App smoke — every screen, every role, no crash | 14 | `E2E-BF-11-001..014` | ✓ (corrected) |
| [BF-12](#bf-12-website-smoke--auth-flows) | Website smoke + auth flows | 14 | `E2E-BF-12-001..014` | ✓ (corrected) |
| [BF-13](#bf-13-permission--security-matrix) | Permission / security matrix | 12 | `E2E-BF-13-001..012` | ✓ |
| [BF-14](#bf-14-bilingual--rtl-sweep) | Bilingual / RTL sweep | 13 | `E2E-BF-14-001..013` | ✓ (corrected) |
| [BF-15](#bf-15-notification-kind--icon--group--deep-link-inventory) | Notification-kind → icon / group / deep-link inventory | 13 | `E2E-BF-15-001..013` | ✓ (corrected) |

**Total: 194 business-flow scenarios across 15 journeys.**

BF-01..BF-09 are feature journeys (Excel sheet *02 · Business Flows*); BF-10..BF-15 are the cross-cutting sweeps — full-CP / full-App smoke, Website smoke, the permission/security matrix, the bilingual/RTL sweep, and the notification-kind inventory (Excel sheet *06 · Cross-cutting*).

---

## BF-01 — Bulk delegation onboarding → badge activation

This cross-page flow runs from the **Control Panel** delegates desk to the **mobile app** badge-activation screen. An administrator opens `/admin/delegates` (`Components/Pages/Admin/DelegatesPage.razor`, page-gated `PermissionCatalog.Visitors.RegisterOnsite`) and uses the bulk-generate panel (nested `AuthorizedAction Permission="Visitors.BulkGenerate"`) to mint a whole delegation of placeholder badges in one request: the CP proxies `POST /account/api/admin/visitors/bulk-generate` to the backend `POST /api/v1/admin/visitors/bulk-generate` (`BulkGenerateVisitorBadgesEndpoint`, `src/Backend/SIMF.Api/Endpoints/Admin/VisitorBulkEndpoints.cs`), which calls `AdminAccountService.BulkGenerateBadgesAsync` (`src/Backend/SIMF.Infrastructure/Identity/AdminAccountService.Bulk.cs`). One numeric count field is rendered **per active audience ProfileType** (`p.IsActive && p.IsVisitor`); the admin sends a list of `BulkBadgeBatch{ProfileTypeId, Count}` plus one `_bulkIsDelegate` checkbox (default **true**) that stamps `IsDelegate` on every generated `UserProfile`. Each badge becomes an `AccountState=Approved`, `UserType=Visitor` account with a synthesized `badge-{guid:N}@simf.local` login, `EmailConfirmed=true`, `DisplayName="{ProfileType.Name} #{n}"`, **no password**, `NationalityId=0`, and a freshly minted QR (`qrIdMinter.MintIfMissingAsync`). The service enforces: **at least one positive batch**, a hard **`MaxPerRequest` = 1000** cap, and that each chosen ProfileType is `IsActive` **and** `IsForVisitor==true` (partner / elevated types are refused as a least-privilege guard). Later the holder claims a printed badge from the app (`lib/features/account/badge_activation_screen.dart`): `POST /api/v1/app/auth/resolve-badge` → `POST /api/v1/app/auth/badge-activation/start` → `POST /api/v1/app/auth/badge-activation/complete` → `POST /api/v1/app/auth/badge-sign-in` (`BadgeAuthEndpoints.cs` / `BadgeAuthService.cs`). Tiers (VVIP / VIP / Normal / Gold) are **ProfileType rows, not an enum**, and there is **no `/admin/delegations` page** — bulk generation lives on `/admin/delegates`.

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-BF-01-001 | Golden journey: bulk-generate the China delegation (1 VVIP + 5 VIP + 50 Normal) → holder scans a Normal badge → activate (supply email → code → first password) → badge sign-in | happy | P0 |
| E2E-BF-01-002 | Second, smaller delegation (Ministry of Interior: 3 VIP + 7 Normal) mints 10 badges with per-tier display names | happy | P1 |
| E2E-BF-01-003 | Permission gate: admin without `Visitors.BulkGenerate` cannot see or call the bulk panel; page gate `Visitors.RegisterOnsite` blocks the whole desk | auth | P0 |
| E2E-BF-01-004 | "At least one positive batch" rule: all counts 0 / empty batches → `VALIDATION_FAILED` | error | P1 |
| E2E-BF-01-005 | `MaxPerRequest` cap: batches summing to 1001 → `VALIDATION_FAILED` "At most 1000 badges…" | error | P1 |
| E2E-BF-01-006 | Least-privilege guard: a partner / elevated ProfileType (`IsForVisitor==false`) is refused → `ADMIN_PROFILE_TYPE_INVALID` | error | P0 |
| E2E-BF-01-007 | Inactive / unknown ProfileType id → `ADMIN_PROFILE_TYPE_INVALID` "The selected profile type is not valid." | error | P2 |
| E2E-BF-01-008 | Badge already activated: a badge that already has a password → activation start → `BADGE_ALREADY_ACTIVATED` (409) | error | P1 |
| E2E-BF-01-009 | Activation code expired / wrong: complete with an expired or wrong code → `AUTH_RESET_CODE_EXPIRED` / `AUTH_RESET_CODE_INVALID` (5-attempt cap) | error | P1 |
| E2E-BF-01-010 | Badge sign-in with an unknown / passwordless QR is indistinguishable from a wrong password → `AUTH_INVALID_CREDENTIALS` (401) | auth | P1 |
| E2E-BF-01-011 | Email collision on activation: holder supplies an email already used by another account → `AUTH_EMAIL_ALREADY_REGISTERED` (409) | error | P2 |
| E2E-BF-01-012 | Resilience: no cross-store transaction (D-157) — a mid-loop failure can leave a `SimfUser` (Identity) without a `UserProfile` (App) | resilience | P2 |
| E2E-BF-01-013 | RTL / bilingual render: CP bulk panel + app activation screen in Arabic; bilingual `SimfAlert` errors | i18n | P1 |

### Scenarios

### E2E-BF-01-001 — Golden journey (bulk mint → activate → sign in)

```gherkin
Feature: Bulk delegation onboarding then badge activation
  Background:
    Given the API is reachable on http://localhost:5175
    And the Control Panel is reachable on http://localhost:5158
    And an Administrator is signed in (password + TOTP via the Get-Totp helper)
    And the audience ProfileTypes "VVIP", "VIP" and "Normal" exist with IsActive=true and IsForVisitor=true

  Scenario: Mint the China delegation and let a member claim their badge
    Given the administrator opens /admin/delegates
    And the bulk-generate panel renders one count field per active audience ProfileType
    And the "Mark as delegation members" checkbox (_bulkIsDelegate) is checked by default
    When they enter VVIP=1, VIP=5, Normal=50
    And they click "Generate badges"
    Then the CP posts /account/api/admin/visitors/bulk-generate with IsDelegate=true
    And the backend POST /api/v1/admin/visitors/bulk-generate returns ApiResult.Ok
    And AdminBulkGenerateBadgesResponse.Created = 56
    And a bilingual SimfAlert success reports 56 badges generated
    And every new SimfUser has AccountState=Approved, UserType=Visitor, EmailConfirmed=true, a badge-{guid}@simf.local login and NO password
    And every new UserProfile has IsDelegate=true, NationalityId=0 and a minted QR id
    And an OperationLog row records AdminBulkBadgesGenerated for the actor with created=56; isDelegate=True

    Given a delegate holds one printed "Normal" badge for that batch
    When the mobile app posts /api/v1/app/auth/resolve-badge with that badge's QrId
    Then ResolveBadgeResponse.Found=true, HasPassword=false, NeedsEmail=true, MaskedEmail=null
    And the app shows the activation screen with the holder's DisplayName (e.g. "Normal #7")
    When the holder enters "khalid.demo@example.com" and the app posts /api/v1/app/auth/badge-activation/start
    Then a 6-digit code is emailed and BadgeActivationStartResponse returns the masked email + CodeExpiresInSeconds=600
    When the tester reads the latest BadgeActivationOtp code from SIMF_Identity.AccountCodes for that user
    And the app posts /api/v1/app/auth/badge-activation/complete with that code + a first password that meets policy
    Then BadgeActivationCompleteResponse.Activated=true and the account's email is now confirmed
    When the app posts /api/v1/app/auth/badge-sign-in with the QrId + that password
    Then a standard SignInResponse (tokens or the 2FA challenge) is returned and the holder is signed in
```

### E2E-BF-01-002 — Second, smaller delegation

```gherkin
Scenario: Ministry of Interior delegation mints 10 badges
  Given the administrator is on /admin/delegates
  When they enter VIP=3, Normal=7 and keep _bulkIsDelegate checked
  And they click "Generate badges"
  Then AdminBulkGenerateBadgesResponse.Created = 10
  And the generated display names follow "{ProfileType.Name} #{n}" using the request-wide running counter
  And each of the 10 UserProfiles carries IsDelegate=true and its own minted QR
  And the visitor walk-in flow above the panel is untouched (the delegates desk is a separate page)
```

### E2E-BF-01-003 — Permission gate

```gherkin
Scenario: The bulk panel is hidden and refused without Visitors.BulkGenerate
  Given a signed-in admin whose role grants Visitors.RegisterOnsite but NOT Visitors.BulkGenerate
  When they open /admin/delegates
  Then the single-delegate registration form renders
  But the bulk-generate panel is not rendered (wrapped in AuthorizedAction Permission="Visitors.BulkGenerate")
  When they replay POST /api/v1/admin/visitors/bulk-generate directly
  Then the API returns HTTP 403 (endpoint Policies gate = Visitors.BulkGenerate + RequireApprovedAccount)

Scenario: The whole delegates desk is gated by Visitors.RegisterOnsite
  Given a signed-in admin whose role grants neither permission
  When they navigate to /admin/delegates
  Then they are redirected to /not-permitted with HTTP 200
```

### E2E-BF-01-004 — At least one positive batch

```gherkin
Scenario: Empty / all-zero batches are rejected
  Given the administrator is on /admin/delegates
  When they leave every count at 0 (or send no batches with Count > 0)
  And they click "Generate badges"
  Then the backend returns ApiResult.Error with Code = "VALIDATION_FAILED" (HTTP 400)
  And a bilingual SimfAlert error reads "Provide at least one batch with a positive count." / "أدخل دفعة واحدة على الأقل بعدد موجب."
  And no badge accounts are created
```

### E2E-BF-01-005 — MaxPerRequest cap (1000)

```gherkin
Scenario: A request over the 1000-badge cap is rejected wholesale
  Given the administrator is on /admin/delegates
  When they enter VVIP=1 and Normal=1000 (sum = 1001)
  And they click "Generate badges"
  Then the backend returns ApiResult.Error with Code = "VALIDATION_FAILED" (HTTP 400)
  And a bilingual SimfAlert error reads "At most 1000 badges can be generated per request."
  And zero badges are created (the cap is checked before the mint loop starts)
```

### E2E-BF-01-006 — Partner / elevated ProfileType refused (least-privilege)

```gherkin
Scenario: A non-visitor ProfileType cannot be bulk-generated
  Given a ProfileType "Media Partner" exists with IsActive=true and IsForVisitor=false
  And a caller submits a batch { ProfileTypeId = <Media Partner id>, Count = 10 } to POST /api/v1/admin/visitors/bulk-generate
  When the backend processes that batch
  Then it returns ApiResult.Error with Code = "ADMIN_PROFILE_TYPE_INVALID" (HTTP 400)
  And a bilingual error reads "Bulk-generate is only available for audience (visitor) profile types."
  And no badge is created for that batch
  # Note: the CP panel only renders count fields for IsForVisitor==true types, so this is an API-level guard against a smuggled ProfileTypeId.
```

### E2E-BF-01-007 — Inactive / unknown ProfileType id

```gherkin
Scenario: An inactive or unknown ProfileType id is rejected
  Given a batch references a ProfileTypeId that is not IsActive (or does not exist)
  When POST /api/v1/admin/visitors/bulk-generate processes it
  Then it returns ApiResult.Error with Code = "ADMIN_PROFILE_TYPE_INVALID" (HTTP 400)
  And a bilingual error reads "The selected profile type is not valid." / "نوع الملف الشخصي المحدّد غير صالح."
```

### E2E-BF-01-008 — Badge already activated

```gherkin
Scenario: Starting activation on a badge that already has a password is refused
  Given a badge account that has already completed activation (it now has a password)
  When the app posts /api/v1/app/auth/badge-activation/start for that QrId
  Then the API returns ApiResult.Error with Code = "BADGE_ALREADY_ACTIVATED" (HTTP 409)
  And the message reads "This account already has a password. Sign in with your email and password."
  And the app routes the holder to normal badge sign-in instead
```

### E2E-BF-01-009 — Activation code expired / wrong

```gherkin
Scenario: An expired activation code is rejected
  Given a badge holder started activation more than 10 minutes ago (CodeLifetime elapsed)
  When they post /api/v1/app/auth/badge-activation/complete with the now-expired code
  Then the API returns ApiResult.Error with Code = "AUTH_RESET_CODE_EXPIRED" (HTTP 400)
  And the message reads "The verification code has expired. Request a new one."

Scenario: A wrong code is rejected and burns an attempt
  Given a badge holder has a valid, unconsumed activation code
  When they post /badge-activation/complete with an incorrect 6-digit code
  Then the API returns ApiResult.Error with Code = "AUTH_RESET_CODE_INVALID" (HTTP 400)
  And the AccountCode AttemptCount is incremented
  When they submit 5 wrong codes in total
  Then every further attempt on that code returns "AUTH_RESET_CODE_INVALID" (MaxAttempts = 5)
```

### E2E-BF-01-010 — Badge sign-in with unknown / passwordless QR

```gherkin
Scenario: An unknown badge is indistinguishable from a wrong password
  Given a QrId that resolves to no approved account (or a passwordless badge that has not been activated)
  When the app posts /api/v1/app/auth/badge-sign-in with that QrId + any password
  Then the API returns ApiResult.Error with Code = "AUTH_INVALID_CREDENTIALS" (HTTP 401)
  And the message reads "The email address or password is not correct." (generic, no valid-QR oracle)
  And a SignInBadCredentials audit row is written with Detail = "badge"
```

### E2E-BF-01-011 — Email collision on activation

```gherkin
Scenario: A holder tries to attach an email already in use
  Given a placeholder badge (badge-{guid}@simf.local, NeedsEmail=true) is being activated
  And another distinct account already owns "khalid.demo@example.com"
  When the app posts /api/v1/app/auth/badge-activation/start with Email="khalid.demo@example.com"
  Then the API returns ApiResult.Error with Code = "AUTH_EMAIL_ALREADY_REGISTERED" (HTTP 409)
  And the message reads "That email address is already in use." / "البريد الإلكتروني مستخدم بالفعل."
  And no verification code is emailed
```

### E2E-BF-01-012 — Resilience: no cross-store transaction (orphan risk)

```gherkin
Scenario: A mid-loop failure can leave an Identity user without an App profile
  Given a bulk-generate request for VVIP=1 + Normal=50
  And the App DB write for one badge's UserProfile fails part-way through the loop
  When the request aborts
  Then the SimfUser rows already committed to SIMF_Identity remain (each badge is created then saved individually — no distributed transaction, D-157)
  And the failing badge may have a SimfUser with NO matching UserProfile in SIMF_App
  And the response surfaces the partial count / error while the already-minted badges stay valid
  # Tester assertion: count SimfUsers with UserName like 'badge-%@simf.local' that have no UserProfile row, and flag any as an onboarding-resilience gap.
```

### E2E-BF-01-013 — RTL / bilingual render

```gherkin
Scenario: The bulk panel and activation screen mirror correctly in Arabic
  Given the Control Panel language is set to العربية
  When the administrator opens /admin/delegates
  Then the page renders with dir="rtl"
  And the bulk-generate section title, per-tier count labels and the "Mark as delegation members" checkbox are Arabic
  And a forced validation error renders as a bilingual SimfAlert (Arabic copy present)

  Given the mobile app locale is Arabic
  When a holder opens the badge activation screen (badge_activation_screen.dart)
  Then the screen is RTL and its labels, hint text and error toasts are Arabic
```

### Notes

- **No `/admin/delegations` page exists** — bulk generation is a panel on `/admin/delegates` (`DelegatesPage.razor`); a delegate is an ordinary visitor with `IsDelegate=true`. Do not look for a separate delegations route.
- **Tiers are data, not an enum.** VVIP / VIP / Normal / Gold are `ProfileType` rows; the panel renders one count field per `IsActive && IsVisitor` type, so the exact tier names in a run depend on the seeded ProfileTypes. The China / Ministry-of-Interior counts above are illustrative test data.
- **No literal secrets.** Admin TOTP comes from the `Get-Totp` helper; the badge-activation OTP is read at run time from `SIMF_Identity.AccountCodes` (`Purpose = BadgeActivationOtp`, 10-minute lifetime, 5-attempt cap) — never a hard-coded code.
- **Display-name numbering is request-wide.** `DisplayName = "{ProfileType.Name} #{n}"` uses a single running counter across all batches in the request, not a per-tier counter, so a 1×VVIP + 5×VIP run yields "VVIP #1", "VIP #2".."VIP #6".
- **Known resilience gap (E2E-BF-01-012).** `BulkGenerateBadgesAsync` writes each `SimfUser` (Identity) then its `UserProfile` (App) with **no cross-store transaction** (D-157 forbids one); a mid-loop failure can orphan the last Identity user. This is the accepted walk-in trade-off, but the tester should record any orphaned `badge-%@simf.local` users as a production-readiness finding.
- **Badge QR is not an enumeration oracle.** `resolve-badge` and `badge-sign-in` are `AllowAnonymous` + rate-limited (`auth` / `auth-email`); the ~60-bit QR entropy plus the generic `AUTH_INVALID_CREDENTIALS` response mean a scanned/guessed QR never confirms account existence.
- **Backend + CP redeploy required.** Per project memory these surfaces need a fresh deploy before this flow can be exercised against production; a debug app build defaults to the production API.

---

## BF-02 — VVIP / VIP / Normal single registration → approve → VIP roster

This business flow drives an on-site registration end to end across the Control Panel and the admin API: a desk clerk registers a single visitor at one of two entry points — the regular walk-in desk (`/admin/visitors/new`, `CreateVisitor.razor` → `CreateVisitorForm` in normal mode; list at `/admin/visitors`, `VisitorsList.razor`) or the VVIP/VIP desk (`/admin/visitors/vip`, `VipRegistration.razor`, D-429, which hosts the same `CreateVisitorForm` with `VipMode=true`). Both POST `/api/v1/admin/visitors/register-onsite` (`RegisterVisitorOnSiteEndpoint`, `AdminAccountService.RegisterOnSiteAsync`, `expectedIsVisitor: true`), which creates the account in `AccountState=PendingApproval` with **no QR badge** (D-425; email optional, synthesized `walkin-{guid}@simf.local`). An administrator then clears it from the pending queue (`/admin/visitors/pending`, `PendingVisitors.razor` → `POST /api/v1/admin/visitors/{id:guid}/approve`, `ApproveVisitorEndpoint`), which mints the QR and **applies the tier** from the optional `ProfileTypeId` (D-386). VVIP/VIP visitors then surface on the roster (`/admin/vips`, `VipsList.razor`) and the موج (Mawj) welcome export (`/admin/visitors/vip/export`, `VipExport.razor` → JSON `/account/api/admin/visitors/vip/roster`, CSV/Excel `…/vip/roster/export?format=csv|xlsx`). The flow exercises these rules: the VIP-mode picker is restricted to the `{VVIP, VIP}` ProfileType names; the desk-scope guard rejects a partner-scope ProfileType with `AdminProfileTypeInvalid` (400); Saudi National ID `^1\d{9}$` vs Iqama `^2\d{9}$`; required organisation typeahead; nationality ISO → `Country` PK; and the permission gates — the CP VIP desk (`/admin/visitors/vip`) + the register-onsite API by `Visitors.RegisterOnsite`, the normal walk-in desk page (`/admin/visitors/new`) by `Visitors.Create`, approval by `Visitors.Approve`, and the موج export by `Visitors.ExportVip`.

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-BF-02-001 | Golden journey: register a Normal visitor → PendingApproval (no QR) → approve with tier → QR minted | happy | P0 |
| E2E-BF-02-002 | Register a VVIP on the VIP desk with موج (Mawj) fields + welcome photo → approve → appears on roster + export | happy | P0 |
| E2E-BF-02-003 | VIP-desk picker is restricted to VVIP / VIP tiers only (no audience/Normal type offered) | happy | P1 |
| E2E-BF-02-004 | Desk-scope guard: a partner-scope ProfileType is rejected with `AdminProfileTypeInvalid` | error | P1 |
| E2E-BF-02-005 | ID validation: Saudi National ID must match `^1\d{9}$`, non-Saudi Iqama `^2\d{9}$` | error | P1 |
| E2E-BF-02-006 | Required organisation missing + unknown nationality ISO → `ProfileNationalityUnknown` | error | P1 |
| E2E-BF-02-007 | Conflict: registering a second account with an email already in use → `AdminEmailAlreadyRegistered` | error | P1 |
| E2E-BF-02-008 | Auth gates: CP pages (`Visitors.RegisterOnsite` / `Visitors.Create`) + API (`Visitors.RegisterOnsite` / `Visitors.Approve` / `Visitors.ExportVip`) each enforced | auth | P0 |
| E2E-BF-02-009 | No QR badge exists before approval; the QR is minted only on approve (D-386) | happy | P1 |
| E2E-BF-02-010 | VIP roster export: CSV + Excel (xlsx) + JSON API all download the موج welcome data | happy | P2 |
| E2E-BF-02-011 | Server 500 on register-onsite → bilingual fallback error, form preserved, no partial account | resilience | P2 |
| E2E-BF-02-012 | RTL / bilingual render of the VIP registration form + roster (Arabic tier pill) | i18n | P1 |

### Scenarios

### E2E-BF-02-001 — Golden journey (Normal visitor: register → pending → approve → QR)

```gherkin
Feature: Single on-site registration then approval mints the badge
  Background:
    Given the API is reachable on http://localhost:5175
    And the Control Panel is reachable on http://localhost:5158
    And an Administrator is signed in (super-admin, TOTP via Get-Totp)

  Scenario: A clerk registers one Saudi visitor and an admin approves it
    Given the clerk opens /admin/visitors/new
    When they select an audience ProfileType "Normal"
    And they fill EnglishName="Faisal Al-Otaibi"
    And they fill ArabicName="فيصل العتيبي"
    And they fill DisplayName="Faisal"
    And they pick Organisation="Royal Saudi Naval Forces" from the required typeahead
    And they keep "Saudi" selected and fill NationalId="1023456789"
    And they fill SaudiMobile="0512345678"
    And they set DateOfBirth="1988-04-12" and Gender="Male"
    And they click "Register"
    Then POST /api/v1/admin/visitors/register-onsite returns ApiResult.Success = true
    And the created account is in AccountState=PendingApproval
    And the success view shows NO QR badge (D-425)
    And an OperationLog row records the on-site registration with the actor id

    When the admin opens /admin/visitors/pending
    Then the new visitor "Faisal Al-Otaibi" appears in the pending queue
    When they approve it, optionally confirming tier ProfileTypeId="Normal"
    Then POST /api/v1/admin/visitors/{id}/approve returns ApiResult.Success = true
    And the account flips to AccountState=Approved
    And a QR badge id is now minted for the visitor (D-386)
    And the visitor no longer appears in /admin/visitors/pending
    And they appear in /admin/visitors
```

### E2E-BF-02-002 — VVIP registration with موج (Mawj) welcome data → roster + export

```gherkin
Scenario: A VVIP dignitary is registered on the VIP desk and reaches the Mawj roster
  Given an Administrator opens /admin/visitors/vip
  And the page hosts CreateVisitorForm with VipMode=true
  When they select tier "VVIP"
  And they fill EnglishName="Lt. Gen. Abdullah Al-Qahtani"
  And they fill ArabicName="الفريق عبدالله القحطاني"
  And they fill DisplayName="Abdullah Al-Qahtani"
  And they fill Honorific="His Excellency"
  And they fill MawjId="MAWJ-2026-0007"
  And they set PreferredLanguage="Arabic"
  And they pick Organisation="Ministry of Defense"
  And they keep "Saudi" and fill NationalId="1055667788"
  And they fill SaudiMobile="0501234567"
  And they attach a VIP welcome photo (walkin-vip-photo)
  And they click "Register"
  Then POST /api/v1/admin/visitors/register-onsite returns Success = true
  And the vip-photo is uploaded to {basePath}/{userId}/vip-photo after the account is created
  And the account is PendingApproval with no QR yet

  When an admin approves it from /admin/visitors/pending with ProfileTypeId="VVIP"
  Then the QR badge is minted and the tier is set to VVIP
  When the admin opens /admin/vips
  Then "Lt. Gen. Abdullah Al-Qahtani" appears on the VIP roster
  When the admin opens /admin/visitors/vip/export
  Then the SimfDataGrid row shows the VVIP tier pill, Honorific="His Excellency",
       MawjId="MAWJ-2026-0007", PreferredLanguage and the welcome photo thumbnail
```

### E2E-BF-02-003 — VIP-desk picker restricted to VVIP / VIP tiers

```gherkin
Scenario: The VIP registration page only offers the VVIP and VIP tiers
  Given an Administrator opens /admin/visitors/vip
  When the ProfileType picker loads (fetched with userType=Visitor, IsVisitor=true)
  Then only ProfileTypes whose Name is in {VVIP, VIP} are offered
  And no ordinary audience/Normal ProfileType is selectable
  And if exactly one VIP tier is seeded it is preselected automatically
  And the موج (Mawj) fields — Mawj ID, Honorific, Preferred language — and the
      VIP welcome-photo input are visible (they are hidden on the regular desk)
```

### E2E-BF-02-004 — Desk-scope guard rejects a partner-scope ProfileType

```gherkin
Scenario: Submitting a partner-scope (IsVisitor=false) ProfileType is rejected
  Given the register-onsite endpoint is called with expectedIsVisitor=true
  And a caller crafts a request whose ProfileTypeId points at a PARTNER-scope
      ProfileType (e.g. an Exhibitor/"Other" audience type, IsVisitor=false)
  When POST /api/v1/admin/visitors/register-onsite is submitted
  Then the API returns HTTP 400
  And ApiResult.Error.Code = "AdminProfileTypeInvalid"
  And no account is created
  And the CP surfaces a bilingual SimfAlert error at the top of the form
```

### E2E-BF-02-005 — National ID / Iqama format validation

```gherkin
Scenario Outline: The ID field is validated against the Saudi/Iqama regex
  Given the clerk is on /admin/visitors/new
  And "Saudi" toggle is "<saudi>"
  When they fill the "<field>" with "<value>"
  And they click "Register"
  Then the "<field>" shows the inline "<outcome>" and the request is <sent>

  Examples:
    | saudi | field          | value       | outcome                       | sent     |
    | yes   | NationalId     | 1023456789  | accepted (matches ^1\d{9}$)   | sent     |
    | yes   | NationalId     | 9023456789  | rejected (must start with 1)  | not sent |
    | yes   | NationalId     | 10234       | rejected (must be 10 digits)  | not sent |
    | no    | IqamaNumber    | 2033445566  | accepted (matches ^2\d{9}$)   | sent     |
    | no    | IqamaNumber    | 1033445566  | rejected (Iqama starts with 2)| not sent |

  # A non-Saudi may instead supply a Passport (<=20 chars); switching the ID
  # sub-picker clears the other ID field so no stale value reaches the server.
```

### E2E-BF-02-006 — Required organisation + unknown nationality ISO

```gherkin
Scenario: Missing organisation blocks submit
  Given the clerk fills every field on /admin/visitors/new except Organisation
  When they click "Register"
  Then an inline error appears under the organisation typeahead
  And POST /api/v1/admin/visitors/register-onsite is NOT sent

Scenario: An unknown nationality ISO is rejected by the server
  Given the clerk selects "non-Saudi" and submits NationalityCode="ZZ"
  When POST /api/v1/admin/visitors/register-onsite is submitted
  Then the API returns HTTP 400
  And ApiResult.Error.Code = "ProfileNationalityUnknown"
  And a bilingual SimfAlert error is shown
```

### E2E-BF-02-007 — Duplicate email conflict

```gherkin
Scenario: Registering a second visitor with an already-used email conflicts
  Given a visitor already exists with email "faisal.otaibi@example.com"
  When the clerk registers another visitor and fills the same email
  And clicks "Register"
  Then POST /api/v1/admin/visitors/register-onsite returns a conflict
  And ApiResult.Error.Code = "AdminEmailAlreadyRegistered"
  And no second account is created
  And a bilingual SimfAlert error is shown; the form fields are preserved

  # Email is optional at the desk — leaving it blank synthesizes
  # walkin-{guid}@simf.local and never collides.
```

### E2E-BF-02-008 — Permission gates (register / approve / export)

```gherkin
Scenario: The VIP register-onsite page requires Visitors.RegisterOnsite
  Given a signed-in admin WITHOUT the "Visitors.RegisterOnsite" permission
  When they navigate to /admin/visitors/vip
  Then they are redirected to /not-permitted with HTTP 200
  And a direct POST /api/v1/admin/visitors/register-onsite returns HTTP 403

Scenario: The normal walk-in desk page requires Visitors.Create
  Given a signed-in admin WITHOUT the "Visitors.Create" permission
  When they navigate to /admin/visitors/new
  Then they are redirected to /not-permitted with HTTP 200

Scenario: Approval requires Visitors.Approve
  Given a signed-in admin WITHOUT the "Visitors.Approve" permission
  When they POST /api/v1/admin/visitors/{id}/approve
  Then the API returns HTTP 403 and no tier/QR change occurs

Scenario: The VIP export requires Visitors.ExportVip
  Given a signed-in admin WITHOUT the "Visitors.ExportVip" permission
  When they open /admin/visitors/vip/export
  Then they are redirected to /not-permitted with HTTP 200
```

### E2E-BF-02-009 — No QR before approval (D-386)

```gherkin
Scenario: A pending visitor has no badge until an admin approves
  Given a visitor was just registered via /admin/visitors/new
  And the account is in AccountState=PendingApproval
  Then no QR badge id is issued yet and the success view offers no badge to print
  And the visitor cannot pass a gate scan
  When an admin approves the visitor from /admin/visitors/pending
  Then the QR badge id is minted at that moment
  And only then is the badge printable / scannable
```

### E2E-BF-02-010 — VIP roster export (CSV + Excel + JSON)

```gherkin
Scenario: The Mawj welcome roster exports in all three formats
  Given at least one approved VVIP and one approved VIP exist
  And an Administrator with Visitors.ExportVip opens /admin/visitors/vip/export
  When they click "Download CSV"
  Then GET /account/api/admin/visitors/vip/roster/export?format=csv downloads a CSV
       containing the tier, English/Arabic name, Honorific, MawjId,
       PreferredLanguage, Email, Mobile and Reference number columns
  When they click "Download Excel"
  Then GET /account/api/admin/visitors/vip/roster/export?format=xlsx downloads an .xlsx
  When they click "API (JSON)"
  Then GET /account/api/admin/visitors/vip/roster returns the roster as JSON
  And the on-screen SimfDataGrid shows the same rows (VVIP pill = "on", VIP = neutral)
  And when no VVIP/VIP exist the grid shows the SimfEmptyState (no error toast)
```

### E2E-BF-02-011 — Server-500 resilience on register-onsite

```gherkin
Scenario: A backend failure during registration surfaces cleanly
  Given the register-onsite call fails (HTTP 500 / InternalError)
  When the clerk clicks "Register" on /admin/visitors/vip
  Then a bilingual SimfAlert fallback error is shown at the top of the form
  And the entered field values (names, org, IDs, Mawj data) are preserved for retry
  And no partial/orphan account is committed (RegisterOnSiteAsync is one transaction)
  And the deferred vip-photo / ID-document uploads are not attempted
```

### E2E-BF-02-012 — RTL / bilingual render

```gherkin
Scenario: The VIP registration and roster render correctly in Arabic
  Given the UI culture is Arabic
  When an Administrator opens /admin/visitors/vip
  Then <html dir="rtl" lang="ar"> and the form, labels and موج fields mirror to RTL
  And the ProfileType tier labels show their Arabic names (NameArabic)
  When they open /admin/visitors/vip/export
  Then the tier pill shows the Arabic tier name (e.g. VVIP → its NameArabic)
  And the name column shows the Arabic name
  And scrollWidth == clientWidth (no horizontal overflow) on both pages
```

### Notes

- **Tiers are `ProfileType` rows, not an enum.** The `{VVIP, VIP}` restriction on the VIP desk is by ProfileType *Name*; if the client's tier names differ once seeded, the picker filter (`VipTierNames`) and these scenarios must be re-grounded against the seeded names.
- **CP page gates differ from the register-onsite API gate.** The VIP desk `/admin/visitors/vip` (`VipRegistration.razor`) is gated `Visitors.RegisterOnsite` (D-429 — it IS on-site registration), but the normal walk-in desk page `/admin/visitors/new` (`CreateVisitor.razor`) is gated `Visitors.Create`, and the pending queue `/admin/visitors/pending` (`PendingVisitors.razor`) is gated `Visitors.View`. The `register-onsite` **API** endpoint is gated `Visitors.RegisterOnsite`, the approve API `Visitors.Approve`, and the export surfaces `Visitors.ExportVip`.
- **Tier applied on approve (D-386).** `ApproveVisitorEndpoint` takes an optional `ProfileTypeId` in the body — `null` leaves the tier unchanged. The desk already requires a ProfileType at registration, so approval typically confirms rather than changes it.
- **No badge until approval (D-425).** D-127 originally auto-approved and minted the QR at the desk; D-425 reversed that so every visitor passes the pending-approval review. Any scenario expecting a desk-time QR is stale.
- **Email is optional; ID document / avatar / vip-photo are deferred uploads.** They are posted *after* a successful `register-onsite` (they need the new user id) and are fire-and-forget — a failed upload does not undo the registration, so assert badge/photo presence only after the upload step, not on the register response.
- **⚠ unverified: exact bilingual toast copy** for the walk-in inline errors (`Admin.WalkIn.Error.*` resx keys) and the approve success toast were not read this session — assert them by structure ("a bilingual SimfAlert error"), not by literal Arabic string.
- **⚠ unverified: National-ID uniqueness conflict code.** No `DuplicateNationalId`-style error code was found in `AdminAccountService`; the grounded conflict path is the duplicate **email** (`AdminEmailAlreadyRegistered`). Do not assert a National-ID collision code that has not been verified.
- **⚠ error-code wire values.** The scenarios reference the error-code C# constant names (`AdminProfileTypeInvalid`, `ProfileNationalityUnknown`, `AdminEmailAlreadyRegistered`), matching the sibling CP catalogues; the actual `ApiResult.Error.Code` wire strings are the SCREAMING_SNAKE values (`ADMIN_PROFILE_TYPE_INVALID`, `PROFILE_NATIONALITY_UNKNOWN`, `ADMIN_EMAIL_ALREADY_REGISTERED`) from `ErrorCodes.cs`.

---

## BF-03 — Staff & Moderator (Other) accounts + profile-scoped gates + scan

This cross-page flow proves the full "partner-account → gate → scan" chain across three surfaces. On the **Control Panel** it walks `/admin/profile-types/other` (`OtherProfileTypesList.razor` → `ProfileTypeForm` with `IsPartnerForm=true`, where the `MobileAppRole` `SimfSelect` renders only for partner types), then `/admin/others` (`OthersList` → `OthersAddEdit` → `CreateOtherForm` → `WalkInRegistrationForm` with `Kind=Other`, picker filtered `IsVisitor==false`) which posts **`POST /api/v1/admin/others/register-onsite`** (`RegisterOtherOnSiteEndpoint`, `expectedIsVisitor:false` → `PendingApproval`; QR minted on approval, D-425), and the gate admin at `/admin/gates` (`GatesList.razor` + `GatesAddEdit`) plus the read consoles `/admin/gates/operator` (`GateOperatorConsole`) and `/admin/gates/dashboard` (`GatesOperationsDashboard`, `AdminGateService.ListCurrentlyInsideAsync`). On the **Flutter app** it drives `gate_scan_screen.dart` at route `/gates/scan` (`RouteNames.gateScanner`, router #105) calling **`POST /api/v1/app/gates/{gateId}/scans`** (`PostScanEndpoint` → `GateOperatorService.RecordScanAsync`). It exercises the core rules: role is data on `ProfileType.MobileAppRole` (`None=0, Visitor=1, Staff=2, Moderator=3, Exhibitor=4`) not a per-user dropdown (D-161); a `Gate` has **no kind enum** — it is scoped only by `DirectionMode` (In=0/Out=1/Both=2) + a `GateProfileTypeAllow` allow-list (empty = everyone, non-empty = only those); the scan endpoint additionally requires the JWT permission `PermissionCatalog.Gates.Operate` (Identity role `GateOperator`) which is **separate** from the mobile Staff app-role (D-406 gap); every scan writes exactly **one append-only `GateScan`** row (never `HallAttendance`); and a denial is still **HTTP 200** carrying a `DenialReasonCode`.

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-BF-03-001 | Golden journey: partner ProfileType(Staff) → Other account → approve+QR → gate MAIN → assign operator → app scan Allowed → currently-inside | happy | P0 |
| E2E-BF-03-002 | Seeded canonical Staff + Moderator (منسّق) rows: `IsForVisitor=false`, `IsAppRegisterable=false`, admin-assigned only; `/admin/others` picker filters `IsVisitor==false` | happy | P1 |
| E2E-BF-03-003 | `MobileAppRole` `SimfSelect` renders ONLY for a partner ProfileType (`IsPartnerForm=true`), hidden for a visitor type | happy | P1 |
| E2E-BF-03-004 | Permission gate: Staff app-role WITHOUT the `Gates.Operate` grant → HTTP 403, no `GateScan` written | auth | P0 |
| E2E-BF-03-005 | Operator HAS `Gates.Operate` but is not assigned to THIS gate → `GATE_OPERATOR_NOT_ASSIGNED` (403) | error | P1 |
| E2E-BF-03-006 | Profile-scoped gate: allow-list denies a non-listed profile type (HTTP 200 `ProfileTypeNotAllowed`); empty-allow-list gate admits everyone | happy | P0 |
| E2E-BF-03-007 | Both-mode alternation: 2nd scan of the same badge infers `CheckOut` (cold start = `CheckIn`) | happy | P1 |
| E2E-BF-03-008 | 5-second duplicate absorption: same-direction re-scan returns same `ScanId` (no new row); a deliberate دخول/خروج switch is NOT absorbed | resilience | P1 |
| E2E-BF-03-009 | Holder denials still HTTP 200: unknown QR → `QrUnknown`; not-yet-approved holder → `HolderNotApproved` | error | P1 |
| E2E-BF-03-010 | Gate `Code` validation + uniqueness (2–16, uppercased, unique) → conflict error, no row | error | P1 |
| E2E-BF-03-011 | Idempotency-key reused with a different payload → `IDEMPOTENCY_KEY_CONFLICT` (409) | resilience | P2 |
| E2E-BF-03-012 | RTL/bilingual: CP gate editor mirrors; app scan screen shows Arabic منسّق; denial message is bilingual | i18n | P1 |
| E2E-BF-03-013 | Scan writes ONLY `GateScan` (never `HallAttendance`); currently-inside derived from each holder's last allowed `CheckIn` | resilience | P2 |

### Scenarios

### E2E-BF-03-001 — Golden journey

```gherkin
Feature: Staff account → profile-scoped gate → app scan
  Background:
    Given the API is reachable and the Control Panel is signed in as
      "superadmin@zagali-ict.com" (password entered at run time; TOTP via the Get-Totp helper)

  Scenario: A walk-in Staff operator is created, approved, assigned, and records a live scan
    # --- CP: partner ProfileType carrying the Staff app-role ---
    Given the admin opens /admin/profile-types/other
    When they create a partner ProfileType Name="Gate Operations" NameArabic="بوابات وتشغيل"
    And IsPartnerForm renders the MobileAppRole select and they pick "Staff"
    And they save
    Then the ProfileType persists with MobileAppRole=Staff and IsForVisitor=false
    # --- CP: create the Other (walk-in) account ---
    When they open /admin/others and choose "Add"
    And CreateOtherForm shows WalkInRegistrationForm with Kind=Other
    And the ProfileType picker lists only IsVisitor==false types and they pick "Gate Operations"
    And they enter FullName="Faisal Al-Otaibi" and a valid national id + phone
    And they submit
    Then POST /api/v1/admin/others/register-onsite returns 200 with expectedIsVisitor=false
    And the account state is PendingApproval and no QR exists yet
    When the admin approves the account
    Then a badge QR is minted for the account (D-425)
    # --- CP: a Both-mode gate open to everyone, operator assigned ---
    When the admin opens /admin/gates and creates Gate Code="MAIN" Name="Main Entrance"
      NameArabic="المدخل الرئيسي" DirectionMode=Both with an EMPTY AllowedProfileTypes
    And on GatesAddEdit they add "Faisal Al-Otaibi" to the AssignedOperatorUserIds multi-select
    And the "Gate Operations" admins carry the GateOperator (Gates.Operate) grant
    And they save
    Then the gate persists with Code="MAIN" (uppercased) and IsActive=true
    # --- App: the Staff operator scans a visitor badge ---
    When Faisal signs into the Flutter app (OTP from SIMF_Identity.AccountCodes at run time)
    And the More drawer shows "Gate scanner" and he opens /gates/scan
    And he scans visitor "Norah Al-Harbi" badge QR
    Then POST /api/v1/app/gates/{MAIN}/scans returns 200 Outcome=Allowed
    And Direction="CheckIn" (Both-mode cold start) with the holder profile echoed
    And exactly ONE append-only GateScan row is written (no HallAttendance row)
    And /admin/gates/dashboard lists Norah under "currently inside"
```

**Evidence captured:**
- App screenshot of the allowed-scan card + the CP dashboard "currently inside" row.
- Exactly one new `GateScan` row (Outcome=Allowed, Direction=CheckIn); zero `HallAttendance` rows.
- `OperationLog` / audit trail for the approval and the gate create.

### E2E-BF-03-002 — Seeded canonical Staff + Moderator rows

```gherkin
Scenario: The two seeded partner roles are admin-assigned only
  Given the identity seeder has run
  Then a ProfileType Name="Staff" exists with MobileAppRole=Staff
  And a ProfileType Name="منسّق" exists with MobileAppRole=Moderator
  And both rows have IsForVisitor=false and IsAppRegisterable=false
  When a would-be user tries the public app self-register path
  Then neither Staff nor Moderator can be self-selected (IsAppRegisterable=false)
  When an admin opens /admin/others "Add"
  Then the ProfileType picker filters to IsVisitor==false and both rows are selectable
```

### E2E-BF-03-003 — MobileAppRole select is partner-only

```gherkin
Scenario: The app-role select renders only for partner profile types
  Given the admin opens ProfileTypeForm for a partner type (IsPartnerForm=true)
  Then the MobileAppRole SimfSelect is visible with options None/Staff/Moderator/Exhibitor
  When the admin instead opens ProfileTypeForm for a visitor-facing type (IsPartnerForm=false)
  Then the MobileAppRole select is NOT rendered
  And the visitor app-role is resolved from UserType at JWT issuance, not from ProfileType
```

### E2E-BF-03-004 — Permission gate (Staff app-role without Gates.Operate)

```gherkin
Scenario: A Staff app user lacking the GateOperator grant is refused at the policy
  Given "Faisal Al-Otaibi" has ProfileType MobileAppRole=Staff
  But his Identity account does NOT hold the GateOperator role (Gates.Operate)
  When the app calls POST /api/v1/app/gates/{MAIN}/scans with a valid badge QR
  Then the RequirePermission(Gates.Operate) policy denies BEFORE HandleAsync runs
  And the response is HTTP 403 Forbidden
  And NO GateScan row is written
  And the mobile Staff app-role alone does NOT confer gate-operate authority (D-406 gap)
```

### E2E-BF-03-005 — Assigned-elsewhere operator

```gherkin
Scenario: An operator with the grant but not assigned to this gate is rejected
  Given "Faisal Al-Otaibi" holds the GateOperator grant (Gates.Operate)
  And he is assigned to gate "HALLA" but NOT to gate "SESSN"
  When the app calls POST /api/v1/app/gates/{SESSN}/scans
  Then RecordScanAsync finds his id absent from the gate's AssignedOperatorUserIds
  And the endpoint throws ErrorCodes.GateOperatorNotAssigned with HTTP 403
  And the error code is "GATE_OPERATOR_NOT_ASSIGNED"
  And the English message reads "You are not assigned to this gate." with an Arabic counterpart
  And no scan is recorded
```

### E2E-BF-03-006 — Profile-type-scoped allow-list

```gherkin
Scenario: A non-empty allow-list denies unlisted profile types; empty admits everyone
  Given gate "HALLA" DirectionMode=In has AllowedProfileTypes = { "Visitor - General" }
  And gate "MAIN" has an EMPTY AllowedProfileTypes (open to everyone)
  When the operator scans a holder whose ProfileType is "Speaker" at HALLA
  Then POST /api/v1/app/gates/{HALLA}/scans returns HTTP 200
  And Outcome=Denied with DenialReasonCode="ProfileTypeNotAllowed"
  And a GateScan denial row is still appended (append-only)
  When the same "Speaker" holder is scanned at MAIN
  Then the response is HTTP 200 Outcome=Allowed (empty allow-list = general/everyone)
```

### E2E-BF-03-007 — Both-mode direction alternation

```gherkin
Scenario: Alternation infers CheckOut on the second allowed scan
  Given gate "MAIN" DirectionMode=Both and holder "Norah Al-Harbi" has no prior scan today
  When the operator scans Norah with no explicit direction
  Then the recorded Direction="CheckIn" (cold start)
  When the operator scans Norah again more than 5 seconds later with no explicit direction
  Then the recorded Direction="CheckOut" (alternated from the last allowed scan)
  And two distinct GateScan rows exist for Norah on MAIN
```

### E2E-BF-03-008 — 5-second duplicate absorption vs deliberate switch

```gherkin
Scenario: A rapid same-direction re-scan is absorbed; a deliberate switch is not
  Given gate "BOOTHZ" DirectionMode=Both and holder "Norah Al-Harbi" was just allowed CheckIn
  When the same badge is re-scanned within 5 seconds with no explicit direction
  Then the response replays the SAME ScanId and NO new GateScan row is written
  When instead the operator toggles خروج (CheckOut) and re-scans within 5 seconds
  Then it is treated as an intentional new movement (D-509), NOT absorbed
  And a new GateScan row with Direction="CheckOut" is appended
```

### E2E-BF-03-009 — Holder denials (still HTTP 200)

```gherkin
Scenario: Unknown QR and unapproved holder are denials, not HTTP errors
  Given the operator is assigned to gate "MAIN"
  When they scan a QR that resolves to nothing
  Then POST /api/v1/app/gates/{MAIN}/scans returns HTTP 200 Outcome=Denied
  And DenialReasonCode="QrUnknown" with a bilingual message
  When they scan a badge whose account is still PendingApproval
  Then the response is HTTP 200 Outcome=Denied DenialReasonCode="HolderNotApproved"
  And each denial appends exactly one GateScan row and emits a GateScanDenied audit entry
```

### E2E-BF-03-010 — Gate Code validation + uniqueness

```gherkin
Scenario: Gate Code is 2–16 chars, uppercased, and unique
  Given a gate "MAIN" already exists
  When the admin tries to save a new gate with Code="M"
  Then GatesAddEdit shows a validation error (min length 2) and does not save
  When the admin saves a new gate with Code="meetg"
  Then it is stored uppercased as "MEETG"
  When the admin tries to save another gate with Code="MAIN"
  Then the save is rejected as a duplicate (a bilingual conflict alert) and no row is created
```

### E2E-BF-03-011 — Idempotency-key conflict

```gherkin
Scenario: Reusing an idempotency key with a different payload conflicts
  Given the operator posts a scan to gate "MAIN" with IdempotencyKey="k-7f3a" and QR=A
  And that scan is recorded Allowed
  When the operator posts again with the same IdempotencyKey="k-7f3a" but QR=B
  Then the response is HTTP 409 with error code "IDEMPOTENCY_KEY_CONFLICT"
  When instead they replay IdempotencyKey="k-7f3a" with the identical QR=A payload
  Then the original result is replayed with the same ScanId and no second row is written
```

### E2E-BF-03-012 — RTL / bilingual render

```gherkin
Scenario: Arabic locale mirrors the CP editor and the app scan surface
  Given the CP language is Arabic
  When the admin opens /admin/gates and edits gate "MAIN"
  Then GatesAddEdit mirrors RTL, NameArabic "المدخل الرئيسي" reads right-aligned,
    and the DirectionMode / AllowedProfileTypes controls are laid out RTL with no horizontal overflow
  When the app is set to Arabic and the operator whose ProfileType is منسّق (Moderator) opens /gates/scan
  Then the scanner chrome renders RTL and a denial shows the Arabic side of the bilingual message
```

### E2E-BF-03-013 — Scan writes only GateScan; currently-inside derivation

```gherkin
Scenario: Gate scans never touch HallAttendance; inside-count is derived
  Given holder "Norah Al-Harbi" is scanned Allowed CheckIn at gate "MAIN"
  Then exactly one GateScan row is appended and ZERO HallAttendance rows are written
  When AdminGateService.ListCurrentlyInsideAsync runs
  Then Norah appears (her last allowed scan is a CheckIn = inside)
  When Norah is later scanned Allowed CheckOut at "MAIN"
  Then a second GateScan row is appended and she drops off the currently-inside list
```

### Notes

- **Two separate 403 gates (D-406).** E2E-BF-03-004 and -005 are deliberately distinct. The `Gates.Operate` policy denies *before* `HandleAsync` (generic HTTP 403 Forbidden — no bespoke code, no `GateScan` written), whereas being un-assigned to a specific gate is caught *inside* `RecordScanAsync` and surfaces the verbatim `GATE_OPERATOR_NOT_ASSIGNED` (`ErrorCodes.GateOperatorNotAssigned`, "You are not assigned to this gate."). Do not conflate them.
- **Role is data, not a per-user field.** There is no per-user role dropdown; the app-role comes from `ProfileType.MobileAppRole`. Re-pointing a profile type's `MobileAppRole` rebalances every holder without a code change (D-161). `Visitor=1` is never persisted on a `ProfileType` row — it is resolved from `UserType` at JWT issuance.
- **Denials are HTTP 200.** `ProfileTypeNotAllowed`, `QrUnknown`, `HolderNotApproved`, `HolderDisabled`, `HolderLocked`, `ProfileTypeInactive`, `GateInactiveAtScan` all ride Outcome=Denied on a 200 with a bilingual message; only `GATE_NOT_FOUND` (404), `GATE_OPERATOR_NOT_ASSIGNED` (403), `IDEMPOTENCY_KEY_CONFLICT` (409) and `GATE_FAILURE_CIRCUIT_OPEN` (429) are true HTTP errors. `OutsideTimeWindow` / `BookingRequiredMissing` denial reasons exist in the enum but are reserved hooks — no rows produce them today, so do not assert them as live outcomes.
- **Append-only.** Every path (allow OR deny) writes exactly one `GateScan`; the 5-second window is the ONLY case that writes none (it replays the prior row). Gate scans never write `HallAttendance` (that belongs to the hall/session flow, out of scope here).
- **No secrets.** Admin sign-in uses the `Get-Totp` helper for TOTP; the Staff/visitor OTP second factor is read from `SIMF_Identity.AccountCodes` at run time. Never inline a password, TOTP secret, or token.
- **⚠ unverified:** the exact operator-list loader route (`/account/api/admin/admins/list`) backing the `AssignedOperatorUserIds` multi-select was not opened this pass — the `AssignedOperatorUserIds` field itself and its use in `RecordScanAsync` / `GateConfigCache` are verified. The gate editor file resolves to `GatesAddEdit.razor` (grounding named `GatesAddEdit.razor.cs`); treat "GatesAddEdit" as the page either way.

---

## BF-04 — Halls per purpose + seat layout

This flow proves that a venue admin can model the physical venue as **purpose-specialised halls** and that each purpose surfaces the right allocation tool. It crosses three Control Panel pages — **Halls** (`/admin/halls`, `HallsList.razor` + the CrudShell-framed `HallsAddEdit.razor`, gated `[RequirePermission(PermissionCatalog.Halls.View)]`), **Seat layouts** (`/admin/halls/seat-layouts`, `HallSeatLayoutEditor.razor`, gated `SeatLayouts.View`), and **Meeting tables** (`/admin/meeting-tables`, `MeetingTablesList.razor`, gated `MeetingTables.View`) — over the admin API in `HallEndpoints.cs`, `SeatReservationEndpoints.cs` and `BusinessMeetingEndpoints.cs`. It exercises the real rules from the grounding: `Hall.Code` is 2–16 chars, unique and uppercased (`POST /admin/halls`, `Halls.Create`); `Hall.Purpose` (`HallPurpose`: `General=0` default, `Booth=1`, `Session=2`, `Meeting=3`, D-248) is set through the dedicated `PUT /admin/halls/{id}/purpose` (`SetHallPurposeRequest`, `Halls.Edit`) — **not** through the Add/Edit form, whose contract omits `Purpose`; a `HallSeatLayout` (`RowLabels` + `SeatsPerRow`, 1:1 with the hall) is persisted via `PUT /admin/halls/{hallId}/seat-layout` (`SetHallSeatLayoutRequest`, `SeatLayouts.Edit`) with `rows ∈ [1,26]`, each label 1–8 chars, `SeatsPerRow ∈ [1,80]` and `rows × SeatsPerRow ≤ Hall.Capacity`; `MeetingTable.HallId` may target only a **Meeting or General** hall; and `SeatSelectionMode` (`AssignedSeat=0` / `OpenSeating=1`, D-485) — overridable per session via `Session.SeatSelectionModeOverride` — decides whether the app shows the seat picker or a bulk join. It also asserts the two grounded negatives: the geofence triple (`GeofenceCenterLat/Lon/RadiusMeters`, D-240) is all-set-or-all-null, and **Hall↔Gate has no direct schema relationship**.

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-BF-04-001 | Golden journey: create General + Session + Booth + Meeting halls, set each purpose, author the Session hall's seat grid | happy | P0 |
| E2E-BF-04-002 | Hall code is uppercased on save; 2–16 length rule enforced | happy | P1 |
| E2E-BF-04-003 | Duplicate hall code rejected (`HALL_CODE_DUPLICATE`) | error | P1 |
| E2E-BF-04-004 | Purpose is set via `PUT /admin/halls/{id}/purpose`, not the Add/Edit form; audit `Hall.PurposeChanged` | happy | P0 |
| E2E-BF-04-005 | Seat layout A–J × 20 persists and round-trips on reload | happy | P0 |
| E2E-BF-04-006 | Seat layout exceeding capacity is rejected (`SEAT_CAPACITY_EXCEEDED`) | error | P0 |
| E2E-BF-04-007 | Invalid row labels / seats-per-row rejected (`SEAT_LAYOUT_INVALID`) | error | P1 |
| E2E-BF-04-008 | Meeting table requires a Meeting/General hall (`HALL_NOT_MEETING_PURPOSE`) | error | P0 |
| E2E-BF-04-009 | Open-seating join on an AssignedSeat session (`SEAT_SELECTION_REQUIRED`) | resilience | P1 |
| E2E-BF-04-010 | Permission gate: an admin lacking `Halls.Create` / `SeatLayouts.Edit` is blocked | auth | P0 |
| E2E-BF-04-011 | Geofence is all-three-or-none (`HALL_GEOFENCE_INVALID`) | error | P2 |
| E2E-BF-04-012 | Soft-deleted hall drops from the grid and from meeting-table / session pickers | happy | P2 |
| E2E-BF-04-013 | Arabic RTL render of the Halls list + seat-layout editor | i18n | P1 |
| E2E-BF-04-014 | Architecture negative: no Hall↔Gate relationship in the schema | resilience | P2 |

### Scenarios

### E2E-BF-04-001 — Golden journey

```gherkin
Feature: Model the venue as purpose-specialised halls with a seat grid
  Background:
    Given the API is reachable on http://localhost:5175
    And the Control Panel is reachable on http://localhost:5158
    And an Administrator is signed in
      # superadmin@zagali-ict.com + TOTP via the Get-Totp helper

  Scenario: Create four halls, dedicate their purpose, and lay out the session hall
    Given the administrator opens /admin/halls

    # 1) A General (un-specialised) hall — the default purpose
    When they click Add and fill
      | Code     | GH1                |
      | Name     | General Hall 1     |
      | NameArabic | القاعة العامة 1  |
      | Capacity | 300                |
      | Floor    | Ground             |
      | SeatMode | Assigned seat      |
    And they Save
    Then a bilingual SimfAlert success appears
    And the grid shows a "GH1" row whose Purpose is General (0)

    # 2) A Session hall — capacity 200, assigned-seat, then a seat grid
    When they Add a hall Code="SES-A" Name="Main Auditorium"
         NameArabic="القاعة الرئيسية" Capacity=200 SeatMode="Assigned seat"
    And they Save
    Then a "SES-A" row appears

    # 3) A Booth (exhibition) hall
    When they Add a hall Code="EXPO1" Name="Exhibition Hall"
         NameArabic="قاعة المعارض" Capacity=500
    And they Save
    Then an "EXPO1" row appears

    # 4) A Meeting hall
    When they Add a hall Code="MTG-1" Name="Meeting Room 1"
         NameArabic="غرفة الاجتماعات 1" Capacity=40
    And they Save
    Then an "MTG-1" row appears

    # Dedicate the three specialised purposes (see E2E-BF-04-004 for the mechanism)
    When they set SES-A → Purpose=Session, EXPO1 → Purpose=Booth, MTG-1 → Purpose=Meeting
    Then each PUT /admin/halls/{id}/purpose returns ApiResult.Ok = true

    # Author the Session hall's seat grid: rows A..J (10) × 20 = 200 = capacity
    When they open /admin/halls/seat-layouts
    And they pick "SES-A — Main Auditorium (cap 200)"
    And they set RowLabels="A,B,C,D,E,F,G,H,I,J" and SeatsPerRow=20
    And they Save
    Then PUT /admin/halls/{SES-A}/seat-layout returns HTTP 200
    And the HallSeatLayoutSnapshot reports 10 rows × 20 = 200 seats (== hall capacity)
    And an audit row HallSeatLayout.Updated records rows + seatsPerRow
    And the console shows 0 errors and the network list shows 0 failed requests
```

### E2E-BF-04-002 — Code uppercased + length rule

```gherkin
Scenario: Lower-case code is stored uppercased; a too-short code is rejected
  Given the administrator opens /admin/halls and clicks Add
  When they fill Code="ses-b" Name="Second Hall" NameArabic="القاعة الثانية" Capacity=120
  And they Save
  Then the created hall's Code is stored as "SES-B" (uppercased)

  When they Add another hall with Code="S" (1 char, below the 2–16 range)
  And they Save
  Then POST /admin/halls returns HTTP 400 with ApiResult.Error.Code = "HALL_INVALID"
  And a bilingual SimfAlert error is shown
  And no hall row is added
```

### E2E-BF-04-003 — Duplicate hall code

```gherkin
Scenario: A second hall reusing an existing code is rejected
  Given a hall with Code="GH1" already exists (from E2E-BF-04-001)
  When the administrator Adds a hall with Code="gh1" (same code, different case)
  And they Save
  Then POST /admin/halls returns HTTP 409 with ApiResult.Error.Code = "HALL_CODE_DUPLICATE"
  And a bilingual SimfAlert error is shown
  And the grid still holds exactly one "GH1" row
```

### E2E-BF-04-004 — Purpose is set through the dedicated endpoint

```gherkin
Scenario: The Add/Edit form cannot set Purpose; the purpose endpoint can
  Given the administrator opens /admin/halls and edits "SES-A"
  Then the Add/Edit form exposes Code, Name, NameArabic, Capacity, SeatMode,
       Floor, EquipmentNotes and the geofence — but NO Purpose field
  And AdminCreateHallRequest / AdminUpdateHallRequest carry no Purpose,
      so a hall created or edited through this form stays Purpose=General (0)

  When the administrator opens /admin/meeting-tables
  And selects hall "SES-A" and sets its Purpose to Session (2)
  Then PUT /admin/halls/{SES-A}/purpose returns ApiResult.Ok = true
  And an audit row Hall.PurposeChanged records "hallId=…; purpose=Session"
  And AdminHallSummary.Purpose for SES-A now reads 2 (Session)

  When they set EXPO1 → Booth (1) and MTG-1 → Meeting (3) the same way
  Then each returns Ok and the grid reflects the new purpose
```

### E2E-BF-04-005 — Seat layout round-trips

```gherkin
Scenario: A saved seat grid survives a reload
  Given SES-A has the layout RowLabels="A..J", SeatsPerRow=20 (from the golden path)
  When the administrator re-opens /admin/halls/seat-layouts and picks "SES-A"
  Then GET /admin/halls/{SES-A}/seat-layout returns the same 10 rows × 20
  And the visual seat grid renders 10 rows labelled A through J with 20 seats each
  And no error toast appears

  When they change SeatsPerRow to 15 and Save
  Then the layout updates to 10 × 15 = 150 seats (≤ capacity 200) and returns HTTP 200
```

### E2E-BF-04-006 — Layout capacity guard

```gherkin
Scenario: A grid larger than the hall capacity is rejected
  Given SES-A has Capacity = 200
  When the administrator sets RowLabels="A,B,C,D,E,F,G,H,I,J,K,L" (12 rows) and SeatsPerRow=20
    # 12 × 20 = 240 > 200
  And they Save
  Then PUT /admin/halls/{SES-A}/seat-layout returns HTTP 400
       with ApiResult.Error.Code = "SEAT_CAPACITY_EXCEEDED"
  And the English message reads "Layout capacity (240) exceeds hall capacity (200)."
  And a bilingual SimfAlert error is shown
  And the previously saved layout is left unchanged
```

### E2E-BF-04-007 — Invalid row labels / seats-per-row

```gherkin
Scenario Outline: Malformed layouts are rejected with SEAT_LAYOUT_INVALID
  Given the administrator is editing the seat layout for SES-A
  When they submit RowLabels="<rows>" and SeatsPerRow=<perRow>
  Then PUT /admin/halls/{SES-A}/seat-layout returns HTTP 400
       with ApiResult.Error.Code = "SEAT_LAYOUT_INVALID"
  And a bilingual SimfAlert error is shown

  Examples:
    | rows                                                                 | perRow | why                          |
    | A,A,B                                                                | 20     | duplicate row label          |
    | ROWLABEL9                                                            | 20     | label > 8 chars              |
    | A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q,R,S,T,U,V,W,X,Y,Z,AA               | 20     | 27 rows (> 26)               |
    | A,B,C                                                                | 0      | seats-per-row below 1        |
    | A,B,C                                                                | 81     | seats-per-row above 80       |
```

### E2E-BF-04-008 — Meeting tables require a Meeting/General hall

```gherkin
Scenario: A meeting table cannot be placed in a Booth or Session hall
  Given EXPO1 has Purpose=Booth and SES-A has Purpose=Session
  When the administrator opens /admin/meeting-tables
  And tries to add a table Code="T-01" Capacity=6 to hall "EXPO1"
  Then the request returns HTTP 409 with ApiResult.Error.Code = "HALL_NOT_MEETING_PURPOSE"
  And the English message reads "Meeting tables require a Meeting or General hall."
  And a bilingual SimfAlert error is shown

  When they instead add table "T-01" (Capacity=6) to the Meeting hall "MTG-1"
  Then the table is created (HTTP 200) and appears in the MTG-1 table grid
  When they add table "T-02" (Capacity=8) to the General hall "GH1"
  Then it is also accepted (General halls host any purpose)
```

### E2E-BF-04-009 — Open-seating join vs assigned-seat

```gherkin
Scenario: Joining without a seat is refused on an assigned-seat session
  Given SES-A has SeatSelectionMode=AssignedSeat (0)
  And a Session S1 is scheduled in SES-A with no SeatSelectionModeOverride
    # effective mode = Session.SeatSelectionModeOverride ?? Hall.SeatSelectionMode = AssignedSeat
    # (SES-A has a seat layout from the golden path, so the effective mode is honoured)
  And an approved visitor is signed in on the app
    # visitor OTP read from SIMF_Identity.AccountCodes at run time
  When the app calls POST /app/sessions/{S1}/seats/join
  Then it returns HTTP 409 with ApiResult.Error.Code = "SEAT_SELECTION_REQUIRED"
  And the app shows the seat picker instead
  When the visitor instead calls POST /app/sessions/{S1}/seats/reserve with RowLabel="C" SeatNumber=7
  Then a seat-specific reservation is created and auto-confirmed (Status=Approved, no Control Panel approval step), held provisionally until the attendee checks in at the hall gate

  Scenario: An open-seating hall lets visitors bulk-join
    Given a hall OPEN-1 has SeatSelectionMode=OpenSeating (1) and hosts Session S2
    When the visitor calls POST /app/sessions/{S2}/seats/join
    Then a general-admission reservation is created with a null row/seat (HTTP 200)
```

### E2E-BF-04-010 — Permission gate

```gherkin
Scenario: An admin without the hall/layout permissions is blocked at the API
  Given a signed-in admin whose role grants Halls.View but NOT Halls.Create or SeatLayouts.Edit
  When they open /admin/halls
  Then the grid loads (View is granted)
  When they attempt to add a hall and their client POSTs /admin/halls
  Then the API returns HTTP 403 (permission policy denies Halls.Create)
  When they open /admin/halls/seat-layouts and attempt Save
  Then PUT /admin/halls/{id}/seat-layout returns HTTP 403 (SeatLayouts.Edit denied)

  Scenario: A non-admin account is bounced from the whole area
    Given a signed-in user with no Administrator role
    When they navigate to /admin/halls
    Then they are redirected to /not-permitted with HTTP 200
```

### E2E-BF-04-011 — Geofence all-or-none

```gherkin
Scenario: A partial geofence triple is rejected; a complete one persists
  Given the administrator edits hall "GH1"
  When they fill only GeofenceCenterLat=24.7136 and leave Lon and RadiusMeters blank
  And they Save
  Then PUT /admin/halls/{GH1} returns HTTP 400 with ApiResult.Error.Code = "HALL_GEOFENCE_INVALID"
  And a bilingual SimfAlert error is shown

  When they fill all three: Lat=24.7136, Lon=46.6753, RadiusMeters=75
  And they Save
  Then the hall is updated (HTTP 200) and the stored geofence is retained
  When they later clear all three fields and Save
  Then the hall persists with no geofence (arrivals fall back to QR door scan only)
```

### E2E-BF-04-012 — Soft-delete drops from pickers

```gherkin
Scenario: Deleting a hall deactivates it without a hard delete
  Given an unused hall "SES-B" exists
  When the administrator deletes it from /admin/halls
  Then DELETE /admin/halls/{SES-B} returns ApiResult.Ok = true
  And the row leaves the default (active-only) grid
  And SES-B no longer appears in the /admin/halls/seat-layouts hall picker
  And SES-B no longer appears in the /admin/meeting-tables hall picker
  And the underlying row is IsActive=false (soft-deleted, not removed)
```

### E2E-BF-04-013 — RTL / bilingual render

```gherkin
Scenario: Arabic toggle mirrors the Halls list and the seat-layout editor
  Given the administrator opens /admin/halls
  When they switch the language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title, grid headers and Add/Edit labels are Arabic
  And each hall row shows its NameArabic (e.g. "القاعة الرئيسية" for SES-A)
  And the DOM check passes: scrollWidth == clientWidth (no horizontal overflow), no broken images

  When they open /admin/halls/seat-layouts in Arabic
  Then the hall picker, hint text and Save button are Arabic
  And the seat grid renders right-to-left with row labels still reading A..J
```

### E2E-BF-04-014 — No Hall↔Gate relationship (architecture negative)

```gherkin
Scenario: Halls and Gates are not linked in the schema
  Given the Hall entity and its admin contracts
  Then there is NO GateId / GateCode field on Hall, AdminHallSummary or AdminHallDetail
  And there is no navigation, foreign key or join between Hall and any Gate entity
  And arrival at a hall is recorded only by QR door scan or (when configured) the hall geofence
  And an admin cannot and need not "assign a gate" to a hall from any Halls page
```

### Notes

- **Purpose is not editable from the Halls Add/Edit form.** `HallsAddEdit.razor` and both `AdminCreateHallRequest` / `AdminUpdateHallRequest` omit `Purpose`, so every hall created or edited through the standard CRUD form is `General (0)`. The only way to dedicate a hall to `Booth` / `Session` / `Meeting` is `PUT /admin/halls/{id}/purpose` (`SetHallPurposeRequest`, `Halls.Edit`, audit `Hall.PurposeChanged`), which is surfaced by the Meeting-Tables page (`MeetingTablesList.razor` → `SetHallPurposeAsync`). Scenario -001 therefore treats "create hall" and "set purpose" as two steps, and -004 verifies the mechanism explicitly. `AdminHallSummary.Purpose` is exposed on the grid row (for display / Excel round-trip) even though the edit form does not write it.
- **Seat-layout bounds are enforced server-side only** in `SeatReservationService.SetLayoutAsync`: rows 1–26 unique, each 1–8 chars, `SeatsPerRow` 1–80 → `SEAT_LAYOUT_INVALID`; `rows × SeatsPerRow ≤ Hall.Capacity` → `SEAT_CAPACITY_EXCEEDED`. The CP editor mirrors the limits in its hint text but the API is the source of truth. A hall with no `HallSeatLayout` has no per-seat picker and falls back to random-only allocation against `Hall.Capacity`. The persisted-layout audit event is `HallSeatLayout.Updated` (`AuditEvents.HallSeatLayoutUpdated`).
- **Effective seat mode** for a session is `Session.SeatSelectionModeOverride ?? Hall.SeatSelectionMode`, further short-circuited to `OpenSeating` when the hall has no seat layout (D-706); the app `join` endpoint returns `409 SEAT_SELECTION_REQUIRED` whenever that resolves to `AssignedSeat`. Seat/booking reservations created by the app are **auto-confirmed** (`Status=Approved`) on reserve with no Control Panel approval step, and held provisionally until the attendee checks in at the hall gate; the CP booking-approval queue (`/admin/bookings/*`) is **retained but dormant** (always empty) and is covered by its own flow.
- **CP action gating.** The Halls grid `Add` button is rendered by `SimfDataGrid` whenever an `OnAdd` handler is wired; it is not individually wrapped in `AuthorizedAction`, so the authoritative gate for create is the API permission policy (`Halls.Create` → `403`). Scenario -010 therefore asserts the API `403`, not a hidden button.
- **⚠ Session seeding assumed.** Scenarios -009 reference concrete sessions (S1/S2) in the halls. Per project notes the programme/sessions table can ship empty in some environments (no session seeder); the tester should seed or create S1/S2 (and the OPEN-1 hall) before running the app-side steps, or treat -009 as blocked-on-data rather than a defect.
- **Geofence coordinates ("ship empty", G-OI-2).** The lat/lon/radius values in -011 are illustrative Riyadh coordinates; production halls ship with the geofence null and the event team seeds real coordinates later. The geofence-driven arrival/attendance chain (FR-305/506/1103) is a deferred item (D-211) and is not asserted here beyond the all-or-none validation.
- **No literal secrets.** Admin TOTP is generated via the `Get-Totp` helper and visitor OTP is read from `SIMF_Identity.AccountCodes` at run time; no code, key or token is embedded in this catalogue.

_Last reviewed:_ 2026-07-11 — authored for the production-readiness cross-page round (BF-04).

---

## BF-05 — Session booking → approve → hall-door arrival → live-now

This cross-page flow walks one seat from booking to attendance across three surfaces. An **admin** creates a session in a hall on the Control Panel (`/admin/sessions`, `SessionsList.razor`) and assigns a moderator (see BF-07); a **visitor** on the mobile app books through the booking hub (`/sessions/join`, `join_session_hub_screen.dart`) → seat picker (`/sessions/:sessionId/pick-seat`, `seat_picker_screen.dart` + `hall_seat_map.dart`) → their seat (`/sessions/:sessionId/my-seat`, `my_seat_screen.dart` + `session_booking_actions.dart`); the booking desk (`/admin/bookings`, `BookingsList.razor`) is **retained but dormant** — attendee reservations auto-confirm on reserve, so its Pending queue is always empty; and a **door operator** records arrival on the hall-arrivals console (`/admin/hall-arrivals`, `HallArrivalsConsole.razor`) — the gate check-in that confirms the provisionally-held seat. The app booking endpoints are `GET /api/v1/app/sessions/{id}/seats` (grid), `POST .../seats/reserve` (pick a specific seat), `POST .../seats/reserve-random`, `POST .../seats/join` (open-seating — returns **409 `SEAT_SELECTION_REQUIRED`** when the session is AssignedSeat), and `DELETE .../seats/mine` (release), all gated by `RequireApprovedAccount` (`SeatReservationEndpoints.cs`). The approval surface — `POST /api/v1/admin/bookings/list` / `/{id}/approve` / `/reject` / `/bulk-approve` (perms `Bookings.View|Approve|Reject`) — is **retained but dormant**: nothing creates a `Pending` booking, so the queue is always empty and these endpoints have no live input. Arrival is the operator QR door-scan `POST /api/v1/admin/sessions/{sessionId}/arrivals` (`HallArrivalEndpoints.cs`, perm `HallArrivals.Record`) which resolves the attendee from the badge QR **server-side** and opens **one** `HallAttendance` row (`AttendanceMethod.QrScan`, D-244). The two chains are **decoupled** — a `SeatReservation` (D-227: every reservation, visitor `UserBooking` / `RandomAssignment` and admin `AdminReservedRow`, is created `Approved`, auto-confirmed on reserve with no Control Panel approval step) is never consulted when a badge is scanned; live-now dashboards read `HallAttendance` via `SessionAttendanceService`. Key rules exercised: auto-confirmed reservations held provisionally until gate check-in, seat/open-seating mode enforcement, one-active-seat-per-user uniqueness, the admin-only door-scan gate, decoupled attendance, and the deferred attendee GPS-geofence path (`POST /api/v1/app/sessions/{id}/arrival` → **403 `NOT_AT_VENUE`** / **`HALL_GEOFENCE_NOT_CONFIGURED`**), which has **no app screen** wired to it.

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-BF-05-001 | Golden journey: create → reserve seat (auto-confirmed) → gate check-in door-scan → live-now | happy | P0 |
| E2E-BF-05-002 | Visitor picks a specific seat → reservation auto-confirmed, held provisionally until gate check-in | happy | P1 |
| E2E-BF-05-003 | Reserve-random assigns a seat (Kind=RandomAssignment), auto-confirmed (Approved) | happy | P1 |
| E2E-BF-05-004 | Open-seating join on an AssignedSeat session → 409 SEAT_SELECTION_REQUIRED | error | P1 |
| E2E-BF-05-005 | Two visitors race the same seat → the second is rejected (seat-taken conflict) | error | P1 |
| E2E-BF-05-006 | Bulk-approve endpoint — retained but dormant (the Pending queue is always empty) | happy | P1 |
| E2E-BF-05-007 | Reject a booking — dormant admin-only path; reason required → BookingRejected notification | error | P1 |
| E2E-BF-05-008 | Auth gate: operator without HallArrivals.Record cannot door-scan | auth | P0 |
| E2E-BF-05-009 | Door-scan opens exactly ONE HallAttendance row, decoupled from booking | happy | P0 |
| E2E-BF-05-010 | Deferred GPS geofence path returns NOT_AT_VENUE / HALL_GEOFENCE_NOT_CONFIGURED | resilience | P2 |
| E2E-BF-05-011 | Live-now reflects arrival; departure fires the rating prompt (→ BF-08) | happy | P1 |
| E2E-BF-05-012 | RTL / bilingual render across booking desk + seat picker + my-seat | i18n | P1 |

### Scenarios

### E2E-BF-05-001 — Golden journey

```gherkin
Feature: A booked seat travels from request to recorded attendance
  Background:
    Given the API is reachable and an Administrator is signed in on the Control Panel
    And the admin TOTP is generated via the Get-Totp helper (never a literal secret)
    And a visitor "Faisal Al-Otaibi" is an Approved app account
    And the visitor OTP is read from SIMF_Identity.AccountCodes at run time

  Scenario: Create the session, reserve a seat (auto-confirmed), scan the badge at the gate, see it live
    Given the admin opens /admin/sessions and creates session "Maritime Cyber Defence"
      in hall "H-2 / القاعة الرئيسية" with mode AssignedSeat and a start time today
    And a moderator is assigned to the session (per BF-07)
    When the visitor opens /sessions/join, selects "Maritime Cyber Defence",
      lands on /sessions/:sessionId/pick-seat, taps free seat "C-14",
      and confirms via POST /api/v1/app/sessions/{id}/seats/reserve {"rowLabel":"C","seatNumber":14}
    Then the reservation is created with Kind=UserBooking and Status=Approved (auto-confirmed, no Control Panel approval step)
    And no notification is sent on reserving; the app shows the confirmation inline
    And /sessions/:sessionId/my-seat shows seat "C-14" as reserved, held provisionally until gate check-in
    And the /admin/bookings queue stays empty (retained but dormant — nothing creates a Pending booking)
    When the door operator opens /admin/hall-arrivals, selects the session,
      and scans the visitor's badge QR (POST /api/v1/admin/sessions/{sessionId}/arrivals)
    Then the server resolves the attendee from the QR id (not the request body)
    And exactly one HallAttendance row opens with AttendanceMethod=QrScan
    And the gate check-in confirms the provisionally-held seat
    And the live-now dashboard (SessionAttendanceService) counts the attendee as present
```

### E2E-BF-05-002 — Pick a specific seat, auto-confirmed (held provisionally)

```gherkin
Scenario: A visitor picks a specific seat and it is confirmed immediately
  Given the visitor is on /sessions/:sessionId/pick-seat with the hall_seat_map loaded
    from GET /api/v1/app/sessions/{id}/seats
  When they tap free seat "A-3" and confirm
  Then POST /api/v1/app/sessions/{id}/seats/reserve {"rowLabel":"A","seatNumber":3} returns 200
  And MySeatReservation shows Status=Approved, Kind=UserBooking (auto-confirmed, no approval step)
  And no notification is sent on reserving; the app shows the confirmation inline
  And /sessions/:sessionId/my-seat renders seat "A-3" as reserved with a Release action, held provisionally until gate check-in
  And the admin /admin/bookings queue stays empty (retained but dormant — nothing creates a Pending booking)
```

### E2E-BF-05-003 — Reserve a random seat

```gherkin
Scenario: A visitor lets the system assign a seat
  Given the visitor is on the booking screen for a session that still has free seats
  When they choose "Assign me any seat"
  And POST /api/v1/app/sessions/{id}/seats/reserve-random returns 200
  Then MySeatReservation carries a concrete rowLabel+seatNumber picked by the server
  And Kind=RandomAssignment and Status=Approved (auto-confirmed, no approval step)
  And the seat now shows occupied on the grid for other visitors
```

### E2E-BF-05-004 — Open-seating join blocked on an assigned-seat session

```gherkin
Scenario: Joining open-seating when the session requires a seat selection
  Given session "Maritime Cyber Defence" is in mode AssignedSeat
  When the visitor calls POST /api/v1/app/sessions/{id}/seats/join
  Then the API responds 409 with ApiResult.Error.Code = "SEAT_SELECTION_REQUIRED"
  And the app routes the visitor to /sessions/:sessionId/pick-seat instead of joining
  And no SeatReservation is created for the visitor
```

### E2E-BF-05-005 — Two visitors race the same seat

```gherkin
Scenario: The same seat cannot be held by two active reservations
  Given visitor "Faisal Al-Otaibi" has an active reservation on seat "C-14"
  When visitor "Noura Al-Harbi" taps seat "C-14" and confirms
    POST /api/v1/app/sessions/{id}/seats/reserve {"rowLabel":"C","seatNumber":14}
  Then the API rejects the second reservation (seat already taken)
  And a bilingual SimfAlert error explains the seat is no longer available
  And Noura's grid refreshes to show "C-14" as occupied
  And only Faisal's single active reservation remains for that seat
```

### E2E-BF-05-006 — Bulk approve (retained but dormant path)

```gherkin
Scenario: The bulk-approve endpoint is retained but dormant (the queue is normally empty)
  Given attendee reservations auto-confirm on reserve, so /admin/bookings normally lists no Pending bookings
  And the bulk-approve endpoint is retained but dormant — reachable only if a Pending row is injected on the admin path
  When (dormant path only) an admin selects Pending rows and clicks "Approve selected"
    POST /api/v1/admin/bookings/bulk-approve {"reservationIds":[...guids...]}
  Then the endpoint returns ApiResult<int> with the approved count
  And those reservations move to Status=Approved with ReviewedByUserId + ReviewedAt set
  And in normal operation the queue is always empty, so this path is never exercised in production
```

### E2E-BF-05-007 — Reject requires a reason (dormant admin-only path)

```gherkin
Scenario: Rejecting a booking demands a reason and notifies the visitor
  Given the reject flow is retained but dormant — attendee reservations auto-confirm, so a Pending booking exists only on the admin path
  And (dormant path only) a Pending booking for "Noura Al-Harbi" on seat "A-3"
  When the admin clicks Reject but leaves the reason blank
  Then the reject form blocks submission (reason is required, ≤512 chars)
  When the admin enters "Row A reserved for the delegation of 12 officers" and confirms
    POST /api/v1/admin/bookings/{id}/reject {"reason":"..."}
  Then the reservation Status becomes Rejected with RejectionReason stored
  And a BookingRejected notification (NotificationKind.BookingRejected) reaches the visitor
  And seat "A-3" is freed on the grid for re-booking
```

### E2E-BF-05-008 — Door-scan permission gate

```gherkin
Scenario: An operator without HallArrivals.Record is denied the door-scan
  Given an admin "Reception Clerk" is signed in without the HallArrivals.Record permission
  When they POST /api/v1/admin/sessions/{sessionId}/arrivals with a valid badge QR
  Then the API responds 403 Forbidden (the PermissionCatalog.HallArrivals.Record policy fails)
  And no HallAttendance row is created
  And a non-admin app account that hits /admin/bookings or /admin/hall-arrivals
    is redirected to /not-permitted (never sees the queue)
```

### E2E-BF-05-009 — One attendance row, decoupled from booking

```gherkin
Scenario: The QR door-scan opens exactly one attendance row and ignores the booking state
  Given a visitor "Walk-in Guest" has NO seat reservation for the session
  When the door operator scans that visitor's badge QR
    POST /api/v1/admin/sessions/{sessionId}/arrivals
  Then one HallAttendance row opens (AttendanceMethod=QrScan) even with no reservation
    (arrival never consults SeatReservation — the two chains are decoupled)
  When the same badge is scanned a second time at the same door
  Then no duplicate HallAttendance row is created (the open row is reused)
  And a visitor whose booking was Rejected can still be scanned in the same way
```

### E2E-BF-05-010 — Deferred attendee GPS-geofence path

```gherkin
Scenario: The self-service geofence endpoint enforces venue proximity (no app screen wired)
  Given the attendee-facing geofence UI is DEFERRED — no mobile screen calls this endpoint
  When a client POSTs /api/v1/app/sessions/{id}/arrival with GPS coordinates outside the hall geofence
  Then the API responds 403 with ApiResult.Error.Code = "NOT_AT_VENUE"
  When the same call targets a session whose hall has no geofence configured
  Then the API responds with ApiResult.Error.Code = "HALL_GEOFENCE_NOT_CONFIGURED"
  And in production the arrival of record is always the operator QR door-scan, not this path
```

### E2E-BF-05-011 — Live-now and departure

```gherkin
Scenario: Arrival lights up live-now; departure ends attendance and prompts a rating
  Given a scanned attendee has an open HallAttendance row for the live session
  When the live-now dashboard refreshes (SessionAttendanceService reads HallAttendance)
  Then the session shows the attendee in its present count
  When departure is recorded for that attendee
  Then the HallAttendance row is closed and the live-now present count drops
  And the app raises the session-rating prompt for that attendee (continued in BF-08)
```

### E2E-BF-05-012 — RTL / bilingual render

```gherkin
Scenario: Booking desk, seat picker and my-seat all render correctly in Arabic
  Given the Control Panel language is switched to العربية
  When the admin opens /admin/bookings
  Then the page renders with <html dir="rtl"> and Arabic column headers, no horizontal overflow
  And any error surfaces as a bilingual SimfAlert (English + Arabic)
  When the visitor opens /sessions/join and /sessions/:sessionId/pick-seat in Arabic
  Then the seat map mirrors RTL, row labels stay readable, and the "احجز مقعداً / Book a seat"
    and "مقعدي / My seat" labels render for their screens
  And a failed reservation shows a bilingual SimfAlert error, not a raw code
```

### Notes

- **Attendee GPS-geofence is deferred (grounded).** `POST /api/v1/app/sessions/{id}/arrival` exists in `HallAttendanceEndpoints.cs` (Haversine vs hall geofence, D-240/D-241) but **no app screen invokes it** — the production arrival path is the operator QR door-scan in `HallArrivalEndpoints.cs`. Scenario -010 exercises the API contract only.
- **⚠ error code correction:** the grounding said the geofence call returns "400 if the hall has no geofence"; the verified code in `ErrorCodes.cs` is **`HALL_GEOFENCE_NOT_CONFIGURED`** (there is no generic 400). Scenario -010 asserts the verbatim code.
- **Booking ⟂ attendance is by design (D-227 / D-244).** Scanning a badge opens attendance regardless of whether a `SeatReservation` exists or its `BookingStatus` — a walk-in with no booking, or a visitor whose booking was Rejected, is still admitted and counted. Do not assert that attendance requires an approved booking.
- **All reservations auto-confirm (D-227, reservation-only):** every reservation — visitor `UserBooking` / `RandomAssignment` and admin `AdminReservedRow` blocks — is created `Approved` on reserve; there is no Control Panel approval step and no notification is sent on reserving (the app shows the confirmation inline). The seat is held provisionally until the attendee checks in at the hall gate, which confirms it; a pre-start sweep releases any hold not checked in shortly before the session starts. The `/admin/bookings` approval queue (list / approve / reject / bulk-approve, `Bookings.Approve|Reject`) is **retained but dormant** — nothing creates a `Pending` booking, so it is always empty. `Rejected` exists only on that dormant admin path.
- **Notification kinds** referenced (`BookingConfirmed`, `BookingRejected`) are additive `NotificationKind` values (D-217 / D-230), persisted by name.
- **Departure → rating** is owned by BF-08; -011 only crosses the boundary and must not restate BF-08's rating scenarios.
- **No literal secrets:** admin TOTP via `Get-Totp`, visitor OTP read from `SIMF_Identity.AccountCodes` at run time.

---

## BF-06 — Meeting requests → CP review desks

This cross-page flow exercises the three physically distinct meeting models SIMF ships and the Control-Panel desks that review them. **(1) Business meetings** are admin-arranged only: there is *no* attendee request queue — an admin schedules a `BusinessMeeting` at a `MeetingTable` (in a Meeting/General hall) for a from–to slot with **two or more** `BusinessMeetingParticipant`s, and the row is created `Confirmed` from `/admin/business-meetings` (create endpoint `POST /admin/business-meetings`, list `POST /admin/business-meetings/list`, detail `GET /admin/business-meetings/{id}`, plus a cancel endpoint); tables live under `/admin/meeting-tables`. **(2) Speaker meeting requests** (`SpeakerMeetingRequest`, D-269) flow attendee → speaker from the app (`meeting_request_sheet.dart` + `meeting_slot_pickers.dart`, reachable from routes `/meet` and the VIP `/meetings` — B18 deleted the dead `/bilateral-meetings` and `/saved-meetings` sentinels, which never hosted the sheet) into the **SMR desk** at `/admin/speaker-meeting-requests` (`SimfDataGrid` + Respond modal: Accept/Reject + optional note; D-716 hall binding on Accept moves the row to `AwaitingSpeaker`). **(3) Delegation meeting requests** (`DelegationMeetingRequest`, delegation ↔ delegation) are worked from `/admin/delegation-meetings` (team accept/reject + notify/email). Two sibling desks mirror the SMR pattern (D-500 Wave 5): `/admin/document-requests` and `/admin/badge-requests` — accepting a badge request applies the requested title to `UserProfile.JobTitle`. Speaker free-slots derive from availability windows at `/admin/speaker-availability` (D-474/476) and halls at `/admin/hall-availability` (D-715). Every desk is permission-gated (`SpeakerMeetingRequests.View`/`.Manage`, `DelegationMeetings.View`, `ParticipationDocumentRequests.View`, `BadgeUpdateRequests.View`/`.Manage`, `BusinessMeetings.View`). **Critical invariant this flow guards:** meetings are only ever `Scheduled`/`Confirmed` — there is *no* meeting check-in / arrival / attendance concept anywhere in the system.

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-BF-06-001 | Admin schedules a Confirmed business meeting at a table for 2+ participants | happy | P0 |
| E2E-BF-06-002 | Attendee submits a speaker meeting request in-app → SMR desk Accepts with a note | happy | P0 |
| E2E-BF-06-003 | SMR desk: Pending → Pending re-response is rejected (`SPEAKER_MEETING_REQUEST_STATUS_INVALID`) | error | P1 |
| E2E-BF-06-004 | App request to a speaker who did not opt in → `SPEAKER_MEETING_REQUESTS_NOT_ALLOWED` | error | P1 |
| E2E-BF-06-005 | SMR desk: Accept + bind a hall/slot → row moves to `AwaitingSpeaker` (D-716) | happy | P1 |
| E2E-BF-06-006 | Business meeting with fewer than 2 distinct participants → `MEETING_PARTICIPANT_INVALID` | error | P1 |
| E2E-BF-06-007 | Business meeting double-books an occupied table slot → `BUSINESS_MEETING_TABLE_CONFLICT` | error | P1 |
| E2E-BF-06-008 | Cancel an already-cancelled business meeting → `BUSINESS_MEETING_NOT_CONFIRMED` | error | P2 |
| E2E-BF-06-009 | Delegation-meetings desk: team Accept + notify/email the counter-delegation | happy | P1 |
| E2E-BF-06-010 | Badge-request desk: Accept applies the requested title to `UserProfile.JobTitle` | happy | P1 |
| E2E-BF-06-011 | Auth gate: non-admin blocked at `/admin/speaker-meeting-requests`; list omits requester email PII | auth | P0 |
| E2E-BF-06-012 | Respond call returns HTTP 500 → bilingual SimfAlert, row stays Pending | resilience | P2 |
| E2E-BF-06-013 | Invariant: no desk or endpoint exposes a meeting check-in / arrival / attendance action | resilience | P2 |
| E2E-BF-06-014 | RTL / bilingual render of the SMR desk + Respond modal | i18n | P1 |

### Scenarios

### E2E-BF-06-001 — Golden: schedule a Confirmed business meeting

```gherkin
Feature: Admin-arranged business meeting (no attendee queue)
  Background:
    Given the API is reachable on http://localhost:5175
    And the Control Panel is reachable on http://localhost:5158
    And an administrator is signed in (password + TOTP via the Get-Totp helper)
    And a Meeting/General hall "Meeting Hall A" exists with an active table "T-04"

  Scenario: Admin schedules a two-party business meeting from the CP
    Given the admin opens /admin/business-meetings
    And clicks "Schedule meeting"
    When they pick table "T-04" in "Meeting Hall A"
    And set the slot 2026-06-16 10:00–10:30 UTC
    And set MeetingType = B2B
    And add participant company "Zamil Offshore"
    And add participant visitor "Fahad Al-Otaibi"
    And enter Notes = "Offshore logistics intro"
    And click "Save"
    Then POST /admin/business-meetings returns ApiResult.Success
    And the created BusinessMeeting.Status = Confirmed
    And ScheduledByUserId is the signed-in admin
    And the grid shows one Confirmed row at "T-04" for the 10:00–10:30 slot
    And no attendee request / approval queue is involved (created straight to Confirmed)
```

### E2E-BF-06-002 — Golden: attendee speaker request → SMR desk Accept

```gherkin
Scenario: Attendee requests a speaker meeting in-app, admin accepts it
  Given speaker "Dr. Layla Al-Harbi" has AllowsMeetingRequests = true
  And a VIP-tier visitor is signed in on the app (OTP read from SIMF_Identity.AccountCodes)
  When the visitor opens the speaker profile and taps "Request a meeting"
  And meeting_request_sheet.dart submits RequesterName="Omar Al-Ghamdi", Subject="Naval logistics collaboration"
  Then a SpeakerMeetingRequest is created with Status = Pending
  When an administrator opens /admin/speaker-meeting-requests
  Then the new row shows Speaker="Dr. Layla Al-Harbi", Requester="Omar Al-Ghamdi", a Pending pill
  When the admin clicks the quiet "Respond" (reply) action on the Pending row
  And selects Status = Accept
  And enters a note "Confirmed — see you at booth 12"
  And clicks "Send response"
  Then the response returns ApiResult.Success
  And the row's Status pill becomes Accepted
  And RespondedAt + RespondedByUserId are stamped
```

### E2E-BF-06-003 — SMR: Pending → Pending is rejected

```gherkin
Scenario: Re-responding a request with the same Pending status is a 400
  Given a SpeakerMeetingRequest for "Dr. Layla Al-Harbi" is Pending
  When an admin submits a response that leaves the status at Pending
  Then the API returns HTTP 400
  And ApiResult.Error.Code = "SPEAKER_MEETING_REQUEST_STATUS_INVALID"
  And a bilingual SimfAlert error is shown
  And the row is still Pending (no RespondedAt written)
```

### E2E-BF-06-004 — App request to a speaker who did not opt in

```gherkin
Scenario: Speaker has not enabled meeting requests
  Given speaker "Capt. Sultan Al-Dosari" has AllowsMeetingRequests = false
  And a visitor is signed in on the app
  When the app attempts to submit a SpeakerMeetingRequest for that speaker
  Then the API returns HTTP 409
  And ApiResult.Error.Code = "SPEAKER_MEETING_REQUESTS_NOT_ALLOWED"
  And the app shows a bilingual error (no "Request a meeting" affordance was expected on that profile)
  And no SpeakerMeetingRequest row is created
```

### E2E-BF-06-005 — SMR Accept with hall binding → AwaitingSpeaker

```gherkin
Scenario: Accepting and binding a hall slot moves the request to AwaitingSpeaker (D-716)
  Given a Pending SpeakerMeetingRequest for "Dr. Layla Al-Harbi"
  And "Meeting Hall A" has a free slot published under /admin/hall-availability
  When the admin opens the Respond modal and selects Status = Accept
  And picks Hall = "Meeting Hall A"
  And picks a free slot 2026-06-16 14:00–14:30 UTC
  And optionally picks table "T-04"
  And clicks "Send response"
  Then the response returns ApiResult.Success
  And the row's Status becomes AwaitingSpeaker (double opt-in, awaiting the speaker)
  And SlotStart/SlotEnd hold the bound hall slot and HallId is set
  And a plain Accept with the Hall left empty would instead land on Accepted
```

### E2E-BF-06-006 — Business meeting needs ≥ 2 participants

```gherkin
Scenario: Scheduling with a single participant is rejected
  Given the admin opens /admin/business-meetings and clicks "Schedule meeting"
  And picks table "T-04" for a valid slot
  When they add only one participant (visitor "Fahad Al-Otaibi") and click "Save"
  Then POST /admin/business-meetings returns HTTP 400
  And ApiResult.Error.Code = "MEETING_PARTICIPANT_INVALID"
  And the English message reads "A meeting needs at least two distinct participants."
  And a bilingual SimfAlert error is shown; no BusinessMeeting is created
```

### E2E-BF-06-007 — Business meeting table double-book

```gherkin
Scenario: A second meeting on the same table + overlapping slot conflicts
  Given a Confirmed BusinessMeeting occupies table "T-04" for 2026-06-16 10:00–10:30 UTC
  When the admin tries to schedule another meeting at "T-04" for 2026-06-16 10:15–10:45 UTC
  Then POST /admin/business-meetings returns HTTP 409/400
  And ApiResult.Error.Code = "BUSINESS_MEETING_TABLE_CONFLICT"
  And a bilingual SimfAlert error is shown
  And the same overlap booked against one shared attendee instead raises "BUSINESS_MEETING_PARTICIPANT_CONFLICT"
```

### E2E-BF-06-008 — Cancel an already-cancelled meeting

```gherkin
Scenario: Cancelling a meeting that is not Confirmed is rejected
  Given a BusinessMeeting was already cancelled (Status = Cancelled, CancellationReason recorded)
  When the admin issues the cancel action for that same meeting id again
  Then the API returns HTTP 409
  And ApiResult.Error.Code = "BUSINESS_MEETING_NOT_CONFIRMED"
  And the row stays Cancelled with its original CancelledByUserId / CancelledAt
  And a first, valid cancel of a Confirmed meeting instead succeeds and stamps CancelledAt
```

### E2E-BF-06-009 — Delegation-meetings desk Accept + notify

```gherkin
Scenario: Team accepts a delegation-to-delegation meeting request and notifies the other side
  Given a DelegationMeetingRequest exists between delegation "Royal Saudi Naval Forces" and "Hellenic Navy"
  And an administrator opens /admin/delegation-meetings (gated by DelegationMeetings.View)
  When they Accept the request and choose to notify/email the counter-delegation
  Then the response returns ApiResult.Success and the row shows Accepted
  And a notification/email is queued to the counter-delegation contact
  And an invalid transition (e.g. responding to a non-existent id) returns
      ApiResult.Error.Code = "DELEGATION_MEETING_REQUEST_NOT_FOUND"
  And a malformed accept/reject payload returns "DELEGATION_MEETING_REQUEST_INVALID"
```

### E2E-BF-06-010 — Badge request Accept applies JobTitle

```gherkin
Scenario: Accepting a badge update request writes the requested title onto the profile
  Given a badge update request from "Omar Al-Ghamdi" with CurrentJobTitle="Operations Officer"
  And RequestedJobTitle="Head of Fleet Operations", Status = Pending
  And an administrator opens /admin/badge-requests (gated by BadgeUpdateRequests.View)
  When they click "Respond", select Accept, and click "Send response"
  Then the response returns ApiResult.Success and the row becomes Accepted
  And the requester's UserProfile.JobTitle is now "Head of Fleet Operations"
  And re-responding the same row while it stays Pending returns
      ApiResult.Error.Code = "BADGE_UPDATE_REQUEST_STATUS_INVALID"
  And the sibling /admin/document-requests desk enforces "PARTICIPATION_DOCUMENT_REQUEST_STATUS_INVALID" the same way
```

### E2E-BF-06-011 — Auth gate + PII omission on the SMR desk

```gherkin
Scenario: Non-admin is denied; the list never leaks requester email
  Given a signed-in user without the SpeakerMeetingRequests.View permission
  When they navigate to /admin/speaker-meeting-requests
  Then they are redirected to /not-permitted (HTTP 200 shell, no data loaded)
  And the list POST would be refused by the endpoint policy
  Given instead an authorised admin views the desk
  Then the grid columns show Speaker, Requester name, Subject, Status, CreatedAt, RespondedAt
  And the requester's email is NOT present in the list payload (PII omitted)
  And the email only loads on demand inside the Respond modal detail lookup
  And the Respond (reply) action is wrapped in AuthorizedAction for SpeakerMeetingRequests.Manage
```

### E2E-BF-06-012 — Respond desk resilience on a 500

```gherkin
Scenario: The Respond call fails with a server error
  Given an admin has the Respond modal open on a Pending SpeakerMeetingRequest
  And the respond endpoint is forced to return HTTP 500
  When the admin clicks "Send response"
  Then a bilingual SimfAlert error is shown at the top of the page
  And the modal stays usable (the admin can retry)
  And the row remains Pending with no RespondedAt written
  And the browser console shows no unhandled client exception
```

### E2E-BF-06-013 — Invariant: no meeting check-in / attendance

```gherkin
Scenario: Meetings are Scheduled/Confirmed only — never "attended"
  Given a Confirmed BusinessMeeting and an Accepted SpeakerMeetingRequest
  When a tester inspects every meeting desk
      ( /admin/business-meetings, /admin/speaker-meeting-requests,
        /admin/delegation-meetings, /admin/document-requests, /admin/badge-requests )
  Then no desk exposes a check-in, arrival, attendance or "mark attended" action
  And no /admin meeting endpoint transitions a meeting into an attended/checked-in state
  And the BusinessMeetingStatus lifecycle is limited to Confirmed → Cancelled
  And the MeetingRequestStatus lifecycle is Pending → Accepted/Rejected/AwaitingSpeaker/Cancelled
```

### E2E-BF-06-014 — RTL / bilingual render

```gherkin
Scenario: SMR desk renders correctly in Arabic (RTL)
  Given an administrator is signed in and switches the CP language to Arabic
  When they open /admin/speaker-meeting-requests
  Then the SimfBanner title, grid headers, and Status pills render in Arabic, right-to-left
  And the page has no horizontal overflow (scrollWidth == clientWidth)
  When they open the Respond modal
  Then the status <select>, hall/slot binding fields, and note textarea are RTL-aligned
  And validation/response failures surface as a bilingual SimfAlert error
```

### Notes

- **Three separate models — do not conflate.** `BusinessMeeting` is admin-arranged with **no attendee-facing request/approval queue** (created `Confirmed`); only `SpeakerMeetingRequest` and `DelegationMeetingRequest` have a submit → review lifecycle. Scenario 001 must assert "no queue", not an approval step.
- **No meeting check-in exists (grounding-critical).** The GPS geofence → arrival → attendance chain (FR-305/506/1103) is a deferred item (D-211, still open pending the G-OI-2 venue-boundary decision). Scenario 013 is a standing regression guard: a tester finding any "check-in/attended" affordance on a meeting desk should treat it as a defect, not a gap to fill.
- **Speaker requests are VIP-only (D-729).** Submitting a `SpeakerMeetingRequest` requires a VVIP/VIP-tier requester; the app hides the "Request a meeting" CTA for non-VIP and the server backstops with `403 Forbidden`. Scenario 002's requester is therefore VIP-tier. Scenario 004's `SPEAKER_MEETING_REQUESTS_NOT_ALLOWED` (a `409`) is checked *before* the VIP gate, so a speaker who did not opt in is rejected regardless of the requester's tier.
- **Delegation desk error code:** the delegation desk raises `DELEGATION_MEETING_REQUEST_INVALID` / `DELEGATION_MEETING_REQUEST_NOT_FOUND` — there is **no** `DELEGATION_MEETING_REQUEST_STATUS_INVALID` variant (unlike the SMR/badge/document desks, which do use `*_STATUS_INVALID`; note the document desk's code is `PARTICIPATION_DOCUMENT_REQUEST_STATUS_INVALID`, not `DOCUMENT_REQUEST_STATUS_INVALID`). Assert the codes verbatim as written.
- **D-716 double opt-in:** an SMR Accept with a hall/slot binding lands on `AwaitingSpeaker`, not `Accepted`; the speaker's own confirmation via a public token link (Slice C) is a separate flow outside this section's scope and is only stubbed here.
- **PII posture:** the SMR list intentionally omits the requester email; it is resolved on demand inside the Respond modal (see the `_loadingDetail` path). Scenario 011 asserts this — do not add email to the grid payload.
- **Auth setup:** admin TOTP is always generated via the `Get-Totp` helper and visitor OTP is read from `SIMF_Identity.AccountCodes` at run time — never a literal secret in a scenario.
- **⚠ unverified:** exact Arabic toast copy for the SMR / badge / delegation desks was not read this session — those toasts are asserted only by structure ("a bilingual SimfAlert error"). The `MEETING_PARTICIPANT_INVALID` English string in scenario 006 is quoted from `BusinessMeetingService.cs`.

---

## BF-07 — Q&A pipeline — pre + live, timing gates, AI-advisory + committee + moderator desk

This flow exercises the full three-stage audience-question pipeline across three surfaces. **Mobile** attendees ask from `send_question_screen.dart` (route `/live/question`, screen #26) via `POST /api/v1/app/sessions/{sessionId}/questions` (`SubmitSessionQuestionEndpoint`, policy `RequireApprovedAccount`, rate-limited). The service (`SessionQuestionService.SubmitAsync`) sets `Phase = now < Start ? Pre : Live` and enforces the **timing gate**: `Session.IsActive` is required; **before `Start`** any approved user may ask with no venue gate (ask-ahead); once **LIVE** (`now >= Start`, before `End`) the attendee must be at the hall — a real `HallAttendance` arrival row when the hall has a geofence (`GeofenceRadiusMeters != null`, D-242/FR-704), else the D-171 `IsAtVenue` self-assert flag (the app always sends `true`); **after `End`** the window is closed with zero grace → `SESSION_NOT_LIVE_FOR_QUESTIONS`. A venue-gate rejection returns `NOT_AT_VENUE` (403) and writes audit `SessionQuestionRejectedNotAtVenue`. Stage 1 is the **advisory** `IQuestionAiFilter` — default `StubQuestionAiFilter` (deterministic `stub-clean`, no AI call); the real `AiQuestionFilter` (`ai-clean` / `ai-flagged` / `ai-unavailable`) is wired only when `SessionQuestions:AiFilterEnabled=true` — the verdict is persisted to `SessionQuestion.AiFilterVerdict` and shown to the Committee but **never** changes status or auto-hides. Stage 2 is the **Scientific-Committee** cross-session queue in the **Control Panel** at `/admin/question-queue` (`QuestionQueueList.razor`, gated `Questions.View`; approve/hide gated `Questions.Moderate`, escalate `Questions.Escalate`) over `GET /admin/questions/queue` (+ `/approve`, `/hide`, `/escalate`). Stage 3 is the per-session **moderator desk** — CP `/sessions/{id}/moderate` (`SessionModerationDesk.razor`, page-gated `Questions.Moderate`) **and** the app `session_moderate_screen.dart` (route `/sessions/:sessionId/moderate`, screen #104) — showing only **Approved** questions with hide/unhide, push (on-stage → `IsPushed`+`PushedAt`) and reorder; the API authorises by the per-session `SessionModerator` grant **or** the `Administrator` role (which bypasses the grant). Grants are managed in CP at `/admin/session-moderators` (`SessionModeratorsList.razor`, `SessionModerators.View`/`.Assign`/`.Revoke`). Submitter **email is redacted** on both moderator desks (A9/D-185) but is shown on the Committee queue.

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-BF-07-001 | Golden journey: assign moderator → pre-question asked → Committee approves → moderator pushes on-stage | happy | P0 |
| E2E-BF-07-002 | LIVE question in a geofenced hall needs a real HallAttendance arrival; without it → NOT_AT_VENUE + audit | error | P0 |
| E2E-BF-07-003 | LIVE question in a non-geofenced hall falls back to the D-171 IsAtVenue self-assert (app sends true) | happy | P1 |
| E2E-BF-07-004 | After End the window is closed with zero grace → SESSION_NOT_LIVE_FOR_QUESTIONS | error | P0 |
| E2E-BF-07-005 | Inactive session (IsActive=false) rejects every question → SESSION_NOT_LIVE_FOR_QUESTIONS | error | P1 |
| E2E-BF-07-006 | QuestionText validation (empty / >1000) → SESSION_QUESTION_INVALID | error | P1 |
| E2E-BF-07-007 | AI filter is advisory-only: default stub tags `stub-clean`, question still lands Pending, nothing auto-hidden | happy | P1 |
| E2E-BF-07-008 | AI resilience: with AiFilterEnabled=true a provider failure yields `ai-unavailable` and never blocks submission | resilience | P2 |
| E2E-BF-07-009 | Committee queue permission gate (Questions.View/.Moderate/.Escalate); escalate stamps role + escalator | auth | P0 |
| E2E-BF-07-010 | Moderator-desk authorisation: per-session grant OR Administrator bypass; app non-moderator → 403 "not authorised" | auth | P0 |
| E2E-BF-07-011 | Moderator desk shows only Approved; hide/unhide, reorder (full-list), push (IsPushed+PushedAt) | happy | P1 |
| E2E-BF-07-012 | PII: submitter email redacted on the moderator desk but shown on the Committee queue | auth | P1 |
| E2E-BF-07-013 | RTL / bilingual render of the Committee queue + a bilingual SimfAlert on a rejected live question | i18n | P1 |

### Scenarios

### E2E-BF-07-001 — Golden journey (assign → ask ahead → approve → push)

```gherkin
Feature: Q&A pipeline golden journey across CP + mobile
  Background:
    Given the API is reachable and the Control Panel is signed in as an Administrator
    And an Administrator TOTP is generated via the Get-Totp helper (never a literal secret)
    And session "S-204" (SessionId 8f2a1c00-0000-4000-8000-000000000204) is IsActive=true
    And "S-204" Start is 2 hours in the FUTURE and End is 3 hours in the future
    And an approved visitor's OTP is read from SIMF_Identity.AccountCodes at run time

  Scenario: A pre-session question flows all the way to the stage
    # Stage 0 — CP grants the per-session moderator
    Given the Administrator opens /admin/session-moderators
    When they click "Add" and pick SessionId="S-204" and UserId=the moderator account
    Then a SessionModerator row (SessionId=S-204, UserId, AssignedByUserId=the admin) is created
    And a success SimfAlert appears

    # Stage 1 — the visitor asks AHEAD of start (no venue gate before Start)
    Given the approved visitor is signed in on the app and opens /live/question for "S-204"
    When they type QuestionText="What is the RSNF plan for unmanned surface vessels?"
    And they choose Recipient=Speaker and the app sends IsAtVenue=true
    And they submit to POST /api/v1/app/sessions/8f2a1c00-0000-4000-8000-000000000204/questions
    Then the API returns HTTP 200 ApiResult.Ok with a new question id
    And the row is persisted with Phase=Pre and Status=Pending
    And AiFilterVerdict="stub-clean" (advisory only)
    And audit event SessionQuestionSubmitted is written for the visitor

    # Stage 2 — the Scientific Committee approves it
    Given the Administrator opens /admin/question-queue
    Then the SimfDataGrid lists the question with Status=Pending and the submitter email visible
    When they click "Approve" on that row
    Then PUT /admin/questions/{id}/approve returns 200 and the row moves to Status=Approved

    # Stage 3 — the per-session moderator pushes it on stage
    Given the moderator opens /sessions/8f2a1c00-0000-4000-8000-000000000204/moderate
    Then the desk lists only Approved questions, with the submitter email REDACTED
    When they click "Push" on the question
    Then PUT /app/sessions/{id}/questions/{id}/push returns 200 with IsPushed=true and PushedAt set
```

### E2E-BF-07-002 — LIVE question in a geofenced hall requires a real arrival

```gherkin
Scenario: Geofenced hall gates the live question on a HallAttendance arrival row
  Given session "S-210" is IsActive=true and now is between its Start and End (LIVE)
  And "S-210" is assigned to a hall whose GeofenceRadiusMeters is NOT null (D-240)
  And the approved visitor has NO HallAttendance arrival row for "S-210"
  When they submit QuestionText="Can you expand on the sonar upgrade timeline?" with IsAtVenue=true
  Then the API returns HTTP 403 with ApiResult.Error.Code = "NOT_AT_VENUE"
  And the message is a bilingual pair ("You must have arrived at the hall to ask a question." / Arabic)
  And self-asserting IsAtVenue=true does NOT bypass the geofence gate
  And an audit row SessionQuestionRejectedNotAtVenue is written with gate="geofence" and ErrorCode=NOT_AT_VENUE

  When a HallAttendance enter record for (SessionId=S-210, the visitor) then exists
  And they resubmit the same question
  Then the API returns HTTP 200 and the question is persisted with Phase=Live
  And a briefly-closed arrival row (Leave set) still satisfies the gate (arrived at any point, not "currently inside")
```

### E2E-BF-07-003 — Non-geofenced hall falls back to the self-assert flag

```gherkin
Scenario: Live question in a hall with no geofence uses the D-171 self-assert toggle
  Given session "S-215" is LIVE (now between Start and End) and IsActive=true
  And "S-215" is in a hall whose GeofenceRadiusMeters IS null (QR-only / coordinates not seeded)
  When the approved visitor submits a valid question with IsAtVenue=true (the app always sends true)
  Then the API returns HTTP 200 and the question lands Phase=Live, Status=Pending
  And no HallAttendance lookup is required (the self-assert flag is the gate)

  When a caller instead submits with IsAtVenue=false
  Then the API returns HTTP 403 with Code = "NOT_AT_VENUE"
  And the audit row records gate="self-assert"
```

### E2E-BF-07-004 — Window closes at End with zero grace

```gherkin
Scenario: A finished session accepts no questions
  Given session "S-204" is IsActive=true but now is AFTER its End (PostEndWindow = TimeSpan.Zero)
  When the approved visitor submits any valid question
  Then the API returns HTTP 400 with ApiResult.Error.Code = "SESSION_NOT_LIVE_FOR_QUESTIONS"
  And the bilingual message reads "The session is over and no longer accepting questions." / (Arabic)
  And the session view is treated as a recording/archive, not a live broadcast
```

### E2E-BF-07-005 — Inactive session rejects every question

```gherkin
Scenario: An IsActive=false session is closed to questions regardless of the clock
  Given session "S-299" exists with IsActive=false
  And now is BEFORE its Start (would otherwise be the open ask-ahead window)
  When the approved visitor submits a valid question
  Then the API returns HTTP 400 with Code = "SESSION_NOT_LIVE_FOR_QUESTIONS"
  And the bilingual message reads "The session is not active." / (Arabic)

Scenario: An unknown session id is a not-found, not a window error
  Given no session exists for SessionId=00000000-0000-4000-8000-0000000000ff
  When a question is submitted to that id
  Then the API returns HTTP 404 with Code = "SESSION_NOT_FOUND"
```

### E2E-BF-07-006 — Question text validation

```gherkin
Scenario Outline: QuestionText must be 1..1000 characters (trimmed)
  Given session "S-204" is inside its open window and IsActive=true
  When the approved visitor submits QuestionText=<text>
  Then the API returns HTTP 400 with ApiResult.Error.Code = "SESSION_QUESTION_INVALID"
  And the bilingual message reads "Question text must be between 1 and 1000 characters." / (Arabic)

  Examples:
    | text                                   |
    | ""  (empty after trim)                 |
    | "   " (whitespace only, trims to 0)    |
    | a 1001-character string                |
```

### E2E-BF-07-007 — AI filter is advisory-only (default stub)

```gherkin
Scenario: The default stub tags a verdict but never changes status or hides
  Given the deployment runs the default wiring (SessionQuestions:AiFilterEnabled is false → StubQuestionAiFilter)
  And session "S-204" is inside its open window
  When an approved visitor submits QuestionText="Any comment on the Red Sea patrol cadence?"
  Then the API returns HTTP 200
  And AiFilterVerdict is persisted as "stub-clean" (no AI provider is called)
  And Status is still Pending (the AI stage never approves, hides, or escalates)
  And the verdict is surfaced to the Committee on /admin/question-queue as an advisory badge
  And no question is auto-hidden by the AI stage
```

### E2E-BF-07-008 — AI resilience: a provider failure never blocks submission

```gherkin
Scenario: With the real filter enabled, an AI outage degrades to ai-unavailable
  Given the deployment sets SessionQuestions:AiFilterEnabled=true (AiQuestionFilter is registered)
  And the central IAiService / seeded question-filter prompt is unreachable or returns a non-JSON reply
  When an approved visitor submits a valid question inside the open window
  Then the API still returns HTTP 200 and persists the question Pending
  And AiFilterVerdict is "ai-unavailable" (the safe fallback — submission is never blocked)

Scenario: A healthy real filter tags clean vs flagged, still advisory
  Given AiFilterEnabled=true and the AI service is healthy
  When a benign question is submitted
  Then AiFilterVerdict is "ai-clean" and Status stays Pending
  When a question the model deems abusive is submitted
  Then AiFilterVerdict is "ai-flagged" and Status is STILL Pending (advisory — the Committee decides)
```

### E2E-BF-07-009 — Committee queue permission gate + escalate

```gherkin
Scenario: The central Q&A queue is gated by the Questions.* permissions
  Given an admin whose role does NOT include Questions.View opens /admin/question-queue
  Then the CP RequirePermission gate denies them (redirect to /not-permitted)
  And GET /admin/questions/queue returns 403 for that principal

  Given an Administrator (wildcard "*") opens /admin/question-queue
  Then the SimfDataGrid shows Pending questions by default across all sessions
  And the list is capped at 200 rows, oldest-first

  Scenario: Escalate routes a question to a role and stamps the escalator
    Given the Administrator holds Questions.Escalate
    When they escalate a Pending question to Role="Scientific Committee — Naval Systems"
    Then PUT /admin/questions/{id}/escalate returns 200
    And the row records AssignedToRole="Scientific Committee — Naval Systems", EscalatedByUserId=the admin, EscalatedAt set
    And a holder of only Questions.View cannot approve, hide, or escalate (each action needs its own permission)
```

### E2E-BF-07-010 — Moderator-desk authorisation (per-session grant OR Administrator)

```gherkin
Scenario: The per-session grant admits the moderator; the Administrator bypasses it
  Given "S-204" has a SessionModerator grant for the moderator account
  When the moderator opens the app moderator screen (route /sessions/S-204/moderate, #104)
  Then GET /app/sessions/S-204/questions/moderate returns 200 with the Approved queue
  And an Administrator with NO grant for "S-204" also gets 200 (the Administrator role bypasses the grant)

Scenario: A signed-in app user with no grant is refused
  Given an approved visitor holds NO SessionModerator grant for "S-204" and is not an Administrator
  When they call GET /app/sessions/S-204/questions/moderate
  Then the API returns HTTP 403
  And the app renders it as a "not authorised" state (no queue is shown)
  And every desk mutation (hide, push, reorder) is refused with 403 for that user

Scenario: The CP desk page carries its own permission attribute
  Given a CP admin without Questions.Moderate opens /sessions/S-204/moderate
  Then the RequirePermission(Questions.Moderate) attribute denies them before any API call
```

### E2E-BF-07-011 — Moderator desk actions (Approved-only, hide/reorder/push)

```gherkin
Scenario: The desk lists only Approved questions and drives the live actions
  Given "S-204" has questions in Pending, Approved, and Hidden states
  When the moderator loads /sessions/S-204/moderate
  Then only Approved questions appear (Pending stays in the Committee queue; Hidden are retained for audit but not shown)
  And the natural order is (Order ASC, CreatedAt ASC) — new arrivals (Order=0) fall to the bottom by FIFO

  When the moderator clicks "Hide" on a question
  Then PUT /app/sessions/S-204/questions/{id}/hide with IsHidden=true returns 200 and it leaves the visible list
  When they "Unhide" it
  Then the same endpoint with IsHidden=false returns 200 and it reappears

  When they reorder the list to [q3, q1, q2]
  Then PUT /app/sessions/S-204/questions/reorder is sent the FULL ordered id list (full-list contract)
  And the persisted Order values reflect the new sequence

  When they "Push" the top question
  Then PUT /app/sessions/S-204/questions/{id}/push returns 200 with IsPushed=true and PushedAt set
```

### E2E-BF-07-012 — PII: email redacted on the desk, shown to the Committee

```gherkin
Scenario: The same submitter's email is visible to the Committee but redacted for the moderator
  Given a visitor "cdr.alharbi@rsnf.gov.sa" submitted a question on "S-204" that the Committee approved
  When an Administrator views the row on /admin/question-queue
  Then the submitter email "cdr.alharbi@rsnf.gov.sa" IS displayed (Committee needs the identity)
  When the moderator views the same question on /sessions/S-204/moderate (CP or app)
  Then the submitter email is REDACTED (A9 / D-185) — the DTO email field is null on the desk contract
  And the moderator can still hide / push / reorder using the question id, without the PII
```

### E2E-BF-07-013 — RTL / bilingual render

```gherkin
Scenario: Arabic Committee queue mirrors and a rejected live question shows a bilingual alert
  Given the Administrator opens /admin/question-queue and toggles Arabic
  Then the page renders with dir="rtl" and Arabic column headers and action labels
  And the SimfDataGrid, its filter, and the row action icons mirror to the right
  And no horizontal overflow appears (scrollWidth == clientWidth)

  Given an approved visitor on the app submits a live question to a geofenced hall without an arrival row
  When the API returns NOT_AT_VENUE
  Then the app shows a bilingual SimfAlert error (English + Arabic pair), not a generic toast
  And switching the app to Arabic renders the same error RTL with the Arabic copy
```

### Notes

- **AI stage is a stub by default.** The shipped wiring registers `StubQuestionAiFilter` (`stub-clean`, no provider call); the real `AiQuestionFilter` (`ai-clean`/`ai-flagged`/`ai-unavailable`) exists but is only registered when `SessionQuestions:AiFilterEnabled=true`. Under either wiring the verdict is **advisory** — it is persisted to `AiFilterVerdict` and shown to the Committee, but it never changes `Status`, never auto-hides, and never blocks submission (any AI failure degrades to `ai-unavailable`). Do not author a scenario that expects the AI to approve/hide a question.
- **Geofence gate depends on data seeding (D-242/FR-704, G-OI-2 open).** The LIVE venue gate is a real `HallAttendance` arrival only when the hall's `GeofenceRadiusMeters` is non-null; halls with no geofence (QR-only / coordinates not yet seeded) fall back to the D-171 `IsAtVenue` self-assert flag, and the app always sends `true`. On a non-geofenced hall the live gate is effectively a self-assertion — flag this to the tester when validating "must be at the venue". The broader GPS geofence → arrival → attendance chain remains a deferred D-211 item.
- **The arrival gate is "arrived at any point", not "currently inside".** There is deliberately no `Leave == null` filter — a visitor who briefly stepped out (row closed) keeps the right to ask within the window.
- **Two distinct moderator concepts.** The per-session `SessionModerator` grant (composite PK `SessionId`,`UserId`; managed at `/admin/session-moderators`) is unrelated to `MobileAppRole.Moderator` (broad mobile-app content authority). The `Administrator` role **bypasses** the per-session grant on the API. Note the CP desk page `/sessions/{id}/moderate` is additionally page-gated by `Questions.Moderate`, which is a CP-only convention on top of the API's per-session/Administrator check.
- **PII asymmetry is intentional (A9/D-185).** The moderator desk (CP and app) redacts the submitter email — the nullable DTO field is kept for wire-compat (D-219) but shipped null; the Committee queue ships the email because the committee needs the identity. A test that asserts the email on the desk is wrong.
- **Zero grace after `End`.** `PostEndWindow = TimeSpan.Zero` — there is no post-session grace period; `now > End` is immediately `SESSION_NOT_LIVE_FOR_QUESTIONS`.
- **Auth setup uses no literal secrets.** Admin TOTP comes from the `Get-Totp` helper; the approved visitor's OTP is read from `SIMF_Identity.AccountCodes` at run time. The submit and mutation endpoints are rate-limited (the `"auth"` limiter), so drive them at human pace.

---

## BF-08 — Session reminder + rating triggers (leave-hall / end-of-session / end-of-day / end-of-programme)

This cross-surface flow exercises the four background triggers that turn a booked seat into a timely nudge and, later, a rating prompt, plus the app rating surface and the Control-Panel viewer that closes the loop. **Backend workers** (`src/Backend/SIMF.Infrastructure/Operations/`): `SessionReminderWorker` (D-217) fires the "starts in 30 min" reminder (`ReminderLeadTime=30m`, poll every 1 min after a 1-min startup delay, dedup `Session.ReminderSent`, audience = every active seat where `ReleasedAt==null && ReservedForUserId!=null`); `SessionRatingPromptWorker` fires "rate this session" when `End` passes (6h back-fill, dedup `Session.RatingPromptSent`); `ProgrammeRatingPromptWorker` (D-679) fires `DayRatingRequest` at end-of-day to everyone who checked in that day and the `Event`+`Exhibition`+`App` trio at end-of-programme to everyone who ever checked in. **Hall exit** (`HallAttendanceService.RecordDepartureAsync`, D-713 GAP-A) closes the attendance row (`Leave`) then calls `PromptSessionRatingOnDepartureAsync`, which **shares one `DeduplicateByRelatedEntity=true` guard per (session, user)** with the clock-end worker so a leaver is never double-prompted. All prompts are **in-app only** (`SendEmail=false`, `NotificationKind` values `BookingConfirmed`, `SessionReminder`, `SessionRatingRequest`, `DayRatingRequest`, `EventRatingRequest`, `ExhibitionRatingRequest`, `AppRatingRequest`). Deep-links come from `NotificationKindCatalog.ClickUrlFor` (`SessionRatingRequest`→`/rate?code=Session&targetId=…`, `DayRatingRequest`→`/rate?code=Day&targetId=…`, `Event|App|Exhibition`→`/rate?code=…`, `BookingConfirmed`→`/badge`); the app `notifications_screen.dart` `_maybeDeepLink` navigates on tap against the allowlist `{'/rate','/badge'}`. **Mobile** `rate_screen.dart` (page #40, route `/rate`, auth-gated + attendee-role-gated to Visitor/Exhibitor via `_routeRoles`) reads `GET /api/v1/app/feedback/form` and posts `POST /api/v1/app/feedback/submit` (`FeedbackEndpoints.cs` / `RatingFormService.cs`, both `RequireApprovedAccount`); the five rating types are seeded by `RatingSeeder.cs` (`App`/`Event`/`Exhibition` Global, `Session` PerSession with Speaker/Sound/Light questions, `Day` PerDay; all `IsSystem=true`). **Control Panel** `/admin/ratings` (`RatingsList`, `PermissionCatalog.Ratings.View`, `POST /api/v1/admin/feedback/ratings` + `GET …/kpi`) and `/admin/rating-config` (`RatingConfig`, `PermissionCatalog.RatingConfig.View`). Time-based triggers are driven by **seeding `Start` / `End` / day boundaries and running the worker's internal scan with a controlled `now`, never by waiting**.

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-BF-08-001 | Golden thread: booked seat → 30-min reminder → end-of-session prompt → tap deep-link → submit rating → CP sees the response | happy | P0 |
| E2E-BF-08-002 | Reminder dedup: a second scan re-sends nothing; a late booker gets no back-reminder | resilience | P1 |
| E2E-BF-08-003 | Rate-on-leave: hall departure closes attendance and fires the session rating prompt (GAP-A) | happy | P0 |
| E2E-BF-08-004 | Shared one-per-(session,user) guard: after the hall-exit prompt, the clock-end worker sends no second prompt | resilience | P1 |
| E2E-BF-08-005 | End-of-day: `DayRatingRequest` reaches only attendees who checked in that day | happy | P1 |
| E2E-BF-08-006 | End-of-programme trio fires exactly once via the global SystemSetting marker | resilience | P1 |
| E2E-BF-08-007 | Auth gate: form/submit require an approved account; rate screen is auth+attendee-gated; CP list needs `Ratings.View` | auth | P0 |
| E2E-BF-08-008 | Submit validation: out-of-range stars / missing type+code rejected with the verbatim messages | error | P1 |
| E2E-BF-08-009 | Deep-link allowlist: a stale/foreign `clickUrl` outside `{/rate,/badge}` does not navigate | error | P1 |
| E2E-BF-08-010 | Per-attendee dispatch failure is swallowed; the batch continues and the dedup stamp still lands | resilience | P2 |
| E2E-BF-08-011 | RTL / bilingual render of the reminder notification + the rate screen | i18n | P1 |
| E2E-BF-08-012 | CP rating-config: a seeded system rating type cannot be deleted | error | P2 |

### Scenarios

### E2E-BF-08-001 — Golden thread (reminder → end-of-session prompt → submit → CP)

```gherkin
Feature: A booked attendee is reminded, prompted to rate, and the rating reaches the CP
  Background:
    Given the API is reachable and the SessionReminderWorker + SessionRatingPromptWorker are registered hosted services
    And the RatingSeeder has run so the "Session" rating type (PerSession, Speaker/Sound/Light questions) exists
    And an approved Visitor "Fatimah" is signed into the app (OTP read from SIMF_Identity.AccountCodes at run time)
    And session S "Future of Naval Logistics" / "مستقبل الخدمات اللوجستية البحرية" (IsActive=true) is in "Main Hall (H1)"
    And Fatimah holds an active seat on S (SeatReservation with ReleasedAt=null, ReservedForUserId=Fatimah) confirmed by a NotificationKind.BookingConfirmed tile that deep-links /badge

  Scenario: The 30-minute reminder fires to the active-seat holder
    Given S.Start is seeded to now + 20 minutes and S.ReminderSent is null
    When RunReminderScanAsync runs with now
    Then Fatimah receives one NotificationKind.SessionReminder tile in group "Sessions"
    And the tile title reads "Session starting soon" / "تبدأ الجلسة قريباً"
    And the notification is in-app only (SendEmail=false, no email row)
    And S.ReminderSent is stamped

  Scenario: The end-of-session prompt fires and deep-links to the rate screen
    Given S.End is seeded to now - 5 minutes and S.RatingPromptSent is null
    When RunRatingPromptScanAsync runs with now (inside the 6h back-fill)
    Then Fatimah receives one NotificationKind.SessionRatingRequest tile in group "Ratings"
    And its ClickUrlFor deep-link is "/rate?code=Session&targetId=<S.Id>"
    And S.RatingPromptSent is stamped

  Scenario: Tapping the tile submits a rating that the CP can read
    Given Fatimah opens the notifications screen and taps the "Rate this session" tile
    Then the app navigates to /rate?code=Session&targetId=<S.Id> (path is in the {/rate,/badge} allowlist)
    And GET /api/v1/app/feedback/form?code=Session&targetId=<S.Id> returns the overall-stars + Speaker/Sound/Light form
    When she selects OverallStars=4, Speaker=5, Sound=4, Light=4 and Comment="Excellent panel, clear audio."
    And POST /api/v1/app/feedback/submit succeeds with HTTP 200 (ApiResult.Ok, RatingSubmissionView)
    Then re-opening the form prefills her prior answers (one submission per user per (type, target))
    When an Administrator (TOTP via Get-Totp) opens /admin/ratings
    Then POST /api/v1/admin/feedback/ratings lists Fatimah's response for the "Session" type
    And GET /api/v1/admin/feedback/ratings/kpi shows the overall average reflecting her 4-star submission
```

### E2E-BF-08-002 — Reminder dedup (once-only, no late back-reminder)

```gherkin
Scenario: A second scan of the same window re-sends nothing
  Given session S.Start = now + 15 minutes and RunReminderScanAsync has already stamped S.ReminderSent
  When RunReminderScanAsync runs a second time with the same now
  Then S is not re-selected (the filter requires ReminderSent == null)
  And no duplicate SessionReminder tile is created
  And the scan returns 0 sessions reminded

Scenario: A visitor who books after the reminder fired gets no back-reminder
  Given the reminder for S already fired at T0 and S.ReminderSent is stamped
  When a new attendee reserves an active seat on S at T0 + 2 minutes
  And RunReminderScanAsync runs again while S is still inside its lead window
  Then the new attendee receives no SessionReminder (per-session dedup is coarse by design; acceptable for a "starts in 30 min" nudge)
```

### E2E-BF-08-003 — Rate-on-leave (hall departure, GAP-A)

```gherkin
Scenario: Leaving the hall closes attendance and prompts the session rating
  Given Fatimah has an open hall-attendance row on session S (entered, Leave=null)
  When RecordDepartureAsync(userId=Fatimah, sessionId=S) is invoked (a hall-exit gate scan)
  Then the row's Leave is set to now and an OperationLog "HallDepartureRecorded" audit row is written
  And PromptSessionRatingOnDepartureAsync dispatches one NotificationKind.SessionRatingRequest
  And it carries RelatedEntityType="Session", RelatedEntityId=S, SendEmail=false, DeduplicateByRelatedEntity=true
  And the returned HallAttendanceStatus reports the departure (leave time already committed)

Scenario: The rating prompt failing never fails the departure
  Given the notification dispatcher throws on this dispatch
  When RecordDepartureAsync runs
  Then the exception is logged and swallowed
  And Leave stays committed and the departure still returns success (the leave is not rolled back)
```

### E2E-BF-08-004 — Shared one-per-(session,user) guard (no double prompt)

```gherkin
Scenario: The clock-end worker does not re-prompt an attendee already prompted on hall exit
  Given Fatimah left hall H1 for session S and already holds a SessionRatingRequest for S (DeduplicateByRelatedEntity=true)
  And S.End later passes and S.RatingPromptSent is still null
  When RunRatingPromptScanAsync runs inside the back-fill window
  Then the dispatcher's DeduplicateByRelatedEntity guard suppresses a second SessionRatingRequest for (S, Fatimah)
  And Fatimah still has exactly one session-rating tile for S
  And S.RatingPromptSent is stamped so the session is not re-scanned (other unprompted attendees still get theirs)
```

### E2E-BF-08-005 — End-of-day prompt (checked-in audience only)

```gherkin
Scenario: DayRatingRequest reaches only attendees who checked in that day
  Given ProgrammeDay D1 "Day 1 — Opening" / "اليوم الأول" (Date=2026-11-24, IsActive=true, RatingPromptSent=null)
  And attendee A has a GateScan Direction=CheckIn, Outcome=Allowed on 2026-11-24 (event-local UTC+3)
  And attendee B has no check-in scan for that day
  And D1 has ended: its latest session End + 30-min grace is before now, within the 24h back-fill
  When RunDayPromptScanAsync runs with now
  Then attendee A receives one NotificationKind.DayRatingRequest in group "Ratings"
  And its deep-link is "/rate?code=Day&targetId=<D1.Id>" (RelatedEntityType="ProgrammeDay")
  And attendee B receives nothing
  And D1.RatingPromptSent is stamped (a second scan re-sends nothing, even for a zero-recipient day)
```

### E2E-BF-08-006 — End-of-programme trio (fires exactly once)

```gherkin
Scenario: The Event + Exhibition + App trio is dispatched once at programme end
  Given the last active ProgrammeDay has ended and now is past its DayEnd + 1h ProgramEndGrace, within the 24h back-fill
  And the SystemSetting "Notifications:ProgramEndRatingSentUtc" does not yet exist
  And attendee A checked in at least once during the programme; attendee C never checked in
  When RunProgramEndScanAsync runs with now
  Then attendee A receives three tiles: EventRatingRequest, ExhibitionRatingRequest, AppRatingRequest
  And their deep-links are "/rate?code=Event", "/rate?code=Exhibition", "/rate?code=App"
  And attendee C receives none of the trio
  And a SystemSetting row keyed "Notifications:ProgramEndRatingSentUtc" is written (IsActive=false marker)

Scenario: A later scan does not re-fire the trio
  Given the "Notifications:ProgramEndRatingSentUtc" marker already exists
  When RunProgramEndScanAsync runs again
  Then it returns false and dispatches nothing (the global marker gates it to exactly one run)
```

### E2E-BF-08-007 — Auth gate (approved account, auth+attendee-gated screen, CP permission)

```gherkin
Scenario: The rating API requires an approved account
  Given a request to POST /api/v1/app/feedback/submit with no bearer token
  Then the response is HTTP 401 Unauthorized
  Given a signed-in but not-yet-approved (Guest effective-role) account calls GET /api/v1/app/feedback/form
  Then the RequireApprovedAccount policy denies it with HTTP 403 Forbidden

Scenario: The rate screen is auth- and attendee-role-gated in the app router
  Given a signed-out user deep-links to /rate
  Then the app router redirects to /sign-in (route #40 requires auth) and does not render the form
  Given a signed-in Guest (pending approval) — or a Staff/Moderator — deep-links to /rate
  Then the role gate redirects them Home (route #40 is attendee-only: Visitor/Exhibitor via _routeRoles) and does not render the form

Scenario: The CP ratings viewer is permission-gated
  Given a signed-in administrator WITHOUT the Ratings.View permission
  When they navigate to /admin/ratings
  Then they are redirected to /not-permitted
  And POST /api/v1/admin/feedback/ratings returns HTTP 403 for that account
```

### E2E-BF-08-008 — Submit validation (verbatim messages)

```gherkin
Scenario: A submission with no type id and no code is rejected
  Given an approved attendee POSTs /api/v1/app/feedback/submit with RatingTypeId=empty and Code=null
  Then the response is a validation failure (ApiResult error envelope, HTTP 400)
  And it carries the message "A rating type id or code is required."

Scenario: Out-of-range stars are rejected
  Given the attendee submits OverallStars=6 for the "Session" type
  Then the response carries "Stars must be between 1 and 5."
  Given the attendee submits an answer with Stars=0
  Then the response carries "Each score must be between 1 and 5."
  Given the attendee submits an answer with an empty QuestionId
  Then the response carries "Each answer must reference a question."

Scenario: An over-long comment is rejected at 2000 chars
  Given the attendee submits a Comment of 2001 characters
  Then the response is a validation failure (MaximumLength(2000) aligned with EF HasMaxLength(2000))
```

### E2E-BF-08-009 — Deep-link allowlist (stale/foreign clickUrl is inert)

```gherkin
Scenario: A notification whose clickUrl is outside the allowlist does not navigate
  Given a notification tile carries a clickUrl of "https://evil.example/steal" (or a stale internal "/admin/ratings")
  When the user taps it on the notifications screen
  Then _maybeDeepLink does not navigate (only the "/rate" and "/badge" paths are in _allowedClickPaths; the query string is ignored)
  And the app stays on the notifications screen with no error

Scenario: A per-target rating tile with a missing target id yields no broken link
  Given a SessionRatingRequest was dispatched with a null RelatedEntityId
  Then ClickUrlFor returns null (an informational, non-navigating tile) rather than a broken /rate URL
```

### E2E-BF-08-010 — Per-attendee dispatch resilience

```gherkin
Scenario: One attendee's dispatch failure does not abort the batch or block the dedup stamp
  Given session S is due for a reminder and has three active-seat holders X, Y, Z
  And the dispatcher throws for Y only
  When RunReminderScanAsync runs
  Then X and Z each receive their SessionReminder tile
  And Y's failure is logged and skipped
  And S.ReminderSent is still stamped (the batch is not rolled back; S is not retried next minute)
```

### E2E-BF-08-011 — RTL / bilingual render

```gherkin
Scenario: Arabic reminder tile and rate screen mirror correctly
  Given the app language is Arabic and the device is in RTL
  When Fatimah receives the SessionReminder tile
  Then it renders "تبدأ الجلسة قريباً" with the body mirrored right-to-left
  When she taps through to /rate?code=Session&targetId=<S.Id>
  Then the rate screen shows the Arabic type name "الجلسة" and Arabic question labels "المتحدث" / "الصوت" / "الإضاءة"
  And the star row and the gold submit button are laid out RTL with no clipped or overflowing text
  And submitting posts the same payload shape as the LTR path (HTTP 200)
```

### E2E-BF-08-012 — CP rating-config: system type cannot be deleted

```gherkin
Scenario: A seeded system rating type is protected from deletion
  Given an administrator with RatingConfig.View opens /admin/rating-config
  And the "Session" rating type is a seeded system type (IsSystem=true)
  When they attempt to delete the "Session" type
  Then the delete is refused with a bilingual SimfAlert error (system types cannot be deleted)
  And the "Session" type, its PerSession scope, and its Speaker/Sound/Light questions remain intact
```

### Notes

- **Time is injected, never waited on.** Every trigger is exercised by seeding `Session.Start` / `Session.End` / `ProgrammeDay` boundaries and invoking the worker's `internal` scan method (`RunReminderScanAsync`, `RunRatingPromptScanAsync`, `RunDayPromptScanAsync`, `RunProgramEndScanAsync`) with a controlled `now` via `TimeProvider`. Do not rely on the live 1-minute poll or the 1-minute startup delay in tests.
- **Dedup is DB-backed and restart-proof.** `Session.ReminderSent`, `Session.RatingPromptSent`, `ProgrammeDay.RatingPromptSent`, and the `Notifications:ProgramEndRatingSentUtc` `SystemSetting` all survive a process restart, so a redeploy mid-window does not resend. The end-of-session prompt and the hall-exit prompt additionally share the dispatcher's `DeduplicateByRelatedEntity=true` guard per (session, user).
- **All prompts are in-app only** (`SendEmail=false`); assert the absence of any outbound email row, not just the presence of the tile.
- **Audience derivations differ by trigger:** reminder + end-of-session use active `SeatReservation` rows; end-of-day + end-of-programme use `GateScan` Check-In/Allowed rows resolved `GateScan.UserProfileId → UserProfile.Id → UserProfile.UserId` (single-DB, no cross-DB join per D-157).
- **Verified numeric kinds:** `BookingConfirmed=40`, `SessionReminder=41` (grounding); the rating-request kind integer values were not re-verified this pass — assert them by **name**, not by integer. ⚠ unverified: the exact HTTP status the `RatingConfig` delete-guard returns and its exact toast copy — E2E-BF-08-012 asserts the *behaviour* (delete refused, system type intact) rather than an unverified error code.
- **Rate route is auth- + attendee-role-gated (D-519 / D-666), and the API is gated independently.** Route #40 is in `_routeRoles` under `_attendee` = {Visitor, Exhibitor}: a signed-out deep-link to `/rate` is redirected to `/sign-in`, and a signed-in **non-attendee** — a pending/Guest account (`effectiveAppRole == guest`) or Staff/Moderator — is redirected **Home** by the role gate, so neither ever renders the form. The `form`/`submit` API is stricter still, behind `RequireApprovedAccount` (401 unauthenticated, 403 signed-in-but-unapproved) — surface both gates to the tester so the screen's role-redirect is not confused with the API's approval check.

---

## BF-09 — Close the year / snapshot-to-archive ("history this year")

This cross-page flow proves that "closing the forum for the year" is **not** one atomic action but **three independent operations**, and that each behaves correctly in isolation and together. It exercises the Control Panel archive page **`/admin/archive`** (`ArchiveList.razor`, gated `[RequirePermission(PermissionCatalog.Archive.View)]`), whose snapshot handler posts `SnapshotCurrentEditionRequest{ MakeVisible }` to **`POST /api/v1/admin/archive/snapshot-current`** (`SnapshotCurrentArchiveEndpoint`, `src/Backend/SIMF.Api/Endpoints/Archive/ArchiveEndpoints.cs`, D-275, gated `Archive.Snapshot` + `RequireApprovedAccount`, rate-limited `auth`). `AdminArchiveService.SnapshotCurrentAsync` (`src/Backend/SIMF.Infrastructure/Archive/AdminArchiveService.cs`) creates a **new `ArchiveEdition` for the current calendar year** with an auto title (`SIMF {year}` / `سيمف {year}`) and **server-computed counters** — `attendees` = distinct `GateScans` where `Outcome == Allowed && Direction == CheckIn && UserProfileId != null`, `sessions` = active `Session` count, `speakers` = active `Speaker` count — reusing `CreateAsync`, which enforces **one edition per year** (`409 archive_edition_year_duplicate`). The optional `MakeVisible=true` flips the archive-visibility toggle (op 2). Op 2 is **`PUT /admin/archive/visibility`** (`ArchiveVisibilityEndpoints.cs`, D-166, gated `Operations.Edit`) with the public read **`GET /app/archive/visibility`**; when the toggle is **off**, public **`GET /app/archive`** returns an **empty list** regardless of active editions (the "current edition stays hidden until the event ends" control). Op 3 is the **manual** forum status: `OrganizationProfile.Status` (`ForumStatus` — `Soon=0`, `Open=1`, `Archived=2`) + `CurrentYear`, edited on `/admin/organization-profile` via `OrganizationProfileAdminService.ParseStatus` (gated `OrganizationProfile.Manage`). Crucially, the snapshot flow does **NOT** flip forum status, does **NOT** deactivate the live event, and does **NOT** close sessions or registration. The app side reads through `archive_screen.dart` / `archive_models.dart`.

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-BF-09-001 | Golden journey: snapshot current event with `MakeVisible=true`, archive goes public | happy | P0 |
| E2E-BF-09-002 | Counters computed server-side (attendees / sessions / speakers) + auto bilingual title | happy | P0 |
| E2E-BF-09-003 | Snapshot with `MakeVisible=false` — edition created but public archive still hidden | happy | P1 |
| E2E-BF-09-004 | One-edition-per-year: second snapshot same year → `409 archive_edition_year_duplicate` | error | P1 |
| E2E-BF-09-005 | Auth gate: admin lacking `Archive.Snapshot` is denied the snapshot | auth | P0 |
| E2E-BF-09-006 | Independence: snapshot does NOT flip forum status, close sessions, or stop registration | happy | P0 |
| E2E-BF-09-007 | Op 2 alone: visibility toggle off → public `GET /app/archive` returns empty list | happy | P1 |
| E2E-BF-09-008 | Op 3 alone: manual forum-status change to `Archived` + `CurrentYear` on org-profile | happy | P1 |
| E2E-BF-09-009 | Validation: manual CRUD create with year `1999` / blank title → `400 archive_edition_invalid` | error | P1 |
| E2E-BF-09-010 | Hidden archive: public edition detail while toggle off → `404 archive_edition_not_found` | error | P1 |
| E2E-BF-09-011 | Resilience: server 500 mid-snapshot → bilingual `SimfAlert`, no partial edition persisted | resilience | P2 |
| E2E-BF-09-012 | RTL/bilingual render of CP archive list + app archive screen (`سيمف 2026`) | i18n | P1 |

### Scenarios

```gherkin
Feature: BF-09 Golden journey — snapshot the current event into a past edition
  As an Administrator closing the 2026 forum
  I want to snapshot the live event into the archive and make it public
  So that visitors see "SIMF 2026" as a completed past edition

Background:
  Given an Administrator "superadmin@zagali-ict.com" is signed in
  And their TOTP is generated via the Get-Totp helper (never a literal secret)
  And the admin role carries the "Archive.Snapshot" permission
  And the live App database currently holds 14 active sessions and 9 active speakers
  And 372 distinct profiles have an Allowed CheckIn gate-scan for this event
  And no ArchiveEdition exists yet for the year 2026

Scenario: One-click "make this year history" with immediate public visibility
  Given the admin opens the Control Panel page "/admin/archive"
  When they invoke the snapshot action with MakeVisible = true
  Then the client POSTs SnapshotCurrentEditionRequest{ MakeVisible: true } to "/api/v1/admin/archive/snapshot-current"
  And the API returns HTTP 200 with an ApiResult<AdminArchiveEditionDetail>
  And the returned edition has Year 2026, TitleEn "SIMF 2026", TitleAr "سيمف 2026"
  And its counters are Attendees 372, Sessions 14, Speakers 9
  And the new edition appears in the CP archive grid
  And an OperationLog / audit row "ArchiveEditionCreated" is written for the actor
  And because MakeVisible was true, "GET /api/v1/app/archive/visibility" now reports the archive is visible
  And the anonymous "GET /api/v1/app/archive" now lists the 2026 edition
```

```gherkin
Scenario: Counters are computed from live data, not supplied by the client
  Given the snapshot request body carries only { MakeVisible } — no counters and no title
  When the admin snapshots the current event
  Then AdminArchiveService.SnapshotCurrentAsync derives Year from the current UTC year (2026)
  And Sessions is the count of Session rows where IsActive is true (14)
  And Speakers is the count of Speaker rows where IsActive is true (9)
  And Attendees is the DISTINCT count of GateScan.UserProfileId where Outcome = Allowed and Direction = CheckIn (372)
  And the title is generated as "SIMF 2026" / "سيمف 2026" with no client-provided title accepted
  And an admin can later adjust these numbers via PUT "/api/v1/admin/archive/{id}" (Archive.Edit) — the computed values are only the starting point
```

```gherkin
Scenario: Snapshot without making it visible keeps the past edition hidden
  Given no ArchiveEdition exists yet for 2026
  And the archive-visibility toggle is currently OFF
  When the admin snapshots the current event with MakeVisible = false
  Then the API returns HTTP 200 and a new active 2026 edition is persisted
  And "GET /api/v1/app/archive/visibility" still reports the archive is NOT visible
  And the anonymous "GET /api/v1/app/archive" still returns an EMPTY list
  And the edition only becomes public after a separate PUT "/api/v1/admin/archive/visibility" with IsVisible = true
```

```gherkin
Scenario: Only one archive edition is allowed per calendar year
  Given a 2026 ArchiveEdition already exists (created by a prior snapshot)
  When the admin invokes the snapshot action again in the same calendar year
  Then CreateAsync detects the year clash
  And the API returns HTTP 409 with error code "archive_edition_year_duplicate"
  And the message is bilingual ("An archive edition for year 2026 already exists." / Arabic)
  And the CP surfaces a bilingual SimfAlert error and no second edition is created
```

```gherkin
Scenario: An admin without the Snapshot permission cannot close the year
  Given a signed-in admin whose role grants "Archive.View" but NOT "Archive.Snapshot"
  When they attempt "POST /api/v1/admin/archive/snapshot-current"
  Then the API rejects the request under the "Archive.Snapshot" policy (HTTP 403)
  And no ArchiveEdition is created
  And on the CP the snapshot action is not offered (the button is gated), while the read-only archive list still renders under "Archive.View"
```

```gherkin
Scenario: Snapshot does not close the live event, sessions, or registration
  Given the live event has 14 active sessions and open visitor registration
  And OrganizationProfile.Status is currently Open (ForumStatus.Open = 1)
  When the admin snapshots the current event into the 2026 archive
  Then the snapshot creates the archive edition only
  And OrganizationProfile.Status remains Open (it is NOT flipped to Archived)
  And every Session row keeps IsActive = true (no session is closed)
  And visitor registration and session join/reserve remain available
  And "closing the forum" still requires the separate, manual op-3 status change
```

```gherkin
Scenario: The visibility toggle alone gates the public archive
  Given at least one active ArchiveEdition exists (e.g. 2025 and 2026)
  And an admin with "Operations.Edit"
  When they set the toggle OFF via PUT "/api/v1/admin/archive/visibility" { IsVisible: false }
  Then the anonymous "GET /api/v1/app/archive" returns an EMPTY list despite the active editions
  When they set the toggle ON via the same endpoint { IsVisible: true }
  Then "GET /api/v1/app/archive" lists the active editions again
  And "GET /api/v1/app/archive/visibility" reflects the current toggle state in both directions
```

```gherkin
Scenario: Manually marking the forum finished for the year
  Given an admin with "OrganizationProfile.Manage" on "/admin/organization-profile"
  When they set Status to "Archived" and CurrentYear to 2026 and save
  Then OrganizationProfileAdminService.ParseStatus maps "Archived" to ForumStatus.Archived (value 2)
  And the org profile persists Status = Archived and CurrentYear = 2026
  And the app/website status badge reflects the archived state
  And this op is fully independent of the snapshot and the visibility toggle
```

```gherkin
Scenario: Manual archive-edition create rejects an out-of-range year and a blank title
  Given an admin with "Archive.Create" using the manual create form on "/admin/archive"
  When they POST "/api/v1/admin/archive" with Year 1999
  Then the API returns HTTP 400 with error code "archive_edition_invalid" (year must be between 2000 and 2100)
  When they instead POST with Year 2027 but an empty TitleEn
  Then the API returns HTTP 400 with error code "archive_edition_invalid" (English title 1–200 characters)
  And in both cases the CP shows a bilingual SimfAlert error and nothing is persisted
```

```gherkin
Scenario: A hidden archive returns not-found on the public detail read
  Given a 2026 ArchiveEdition exists but the archive-visibility toggle is OFF
  When an anonymous client requests "GET /api/v1/app/archive/{2026-edition-id}"
  Then the API returns HTTP 404 with error code "archive_edition_not_found"
  And the same 404 "archive_edition_not_found" is returned for a missing id or a soft-deleted (inactive) edition
```

```gherkin
Scenario: A server fault mid-snapshot leaves no half-written edition
  Given the snapshot save fails (e.g. the App database is briefly unavailable)
  When the admin invokes "POST /api/v1/admin/archive/snapshot-current"
  Then the API returns HTTP 500
  And the CP renders a bilingual SimfAlert error
  And no partial ArchiveEdition row for 2026 is persisted (the create is a single SaveChanges unit)
  And no ArchiveEditionCreated audit row is written for the failed attempt
```

```gherkin
Scenario: RTL and bilingual render of the archived edition
  Given the UI language is Arabic (dir = rtl)
  And a 2026 edition exists with TitleAr "سيمف 2026"
  When the admin views the CP archive grid at "/admin/archive"
  Then the Arabic title "سيمف 2026" renders right-to-left with correct column mirroring
  And scrollWidth equals clientWidth (no horizontal overflow) with no broken thumbnails
  When a visitor opens the archive on the app (archive_screen.dart) in Arabic
  Then the past edition card shows the Arabic title, summary and counters in RTL order
```

### Notes

- **There is no atomic "close edition" action.** "History this year" = three independent operations that must be performed (and tested) separately: (1) snapshot-to-archive, (2) archive-visibility toggle, (3) manual forum status. A tester must not assume any one of them cascades into the others.
- **Snapshot is fully server-driven.** `SnapshotCurrentAsync` accepts only `MakeVisible`; the year, bilingual title, and all three counters are computed. The counters are a **point-in-time starting value** — they are persisted as plain integers and can be edited afterwards via `PUT /admin/archive/{id}` (`Archive.Edit`); the archive edition does not re-query live data after creation.
- **Attendees depends on real gate-scan data.** On a fresh or unseeded database with no `Allowed` `CheckIn` `GateScan` rows, the `attendees` counter is legitimately **0** — this is expected behaviour, not a defect.
- **`MakeVisible` runs the toggle server-side.** When true, the service calls `IOperationsToggleService.UpdateArchiveVisibilityAsync` internally (no separate CP round-trip); the toggle's own endpoint `PUT /admin/archive/visibility` is separately gated by `Operations.Edit`.
- **Forum status is manual and enum-stable.** `ForumStatus` (`Soon=0`, `Open=1`, `Archived=2`, D-495) is append-only — never rename/reorder existing values. The snapshot flow never touches `OrganizationProfile.Status`; closing the forum for real is a deliberate op-3 edit under `OrganizationProfile.Manage`.
- **Auth setup uses no literal secrets:** admin TOTP is generated via the `Get-Totp` helper; any visitor OTP needed for gate-scan seeding is read from `SIMF_Identity.AccountCodes` at run time. Never embed a secret in the catalogue.

---

## BF-10 — Full Control-Panel smoke — no dead button, no crash, permission gate

> **Automated coverage as of 2026-07-29 (WS2/WS4).** `CpElementSweepTests` drives
> all 94 sweepable CP routes signed in as an Administrator and asserts each renders
> without bouncing, with **zero console errors**, no broken image, every same-origin
> link/asset < 400, and no horizontal overflow — that is **E2E-BF-10-001**, executed:
> 92 passed, 2 skipped (`/admin/meeting-tables`, `/admin/speaker-presentations` keep
> their grids behind a parent selection). **E2E-BF-10-003** is covered only in the
> "present and correctly gated" sense: the run diffs each page against
> `predicted-inventory.json`, so a toolbar button that vanished or lost its
> selection-gating fails the build — it does **not** click anything, so "the dialog
> actually opens" is still unexecuted. **E2E-BF-10-006** is held by the two build
> guards named above plus the sweep's own no-bounce assertion.
> **Still unexecuted: -002, -004, -005** — they need interaction, not a page load.

This flow is a systematic **smoke sweep over every ✅ Real Control-Panel route** listed in [`docs/pages/PAGE-INDEX.md`](../../pages/PAGE-INDEX.md) — the `/` dashboard plus the full `/admin/*` catalogue (`/admin/admins`, `/admin/visitors`, `/admin/roles`, `/admin/sessions`, `/admin/speakers`, `/admin/sponsors`, `/admin/organisations`, `/admin/programme-days`, `/admin/configuration`, `/admin/email/templates`, `/admin/statistics`, … through every row marked ✅ Real). It is driven on the CP surface (`http://localhost:5158`) against the admin API (`http://localhost:5175`). The flow exercises four cross-cutting rules that every CP page must honour: **(1)** each page renders with no error boundary and a clean console; **(2)** every list page is a `SimfDataGrid` (`src/Shared/SIMF.Components/Forms/SimfDataGrid.razor` — per-column filter + sort + select-all + row checkbox + quiet icon actions) and an empty list renders `SimfEmptyState` with bilingual copy; **(3)** primary action buttons are enabled and actually do something (open a working dialog / navigate / mutate — never dead); **(4)** the **permission gate** — every CP page carries `@attribute [RequirePermission(PermissionCatalog.X.Y)]` (`src/ControlPanel/SIMF.ControlPanel/Authorization/PermissionAuthorization.cs` — `RequirePermissionAttribute : AuthorizeAttribute`), so a signed-in user whose role lacks the page permission is redirected to `/not-permitted` with **HTTP 200** (the `AccessDeniedPath` wired in `Program.cs`). `PermissionCatalog` (`src/Shared/SIMF.Common/PermissionCatalog.cs`) is the single source of truth — `Administrator` resolves to the wildcard `"*"` (`PermissionCatalog.Wildcard`) and holds every code implicitly; the matching API endpoint gates with `Policies(PermissionCatalog.PolicyFor(code))` → the `perm:{code}` policy. Two build guards, `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs` and `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs`, fail the build if any gate is missing. One known render trap is also swept: a CP `SimfTextField` **without** `ValueExpression` (i.e. not `@bind-Value`, or `Numeric=true`) **freezes the page mid-render** (D-648).

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-BF-10-001 | Golden sweep — Administrator drives every ✅ Real route; each renders, no error boundary, zero console errors | happy | P0 |
| E2E-BF-10-002 | Every list page is a `SimfDataGrid` (filter + sort + select-all + row checkbox + quiet icon actions) | happy | P1 |
| E2E-BF-10-003 | Primary action buttons are enabled and do something (open a working create dialog — not dead) | happy | P0 |
| E2E-BF-10-004 | Required-field validation blocks a create submit with a bilingual `SimfAlert` | error | P1 |
| E2E-BF-10-005 | Empty list renders `SimfEmptyState` with bilingual copy, no error toast | happy | P1 |
| E2E-BF-10-006 | Permission gate — a role lacking the page permission is redirected to `/not-permitted` (HTTP 200) | auth | P0 |
| E2E-BF-10-007 | Administrator wildcard `"*"` reaches every ✅ Real route (no false 403) | auth | P1 |
| E2E-BF-10-008 | Side-nav is permission-filtered — items whose `RequiredPermission` the role lacks are hidden | auth | P1 |
| E2E-BF-10-009 | API gate parity — the list endpoint returns HTTP 403 for the same role the CP redirects | auth | P0 |
| E2E-BF-10-010 | Build guards fail if a page/endpoint gate is missing (`CpNavigationPermissionTests` + `PermissionEnforcementTests`) | resilience | P1 |
| E2E-BF-10-011 | D-648 render-trap guard — no CP `SimfTextField` without `ValueExpression` (page freeze) | resilience | P1 |
| E2E-BF-10-012 | Server 500 on page load renders an error surface, not a blank white crash | resilience | P2 |
| E2E-BF-10-013 | RTL / bilingual sweep — Arabic toggle mirrors every page, no horizontal overflow | i18n | P1 |

### Scenarios

### E2E-BF-10-001 — Golden sweep

```gherkin
Feature: Full Control-Panel smoke sweep
  As a production-readiness tester
  I want to open every real CP page as an Administrator
  So that I prove no page throws, no console errors, and nothing is dead

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator is signed in (password + TOTP via the Get-Totp helper)

Scenario: Drive every ✅ Real route from PAGE-INDEX
  Given the list of ✅ Real CP routes is read from docs/pages/PAGE-INDEX.md
    (the "/" dashboard plus every /admin/* row marked ✅ Real)
  When the tester navigates to each route in turn
    | route (examples)          |
    | /                         |
    | /admin/sessions           |
    | /admin/speakers           |
    | /admin/sponsors           |
    | /admin/roles              |
    | /admin/visitors           |
    | /admin/organisations      |
    | /admin/programme-days     |
    | /admin/configuration      |
    | /admin/email/templates    |
  Then each page reaches HTTP 200 and renders its SimfBanner / page header
  And no Blazor error-boundary ("An unhandled error has occurred") appears
  And the browser console shows zero errors on each page
  And the network list shows zero failed assets (no 404 img/css/js)
  And a DOM check confirms scrollWidth == clientWidth (no horizontal overflow)
```

**Evidence captured:**
- Full-page screenshot per route under `docs/screenshots/bf-10-smoke-{slug}.png`
- Console errors: 0 expected on every route
- Network failures: 0 expected on every route

### E2E-BF-10-002 — SimfDataGrid conformance

```gherkin
Scenario: Every CP list page is a SimfDataGrid, not a raw table
  Given the Administrator opens each list route in the sweep
    (e.g. /admin/sessions, /admin/speakers, /admin/sponsors, /admin/roles, /admin/visitors)
  Then the page renders a SimfDataGrid (not a bare <table>)
  And the grid exposes a per-column filter row
  And each sortable column header toggles ascending / descending
  And a select-all header checkbox selects every visible row
  And each row carries its own selection checkbox
  And row actions are quiet icon buttons (edit / delete / view), not text buttons
  And server-side paging works: changing the page fetches the next slice
```

### E2E-BF-10-003 — Primary action is not dead

```gherkin
Scenario: The page's primary action button is enabled and does something
  Given the Administrator is on /admin/speakers
  And the header shows the primary action "Add speaker" wrapped in <AuthorizedAction>
  When they click "Add speaker"
  Then the create dialog / form opens (the button is not inert)
  And every field is a real bound input (SimfTextField with @bind-Value, dropdowns populated)
  When the tester repeats this for the primary action on each list route in the sweep
  Then no primary action button is disabled-without-cause or a no-op
```

### E2E-BF-10-004 — Required-field validation

```gherkin
Scenario: Submitting a create form empty is blocked with a bilingual alert
  Given the Administrator has opened the "Add speaker" dialog on /admin/speakers
  When they click "Save" with the required Name / NameArabic fields left blank
  Then the form does not submit
  And a bilingual SimfAlert (validation) appears listing the missing required fields
  And the FluentValidation MaximumLength matches the EF HasMaxLength and the UI MaxLength
    (the validation triple-lock — over-long input is rejected the same way on all three)
  And no row is created (the grid count is unchanged)
```

### E2E-BF-10-005 — Empty-state bilingual render

```gherkin
Scenario: An empty list shows SimfEmptyState, not a broken grid
  Given a list surface has zero rows (e.g. /admin/media-partners on a fresh DB)
  When the Administrator opens the page
  Then the grid area renders the shared SimfEmptyState component
  And its copy is bilingual (English + Arabic)
  And no error toast and no error boundary appears
  And the primary "Add …" action is still enabled so the list can be populated
```

### E2E-BF-10-006 — Permission gate → /not-permitted

```gherkin
Scenario: A signed-in role lacking the page permission is denied
  Given a custom CP role "ProgrammeEditor" exists that holds Sessions.View
    but does NOT hold Roles.View
  And a user in that role is signed in (not an Administrator)
  When they navigate directly to /admin/roles
  Then RequirePermissionAttribute denies access
  And the browser is redirected to /not-permitted with HTTP 200
    (the AccessDeniedPath configured in Program.cs)
  And the /not-permitted page renders (no raw 403 body, no error boundary)
  When the same user navigates to /admin/sessions (they hold Sessions.View)
  Then the page renders normally
```

### E2E-BF-10-007 — Administrator wildcard reaches everything

```gherkin
Scenario: Administrator's "*" wildcard opens every real page
  Given an Administrator is signed in
  And their perm claim carries the wildcard PermissionCatalog.Wildcard ("*")
  When they drive every ✅ Real route in the sweep
  Then no route ever redirects them to /not-permitted
  And every page the sweep visits renders (the wildcard satisfies each RequirePermission check)
```

### E2E-BF-10-008 — Nav is permission-filtered

```gherkin
Scenario: The side menu only shows items the role may reach
  Given the "ProgrammeEditor" role (holds Sessions.View, lacks Roles.View / Admins.View) is signed in
  When the CP shell renders the navigation
  Then CpNavigation items whose RequiredPermission the role lacks are hidden
    (e.g. "Roles" and "Admins" do not appear in the menu)
  And items the role holds (e.g. "Sessions") do appear
  And the dashboard "/" (RequiredPermission = null) is always visible
```

### E2E-BF-10-009 — API gate parity

```gherkin
Scenario: The admin endpoint denies the same role the CP redirects
  Given the "ProgrammeEditor" role lacks Roles.View
  And its bearer token is minted with only the codes it holds
  When it calls the admin list endpoint behind /admin/roles
    (gated with Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Roles.View)))
  Then the API responds HTTP 403 Forbidden
  And the ApiResult<T> envelope carries the failure (no data leaked)
  When an Administrator (wildcard) calls the same endpoint
  Then it responds HTTP 200 with the paged ApiResult<T> payload
```

### E2E-BF-10-010 — Build guards catch a missing gate

```gherkin
Scenario: A page or endpoint with no permission gate fails the build
  Given a new CP page is added under /admin without an @attribute [RequirePermission(...)]
  When the solution is built and the test suite runs
  Then CpNavigationPermissionTests fails (the page / nav item has no RequiredPermission)
  And when a new admin endpoint ships without Policies(PermissionCatalog.PolicyFor(...))
  Then PermissionEnforcementTests fails
  And the missing gate is treated as a security defect, not a warning
  # An ungated admin page/endpoint is reachable by ANY signed-in admin regardless of role.
```

### E2E-BF-10-011 — D-648 render-trap guard

```gherkin
Scenario: No CP SimfTextField is left without a ValueExpression
  Given the CP uses the shared SimfTextField component
  When any page renders a SimfTextField that has neither @bind-Value nor ValueExpression
    (or is marked Numeric=true without a ValueExpression)
  Then that page FREEZES mid-render (blank / spinner that never resolves) — the D-648 trap
  Therefore during the sweep every text page (e.g. the create/edit dialogs on
    /admin/sessions, /admin/speakers, /admin/configuration) is opened and asserted to
    finish rendering (form fields interactive) within the normal load budget
  And any page that hangs on open is flagged as a D-648 regression
```

### E2E-BF-10-012 — Server 500 error surface

```gherkin
Scenario: A backend 500 on load shows an error surface, not a white crash
  Given the admin API for a page returns HTTP 500 (simulated backend fault)
  When the Administrator opens that page
  Then the page renders a bilingual error surface (SimfAlert / SimfEmptyState error variant)
  And it is not a blank white screen and not an unhandled Blazor error boundary
  And a retry / reload affordance is available
  And the console records the handled failure without an uncaught exception
```

### E2E-BF-10-013 — RTL / bilingual sweep

```gherkin
Scenario: Arabic toggle mirrors every swept page with no overflow
  Given the Administrator switches the CP language to العربية
  When they re-drive the swept routes (e.g. /, /admin/sessions, /admin/roles, /admin/sponsors)
  Then each page reloads with <html dir="rtl" lang="ar">
  And SimfDataGrid columns, filters and quiet icon actions mirror to the right
  And SimfEmptyState copy shows its Arabic text
  And a DOM check confirms scrollWidth == clientWidth on every page (no horizontal overflow)
  And no label renders as tofu / missing-glyph boxes
```

### Notes

- **Sweep is data-driven, not hand-listed.** The authoritative route set is the ✅ Real rows of `docs/pages/PAGE-INDEX.md` (the `/` dashboard + every `/admin/*` row). Rows marked 🚧 Stub (e.g. `/m/{module}`) and 🔒 Auth-only pages are **out of scope here** — auth-only login/account pages are covered by [`cp-auth-flow.md`](cp-auth-flow.md); do not re-drive them in this flow.
- **Operator/moderator sub-consoles** (`/admin/gates/operator`, `/sessions/{id}/moderate`, `/admin/gates/dashboard`) are role-scoped surfaces — when the tester's role holds their permission they render; otherwise they correctly land on `/not-permitted`. Both branches are the same gate assertion as E2E-BF-10-006.
- **No literal secrets.** Admin sign-in uses the `Get-Totp` helper for the TOTP step; any visitor OTP needed for a sub-check is read from `SIMF_Identity.AccountCodes` at run time. Never paste a TOTP secret, password, or token into a scenario.
- **`/not-permitted` returns HTTP 200** (it is the friendly access-denied page, `AccessDeniedPath` in `Program.cs`), while the **matching API returns HTTP 403** — the two are asserted separately (E2E-BF-10-006 vs -009); do not conflate them.
- **Custom test role.** E2E-BF-10-006/-008/-009 require a non-Administrator role (e.g. "ProgrammeEditor") granted a narrow code set (e.g. `Sessions.View`) via `/admin/roles/{id}/permissions`. If no such role is seeded in the target environment, create it as part of test setup — the wildcard-only Administrator cannot exercise the negative gate path.
- ⚠ unverified: the "ProgrammeEditor" role name is illustrative — use any seeded non-Administrator role that holds `Sessions.View` but lacks `Roles.View`; the flow does not depend on that exact name.

---

## BF-11 — Full mobile-App smoke — every screen, every role, no crash

> **Automated coverage as of 2026-07-29 (WS1.4).**
> `test/app/screen_element_contract_test.dart` drives **56 of 61** parameterless
> routes by path (5 correctly redirect for the test account) and asserts none throws
> during render and no icon-only control lacks an accessible name — 0 routes threw.
> Alongside it: `flutter analyze` 0 errors / 0 warnings, and `flutter test`
> **1247 passed / 0 failed** including every golden. Two screens remain **excluded
> and open**: `/sessions/join` and `/session-summaries` throw "BoxConstraints forces
> an infinite height" (nested scroll hosts; the fix belongs in `SimfPageShell` and
> touches ~40 screens — D-792, owner decision pending).

A **manual, human-driven** regression over EVERY screen of the SIMF Flutter app (`src/Mobile/simf_app`), one role at a time, proving nothing crashes and no navigation is dead. The authority for what exists is the route table in `lib/app/router.dart` (39 in-app mockup screens + the aux auth + FDS-014/role sentinel routes) and the per-page catalogue files `docs/tests/e2e/mobile-*.md`. The sweep exercises the five persistent bottom-nav tabs (Home `/`, Sessions `/sessions`, Badge `/badge`, Venue map `/map`, My Area `/my-area` — a single `StatefulShellRoute.indexedStack`, D-422), the public anonymous reads (`/speakers`, `/sponsors`, `/booths`, `/delegations`, `/archive`, `/news`, `/media`, `/about`, `/faq`, `/session-summaries`, `/live`, `/ai-summary`), and the role-restricted routes behind `redirectDecision` in `router.dart`. Key rules under test: the **login gate** on Sessions (#16) + session detail (#17) that router-redirects a guest to `/sign-in` (D-576); the **in-screen** login prompt on Live (#25) that never redirects and keeps `GET /api/v1/app/programme/sessions/{id}` `AllowAnonymous` (D-577); the **effective-Guest** collapse of an unapproved account via `CurrentUser.effectiveAppRole` (D-666); the **D-519 role gate** (attendee = Visitor + Exhibitor; Staff and Moderator are focused, not an attendee superset) that sends a wrong-role signed-in user home (`/`); the persistent bottom nav that **never dead-bounces**; pull-to-refresh on every data page (`SimfPullToRefresh` + `SimfPullableHost`, always-scrollable body); flexible/responsive width in portrait (the owner is on a **tablet** — content stretches, icons/avatars/QR stay fixed); authenticated-Dio image bytes with an `errorBuilder` (never `Image.network` for bearer/self-signed, D-422); and correct RTL. Endpoints touched incidentally by the write paths inside the sweep: `POST /api/v1/app/sessions/{sessionId}/questions`, `GET /app/gates/my-assignments`, `POST /app/gates/{gateId}/scans`. **Auth setup (manual):** an approved Visitor session via the email OTP read from `SIMF_Identity.AccountCodes` at run time; Staff/Moderator/admin second factor via the `Get-Totp` helper. No literal secrets — read the OTP/TOTP at run time.

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-BF-11-001 | Guest sweep — every public screen renders, bottom nav persists, no crash / no red screen | happy | P0 |
| E2E-BF-11-002 | Guest login-gate (D-576) — Sessions tab + session detail router-redirect to `/sign-in` | auth | P0 |
| E2E-BF-11-003 | Guest on Live (D-577) — in-screen "sign in to watch" prompt, no redirect, read stays anonymous | auth | P0 |
| E2E-BF-11-004 | Unapproved account = effective Guest (D-666) — universal-auth screens reachable, attendee routes bounce home | auth | P0 |
| E2E-BF-11-005 | Approved-Visitor sweep — every attendee screen reachable, no dead navigation | happy | P0 |
| E2E-BF-11-006 | Staff focused sweep — gate + register-visitor reachable, attendee routes redirect home, tabs never dead-bounce | auth | P1 |
| E2E-BF-11-007 | Moderator focused sweep — Q&A desk reachable, staff-only + attendee routes redirect home | auth | P1 |
| E2E-BF-11-008 | Send-question window — 400/404 `SESSION_NOT_LIVE_FOR_QUESTIONS` → bilingual not-open toast | error | P1 |
| E2E-BF-11-009 | Server-side authority conflict — Staff without `Gates.Operate` → 403 "not authorised" state (app gate passes, server denies) | error | P1 |
| E2E-BF-11-010 | Images — no black/broken images anywhere (badge QR, speaker photos, sponsor logos); QR errorBuilder on a GMS-less device | resilience | P1 |
| E2E-BF-11-011 | Pull-to-refresh + responsive width — always-scrollable body on every data page; no dead side gutters on the tablet | resilience | P1 |
| E2E-BF-11-012 | RTL sweep — Arabic mirrors every screen (start/end), no overflow / clipped text | i18n | P1 |
| E2E-BF-11-013 | No dead navigation — every bottom-nav / drawer / More / home-tile / deep-link target resolves | resilience | P1 |
| E2E-BF-11-014 | Bottom-nav persistence — switching tabs keeps the bar fixed and preserves each tab's state (IndexedStack, D-422) | happy | P2 |

### Scenarios

### E2E-BF-11-001 — Guest sweep (every public screen renders, no crash)

```gherkin
Feature: Full mobile-App smoke — guest
  As a tester on the tablet
  I want to open every guest-reachable screen
  So that no screen crashes, shows a red error screen, or dead-ends

Background:
  Given a fresh install signed out (a guest via "Continue as guest" on /guest)
  And the app is pointed at the production API

Scenario: Walk every public screen a guest can reach
  When the tester opens, in turn, Home "/", Venue map "/map",
       Speakers "/speakers" then a speaker profile "/speakers/{speakerId}",
       Sponsors "/sponsors" then "/sponsors/{sponsorId}",
       Booths "/booths" then "/exhibitors/{boothId}",
       Delegations "/delegations", Archive "/archive", News "/news",
       Media gallery "/media", About the forum "/about", FAQ "/faq",
       Session summaries "/session-summaries", and About the app "/about-app"
  Then each screen renders its content (never a Flutter red-screen / grey exception box)
  And the five-tab bottom nav stays visible on the tab screens (Home, Sessions, Badge, Map, My Area)
  And every list is a builder list (no jank) and pull-to-refresh works on each data screen
  And no image is black or broken (photos/logos load via the authenticated Dio bytes)
  And the layout fills the tablet width with token margins (no fixed 375px frame / dead gutters)
```

### E2E-BF-11-002 — Guest login-gate on Sessions + session detail (D-576)

```gherkin
Scenario: Tapping the Sessions tab as a guest bounces to sign-in
  Given the guest is on Home
  When they tap the "Sessions" bottom-nav tab (route #16, "/sessions")
  Then the router redirect (D-576) sends them to "/sign-in"
  And the Sessions list is never shown to the signed-out user

Scenario: Opening a session detail deep-link as a guest bounces to sign-in
  When the guest opens a session detail "/sessions/{sessionId}" (route #17)
  Then they are router-redirected to "/sign-in"
  # This supersedes the older "sessions are public" design; every signed-in role passes the gate.
```

### E2E-BF-11-003 — Guest on Live: in-screen prompt, not a redirect (D-577)

```gherkin
Scenario: A guest opening Live sees the need-login prompt, never the player
  Given the guest opens "/live?sessionId={id}" (route #25)
  Then they are NOT router-redirected (unlike Sessions/#16 under D-576) — they stay on the live screen
  And the player is NOT shown
  And an in-screen prompt reads "Sign in to watch the live stream." with a Sign-in button
  And no GET /api/v1/app/programme/sessions/{id} read is issued for the guest
  When the guest taps the Sign-in button
  Then the sign-in screen opens
  # The read endpoint itself stays AllowAnonymous — the app gates the screen, not the API.
```

### E2E-BF-11-004 — Unapproved account is effectively Guest (D-666)

```gherkin
Scenario: A pending sign-up account behaves as a guest for the role gate
  Given a tester completes sign-up (email OTP from SIMF_Identity.AccountCodes) but is NOT yet approved
  And the account resolves to effectiveAppRole = guest (D-666)
  When they open the universal-auth screens — Home "/", My Area "/my-area",
       Badge "/badge", Notifications "/notifications", Registration status "/registration/status"
  Then each opens for the signed-in-but-pending user (no bounce)
  And the identity/face-capture screen "/my-area/verify-identity" (#103) is reachable
       (moved to the universal-auth set by D-694 so sign-up is not dead-bounced)
  When they try an attendee-only route — Rate "/rate", Requests "/requests",
       or Join a session "/sessions/join"
  Then the D-519 role gate sends them Home "/" (their role is not in the attendee set)
  And the under-review guest-home card explains the account is awaiting approval
```

### E2E-BF-11-005 — Approved-Visitor sweep (every attendee screen reachable)

```gherkin
Scenario: A signed-in approved Visitor reaches every attendee screen with no dead navigation
  Given a Visitor is signed in (email OTP from SIMF_Identity.AccountCodes) and approved
  When they walk Sessions "/sessions" → a detail "/sessions/{id}" → My seat "/sessions/{id}/my-seat" (#18),
       Send a question "/live/question?sessionId={id}" (#26),
       Book a seat "/sessions/join" (#110) → Seat picker "/sessions/{id}/pick-seat" (#109),
       My sessions "/my-sessions" (#113), Requests "/requests" (#108),
       Meet people "/meet" (#35), Rate "/rate" (#40),
       My Contacts "/contacts" (#100) → Share "/contacts/share" → Scan "/contacts/scan",
       Badge "/badge" and Notifications "/notifications"
  Then every route resolves to its real screen (none falls through to ComingSoon unexpectedly)
  And the bottom nav stays fixed across tab switches
  And each data screen supports pull-to-refresh and shows loading → data → error (SimfErrorState) states
  And no image (avatar, badge QR, speaker photo) renders black or broken
```

### E2E-BF-11-006 — Staff focused sweep (gate ops reachable, attendee routes bounce)

```gherkin
Scenario: A Staff account reaches its focused screens and is kept out of attendee ones
  Given a Staff account is signed in (second factor via Get-Totp) and approved
  When they open the Gate scanner "/gates/scan" (#105)
       and Walk-in visitor registration "/staff/register-visitor" (#114)
  Then both open (App role-gate = AppRole.staff)
  When they navigate to an attendee-only route — Rate "/rate", Meet people "/meet",
       or My Contacts "/contacts"
  Then the D-519 role gate redirects them Home "/" (Staff is NOT an attendee superset)
  And the drawer/More menu shows no attendee-only entries for Staff
  And the five bottom-nav tabs (Home, Sessions, Badge, Map, My Area) still open for Staff and never dead-bounce
```

### E2E-BF-11-007 — Moderator focused sweep (Q&A desk reachable, others bounce)

```gherkin
Scenario: A Moderator reaches only the session Q&A desk among role screens
  Given a Moderator account is signed in (second factor via Get-Totp) and approved
  When they open the Session Q&A desk "/sessions/{sessionId}/moderate" (#104)
  Then it opens (App role-gate = AppRole.moderator, exclusive since D-519 — Staff no longer inherits it)
  When they navigate to the Gate scanner "/gates/scan" (#105, staff-only)
       or an attendee route like Rate "/rate"
  Then the D-519 role gate redirects them Home "/"
  And the Moderator's Home body + drawer are trimmed to their own pages, but the bottom bar is unchanged
```

### E2E-BF-11-008 — Send-question window error (SESSION_NOT_LIVE_FOR_QUESTIONS)

```gherkin
Scenario: Submitting a question to a closed session shows the not-open toast
  Given an approved attendee is on Send a question "/live/question?sessionId={id}" (#26)
  And the target session is past its End (the after-view is a recording)
  And the question text is "How deep is the reef?"
  When they submit POST /api/v1/app/sessions/{id}/questions
  Then the server returns HTTP 400 with error code "SESSION_NOT_LIVE_FOR_QUESTIONS"
  And the screen shows a bilingual not-open toast — a SnackBar rendering l10n.sendQuestionNotOpen
       ("Questions are closed for this session." / "الأسئلة مغلقة لهذه الجلسة.")
  # DEF-MOD-006 — the old copy claimed a 5-minute pre-start window the server
  # has never enforced (there is no lower bound; questions close at the End).
  And a 404 for the same submit maps to the same not-open toast
  And the question box keeps the typed text (nothing is lost)
```

### E2E-BF-11-009 — Server authority conflict: Staff without the gate grant (403)

```gherkin
Scenario: A Staff account without the GateOperator grant is denied by the server
  Given a Staff account (App role-gate passes) opens the Gate scanner "/gates/scan"
  But the account lacks the Gates.Operate permission / gate assignment
  When GET /app/gates/my-assignments returns HTTP 403
  Then the screen shows the "not authorised to operate gates" state (no scanner camera)
  # The router role-gate is only the UX gate; the server stays the real authority.

Scenario: A denied badge scan surfaces the server's reason, not a crash
  Given an assigned Staff operator scans an ineligible badge
  When POST /app/gates/{gateId}/scans returns outcome=Denied (HTTP 200) with a denialReasonCode
  Then the red "ممنوع / Denied" card shows the server's denial message
  And the console does not crash and offers "scan again"
```

### E2E-BF-11-010 — Images render (no black/broken), QR errorBuilder holds

```gherkin
Scenario: Every image across the sweep loads through authenticated bytes
  Given the tester walks the sweep as an approved Visitor
  Then avatars, speaker photos "/speakers", sponsor logos "/sponsors", booth/exhibitor logos,
       news/media thumbnails and the entry Badge QR all render actual pixels
  And none renders as a black box or a broken-image glyph
  # Bearer/self-signed URLs are fetched via the authenticated Dio client, never a raw Image.network (D-422).

Scenario: The Badge QR shows a fallback on a device with no Google Play Services
  Given the tablet has no GMS (the QR plugin native path is unavailable)
  When the Badge "/badge" screen renders
  Then the QR errorBuilder shows its fallback (the GMS QR stub), not a crash or a blank square
```

### E2E-BF-11-011 — Pull-to-refresh + responsive width on every data page

```gherkin
Scenario: Every data screen pulls to refresh with an always-scrollable body
  Given the tester is on any data screen (e.g. Speakers, Sponsors, News, Sessions, Notifications)
  When they pull down from the top
  Then a refresh spinner appears and the screen re-fetches (SimfPullToRefresh + SimfPullableHost)
  And even a short empty/error body is scrollable enough to pull (AlwaysScrollableScrollPhysics)
  And Registration status "/registration/status" is the one intentional exception (it uses an explicit Re-check button)

Scenario: Content fills the tablet width, fixed elements stay fixed
  Given the tablet (a wide portrait screen)
  Then cards, banners, tiles, buttons and forms stretch to the available width with token margin/padding
  And there are no dead side gutters (no content sized to a 375px phone frame)
  But icons, avatars, flag/badge boxes and the Badge QR square keep their intrinsic fixed sizes
  And the app stays portrait-locked (no landscape/two-pane)
```

### E2E-BF-11-012 — RTL sweep (Arabic mirrors every screen)

```gherkin
Scenario: Switching to Arabic mirrors every screen correctly
  Given the app language is set to Arabic (the primary locale)
  When the tester repeats the guest + Visitor sweep
  Then every screen lays out right-to-left (start/end, EdgeInsetsDirectional — never left/right)
  And back chevrons, list rows, toggles and the bottom nav mirror
  And no text is clipped or overflowing at textScaler 1.0 and 1.3
  And example Arabic labels render correctly — e.g. "الجلسات" (Sessions), "البطاقة"/"بطاقة الدخول" (Badge),
       "الملف الشخصى" (My Area), the gate movement toggle "دخول/خروج", and "وضع الضيف" (Guest mode)
```

### E2E-BF-11-013 — No dead navigation (every target resolves)

```gherkin
Scenario: Every navigation target from every surface resolves
  Given the tester exercises every tap target for the active role — the five bottom-nav tabs,
       every side-drawer entry, every "More" hub entry, every Home tile, and any deep-link
       (a notification clickUrl, a booth "أرشدني" → "/booths/{boothId}/map", a rate link "/rate?code=...")
  Then each target navigates to a real, matching route (no unmatched-route crash, no white screen)
  # B18 (2026-07-27): the two remaining ComingSoon sentinels — Bilateral meetings
  # "/bilateral-meetings" #204 and Saved meetings "/saved-meetings" #206 — were DELETED.
  # Both had no screen, no inbound navigation and nothing persisted behind them: #204's
  # Home tile went to the real VIP "/meetings" #116 with D-745, and #206's My-Area stat
  # tile became display-only (D-653) after D-609 retired the drill-down screens.
  And no declared route falls through to ComingSoonScreen with no inbound caller
  And a role never sees a menu entry that would only bounce it home
```

### E2E-BF-11-014 — Bottom-nav persistence (state preserved across tabs)

```gherkin
Scenario: Switching tabs keeps the bar fixed and preserves each tab's state
  Given a signed-in user scrolls the Home tab and then the My Area tab
  When they switch between the five tabs (Home, Sessions, Badge, Map, My Area)
  Then the bottom bar stays fixed with no page-transition animation (StatefulShellRoute.indexedStack, D-422)
  And each tab keeps its scroll position and state (an IndexedStack, not a fresh push per tap)
  And the tabs are identical for every signed-in role (a focused Staff/Moderator still gets all five, never a dead-bounce)
```

### Notes

- **This sheet is MANUAL.** It is driven by a human tester on the physical tablet; the agent session cannot drive the device (USB is flaky and the emulator's SurfaceView renders black for video/camera). Treat it as a whole-app regression pass, not an automated runner catalogue — the per-screen automated coverage lives in the individual `docs/tests/e2e/mobile-*.md` files this flow stitches together.
- **The tablet points at the production API by default** (owner rule), so an approved Visitor/Staff/Moderator must exist there; read the email OTP from `SIMF_Identity.AccountCodes` and the admin/staff TOTP via `Get-Totp` at run time — never a literal secret.
- **Live video is device-only.** Real YouTube/HLS playback (the D-577-gated player) needs a real device with Play Services; the emulator fails YouTube on cert trust. Only the not-live / recording / need-login states are checkable without live playback.
- **ComingSoonScreen is intentional** for undesigned entries (e.g. Bilateral meetings #204, Saved meetings #206). Seeing it there is expected; seeing it where a built screen should be (or an unmatched-route crash) is the defect this flow hunts.
- **`registration_status` is the one pull-to-refresh exception** — it is a gate screen with an explicit Re-check/poll button, deliberately not wrapped in `SimfPullToRefresh` (§13.6).
- **Empty content is not a crash.** On production, some collections (e.g. booths, delegations, FAQ, venue-map) may be un-seeded and render the `SimfEmptyState`; that is a data-gap, not a screen defect — record it separately from any real crash/red-screen.
- The D-519 role model is not a ladder: **attendee = Visitor + Exhibitor**; **Staff** and **Moderator** are focused sets (Staff ≠ Moderator; neither inherits attendee). A wrong-role signed-in user is redirected Home (`/`), never shown a not-permitted page — verify by the home-bounce, not an error screen.

---

## BF-12 — Website smoke + auth flows

> **Automated coverage as of 2026-07-29 (WS2).** `WebElementSweepTests` drives all
> 17 parameterless public routes and asserts HTTP < 400, zero console errors, no
> broken image, every same-origin link/asset < 400, and no horizontal overflow —
> **34 passed / 0 failed** (each route in LTR and Arabic). The run found and closed a
> real defect: `App.razor` linked `SIMF.Web.styles.css`, a scoped-CSS bundle this
> project can never generate, so every page 404'd it and logged a MIME-type refusal
> (D-795). **Not covered:** the auth flows in this flow's later scenarios, and
> `/sessions/{id}` + `/meeting/confirm`, which need seeded data or a token and are
> not reachable by URL alone.

> ### ⚠ PARTIALLY RETIRED 2026-07-27 — D-774
>
> **Owner decision: the public Website has no login and no account area.** The
> routes `/login`, `/login/verify`, `/forgot-password`, `/reset-password`,
> `/account`, `/account/profile`, `/account/notifications`, `/account/pending`
> and `/account/rejected` were deleted, together with the Website's cookie
> authentication, the `/auth/complete` + `/auth/sign-out` + `/session/status`
> endpoints and the `/account/api/*` BFF proxy. The per-page catalogue files this
> section cites (`web-login.md`, `web-otp-verify.md`, `web-forgot-password.md`,
> `web-reset-password.md`, `web-home.md`, `web-account-*.md`) were deleted with
> them.
>
> **Still live and still in scope:** the public smoke over `/`, `/programme`,
> `/visit` and the other anonymous marketing routes, and the anonymous
> token-addressed `/meeting/confirm` journey.
>
> **Retired — do NOT run:** every scenario below that signs a visitor in or
> asserts an account-area route, i.e. `E2E-BF-12-003` onward wherever it touches
> `/login`, `/login/verify` or `/account*`, plus the `/account` self-guard clause
> of `E2E-BF-12-001`. The equivalent visitor journeys are covered by the Flutter
> app catalogue (`mobile-sign-in.md`) and the admin journeys by `cp-auth-flow.md`.
> The text below is kept verbatim as the historical record; a clean re-issue of
> BF-12 as a pure public-site smoke is a tracked follow-up.

This cross-page business flow drives the **Website** surface (SIMF.Web) end-to-end as a production-readiness smoke: every ✅ *Real* Website route from the `web-*.md` catalogue is opened and asserted, then the visitor authentication journeys are run to completion. It exercises the four AllowAnonymous public routes — `/` (marketing landing, `web-landing`, fed by `GET /content/site`), `/programme` (`web-programme`, anonymous `SimfPublicClient` over `GET /api/v1/app/programme/sessions` + `GET /api/v1/app/speakers`), `/visit` (`web-visit`, static SSR), and `/account` (`web-home`, an AllowAnonymous route that self-guards to `/login` when unauthenticated) — plus the auth routes `/login`, `/login/verify`, `/forgot-password`, `/reset-password`, `/account/profile`, `/account/notifications`, `/account/pending`, `/account/rejected` and the public `/meeting/confirm`. The endpoints under test are `POST /api/v1/app/auth/sign-in` (`{ email, password, audience: "Web" }`), `POST /api/v1/app/auth/verify-otp` (`{ otpToken, code }`), the BFF hand-off `/auth/complete?reference=…`, `/auth/sign-out`, and `GET/POST /api/v1/app/meeting-actions/{token}`. The key rules it proves: **D-033** — a Visitor's second factor is an emailed OTP (read at run time from `SIMF_Identity.AccountCodes`, `Purpose = SignInOtp`, latest unconsumed — never a literal), whereas an admin uses TOTP; the **audience gate** — an Administrator account signing in on the Website is rejected by `SignInService.EnforceAudienceAsync` with `AUTH_WRONG_SURFACE_WEB` (the Website is the visitor surface; `/login` is AllowAnonymous, so the gate is audience + account-state routing, not a permission redirect); and, on every route, **page renders, zero console errors, zero broken assets (no 404 `<img>`), `scrollWidth == clientWidth` (no horizontal overflow), and the RTL toggle mirrors the layout**.

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-BF-12-001 | Public smoke sweep — `/`, `/programme`, `/visit` render anonymously; `/account` self-guards to `/login` (0 console errors, 0 broken assets, no h-overflow) | happy | P0 |
| E2E-BF-12-002 | Landing live content — `/` renders `SITE_DEFAULTS` then merges `GET /content/site`; feed offline keeps the defaults, page never blanks | happy | P1 |
| E2E-BF-12-003 | Visitor auth golden journey — `/login` → emailed OTP → `/login/verify` → `/auth/complete` → `/account/profile` | happy | P0 |
| E2E-BF-12-004 | Audience gate — Administrator credentials on the Website → 403 `AUTH_WRONG_SURFACE_WEB`, stays on `/login` | auth | P0 |
| E2E-BF-12-005 | Account-state route — pending visitor → `/account/pending` | auth | P1 |
| E2E-BF-12-006 | Account-state route — rejected visitor → `/account/rejected` with the verbatim bilingual reason | auth | P1 |
| E2E-BF-12-007 | Bad credentials — wrong password → 401 `AUTH_INVALID_CREDENTIALS` (identical whether the email exists or not) | error | P0 |
| E2E-BF-12-008 | Client validation — empty email + password → inline field errors, no `sign-in` request fires | error | P1 |
| E2E-BF-12-009 | State-block errors — unverified → `AUTH_EMAIL_NOT_VERIFIED`; disabled → `AUTH_ACCOUNT_DISABLED` | error | P1 |
| E2E-BF-12-010 | Forgot → reset round trip — `/forgot-password` (anti-enumeration) → emailed 6-digit code → `/reset-password` → `/login` | auth | P1 |
| E2E-BF-12-011 | Authenticated-area smoke + sign-out — `/account/profile` + `/account/notifications` render, `POST /auth/sign-out` clears the cookie → `/login` | happy | P1 |
| E2E-BF-12-012 | Public meeting-confirm link — `/meeting/confirm?token=…` GET-preview is token-safe; Confirm → Accepted; a used token → neutral "no longer valid" card | happy | P1 |
| E2E-BF-12-013 | OTP request throttle — a 6th sign-in code within the hour → 429 `RATE_LIMIT_EXCEEDED` | resilience | P2 |
| E2E-BF-12-014 | RTL / bilingual sweep — Arabic toggle on `/` and `/login` mirrors the layout (`<html dir="rtl" lang="ar">`), no h-overflow in RTL | i18n | P1 |

### Scenarios

### E2E-BF-12-001 — Public smoke sweep

```gherkin
Feature: Every Website route renders clean
  As a production-readiness tester
  I want to open every Real Website route and assert the render invariants
  So that the smoke pass proves no page is broken

Background:
  Given the API is reachable on http://localhost:5175
  And the Website is reachable on http://localhost:5115
  And the browser has NO auth cookie (a fresh anonymous session)

Scenario: The three anonymous public pages render and the signed-in home self-guards
  When I navigate to http://localhost:5115/
  Then the marketing landing renders (hero + dynamic sections)
  And the console reports zero errors
  And the network list has zero failed requests and zero broken <img> (no 404 assets)
  And document.scrollWidth equals document.clientWidth (no horizontal overflow)

  When I navigate to /programme
  Then the anonymous agenda renders (read from GET /api/v1/app/programme/sessions + GET /api/v1/app/speakers, no bearer)
  And the same three render invariants hold (0 console errors, 0 broken assets, no h-overflow)

  When I navigate to /visit
  Then the static "Visit & entry" page renders with no API call
  And the same three render invariants hold

  When I navigate to /account
  Then Home.razor detects Session.IsSignedIn = false in OnInitialized
  And the browser is redirected to /login (the AllowAnonymous route self-guards; there is no /not-permitted gate)
  And no unhandled exception reaches the console
```

**Evidence captured:**
- Screenshot per route: `docs/screenshots/bf12-smoke-{landing|programme|visit|account}.png`
- Console errors: 0 on every route
- Network: 0 failed requests / 0 broken `<img>` on every route
- DOM: `scrollWidth == clientWidth` asserted on `/`, `/programme`, `/visit`

### E2E-BF-12-002 — Landing live content merges the feed

```gherkin
Scenario: The landing merges GET /content/site over its built-in defaults
  Given the API is seeded with >=1 published session, speaker, sponsor, media-partner, news and archive edition
  And the browser is on http://localhost:5115/
  When index.html renders SITE_DEFAULTS client-side
  And loadSiteContentRemote() fetches GET /content/site (same-origin Website proxy, D-294)
  Then the fetch returns 200 with only the sections that have rows, each row carrying `field` + `field_en`
  And the dynamic sections (sessions / speakers / partners / news / archive) show the seeded rows, not the defaults
  And gallery images stream via GET /content/media/{id}/image
  And the console reports zero errors and there is no horizontal overflow

Scenario: With the feed offline the landing keeps its defaults and never blanks
  Given GET /content/site returns 503 (or the API is stopped)
  When the browser opens /
  Then loadSiteContentRemote() resolves to null
  And the page keeps SITE_DEFAULTS (the built-in content) with no error banner
  And no unhandled promise rejection appears in the console
```

### E2E-BF-12-003 — Visitor auth golden journey

```gherkin
Scenario: A 2FA visitor signs in with password + emailed OTP and lands on the profile
  Given an approved Visitor account visitor@example.com exists with TwoFactorEnabled = true
  And the browser is on /login (the "Sign in" card with "Sign in to your SIMF account.")
  When the visitor fills "Email address" = "visitor@example.com"
  And fills "Password" = the approved visitor's test password (a dev fixture — not stored here)
  And clicks "Sign in"
  Then POST http://localhost:5175/api/v1/app/auth/sign-in fires with body { email, password, audience: "Web" }
  And the API returns HTTP 200 with ApiResult.Success = true, Data.OtpToken set and Data.Tokens null
  And the browser navigates to /login/verify
  And an info SimfAlert reads that a code was sent to the masked email (e.g. "vi****@example.com")

  When the tester reads the 6-digit code at run time from SIMF_Identity.AccountCodes (Purpose = SignInOtp, latest unconsumed) as {otp}
  And fills the "Verification code" field with {otp}
  And clicks "Verify"
  Then POST /api/v1/app/auth/verify-otp fires with { otpToken, code: {otp} } and returns 200 with Data.Tokens (AccessToken, RefreshToken, User)
  And the browser force-loads /auth/complete?reference={ticket}
  And /auth/complete writes the auth cookie and 302-redirects to /account/profile
  And /account/profile renders for "visitor@example.com" with 0 console errors and no h-overflow
```

**Evidence captured:**
- Network: `/api/v1/app/auth/sign-in` → 200, `/api/v1/app/auth/verify-otp` → 200, `/auth/complete` → 302 → `/account/profile`
- Audit rows: `SignIn.SecondFactorIssued` (detail = `EmailOtp`), then `SignIn.Succeeded`, both with the visitor's user id
- The consumed `AccountCodes` row is now `ConsumedAt`-stamped (single-use)

### E2E-BF-12-004 — Audience gate (Administrator on the Website)

```gherkin
Scenario: An Administrator account is rejected on the visitor surface
  Given the Administrator account superadmin@zagali-ict.com exists (UserType = Admin)
  And the browser is on /login
  When the operator fills "Email address" = "superadmin@zagali-ict.com"
  And fills "Password" = the admin's test password (a dev fixture — not stored here)
  And clicks "Sign in"
  Then POST /api/v1/app/auth/sign-in fires with audience: "Web"
  And SignInService.EnforceAudienceAsync rejects it: HTTP 403 with ApiResult.Error.Code = "AUTH_WRONG_SURFACE_WEB"
  And a bilingual SimfAlert error appears at the top of the form whose English reads
      "Sign in to the Control Panel instead — this account is not allowed on the visitor surfaces." (with its Arabic counterpart)
  And the page stays on /login (no navigation to /login/verify, no emailed OTP)
  And a "SignIn.WrongSurface" audit row is written with detail = "Web"
```

### E2E-BF-12-005 — Account-state route: pending visitor

```gherkin
Scenario: A pending-approval visitor lands on /account/pending
  Given a Visitor account in AccountState = PendingApproval exists with TwoFactorEnabled = false
  And the browser is on /login
  When the visitor signs in with correct credentials
  Then POST /api/v1/app/auth/sign-in returns HTTP 200 with Data.AccountState.State = "PendingApproval"
  And /auth/complete copies account_state into the cookie and 302-redirects to /account/pending
  And the /account/pending page shows the bilingual "awaiting approval" state message and the account email
  And the only action is a "Sign out" button that posts to /auth/sign-out
  And the page renders with 0 console errors and no h-overflow
```

### E2E-BF-12-006 — Account-state route: rejected visitor

```gherkin
Scenario: A rejected visitor lands on /account/rejected with the verbatim reason
  Given a Visitor account in AccountState = Rejected exists with a stored bilingual rejection reason (10–500 chars per RejectRouteRequestValidator)
  And TwoFactorEnabled = false
  And the browser is on /login
  When the visitor signs in with correct credentials
  Then POST /api/v1/app/auth/sign-in returns HTTP 200 with Data.AccountState.State = "Rejected" and RejectionReason populated
  And /auth/complete copies account_state + rejection_reason(_ar) into the cookie and 302-redirects to /account/rejected
  And the /account/rejected page shows the admin-typed rejection reason verbatim in an error SimfAlert, in the active culture
  And Arabic culture surfaces rejection_reason_ar; English surfaces rejection_reason
  And the only action is "Sign out" (POST /auth/sign-out)
```

### E2E-BF-12-007 — Bad credentials

```gherkin
Scenario: A wrong password surfaces the generic invalid-credentials banner
  Given an approved Visitor account visitor@example.com exists
  And the browser is on /login
  When the visitor fills "Email address" = "visitor@example.com"
  And fills "Password" = a deliberately wrong value
  And clicks "Sign in"
  Then POST /api/v1/app/auth/sign-in returns HTTP 401 with Error.Code = "AUTH_INVALID_CREDENTIALS"
  And a bilingual SimfAlert error appears whose English reads "The email address or password is not correct." (with its Arabic counterpart)
  And the message is identical whether the email exists or not (no enumeration oracle)
  And the page stays on /login and no auth cookie is set
  And a "SignIn.BadCredentials" audit row is written
```

### E2E-BF-12-008 — Client validation: both fields empty

```gherkin
Scenario: Submitting an empty form shows inline errors and fires no request
  Given the browser is on /login with both fields blank
  When the visitor clicks "Sign in"
  Then the "Email address" field shows its bilingual inline "Enter your email address." error
  And the "Password" field shows its bilingual inline "Enter your password." error
  And NO POST /api/v1/app/auth/sign-in request fires (the client-side guard returns first)
  And the page stays on /login
```

### E2E-BF-12-009 — State-block errors (unverified / disabled)

```gherkin
Scenario: An unverified account cannot sign in
  Given a Visitor account in AccountState = Registered (email not verified) exists
  And the browser is on /login
  When the visitor signs in with correct credentials
  Then POST /api/v1/app/auth/sign-in returns HTTP 403 with Error.Code = "AUTH_EMAIL_NOT_VERIFIED"
  And a bilingual SimfAlert error appears whose English reads "Verify your email address before signing in."
  And the page stays on /login

Scenario: A disabled account is hard-blocked
  Given a Visitor account in AccountState = Disabled exists
  And the browser is on /login
  When the visitor signs in with correct credentials
  Then POST /api/v1/app/auth/sign-in returns HTTP 403 with Error.Code = "AUTH_ACCOUNT_DISABLED"
  And a bilingual SimfAlert error appears whose English reads "This account is not active."
  And the page stays on /login
```

### E2E-BF-12-010 — Forgot → reset round trip

```gherkin
Scenario: Forgot password → emailed code → reset → back to sign-in
  Given a visitor has forgotten their password
  When they open /forgot-password and submit their email "visitor@example.com"
  Then the page always shows the success message (anti-enumeration — identical whether the email exists or not)
  And (if the email exists) a 6-digit reset code is emailed with a 15-minute TTL

  When they read the reset code at run time from SIMF_Identity.AccountCodes (reset purpose, latest unconsumed) as {resetCode}
  And they open /reset-password and paste {resetCode}
  And type a new password that meets the complexity policy (not stored here)
  And click "Reset password"
  Then they see a success toast and land on /login
  And all prior sessions for the account are revoked

  When they sign in with the new password on /login
  Then the sign-in completes (the visitor OTP second factor is still required per D-033)
  And a "PasswordReset.Completed" audit row exists for the account
```

### E2E-BF-12-011 — Authenticated-area smoke + sign-out

```gherkin
Scenario: The signed-in account pages render and sign-out clears the session
  Given a visitor completed the E2E-BF-12-003 golden journey and holds a valid auth cookie
  When they navigate to /account/profile
  Then the profile page renders with 0 console errors, 0 broken assets and no h-overflow
  When they navigate to /account/notifications
  Then the notifications page renders with the same render invariants
  When they click "Sign out"
  Then a POST /auth/sign-out fires and the API SignOutAsync revokes the refresh token
  And the auth cookie is cleared
  And the browser lands on /login
  When they navigate directly back to /account
  Then the self-guard routes the now-anonymous session to /login again
```

### E2E-BF-12-012 — Public meeting-confirm link

```gherkin
Scenario: A speaker opens the emailed meeting link, previews safely, then confirms
  Given an admin accepted a speaker meeting request and bound it to a hall slot (request is AwaitingSpeaker)
  And the speaker received Approve and Reject links, each with a distinct single-use action-bound token
  And the browser is a fresh anonymous session (the token is the only credential)
  When the speaker opens /meeting/confirm?token={approveToken}
  Then GET /api/v1/app/meeting-actions/{approveToken} previews the pending decision (requester, topic, time, hall)
  And the token is NOT consumed (email-scanner prefetch is safe)
  When the speaker reopens the same link
  Then it still previews (a second GET does not burn the token)
  When the speaker clicks "Confirm"
  Then POST /api/v1/app/meeting-actions/{approveToken} applies the decision: request → Accepted, the requester is notified "confirmed", the token is marked used
  And the page renders with 0 console errors and no h-overflow

Scenario: A used token (or its sibling) shows the neutral not-valid card
  Given the Approve token from above has been consumed
  When the speaker opens /meeting/confirm?token={approveToken} (or its sibling {rejectToken})
  Then GET /api/v1/app/meeting-actions/{token} returns a neutral 404
  And the page shows the neutral "This link is no longer valid" card (never leaking which reason)
```

### E2E-BF-12-013 — OTP request throttle

```gherkin
Scenario: Too many sign-in codes within the hour are throttled
  Given an approved Visitor account with TwoFactorEnabled = true (email-OTP second factor)
  And 5 sign-in codes have already been issued for this account within the last hour
  And the browser is on /login
  When the visitor signs in correctly a 6th time
  Then POST /api/v1/app/auth/sign-in returns HTTP 429 with Error.Code = "RATE_LIMIT_EXCEEDED"
  And a bilingual SimfAlert error appears whose English reads "Too many sign-in codes have been requested. Try again later."
  And no second-factor ticket is issued (the throttle fires before the ticket)
  And the page stays on /login
```

### E2E-BF-12-014 — RTL / bilingual sweep

```gherkin
Scenario: The Arabic toggle mirrors the landing and the sign-in card without overflow
  Given the browser is on http://localhost:5115/ in English
  When the tester switches culture to Arabic
  Then the document becomes <html dir="rtl" lang="ar"> and the marketing sections mirror (fields read from `field` not `field_en`)
  And document.scrollWidth still equals document.clientWidth (no horizontal overflow in RTL)

  When the tester opens /login and switches to Arabic (via /culture?culture=ar&redirectUri=%2Flogin)
  Then the page reloads as <html dir="rtl" lang="ar"> on /login
  And the card title, field labels ("Email address" / "Password") and the "Sign in" button all render in Arabic
  And the brand panel and the form swap sides
  And the console reports zero errors and there is no horizontal overflow
```

### Notes

- **`/login` has no CP permission gate.** `SignInEndpoint` is `AllowAnonymous`, so the canonical CP "non-admin → `/not-permitted`" auth-gate scenario does not apply. The access-control coverage here is the **audience gate** (E2E-BF-12-004, `AUTH_WRONG_SURFACE_WEB` from `SignInService.EnforceAudienceAsync`) plus the **account-state routing** (005/006/009). Confirmed constants: `ErrorCodes.AuthWrongSurfaceWeb = "AUTH_WRONG_SURFACE_WEB"`, `AuthInvalidCredentials`, `AuthEmailNotVerified`, `AuthAccountDisabled`, `AuthAccountLocked`, `RateLimitExceeded`, and `AuthOtp{Invalid,Expired,TokenInvalid}` in `src/Shared/SIMF.Common/ErrorCodes.cs`.
- **`/account` is AllowAnonymous but self-guards.** `Home.razor` reads the in-circuit `SimfAuthSession` (not the cookie) and, when not signed in, `OnInitialized` routes to `/login`. So the "public smoke" expectation for `/account` is a redirect, not a rendered page — the genuinely anonymous-renderable public pages are `/`, `/programme` and `/visit`.
- **Visitor OTP is a run-time read, never a literal** (D-033): the 6-digit code is the latest unconsumed `SIMF_Identity.AccountCodes` row with `Purpose = SignInOtp` (plaintext in dev). Admin TOTP (for the audience-gate fixture) comes from the `Get-Totp` helper — no secret appears in this catalogue. Passwords above are dev fixtures referenced by description, not written out.
- **Bilingual toasts are asserted by structure** (a bilingual `SimfAlert` error) plus the well-known English string; the Arabic counterpart is asserted to be present but not transcribed here. Full per-page Arabic copy lives in the owning `web-*.md` files (`web-login`, `web-otp-verify`, `web-account-rejected`).
- **This flow is a cross-page smoke, not a restatement.** Deep per-page edges (OTP attempt-cap `AUTH_OTP_TOKEN_INVALID`, expired-code `AUTH_OTP_EXPIRED`, double-submit guard, theme persistence, 2FA-off direct-token path, transport-down banner) are owned by `web-login.md` (E2E-WLG-*) and `web-otp-verify.md` (E2E-WOT-*) and are not duplicated here.
- **`web/programme.md` reference doc is not yet authored** — the `web-programme.md` catalogue notes it is grounded directly in `Programme.razor`. ⚠ unverified: the human-readable per-page reference doc for `/programme` (the catalogue and route are confirmed; only the companion reference doc is outstanding).
- Many assertions above are also covered at the API layer (no browser) by `tests/SIMF.Api.Tests/SignInTests.cs` (bad-credentials, unverified, disabled, lockout, the visitor emailed-code round-trip, the Web audience gate, pending/rejected `AccountStateInfo`) and `tests/SIMF.Api.Tests/MeetingActionTokenTests.cs` (meeting-confirm token safety). The browser scenarios add the UI-layer assertions (navigation, cookie hand-off, render invariants, RTL) those cannot reach.

---

## BF-13 — Permission / security matrix

> **Automated coverage as of 2026-07-29 (WS3).** Eleven of the twelve scenarios
> now execute in CI, across three files:
>
> | Scenario | Where | State |
> |---|---|---|
> | -001 Administrator wildcard | `PermissionEnforcementTests` | executed |
> | -002 custom role, API half | `PermissionEnforcementTests` | executed |
> | -003 role-less admin forbidden | `PermissionEnforcementTests` | executed |
> | -004 app/visitor token on admin endpoint → 403 | `BusinessFlow13PermissionMatrixTests` | executed |
> | -005 anonymous → 401, **and the anonymous auth set is pinned** | `BusinessFlow13PermissionMatrixTests` | executed |
> | -006 over-posting an unmapped field is ignored | `BusinessFlow13PermissionMatrixTests` | executed |
> | -007 baseline grant reaches News, not Sessions | `BusinessFlow13PermissionMatrixTests` | executed |
> | -009 a grant needs a fresh token mint | `BusinessFlow13PermissionMatrixTests` | executed |
> | -010 a token this API did not mint → 401 | `BusinessFlow13PermissionMatrixTests` | executed |
> | -011 build guards fail CI on an ungated surface | `PermissionEnforcementTests` + `CpNavigationPermissionTests` | executed |
> | -012 `/not-permitted` in Arabic | `CpElementSweepTests` | executed |
> | -008 deep-link bypass → `/not-permitted` | browser | **not executed — see below** |
>
> **-008 cannot be automated from the browser suite, and the reason is concrete.**
> It needs a signed-in admin who LACKS a permission. The sweep account is the
> super-admin, whose wildcard `"*"` satisfies every gate, so it can never be
> redirected and can never exercise the deny path. Creating a restricted admin does
> not help: `SignInService` forces TOTP for any user holding a role
> (`roles.Count > 0`), and a freshly created admin has no enrolled authenticator
> key, so a black-box suite cannot complete its sign-in. It needs a fixture that
> seeds a restricted admin **with a known TOTP secret** — database access this
> suite deliberately does not have. Note what is and is not missing: the API half
> of the same rule IS proven (`PermissionEnforcementTests` returns 403 for an
> ungranted role), so the unproven part is specifically the Control Panel's
> redirect, not the gate behind it.
>
> **Two corrections to this flow's text came out of executing it.**
>
> 1. **The anonymous auth surface is 17 endpoints, not 3.** Scenario -005 is
>    written as "the anonymous auth set is exactly {sign-in, sign-up,
>    forgot-password}", restating CLAUDE.md §4. The real set also contains
>    `verify-email`, `resend-code`, `verify-otp`, `verify-totp`, `resend-otp`,
>    `verify-recovery-code`, `reset-password`, `complete-password-change`,
>    `refresh`, `resolve-badge`, `badge-sign-in`, `badge-activation/start`,
>    `badge-activation/complete`, `device-keys/{id}/challenge` and
>    `sign-in-with-device-key`. **None is a defect** — each carries its own
>    credential (an emailed code, a reset token, a refresh token, a badge code, a
>    device-key signature) rather than a bearer token, and gating them on a bearer
>    would break sign-up, 2FA and password reset outright. The test pins the full
>    reviewed list, so an 18th still breaks the build. **The §4 wording is stale
>    and should be corrected by the owner.**
>
> 2. **`Code`, `IsActive` and `UserProfileId` are legitimate speaker-update
>    fields**, not over-posting targets — the over-post surface for -006 is only
>    what the update DTO does not expose (`id`, `createdAt`).
>
> **A real defect fell out of -006:** a speaker create/update carrying an unknown
> `userProfileId` returned **500**. `Speaker.UserProfileId` is a real same-database
> FK (`OnDelete.Restrict`), so the unknown id threw at SaveChanges, while the
> service's own summary claimed the link was cross-context and that a stale id
> "degrades gracefully" — untrue since `UserProfile` moved onto the App context.
> Now validated up front and returns 400. Regression tests in the same file.


This cross-page flow proves the SIMF **per-page / per-action permission system** (D-207 / D-208) cannot be bypassed on either surface. Assignment is **roles-only**, permission codes are baked into the JWT `perm` claim, and `Administrator` resolves to the wildcard `"*"` at token-mint time (`PermissionCatalog.Wildcard`) so it holds every code implicitly. The single source of truth is `src/Shared/SIMF.Common/PermissionCatalog.cs`. Every CP page carries `@attribute [RequirePermission(PermissionCatalog.X.Y)]` (e.g. `/admin/sessions` → `Sessions.View`, `/admin/themes` → `Themes.View`); a signed-in user who lacks the code is bounced to `/not-permitted`. Every admin endpoint pairs the permission policy with the approval policy — `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.X.Y), nameof(AuthorizationPolicies.RequireApprovedAccount))` — so `perm:Sessions.View` gates `POST /api/v1/admin/sessions/list`, `perm:Themes.View` gates `POST /api/v1/admin/themes/list`, `perm:Speakers.Edit` gates `PUT /api/v1/admin/speakers/{id}`. The flow also exercises the over-posting guard (admin UPDATE endpoints bind their **own** DTO and map explicitly — `UpdateSpeakerRequest` → `AdminUpdateSpeakerRequest`, the D-544 dual-DTO gotcha), the anonymous auth surface (`sign-in` / `sign-up` / `forgot-password`), and the two build-breaking xUnit guards — `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs` and `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` — which fail CI if any admin page/endpoint ships ungated (a security defect: an ungated surface is reachable by **any** signed-in admin regardless of role).

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-BF-13-001 | Administrator wildcard reaches every gated page + endpoint | happy | P0 |
| E2E-BF-13-002 | Custom role granted only `Sessions.View` — CP `/admin/themes` → `/not-permitted`, `POST /admin/themes/list` → 403 | auth | P0 |
| E2E-BF-13-003 | Role-less admin (zero grants) is forbidden everywhere | auth | P0 |
| E2E-BF-13-004 | App-surface (visitor) token is rejected on an admin endpoint → 403 | auth | P0 |
| E2E-BF-13-005 | Anonymous request to any `/api/v1/admin/**` endpoint → 401; anonymous auth set is exactly {sign-in, sign-up, forgot-password} | auth | P0 |
| E2E-BF-13-006 | Over-posting a protected field on an UPDATE is ignored (speaker dual-DTO) | error | P1 |
| E2E-BF-13-007 | Baseline role grant — Public Relations reaches News/VIP/Invitations but is 403 on Sessions | auth | P1 |
| E2E-BF-13-008 | Deep-link / address-bar bypass attempt is bounced to `/not-permitted` | auth | P1 |
| E2E-BF-13-009 | Roles-only + JWT-baked — a new grant takes effect only after a fresh token mint | auth | P1 |
| E2E-BF-13-010 | A live-stream (StreamAudience) token is never accepted as a user token → 401 | resilience | P2 |
| E2E-BF-13-011 | Build guards fail CI when an admin page/endpoint ships ungated | resilience | P1 |
| E2E-BF-13-012 | `/not-permitted` renders correctly in Arabic (RTL) | i18n | P1 |

### Scenarios

### E2E-BF-13-001 — Administrator wildcard reaches everything

```gherkin
Feature: The Administrator wildcard holds every permission
  Background:
    Given the API is reachable and the Control Panel is reachable
    And an account in the built-in "Administrator" role is Approved
    And it signs in on the CP audience (password + TOTP via Get-Totp)

  Scenario: Wildcard "*" satisfies every page + endpoint gate
    Given the signed-in principal carries perm="*" (PermissionCatalog.Wildcard)
    When they open /admin/sessions
    Then the RequirePermission(Sessions.View) gate passes and the grid renders
    When they open /admin/themes
    Then the RequirePermission(Themes.View) gate passes and the grid renders
    When they POST /api/v1/admin/sessions/list with a GridQuery body
    Then the response is HTTP 200 with an ApiResult<GridPage<...>>-shaped envelope
    When they POST /api/v1/admin/themes/list with a GridQuery body
    Then the response is HTTP 200
    And no admin nav item is hidden by the shell filter
```

### E2E-BF-13-002 — Custom role sees only what it was granted

```gherkin
Scenario: A role granted exactly Sessions.View is denied Themes on both surfaces
  Given a custom (non-baseline) role "Programme Viewer" exists
  And it is granted exactly one permission code: Sessions.View
  And an Approved UserType.Admin account holds that role and is signed in on the CP
  When they open /admin/sessions
  Then the RequirePermission(Sessions.View) gate passes and the sessions grid renders
  When they POST /api/v1/admin/sessions/list
  Then the response is HTTP 200
  When they navigate to /admin/themes
  Then the CP redirects them to /not-permitted (they never see the themes grid)
  And the /admin/themes nav item is not rendered in the side menu
  When they POST /api/v1/admin/themes/list with the same bearer token
  Then the response is HTTP 403 Forbidden
  # Mirrors PermissionEnforcementTests.Custom_role_reaches_only_its_granted_endpoint
```

### E2E-BF-13-003 — Role-less admin is forbidden everywhere

```gherkin
Scenario: An admin whose role grants nothing reaches no gated surface
  Given a custom role "Empty Role" exists with zero permission grants
  And an Approved UserType.Admin account holds that role (empty perm claim set)
  And it is signed in on the CP
  When they POST /api/v1/admin/sessions/list
  Then the response is HTTP 403 Forbidden
  When they open /admin/sessions in the browser
  Then the CP redirects them to /not-permitted
  And every permission-gated item is filtered out of the side menu
  And only the dashboard "/" (the ungated exception) remains reachable
  # Mirrors PermissionEnforcementTests.Admin_user_with_no_permissions_is_forbidden
```

### E2E-BF-13-004 — App-surface token rejected on an admin endpoint

```gherkin
Scenario: A visitor's app access token cannot reach the admin API
  Given a Visitor account is Approved
  And it signs in on the App audience (SignInAudience.App) — OTP read from SIMF_Identity.AccountCodes at run time
  And the minted access token carries no "perm" claim (visitors hold no admin permission)
  When that app token is sent as Bearer to POST /api/v1/admin/sessions/list
  Then the permission policy perm:Sessions.View is not satisfied
  And the response is HTTP 403 Forbidden
  And no session data is returned
```

### E2E-BF-13-005 — Anonymous surface is exactly the auth entry points

```gherkin
Scenario: Admin endpoints reject anonymous callers
  Given no Authorization header is present
  When an anonymous client POSTs /api/v1/admin/sessions/list
  Then the response is HTTP 401 Unauthorized
  When an anonymous client PUTs /api/v1/admin/speakers/{id}
  Then the response is HTTP 401 Unauthorized

Scenario: The anonymous authentication set is exactly {sign-in, sign-up, forgot-password}
  Given the audit of the auth endpoint family
  Then POST /api/v1/app/auth/sign-in is AllowAnonymous
  And POST /api/v1/app/auth/sign-up is AllowAnonymous
  And POST /api/v1/app/auth/forgot-password is AllowAnonymous
  And no /api/v1/admin/** endpoint is AllowAnonymous
  # Global rule §4: AllowAnonymous only for SignIn / SignUp / ForgotPassword.
  # See Notes for the deliberately-anonymous public content-read surface
  # and the two flow-completion companions (verify-email, reset-password).
```

### E2E-BF-13-006 — Over-posting a protected field is ignored on UPDATE

```gherkin
Scenario: An UPDATE binds its own DTO, so a field it does not map cannot be set
  Given an Administrator is signed in with Speakers.Edit
  And speaker "SPK-014" exists
  When they PUT /api/v1/admin/speakers/{id} with a JSON body that ALSO carries
       unmapped/hostile fields (e.g. "Id":"<another-guid>", "CreatedBy":"<someone>",
       "IsDeleted":true) alongside the legitimate Name / WebsiteUrl fields
  Then the endpoint binds UpdateSpeakerRequest and maps only its known properties
       into AdminUpdateSpeakerRequest (Code, Name, NameArabic, Rank, ..., WebsiteUrl,
       ContactId, IsActive)
  And the injected/unmapped fields have no effect (the route {id:guid} — not the body — is authoritative)
  And the response is HTTP 200 with the persisted, mapped values only
  # D-544 dual-DTO gotcha: CREATE binds the contract; UPDATE has its own DTO + explicit map.
```

### E2E-BF-13-007 — Baseline role grant is scoped

```gherkin
Scenario: The Public Relations baseline role reaches its News/VIP surface but not Sessions
  Given an Approved admin holds the built-in "Public Relations" role
  And that role's seeded baseline grants include News.*, Vips.* and Invitations.*
  And it is signed in on the CP
  When they POST /api/v1/admin/news/list
  Then the response is HTTP 200 (News.View is a PublicRelations baseline grant)
  When they open /admin/sessions
  Then the CP redirects them to /not-permitted (Sessions.View is AdminOnly — not granted)
  When they POST /api/v1/admin/sessions/list
  Then the response is HTTP 403 Forbidden
```

### E2E-BF-13-008 — Deep-link bypass is bounced

```gherkin
Scenario: Typing a privileged route into the address bar does not bypass the gate
  Given a signed-in admin whose role lacks Roles.View
  When they paste "/admin/roles" directly into the browser address bar
  Then the RequirePermission(Roles.View) gate denies them
  And they land on /not-permitted (never a partial render of the roles page)
  And the matching POST /api/v1/admin/roles/list independently returns HTTP 403
```

### E2E-BF-13-009 — Roles-only, baked into the JWT

```gherkin
Scenario: A newly granted permission takes effect only after a fresh token mint
  Given an admin holds a custom role that currently lacks Themes.View
  And they POST /api/v1/admin/themes/list → HTTP 403 with their current token
  When an Administrator grants Themes.View to that role (roles-only assignment — no per-user grant)
  Then the change is written to RolePermission, not to any user record
  And the already-issued token still carries the old perm claim set (403 persists on it)
  When the admin re-signs-in (or the token is refreshed) and a new token is minted
  Then the fresh "perm" claim set now includes Themes.View
  And POST /api/v1/admin/themes/list returns HTTP 200
```

### E2E-BF-13-010 — Stream token is not a user token

```gherkin
Scenario: A live-stream audience token is rejected on user/admin endpoints
  Given a token minted for the live-stream audience (Jwt:StreamAudience = "simf-stream")
  When it is presented as Bearer to POST /api/v1/admin/sessions/list
  Then JWT audience validation fails (ValidAudience is the user audience "SIMF")
  And the response is HTTP 401 Unauthorized
  # JwtBearerSetup keeps the stream audience separate so a stream token is never accepted as a user token.
```

### E2E-BF-13-011 — Build guards enforce the matrix

```gherkin
Scenario: CI fails if any admin page or endpoint ships ungated
  Given the two guard suites are part of the build
  When a developer adds an /admin route nav item with RequiredPermission = null
  Then CpNavigationPermissionTests.Every_real_admin_nav_item_is_permission_gated fails the build
  When a developer references a nav permission that is not in PermissionCatalog.All
  Then CpNavigationPermissionTests.Every_nav_required_permission_is_a_real_catalogue_code fails the build
  And PermissionEnforcementTests proves at the API that a custom role reaches only its
      granted endpoint, the Administrator wildcard reaches everything, and a role-less
      admin reaches nothing
  # These guards make "an ungated admin page/endpoint" a build failure, not a runtime surprise.
```

### E2E-BF-13-012 — /not-permitted renders in Arabic (RTL)

```gherkin
Scenario: The denial page mirrors correctly in Arabic
  Given a signed-in admin who lacks the required permission is redirected to /not-permitted
  When the CP culture is Arabic (dir="rtl", lang="ar")
  Then the "access denied" heading + body render from the IStringLocalizer<Strings> resources (bilingual)
  And the layout mirrors: content aligns start=right, the shell chrome swaps sides
  And scrollWidth == clientWidth (no horizontal overflow) and no console errors
```

### Notes

- **Anonymous surface caveat (E2E-BF-13-005).** Global rule §4 restricts `AllowAnonymous` to sign-in / sign-up / forgot-password for the **credential/account** surface, and the scenario asserts exactly that plus "no `/api/v1/admin/**` endpoint is anonymous". Two grounded nuances the tester must not flag as defects: (1) the sign-up and forgot flows are completed by `verify-email` and `reset-password`, which belong to those same two anonymous flows; (2) SIMF also exposes a **deliberately public content-read surface** (e.g. `GET /app/archive`, other app read endpoints) that is `AllowAnonymous` by design so Guests can browse before signing in — that is a separate, intentional read surface, not a violation of the auth-surface rule. ⚠ unverified against the strict three-item rule: a fourth anonymous credential endpoint, **badge sign-in** (`/app/auth/badge-sign-in`, D-738), exists and mirrors the forgot-password rate-limit — confirm it is expected before treating it as an exception.
- **App-surface rejection mechanism (E2E-BF-13-004).** A visitor/app token is refused on admin endpoints because it carries **no `perm` claim**, so every `perm:*` policy denies (403). This is the same mechanism the xUnit guard `Admin_user_with_no_permissions_is_forbidden` proves structurally.
- **Do not duplicate the guards.** E2E-BF-13-011 references `CpNavigationPermissionTests` and `PermissionEnforcementTests` as the executable proof; this flow drives the live surfaces, it does not re-implement those unit assertions.
- **No secrets.** Admin TOTP is generated at run time via the `Get-Totp` helper; visitor OTP is read from `SIMF_Identity.AccountCodes` at run time. No literal secret, token, key or connection string appears in any scenario.
- **403 vs 401.** An authenticated-but-unauthorized caller gets **403 Forbidden** (permission missing); an unauthenticated caller or a wrong-audience token gets **401 Unauthorized** (identity/audience missing). The scenarios use these two codes deliberately.

---

## BF-14 — Bilingual / RTL sweep

> **Automated coverage as of 2026-07-29 (WS2/WS3).** Both surfaces now run the full
> element contract a second time under `?culture=ar`:
> **Control Panel — 94 of 94 routes passed**, each asserting `document.dir == "rtl"`,
> zero console errors and `scrollWidth == clientWidth` (no horizontal overflow, which
> is where RTL layout usually breaks: pinned grid action columns, toolbars, pagers).
> **Website — 17 of 17 routes passed** under the same contract.
> This is the structural half of the flow. It proves the pages do not break in RTL;
> it does **not** check that the Arabic *copy* is right, that glyph-level mirroring is
> correct, or that no string is left untranslated — those still need a reader, and the
> ~60 Arabic strings awaiting native review are unaffected by this run.

SIMF is **Arabic-first**, so the RTL render is a **P1 acceptance surface on every screen — not an afterthought**. This flow drives one Arabic toggle across all three surfaces and asserts the direction contract end-to-end. On the **Control Panel** it exercises the `SimfDataGrid` list at `/admin/speakers` plus its `SpeakersAddEdit` modal; on the **Website** the public landing `/` (static `wwwroot/index.html` fed by `GET /content/site`) and the visitor sign-in `/login` (`SignIn.razor`, anonymous); on the **Mobile app** the home screen #13 `/` (`GET /api/v1/app/bootstrap`), the delegations list `/delegations` (`GET /app/delegations`, anonymous — the 12 invited countries seeded in D-687/D-691), and the credentials form screen #5 `/sign-up` (`POST /api/v1/app/auth/sign-up`). The language switch itself is a full navigation to the host `GET /culture?culture=ar&redirectUri=<relative>` endpoint (present on both CP and Website — `CultureEndpoint`), driven from `SimfLanguageSwitch` (globe icon + `العربية` / `English`), which writes the `CookieRequestCultureProvider` culture cookie and `LocalRedirect`s back so the whole document re-renders. Both web hosts set `<html lang="@CurrentUICulture.TwoLetterISOLanguageName" dir="@(IsRightToLeft ? "rtl" : "ltr")">` in `App.razor`. Rules under test: no user-facing string is hardcoded (every label via **resx / l10n**); layout must **mirror** and form actions must **reverse**; the LTR-only islands (**email, phone, national id, plate, URLs**) stay LTR *inside* the RTL page; colour is never the only signal of a state; Arabic must not clip inside fixed-width chips; and no page scrolls horizontally (`scrollWidth == clientWidth`). Two known RTL fixes are regression-checked: **D-686** — the session-summary card used physical `.end` where it must use logical `.start` (Figma `1072:13518`), so Arabic now right-aligns; and Arabic copy must not truncate in fixed-width status/tier chips.

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-BF-14-001 | Golden: toggle Arabic on `/admin/speakers` → whole document flips to `dir="rtl" lang="ar"`, Arabic labels, mirrored nav | happy | P0 |
| E2E-BF-14-002 | `GET /culture` sets the culture cookie + `LocalRedirect`s to the same page; bad `culture` → `en`, non-relative `redirectUri` → `/` | resilience | P1 |
| E2E-BF-14-003 | CP `SpeakersAddEdit` modal in Arabic: mirrored form, **reversed** Save/Cancel order, bilingual Name/NameArabic, `WebsiteUrl` stays LTR | i18n | P0 |
| E2E-BF-14-004 | CP speaker validation error renders as a bilingual `SimfAlert` inside the RTL modal without breaking layout | error | P1 |
| E2E-BF-14-005 | Website landing `/` in Arabic: `dir="rtl"`, Arabic hero copy from `GET /content/site`, mirrored layout, no horizontal overflow | i18n | P1 |
| E2E-BF-14-006 | Website `/login` in Arabic: panels swap sides, Arabic labels, **email** field is an LTR island, bilingual sign-in error still bilingual | i18n | P1 |
| E2E-BF-14-007 | App home #13 in Arabic: RTL scaffold, mirrored chrome, Arabic copy, all strings via l10n | i18n | P1 |
| E2E-BF-14-008 | App delegations `/delegations` in Arabic: 12 invited-country cards, head + member counts, RTL order, Arabic country name not clipped | i18n | P1 |
| E2E-BF-14-009 | App sign-up form #5 in Arabic: RTL form, reversed actions, **email / national id / plate / phone** stay LTR islands | i18n | P0 |
| E2E-BF-14-010 | D-686 regression: session-summary right-aligns in Arabic (logical `.start`, not physical `.end`) — Figma `1072:13518` | i18n | P1 |
| E2E-BF-14-011 | Auth/permission gate is culture-independent: non-admin → `/not-permitted` even with the Arabic cookie set | auth | P0 |
| E2E-BF-14-012 | Colour-not-only-signal + no hardcoded string: every state carries text/icon, no raw resx key leaks in either language | i18n | P1 |
| E2E-BF-14-013 | Horizontal-overflow sweep: fixed-width tier/status chips fit Arabic; `scrollWidth == clientWidth` on CP + Website in `rtl` | resilience | P1 |

### Scenarios

### E2E-BF-14-001 — Golden: Arabic toggle flips the CP list document

```gherkin
Feature: BF-14 Arabic toggle on a representative Control Panel list
  Background:
    Given the API is reachable on http://localhost:5175
    And the Control Panel is reachable on http://localhost:5158
    And an Administrator is signed in (superadmin@zagali-ict.com + TOTP via Get-Totp)

  Scenario: Switching to Arabic re-renders the whole speakers page RTL
    Given the administrator is on /admin/speakers in English
    And <html lang="en" dir="ltr">
    When they click the SimfLanguageSwitch labelled "العربية"
    Then the browser does a full navigation to /culture?culture=ar&redirectUri=%2Fadmin%2Fspeakers
    And it LocalRedirects back to /admin/speakers
    And the document now renders <html lang="ar" dir="rtl">
    And the SimfDataGrid column headers, the page title and the nav labels are all Arabic
    And the side navigation is mirrored to the right edge
    And the row-action icons (Sessions deep-link, Edit, Details, Deactivate) reverse to the leading edge
    And the culture cookie (CookieRequestCultureProvider default name) is set for ~1 year
    And document.scrollingElement.scrollWidth == clientWidth (no horizontal overflow)
    And 0 console errors are logged
```

### E2E-BF-14-002 — `/culture` endpoint contract

```gherkin
Scenario: The culture endpoint sets the cookie and returns to the same page
  Given the administrator is on /admin/speakers
  When a GET /culture?culture=ar&redirectUri=%2Fadmin%2Fspeakers is issued
  Then the response sets the CookieRequestCultureProvider culture cookie to "ar"
  And responds with a LocalRedirect to /admin/speakers

Scenario: An unsupported culture value falls back to English
  When a GET /culture?culture=fr&redirectUri=%2Fadmin%2Fspeakers is issued
  Then the cookie culture is coerced to "en" (only "en"/"ar" are Supported)

Scenario: A non-relative or off-site redirectUri is refused
  When a GET /culture?culture=ar&redirectUri=https%3A%2F%2Fevil.example%2Fx is issued
  Then the redirect target is coerced to "/" (only a well-formed relative path starting with "/" is honoured)
  And no open-redirect off the CP origin is possible
```

### E2E-BF-14-003 — CP add/edit modal mirrors and reverses actions

```gherkin
Scenario: The speaker create modal is fully RTL with reversed actions
  Given the administrator is on /admin/speakers with <html dir="rtl" lang="ar">
  When they click the "add speaker" action and the SpeakersAddEdit modal opens
  Then the modal renders inside the dir="rtl" document
  And the field labels (Name, NameArabic, Title, Organisation, WebsiteUrl) are Arabic
  And the bilingual pair shows both the English "Name" and Arabic "NameArabic" inputs
  And the primary "Save" button sits on the leading (right) edge and "Cancel" trails it — reversed vs LTR
  When they type NameArabic="سعادة الفريق البحري" and WebsiteUrl="https://speaker.example.com"
  Then the NameArabic input accepts and right-aligns the Arabic text
  And the WebsiteUrl input stays an LTR island (the URL reads left-to-right, unmirrored) even inside the RTL page
  And the label/helper text around it is still Arabic
```

### E2E-BF-14-004 — Bilingual validation error inside the RTL modal

```gherkin
Scenario: A validation error surfaces as a bilingual SimfAlert without breaking RTL layout
  Given the SpeakersAddEdit modal is open in Arabic (dir="rtl")
  When the administrator submits with a required field empty (or NameArabic over its max-length triple-lock)
  Then a bilingual SimfAlert error is shown (English + Arabic copy, structural — not asserted verbatim)
  And the alert text is right-aligned and does not overflow the modal
  And the error state is conveyed by an icon + text, not by colour alone
  And the modal stays open with the entered values preserved
  And no partial/duplicate speaker row is created
```

### E2E-BF-14-005 — Website landing in Arabic

```gherkin
Feature: BF-14 Arabic render of the public Website
  Background:
    Given the Website is reachable and the anonymous landing page / is loaded

  Scenario: The landing page mirrors and shows Arabic content
    Given a visitor is on / in English (<html lang="en" dir="ltr">)
    When they use the language switch to العربية (GET /culture?culture=ar&redirectUri=%2F)
    Then the page reloads with <html lang="ar" dir="rtl">
    And the hero + section copy render in Arabic from the GET /content/site feed
    And the header nav and hero call-to-action mirror to the right
    And every visible string resolves via resx/l10n (no raw key such as "Home.Hero.Title" leaks)
    And document.scrollingElement.scrollWidth == clientWidth (no horizontal overflow)
    And no broken images and 0 console errors
```

### E2E-BF-14-006 — Website login in Arabic with an LTR email island

```gherkin
Scenario: The visitor sign-in page is RTL but the email field stays LTR
  Given a visitor opens /login (SignIn.razor, anonymous) in Arabic
  Then <html dir="rtl" lang="ar"> and the brand panel + form swap sides
  And the field labels and the "Sign in" button caption are Arabic
  When they focus the Email field and type "visitor@example.com"
  Then the email renders left-to-right (LTR island) inside the RTL page, caret on the left
  When they submit the right email with a wrong password
  Then a bilingual SimfAlert error appears (English + Arabic, structural)
  And it is right-aligned, un-clipped, and no auth cookie is issued
  And the URL is still /login
```

### E2E-BF-14-007 — App home screen RTL

```gherkin
Feature: BF-14 Arabic render of the mobile app
  Background:
    Given the Flutter app is signed in as an approved visitor
    And the app language is switched to العربية

  Scenario: The home screen (#13) renders right-to-left
    Given the home bundle GET /api/v1/app/bootstrap has returned
    When the home screen renders in Arabic
    Then Directionality is TextDirection.rtl for the screen
    And the top bar, greeting and the tile grid mirror to the right
    And every label comes from the app l10n (no hardcoded English literal, no raw key)
    And the unread notification badge count is legible (GET /app/account/notifications/unread-count)
    And no tile text is clipped and the body has no horizontal scroll
```

### E2E-BF-14-008 — App delegations list RTL with real seeded data

```gherkin
Scenario: The delegations list shows the 12 invited countries mirrored, Arabic un-clipped
  Given GET /app/delegations (anonymous) returns the seeded invited countries (D-687/D-691)
  When the /delegations screen renders in Arabic (TextDirection.rtl)
  Then the stats strip shows the participating-country count and total participants
  And there is one card per invited country (12 countries: e.g. France, India, United Kingdom,
      United Arab Emirates — NOT Saudi Arabia: the host is never marked Country.IsInvited, D-768,
      and the seeded set is AE, BH, KW, OM, QA, EG, GB, US, FR, PK, IN, TR)
  And each card shows the flag, the bilingual country name (Arabic shown, un-clipped in its cell),
      the head of delegation with an initial avatar, an arrival→departure date range, and a member count
  And the card content order is mirrored: flag/name lead the right edge, the count trails the left
  And a country whose delegate roster is empty still renders (head + zero-member count), never a blank card
  And the list is pull-to-refresh and does not overflow horizontally
```

### E2E-BF-14-009 — App sign-up form RTL with LTR islands preserved

```gherkin
Scenario: The credentials form (#5) is RTL but keeps its LTR-only fields LTR
  Given a guest opens /sign-up (screen #5) in Arabic
  Then the form renders TextDirection.rtl with Arabic field labels and helper text
  And the primary "Next"/"Create account" button leads the right edge, secondary action trails (reversed)
  When they type Email="new.visitor@example.com"
  Then the email renders left-to-right (LTR island) inside the RTL form
  And on the downstream visitor profile step the National ID, mobile phone and vehicle plate fields
      are likewise LTR islands (digits/plate read left-to-right) while their labels stay Arabic
  When they submit an already-registered email
  Then a generic 201 SignUpResponse is still returned (enumeration-resistant, D-198/D-270) — no bilingual copy leaks which email exists
```

### E2E-BF-14-010 — D-686 regression: session-summary right-aligns in Arabic

```gherkin
Scenario: The session-summary card uses logical .start so Arabic is right-aligned
  Given the session-summary screen (Figma 1072:13518) is opened in Arabic
  When summary_session_card.dart and summary_content_card.dart render (TextDirection.rtl)
  Then the "الجلسة" label, the gold session title, the sub-line, the section heading and the bullets
      are RIGHT-aligned (logical .start = leading edge = right in RTL) — the D-686 fix
  And they are NOT left-aligned (the pre-fix bug used physical .end which resolves to LEFT in RTL)
  When the same screen is viewed in English
  Then the identical widgets left-align (.start = leading edge = left in LTR) — one code path serves both
  And the agenda-row time stays pinned LTR (a deliberate LTR island, correct in both directions)
```

### E2E-BF-14-011 — Permission gate is culture-independent

```gherkin
Scenario: RTL does not weaken authorization
  Given a signed-in Control Panel user WITHOUT the Speakers.View permission
  And their culture cookie is set to "ar" (dir="rtl")
  When they navigate to /admin/speakers
  Then they are redirected to /not-permitted (HTTP 200) exactly as in English
  And the /not-permitted page itself renders in Arabic (dir="rtl", Arabic NotPermitted.Title/Text)
  And no speaker data is returned to them regardless of language
  And the matching admin API call is refused by policy, not by UI culture
```

### E2E-BF-14-012 — Colour is never the only signal; no hardcoded strings

```gherkin
Scenario: State is conveyed by text/icon (not colour alone) and nothing is hardcoded
  Given any list with status chips (e.g. speaker Active/Inactive, booking status) is open in Arabic
  Then each status chip carries an Arabic text label and/or icon — colour alone never encodes the state
  And a colour-blind reviewer can distinguish every state from its label/icon
  And no visible string is a raw resx/l10n key (e.g. "Admin.Speakers.Field.NameArabic" never leaks as literal text)
  And switching en↔ar changes every user-facing string (proving each is bound to a resource, not a constant)
```

### E2E-BF-14-013 — Horizontal-overflow sweep in RTL

```gherkin
Scenario: Arabic does not clip in fixed-width chips and no page scrolls sideways
  Given the CP /admin/speakers list and the Website landing / are each loaded in Arabic (dir="rtl")
  Then for every fixed-width chip (tier, status, count badge) the Arabic label fits — no ellipsis/overflow clip
  And document.scrollingElement.scrollWidth == document.scrollingElement.clientWidth on both pages
  And the SimfDataGrid header row and the landing hero align to the right edge with no gutter gap
  And resizing to a narrow (mobile) width keeps the body free of horizontal scroll in RTL
```

### Notes

- **Culture switch is a full navigation, not SPA state.** `GET /culture` (CP: `SIMF.ControlPanel.Endpoints.CultureEndpoint`; Website: `SIMF.Web.Endpoints.CultureEndpoint`) writes the `CookieRequestCultureProvider` cookie and `LocalRedirect`s, so the whole document — including `<html lang/dir>` — re-renders. Only `en` and `ar` are `Supported`; anything else coerces to `en`, and a non-relative/off-origin `redirectUri` coerces to `/` (open-redirect guard) — asserted in E2E-BF-14-002.
- **D-686 is an App-side (Flutter) fix**, not a web-CSS fix: the correction was `TextAlign.end`/`CrossAxisAlignment.end` → `.start` in `summary_session_card.dart` + `summary_content_card.dart`. The web/CP equivalent of the same rule is "use logical margin/padding (`margin-inline-start`/`.start`), never physical `.left`/`.right`" — reviewers should spot-check CP/Website CSS for physical-side properties that would break in `rtl`.
- **LTR islands are intentional**, not bugs: email, phone, national id, vehicle plate, and URLs (e.g. the speaker `WebsiteUrl`, the agenda-row time on the summary card) must read left-to-right even inside an RTL page. A test that "fixes" them to RTL is wrong.
- **Do not assert invented Arabic copy.** Bilingual toasts/errors are asserted by **structure** ("a bilingual `SimfAlert` error", English + Arabic). The only Arabic literals used here are ones verified in the codebase/seed: the `العربية` switch label and the `الجلسة الافتتاحية` seeded opening session (D-681); realistic Arabic like `سعادة الفريق البحري` is illustrative test input, not an asserted UI string.
- **Delegations counts are demo seed data** (D-687/D-691: 12 invited countries, fake head + member `UserProfile` rows) intended to be replaced/removed before go-live — assert the *shape* (12 cards, head + member count, un-clipped Arabic name), not exact people. Member counts grow as real delegates register.
- **Auth is checked independently of culture** (E2E-BF-14-011): the CP permission catalogue gates both the page (`@attribute [RequirePermission(...)]`) and the API `Policies(...)`; the RTL cookie must not change that outcome. `/not-permitted` is a real CP route.
- **The Website `/login` page** is the Blazor SSR component `SignIn.razor` (`@page "/login"`, anonymously reachable); it drives the backend sign-in service. The mobile-app API endpoint `SignInEndpoint` (`POST /api/v1/app/auth/sign-in`, `AllowAnonymous`) is a separate surface — do not conflate the two.
- Live-verify each surface per the delivery gate: capture the `rtl` screenshot, the console (0 errors), the network list (0 broken assets), and a DOM check (`scrollWidth == clientWidth`) for CP + Website; for the app, capture the RTL render on device.

---

## BF-15 — Notification-kind → icon / group / deep-link inventory

This cross-page flow proves the **backend-driven notification contract** end-to-end: for every `NotificationKind` the server decides the **group** and the **deep-link (`clickUrl`)**, and each surface renders/behaves from those server values. The single source of truth is `SIMF.Application.Notifications.NotificationKindCatalog` — `GroupFor(kind)` (groups `Account` / `Vip` / `Bookings` / `Sessions` / `Meetings` / `Ratings`) and `ClickUrlFor(kind, relatedId)` (rating kinds → `/rate?code=…&targetId=…`, `BookingConfirmed` → `/badge`, everything else → `null`); `NotificationDispatcher` stamps every row from it when the dispatch request leaves the values null (unit-guarded by `tests/SIMF.Api.Tests/NotificationKindCatalogTests.cs`). **Surfaces:** the mobile Notifications screen (`src/Mobile/simf_app/lib/features/notifications/notifications_screen.dart`, route `/notifications`, Figma `758:2491`) with its per-kind icon (`widgets/notification_category_icon.dart`) and the CP account notifications page (`src/ControlPanel/…/Components/Pages/Account/Notifications.razor`, route `/account/notifications`). **Endpoints** (all under `api/v1`, auth-only): `POST /app/account/notifications/list`, `GET /app/account/notifications/unread-count`, `POST /app/account/notifications/{id:guid}/read`, `POST /app/account/notifications/read-all`, `DELETE /app/account/notifications/{id:guid}` — the `list` / `read` / `read-all` / `delete` endpoints add the `auth` rate-limit policy (`unread-count` is auth-only, no rate limit). **Key rules exercised:** the DTO carries `kind` + `group` + `clickUrl` as strings; the app deep-links only to the allowlist `{ /rate, /badge }` (path-only match, query ignored) and **ignores** any foreign/stale URL because the router has no error page; a kind-based fallback covers pre-migration rows that predate the `group`/`clickUrl` columns (D-677 / D-678, generalising the D-672 hardcode).

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-BF-15-001 | Golden journey: dispatched `BookingConfirmed` is stamped group `Bookings` + clickUrl `/badge`, the app renders the green event-available icon under Bookings, tapping opens the badge | happy | P0 |
| E2E-BF-15-002 | Group inventory: every `NotificationKind` resolves to its correct group; `GroupFor` classifies **every** defined kind (no null bucket) | happy | P0 |
| E2E-BF-15-003 | ClickUrl inventory: per-target rating kinds carry the `targetId`, global rating kinds carry none, `BookingConfirmed`→`/badge`, a per-target kind **without** an id and every informational kind → `null` | happy | P1 |
| E2E-BF-15-004 | Icon inventory: each documented kind → its (colour, glyph); unknown/future kind → severity fallback (forward-safe) | happy | P1 |
| E2E-BF-15-005 | Deep-link on tap: a `SessionRatingRequest` row navigates to `/rate?code=Session&targetId=<sessionId>` | happy | P1 |
| E2E-BF-15-006 | Guard: a non-allowlisted / foreign `clickUrl` is ignored — no navigation, no router error page | resilience | P1 |
| E2E-BF-15-007 | Pre-migration fallback: a row with `group=null` + `clickUrl=null` gets a client-derived group and the kind-based `/badge` / `/rate` fallback | resilience | P1 |
| E2E-BF-15-008 | Unread badge + open-inbox auto mark-all-read clears the Home bell count | happy | P0 |
| E2E-BF-15-009 | Mark one read is idempotent (`POST …/{id}/read`); tapping flips the dot locally and re-polls the count | happy | P1 |
| E2E-BF-15-010 | Auth gate: the list/read/read-all endpoints need a `sub` claim (401 otherwise); CP page is `[Authorize]` | auth | P0 |
| E2E-BF-15-011 | Filter chips map to groups: **جلسات** covers `{Sessions, Bookings, Meetings, Ratings}`, **VIP** covers `{Vip}`, **الكل** shows all | happy | P1 |
| E2E-BF-15-012 | Empty state renders `SimfEmptyState` (no error toast) | happy | P2 |
| E2E-BF-15-013 | RTL render: the Arabic notifications screen mirrors correctly | i18n | P1 |

### Scenarios

### E2E-BF-15-001 — Golden journey (dispatch → group + clickUrl → icon → tap)

```gherkin
Feature: Backend-driven notification contract, end to end
  Background:
    Given the API is reachable on http://localhost:5175
    And an approved visitor is signed in on the app
      # visitor OTP read from SIMF_Identity.AccountCodes at run time — never a literal
    And the visitor has no prior notifications

  Scenario: A confirmed booking is stamped, rendered, and deep-links to the badge
    Given the visitor self-picks a seat and the reservation is confirmed
    Then a notification row is created with kind "BookingConfirmed"
    And NotificationDispatcher stamped it from NotificationKindCatalog:
      | field    | value    |
      | group    | Bookings |
      | clickUrl | /badge   |
    When the app calls POST /api/v1/app/account/notifications/list
    Then the row returns with kind="BookingConfirmed", group="Bookings", clickUrl="/badge"
    And the app renders it with the green "event available" glyph (SimfTokens.notifGreen)
    And it appears under the جلسات chip (Bookings ∈ the sessions chip groups)
    When the visitor taps the row
    Then "/badge" is on the allowlist { /rate, /badge } so the app pushes the badge/QR screen (758-1469)
    And the row is marked read (best effort) and its unread dot clears
```

### E2E-BF-15-002 — Group inventory (every kind → its group)

```gherkin
Scenario Outline: GroupFor maps each NotificationKind to its section
  When NotificationKindCatalog.GroupFor is asked for <kind>
  Then it returns "<group>"

  Examples: Account
    | kind                              | group   |
    | CredentialEmailVerificationSent   | Account |
    | CredentialEmailVerificationResent | Account |
    | CredentialSignInOtpSent           | Account |
    | CredentialPasswordResetRequested  | Account |
    | AccountProfileSubmitted           | Account |
    | AdminPendingVisitor               | Account |
    | AccountApproved                   | Account |
    | AccountRejected                   | Account |
    | AccountTwoFactorReset             | Account |
    | AccountWelcome                    | Account |
    | AccountPasswordChanged            | Account |
    | AccountPasswordResetCompleted     | Account |
    | AdminPendingApproval              | Account |

  Examples: Vip
    | kind               | group |
    | InvitationReceived | Vip   |
    | VipBroadcast       | Vip   |

  Examples: Bookings
    | kind             | group    |
    | BookingConfirmed | Bookings |
    | BookingRejected  | Bookings |

  Examples: Sessions
    | kind            | group    |
    | SessionReminder | Sessions |

  Examples: Meetings
    | kind                    | group    |
    | MeetingScheduled        | Meetings |
    | MeetingCancelled        | Meetings |
    | MeetingRequestConfirmed | Meetings |

  Examples: Ratings
    | kind                    | group   |
    | SessionRatingRequest    | Ratings |
    | DayRatingRequest        | Ratings |
    | EventRatingRequest      | Ratings |
    | AppRatingRequest        | Ratings |
    | ExhibitionRatingRequest | Ratings |

Scenario: No kind falls into the null bucket
  When GroupFor is evaluated for every value of the NotificationKind enum
  Then none returns null or empty
  # NotificationKindCatalogTests.GroupFor_covers_every_defined_kind — a new
  # kind added without a catalog arm fails the build here.
```

### E2E-BF-15-003 — ClickUrl inventory (deep-link per kind)

```gherkin
Scenario Outline: ClickUrlFor(kind, relatedId) yields the app-internal deep-link
  When NotificationKindCatalog.ClickUrlFor is asked for <kind> with relatedId <relatedId>
  Then it returns <clickUrl>

  Examples: navigating kinds
    | kind                    | relatedId                            | clickUrl                                                    |
    | BookingConfirmed        | (any)                                | /badge                                                      |
    | SessionRatingRequest    | 9f2c8b41-0d7a-4e63-b1a2-3c4d5e6f7a80 | /rate?code=Session&targetId=9f2c8b41-0d7a-4e63-b1a2-3c4d5e6f7a80 |
    | DayRatingRequest        | 4b7e1c22-8a90-4d3e-9f11-22aa33bb44cc | /rate?code=Day&targetId=4b7e1c22-8a90-4d3e-9f11-22aa33bb44cc     |
    | EventRatingRequest      | (none)                               | /rate?code=Event                                            |
    | AppRatingRequest        | (none)                               | /rate?code=App                                              |
    | ExhibitionRatingRequest | (none)                               | /rate?code=Exhibition                                       |

  Examples: no-navigation kinds (null clickUrl)
    | kind                 | relatedId | clickUrl |
    | SessionRatingRequest | (none)    | null     |
    | DayRatingRequest     | (none)    | null     |
    | AccountApproved      | (any)     | null     |
    | SessionReminder      | (any)     | null     |
    | MeetingScheduled     | (any)     | null     |
    | MeetingCancelled     | (any)     | null     |
    | BookingRejected      | (any)     | null     |

Scenario: A per-target rating kind with no id yields null, not a broken link
  Given a SessionRatingRequest is dispatched without a RelatedEntityId
  When ClickUrlFor is evaluated
  Then it returns null (the tile is informational — no broken /rate link is produced)
  # NotificationKindCatalogTests.ClickUrlFor_a_per_target_kind_without_an_id_is_null
```

### E2E-BF-15-004 — Icon inventory (per-kind colour + glyph, Figma 758:2491)

```gherkin
Scenario Outline: NotificationCategoryIcon styles each kind
  Given a notification row with kind "<kind>" and severity "<severity>"
  When the app renders its category icon
  Then the circle colour is <colour> and the glyph is <glyph>

  Examples: frame-documented + grouped-by-meaning kinds
    | kind                 | severity | colour               | glyph                        |
    | AccountApproved      | Success  | SimfTokens.accent    | confirmation_number_rounded  |
    | SessionReminder      | Info     | SimfTokens.notifGreen| check_circle_rounded         |
    | MeetingScheduled     | Info     | SimfTokens.notifGreen| credit_card_rounded          |
    | InvitationReceived   | Info     | SimfTokens.notifCoral| star_rounded                 |
    | VipBroadcast         | Info     | SimfTokens.notifCoral| star_rounded                 |
    | BookingConfirmed     | Success  | SimfTokens.notifGreen| event_available_rounded      |
    | BookingRejected      | Error    | SimfTokens.notifCoral| event_busy_rounded           |
    | MeetingCancelled     | Error    | SimfTokens.notifCoral| event_busy_rounded           |
    | AccountRejected      | Error    | SimfTokens.notifCoral| cancel_rounded               |
    | SessionRatingRequest | Info     | SimfTokens.accent    | star_outline_rounded         |

Scenario: An unknown / future kind falls back to its severity style (forward-safe)
  Given a notification row with an unrecognised kind "SomeFutureKind" and severity "Warning"
  When the app renders its category icon
  Then it uses the severity fallback (accent + priority_high_rounded)
  And the wire stays forward-compatible (no crash on an unknown kind)
```

### E2E-BF-15-005 — Deep-link on tap (rating prompt → /rate)

```gherkin
Scenario: Tapping a session-rating notification opens the correct rate target
  Given the visitor has an unread notification:
    | kind     | SessionRatingRequest                                             |
    | group    | Ratings                                                          |
    | clickUrl | /rate?code=Session&targetId=9f2c8b41-0d7a-4e63-b1a2-3c4d5e6f7a80 |
  When the visitor taps the row
  Then the app parses clickUrl, matches path "/rate" against the allowlist { /rate, /badge }
  And pushes /rate?code=Session&targetId=9f2c8b41-0d7a-4e63-b1a2-3c4d5e6f7a80
  And the deep-link fires BEFORE the best-effort mark-read (so navigation is never skipped by an unmount)
  And POST /api/v1/app/account/notifications/{id}/read is then called in the background
```

### E2E-BF-15-006 — Guard: foreign clickUrl is ignored

```gherkin
Scenario: A stale or foreign clickUrl never lands on the router fallback
  Given the visitor has a notification whose clickUrl is "https://evil.example.com/x"
  When the visitor taps the row
  Then Uri.path "/x" (or the external host) is NOT in the allowlist { /rate, /badge }
  And no navigation happens
  And no kind-based fallback applies (the kind is not one of the fallback kinds)
  And the app stays on /notifications with no error page (the router has none)

Scenario: Only the path is matched — the query string is ignored by the guard
  Given a notification with clickUrl "/rate?code=Session&targetId=9f2c8b41-0d7a-4e63-b1a2-3c4d5e6f7a80"
  When the visitor taps the row
  Then only "/rate" is checked against the allowlist and the full URL (query included) is pushed
```

### E2E-BF-15-007 — Pre-migration fallback (no group / no clickUrl)

```gherkin
Scenario: A row created before the group + clickUrl columns still routes and sections correctly
  Given a legacy notification with group=null and clickUrl=null and kind "BookingConfirmed"
  When the app computes its group
  Then _groupForItem derives "Bookings" from the kind (client fallback)
  When the visitor taps the row
  Then the kind-based fallback pushes the badge/QR screen (BookingConfirmed OR AccountApproved → /badge)

Scenario: A legacy SessionRatingRequest with a relatedEntityId falls back to /rate
  Given a legacy notification with clickUrl=null, kind "SessionRatingRequest",
    and relatedEntityId "9f2c8b41-0d7a-4e63-b1a2-3c4d5e6f7a80"
  When the visitor taps the row
  Then the app pushes RouteNames.rate with code=Session and targetId=9f2c8b41-0d7a-4e63-b1a2-3c4d5e6f7a80
```

### E2E-BF-15-008 — Unread badge + open-inbox auto mark-all-read

```gherkin
Scenario: Opening the inbox marks everything read and clears the Home bell
  Given the visitor has 3 unread notifications
  And GET /api/v1/app/account/notifications/unread-count returns 3 (the Home bell shows 3)
  When the visitor opens /notifications
  Then the list loads via POST …/notifications/list (newest first)
  And because at least one row is unread, the screen calls POST …/notifications/read-all
  And the unread dots clear
  And unreadNotificationCountProvider is invalidated so the Home bell badge drops to 0
  And no "mark all read" button remains (hasUnread is now false)
```

### E2E-BF-15-009 — Mark one read is idempotent

```gherkin
Scenario: Tapping an unread row marks exactly that row read, idempotently
  Given the visitor has an unread informational notification (clickUrl=null, e.g. AccountWelcome)
  When the visitor taps the row
  Then POST /api/v1/app/account/notifications/{id}/read is called
  And the row flips to read locally (no full reload) and its dot clears
  And unreadNotificationCountProvider is invalidated
  When the same {id}/read is POSTed again
  Then it succeeds again with ApiResult.Ok(true) (idempotent — no error, no double-count)

Scenario: A mark-read that fails leaves the row unread (best effort)
  Given the mark-read call returns an ApiFailure
  When the visitor taps the unread row
  Then the deep-link (if any) still fired first
  And the row remains unread locally (best-effort — not falsely cleared)
```

### E2E-BF-15-010 — Auth gate

```gherkin
Scenario: The notification endpoints require an authenticated user
  Given a request to POST /api/v1/app/account/notifications/list with no valid bearer (no "sub" claim)
  Then the endpoint responds HTTP 401 Unauthorized
  And the same holds for POST …/{id}/read and POST …/read-all and GET …/unread-count
  And every list/read/read-all/delete endpoint is rate-limited under the "auth" policy

Scenario: The CP notifications page requires a signed-in administrator
  Given an unauthenticated browser opens /account/notifications
  Then it is redirected to the CP sign-in
  # The page carries [Authorize] — it is the admin's OWN account notifications,
  # not a permission-catalogued admin action, so any signed-in admin may view their own.
```

### E2E-BF-15-011 — Filter chips map to groups

```gherkin
Scenario Outline: The chip filters by the server group code
  Given the visitor's inbox contains rows across groups Account, Bookings, Sessions, Meetings, Ratings, Vip
  When the visitor selects the "<chip>" chip
  Then only rows whose group ∈ <groups> are shown

  Examples:
    | chip   | groups                                  |
    | الكل   | (all rows — no group filter)            |
    | جلسات  | Sessions, Bookings, Meetings, Ratings   |
    | VIP    | Vip                                      |

Scenario: The search box filters the visible rows by title/body within the active chip
  Given the جلسات chip is active
  When the visitor types a query that matches no visible title or body
  Then a SimfEmptyState with the "no matches" message renders (search_off icon)
  And no error toast appears
```

### E2E-BF-15-012 — Empty state

```gherkin
Scenario: A visitor with no notifications sees the empty state
  Given the visitor has zero notifications
  When they open /notifications
  Then POST …/notifications/list returns an empty items array
  And the screen shows SimfEmptyState (notifications_none_outlined icon) with the empty message
  And no "mark all read" button and no error toast appear
  And pull-to-refresh still works (the empty state sits in a viewport-tall scrollable host)
```

### E2E-BF-15-013 — RTL render

```gherkin
Scenario: The Arabic notifications screen mirrors correctly
  Given the app language is Arabic (the primary language, RTL)
  When the visitor opens /notifications
  Then the header title reads "الإشعارات" and the back affordance sits on the right
  And the الكل / جلسات / VIP chips lay out right-to-left
  And each row's category icon, title, body, and unread dot mirror to the RTL edge
  And the day groupings render as اليوم / أمس / date
  And scrollWidth == clientWidth (no horizontal overflow)
```

### Notes

- **Group + clickUrl are server-decided.** The dispatcher stamps both from `NotificationKindCatalog` only when the dispatch request leaves them null; a call site may override either, so a test that dispatches with explicit values should assert the override, not the catalog default.
- **`MeetingRequestConfirmed` (=50) has no client-side fallback arm.** The app's `_groupForItem` switch lists only `MeetingScheduled`/`MeetingCancelled` for Meetings, so a `MeetingRequestConfirmed` row with a **null** server `group` would fall to `Account` client-side. In practice this kind postdates the `group` column, so its server `group` is always `Meetings` — but a hand-crafted legacy fixture would expose the gap. Its `clickUrl` is `null` (informational — no navigation), consistent with the other meeting kinds.
- **Meeting + rejection kinds do not navigate.** `MeetingScheduled`, `MeetingCancelled`, `MeetingRequestConfirmed`, `SessionReminder`, and `BookingRejected` are grouped but have `null` clickUrl — tapping them marks read and does nothing else. Only `BookingConfirmed` (→ `/badge`) and the five rating kinds (→ `/rate`) navigate.
- **Deliberate icon deviation (Figma 758:2491).** The VIP kinds (`InvitationReceived` / `VipBroadcast`) render a coral **star** (`star_rounded`), not the ✕/close-circle the mockup literally shows, because a ✕ on a positive VIP invitation reads as an error. Documented in `notification_category_icon.dart`; do not "correct" it without owner sign-off.
- **Allowlist is path-only.** `_allowedClickPaths = { /rate, /badge }` matches `Uri.path` and ignores the query string; a foreign host or unknown path is silently ignored because the router has no error page — assert "no navigation", not "error page shown".
- **CP page auth model.** `/account/notifications` is `[Authorize]` (own-account view), not a `PermissionCatalog`-gated admin action, so it is not covered by the per-page permission tests — its gate is authentication only.
- **Wire contract is frozen (D-219).** `kind`, `group`, `clickUrl`, `severity`, `relatedEntityType`, `relatedEntityId` and the title/body pairs are the JSON keys the shipped app decodes; assert them by name and never rename them in a fixture-driven test.

---

_Created 2026-07-11 by the SIMF Team (authored + adversarially verified via a multi-agent workflow). Living catalogue._
