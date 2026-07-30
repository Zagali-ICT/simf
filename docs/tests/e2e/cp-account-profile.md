# E2E test catalogue — My profile (`/account/profile`)

| | |
|--|--|
| **Page** | [`cp/account-profile.md`](../../pages/cp/account-profile.md) |
| **Route** | `/account/profile` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Page shape (verified against `Profile.razor`, 2026-06-02).** This is a
> **personal self-service** page — every signed-in CP user reaches it from the
> top-bar user link. It is gated by `[Authorize]` **only**; it carries **no**
> `@attribute [RequirePermission(...)]` and is **not** a `CpNavigation` item with
> a `RequiredPermission`. So the "auth gate" here is the *unauthenticated*
> redirect to `/login`, not a `/not-permitted` per-permission denial.
>
> The page has five `simf-card` sections (the reference doc's old "Identity card /
> Sessions card / display-name edit" copy is **stale** — those affordances are not
> on this page):
> 1. **Two-factor authentication** — Enable → scan QR → Confirm; Disable (code-gated); Re-enrol.
> 2. **Recovery codes** (only rendered when 2FA is on) — Generate / Regenerate; show-once list; "I have saved these codes".
> 3. **Change password** — the shared `ChangePasswordCard` (Current / New / Confirm) — success signs the user out.
> 4. **My roles** — read-only list of the account's role names.
> 5. **My avatar** — pick file → `SimfImageCropperModal` (crop 400×400) → upload; Remove avatar.
>
> Every action goes through CP BFF proxy routes under `/account/api/…` (the page
> never sees the access token); each proxy forwards to the SIMF API and returns
> the upstream status verbatim.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-PRF-001 | Golden path — load profile, read 2FA status + roles, round-trip avatar (pick → crop → save → remove) | happy | P0 | _to author_ |
| E2E-PRF-002 | Enable 2FA — setup QR → enter live TOTP → Confirm → recovery codes shown once | happy | P0 | _to author_ |
| E2E-PRF-003 | Disable 2FA — Disable → enter live TOTP → confirm → status flips to Off | happy | P1 | _to author_ |
| E2E-PRF-004 | Re-enrol 2FA — Re-enrol opens a fresh secret + QR | happy | P2 | _to author_ |
| E2E-PRF-005 | Generate / regenerate recovery codes — 10 fresh codes shown once, count refreshes | happy | P1 | _to author_ |
| E2E-PRF-006 | Change password (golden) — valid change signs the user out | happy | P0 | _to author_ |
| E2E-PRF-007 | My roles — read-only list renders the account's roles (and the empty "no roles" copy) | happy | P1 | _to author_ |
| E2E-PRF-008 | Remove avatar — Remove button clears the avatar and the top-bar chrome | happy | P1 | _to author_ |
| E2E-PRF-009 | Empty / first-load state — no avatar shows the placeholder, 2FA-off hides recovery card | happy | P1 | _to author_ |
| E2E-PRF-010 | Auth gate — anonymous visitor to `/account/profile` is redirected to `/login` | auth | P0 | _to author_ |
| E2E-PRF-011 | Validation — confirm 2FA with a wrong code → bilingual flash error, stays in setup | error | P1 | _to author_ |
| E2E-PRF-012 | Validation — change-password mismatch / weak password → in-card error, no sign-out | error | P1 | _to author_ |
| E2E-PRF-013 | Avatar rejected — > 2 MB or non-image file → bilingual avatar error | error | P1 | _to author_ |
| E2E-PRF-014 | Disable 2FA with wrong code → bilingual flash error, 2FA stays on | error | P1 | _to author_ |
| E2E-PRF-015 | Server 500 resilience — `/account/api/profile` fails → loading copy persists, no crash | resilience | P2 | _to author_ |
| E2E-PRF-016 | RTL / Arabic render — page + cards + cropper modal mirror | i18n | P1 | _to author_ |
| E2E-PRF-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-PRF-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-PRF-001 — Golden path (load + avatar round-trip)

