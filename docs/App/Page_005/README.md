# Page 005 — إنشاء حساب · Sign up (signUpForm)

Per-page documentation folder. Everything about this app page lives here.

*Last updated: 2026-06-13 — as-built conformance pass against the KSA-Project redesign (W2-1, D-370).*

The sign-up form is **step 1** of account creation: the visitor enters an email,
a password, and a password confirmation. Submitting creates a new **Visitor**
account with **no privilege**, in the **`Registered`** state (email not yet
verified — review/approval comes later in the lifecycle), with **no profile
yet**, and the server emails a **6-digit OTP** to verify the address. To resist
account enumeration (D-198) the response is **always a generic 201** — no `409`
is ever returned — so the app always shows the same "check your email" step,
even when the email is already registered.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_005_Function.md](Page_005_Function.md) | What the user does — fields, actions, navigation, the privilege/auth gate, acceptance criteria |
| Logic | [Page_005_Logic.md](Page_005_Logic.md) | Client + server logic, state transitions, validation, enumeration-resistance, error/empty/RTL handling |
| API | [Page_005_API.md](Page_005_API.md) | The backend endpoint + DTOs that serve this page (authoritative contract) |
| Design | [Page_005_Design.md](Page_005_Design.md) | Flutter screen design — layout, components, states, RTL, localization |

## Identity
| | |
|---|---|
| Mockup page | **5** (`Mockup.html`) — owner page 005 |
| Design frame | KSA-Project Figma **168:3454** (Wave 2 W2-1, D-370) |
| Route | `RouteNames.signUpForm` → `/sign-up` |
| Titles | AR **إنشاء حساب** · EN **Sign up** |
| Section | 1 — Authentication |
| Nature | **Form** (sign-up step 1 — email + password + confirm) |
| App privilege | **Guest** (unauthenticated; the screen creates the account) |
| Outcome | New **Visitor**, no privilege, **`Registered`** (unverified), no profile yet; **6-digit OTP** emailed (10-minute lifetime) |
| Status | **Built** — Flutter `SignUpFormScreen` (`lib/features/auth/sign_up_form_screen.dart`) rebuilt 2026-06-12 to the KSA-Project frame 168:3454 (W2-1, D-370 — login chrome + beige card; logic byte-identical), wired to `POST /app/auth/sign-up` via `AuthController.signUp` (client validation → generic 201 → email-OTP screen, D-198/D-270); API **built**; the previous mockup-era screen is parked in `_legacy_mockup/` |

## Sources of truth
KSA-Project Figma frame **168:3454** (visual, W2-1 — supersedes `Mockup.html` screen 5) ·
`SIMF_Screen_Guide_and_User_Journey` (narrative, Screen 5) ·
SIMF-MOB-API-001 (shared API conventions + auth) · SIMF-MAA-001 (mobile architecture) ·
DECISIONS_LOG **D-198** (enumeration-resistant sign-up) · **D-370** (KSA-Project rebuild, W2-1).

> **Enumeration resistance (D-198) — read before changing anything.** The server
> returns a **generic 201** with no body distinction whether the email is new,
> already registered but unverified (restart), or already verified (deflect).
> There is **no `409 Conflict`** and the Flutter screen has **no
> "you already have an account" branch** — the app routes to the generic
> email-OTP / "check your email" screen on every success.
