# Test-Case Sheet — `Sign In` (app screen #3)

> Author sections 1–4 once; the tester fills the **bold** columns during the run.
> **A new build is a new run** — copy this sheet rather than overwriting a result.

---

## 1. Identification and run context

| | | | |
|---|---|---|---|
| **Screen name** | تسجيل الدخول · Sign In | **Doc id** | `TC-MOB-SGI` |
| **Route / screen id** | `/sign-in` (`RouteNames.signIn`) — app screen **#3** | **Surface** | Mobile app (Flutter) |
| **APIs under test** | `POST /app/auth/sign-in` · `GET /app/users/me` (privilege hydration) · `POST /app/auth/refresh` · device-key sign-in (biometric) | **Audience** | Guest entry; promotes to Visitor / Moderator / Staff on success |
| **Build / version under test** | _(fill)_ | **Environment** | Local / Test / Staging / Production |
| **Device + OS** | _(fill — include one Huawei / no-GMS handset and one tablet)_ | **Language run** | Arabic (RTL) / English (LTR) |
| **Tester name** | _(fill)_ | **Test date** | _(fill)_ |
| **Reviewed by (QA Lead)** | _(fill)_ | **Review date** | _(fill)_ |
| **Reference docs** | [pages/mobile/sign-in/](../../pages/mobile/sign-in/README.md) · [e2e/mobile-sign-in.md](../../tests/e2e/mobile-sign-in.md) `E2E-MOB003-001…021` · [FDS-001](../../SIMF-FDS-001-Authentication-and-Login.md) `T-07…T-11`, `T-19` · Figma `168:2800` | | |

### Source of the expected values on this sheet

| Value | Source |
|---|---|
| Email field cap **50**; password field cap **128** (matches `PasswordPolicy.MaxLength`) | `src/Mobile/simf_app/lib/features/account/widgets/account_form_field.dart` |
| Remember-me default: **ON** on mobile, **OFF** on web (`!kIsWeb`) | `sign_in_screen.dart` |
| `AutofillGroup` + `finishAutofillContext(shouldSave: _rememberMe)` | `sign_in_screen.dart` |
| Routes out: forgot-password · sign-up form · badge sign-in · guest mode · verify-OTP | `sign_in_screen.dart` |
| Account lockout **5 failed sign-ins → 15 minutes** | `src/Backend/SIMF.Infrastructure/DependencyInjection.cs` |
| Rate limits **20 req / 60 s / IP** (`auth`) and **5 req / 60 s / email** (`auth-email`) | `RateLimitOptions.cs` + `Program.cs` |
| Sign-in OTP: lifetime **10 min**, ticket **5 min**, **5** second-factor attempts, **5** OTP requests per hour, resend cooldown **120 s** | `src/Backend/SIMF.Application/IdentityAccess/SignInService.cs` |
| Privilege comes from `GET /app/users/me`, never from the token payload | `E2E-MOB003-004` |

> **Password cap is 128, not 32.** `E2E-MOB003-006` previously stated 32; that
> line was **corrected on 2026-08-03** to match `AccountPasswordField(maxLength: 128)`
> and `PasswordPolicy.MaxLength`. The old 32 cap locked out users with a valid
> long passphrase — `TC-MOB-SGI-B06` is the regression guard for that.

## 2. Pre-conditions, fixtures and test data

| Item | Value / how to obtain |
|---|---|
| **FX-1** Approved visitor, no 2FA | signs straight through to Home |
| **FX-2** Approved visitor with 2FA on | routes to the email-OTP screen |
| **FX-3** `Registered` (email not verified) | |
| **FX-4** `PendingApproval` | |
| **FX-5** `Rejected` | |
| **FX-6** `Disabled` | |
| **FX-7** Approved visitor with `profileComplete = false` | must route to the profile-completion screen |
| **FX-8** Moderator account · **FX-9** Staff account | for the privilege-hydration rows |
| **FX-10** Account with a **long** password (60+ characters) | for `TC-MOB-SGI-B06` |
| Codes | OTP read from `SIMF_Identity.AccountCodes` or the test mailbox **at run time**. |
| API tool | REST client for §D. |
| Devices | Biometric-capable device **and** a device with no enrolled biometric. |
| Cleanup | Fixtures tagged `QA-`; added to the cleanup register. |

