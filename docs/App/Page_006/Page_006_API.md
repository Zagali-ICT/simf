# Page 006 — API (التحقق بالبريد · Email verification)

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
| 404 | `auth.account_not_found` | No account exists for `email`. |
| 400 | `auth.code_invalid` | Account already verified (not `Registered`) · no outstanding code · attempt cap reached · submitted code is wrong (consumes one attempt). |
| 400 | `auth.code_expired` | The code is past `ExpiresAt`. |
| 400 | (validation) | `email` / `code` fail `VerifyEmailRequestValidator` (empty, bad email, not 6 digits) — bilingual field messages. |
| 429 | (throttle) | `auth` rate limit exceeded. |

## E2 — `POST /app/auth/resend-code`
| | |
|---|---|
| Full route | `POST /api/v1/app/auth/resend-code` |
| Access | **Anonymous** (`AllowAnonymous`), `auth` rate limiter. No permission code. |
| App privilege | Anonymous (mid sign-up) |
| Returns | `ApiResult<ResendCodeResponse>` |
| Effect | Issues a fresh `EmailVerification` code (invalidating the previous one) and enqueues the verification email. Subject to an account-scoped resend cap independent of the IP limiter. |

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
    "codeExpiresInSeconds": 0   // lifetime of the new code → drives the client resend cooldown
  },
  "error": null
}
```

**Errors**
| HTTP | `error.code` | When |
|---|---|---|
| 404 | `auth.account_not_found` | No account exists for `email`. |
| 400 | `auth.code_invalid` | Account already verified (not `Registered`). |
| 400 | (cap) | Account-scoped resend cap reached — bilingual cap message. |
| 400 | (validation) | `email` fails `ResendCodeRequestValidator`. |
| 429 | (throttle) | `auth` rate limit exceeded. |

## Notes
- Both endpoints existed before this page was documented; this screen is a **client of**
  the shared sign-up auth contract (SIMF-API-001 §12.4) — no new endpoint, schema, enum
  or migration is introduced by Page 006.
- `MaxCodeAttempts` (verify) and the resend cap (resend) are server-owned constants in
  `RegistrationService`; the client only reflects their outcomes via the bilingual errors.
