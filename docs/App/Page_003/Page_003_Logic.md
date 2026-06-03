# Page 003 — Logic (تسجيل الدخول · Sign in)

Client + server logic, state transitions, validation and error/empty/RTL handling for
the sign-in screen. The endpoint contracts are in [Page_003_API.md](Page_003_API.md).

## Decisions (maintainer-confirmed)
| Id | Decision |
|---|---|
| **D1** | The **5-day session window** is a **config-bound device-key refresh** lifetime — the device-key refresh stays valid for the configured window; biometric re-open works only inside it. |
| **D2** | Email **≤50** and password **≤32** are **CLIENT caps only**. The server contract stays at **email 256 / password 128** — the app must never assume the server enforces 50/32. |
| **D3** | The app **handles the 2FA email-OTP branch**: when sign-in signals 2FA, the app collects the emailed code and calls `verify-otp` before it is signed in. |
| **D4** | **Nafath is dropped** for this screen — no national-identity provider path here. |

## Client logic

### L-1 — Boot / session decision
1. On launch, read the stored tokens + the device-key window state.
2. **Valid in-window session** → silently refresh (`POST /app/auth/refresh`) and skip /sign-in.
3. **Expired window** → show /sign-in, pre-fill email from local store (L-3).
4. **No session / no store** → show /sign-in with empty fields.

### L-2 — Biometric (face) re-open
- Offered only when **(a)** a device-key is enrolled for this user on this device, and **(b)** the 5-day window is unexpired (D1).
- On biometric success, the app calls the **device-key refresh** endpoint (under `/app/auth/device-key`) to mint fresh tokens — **no password is typed**.
- On biometric failure or a rejected device-key, fall back to password sign-in; do not block the user.
- Enrolment of the device-key happens after a successful password sign-in (opt-in), via the device-key endpoints.

### L-3 — Email pre-fill
- On a successful sign-in, persist the email to the local store.
- When the session window has **expired** (not on a fresh install / explicit sign-out), pre-fill the email field from that store so the user only re-types the password.
- Explicit sign-out clears the store; the field is then empty.

### L-4 — Validation (client caps, D2)
| Field | Client rule |
|---|---|
| Email | Required; trimmed; basic email shape; **max 50 chars** (UI cap). |
| Password | Required; **max 32 chars** (UI cap). |
- These caps are UI-only. The server validates against its own limits (email 256 / password 128) and remains the authority — surface server validation errors verbatim from `ApiResult<T>.errors`.

### L-5 — 2FA email-OTP branch (D3)
- If `sign-in` indicates 2FA is required, the app navigates to OTP entry instead of treating the response as signed-in.
- The code is delivered by **email**; the user enters it; the app calls `POST /app/auth/verify-otp`.
- Only after `verify-otp` returns tokens is the session considered authenticated.

### L-6 — Forgot password / reset
- `POST /app/auth/forgot-password { email }` → server emails an OTP (always returns success-shaped to avoid account enumeration).
- User enters OTP + new password → `POST /app/auth/reset-password { email/token, otp, newPassword }`.
- On success, return to /sign-in (email pre-filled) for a fresh password sign-in.

## State transitions
```
[Boot]
  ├─ valid in-window session ──► refresh ──► [Home]
  ├─ expired window ──────────► [SignIn: email pre-filled]
  └─ no session ──────────────► [SignIn: empty]

[SignIn]
  ├─ Sign in (no 2FA) ──► tokens ──► [Home]
  ├─ Sign in (2FA) ─────► [OTP entry] ──verify-otp──► tokens ──► [Home]
  ├─ Biometric (in-window) ──device-key refresh──► tokens ──► [Home]
  │     └─ failure ──► [SignIn: password]
  └─ Forgot password ──► forgot-password ──► [Reset: OTP+new pwd] ──reset-password──► [SignIn]
```

## Error / empty / RTL handling
| Case | Behaviour |
|---|---|
| Invalid credentials | Inline error from `ApiResult<T>.errors`; password cleared; email kept. Bilingual message (AR primary in RTL). |
| Account not approved / locked | Show the server's bilingual message; offer the relevant next step (e.g. await approval). |
| 2FA required | Treated as success-of-step-1, not an error — route to OTP entry (L-5). |
| OTP wrong / expired | Inline error on the OTP field; allow resend per the OTP policy. |
| Network / 500 | Non-blocking retry banner; fields preserved; no token state mutated. |
| Empty fields | Sign-in button disabled until both fields are non-empty. |
| Biometric unavailable / not enrolled | Biometric control hidden; password path is the default. |
| RTL | Arabic layout mirrors the screen; labels, errors and toasts are bilingual with AR primary; numerals and field alignment follow the locale. |

## Dependencies
- Identity/auth server (`SIMF_Identity` via `SimfIdentityDbContext`) issues + refreshes tokens and owns 2FA + device-key state.
- Email delivery for the forgot-password OTP and the 2FA OTP.
- Local secure store for tokens, last email, and device-key window state.