> **No literal secret appears in this document.** Never record a password or an
> OTP here or in an evidence file.

## 3. Inherited common cases

| Block | Applies | **Status** | **Notes** |
|---|---|---|---|
| CB-01 Render, console and overflow | yes | | |
| CB-02 Arabic RTL and English LTR | yes | | |
| CB-03 Loading, empty and error states | partial — loading only | | |
| CB-04 Auth gate and account state | yes — this screen **is** the gate | | |
| CB-05 Session expiry and token refresh | yes — post sign-in | | |
| CB-06 Network failure and retry | yes | | |
| CB-07 Server 500 and malformed payload | yes | | |
| CB-08 Accessibility baseline | yes | | |
| CB-09 Pull-to-refresh | **N-A** — form | | |
| CB-10 Audit trail | yes | | |

## 4. Test cases

**Status:** `PASS` · `FAIL` · `BLOCKED` · `N-A` · `NOT-RUN` · `PROD-SKIP`

### A. Render and layout

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SGI-A01` | Screen chrome | P1 | Launch the app signed out and reach sign-in. | Navy + beige-card design; **no app bar**; back chevron top-left; **globe language toggle** top-right; title; email and password fields; remember-me checkbox and forgot-password link on one row; gold sign-in CTA; "create account" foot; **Face-ID** button; underlined **guest** link; badge sign-in entry. | Figma `168:2800` | | | | |
| `TC-MOB-SGI-A02` | CTA gating | P1 | Fill the fields one at a time. | The sign-in CTA is disabled until both fields have content, and disables again if either is cleared. | code `_canSubmit` | | | | |
| `TC-MOB-SGI-A03` | Remember-me default | P0 | Open the screen on a **phone**, then on the **web** build if in scope. | Checked by default on mobile; **unchecked** by default on web (shared-browser storage). | code `_rememberMe = !kIsWeb` | | | | |
| `TC-MOB-SGI-A04` | Face-ID button always visible | P1 | Open on a device with **no** enrolled biometric. | The Face-ID button is still shown. Tapping it fails **silently and gracefully** with a localized message — it does not crash and does not block the password path. | `E2E-MOB003-012`; code `biometricUnavailable` / `biometricNotEnrolled` | | | | |
| `TC-MOB-SGI-A05` | Busy state | P1 | Submit on a throttled connection. | CTA busy; fields disabled; no second submit possible; other links inert. | code `_busy` | | | | |
| `TC-MOB-SGI-A06` | Tablet width | P1 | Open on a tablet in portrait. | Content fills the frame with no dead side gutters and no edge-to-edge stretch. | responsive rule §13.7 | | | | |
| `TC-MOB-SGI-A07` | Keyboard | P1 | Focus each field on a small phone. | Both fields clear the keyboard; the CTA stays reachable; nothing is clipped. | CB-01 | | | | |

### B. Field validation (client)

| ID | Field | Pri | Test data | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SGI-B01` | Email — empty | P0 | _(empty)_ | Inline **required** error; **no** sign-in request. | `_validateEmail` | | | | |
| `TC-MOB-SGI-B02` | Email — malformed | P0 | `admin@` · `@example.sa` · `admin example.sa` · `admin@example` | Inline **"Invalid email" / "بريد إلكتروني غير صالح"** for each, and the sign-in round-trip is **blocked** — no request fires. | `E2E-MOB003-018` | | | | |
| `TC-MOB-SGI-B03` | Email — length boundary | P1 | 49 / 50 / attempt 51 characters | 49 and 50 type fully; the **51st character cannot be entered**. | `AccountEmailField maxLength = 50` | | | | |
| `TC-MOB-SGI-B04` | Email — client cap vs server cap | P1 | A valid address longer than 50 characters | Cannot be entered, although the server accepts up to 256. Raise a defect if a real user address is affected. | client 50 vs server 256 | | | | |
| `TC-MOB-SGI-B05` | Password — empty | P0 | _(empty)_ | Inline **required** error; **no** request. | `_validatePassword` | | | | |
| `TC-MOB-SGI-B06` | Password — long passphrase | P0 | **FX-10**, a 60+ character password | The password **can be typed in full** (the field caps at 128, matching the server policy) **and signs in successfully**. A user who set a long passphrase at reset must be able to sign in here. | `AccountPasswordField maxLength = 128`; `PasswordPolicy.MaxLength = 128` | | | | |
| `TC-MOB-SGI-B07` | Password — length boundary | P1 | 127 / 128 / attempt 129 characters | 127 and 128 type fully; the **129th cannot be entered**. | `maxLength: 128` | | | | |
| `TC-MOB-SGI-B08` | Password — masked | P0 | Type a password. | Masked by default; revealing is an explicit user action. | A7-2 | | | | |
| `TC-MOB-SGI-B09` | Password — no client-side policy check | P1 | Enter a password that violates the policy (e.g. 4 characters). | The client does **not** run the new-password policy here — sign-in only requires a non-empty value, and the server decides. Confirm the failure message is the generic credentials error, **not** a policy hint that would leak what the stored password looks like. | `_validatePassword`; A7 | | | | |
| `TC-MOB-SGI-B10` | Email direction in Arabic | P1 | Switch to Arabic and type an address. | The address renders **left-to-right** inside the RTL layout. | `E2E-MOB003-011` | | | | |

