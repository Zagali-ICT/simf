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
| Returns | `ApiResult<SignInResult>` |

```jsonc
// Request — SignInRequest
{
  "email":    "string",   // server ≤256
  "password": "string"    // server ≤128
}
```
```jsonc
// Response — ApiResult<SignInResult>.data
{
  "requiresTwoFactor": false,        // true ⇒ 2FA email-OTP branch (Logic L-5)
  "twoFactorToken":    "string?",    // present when requiresTwoFactor; passed to verify-otp
  "accessToken":       "string?",    // present when NOT requiresTwoFactor
  "refreshToken":      "string?",    // present when NOT requiresTwoFactor
  "expiresInSeconds":  0             // access-token lifetime
}
```
| Error code | When |
|---|---|
| `AUTH_INVALID_CREDENTIALS` | Wrong email/password. |
| `AUTH_ACCOUNT_NOT_APPROVED` | Account exists but not approved. |
| `AUTH_ACCOUNT_LOCKED` | Account locked / disabled. |
| `VALIDATION_ERROR` | Missing/invalid email or password (server limits). |

## E2 — `POST /app/auth/verify-otp`  (2FA email-OTP branch — Logic D3/L-5)
| | |
|---|---|
| Route | `POST /api/v1/app/auth/verify-otp` |
| Access | **Anonymous** (carries the `twoFactorToken` from E1) |
| Returns | `ApiResult<SignInResult>` (now with tokens) |

```jsonc
// Request — VerifyOtpRequest
{
  "twoFactorToken": "string",  // from E1
  "otp":            "string"   // code delivered by email
}
```
```jsonc
// Response — ApiResult<SignInResult>.data
{ "accessToken": "string", "refreshToken": "string", "expiresInSeconds": 0 }
```
| Error code | When |
|---|---|
| `AUTH_OTP_INVALID` | Wrong code. |
| `AUTH_OTP_EXPIRED` | Code expired / consumed. |

## E3 — `POST /app/auth/refresh`  (silent session refresh — Logic L-1)
| | |
|---|---|
| Route | `POST /api/v1/app/auth/refresh` |
| Access | **Anonymous** (carries the refresh token) |
| Returns | `ApiResult<SignInResult>` (rotated tokens) |

```jsonc
// Request — RefreshRequest
{ "refreshToken": "string" }
```
```jsonc
// Response — ApiResult<SignInResult>.data
{ "accessToken": "string", "refreshToken": "string", "expiresInSeconds": 0 }
```
| Error code | When |
|---|---|
| `AUTH_REFRESH_INVALID` | Refresh token unknown / revoked. |
| `AUTH_REFRESH_EXPIRED` | Refresh token past its lifetime. |

## E4 — `POST /app/auth/forgot-password`  (emails an OTP — Logic L-6)
| | |
|---|---|
| Route | `POST /api/v1/app/auth/forgot-password` |
| Access | **Anonymous** |
| Returns | `ApiResult<object>` — always success-shaped (no account enumeration) |

```jsonc
// Request — ForgotPasswordRequest
{ "email": "string" }
```
Server emails a one-time code to the address if it exists. Response carries no data
beyond success; the app proceeds to the reset step regardless.

## E5 — `POST /app/auth/reset-password`  (OTP + new password — Logic L-6)
| | |
|---|---|
| Route | `POST /api/v1/app/auth/reset-password` |
| Access | **Anonymous** (carries the OTP from E4) |
| Returns | `ApiResult<object>` |

```jsonc
// Request — ResetPasswordRequest
{
  "email":       "string",
  "otp":         "string",
  "newPassword": "string"   // server ≤128
}
```
| Error code | When |
|---|---|
| `AUTH_OTP_INVALID` | Wrong reset code. |
| `AUTH_OTP_EXPIRED` | Reset code expired. |
| `VALIDATION_ERROR` | New password fails server policy. |

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
- All six endpoints return the standard `ApiResult<T>` envelope (`success`, `data`,
  `errors[]`, `traceId`) per SIMF-API-001.
- Anonymous access here is the documented exception for SignIn / ForgotPassword / reset /
  refresh / OTP-verify (auth bootstrap) — every other App endpoint requires a valid token.
