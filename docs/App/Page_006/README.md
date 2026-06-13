# Page 006 — التحقق بالبريد · Email verification (sign-up OTP)

_Last updated: 2026-06-13 — as-built conformance pass (KSA redesign D-364; shared `OtpCodeBoxes`/`OtpMark` D-369)._

Per-page documentation folder. Everything about this app page lives here.

This screen is step 2 of sign-up: the user enters the **6-digit code emailed at
sign-up** to verify ownership of the email address and unlock the rest of the
sign-up journey. It exposes **six segmented OTP code boxes** (shared
`OtpCodeBoxes` widget), the gold **تحقّق / Verify** button pinned at the bottom
and the **لم يصلك الرمز؟ إعادة الإرسال / Didn't get the code? Resend** footer.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_006_Function.md](Page_006_Function.md) | What the user does — elements, step-by-step actions, navigation, the auth gate, acceptance criteria |
| Logic | [Page_006_Logic.md](Page_006_Logic.md) | Client + server logic, state transitions, validation, error / empty / RTL handling |
| API | [Page_006_API.md](Page_006_API.md) | The backend endpoints + DTOs that serve this page (authoritative contract) |
| Design | [Page_006_Design.md](Page_006_Design.md) | Flutter screen design — layout, components, RTL, states (loading / empty / error / success) |

## Identity
| | |
|---|---|
| Mockup page | **4-01** (`Mockup.html`, footnote "التحقق بالبريد · OTP") — owner page 006 (mockup slot 06 is the separate photo-verify screen) |
| Screen key | `emailOtp` |
| Route | `RouteNames.emailOtp` → `/sign-up/otp` |
| Titles | AR **التحقق بالبريد** · EN **Email verification** |
| Section | 1 — Authentication / sign-up |
| Nature | **OTP verification step** (6-digit emailed code) |
| App privilege | **Anonymous** (mid sign-up, no token yet) |
| Status | **Built** — Flutter `SignUpEmailVerifyScreen` wired to `verify-email` + `resend-code` (6-digit verify, resend cooldown from `codeExpiresInSeconds`, verified → sign-in); API **built**; UI rebuilt 2026-06-11 to the **KSA-Project Figma frame 505:837** (D-364); the segmented boxes + gold mark live in shared `OtpCodeBoxes`/`OtpMark` since the 2FA screen became a second consumer (D-369) |

## Sources of truth
**KSA-Project Figma frame 505:837** (visual — D-364; supersedes `Mockup.html` 4-01,
parked in `lib/features/_legacy_mockup/`) · `docs/SIMF-App-Redesign-Program.md`
(board row 4) · `SIMF_Screen_Guide_and_User_Journey` (narrative, Screen 6) ·
SIMF-API-001 §12.4 (verify-email / resend-code contract) · SIMF-MOB-API-001 (shared
API conventions + auth) · SIMF-MAA-001 (mobile architecture).

> Owner-ref note: this is **owner page 006** in the mockup numbering. The two
> endpoints it calls are already shipped (App↔CP split, routes under
> `/api/v1/app/auth/*`). Nothing on this page is "(TO BUILD)".
