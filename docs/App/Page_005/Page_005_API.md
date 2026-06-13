# Page 005 — API (إنشاء حساب · Sign up)

Authoritative backend contract for this page. Inherits the `ApiResult<T>`
envelope, headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001
§3–§4. The rules behind the call are in [Page_005_Logic.md](Page_005_Logic.md).

*Last updated: 2026-06-13 — as-built conformance pass (W2-1, D-370). The KSA
redesign changed visuals only; this contract is unchanged ("logic byte-identical").*

> **Status:** endpoint **exists** (`SignUpEndpoint`,
> `src/Backend/SIMF.Api/Endpoints/Auth/SignUpEndpoint.cs`). No schema change,
> no enum change, no migration.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split,
> D-247) — so the route below is `POST /api/v1/app/auth/sign-up`.
>
> **D-198 — enumeration resistance (binding):** the endpoint returns a **generic
> 201** with the same body shape for a new email, an already-registered but
> **unverified** email (the account is restarted — see below), and an
> already-**verified** email (deflected), with **no `409`** and no
> distinguishing body. The app shows the generic "check your email" step on
> every success; there is no "you already have an account" path in the app.

## E1 — `POST /app/auth/sign-up`
| | |
|---|---|
| Route | `POST /api/v1/app/auth/sign-up` |
| Access | **Anonymous** (Guest) — `AllowAnonymous()`; sign-up is one of the few AllowAnonymous endpoints |
| Rate limit | Per-IP **"auth"** route policy (fixed window) stacked on the global per-IP limiter; the per-email `"auth-email"` policy is **not** applied to this route |
| App privilege | Guest (creates the account; does not sign in) |
| Returns | `ApiResult<SignUpResponse>` with HTTP **201** (generic, always) |

### Request
```jsonc
// SignUpRequest (src/Shared/SIMF.Contracts/Authentication/SignUp.cs)
{
  "email":           "string",  // required; valid email; ≤256 server-side. The app sends it trimmed + lower-cased (client normalisation; the UI field caps input at 50 chars)
  "password":        "string",  // required; 8–128 chars; ≥1 letter + ≥1 digit; must not equal the email (case-insensitive). The UI field caps input at 32 chars
  "confirmPassword": "string"   // required; MUST equal password
}
```
> **Corrected (D-270):** `confirmPassword` **is part of the request body** and is
> **re-validated server-side** (`SignUpRequestValidator`: `ConfirmPassword == Password`)
> — the device also checks it for instant feedback (Page_005_Logic L-1). The app's
> `SignUpRequest.toJson()` sends all three fields. A request whose value
> mismatches is rejected **400** "The passwords do not match." /
> "كلمتا المرور غير متطابقتين." (An earlier draft of this doc wrongly said
> confirm-password was client-only.)

### Response (success — always 201, generic)
```jsonc
// ApiResult<SignUpResponse>  — envelope is { success, data, error, meta }
{
  "success": true,
  "data": {
    "email": "string",            // the email the OTP was sent to (as submitted)
    "codeExpiresInSeconds": 600   // OTP lifetime — 10 minutes (RegistrationService.CodeLifetime)
  },
  "error": null
}
```
> **Corrected (D-270):** the success payload is `SignUpResponse { email,
> codeExpiresInSeconds }` (not a `{ message }` object), and the envelope key is
> `success` with a single nullable `error` — **not** `succeeded` / `errors[]`
> (`ApiResult<T>` = `success` / `data` / `error` / `meta`, per
> `src/Shared/SIMF.Common/ApiResult.cs`).

What the same generic 201 means server-side (`RegistrationService.SignUpAsync`, D-198):

- **New email** — creates a **Visitor** account (`UserType` defaults to
  `Visitor`) in **`AccountState.Registered`** with `DisplayName` = the email,
  `TwoFactorEnabled = true` (D-373), **no profile yet**, and emails a
  **6-digit OTP** (email-verification purpose, 10-minute lifetime). The user
  row and its first code commit in one transaction.
- **Already registered, still `Registered` (unverified)** — the sign-up is
  treated as "start over": the newly-typed password replaces the old one, the
  security stamp is rolled, and a fresh code is issued — capped at **5 codes
  per hour per account** (shared with resend; over the cap → **429**).
- **Already verified** — nothing is created or changed; the **owner** is sent a
  "account already exists" heads-up email, and the same generic body returns.

The app navigates to the email-OTP / verify-email screen on this response
**regardless** of which path ran.

### Error responses (`ApiResult<T>` with `success: false`, `error` populated)
| HTTP | `ErrorCodes` (`src/Shared/SIMF.Common/ErrorCodes.cs`) | Cause | App handling |
|---|---|---|---|
| 400 | `VALIDATION_FAILED` (FluentValidation — bilingual messages, field details), or the `DataValidationException` "The account could not be created." / "تعذّر إنشاء الحساب." when ASP.NET Identity rejects the password | Malformed request that passed local checks | The app shows the server's bilingual `error.message` **inline** under the fields (red text); form kept |
| 403 | `REGISTRATION_CLOSED` (D-166 registration gate) — "Registration is currently closed. Please try again later." / "التسجيل مغلق حالياً. يرجى المحاولة لاحقاً." | The CP registration toggle is closed; nothing is created, no email sent | Same inline display of the server message |
| 429 | `RATE_LIMIT_EXCEEDED` — the per-IP "auth" route limiter ("Too many requests. Please try again shortly." / "عدد الطلبات كبير. حاول مرة أخرى بعد قليل.") or the per-account code cap on the unverified-restart path ("Too many verification codes have been requested. Try again later.") | Too many attempts | Same inline display of the server message |
| 5xx / network | — (transport) | Server/transport failure | Network/timeout (`NetworkUnavailable`) → the app's `networkErrorBody` string inline; form kept |
| — | **(no 409)** | Already-registered email is **not** an error | Treated as success → generic email-OTP screen (D-198) |

> The app does not special-case these statuses: every `AuthFailure` except
> `NetworkUnavailable` renders `failure.source.message` (the envelope's
> bilingual `error.message`) inline; `NetworkUnavailable` renders the local
> `networkErrorBody` string. No toast is used for errors.

## Follow-on (step 2 — not this screen)
Email verification consumes the emailed **6-digit OTP** on the email-OTP /
verify-email screen (`/sign-up/otp`, Page 006), which this screen opens with the
address as a query parameter. That call belongs to that page's API doc, not
Page 005.

## Build dependencies
- None new — the endpoint is shipped. Password policy + email-format limits are
  enforced server-side in `SignUpRequestValidator` (SIMF-API-001 §12.5).
