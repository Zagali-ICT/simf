# Page 010 — تم التسجيل بنجاح · Registration success

Per-page documentation folder. Everything about this app page lives here.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_010_Function.md](Page_010_Function.md) | What the page does — the confirmation message, user actions, navigation, acceptance criteria |
| Logic | [Page_010_Logic.md](Page_010_Logic.md) | Business rules — when it shows, the pending-approval state, optional status poll, edge cases |
| API | [Page_010_API.md](Page_010_API.md) | The backend endpoints this page may call (authoritative contract) |
| Design | [Page_010_Design.md](Page_010_Design.md) | Flutter screen design — layout, components, states, RTL, localization |

## Identity
| | |
|---|---|
| Mockup page | **10** (`Mockup.html`) |
| Route | `RouteNames.registrationSuccess` → `/registration/success` |
| Titles | AR **تم التسجيل بنجاح** · EN **Registration success** |
| Section | 1 — Onboarding / sign-up |
| Nature | **Transitional confirmation** (terminal step of the 4-step sign-up; "wait for approval") |
| App privilege | **Signed-in, pending approval** (account just created, not yet Approved) |
| Status | **🟢 Screen built** (D-291) — Flutter `RegistrationSuccessScreen`; transitional only, no own write API (the optional status poll lives on Page_011) |

## As built (Flutter, D-291)
`RegistrationSuccessScreen` (route `registrationSuccess` → `/registration/success`,
auth-gated) is a static, offline-safe confirmation: a success check + the
"received / under admin review" message + a primary **حالة التسجيل / Registration
status** button (→ Page_011) and a ghost **الانتقال للرئيسية / Go to home** button
(→ home). It has **no app-bar back** and is reached via a **replacement**
navigation, so the multi-step sign-up form is off the back stack. It owns no API
(the optional auto-advance poll is deferred to Page_011, which owns the real status
polling — Page_010 L-3). The Page_007 profile save now routes **here** (then on to
Page_011), matching the documented sign-up flow.

## Sources of truth
`Mockup.html` (visual) · `SIMF_Screen_Guide_and_User_Journey` (narrative, Screen 10) ·
SIMF-MOB-API-001 (shared API conventions + auth) · SIMF-MAA-001 (mobile architecture).

> **Owner reference:** owner page **010** "registrationSuccess". This screen is the
> success/confirmation shown immediately after profile completion (Page 009), telling
> the user their registration was received and is awaiting admin approval.
