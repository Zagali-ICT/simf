# E2E test catalogue — TOTP re-pairing (`/account/totp-pairing`)

| | |
|--|--|
| **Page** | [`cp/account-totp-pairing.md`](../../pages/cp/account-totp-pairing.md) |
| **Route** | `/account/totp-pairing` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@simrsnf.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Page shape (read from `TotpPairing.razor`, not the older reference doc).**
> This is a **personal account page** under `CpShellLayout`, gated by
> *authentication only* — the BFF group `MapGroup("/account/api")` is
> `.RequireAuthorization()` and the page carries **no `[RequirePermission]`
> attribute**. Any signed-in admin reaches their *own* pairing; there is no
> per-page permission, so the auth-gate scenario is the **signed-out →
> `/login` redirect**, not `/not-permitted`.
>
> The live page renders exactly four states and one action:
> 1. **Loading** — `TotpPairing.Loading` ("Loading…") while GET `/account/api/totp/pairing` is in flight.
> 2. **No secret** (API 404) — a warning `SimfAlert` ("This account does not have an authenticator secret yet. Use the Profile page to enrol.") + a "Go to profile" button → `/account/profile`.
> 3. **Load error** (API 5xx / non-404 error envelope) — an error `SimfAlert` ("Could not load the pairing QR.").
> 4. **Paired** — the QR SVG, the manual-entry secret in a `<code>`, then the **Confirm the scan** section: a `SimfCodeField` (6-digit, `maxlength=6`, `inputmode=numeric`) + **Verify** button posting to `/account/api/totp/pairing/verify`.
>
> The reference doc's "10 recovery codes shown" + "Pair" / "Continue" buttons do
> **not** exist on the current page — re-pairing never rotates the secret and
> never mints recovery codes (that is the whole D-096/D-102 point). Scenarios
> below are grounded in the real `.razor`, not that stale section.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-TPP-001 | Golden path — enter a live code → QR for the current secret is revealed → Verify a live code → "Scan confirmed" success | happy | P0 | _to author_ |
| E2E-TPP-002 | Manual-entry secret matches the QR + `Get-Totp` of that secret verifies | happy | P1 | _to author_ |
| E2E-TPP-003 | No-secret state (API 404) → warning + "Go to profile" routes to `/account/profile` | happy | P1 | _to author_ |
| E2E-TPP-004 | The reveal button shows its loading state while the code is checked | happy | P2 | _to author_ |
| E2E-TPP-005 | Auth gate — signed-out visitor → `/login` redirect (no `/totp/pairing` call) | auth | P0 | _to author_ |
| E2E-TPP-006 | Wrong code → `Valid:false` (HTTP 200) → red "That code is not correct" alert; QR stays | error | P0 | _to author_ |
| E2E-TPP-007 | Empty / short code submitted → still posts, server returns `Valid:false` → error alert | error | P1 | _to author_ |
| E2E-TPP-008 | Re-pair does NOT rotate — secret + recovery-code count unchanged after Verify | happy | P0 | _to author_ |
| E2E-TPP-009 | Server 500 on the pairing POST → load-error alert ("Could not load the pairing QR."), QR stays hidden | resilience | P2 | _to author_ |
| E2E-TPP-010 | RTL / Arabic render — page + Verify section mirror, Arabic strings | i18n | P1 | _to author_ |
| E2E-TPP-011 | The QR is NEVER shown to a bearer token alone: opening the page reveals no secret, and a wrong code is refused 400 with the secret absent from the response | auth | P0 | _to author_ |
| E2E-TPP-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-TPP-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-TPP-001 — Golden path (load QR → verify a live code)

