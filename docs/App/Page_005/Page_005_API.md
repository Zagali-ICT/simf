# Page 005 — API (إنشاء حساب · Sign up)

Authoritative backend contract for this page. Inherits the `ApiResult<T>`
envelope, headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001
§3–§4. The rules behind the call are in [Page_005_Logic.md](Page_005_Logic.md).

> **Status:** endpoint **exists** (`POST /app/auth/sign-up`). No schema change,
> no enum change, no migration.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split,
> D-247) — so the route below is `POST /api/v1/app/auth/sign-up`.
>
> **D-198 — enumeration resistance (binding):** the endpoint returns a **generic
> 201** for both a new and an already-registered email, with **no `409`** and no
> distinguishing body. The app shows the generic "check your email" screen on
> every success; the Flutter "you already have an account" path is **dead**.

## E1 — `POST /app/auth/sign-up`
| | |
|---|---|
| Route | `POST /api/v1/app/auth/sign-up` |
| Access | **Anonymous** (Guest) — sign-up is one of the few AllowAnonymous endpoints |
| App privilege | Guest (creates the account; does not sign in) |
| Returns | `ApiResult<SignUpResponse>` with HTTP **201** (generic, always) |

### Request
```jsonc
// SignUpRequest
{
  "email":           "string",  // required; valid email ≤256; server trims + lower-cases
  "password":        "string",  // required; ≥8 and ≤128; ≥1 letter + ≥1 digit; must not equal the email
  "confirmPassword": "string"   // required; MUST equal password
}
```
> **Corrected (D-270):** `confirmPassword` **is part of the request body** and is
> **re-validated server-side** (`SignUpRequestValidator`: `confirmPassword == password`)
> — the device also checks it for instant feedback (Page_005_Logic L-1). The app's
> `SignUpRequest.toJson()` already sends all three fields. A request that omits it
> (or whose value mismatches) is rejected **400** "The passwords do not match." (An
> earlier draft of this doc wrongly said confirm-password was client-only.)

### Response (success — always 201, generic)
```jsonc
// ApiResult<SignUpResponse>  — envelope is { success, data, error, meta }
{
  "success": true,
  "data": {
    "email": "string",            // the normalised email the OTP was sent to
    "codeExpiresInSeconds": 0     // OTP lifetime, integer seconds
  },
  "error": null
}
```
> **Corrected (D-270):** the success payload is `SignUpResponse { email,
> codeExpiresInSeconds }` (not a `{ message }` object), and the envelope key is
> `success` with a single nullable `error` — **not** `succeeded` / `errors[]`
> (`ApiResult<T>` = `success` / `data` / `error` / `meta`, per
> `src/Shared/SIMF.Common/ApiResult.cs`).
On success the server has (for a new email) created a **Visitor** account in
**`UnderReview`** with an **incomplete profile**, and emailed a **6-digit OTP**
for verification (Page_005_Logic L-3). For an already-registered email it returns
the **same** generic 201 and creates nothing (D-198). The app navigates to the
OTP / verify-email screen on this response **regardless**.

### Error responses (`ApiResult<T>` with `success: false`, `error` populated)
| HTTP | `ErrorCodes` (cite from `src/Shared/SIMF.Common/ErrorCodes.cs`) | Cause | App handling |
|---|---|---|---|
| 400 | validation codes (email format / password policy) | Malformed request that passed local checks | Map to the field; show inline |
| 429 | rate-limit code | Too many attempts | "حاول لاحقاً" / "Please try again later" toast |
| 5xx | server error code | Server/transport failure | Generic retry toast; keep form |
| — | **(no 409)** | Already-registered email is **not** an error | Treated as success → generic OTP screen (D-198) |

> The exact `ErrorCodes` constant names are defined in
> `src/Shared/SIMF.Common/ErrorCodes.cs`; grep there only if you need the literal
> code strings for the validation/rate-limit cases above.

## Follow-on (step 2 — not this screen)
Email verification consumes the emailed **6-digit OTP** on the OTP / verify-email
screen. That call belongs to that page's API doc, not Page 005.

## Build dependencies
- None new — the endpoint is shipped. Password policy + email normalisation are
  enforced server-side (SIMF-MOB-API-001).
