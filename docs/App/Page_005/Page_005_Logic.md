# Page 005 — Logic (إنشاء حساب · Sign up)

Business rules behind the sign-up form: client validation, the server-side
state transitions, enumeration resistance, and the error / empty / RTL handling.
The wire contract is in [Page_005_API.md](Page_005_API.md).

## L-1 — Client-side validation (before any call)
| Field | Rule | Failure message (AR / EN) |
|---|---|---|
| Email | Required; valid email format; trimmed + lower-cased | "بريد إلكتروني غير صالح" / "Invalid email" |
| Password | Required; meets the password policy from SIMF-MOB-API-001 (length + complexity) | "كلمة المرور لا تستوفي الشروط" / "Password does not meet the requirements" |
| Confirm password | Required; **must equal** Password | "كلمتا المرور غير متطابقتين" / "Passwords do not match" |

The confirm-password check is **client-only** — only `email` + `password` are
sent. If any rule fails, the call is not made and the field error is shown.

## L-2 — Submit logic (client)
1. Disable the Submit button and show an inline busy state.
2. POST `email` + `password` to `/app/auth/sign-up`.
3. On the generic **201**, navigate to the OTP / "check your email" screen.
4. On network / 5xx, re-enable Submit and show a generic retry toast.

## L-3 — Server-side outcome (state transitions)
On a well-formed request the server, for a **new** email:
1. Creates the account as **UserType = Visitor**, **no privilege/role**.
2. Sets **`AccountState = UnderReview`** (account exists but is not approved).
3. Marks the **profile as incomplete** — the user must finish profile capture
   before approval.
4. Generates a **6-digit OTP** (account-verification purpose) and **emails** it
   to the address for step 2.

For an **already-registered** email the server **does not** create a duplicate
and **does not** reveal that the email exists — it returns the **same generic
201** and, per the existing flow, does not leak state through the response.

## L-4 — Enumeration resistance (D-198) — HARD RULE
- The endpoint **never** returns `409 Conflict` and **never** returns a body that
  differs between "new" and "already registered".
- The client therefore treats **every 201 identically**: navigate to the generic
  OTP / "check your email" screen.
- The Flutter **"you already have an account"** branch is **dead code** — it must
  not be reachable from a sign-up response. Do not re-introduce a 409 path.

## L-5 — What this screen does NOT do
- It does **not** sign the user in (no token issued here).
- It does **not** complete the profile (that is a later step).
- It does **not** grant any privilege (account stays under review).
- It does **not** verify the email (that is the OTP step that follows).

## L-6 — Error / empty / RTL handling
| Condition | Behaviour |
|---|---|
| Empty fields | Submit blocked; per-field required errors (L-1). |
| Invalid email / weak password / mismatch | Inline field error; no call (L-1). |
| Validation rejected by server (`400`) | Map to the field via `ErrorCodes`; show inline. |
| Network down / timeout / 5xx | Generic retry toast; form kept; Submit re-enabled. |
| Rate-limited (`429`) | "حاول لاحقاً" / "Please try again later" toast. |
| RTL (Arabic) | Field labels, errors, and the form align right-to-left; the email field stays LTR for the address text inside an RTL layout. |

## L-7 — Dependencies
- Password policy + email format rules: SIMF-MOB-API-001.
- Error codes: `src/Shared/SIMF.Common/ErrorCodes.cs`.
- Account lifecycle (Visitor / UnderReview / profile-incomplete): the shipped
  account model; this screen only triggers the **create + email-OTP** transition.
