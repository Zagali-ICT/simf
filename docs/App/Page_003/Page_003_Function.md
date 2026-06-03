# Page 003 — Function (تسجيل الدخول · Sign in)

What the user does on this screen, step by step, and the auth gate around it.

## Privilege / auth gate
| | |
|---|---|
| Entry privilege | **Guest** (unauthenticated). This is the app's main sign-in entry point. |
| On success | Promotes the session to **Visitor** (or **Admin/Moderator/Staff** per the account's roles baked into the JWT) and routes onward per the post-login state machine. |
| Reachable when | No valid session, or a session whose 5-day window has expired (see Logic D1). |
| Not reachable when | A valid in-window session exists — the app refreshes silently and skips this screen. |

## Elements
| Element | Type | Notes |
|---|---|---|
| Email field | Text input | UI max **50** chars; keyboard = email; trims whitespace. Pre-filled from local store when the prior session expired (Logic L-3). |
| Password field | Secure input | UI max **32** chars; obscured by default with a show/hide toggle. |
| Sign in | Primary button | Calls `POST /app/auth/sign-in`. Disabled while a request is in flight. |
| Biometric (face) | Icon button / prompt | Shown only when a device-key is enrolled and the session is still in-window; re-opens the session via the device-key refresh path (Logic L-2). |
| Forgot password? | Text link | Opens the forgot-password flow → emails an OTP. |
| Create account | Text link | Navigates to the sign-up flow (owner screen for sign-up). |

## User actions — step by step

### A. Password sign-in (cold / expired session)
1. User opens the app with no valid session → lands on **/sign-in**.
2. If the local store holds the last email and the prior session merely **expired**, the email is **pre-filled**; otherwise the field is empty.
3. User enters / confirms email (≤50) and password (≤32).
4. User taps **Sign in** → app calls `POST /app/auth/sign-in`.
5. **No 2FA:** server returns tokens → app stores them, routes onward.
6. **2FA email-OTP required (Logic D3):** server signals the OTP branch → app navigates to the OTP entry, the code arrives by email, user enters it, app calls `POST /app/auth/verify-otp`, then receives tokens.

### B. Biometric re-open (warm, in-window session)
1. App re-opens within the **5-day** window with an enrolled device-key.
2. Biometric (face) prompt appears; user authenticates locally on-device.
3. App calls the **device-key refresh** endpoint to mint fresh tokens — **no password typed** (Logic L-2).
4. On success → routes onward; on device-key failure → falls back to password sign-in.

### C. Forgot password
1. User taps **Forgot password?**.
2. App calls `POST /app/auth/forgot-password` with the email → server emails a one-time code (OTP).
3. User enters the OTP and the new password.
4. App calls `POST /app/auth/reset-password` (OTP + new password) → on success, returns to /sign-in (email pre-filled) for a fresh password sign-in.

## Navigation
| From | Trigger | To |
|---|---|---|
| /sign-in | Sign in success (no 2FA) | Post-login home (per role) |
| /sign-in | Sign in success (2FA) | OTP entry → on verify → post-login home |
| /sign-in | Forgot password? | Forgot-password / reset flow |
| /sign-in | Create account | Sign-up flow |
| /sign-in | Biometric success | Post-login home (per role) |

## Acceptance criteria
- AC-1: With a valid in-window session, the app does **not** show /sign-in — it refreshes silently.
- AC-2: Email field accepts at most 50 chars; password field at most 32 chars (client caps, Logic D2).
- AC-3: When the prior session expired, the email field is pre-filled from the local store.
- AC-4: Biometric is offered only when a device-key is enrolled and the window is unexpired; failure cleanly falls back to password.
- AC-5: A 2FA-enabled account is routed through the email-OTP branch and signs in only after `verify-otp` succeeds.
- AC-6: Forgot-password emails an OTP and the reset completes via `reset-password`.
- AC-7: All error, empty and loading states render correctly in RTL (Arabic).
