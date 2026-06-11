# Page 003 — Design (تسجيل الدخول · Sign in)

Flutter screen design — layout, components, data binding, states, RTL and localization.
Behaviour is in [Page_003_Function.md](Page_003_Function.md); rules in
[Page_003_Logic.md](Page_003_Logic.md).

## Layout (top → bottom) — KSA-Project design (Figma 168:2800, D-360)
| Zone | Content |
|---|---|
| Background | Navy `SimfTokens.navySurface` full-bleed; subtle rotated decorative sweep behind the header; **no app bar**. Top controls (Figma 627:2361, D-363): back chevron top-left (pops, else onboarding) + the **globe language toggle** top-right (40×40 `navyDeep` square, radius 4, gold globe — wired to `LocaleController`, AR ↔ EN persisted). |
| Brand / header | `SimfLogo` (44) + forum name **الملتقى الدولى البحرى** in white, centered. |
| Card | Beige `SimfTokens.cardBeige` card (radius 4, padding 24) holding everything below. |
| Card title | AR **تسجيل الدخول** / EN **Sign in**, centered, 24 semibold. |
| Email field | Labelled outlined input (beige border), email keyboard, LTR text, UI max **50**; pre-filled from the local store. |
| Password field | Labelled outlined secure input, eye show/hide toggle, UI max **32**. |
| Remember row | **Remember me** checkbox (default ON — gates the email prefill store) + **Forgot password?** link. |
| Primary action | Gold **دخول / Sign in** button (full-width, 48, white bold). Spinner while in flight. |
| Sign-up row | **ليس لديك حساب؟ إنشاء حساب** — link to the sign-up flow. |
| Divider | Hairline — **او / or** — hairline. |
| Face ID | Outlined **التسجيل ببصمة الوجه** button, gold text + face icon — **always visible** per the design; unavailable devices fall back silently to the password path. |
| Guest link | Underlined **الدخول كزائر / Enter as guest** (design-native since the frame's D-363 update — only guest entry, Page_012). |

## Components
| Component | Binding / behaviour |
|---|---|
| Email input | Two-way bound to `email`; trims; enforces 50-char UI cap; pre-fill from local store (Logic L-3). |
| Password input | Two-way bound to `password`; obscured; 32-char UI cap; cleared on invalid-credentials error. |
| Sign in button | Disabled when either field is empty or a request is in flight; triggers `POST /app/auth/sign-in`. |
| Biometric button | Triggers on-device face auth → device-key refresh (Logic L-2); **always rendered** (D-360 design); failures fall back silently to the password path. After a successful password sign-in the screen runs a best-effort device-key enrolment. |
| Remember-me checkbox | Default **checked** (preserves the historical always-store behaviour); unchecked → the email is not stored for the next prefill. |
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
