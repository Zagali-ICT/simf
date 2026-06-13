# Page 005 — Design (إنشاء حساب · Sign up)

Flutter screen design for the sign-up form — layout, components, the screen
states, RTL, and localization. Behaviour is in
[Page_005_Function.md](Page_005_Function.md); rules in
[Page_005_Logic.md](Page_005_Logic.md); contract in
[Page_005_API.md](Page_005_API.md).

*Last updated: 2026-06-13 — as-built conformance pass (W2-1, D-370); this doc
now describes the shipped KSA-Project screen, not the old mockup layout.*

> **As-built (KSA-Project redesign, 2026-06-12 — D-370, Figma 168:3454):**
> `SignUpFormScreen` (`lib/features/auth/sign_up_form_screen.dart`) wears the
> login-frame chrome — `navySurface` background with the rotated diagonal sweep
> (`surfaceTint`, 28.28°, Figma node 168:3534), the forced-LTR top controls
> (back chevron left, the **wired globe language toggle** right — D-363
> pattern), and the `SimfLogo` 44 + forum-name header over the beige
> `cardBeige` card (radius 4, padding 24, max width 400). **Logic
> byte-identical** to the pre-redesign screen (validators, generic-201 →
> email-OTP forward, enumeration resistance, error/busy states). The old
> mockup-era screen is parked in `_legacy_mockup/`.

## Layout
A single scrollable, vertically-centred auth card over the navy entry surface.

```
┌──────────────────────────────────┐
│ ‹                          [🌐] │   ← forced-LTR top row: back chevron + globe toggle
│                                  │
│     ◇  الملتقى الدولى البحرى      │   ← SimfLogo 44 + forum name (white, 24)
│                                  │
│ ┌──────────────────────────────┐ │   ← beige card (cardBeige, radius 4, pad 24)
│ │         إنشاء حساب           │ │   ← centred head, 24 SemiBold headlineInk
│ │ البريد الإلكتروني             │ │   ← 12 grey label (inline start)
│ │ ┌──────────────────────────┐ │ │
│ │ │ email (LTR text)         │ │ │   ← outlined field, beigeBorder, gold focus
│ │ └──────────────────────────┘ │ │
│ │ كلمة المرور                   │ │
│ │ ┌──────────────────────────┐ │ │
│ │ │ ••••••••            👁   │ │ │   ← obscured + eye-off show/hide
│ │ └──────────────────────────┘ │ │
│ │ تأكيد كلمة المرور             │ │
│ │ ┌──────────────────────────┐ │ │
│ │ │ ••••••••            👁   │ │ │   ← obscured + eye-off show/hide
│ │ └──────────────────────────┘ │ │
│ │ (inline server error — red)  │ │   ← only when a call failed
│ │ [        إنشاء حساب       ]  │ │   ← gold button, 48 px, white bold 16
│ │   لديك حساب؟  تسجيل الدخول    │ │   ← grey 12 + linkNavy semibold link
│ └──────────────────────────────┘ │
└──────────────────────────────────┘
```

## Components
| Component | Role | Notes |
|---|---|---|
| Back chevron | Pop / fall back to sign-in | `Icons.arrow_back_ios_new`, white, 20; forced LTR so it sits left + points left even under RTL; disabled while busy |
| Globe language toggle | AR ↔ EN switch (persisted) | 40×40 `IconButton`, `navyDeep` background, `accent` globe icon, radius 4; tooltip `languageToggleLabel`; D-363 pattern; disabled while busy |
| Logo + forum name | Branding header | `SimfLogo` 44 + `signInForumTitle` (white, 24, Medium), centred row |
| Card head | Screen name | `signUpTitle` — AR **إنشاء حساب** · EN **Sign up**; centred, 24 SemiBold, `headlineInk` |
| Field labels | Above each input | 12, Medium, `greyText`, aligned to the inline start (right under RTL) |
| Email field | Credential input | Email keyboard; **LTR-pinned** text (left-aligned); max input length 50 (counter hidden); inline validation via the form |
| Password field | Credential input | Obscured; eye-off/eye show/hide suffix; max input length 32 |
| Confirm-password field | Match input | Obscured; eye-off/eye show/hide suffix; max input length 32; keyboard submit triggers the form |
| Inline error text | Server-failure display | Red (`danger`), 12; appears between the fields and the button only after a failed call — **errors are inline, not toasts** |
| Primary button | Submit | Gold (`accent`) `FilledButton`, 48 px tall, radius 4, white bold 16 `signUpButton`; shows a 20 px white spinner + disables (50 % gold) while submitting |
| Sign-in foot | Navigate to existing-user flow | `haveAccountQuestion` (grey 12) + `signInTitle` text-link (`linkNavy`, 12 SemiBold) → sign-in screen |