### C. Functional and business rules

| ID | Action | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SGI-C01` | Golden path (no 2FA) | P0 | Sign in as **FX-1**. | One `POST /app/auth/sign-in`; tokens stored; the app hydrates the real role from `GET /app/users/me`; routes to **Home** as a **Visitor** (not Guest); the email is stored for next time. | `E2E-MOB003-001` | | | | |
| `TC-MOB-SGI-C02` | Wrong password | P0 | Sign in as **FX-1** with a wrong password. | `AUTH_INVALID_CREDENTIALS`; an inline **bilingual** error; the **password field is cleared** and the **email is kept**; the app stays on sign-in; no token is issued. | `E2E-MOB003-002` | | | | |
| `TC-MOB-SGI-C03` | 2FA path | P0 | Sign in as **FX-2**. | The response carries `mfaRequired = true` with an OTP ticket and **no** tokens; the app routes to the **email-OTP** screen; entering the emailed code issues tokens. | `E2E-MOB003-003` | | | | |
| `TC-MOB-SGI-C04` | Privilege from `/users/me`, not the token | P0 | Sign in as **FX-8** (Moderator) and **FX-9** (Staff). | The app reads `appRole` from `GET /app/users/me`. Each lands with the correct privilege — never silently defaulted to Guest, and never trusting a role claim in the token payload. | `E2E-MOB003-004` | | | | |
| `TC-MOB-SGI-C05` | Profile-completion routing | P0 | Sign in as **FX-7**, then as **FX-1**. | The **server-computed** `profileComplete` flag decides: `false` → the visitor profile-completion screen; `true` → Home. The same rule applies on the password path **and** the OTP path. | `E2E-MOB003-013` (D-374) | | | | |
| `TC-MOB-SGI-C06` | Email pre-fill | P1 | Sign in successfully, sign out, reopen sign-in. | The email field is pre-filled with the last successfully used address; focus goes to the password. | `E2E-MOB003-005` | | | | |
| `TC-MOB-SGI-C07` | Remember-me OFF — no stored email | P0 | Uncheck remember-me, sign in successfully, sign out, reopen. | The email is **not** pre-filled, and any previously stored address is cleared. The OS autofill context is discarded (`shouldSave: false`). | `E2E-MOB003-015`, `E2E-MOB003-018` | | | | |
| `TC-MOB-SGI-C08` | Remember-me OFF — session is memory-only | P0 | Sign in with remember-me unchecked, then **force-quit and relaunch** the app. | The session works for that run, but nothing durable is written — after the restart the user lands **signed out**. | `E2E-MOB003-015` | | | | |
| `TC-MOB-SGI-C09` | Remember-me ON — session survives restart | P0 | Sign in with remember-me checked, force-quit, relaunch. | The user is still signed in. | `E2E-MOB003-015` | | | | |
| `TC-MOB-SGI-C10` | Language toggle | P1 | Tap the globe in Arabic, then again. | Switches to English and **persists** the preference; tapping again returns to Arabic; the choice survives an app restart. | `E2E-MOB003-016` | | | | |
| `TC-MOB-SGI-C11` | Guest entry | P1 | Tap the underlined guest link. | The guest-mode landing opens; "continue as guest" enters public Home with **no token**; guest content is browsable; account-only actions still gate to sign-in. | `E2E-MOB003-014` | | | | |
| `TC-MOB-SGI-C12` | Create-account link | P1 | Tap "create account". | Opens the sign-up form. Back returns to sign-in with the typed email preserved. | code `pushNamed(signUpForm)` | | | | |
| `TC-MOB-SGI-C13` | Forgot-password link | P0 | Tap "forgot password". | Opens the forgot-password screen. See [forgot-password.md](forgot-password.md). | code | | | | |
| `TC-MOB-SGI-C14` | Badge sign-in entry | P1 | Tap the badge entry. | Opens the badge sign-in screen. | code `pushNamed(badgeSignIn)` | | | | |
| `TC-MOB-SGI-C15` | Biometric re-open | P0 | Enrol a device key, sign out, tap Face-ID, authenticate. | The app signs the server challenge and mints fresh tokens **with no typed password**. A biometric sign-in always persists the session. | `E2E-MOB003-012` | | | | |
| `TC-MOB-SGI-C16` | Post-sign-in Face-ID nudge | P1 | Sign in on a biometric-capable device where Face-ID is **not** yet enabled — once via password, once via the OTP path. | A notification-style prompt with an **Enable** action appears after **both** paths. Tapping Enable routes to the **emailed-OTP step-up** screen, not a one-tap enrol. It still routes correctly after the app has moved to Home. | `E2E-MOB003-017` | | | | |
| `TC-MOB-SGI-C17` | Nudge stays silent when it should | P1 | Sign in with Face-ID already enabled; then on a device with no usable biometric. | **No** nudge in either case. | `E2E-MOB003-017` | | | | |
| `TC-MOB-SGI-C18` | Double submit | P0 | Tap the CTA twice rapidly. | One request only; one session; the failed-attempt counter moves by at most 1. | CB-06.5 | | | | |
| `TC-MOB-SGI-C19` | OS autofill saves the final value | P1 | Start sign-up, mistype the email, correct it, verify, return to sign-in. | The field pre-fills with the **corrected** address and the OS password manager offers the corrected credentials — not the first-typed guess. | `E2E-MOB003-018` (D-742) | | | | |

### D. Server-side and NCA security

> **Run every row here against the API directly**, not only through the app.

| ID | Control | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SGI-D01` | **Generic credentials error** | P0 | Sign in with (a) an unknown email, (b) a known email and a wrong password. | **Identical** generic `AUTH_INVALID_CREDENTIALS` in both cases. Nothing reveals whether the account exists. | A7, `T-11` | | | | |
| `TC-MOB-SGI-D02` | Enumeration — timing | P1 | Time 10 requests for each of D01's cases. | No consistent exploitable timing difference. Record both medians. | A7 | | | | |
| `TC-MOB-SGI-D03` | **Account lockout** | P0 | Submit **5** wrong passwords for one account, then a **6th** — then try the **correct** password. | The 6th is refused with a lockout response carrying the unlock time. The **correct** password is also refused while locked. Access returns after **15 minutes**. | `MaxFailedAccessAttempts = 5`, `DefaultLockoutTimeSpan = 15 min`; A7-8, `T-19` | | | | |
| `TC-MOB-SGI-D04` | Lockout counter resets on success | P1 | Submit 3 wrong passwords, then the correct one, then 3 more wrong. | The successful sign-in resets the counter — 3 + 3 does not lock the account. | A7-8 | | | | |
| `TC-MOB-SGI-D05` | Per-**IP** rate limit | P0 | 21 sign-in requests from one IP within 60 seconds, varying the email. | The 21st returns **429**. | `auth` = 20 / 60 s | | | | |
| `TC-MOB-SGI-D06` | Per-**email** rate limit | P0 | 6 sign-in requests for one address within 60 seconds. | The 6th returns **429**. | `auth-email` = 5 / 60 s | | | | |
| `TC-MOB-SGI-D07` | No IP lockout tier | P1 | After tripping D05, continue from the same IP after the window. | Requests resume — there is **no** long IP lockout, only the 60-second window. This is a **known accepted gap** against the NCA control; record the observed behaviour rather than raising it as new. | A7-8 (⚠ partial) | | | | |
| `TC-MOB-SGI-D08` | `Registered` account is refused | P0 | Sign in as **FX-3**. | Refused with the email-not-verified outcome. The user is routed to verification, **not** into app content. | `T-08`, CB-04.4 | | | | |
| `TC-MOB-SGI-D09` | `PendingApproval` account is contained | P0 | Sign in as **FX-4**. | The session is limited to the pending/status surface. **No** protected content (sessions, seat, badge, contacts) is reachable. | `T-09`, CB-04.4 | | | | |
| `TC-MOB-SGI-D10` | `Rejected` / `Disabled` refused | P0 | Sign in as **FX-5**, then **FX-6**. | Each lands on its own state surface and reaches no content. A disabled account cannot obtain tokens. | `T-10`, CB-04.4 | | | | |
| `TC-MOB-SGI-D11` | Token payload carries no role | P0 | Decode the issued access token. | The payload carries identity only. The **role is not trusted from the token** — the client hydrates it from `/app/users/me`. Forging a role claim must gain nothing. | `E2E-MOB003-004`; A1 | | | | |
| `TC-MOB-SGI-D12` | Access-token lifetime | P0 | Hold a session past **5 minutes**. | The access token expires at 5 minutes and the client refreshes silently, single-flight — no burst of duplicate refresh calls. | CB-05.1 | | | | |
| `TC-MOB-SGI-D13` | Absolute session cap | P1 | Keep a session continuously active for **24 hours**. | The session ends at the cap; activity does **not** slide it; the user must sign in again. | CB-05.2 | | | | |
| `TC-MOB-SGI-D14` | Refresh-token rotation and reuse | P0 | Refresh once, then replay the **old** refresh token. | The old token is refused and the reuse is logged. | A7 | | | | |
| `TC-MOB-SGI-D15` | Sign-out revokes everything | P0 | Sign out, then replay the captured refresh token. | Refused. Every refresh token for the account is revoked and the security stamp rolls. | CB-05.4 | | | | |
| `TC-MOB-SGI-D16` | Server re-validates input | P0 | POST with an empty email, a malformed email, a 300-character email, a null password, and a non-string JSON type. | Each is rejected by the **server** with the standard envelope. | A3 | | | | |
| `TC-MOB-SGI-D17` | Second-factor attempt cap | P0 | On the 2FA path, submit **6** wrong OTP codes against one ticket. | Attempt 6 is refused. | `MaxSecondFactorAttempts = 5`; A7-8 | | | | |
| `TC-MOB-SGI-D18` | OTP ticket lifetime | P0 | Reach the OTP screen, wait past **5 minutes**, then submit a valid code. | The ticket is expired and the code is refused; the user must restart sign-in. | `TicketLifetime = 5 min` | | | | |
| `TC-MOB-SGI-D19` | No credential in a URL or log | P0 | Inspect navigation URLs, deep links and the device log across a full run. | No password, OTP, ticket or token appears anywhere. | A7-36, A9-9 | | | | |
| `TC-MOB-SGI-D20` | Transport | P0 | Capture the request. | TLS only. Credentials are never sent over plain HTTP. | A5 | | | | |
| `TC-MOB-SGI-D21` | Nothing sensitive survives backgrounding | P1 | Type a password, background the app, inspect the recents thumbnail. | The password is not visible in the recents snapshot and is not retained in unencrypted local storage. | A11 | | | | |
| `TC-MOB-SGI-D22` | Last-account-use notice | P1 | Sign in, then look for a "last signed in at …" notice. | The backend records and returns the previous sign-in. **Known follow-up:** surfacing it in the app UI was an outstanding client task — record whether it is now visible and raise it if not. | A7-31 | | | | |

