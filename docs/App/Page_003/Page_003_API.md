# Page 003 — API (تسجيل الدخول · Sign in)

Authoritative backend contract for this page. Inherits the `ApiResult<T>` envelope,
headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001 §3–§4. Session,
biometric and OTP rules are in [Page_003_Logic.md](Page_003_Logic.md).

> **Status:** all endpoints below are **built** (maintainer-verified). The device-key
> (biometric) endpoints exist under `/app/auth/device-key`.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split, D-247) —
> so the routes below resolve to `POST /api/v1/app/auth/sign-in`, etc.
>
> **Caps note (Logic D2):** the UI caps email at 50 and password at 32, but the **server
> contract stays email ≤256 / password ≤128**. The client must not assume 50/32 server-side.

## E1 — `POST /app/auth/sign-in`
| | |
|---|---|
| Route | `POST /api/v1/app/auth/sign-in` |
| Access | **Anonymous** (sign-in entry point) |
| App privilege | Guest |
| Returns | `ApiResult<SignInResponse>` |

```jsonc
// Request — SignInRequest
{
  "email":    "string",   // server ≤256
  "password": "string"    // server ≤128
}
```
> The app sends only `email` + `password`. `SignInRequest` also carries an
> optional `audience` (`Web` | `Cp` | `App`) that defaults to `Web` server-side
> when omitted; the app does not send it.

```jsonc
// Response — ApiResult<SignInResponse>.data
{
  "mfaRequired": false,           // true ⇒ a second factor is required before tokens are issued (Logic L-5)
  "mfaToken":    null,            // string when set — Control-Panel TOTP accounts only (the app ignores it)
  "otpToken":    null,            // string when set — visitor email-OTP; pass to verify-otp (E2)
  "tokens": {                     // null until sign-in is complete (mfaRequired = false)
    "accessToken":  "string",
    "refreshToken": "string",
    "tokenType":    "Bearer",
    "accessTokenExpiresInSeconds": 0,
    "user": { "id": "guid", "email": "string", "displayName": "string" }
  },
  "accountState": null,           // non-null ONLY when the account is NOT Approved (D-051):
                                  //   { state, rejectionReason, rejectionReasonArabic, stateChangedAt }
  "passwordChangeToken": null     // string on a forced CP password change (D-206); null for the app
}
```
| Error code | When |
|---|---|
| `AUTH_INVALID_CREDENTIALS` | Wrong email/password (401). |
| `AUTH_ACCOUNT_LOCKED` | Lockout after repeated failures (423). |
| `AUTH_EMAIL_NOT_VERIFIED` | Account exists but its email is not verified. |
| `AUTH_ACCOUNT_DISABLED` | Account disabled. |
| `AUTH_WRONG_SURFACE_WEB` / `AUTH_WRONG_SURFACE_CP` | Account not allowed on this sign-in surface (audience gate, P2). |
| `AUTH_PASSWORD_CHANGE_REQUIRED` | Forced password change pending (403) — CP accounts (D-206). |
| `VALIDATION_FAILED` | Missing/invalid email or password. |

> A not-yet-approved account is **not** an error: sign-in returns `200` with
> `accountState` populated (`Pending` / `Rejected`) and `tokens` still issued, and
> the app routes to the registration-status screen (Page 011).

## E2 — `POST /app/auth/verify-otp`  (2FA email-OTP branch — Logic D3/L-5)
| | |
|---|---|
| Route | `POST /api/v1/app/auth/verify-otp` |
| Access | **Anonymous** (carries the `otpToken` from E1) |
| Returns | `ApiResult<AuthTokens>` (the issued tokens) |

```jsonc
// Request — VerifyOtpRequest
{
  "otpToken": "string",  // the otpToken from the E1 sign-in response
  "code":     "string"   // the 6-digit code delivered by email
}
```
```jsonc
// Response — ApiResult<AuthTokens>.data
{
  "accessToken":  "string",
  "refreshToken": "string",
  "tokenType":    "Bearer",
  "accessTokenExpiresInSeconds": 0,
  "user": { "id": "guid", "email": "string", "displayName": "string" }
}
```
| Error code | When |
|---|---|
| `AUTH_OTP_TOKEN_INVALID` | The `otpToken` is unknown / malformed / already used. |
| `AUTH_OTP_INVALID` | Wrong code. |
| `AUTH_OTP_EXPIRED` | Code expired / consumed. |

## E3 — `POST /app/auth/refresh`  (silent session refresh — Logic L-1)
| | |
|---|---|
| Route | `POST /api/v1/app/auth/refresh` |
| Access | **Anonymous** (carries the refresh token) |
| Returns | `ApiResult<AuthTokens>` (rotated tokens) |

```jsonc
// Request — RefreshRequest
{ "refreshToken": "string" }
```
```jsonc
// Response — ApiResult<AuthTokens>.data  (same shape as E2)
{
  "accessToken":  "string",
  "refreshToken": "string",
  "tokenType":    "Bearer",
  "accessTokenExpiresInSeconds": 0,
  "user": { "id": "guid", "email": "string", "displayName": "string" }
}
```
| Error code | When |
|---|---|
| `AUTH_REFRESH_TOKEN_INVALID` | Refresh token unknown / revoked. |
| `AUTH_REFRESH_TOKEN_EXPIRED` | Refresh token past its lifetime. |

## E4 — `POST /app/auth/forgot-password`  (emails an OTP — Logic L-6)
| | |
|---|---|
| Route | `POST /api/v1/app/auth/forgot-password` |
| Access | **Anonymous** |
| Returns | `ApiResult<ForgotPasswordResponse>` — always success-shaped (no account enumeration) |

