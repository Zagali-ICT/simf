# Page 006 — Design (التحقق بالبريد · Email verification)

_Last updated: 2026-06-13 — as-built conformance pass (D-364/D-369)._

Flutter screen design for the sign-up email-OTP step. Behaviour is in
[Page_006_Function.md](Page_006_Function.md) / [Page_006_Logic.md](Page_006_Logic.md);
the contract is in [Page_006_API.md](Page_006_API.md).

> **As-built (KSA-Project redesign, 2026-06-11 — D-364, Figma 505:837):**
> navy `navySurface` surface with the decorative sweep and a custom header
> (back chevron + centred 24-medium title — no Material app bar); a 96 px
> gold-ringed `navyDeep` circle with the gold mail icon; **أدخل رمز التحقق**
> heading; the sent-to caption with the address in gold; **six segmented code
> boxes** (`#01132D` fill, `#1E3A5F` border, radius 14, gold focus highlight,
> one invisible capture field beneath — replaces the old single 6-digit
> field); the countdown as gold `mm:ss` + a muted-blue label; the gold تحقق
> button pinned at the bottom with the **لم يصلك الرمز؟ إعادة الإرسال** footer.
> Verify/resend/cooldown logic unchanged; the previous screen is parked in
> `lib/features/_legacy_mockup/`. The boxes + mark were extracted into the
> shared `OtpCodeBoxes`/`OtpMark` widgets
> (`lib/features/auth/widgets/otp_code_boxes.dart`) when the 2FA OTP screen
> became a second consumer (D-369).

## Layout (top → bottom)
| Zone | Content |
|---|---|
| Header band (56 px, no Material app bar) | Back chevron `arrow_back_ios_new` (white 20, **forced LTR**, disabled while a call is in flight) + centred title **التحقق بالبريد / Email verification** (white, 24, w500). |
| Mark | `OtpMark` — 96 px `navyDeep` circle with a 1.2 px gold (`accent`) ring and the gold `mail_outline` icon (34). |
| Heading | **أدخل رمز التحقق / Enter the verification code** (white, 20, w700). |
| Sent-to caption | "أرسلنا رمز التحقق إلى / We sent a verification code to" (`beigeBorder`, 14) with the address on its own line beneath in **gold** (`accent`, 14, w500, forced LTR). No digit count in the copy (D-373 — the six boxes make it obvious). |
| OTP input | `OtpCodeBoxes` — **six segmented boxes** (52 px, `navy` `#01132D` fill, radius 14, 1.5 px `#1E3A5F` border; the box at the caret highlights **gold** while focused) rendered over **one invisible capture `TextField`** (numeric keyboard, digits-only, max 6; tapping anywhere on the row focuses it; a 6-digit paste fills it). |
| Countdown (only while a resend cooldown runs) | Muted-blue (`#8A9CC0`) "إعادة الإرسال خلال / Resend in" label + gold **`mm:ss`** (forced LTR, 14, w700). |
| Inline error (conditional) | Server / network message in `danger` red (12, centred) below the boxes — rendered only when present (no reserved height). |
| Primary action (pinned at bottom) | Gold **تحقّق / Verify** `FilledButton`, full-width, disabled until 6 digits are entered. |
| Footer | **لم يصلك الرمز؟ / Didn't get the code?** (white, 14, w500) + the gold **إعادة الإرسال / Resend** `TextButton` (14, w700), disabled while a call is in flight or the cooldown runs. |

## Components
- `OtpCodeBoxes` (shared, D-369): six rendered boxes over one invisible capture
  `TextField` — **not** six fields, so there is no per-box focus auto-advance or
  backspace-to-previous; the gold highlight simply follows the caret position.
  Numeric-only (`digitsOnly` formatter), `maxLength: 6`, no cursor, no selection;
  submitting from the keyboard triggers Verify when 6 digits are present.
- `OtpMark` (shared, D-369): the gold-ringed circular mark, here with `mail_outline`.
- Primary `FilledButton` (Verify) with an inline 20 px progress spinner while a call
  is in flight.
- Footer `TextButton` (Resend) gated by a countdown timer seeded from the resend
  response's `codeExpiresInSeconds` (fallback 60 s); the countdown row renders only
  while the cooldown runs.
- Inline error `Text` (`SimfTokens.danger`, 12) rendered conditionally — no reserved
  height.

## RTL
- The screen renders under the app locale, so labels, the footer row and text
  alignment mirror under Arabic.
- **Forced LTR exceptions:** the OTP box row (digit entry is inherently LTR — box 1
  is always the first-typed digit), the email address line, the `mm:ss` countdown
  digits, and the back-chevron icon glyph.
- All strings come from `AppL10n` (AR + EN); the inline server error arrives already
  in the request's language; no hard-coded copy.

## States
| State | Visual |
|---|---|
| **Loading (verify)** | Verify button shows the spinner; the capture field, back chevron and Resend disable; no full-screen blocker. |
| **Loading (resend)** | Same `busy` flag — the (already code-gated) Verify button shows the spinner, the Resend link disables; the Resend link itself has **no** spinner. On success the cooldown countdown appears and Resend stays disabled until it elapses. |
| **Empty / incomplete** | Fewer than 6 digits → Verify disabled; no inline error, no API call. This screen has no list/empty-collection state. |
| **Error — wrong code** | The capture field **clears** (all boxes empty; no automatic refocus); inline "The verification code is not correct / رمز التحقق غير صحيح" (server message in the request language). |
| **Error — expired / attempt cap** | Inline server message; the copy itself steers the user to **Resend** ("Request a new one / a new code"). |
| **Error — resend cap (429)** | Inline cap message; no cooldown starts, so Resend is **re-enabled** once the call finishes (the server keeps rejecting until the window passes). |
| **Error — network** | Inline `networkErrorBody` ("Could not reach the server. Check your internet connection and try again." / "تعذر الاتصال بالخادم. تحقق من الاتصال بالإنترنت وحاول مرة أخرى."). A failed **verify** still clears the entered code; a failed **resend** leaves it intact. User stays on-screen. |
| **Success** | SnackBar toast "Email verified / تم التحقق من البريد"; navigate to **`/sign-in`** (verify-email issues no session — the user signs in next). |

## Notes
- No avatars, images or remote media on this screen — it is a single short form, so there is
  no skeleton/shimmer list state.
- The email address is display-only here; it is not an editable field (editing happens on screen #5).
- Decorative sweep: a rotated (28.28°) rounded rectangle in a 4 %-white tint, positioned
  off the top-right corner (Figma 505:887).