```gherkin
Feature: TOTP re-pairing golden path
  As an Administrator whose authenticator device was lost
  I want to re-scan the QR for my CURRENT secret and confirm it works
  So that I can keep signing in without resetting my secret or recovery codes

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And the Administrator superadmin@simrsnf.com has signed in via /login + /login/totp
  And that account already has an active authenticator secret (it is enrolled)
  And the Administrator has navigated to /account/totp-pairing

Scenario: Re-fetch the QR and confirm a live code
  When the page loads
  Then GET /account/api/totp/pairing returns HTTP 200 with ApiResult.Success = true
  And the instruction reads "Scan this QR code with Google Authenticator (or any compatible TOTP app). The secret stays the same — re-scanning does not affect your existing recovery codes."
  And an <svg> QR code renders inside .simf-qr
  And the manual-entry secret renders in a <code class="simf-totp-secret"> element
  And the "Confirm the scan" heading is visible above a "Verification code" field and a "Verify" button

  When the tester computes a live code with: Get-Totp -Secret <the .simf-totp-secret text>
  And fills the "Verification code" field with that 6-digit code
  And clicks "Verify"
  Then POST /account/api/totp/pairing/verify is sent with body { "code": "<6 digits>" }
  And the API returns HTTP 200 with ApiResult.Data.Valid = true
  And a green SimfAlert reads "Scan confirmed. Your authenticator is now paired with this account."
  And the "Verification code" field is cleared
  And no error alert is shown
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-account-totp-pairing-golden-before.png` (QR + secret + empty Verify field)
- Screenshot after: `docs/screenshots/cp-account-totp-pairing-golden-after.png` (green "Scan confirmed" alert)
- Console errors: 0 expected
- Network: `GET /account/api/totp/pairing` → 200; `POST /account/api/totp/pairing/verify` → 200 with `Data.Valid = true`
- Audit row: **none expected** — D-102 verify is deliberately side-effect-free (no replay-guard update, no flag change, no audit row). Confirm `OperationLog` / `RowAudit` gain **no** new row for this action.

### E2E-TPP-002 — Manual-entry secret matches the QR

```gherkin
Scenario: The displayed secret is the real base32 the QR encodes
  Given the Administrator is on /account/totp-pairing with the QR rendered
  When the tester reads the text of the <code class="simf-totp-secret"> element
  And computes a code with: Get-Totp -Secret <that secret text>
  And fills "Verification code" with that code
  And clicks "Verify"
  Then the API returns ApiResult.Data.Valid = true
  And the green "Scan confirmed. Your authenticator is now paired with this account." alert appears
  # Proves the manual-entry secret is the genuine active secret, not a decoy or a freshly rotated one.
```

### E2E-TPP-003 — No-secret state (API 404)

```gherkin
Scenario: Account without an authenticator secret sees the enrol nudge
  Given a signed-in admin account that has NEVER enrolled in TOTP
  When they navigate to /account/totp-pairing
  Then GET /account/api/totp/pairing returns HTTP 404
  And the page treats the 404 as "no secret" (envelope failed, no specific error code)
  And a warning SimfAlert reads "This account does not have an authenticator secret yet. Use the Profile page to enrol."
  And no QR and no "Confirm the scan" section render
  And a "Go to profile" button is visible

  When they click "Go to profile"
  Then the browser navigates to /account/profile
```

### E2E-TPP-004 — Loading state

```gherkin
Scenario: Loading copy shows before the QR paints
  Given the API GET /account/api/totp/pairing is artificially delayed (e.g. throttled network)
  When the Administrator opens /account/totp-pairing
  Then while the call is in flight the page shows "Loading…" (TotpPairing.Loading)
  And once the 200 arrives the loading text is replaced by the QR + secret + Verify section
```

### E2E-TPP-005 — Auth gate (signed-out → /login)

```gherkin
Scenario: An unauthenticated visitor cannot reach the pairing page
  Given no SIMF Control Panel auth cookie is present (signed out / fresh browser)
  When the browser navigates to http://localhost:5158/account/totp-pairing
  Then the Control Panel redirects to /login (the CpShellLayout authentication challenge)
  And NO GET /account/api/totp/pairing request fires (the BFF group is RequireAuthorization)
  # Note: this page carries NO [RequirePermission] attribute, so the gate is
  # authentication, not a per-page permission — a signed-in admin lacking other
  # permissions still reaches their OWN pairing page (not /not-permitted).
```

