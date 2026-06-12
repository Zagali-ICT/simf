# Page 005 — Design (إنشاء حساب · Sign up)

Flutter screen design for the sign-up form — layout, components, the four screen
states, RTL, and localization. Behaviour is in
[Page_005_Function.md](Page_005_Function.md); rules in
[Page_005_Logic.md](Page_005_Logic.md); contract in
[Page_005_API.md](Page_005_API.md).

> **As-built (KSA-Project redesign, 2026-06-12 — D-370, Figma 168:3454):** the
> screen now uses the login-frame chrome — `navySurface` background with the
> rotated sweep tint, the forced-LTR top controls (back chevron left, the
> **wired globe language toggle** right, D-363 pattern), and the `SimfLogo` 44
> + forum-name header over the beige `cardBeige` card (radius 4, padding 24).
> Inside the card: centred **إنشاء حساب** head (24 SemiBold `headlineInk`),
> the three fields in the login field language (12-grey label, 48 px
> `beigeBorder`-bordered transparent input, radius 4, gold focus, eye-off
> show/hide on both password fields), the gold **إنشاء حساب** button (48 px,
> white bold 16), and the **هل لديك حساب ؟ / تسجيل الدخول** foot (grey 12 +
> `linkNavy` semibold link). **Logic byte-identical** (validators, generic-201
> → email-OTP forward, enumeration resistance, error/busy states). The ASCII
> frame below documents the pre-redesign layout; states, RTL and localization
> sections still apply. Old screen parked in `_legacy_mockup/`.

## Layout
A single scrollable, vertically-centred auth form on a plain background.

```
┌──────────────────────────────┐
│            [ logo ]          │
│                              │
│        إنشاء حساب · Sign up   │   ← title
│                              │
│  ┌────────────────────────┐  │
│  │ Email                  │  │   ← email field (LTR text inside RTL)
│  └────────────────────────┘  │
│  ┌────────────────────────┐  │
│  │ Password           👁  │  │   ← obscured + show/hide
│  └────────────────────────┘  │
│  ┌────────────────────────┐  │
│  │ Confirm password   👁  │  │   ← obscured + show/hide
│  └────────────────────────┘  │
│                              │
│   [   Create account     ]   │   ← primary button (full width)
│                              │
│   Already have an account?   │
│        → Sign in             │   ← text link
└──────────────────────────────┘
```

## Components
| Component | Role | Notes |
|---|---|---|
| Logo / title | Branding + screen name | AR **إنشاء حساب** · EN **Sign up** |
| Email field | Credential input | Email keyboard; inline error slot below |
| Password field | Credential input | Obscured; show/hide toggle; inline error slot |
| Confirm-password field | Match input | Obscured; show/hide toggle; "passwords do not match" error |
| Primary button | Submit | Full-width; shows spinner + disables while submitting |
| Sign-in link | Navigate to existing-user flow | Text button under the form |

## States
| State | Trigger | UI |
|---|---|---|
| **Idle / empty** | Screen opened | Empty fields, no errors, Submit enabled once fields are touched-valid |
| **Validation error** | Local check fails (L-1) | Inline per-field error; Submit stays; no call made |
| **Loading** | Submit tapped, call in flight | Button shows spinner; fields + button disabled |
| **Success** | Generic **201** received | Navigate to OTP / "check your email" screen — **same screen for new and existing email (D-198)** |
| **Error** | Network / 5xx / 429 | Generic retry toast; form kept; button re-enabled |

> There is no "you already have an account" state on this screen — the success
> path is identical regardless of whether the email is new (D-198).

## RTL (Arabic)
- The whole form mirrors right-to-left: labels, errors, and the link align right.
- The **email field keeps LTR** for the address text inside the RTL layout, so
  the address reads correctly; the field label and error stay RTL.
- The primary button label and toast text use the AR strings under Arabic.

## Localization
| Key | AR | EN |
|---|---|---|
| Title | إنشاء حساب | Sign up |
| Email label | البريد الإلكتروني | Email |
| Password label | كلمة المرور | Password |
| Confirm label | تأكيد كلمة المرور | Confirm password |
| Submit | إنشاء حساب | Create account |
| Sign-in link | لديك حساب؟ تسجيل الدخول | Have an account? Sign in |
| Generic success toast | تحقق من بريدك الإلكتروني | Check your email |
| Retry toast | تعذّر الإنشاء، حاول مجدداً | Could not sign up, try again |
| Rate-limit toast | حاول لاحقاً | Please try again later |

All strings come from the app's resource bundle; no literal text is hard-coded
in widgets.