```gherkin
Feature: My profile golden path
  As a signed-in Control Panel user
  I want to view my profile and manage my avatar
  So that my account stays current

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And superadmin@zagali-ict.com has signed in via /login + /login/totp (code from Get-Totp)
  And they have navigated to /account/profile

Scenario: Page loads, shows status + roles, then a full avatar round-trip
  Given the page has finished loading
  Then the "Loading…" placeholder is gone
  And a "Two-factor authentication" card shows either "Two-factor authentication is on." or "…is off."
  And a "Change password" card shows three fields: Current password, New password, Confirm new password
  And a "My roles" card lists the account's roles (e.g. "Administrator")
  And a "My avatar" card shows either the current image or the user placeholder icon
  And the helper text reads "PNG, JPEG or WebP, up to 2 MB."

  When the user picks a valid 600x600 PNG via the "Choose avatar image file" input
  Then the SimfImageCropperModal opens titled "Crop your avatar"
  When they confirm the crop
  Then the cropper closes
  And POST /account/api/avatar is sent as multipart with the cropped 400x400 PNG
  And the proxy returns HTTP 200 with ApiResult.Data.AvatarUrl containing "/account/api/avatar/<userId>?v="
  And a green SimfAlert reads "Avatar updated." / "تم تحديث الصورة الشخصية."
  And the avatar card image src now points at /account/api/avatar/<userId>?v=<ticks>
  And the top-bar chrome avatar refreshes to the same image

  When the user clicks "Remove avatar"
  Then a SimfConfirm dialog opens titled "Remove profile photo" (D-809)
  And no DELETE has been sent yet
  When they click "Remove avatar" in the dialog
  Then DELETE /account/api/avatar returns HTTP 200
  And a green SimfAlert reads "Avatar removed." / "تمت إزالة الصورة الشخصية."
  And the avatar card falls back to the placeholder icon
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-account-profile-golden-before.png`
- Screenshot after (avatar set): `docs/screenshots/cp-account-profile-golden-avatar.png`
- Screenshot after (avatar removed): `docs/screenshots/cp-account-profile-golden-removed.png`
- Console errors: 0 expected (D-123 fixed the cropper dispose crash by load-ordering `cropper.min.js` before `cropperJsInterop.min.js`)
- Network: `GET /account/api/profile`, `POST /account/api/avatar`, `DELETE /account/api/avatar` and the `GET /account/api/avatar/<userId>?v=…` image fetch all return 200
- Audit / lower-layer cover: the API-level avatar set/clear is exercised at `tests/SIMF.Api.Tests/ProfileEndpointsTests.cs`

### E2E-PRF-002 — Enable 2FA (setup → confirm → recovery codes)

```gherkin
Scenario: Enrol a fresh authenticator and capture the one-time recovery codes
  Given the user's 2FA is currently off ("Two-factor authentication is off.")
  When they click "Enable"
  Then POST /account/api/totp/setup returns HTTP 200 with Data.Secret, Data.OtpAuthUri and Data.QrCodeSvg
  And the card renders the QR (inline SVG) and the secret in a <code class="simf-totp-secret">
  And the instruction reads "Scan this QR code with Google Authenticator, then enter the six-digit code below…"
  When the tester feeds Data.Secret to the Get-Totp helper to produce the current six-digit code
  And enters it into the "Verification code" field
  And clicks "Confirm"
  Then POST /account/api/totp/confirm returns HTTP 200 with Data.TwoFactorEnabled = true
  And a green SimfAlert reads "Two-factor authentication is now enabled." / "تم تفعيل المصادقة الثنائية."
  And the Recovery codes card now appears with the show-once banner "Save these codes now. They will not be shown again. Each works only once."
  And exactly the codes from Data.RecoveryCodes render in an ordered <ol class="simf-recovery-codes">
  When they click "I have saved these codes"
  Then the code list is dismissed and the card shows "{n} of 10 recovery codes remaining."
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-account-profile-2fa-setup.png`, `docs/screenshots/cp-account-profile-2fa-codes.png`
- Network: `/account/api/totp/setup` 200, `/account/api/totp/confirm` 200
- Console errors: 0 expected
- Note: this mutates the signed-in account's 2FA — run against a throwaway test admin or restore via E2E-PRF-003 afterward.

### E2E-PRF-003 — Disable 2FA

```gherkin
Scenario: Disable two-factor with a live code
  Given the user's 2FA is currently on
  When they click "Disable"
  Then a code form appears with a "Verification code" field, "Confirm" and "Cancel"
  When the tester enters the current code from Get-Totp
  And clicks "Confirm"
  Then POST /account/api/totp/disable returns HTTP 200 with Data.TwoFactorEnabled = false
  And a green SimfAlert reads "Two-factor authentication is now disabled." / "تم تعطيل المصادقة الثنائية."
  And the status line flips to "Two-factor authentication is off."
  And the Recovery codes card is no longer rendered
```

### E2E-PRF-004 — Re-enrol 2FA

```gherkin
Scenario: Re-enrol replaces the paired secret
  Given the user's 2FA is currently on
  When they click "Re-enrol"
  Then POST /account/api/totp/setup returns HTTP 200 with a fresh Data.Secret + Data.QrCodeSvg
  And the QR + secret + Verification-code form render exactly as in the Enable flow
  When they click "Cancel"
  Then the setup form is dismissed with no further API call and 2FA stays on
```

### E2E-PRF-005 — Generate / regenerate recovery codes

