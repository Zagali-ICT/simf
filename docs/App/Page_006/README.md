# Page 006 — التحقق بالبريد · Email verification (sign-up OTP)

Per-page documentation folder. Everything about this app page lives here.

This screen is step 2 of sign-up: the user enters the **6-digit code emailed at
sign-up** to verify ownership of the email address and unlock the rest of the
sign-up journey. It exposes a 6-box OTP entry, a **Resend code** action and a
**Verify** button.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_006_Function.md](Page_006_Function.md) | What the user does — elements, step-by-step actions, navigation, the auth gate, acceptance criteria |
| Logic | [Page_006_Logic.md](Page_006_Logic.md) | Client + server logic, state transitions, validation, error / empty / RTL handling |
| API | [Page_006_API.md](Page_006_API.md) | The backend endpoints + DTOs that serve this page (authoritative contract) |
| Design | [Page_006_Design.md](Page_006_Design.md) | Flutter screen design — layout, components, RTL, states (loading / empty / error / success) |

## Identity
| | |
|---|---|
| Mockup page | **6** (`Mockup.html`) — owner page 006 |
| Screen key | `emailOtp` |
| Route | `RouteNames.emailOtp` → `/sign-up/otp` |
| Titles | AR **التحقق بالبريد** · EN **Email verification** |
| Section | 1 — Authentication / sign-up |
| Nature | **OTP verification step** (6-digit emailed code) |
| App privilege | **Anonymous** (mid sign-up, no token yet) |
| Status | API **built** (`POST /app/auth/verify-email`, `POST /app/auth/resend-code`); design **drafted** |

## Sources of truth
`Mockup.html` (visual) · `SIMF_Screen_Guide_and_User_Journey` (narrative, Screen 6) ·
SIMF-API-001 §12.4 (verify-email / resend-code contract) · SIMF-MOB-API-001 (shared
API conventions + auth) · SIMF-MAA-001 (mobile architecture).

> Owner-ref note: this is **owner page 006** in the mockup numbering. The two
> endpoints it calls are already shipped (App↔CP split, routes under
> `/api/v1/app/auth/*`). Nothing on this page is "(TO BUILD)".