```jsonc
// Request — ForgotPasswordRequest
{ "email": "string" }
```
```jsonc
// Response — ApiResult<ForgotPasswordResponse>.data
{ "codeExpiresInSeconds": 0 }   // a constant lifetime, returned whether or not the account exists
```
Server emails a one-time code to the address if it exists. The constant
`codeExpiresInSeconds` never reveals whether the account exists; the app proceeds
to the reset step regardless.

## E5 — `POST /app/auth/reset-password`  (OTP + new password — Logic L-6)
| | |
|---|---|
| Route | `POST /api/v1/app/auth/reset-password` |
| Access | **Anonymous** (carries the reset code from E4) |
| Returns | `ApiResult<ResetPasswordResponse>` |

```jsonc
// Request — ResetPasswordRequest
{
  "email":           "string",
  "code":            "string",   // the reset code emailed by E4
  "newPassword":     "string",   // server ≤128
  "confirmPassword": "string"    // must equal newPassword
}
```
```jsonc
// Response — ApiResult<ResetPasswordResponse>.data
{ "passwordReset": true }
```
| Error code | When |
|---|---|
| `AUTH_RESET_CODE_INVALID` | Wrong reset code, or email/code mismatch. |
| `AUTH_RESET_CODE_EXPIRED` | Reset code expired. |
| `AUTH_PASSWORD_POLICY` / `VALIDATION_FAILED` | New password fails server policy, or `confirmPassword` mismatch. |

## E6 — Device-key (biometric) endpoints (backend D-172, Logic L-2)
| | |
|---|---|
| Routes | `POST /api/v1/app/auth/device-keys` (enrol) · `GET …/device-keys` (list mine) · `POST …/device-keys/{id}/challenge` (issue challenge) · `POST /api/v1/app/auth/sign-in-with-device-key` (verify + mint) · `DELETE …/device-keys/{id}` (revoke mine) |
| Access | enrol / list / revoke = **Approved account** (post password sign-in); challenge + sign-in = **Anonymous** (a leaked device-key id is useless without the private key) |
| Crypto | **ES256** (ECDSA P-256). Public key = base64 `SubjectPublicKeyInfo`; signature = base64 **IEEE-P1363** (`r‖s`, 64 bytes) over **SHA-256(challenge bytes)**. Challenge = 32 random bytes (base64), 5-minute single-use lifetime. |
| Returns | `ApiResult<...>` |

The 5-day session window (D1) is the **config-bound device-key refresh lifetime**. The biometric
re-open path signs a fresh server challenge to mint tokens without a typed password; enrolment
registers the device-key after a successful password sign-in. The private scalar never leaves the
device (secure storage; a secure-enclave key is the simf-run hardening follow-up).

```jsonc
// Enrol (after password sign-in) — RegisterDeviceKeyRequest
{ "publicKey": "<base64 SubjectPublicKeyInfo>", "algorithm": "ES256", "label": "iPhone 15 Pro" }
// Returns ApiResult<DeviceKeyEntry { id, userId, algorithm, label, createdAt, lastUsedAt?, revokedAt? }>
```
```jsonc
// Challenge — POST …/device-keys/{id}/challenge  (anonymous, empty body)
// Returns ApiResult<DeviceKeyChallenge { challenge: "<base64>", expiresInSeconds: 300 }>
```
```jsonc
// Sign-in (biometric re-open) — SignInWithDeviceKeyRequest
{ "deviceKeyId": "guid", "challenge": "<base64>", "signature": "<base64 IEEE-P1363 r‖s>" }
// Returns ApiResult<AuthTokens { accessToken, refreshToken, tokenType, accessTokenExpiresInSeconds, user }>
```
| Error code | When (which endpoint surfaces it) |
|---|---|
| `DEVICE_KEY_INVALID` | Enrol: public key missing / too large / unparseable, or label length ∉ [1,64]. |
| `DEVICE_KEY_ALGORITHM_UNSUPPORTED` | Enrol: `algorithm` is not `ES256`. |
| `DEVICE_KEY_NOT_FOUND` | Challenge / revoke: unknown id (also returned to a non-owner on revoke). |
| `DEVICE_KEY_REVOKED` | Challenge requested for a revoked key (401). |
| `DEVICE_KEY_SIGNATURE_INVALID` | Sign-in: **all** verify failures collapse to this 401 (bad signature, challenge mismatch / expiry, disabled owner). |

> The granular `DEVICE_KEY_CHALLENGE_INVALID` / `DEVICE_KEY_OWNER_UNAVAILABLE` codes exist but are
> **audit-log only** — the sign-in endpoint returns the single `DEVICE_KEY_SIGNATURE_INVALID` 401 so
> a caller cannot distinguish *why* a proof failed.
>
> **.NET ↔ Dart interop is proven, not assumed:** the Flutter `DeviceKeyClient` (pointycastle) SPKI
> public key + IEEE-P1363 signature are captured as a golden vector and run through the real backend
> verify path in `DeviceKeySignInTests.Dart_client_signature_verifies_against_the_backend` (D-266).
> Only the on-device biometric prompt + a secure-enclave key remain a simf-run item (native android/ios).

## Dropped
- **Nafath** national-identity sign-in is **not** part of this screen (Logic D4) — no endpoint.

## Notes
- Every endpoint returns the standard `ApiResult<T>` envelope — `success`, `data`,
  `error`, `meta` (SIMF-API-001). On failure `error` is a **single** object
  `{ code, message, messageArabic, details: [ { field, message, messageArabic } ] }`;
  field-level reasons live in `details[]`. There is **no** top-level `errors[]`
  array and **no** `traceId`.
- Anonymous access here is the documented exception for SignIn / ForgotPassword / reset /
  refresh / OTP-verify (auth bootstrap) — every other App endpoint requires a valid token.