```gherkin
Scenario: Regenerate the recovery-code batch
  Given the user's 2FA is on and the Recovery codes card is visible
  When they click "Regenerate recovery codes" (or "Generate recovery codes" when none remain)
  Then POST /account/api/recovery-codes/regenerate returns HTTP 200 with Data.RecoveryCodes
  And the show-once banner appears and exactly those codes render in the <ol>
  And the previous codes are now invalid
  When they click "I have saved these codes"
  Then the list is dismissed and the status reads "10 of 10 recovery codes remaining."
  And when the remaining count is <= 3 the info alert "You are running low on recovery codes…" shows instead
```

### E2E-PRF-006 — Change password (golden)

```gherkin
Scenario: A valid password change signs the user out
  Given the user is on /account/profile
  When they fill Current password = "[REDACTED - supply via SIMF_SuperAdmin__TempPassword]"
  And New password = "Bb@987654321"
  And Confirm new password = "Bb@987654321"
  And click "Update password"
  Then POST /account/api/change-password returns HTTP 200 (Success = true)
  And the page invokes simfAccount.signOut (POST /auth/sign-out — never a GET)
  And the browser lands back on /login (the security stamp rolled, every session revoked)
  And signing in again succeeds only with the NEW password
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-account-profile-pwd-before.png`, `docs/screenshots/cp-account-profile-pwd-signedout.png`
- Network: `/account/api/change-password` 200 then `/auth/sign-out` POST
- Note: destructive — it signs you out and changes the password. Run last, or against a throwaway admin, and record the new password.

### E2E-PRF-007 — My roles (read-only)

```gherkin
Scenario: Roles card lists the account's roles
  Given the signed-in account has the "Administrator" role
  When the profile finishes loading
  Then the "My roles" card lists "Administrator" in a <ul class="simf-role-list">
  And there is no add / edit / remove control on this card (it is read-only)

Scenario: Account with no roles shows the empty copy
  Given the signed-in account has zero roles
  When the profile finishes loading
  Then the "My roles" card shows "No roles are assigned to this account."
```

### E2E-PRF-008 — Remove avatar

```gherkin
Scenario: Remove the existing avatar
  Given the account currently has an avatar (the image is shown, "Remove avatar" is visible)
  When they click "Remove avatar"
  Then DELETE /account/api/avatar returns HTTP 200
  And the avatar card shows the placeholder icon
  And a green SimfAlert reads "Avatar removed." / "تمت إزالة الصورة الشخصية."
  And the "Remove avatar" button is no longer rendered (no avatar to remove)
```

### E2E-PRF-009 — Empty / first-load state

```gherkin
Scenario: A clean account renders placeholders, not errors
  Given a freshly seeded admin with no avatar and 2FA off
  When they open /account/profile
  Then the avatar card shows the <SimfIcon Name="user"> placeholder, not a broken <img>
  And the Recovery codes card is NOT rendered (it only shows when 2FA is on)
  And the Two-factor card shows just the "Enable" button
  And no red SimfAlert appears on any card
```

### E2E-PRF-010 — Auth gate (anonymous → /login)

```gherkin
Scenario: An unauthenticated request is redirected to sign in
  Given no CP auth cookie is present (signed out)
  When the browser navigates to /account/profile
  Then the [Authorize] gate redirects to /login (with the return URL preserved)
  And no /account/api/profile request fires while signed out

# NOTE: this page carries no per-page RequirePermission — any *signed-in* CP user
# may open it (it is personal self-service). So there is NO /not-permitted path
# here; the only gate is the unauthenticated /login redirect.
```

### E2E-PRF-011 — Wrong 2FA confirm code

```gherkin
Scenario: Confirming enrolment with a wrong code shows a bilingual error
  Given the user clicked "Enable" and the QR + Verification-code form are shown
  When they enter "000000" (a code that does not match the new secret)
  And click "Confirm"
  Then POST /account/api/totp/confirm returns a non-2xx with ApiResult.Error.Code = "TOTP_ENROLMENT_CODE_INVALID"
  And a red SimfAlert (the flash error) surfaces the bilingual MessageForCurrentCulture()
  And the setup form stays open (QR + field still visible) so the user can retry
  And 2FA is NOT enabled
```

### E2E-PRF-012 — Change-password validation

```gherkin
Scenario: Mismatched new password is rejected without signing out
  Given the user is on /account/profile
  When they fill Current password = "[REDACTED - supply via SIMF_SuperAdmin__TempPassword]"
  And New password = "Bb@987654321"
  And Confirm new password = "different"
  And click "Update password"
  Then POST /account/api/change-password returns HTTP 400 (Success = false)
  And the ChangePasswordCard shows a red SimfAlert with the bilingual server message
  And the user is NOT signed out (still on /account/profile)

Scenario: A new password that fails the policy is rejected
  Given the user is on /account/profile
  When they submit a New password = "weak"
  Then the API responds with a DataValidationException mapped from AUTH_PASSWORD_POLICY
  And the card shows the bilingual password-policy error
  And the user stays signed in on the page
```