### E2E-TPP-006 — Wrong code

```gherkin
Scenario: An incorrect code is rejected without a server error
  Given the Administrator is on /account/totp-pairing with the QR rendered
  When they fill "Verification code" with "000000" (a code the authenticator is NOT showing)
  And click "Verify"
  Then POST /account/api/totp/pairing/verify returns HTTP 200 with ApiResult.Success = true and ApiResult.Data.Valid = false
  And a red SimfAlert reads "That code is not correct. Wait for the next code and try again."
  And NO success alert is shown
  And the QR and Verify section stay on screen so the user can read the next code and retry
  # The verify endpoint never emits an error envelope for a bad code — it returns
  # a SUCCESSFUL 200 with Valid=false, and the page maps Valid=false to _verifyError.
```

### E2E-TPP-007 — Empty / short code

```gherkin
Scenario: Submitting an empty or partial code is rejected by the server, not the client
  Given the Administrator is on /account/totp-pairing with the QR rendered
  And the "Verification code" field is left blank (or holds fewer than 6 digits)
  When they click "Verify"
  Then the page posts { "code": "" } (or the partial value) to /account/api/totp/pairing/verify
  And the API returns HTTP 200 with ApiResult.Data.Valid = false
  And the red "That code is not correct. Wait for the next code and try again." alert appears
  # The SimfCodeField has maxlength=6 but no client-side "required" validation on
  # this page; the empty/short value is sent and the server answers Valid=false.
```

### E2E-TPP-008 — Re-pair does NOT rotate the secret

```gherkin
Scenario: Re-pairing leaves the secret and recovery-code count untouched
  Given the Administrator has signed in and opened /account/profile
  And notes the current "recovery codes remaining" count (from ProfileResponse.RecoveryCodesRemaining)
  When they open /account/totp-pairing and read the displayed secret S1
  And they reload /account/totp-pairing and read the displayed secret S2
  Then S2 equals S1 (the read endpoint is idempotent — GET /auth/totp/pairing never rotates)
  When they Verify a live Get-Totp code and see "Scan confirmed."
  And they reopen /account/profile
  Then the "recovery codes remaining" count is unchanged from before
  And TwoFactorEnabled is still true
  # Contrast with POST /auth/totp/setup which rotates a candidate secret each call.
```

### E2E-TPP-009 — Server 500 on the pairing GET

```gherkin
Scenario: A 500 from the pairing GET shows the load-error alert
  Given the API is configured to return HTTP 500 on GET /auth/totp/pairing (e.g. DB down)
  When the Administrator opens /account/totp-pairing
  Then the BFF forwards the failed envelope (non-404, with an error code) to the page
  And an error SimfAlert reads "Could not load the pairing QR." (TotpPairing.LoadError)
  And no QR, no secret and no "Confirm the scan" section render
  # The page distinguishes 404 (-> "no secret" warning) from other failures
  # (-> LoadError). A thrown/parse exception in the JS interop also lands here.
```

### E2E-TPP-010 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the Verify section
  Given the Administrator is on /account/totp-pairing in English with the QR rendered
  When they switch the UI language to العربية from the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the page title reads "إعادة إقران تطبيق المصادقة"
  And the instruction reads "امسح رمز QR هذا باستخدام Google Authenticator (أو أي تطبيق متوافق مع TOTP). يبقى السر نفسه — إعادة المسح لا تؤثر على رموز الاسترداد الحالية."
  And the secret hint reads "أو أدخل هذا السر يدويًا في التطبيق:"
  And the "Confirm the scan" heading reads "تأكيد المسح"
  And the field label reads "رمز التحقق" and the button reads "تحقّق"
  And the nav rail and card mirror to RTL

  When they enter a wrong code and click "تحقّق"
  Then the error alert reads "الرمز غير صحيح. انتظر الرمز التالي وحاول مرة أخرى."
  When they enter a live Get-Totp code and click "تحقّق"
  Then the success alert reads "تم تأكيد المسح. تطبيق المصادقة الآن مقترن بهذا الحساب."
  # Note: the SimfCodeField input itself stays left-to-right (digits entered LTR)
  # by component design, even under dir="rtl".