### E. Error handling and resilience

| ID | Condition | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SGI-E01` | Server error language | P0 | Trigger C02 in Arabic, then in English. | The server's message renders in the app's current language — the envelope carries both and the data layer picks by locale. | `E2E-MOB003-019` | | | | |
| `TC-MOB-SGI-E02` | 429 rate limited | P0 | Trip D06, sign in through the app. | Localized "too many attempts" inline; fields preserved; no navigation; no token. | — | | | | |
| `TC-MOB-SGI-E03` | Lockout message | P0 | Trip D03 through the app. | A localized lockout message that tells the user when they can retry — not a bare generic failure. | `T-19` | | | | |
| `TC-MOB-SGI-E04` | Network / 500 | P1 | Force a 500 and a network failure. | A non-blocking localized error; **fields are preserved**; **no token mutation** — an existing session is not corrupted or cleared by a failed sign-in. | `E2E-MOB003-010` | | | | |
| `TC-MOB-SGI-E05` | Offline sign-in | P0 | Network off, submit valid credentials. | A failure is surfaced. The app **must not** navigate to Home and must not imply success. | CB-06.2 | | | | |
| `TC-MOB-SGI-E06` | Recovery | P1 | Restore the network and retry. | Signs in normally. | CB-06.3 | | | | |
| `TC-MOB-SGI-E07` | Navigate away mid-request | P1 | Submit then immediately background or tap back. | No crash; no error painted on a disposed screen; state stays consistent. | code `if (!mounted) return` | | | | |
| `TC-MOB-SGI-E08` | Biometric failure paths | P1 | Cancel the OS biometric prompt; then fail it repeatedly. | Cancelling returns to the form cleanly. Repeated failure surfaces a localized message and the password path still works. | code `biometricUnavailable` / `biometricNotEnrolled` | | | | |

### F. Accessibility and localisation

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SGI-F01` | Arabic RTL | P0 | Run the whole sheet in Arabic. | Fields, errors and links mirror; the email stays LTR; the back chevron and globe sit correctly; no double-mirrored icon. | `E2E-MOB003-011` | | | | |
| `TC-MOB-SGI-F02` | No hardcoded string | P0 | Compare every visible string in both languages. | Title, both labels, both validation errors, remember-me, forgot-password, CTA, create-account, guest link, badge entry and every error are translated. | CB-02.3 | | | | |
| `TC-MOB-SGI-F03` | Accessible names | P1 | Screen reader on; traverse the screen. | The **email box**, the **password box** and the **remember-me checkbox** each expose their visible caption as their own accessible name — none announces as a bare "edit box" or "checkbox". | `E2E-MOB003-021` (BUG-012) | | | | |
| `TC-MOB-SGI-F04` | Face-ID button label | P1 | Screen reader on; focus the Face-ID button. | It announces a meaningful label, not just an icon. | CB-08.1 | | | | |
| `TC-MOB-SGI-F05` | Text scaling | P2 | Largest supported font size. | Nothing clipped or overlapping; the CTA and links stay reachable. | CB-08.5 | | | | |
| `TC-MOB-SGI-F06` | Errors are not colour-only | P1 | Trigger B01 and C02. | Failure is conveyed by text, not only by a red border. | CB-08.4 | | | | |