### E2E-PRF-013 — Avatar rejected (too large / wrong type)

```gherkin
Scenario: An oversized image is rejected with a bilingual error
  Given the user is on /account/profile
  When they pick a PNG larger than 2 MB and confirm the crop
  Then POST /account/api/avatar returns HTTP 400 with ApiResult.Error.Code = "AVATAR_FILE_TOO_LARGE"
  And a red SimfAlert surfaces the bilingual MessageForCurrentCulture()
  And the existing avatar (if any) is unchanged

Scenario: A non-image file is rejected at the picker
  Given the user is on /account/profile
  When they pick a .pdf via the file input (accept="image/png,image/jpeg,image/webp")
  Then the cropper does not open OR the upload returns AVATAR_MIME_UNSUPPORTED
  And a red SimfAlert reads "The file could not be read. Please pick another image." (pick failure)
    or surfaces the bilingual server message for an unsupported MIME type
```

### E2E-PRF-014 — Wrong 2FA disable code

```gherkin
Scenario: Disabling 2FA with a wrong code leaves it on
  Given the user's 2FA is on and they clicked "Disable"
  When they enter "000000" in the Verification-code field
  And click "Confirm"
  Then POST /account/api/totp/disable returns a non-2xx (TOTP code invalid)
  And a red SimfAlert (flash error) shows the bilingual message
  And the status line still reads "Two-factor authentication is on."
  And the Recovery codes card is still rendered
```

### E2E-PRF-015 — Server 500 on profile load

```gherkin
Scenario: The profile API failing does not crash the page
  Given the API is configured to fail GET /api/v1/account/profile (e.g. DB down → 500)
  When the user opens /account/profile
  Then the BFF GET /account/api/profile returns the upstream non-200 verbatim
  And _profile stays null so the page keeps showing the "Loading…" card
  And no unhandled Blazor circuit exception appears in the console
```

### E2E-PRF-016 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the whole page + the cropper modal
  Given the user is on /account/profile in English
  When they switch the UI language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the page title reads "ملفي الشخصي"
  And the card titles read "المصادقة الثنائية", "تغيير كلمة المرور", "صلاحياتي", "صورتي الشخصية"
  And the form action buttons appear in reverse order
  When they open the avatar cropper
  Then the SimfImageCropperModal renders in RTL with its Arabic title
```

---

## Implementation notes

- **API integration coverage already exists** at
  `tests/SIMF.Api.Tests/ProfileEndpointsTests.cs` — it covers the lower layer
  for the avatar surface without a browser:
  - `GetProfile_returns_the_signed_in_users_details` (200, fields, empty roles)
  - `GetProfile_without_a_bearer_token_returns_401` (the auth gate at the API)
  - upload happy path + replace + delete (`AvatarUrl` round-trip, on-disk file count)
  - rejection paths: `AVATAR_MIME_UNSUPPORTED` (wrong type, **and** bytes-vs-declared-MIME mismatch),
    `AVATAR_FILE_TOO_LARGE` (> 2 MB), `AVATAR_FILE_MISSING` (empty part),
    case-insensitive MIME allowlist, and `403`/`404` on the fetch-by-id endpoint.
  These cover E2E-PRF-001 / -008 / -013 at the API layer; the E2E rows add the
  browser-driven cropper flow, the chrome-avatar refresh and the bilingual toasts.
- **2FA / change-password** are exercised by the auth-flow integration tests
  (the `AuthFlow` helper used across `SIMF.Api.Tests`); the E2E rows here add the
  CP BFF proxy hop (`/account/api/totp/*`, `/account/api/change-password`) and the
  live `Get-Totp` code generation that an integration test stubs out.
- **BFF proxy surface** (all under `/account/api`, defined in
  `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs`):
  `GET /profile`, `POST /totp/setup`, `POST /totp/confirm`, `POST /totp/disable`,
  `POST /recovery-codes/regenerate`, `POST /change-password`, `POST /avatar`
  (multipart, `DisableAntiforgery`), `DELETE /avatar`, `GET /avatar/{userId}`.
  Each reads the access token from the cookie and forwards verbatim — the page
  never holds the token.
- **Convert to Playwright** later by copying each Gherkin scenario into a
  `.feature` under `tests/SIMF.E2E.Tests/` (project to be created) + a
  step-definition class. The Gherkin shape is already runner-agnostic. Keep the
  `Get-Totp` step as a helper binding so the live-code scenarios stay reproducible.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