Field styling (shared by all three): transparent fill, `beigeBorder` outline at
rest, `accent` (gold) outline on focus, radius 4, dense content padding
(14 horizontal / 15 vertical), input text 14 Medium `inputInk`.

## States
| State | Trigger | UI |
|---|---|---|
| **Idle / empty** | Screen opened | Empty fields, no errors. The Submit button is **always enabled** (unless busy); tapping it runs the form validators and stops on failure |
| **Validation error** | Local check fails (L-1) | Inline per-field error under the offending field (`autovalidateMode: onUserInteraction` re-checks as the user types); no call made |
| **Loading** | Submit tapped, call in flight | Button shows a spinner and disables; fields, back chevron and globe toggle disabled |
| **Success** | Generic **201** received | SnackBar `signUpCheckEmail` + push to the email-OTP screen (`/sign-up/otp`) carrying the address — **same path for new and existing email (D-198)** |
| **Error** | Network / 400 / 403 / 429 / 5xx | The server's bilingual message (or `networkErrorBody` when offline) shown **inline in red** inside the card; form kept; button re-enabled |

> There is no "you already have an account" state on this screen — the success
> path is identical regardless of whether the email is new (D-198).

## RTL (Arabic)
- The card content mirrors right-to-left: the field labels, inline error, and
  the foot row follow the ambient direction (labels use
  `AlignmentDirectional.centerStart`).
- The **top controls row is forced LTR** (back chevron stays left, globe stays
  right) so the chrome matches the Figma frame under both languages (D-363).
- The **email field is LTR-pinned** (`textDirection: ltr`, left-aligned) so the
  address reads correctly inside the RTL layout; its label stays RTL.
- The globe button toggles AR ↔ EN and persists the choice (D-363).

## Localization
All strings come from `AppL10n` (`lib/app/localization/app_l10n.dart`); no
literal text is hard-coded in widgets. Exact as-built strings:

| Key | AR | EN |
|---|---|---|
| `signUpTitle` (head) | إنشاء حساب | Sign up |
| `emailLabel` | البريد الإلكتروني | Email |
| `passwordLabel` | كلمة المرور | Password |
| `confirmPasswordLabel` | تأكيد كلمة المرور | Confirm password |
| `signUpButton` (submit) | إنشاء حساب | Create account |
| `haveAccountQuestion` | لديك حساب؟ | Have an account? |
| `signInTitle` (foot link) | تسجيل الدخول | Sign in |
| `signUpCheckEmail` (success SnackBar) | تحقق من بريدك الإلكتروني | Check your email |
| `invalidEmail` | بريد إلكتروني غير صالح | Invalid email |
| `passwordPolicyError` | كلمة المرور لا تستوفي الشروط | Password does not meet the requirements |
| `passwordsDoNotMatch` | كلمتا المرور غير متطابقتين. | The passwords do not match. |
| `networkErrorBody` (offline, inline) | تعذر الاتصال بالخادم. تحقق من الاتصال بالإنترنت وحاول مرة أخرى. | Could not reach the server. Check your internet connection and try again. |
| `signInForumTitle` (header) | الملتقى الدولى البحرى | International Maritime Forum |
| `showPasswordTooltip` | إظهار كلمة المرور | Show password |
| `hidePasswordTooltip` | إخفاء كلمة المرور | Hide password |
| `languageToggleLabel` (globe tooltip) | العربية · English | العربية · English |

> Server-side failures (400 / 403 / 429 / account-creation errors) display the
> **server's own bilingual message** inline — the app defines no local strings
> for them.