```

---

## Implementation notes

- **Lower-layer API coverage already exists.** `tests/SIMF.Api.Tests/TotpEnrolmentTests.cs`
  covers this surface at the HTTP layer without a browser:
  - `Pairing_returns_404_when_the_account_has_no_active_secret` — backs **E2E-TPP-003** (the 404 → "no secret" path).
  - `Pairing_returns_the_same_QR_for_the_enrolled_users_active_secret` — backs
    **E2E-TPP-002** and **E2E-TPP-008** (idempotent read; same `Secret`, an
    `otpauth://totp/` URI prefix and a `<svg` body on each call).
  The `pairing/verify` `Valid:false` path (**E2E-TPP-006/007**) is exercised
  indirectly by the wider TOTP suite's confirm/verify cases; an explicit
  `pairing/verify` Valid-true / Valid-false pair is a candidate to add there.
- **Manual smoke is canonical today.** Until a Playwright project exists, the
  canonical run is a Chrome DevTools MCP session: sign in per the Auth setup,
  walk each scenario, and save screenshots into
  `docs/screenshots/cp-account-totp-pairing-{scenario}.png`. The `Get-Totp`
  PowerShell helper supplies live codes for E2E-TPP-001/002/008/010.
- **Convert to Playwright** when adopted: each Gherkin block maps to a
  `.feature` scenario under `tests/SIMF.E2E.Tests/` (project to be created)
  plus step definitions; the steps are deliberately tool-agnostic.
- **No audit assertion on Verify.** Unlike most CP actions, the D-102 verify is
  intentionally side-effect-free — assert the *absence* of a new audit row
  (E2E-TPP-001), do not look for one.

---

### E2E-TPP-011: the pairing QR costs a code

The page used to render the QR on load, which meant the account's TOTP secret was
readable in plaintext by anything holding a valid access token - so a stolen token
could be turned into an indefinite second factor. The reveal is now an action, and
the API refuses it without a current code from the authenticator the admin already
holds.

This takes nothing away. The page never could serve an admin who has LOST their
authenticator: losing it means failing the second factor at sign-in and never
reaching a signed-in page. That case needs a reset, not this page.

```gherkin
Scenario: Opening the page shows no secret
  Given an enrolled Administrator signed in to the Control Panel
  When they open "/account/totp-pairing"
  Then no QR image is rendered
  And the page body contains no base32 secret
  And a code field and a "Show pairing QR" button are shown

Scenario: A wrong code is refused and reveals nothing
  Given an enrolled Administrator on "/account/totp-pairing"
  When they submit the code "000000"
  Then the response is 400 with error code "AUTH_TOTP_INVALID"
  And the response body does not contain the account's secret
  And the QR is still not rendered

Scenario: A live code reveals the same secret, unrotated
  Given an enrolled Administrator on "/account/totp-pairing"
  When they submit a current code from their authenticator
  Then the QR and the base32 secret are rendered
  And the secret equals the one their authenticator already holds
  And their recovery-code count is unchanged
```

**Evidence (API layer):** `tests/SIMF.Api.Tests/TotpEnrolmentTests.cs`:
`Pairing_returns_the_same_QR_for_the_enrolled_users_active_secret`,
`Pairing_without_a_valid_code_does_not_hand_over_the_secret`,
`Pairing_returns_404_when_the_account_has_no_active_secret`.

---

_Last reviewed:_ 2026-08-19 by Claude: the pairing QR moved behind a code
challenge (`POST /app/auth/totp/pairing`, was a bodiless GET), so the account's
TOTP secret is no longer readable by a bearer token alone. E2E-TPP-011 added;
001, 004 and 009 rewritten for the reveal-on-demand page.
Prior: 2026-06-02 by Claude (E2E catalogue rebuild).
