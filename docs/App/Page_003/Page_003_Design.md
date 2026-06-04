# Page 003 — Design (تسجيل الدخول · Sign in)

Flutter screen design — layout, components, data binding, states, RTL and localization.
Behaviour is in [Page_003_Function.md](Page_003_Function.md); rules in
[Page_003_Logic.md](Page_003_Logic.md).

## Layout (top → bottom)
| Zone | Content |
|---|---|
| Brand / header | SIMF logo + screen title — AR **تسجيل الدخول** / EN **Sign in**. |
| Email field | Labelled text input, email keyboard, UI max **50** chars; pre-filled when the session expired. |
| Password field | Secure input, obscured, show/hide toggle, UI max **32** chars. |
| Primary action | **Sign in** button (full-width). Spinner replaces the label while a request is in flight. |
| Biometric | Face/biometric icon button — shown only when a device-key is enrolled and the window is in-window. |
| Secondary links | **Forgot password?** and **Create account**. |

## Components
| Component | Binding / behaviour |
|---|---|
| Email input | Two-way bound to `email`; trims; enforces 50-char UI cap; pre-fill from local store (Logic L-3). |
| Password input | Two-way bound to `password`; obscured; 32-char UI cap; cleared on invalid-credentials error. |
| Sign in button | Disabled when either field is empty or a request is in flight; triggers `POST /app/auth/sign-in`. |
| Biometric button | Triggers on-device face auth → device-key refresh (Logic L-2); hidden when unavailable. |
| Forgot password link | Opens the forgot-password / reset flow (Logic L-6). |
| Create account link | Navigates to the sign-up flow. |
| Error surface | Inline field errors + a bilingual snackbar/banner sourced from `ApiResult<T>.errors`. |

## States
| State | Appearance |
|---|---|
| **Loading** | Sign-in button shows a spinner; inputs disabled; no layout shift. |
| **Empty** | Both fields blank (fresh install / signed-out); Sign-in disabled until both filled. |
| **Pre-filled** | Email populated from local store (expired window); focus lands on the password field. |
| **Error** | Inline message under the offending field + bilingual banner; password cleared on bad credentials; fields otherwise preserved. |
| **2FA branch** | On `requiresTwoFactor`, navigate to the OTP entry screen (not an error state). |
| **Biometric prompt** | Native face-auth sheet; on success the screen transitions straight to home; on failure it returns to the password path. |
| **Success** | Brief confirmation, then route to the post-login home per role. |

## RTL & localization
- Arabic is the primary locale; the whole screen mirrors in **RTL** — field alignment, icon
  placement and the back affordance flip.
- Every label, hint, button, error and toast is **bilingual** with **AR primary** in RTL
  (EN primary in LTR).
- Numerals, date/time and field directionality follow the active locale.
- The email field stays **LTR-isolated** for the address even within an RTL layout so the
  address reads correctly.

## Accessibility
- Inputs carry semantic labels; the password show/hide toggle is announced.
- Biometric is an enhancement, never the only path — the password flow is always reachable.
- Error messages are associated with their fields for screen-reader focus.

## Notes
- No inline styles / no hardcoded colors — follow the app theme tokens.
- The 5-day biometric window (Logic D1) is a config-bound value; the design must not show a
  biometric affordance once the window has expired — the screen reverts to the pre-filled
  password layout.
- **Nafath is not on this screen (D4)** — no national-identity button.
