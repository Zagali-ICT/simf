# Page 003 — Logic (تسجيل الدخول · Sign in)

Client + server logic, state transitions, validation and error/empty/RTL handling for
the sign-in screen. The endpoint contracts are in [Page_003_API.md](Page_003_API.md).

## Decisions (maintainer-confirmed)
| Id | Decision |
|---|---|
| **D1** | The owner's original **5-day biometric window is NOT implemented as-built**. The KSA design renders the Face-ID button unconditionally (D-360); the device-key stays usable until revoked, and a device-key sign-in mints a session whose refresh token has the same fixed **30-day** server lifetime as a password sign-in (`DeviceKeyService.RefreshTokenLifetime`, a constant). The only timed element is the **5-minute single-use challenge**. |
| **D2** | Email **≤50** and password **≤32** are **CLIENT caps only** (`maxLength` on the fields). The server validates **email ≤256** at sign-in; the sign-in password is only checked NotEmpty — the **≤128 password policy** applies where a password is *set* (sign-up / reset / change). The app must never assume 50/32 server-side. |
| **D3** | The app **handles the 2FA email-OTP branch**: when sign-in signals 2FA, the app collects the emailed code on `/auth/verify-otp` and calls `verify-otp` before it is signed in. |
| **D4** | **Nafath is dropped** for this screen — no national-identity provider path here. |

## Client logic

### L-1 — Boot / session decision (owned by Page 001)
1. On launch the cold-start restore (`AuthController._restoreFromStorage`) reads the stored tokens (time-boxed, D-295).
2. **Valid access token** → restore immediately and re-hydrate `GET /app/users/me`; **expired/missing** → silent `POST /app/auth/refresh`.
3. **Offline** → resume on the cached identity in a degraded state; **invalid/revoked refresh token** → signed out → /sign-in.
4. The splash routes a restored session straight onward (home or the profile form, D-374) — /sign-in is shown only when the restore resolves to signed-out.

### L-2 — Biometric (face) sign-in
- The Face-ID button is **always rendered** (D-360 design) — there is no enrolment or window pre-check on visibility.
- Tap → `local_auth.authenticate` (biometric-only, sticky). On local success the app reads the stored device-key id + P-256 private key from secure storage (**no-op if none enrolled** — the silent fallback), requests a challenge (`POST …/device-keys/{id}/challenge`), signs it (ES256, IEEE-P1363 over SHA-256), and calls `POST /app/auth/sign-in-with-device-key` — **no password is typed**.
- A wire `AuthFailure` shows the inline error; a local biometric/plugin failure or unsupported device falls back **silently** to the password path; the user is never blocked.
- Enrolment is **automatic and best-effort** after a successful password sign-in (`_maybeEnrolBiometric`): skipped when a key is already enrolled or the device is unsupported; any failure is swallowed and never blocks sign-in.

### L-3 — Email pre-fill (remember-me)
- On a successful sign-in the email is persisted to the local prefs store **only when the remember-me checkbox (default ON) is checked**.
- A completed password reset also stores the email (so /sign-in comes back pre-filled).
- The field is pre-filled from that store whenever a value exists — as-built the store is **not** cleared on sign-out.

### L-3b — Post-sign-in routing (profile-completion gate, D-374)
- The server computes **`profileComplete`** (names + ≥1 interest + the C7 male-photo rule) and it rides the `GET /app/users/me` hydration that follows every completed sign-in — there is **no separate profile probe** (this replaced the old D-288 client-side `GET /app/account/user-profile` probe).
- One shared rule, `routeAfterAuth()` (`features/auth/post_auth_route.dart`), runs after the password sign-in, the device-key sign-in **and** the 2FA OTP completion (the splash restore applies the same rule): `profileComplete == false` → the visitor profile form (Page 007, `/sign-up/visitor`); otherwise home (`/`).

