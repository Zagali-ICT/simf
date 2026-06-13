# Page 005 — Logic (إنشاء حساب · Sign up)

Business rules behind the sign-up form: client validation, the server-side
state transitions, enumeration resistance, and the error / empty / RTL handling.
The wire contract is in [Page_005_API.md](Page_005_API.md).

*Last updated: 2026-06-13 — as-built conformance pass (W2-1, D-370). The KSA
redesign changed visuals only; this logic is the shipped behaviour.*

## L-1 — Client-side validation (before any call)
| Field | Rule (as-built, `SignUpFormScreen`) | Failure message (AR / EN) |
|---|---|---|
| Email | Trimmed, then must match `^[^@\s]+@[^@\s]+\.[^@\s]+$` (an empty field fails the same check — there is no separate "required" message) | `invalidEmail` — "بريد إلكتروني غير صالح" / "Invalid email" |
| Password | ≥ 8 chars **and** ≥ 1 letter (`[A-Za-z]`) **and** ≥ 1 digit — the client mirror of the server policy (SIMF-MOB-API-001); the server re-validates | `passwordPolicyError` — "كلمة المرور لا تستوفي الشروط" / "Password does not meet the requirements" |
| Confirm password | Must equal the Password field's current text | `passwordsDoNotMatch` — "كلمتا المرور غير متطابقتين." / "The passwords do not match." |

The fields validate on submit and re-validate as the user types after first
interaction (`autovalidateMode: onUserInteraction`). The UI also caps raw input
length: email 50 chars, both password fields 32 chars (the server allows up to
256 / 128 respectively).

The confirm-password check runs **client-side** for instant feedback **and**
`confirmPassword` is included in the request body — `email` + `password` +
`confirmPassword` are sent, and the server re-validates `ConfirmPassword ==
Password` (`SignUpRequestValidator`; D-270). If any rule fails, the call is not
made and the field error is shown.

## L-2 — Submit logic (client)
1. Run the form validators; abort on any failure (the Submit button itself is
   not gated — it validates on tap).
2. Normalise the email: `trim().toLowerCase()`.
3. Set the busy state (button spinner; fields, back chevron and globe toggle
   disabled) and clear any previous inline error.
4. Call `AuthController.signUp` → `AuthRepositoryImpl.signUp` → `AuthApi.signUp`
   → POST `{ email, password, confirmPassword }` to `/app/auth/sign-up`. The
   controller does **not** change `AuthState` and no token is issued.
5. On the generic **201**: show the `signUpCheckEmail` SnackBar and **push** the
   email-OTP screen (`RouteNames.emailOtp` → `/sign-up/otp`) with
   `?email=<address>`.
6. On `AuthFailure`: render an inline error inside the card —
   `networkErrorBody` when the failure is `NetworkUnavailable`, otherwise the
   server's bilingual `failure.source.message` — and clear the busy state so
   the user can retry.

## L-3 — Server-side outcome (state transitions)
`RegistrationService.SignUpAsync` first honours the **registration gate**
(D-166): when registration is closed it throws **403 `REGISTRATION_CLOSED`**
and creates nothing. Otherwise, for a **new** email it:
1. Creates the account with **`UserType = Visitor`** (the default), **no
   privilege/role**, `DisplayName` = the email.
2. Sets **`AccountState = Registered`** — the start of the lifecycle
   (Registered → EmailVerified → PendingApproval → Approved); the account is
   not yet verified, let alone approved.
3. Sets **`TwoFactorEnabled = true`** (D-373 — email-OTP second factor on for
   every new visitor account). No profile row exists yet — profile capture is a
   later step.
4. Generates a **6-digit OTP** (email-verification purpose, cryptographically
   random, **10-minute lifetime**) in the same transaction as the user row,
   queues the verification email, and drops an in-app "code sent" notification.

For an **already-registered** email the server **does not** reveal that the
email exists — it returns the **same generic 201** either way (D-198):
- Still **`Registered`** (never verified): the sign-up is treated as "start
  over" — the newly-typed password replaces the old one, the security stamp is
  rolled, and a fresh code is issued (capped at 5 codes/hour per account;
  over the cap → 429).
- Already **verified**: nothing is created or changed; the account **owner** is
  emailed a "your email was used to sign up" heads-up.

## L-4 — Enumeration resistance (D-198) — HARD RULE
- The endpoint **never** returns `409 Conflict` and **never** returns a body that
  differs between "new", "registered-unverified" and "already verified".
- The client therefore treats **every 201 identically**: SnackBar + push to the
  generic email-OTP / "check your email" screen.
- The Flutter screen has **no "you already have an account" branch** — do not
  introduce one, and do not re-introduce a 409 path.

## L-5 — What this screen does NOT do
- It does **not** sign the user in (no token issued; `AuthState` unchanged).
- It does **not** create or complete the profile (that is a later step).
- It does **not** grant any privilege (the account is merely `Registered`).
- It does **not** verify the email (that is the OTP step that follows).

## L-6 — Error / empty / RTL handling
| Condition | Behaviour |
|---|---|
| Empty / invalid fields | Per-field inline error on submit (and on typing after first interaction); no call (L-1). |
| Validation rejected by server (`400`) | The server's bilingual `error.message` shown **inline in red** inside the card (the app does not map field-level details back onto fields); form kept. |
| Registration closed (`403 REGISTRATION_CLOSED`) | Same inline display — "التسجيل مغلق حالياً. يرجى المحاولة لاحقاً." / "Registration is currently closed. Please try again later." |
| Rate-limited (`429 RATE_LIMIT_EXCEEDED`) | Same inline display of the server's bilingual message. |
| Network down / timeout | `networkErrorBody` shown inline — "تعذر الاتصال بالخادم. تحقق من الاتصال بالإنترنت وحاول مرة أخرى." / "Could not reach the server. Check your internet connection and try again."; form kept; Submit re-enabled. |
| RTL (Arabic) | Card content right-to-left; the email field stays LTR for the address text; the top controls row (back chevron + globe) is forced LTR (D-363). |

## L-7 — Dependencies
- Password policy + email format rules: `SignUpRequestValidator`
  (`src/Backend/SIMF.Api/Endpoints/Auth/Validators/SignUpRequestValidator.cs`,
  SIMF-API-001 §12.5) — mirrored client-side in `SignUpFormScreen`.
- Error codes: `src/Shared/SIMF.Common/ErrorCodes.cs` (`VALIDATION_FAILED`,
  `REGISTRATION_CLOSED`, `RATE_LIMIT_EXCEEDED`).
- Failure mapping: `mapAuthFailure` in
  `packages/simf_auth_pkg/lib/src/domain/auth_failure.dart` (network/timeout →
  `NetworkUnavailable`; everything else surfaces its bilingual message).
- Account lifecycle (`Registered` → `EmailVerified` → `PendingApproval` →
  `Approved`): `AccountState` enum + `RegistrationService`; this screen only
  triggers the **create + email-OTP** transition.
