# Mandatory two-factor enrolment (CP) — `/login/enrol-2fa`

| | |
|--|--|
| **Route** | `/login/enrol-2fa` |
| **Audience** | Mid-sign-in Control Panel admin with **no** authenticator secret |
| **Auth** | `[AllowAnonymous]` — no token and no cookie exists yet. The credential is the single-use, 15-minute, attempt-capped enrolment ticket the password step returned. |
| **Status** | ✅ Real |
| **Backend** | `POST /api/v1/app/auth/totp/enrolment/start` → QR + base32 key · `POST /api/v1/app/auth/totp/enrolment/complete` → session + recovery codes (both `AllowAnonymous`, both on the reviewed allow-list in `BusinessFlow13PermissionMatrixTests`) |
| **Source** | [`TwoFactorEnrolment.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Auth/TwoFactorEnrolment.razor) · [`.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Auth/TwoFactorEnrolment.razor.cs) |
| **E2E** | [`cp-2fa-enrolment.md`](../../tests/e2e/cp-2fa-enrolment.md) — `E2E-TFE-001..013` |
| **Last reviewed** | 2026-07-30 |

## 1. Purpose

Closes defect **#2** under owner decision **Q1 (2026-07-30) — enrolment-first**.

`SignInService` used to issue a full access token on the password alone whenever
`TwoFactorEnabled` was false, with no branch on the audience — so a Control Panel
admin held an admin session after one factor, and the production super-admin is
recorded as 2FA-off. The fix withholds the token; this page is how the admin gets
in anyway, so nobody is locked out by the fix.

## 2. Not the same page as `/account/totp-pairing`

| | `/login/enrol-2fa` (this page) | `/account/totp-pairing` (D-096) |
|--|--|--|
| When | during sign-in, before any token | after sign-in, inside the shell |
| Secret | **creates** a new one | **re-renders** the existing one |
| Auth | enrolment ticket | authentication cookie |
| Ends with | a session + 10 recovery codes | nothing changed |

`/account/totp-pairing` explicitly cannot create a secret, which is why the
existing page could not close #2.

## 3. Flow

1. `/login` password step returns `SignInResponse.TwoFactorEnrolmentToken`
   (everything else null). `SignIn.razor.cs` stashes it on
   `SimfAuthSession.PendingEnrolmentToken` and routes here.
2. `OnInitializedAsync` exchanges the ticket at `/totp/enrolment/start` and
   renders the QR + the base32 key.
3. The admin scans, enters the six-digit code, and submits to
   `/totp/enrolment/complete`. The staged secret becomes active,
   `TwoFactorEnabled` flips on, and the withheld session is issued —
   stamped `amr=mfa`, because the code just verified IS the second factor.
4. The 10 one-time recovery codes are shown (D-040) and must be acknowledged.
5. "Continue" stashes the tokens in `SignInTicketStore` and hands off to
   `/auth/complete`, the same cookie-issuing endpoint the TOTP step uses.

## 4. Controls

| Control | Notes |
|---------|-------|
| QR code | server-rendered SVG (`TotpEnrollmentService.BuildQrSvg`), `.simf-qr` |
| Base32 key | `.simf-totp-secret`, for admins who cannot scan |
| Verification code | `SimfCodeField`, six digits, validated client-side before any request |
| Confirm and sign in | `SimfButton`, disabled while in flight |
| Back to sign in | returns to `/login`; the ticket is simply abandoned |
| Recovery codes | shown once, ordered list, with an explicit acknowledge action |
| Language / theme | `SimfLanguageSwitch` + `SimfThemeToggle`, as on every auth page |

## 5. Configuration

`IdentityLifecycle:RequireControlPanelTwoFactorEnrolment` — default **true**
(`IdentityLifecycleOptions`, mirrored in `appsettings.json`). When false the
Control Panel reverts to the pre-#2 behaviour AND `AdminAccountService` stops
forcing `TwoFactorEnabled` at creation, because forcing it without this page is a
lockout. The general integration suite pins it off; the production posture is
proved by `ControlPanelTwoFactorApiFactory`.

## 6. Permission

None, and deliberately so: this runs before a token exists, so it belongs to the
anonymous authentication surface. It is not a nav item and carries no
`[RequirePermission]`.

## 7. Edge cases

- **No ticket in the circuit** (direct navigation, refresh after completion) →
  redirect to `/login`; nothing renders.
- **Wrong code** → `TOTP_ENROLMENT_CODE_INVALID` 400, bilingual alert, no session.
  The attempt also counts against the account lockout budget and the ticket's own
  five-attempt budget.
- **Ticket expired (>15 min), consumed, or unknown** →
  `AUTH_TWO_FACTOR_ENROLMENT_REQUIRED` 400 with "sign in again to start over".
- **Account locked or forced-password-change** between the password step and
  enrolment → the ticket is refused; those gates are re-run on every enrolment call.
- **Reload of the QR** → a new secret is staged. Harmless: only the confirmed one
  becomes active.

## 8. Use cases

UC-AUTH-2FA-ENROL-MANDATORY, UC-AUTH-2FA-ENROL-RECOVERY-CODES.

## 9. E2E

| Scenario | ID |
|----------|----|
| Golden path — enrol and reach the shell | E2E-TFE-001 |
| Password alone never yields a session | E2E-TFE-002 |
| Already-enrolled admin never sees this page | E2E-TFE-003 |
| Wrong code rejected, no session | E2E-TFE-004 |
| Direct navigation bounces to `/login` | E2E-TFE-005 |
| Spent ticket cannot be replayed | E2E-TFE-006 |
| Recovery codes shown once | E2E-TFE-007 |
| Code-field validation | E2E-TFE-008 |
| Expired ticket | E2E-TFE-009 |
| Server 500 | E2E-TFE-010 |
| RTL render | E2E-TFE-011 |
| Newly provisioned admin's first sign-in | E2E-TFE-012 |
| App audience unaffected | E2E-TFE-013 |

_Last reviewed:_ 2026-07-30 by Track A (auth & security), fix-all round.
