# Page 003 — Function (تسجيل الدخول · Sign in)

What the user does on this screen, step by step, and the auth gate around it.

## Privilege / auth gate
| | |
|---|---|
| Entry privilege | **Guest** (unauthenticated). This is the app's main sign-in entry point. |
| On success | The session is hydrated from `GET /app/users/me` — the resolved app role (**Visitor**, **Moderator** or **Staff**) plus `registrationStatus` and the server-computed `profileComplete` flag — and the shared post-auth rule routes onward (D-374): incomplete profile → the profile form (Page 007), else home. |
| Reachable when | Signed out, or navigated to explicitly. A signed-in user landing on `/sign-in` is intentionally **not** bounced away (D-295) — post-sign-in routing belongs to the screen itself. |
| Skipped when | A valid persisted session restores on the splash (Page 001): the cold-start restore refreshes silently and routes home / to the profile form without showing this screen. |

## Elements
| Element | Type | Notes |
|---|---|---|
| Back chevron | Icon button (top-left, forced LTR) | Pops the navigation stack; falls back to onboarding when there is nothing to pop. |
| Globe language toggle | 40×40 icon button (top-right, forced LTR) | Toggles AR ↔ EN via `LocaleController` and persists the choice (D-363). |
| Email field | Text input | UI max **50** chars; keyboard = email; LTR-pinned; trims whitespace. Pre-filled from the local store when remember-me stored it (Logic L-3). |
| Password field | Secure input | UI max **32** chars; obscured by default with a show/hide toggle; enter submits when both fields are filled. |
| Remember me | Checkbox (default **ON**) | Gates whether the email is stored for the next pre-fill (Logic L-3). |
| Sign in | Primary button (gold, 48) | Calls `POST /app/auth/sign-in`. Disabled while either field is empty or a request is in flight. |
| Biometric (face) | Outlined button | **Always rendered** (D-360 design). Tap → on-device face prompt → device-key challenge sign-in (Logic L-2); unsupported devices / failures fall back silently to the password path. |
| Forgot password? | Text link | Opens the forgot-password flow at `/auth/forgot-password` → emails an OTP. |
| Create account | Text link | Pushes the sign-up form (`/sign-up`). |
| Enter as guest | Underlined text link | Pushes guest mode (`/guest`) — the app's only guest entry (D-325/D-363). |

## User actions — step by step

### A. Password sign-in
1. User opens the app with no valid session → the splash resolves to **/sign-in**.
2. If remember-me stored the last email (or a password reset just completed), the email is **pre-filled**; otherwise the field is empty.
3. User enters / confirms email (≤50) and password (≤32).
4. User taps **Sign in** → app calls `POST /app/auth/sign-in`; with remember-me checked the email is stored for the next pre-fill.
5. **No 2FA:** server returns tokens → app stores them, hydrates `GET /app/users/me`, fires a **best-effort device-key enrolment** (for future Face-ID sign-in, never blocking), and routes via the shared post-auth rule (D-374).
6. **2FA email-OTP required (Logic D3):** the controller enters `AuthStateAwaitingOtp` and the app navigates to **/auth/verify-otp** (the KSA OTP screen, D-369); the code arrives by email, the user enters it, the app calls `POST /app/auth/verify-otp`, receives tokens and runs the same post-auth rule.

### B. Biometric sign-in (enrolled device-key)
1. User taps the **Face-ID** button (always visible).
2. The native biometric prompt appears (`local_auth`, biometric-only); the user authenticates on-device.
3. App requests a server challenge for the stored device-key id, signs it with the stored P-256 private key, and calls `POST /app/auth/sign-in-with-device-key` — **no password typed** (Logic L-2).
4. On success → hydrate + route onward like step A.5; on a wire failure an inline error shows; on a local biometric/plugin failure (or no enrolled key) it falls back **silently** to the password path.

### C. Forgot password
1. User taps **Forgot password?** → `/auth/forgot-password` (KSA chrome, D-374).
2. App calls `POST /app/auth/forgot-password` with the email → server emails a one-time code (always success-shaped; the app **always proceeds** to the reset step).
3. On `/auth/reset-password` the user enters the OTP, the new password and its confirmation (client-side match check).
4. App calls `POST /app/auth/reset-password` → on success, stores the email for pre-fill and returns to /sign-in for a fresh password sign-in.

## Navigation
| From | Trigger | To |
|---|---|---|
| /sign-in | Sign in success (no 2FA) | Home (`/`), or the profile form (`/sign-up/visitor`) when `profileComplete` is false (D-374) |
| /sign-in | Sign in success (2FA) | `/auth/verify-otp` → on verify → same post-auth rule |
| /sign-in | Forgot password? | `/auth/forgot-password` → `/auth/reset-password` → back to /sign-in |
| /sign-in | Create account | `/sign-up` (push) |
| /sign-in | Biometric success | Same post-auth rule as a password sign-in |
| /sign-in | Enter as guest | `/guest` (push) |
| /sign-in | Back chevron | Pops; falls back to onboarding |

## Acceptance criteria
- AC-1: A valid persisted session restores on the splash and routes onward without showing /sign-in.
- AC-2: Email field accepts at most 50 chars; password field at most 32 chars (client caps, Logic D2).
- AC-3: With remember-me checked, the email of the last successful sign-in pre-fills the field on the next visit; unchecked, nothing is stored.
- AC-4: The Face-ID button is always visible; biometric/plugin failure or an unsupported device falls back silently to the password path.
- AC-5: A 2FA-enabled account is routed through the email-OTP branch (`/auth/verify-otp`) and signs in only after `verify-otp` succeeds.
- AC-6: Forgot-password emails an OTP and the reset completes via `reset-password`; the reset email pre-fills /sign-in.
- AC-7: All error, empty and loading states render correctly in RTL (Arabic); the top chevron/globe row stays LTR-pinned per the design.

*Last updated: 2026-06-13 — as-built conformance pass (D-360/D-363 sign-in; D-369 OTP step; D-374 post-auth routing).*
