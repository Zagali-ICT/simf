# Page 005 — إنشاء حساب · Sign up (signUpForm)

Per-page documentation folder. Everything about this app page lives here.

The sign-up form is **step 1** of account creation: the visitor enters an email,
a password, and a password confirmation. Submitting creates a new **Visitor**
account with **no privilege**, in an **under-review** state that still has to
complete its profile, and the server emails a **6-digit OTP** to verify the
address. To resist account enumeration (D-198) the response is **always a
generic 201** — no `409` is ever returned — so the app always shows the same
"check your email" screen, even when the email is already registered.

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
| Route | `RouteNames.signUp` → `/sign-up` |
| Titles | AR **إنشاء حساب** · EN **Sign up** |
| Section | 1 — Authentication |
| Nature | **Form** (sign-up step 1 — email + password + confirm) |
| App privilege | **Guest** (unauthenticated; the screen creates the account) |
| Outcome | New **Visitor**, no privilege, under review, profile incomplete; **6-digit OTP** emailed |
| Status | API **exists** (`POST /app/auth/sign-up`); design **drafted** |

## Sources of truth
`Mockup.html` (visual, screen 5) · `SIMF_Screen_Guide_and_User_Journey` (narrative, Screen 5) ·
SIMF-MOB-API-001 (shared API conventions + auth) · SIMF-MAA-001 (mobile architecture) ·
DECISIONS_LOG **D-198** (enumeration-resistant sign-up).

> **Enumeration resistance (D-198) — read before changing anything.** The server
> returns a **generic 201** with no body distinction whether the email is new or
> already registered. There is **no `409 Conflict`**. The Flutter
> "you already have an account" branch is therefore **dead code** — the app must
> route to the generic OTP / "check your email" screen on every success.
