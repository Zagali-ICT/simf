# Page 006 — Design (التحقق بالبريد · Email verification)

Flutter screen design for the sign-up email-OTP step. Behaviour is in
[Page_006_Function.md](Page_006_Function.md) / [Page_006_Logic.md](Page_006_Logic.md);
the contract is in [Page_006_API.md](Page_006_API.md).

## Layout (top → bottom)
| Zone | Content |
|---|---|
| App bar | Back affordance (→ screen #5) + screen title **التحقق بالبريد / Email verification**. |
| Header block | Mail/lock illustration or icon, then heading and a subtitle echoing the target address: "We sent a 6-digit code to **{email}**" / "أرسلنا رمزًا من 6 أرقام إلى **{email}**". |
| OTP input | A single **6-box** code field — one digit per box, numeric keyboard, auto-advance, paste-fills-all. |
| Inline message | Reserved space below the boxes for the bilingual validation / server error. |
| Primary action | **Verify** (تحقّق) — full-width button, disabled until 6 digits entered. |
| Secondary action | **Resend code** (إعادة إرسال الرمز) text button + cooldown countdown ("Resend in {n}s" / "إعادة الإرسال خلال {n} ث"). |

## Components
- 6-cell OTP widget (boxed style), numeric-only, with focus auto-advance and backspace-to-previous.
- Primary `ElevatedButton` (Verify) with an inline progress spinner while the call is in flight.
- `TextButton` (Resend) gated by a countdown timer seeded from `codeExpiresInSeconds`.
- Inline error `Text` styled as error, occupying reserved height so the layout does not jump.

## RTL
- Under Arabic the whole screen mirrors: app bar back arrow, header, button order and text alignment flip.
- **Exception:** the OTP boxes remain **left-to-right** (digit entry is inherently LTR) even in RTL, so box 1 is always the first-typed digit. Labels, subtitle and the resend line mirror.
- All strings come from localized resources (AR + EN); no hard-coded copy.

## States
| State | Visual |
|---|---|
| **Loading (verify)** | Verify button shows a spinner; OTP boxes + Resend disabled; no full-screen blocker. |
| **Loading (resend)** | Resend button shows a spinner; on return, cooldown countdown begins and Resend disables. |
| **Empty / incomplete** | Fewer than 6 digits → Verify disabled; no inline error, no API call. This screen has no list/empty-collection state. |
| **Error — wrong code** | Boxes clear and refocus box 1; bilingual inline "The verification code is not correct / رمز التحقق غير صحيح". |
| **Error — expired / attempt cap** | Bilingual inline message; visual emphasis steers the user to **Resend** rather than retry. |
| **Error — resend cap** | Bilingual cap message; Resend stays disabled. |
| **Error — network / 500 / 429** | Bilingual generic / throttle message; inputs keep their value for retry; user stays on-screen. |
| **Success** | Toast "Email verified / تم التحقق من البريد"; navigate forward in the sign-up flow. |

## Notes
- No avatars, images or remote media on this screen — it is a single short form, so there is
  no skeleton/shimmer list state.
- The email address is display-only here; it is not an editable field (editing happens on screen #5).
