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
| Email input | Two-way bound to `email`; trims; enforces 50-char UI cap (counter hidden); pre-fill from the local store in `initState` (Logic L-3). |
| Password input | Two-way bound to `password`; obscured; 32-char UI cap; submit-on-enter when both fields are filled; cleared on any sign-in `AuthFailure`. |
| Sign in button | Disabled when either field is empty or a request is in flight; triggers `POST /app/auth/sign-in`. |
| Biometric button | Triggers on-device face auth → device-key challenge sign-in (`POST /app/auth/sign-in-with-device-key`, Logic L-2); **always rendered** (D-360 design); failures fall back silently to the password path. After a successful password sign-in the screen runs a best-effort device-key enrolment. |
| Remember-me checkbox | Default **checked** (preserves the historical always-store behaviour); unchecked → the email is not stored for the next prefill. |
| Forgot password link | Opens the forgot-password / reset flow at `/auth/forgot-password` (Logic L-6 — KSA `KsaAuthScaffold` chrome, D-374). |
| Create account link | Pushes the sign-up form (`/sign-up`). |
| Globe language toggle | Toggles AR ↔ EN via `LocaleController.setLanguage` and persists the choice (D-363); tooltip **العربية · English**. |
| Guest link | Pushes guest mode (`/guest`) — the app's only guest entry (D-325/D-363). |
| Error surface | A **single inline red message** inside the card, above the sign-in button: the envelope's `error.message` (server-localised to the request language) — or the local bilingual `networkErrorBody` string on a network failure. No snackbar/banner; no per-field server errors. |

## States
| State | Appearance |
|---|---|
| **Loading** | Sign-in button shows a 20px white spinner; inputs, links, checkbox, chevron and globe all disabled (`_busy`); no layout shift. |
| **Empty** | Both fields blank (fresh install / nothing remembered); Sign-in disabled until both filled. |
| **Pre-filled** | Email populated from the local store when remember-me stored it on the last successful sign-in (or after a password reset); no autofocus is forced. |
| **Error** | Single inline red message inside the card (above the button); password cleared on a sign-in failure; email preserved. |
| **2FA branch** | On `mfaRequired: true` the controller enters `AuthStateAwaitingOtp` and the screen navigates to `/auth/verify-otp` (not an error state). |
| **Biometric prompt** | Native face-auth sheet (`local_auth`, biometric-only + sticky); on success the device-key sign-in runs and the post-auth route fires; on local failure or an unsupported device it falls back **silently** to the password path. |
| **Success** | No confirmation UI — `routeAfterAuth` (D-374) routes immediately: incomplete profile → the profile form (`/sign-up/visitor`, Page 007), else home (`/`). |

## RTL & localization
- Arabic is the primary locale; the card content mirrors in **RTL** — field labels, the
  remember-me row and the header sit at the inline start (`AlignmentDirectional`).
- The **top controls row is forced LTR** (`textDirection: TextDirection.ltr`) so the back
  chevron stays left and the globe stays right under both locales, matching the Figma
  frame — these do **not** flip.
- Every label, hint, button and error string is **bilingual** via `AppL10n` (`_t(ar, en)`);
  server error messages arrive already localised to the request language.
- The email field stays **LTR-pinned** (`textDirection: ltr`, left-aligned) even within an
  RTL layout so the address reads correctly.

## Accessibility
- The password show/hide toggle, globe toggle and biometric button carry tooltips
  (`showPasswordTooltip` / `hidePasswordTooltip` / `languageToggleLabel` /
  `biometricSignInTooltip`).
- Biometric is an enhancement, never the only path — the password flow is always reachable.
- The error message is a single text element inside the card (not field-associated).

## 2FA OTP screen — `/auth/verify-otp` (D-369)
The email-OTP second factor (Logic L-5) renders on `EmailOtpVerifyScreen`, restyled to the
shared KSA OTP frame: navy `navySurface` + the rotated sweep, a 56-high header band (back
chevron left — pops, else sign-in — and the centred title **رمز التحقق / Verification
code**), the gold `OtpMark` (mail icon), heading **أدخل رمز التحقق / Enter the verification
code**, body **أدخل الرمز المُرسَل إلى بريدك الإلكتروني. / Enter the code we sent to your
email.**, the shared segmented `OtpCodeBoxes`, an inline red error line, and a bottom
full-width **تحقّق / Verify** button (spinner while busy, enabled from 4 entered digits).
**No resend control on this step.** The verify contract is untouched by the restyle.

## Notes
- No inline styles / no hardcoded colors — the palette is the `SimfTokens` KSA set
  (`navySurface`, `cardBeige`, `beigeBorder`, `accent`, `goldSoft`, `headlineInk`,
  `greyText`, `linkNavy`, `inputInk`, `danger`, `surfaceTint`, `navyDeep`, `radiusSmall`).
- The Face-ID button is **always rendered** (D-360 design) — there is no biometric
  window and no hidden state; unsupported devices fall back silently to the password path
  (Logic D1).
- **Nafath is not on this screen (D4)** — no national-identity button.

*Last updated: 2026-06-13 — as-built conformance pass (D-360/D-363 sign-in; D-369 OTP
restyle; D-374 post-auth routing).*