### L-4 — Validation (client caps, D2)
| Field | Client rule |
|---|---|
| Email | Required (non-empty after trim); **max 50 chars** (UI cap). No client-side email-shape check — the server's `EmailAddress` rule is the authority. |
| Password | Required (non-empty); **max 32 chars** (UI cap). |
- The Sign-in button stays disabled until both fields are non-empty. Server validation errors (`VALIDATION_FAILED`) surface through the single inline message — the envelope's `error.message`, already localised to the request language.

### L-5 — 2FA email-OTP branch (D3)
- When `sign-in` returns `mfaRequired: true`, the controller holds the `otpToken` in `AuthStateAwaitingOtp` and the screen navigates to `/auth/verify-otp` (the KSA OTP screen, D-369) instead of treating the response as signed-in.
- The code is delivered by **email**; the user enters it into the segmented `OtpCodeBoxes` (submit enabled from 4 digits — a lenient client guard); the app calls `POST /app/auth/verify-otp` with the held `otpToken` + code.
- **No resend control on this step.** Only after `verify-otp` returns tokens is the session authenticated — then the same hydration + post-auth rule (L-3b) runs.

### L-6 — Forgot password / reset
- `POST /app/auth/forgot-password { email }` → server emails an OTP (always success-shaped to avoid account enumeration); the app **always proceeds** to `/auth/reset-password?email=…`.
- The reset screen collects the OTP + new password + confirmation; the match is checked client-side, then `POST /app/auth/reset-password { email, code, newPassword, confirmPassword }`.
- On success the email is stored for pre-fill and the app returns to /sign-in for a fresh password sign-in. Both screens wear the shared `KsaAuthScaffold` chrome (D-374).

## State transitions
```
[Boot — Page 001 splash]
  ├─ valid/refreshable session ──► hydrate /users/me ──► [Home] or [Profile form] (D-374)
  └─ no session / refresh failed ─► [SignIn]

[SignIn]
  ├─ Sign in (no 2FA) ──► tokens ──► hydrate ──► routeAfterAuth ──► [Home] / [Profile form]
  ├─ Sign in (2FA) ─────► [/auth/verify-otp] ──verify-otp──► tokens ──► same rule
  ├─ Face-ID tap ──prompt──► challenge ──sign──► sign-in-with-device-key ──► same rule
  │     └─ local failure / no key ──► [SignIn: password path] (silent)
  ├─ Forgot password ──► [/auth/forgot-password] ──► [/auth/reset-password] ──► [SignIn: pre-filled]
  ├─ Create account ──► [/sign-up]  ·  Guest link ──► [/guest]
  └─ Back ──► pop / onboarding
```

## Error / empty / RTL handling
| Case | Behaviour |
|---|---|
| Invalid credentials | Single inline red message in the card (the server's localised `error.message`); password cleared; email kept. |
| Account locked / disabled / not verified | The server's localised message shows in the same inline surface. A **pending/rejected** account is *not* an error — sign-in completes with tokens; the status is read from `/app/users/me` (Page 011). |
| 2FA required | Treated as success-of-step-1, not an error — route to `/auth/verify-otp` (L-5). |
| OTP wrong / expired | Inline error on the OTP screen; **no resend** on this step; the user can re-enter the code. |
| Network / timeout | The local bilingual `networkErrorBody` string shows inline; fields preserved; no token state mutated. |
| Empty fields | Sign-in button disabled until both fields are non-empty. |
| Biometric unavailable / not enrolled | The button stays **visible**; the tap falls through silently to the password path. |
| RTL | The card mirrors in RTL with bilingual strings (AR primary); the top chevron/globe row and the email field are LTR-pinned per the design. |

## Dependencies
- Identity/auth server (`SIMF_Identity` via `SimfIdentityDbContext`) issues + refreshes tokens and owns 2FA + device-key state.
- Email delivery for the forgot-password OTP and the 2FA OTP.
- Local **secure storage** for tokens + the device-key id/private key; local **prefs storage** for the remembered email and the persisted language choice.

*Last updated: 2026-06-13 — as-built conformance pass (D-360/D-363 sign-in; D-369 OTP step; D-374 post-auth gate; D1 corrected to the as-built device-key behaviour).*
