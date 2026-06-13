# Page 006 — API (التحقق بالبريد · Email verification)

_Last updated: 2026-06-13 — as-built conformance pass (D-364/D-369; contract unchanged by the redesign)._

Authoritative backend contract for this page. Inherits the `ApiResult<T>` envelope,
headers, error model and rate-limiting from SIMF-API-001 §12.4 + SIMF-MOB-API-001 §3–§4.
The client + server logic is in [Page_006_Logic.md](Page_006_Logic.md).

> **Status:** both endpoints are **built and shipped** — nothing here is "(TO BUILD)".
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split shipped).
> The full routes are `POST /api/v1/app/auth/verify-email` and
> `POST /api/v1/app/auth/resend-code`.
>
> **Auth note:** both are `AllowAnonymous` (the user has no token yet) and bound to the
> `auth` rate limiter. Identity is asserted by **email + emailed 6-digit code**.

## E1 — `POST /app/auth/verify-email`
| | |
|---|---|
| Full route | `POST /api/v1/app/auth/verify-email` |
| Access | **Anonymous** (`AllowAnonymous`), `auth` rate limiter. No permission code. |
| App privilege | Anonymous (mid sign-up) |
| Returns | `ApiResult<VerifyEmailResponse>` |
| Effect | On success: `Registered → EmailVerified`, `EmailConfirmed = true`, code consumed, welcome notification dispatched (best-effort). |

**Request — `VerifyEmailRequest`**
```jsonc
{
  "email": "string",  // required, valid email, ≤ 256 chars
  "code":  "string"   // required, exactly 6 digits  (^\d{6}$)
}
```

**Response — `ApiResult<VerifyEmailResponse>`**
```jsonc
{
  "success": true,
  "data": {
    "email": "string",        // the verified address
    "emailVerified": true     // always true on the success path
  },
  "error": null
}
```

**Errors**
| HTTP | `error.code` | When |
|---|---|---|
| 404 | `AUTH_ACCOUNT_NOT_FOUND` | No account exists for `email`. |
| 400 | `AUTH_CODE_INVALID` | Account already verified (not `Registered`) · no outstanding code · attempt cap reached · submitted code is wrong (consumes one attempt). |
| 400 | `AUTH_CODE_EXPIRED` | The code is past `ExpiresAt`. |
| 400 | (validation) | `email` / `code` fail `VerifyEmailRequestValidator` (empty, bad email, not 6 digits) — bilingual field messages. |
| 429 | (throttle) | `auth` rate limit exceeded. |

## E2 — `POST /app/auth/resend-code`
| | |
|---|---|
| Full route | `POST /api/v1/app/auth/resend-code` |
| Access | **Anonymous** (`AllowAnonymous`), `auth` rate limiter. No permission code. |
| App privilege | Anonymous (mid sign-up) |
| Returns | `ApiResult<ResendCodeResponse>` |
| Effect | Issues a fresh `EmailVerification` code (invalidating the previous one), enqueues the verification email and dispatches the in-app `NotificationKind.CredentialEmailVerificationResent` trail (best-effort). Subject to an account-scoped resend cap independent of the IP limiter. |

**Request — `ResendCodeRequest`**
```jsonc
{
  "email": "string"   // required, valid email, ≤ 256 chars
}
```

**Response — `ApiResult<ResendCodeResponse>`**
```jsonc
{
  "success": true,
  "data": {
    "email": "string",
    "codeExpiresInSeconds": 600   // lifetime of the new code (CodeLifetime = 10 min) → drives the client resend cooldown
  },
  "error": null
}
```

**Errors**
| HTTP | `error.code` | When |
|---|---|---|
| 404 | `AUTH_ACCOUNT_NOT_FOUND` | No account exists for `email`. |
| 400 | `AUTH_CODE_INVALID` | Account already verified (not `Registered`). |
| 429 | `RATE_LIMIT_EXCEEDED` | Account-scoped resend cap reached — bilingual cap message. Same wire signature as the per-IP throttle below. |
| 400 | (validation) | `email` fails `ResendCodeRequestValidator`. |
| 429 | (throttle) | `auth` rate limit exceeded. |

## Notes
- Both endpoints existed before this page was documented; this screen is a **client of**
  the shared sign-up auth contract (SIMF-API-001 §12.4) — no new endpoint, schema, enum
  or migration is introduced by Page 006. The D-364 redesign changed visuals only; the
  wire contract is byte-identical.
- Server-owned constants in `RegistrationService`: `CodeLifetime` = 10 minutes (so
  `codeExpiresInSeconds` = 600), `MaxCodeAttempts` = 5 (verify), and the resend cap =
  `MaxCodesPerWindow` 5 codes per rolling `ResendWindow` of 1 hour (shared with the
  D-198 unverified-restart sign-up path). The client only reflects their outcomes via
  the returned errors.
- `error.message` arrives in the **request's language** (the app sends `Accept-Language`
  `ar`/`en`; SIMF-API-001 §11) — one message field on the wire, displayed as-is.
- Client consumption: the app reads only `codeExpiresInSeconds` from the resend
  response (the `email` field is ignored) and the verify success is consumed as
  envelope-success only (the `{ email, emailVerified }` data is not read).