### G. Data integrity and audit

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SGI-G01` | Successful sign-in is audited | P1 | Complete C01; inspect the audit trail. | A `SignIn.Succeeded` row records the actor, timestamp and source IP. | A9-7 | | | | |
| `TC-MOB-SGI-G02` | Failed sign-in is audited | P1 | Complete C02 and D03; inspect the audit trail. | Each failure is recorded, including the attempt that triggered the lockout. | A9-15, A1-12 | | | | |
| `TC-MOB-SGI-G03` | Second factor is audited | P1 | Complete C03. | The second-factor issue and verification are recorded. | A9-7 | | | | |
| `TC-MOB-SGI-G04` | Sign-out is audited | P1 | Sign out. | A `SignOut.Succeeded` row is written. | A9-7 | | | | |
| `TC-MOB-SGI-G05` | Audit content is safe | P0 | Inspect the rows from G01–G04. | No password, OTP, ticket or token is stored. | A9-9 | | | | |
| `TC-MOB-SGI-G06` | No secret in the client log | P0 | Capture the device log across a full run. | No password, OTP or token is printed. | A9-9 | | | | |

### H. Customer acceptance and Definition of Done

| ID | Criterion | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SGI-H01` | Requirement satisfied | P0 | Run C01 → C05. | *"The system shall authenticate a user by email and password."* is met, and no Nafath or Face-ID **sign-in-in-place-of-password** is offered — the biometric path is a re-open of an enrolled device key, not an alternative identity provider. | **FR-104** | | | | |
| `TC-MOB-SGI-H02` | Account-state gating | P0 | Run D08 → D10. | Every non-approved state is contained on its own surface. | **NFR-01**, CB-04.4 | | | | |
| `TC-MOB-SGI-H03` | Brute-force protection | P0 | Run D03 → D07, D17. | Lockout, per-email and per-IP limits and the second-factor cap all hold. | **NFR-01**, A7-8 | | | | |
| `TC-MOB-SGI-H04` | Design parity | P1 | Compare the live render against Figma `168:2800`. | Strings, typography, colour, spacing and radii match. Record any deliberate deviation. | DoD-Gate-4 | | | | |
| `TC-MOB-SGI-H05` | Live-render gate | P0 | Capture a full screenshot, the console/device log and the network list for a complete run. | Screenshot captured; **zero** console errors; **zero** failed assets; no horizontal overflow. | DoD-Gate-4 | | | | |
| `TC-MOB-SGI-H06` | Both languages completed | P0 | Confirm two runs recorded. | Arabic (RTL) and English (LTR) both complete. | SIMF-TST-001 §11 | | | | |
| `TC-MOB-SGI-H07` | Catalogue alignment | P1 | Cross-check against `E2E-MOB003-001…021`. | Every scenario in the catalogue is covered here and none contradicts it. (`E2E-MOB003-006`'s password cap was corrected from 32 to 128 on 2026-08-03.) | DoD-SES-7 | | | | |
| `TC-MOB-SGI-H08` | Device matrix | P0 | Run C01, C15 and C16 on a standard Android phone, a **Huawei / no-GMS** handset and a tablet. | Sign-in works on every device class. On the no-GMS handset the biometric and any camera-adjacent path degrade gracefully rather than failing hard. | SIMF-MAA-001 | | | | |

## 5. Execution summary

| | Count |
|---|---|
| Total cases (75 authored + 9 applicable inherited blocks) | |
| PASS | |
| FAIL | |
| BLOCKED | |
| N-A | |
| NOT-RUN | |
| **Pass rate** (PASS / (PASS+FAIL)) | |

| Exit criterion | Met? | Note |
|---|---|---|
| Every **P0** case is PASS | | |
| No open **high-severity** defect | | |
| Both language runs completed | | |
| Device matrix (phone / no-GMS / tablet) completed | | |
| Evidence captured for every PASS and FAIL | | |
| Locked-out fixtures released; cleanup register drained | | |

## 6. Defect, fix and re-test ledger

| Case ID | Defect ID | Severity | Reported by / date-time | Expected vs actual | Root cause | **Developer** | **Fix ref (commit / PR)** | **Fixed date + time** | **Re-test result** | **Re-test date + time** | **Re-tested by** | **State** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| | | Critical / High / Medium / Low | | | | | | | PASS / FAIL | | | New / Assigned / Fixed / Re-test / Closed / Reopened |

**After any fix, re-run this screen's whole P0 set** and record the regression
outcome here. Sign-in is a **shared foundation** — a change here can break
sign-up, badge sign-in, biometric re-open and every role-gated route, so the
regression pass must also cover [reset-password.md](reset-password.md),
[forgot-password.md](forgot-password.md) and the verify-OTP sheet.

## 7. Sign-off

| Role | Name | Date | Verdict |
|---|---|---|---|
| Tester | | | Accept / Reject |
| QA Lead | | | Accept / Reject |
| Developer | | | Fixes complete: yes / no |
| Owner | | | Accepted for release: yes / no |

## 8. Revision history

| Version | Date | Author | Change |
|---|---|---|---|
| 1.0 | 2026-08-01 | SIMF Team | First issue. Grounded in `sign_in_screen.dart`, `account_form_field.dart`, `SignInService.cs`, `DependencyInjection.cs`, `RateLimitOptions.cs` and `e2e/mobile-sign-in.md`. |

---

_Authored:_ 2026-08-01 · _Last reviewed:_ 2026-08-01
