# Page 003 — تسجيل الدخول · Sign in

Per-page documentation folder. Everything about this app page lives here.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_003_Function.md](Page_003_Function.md) | What the page does — elements, user actions, navigation, acceptance criteria |
| Logic | [Page_003_Logic.md](Page_003_Logic.md) | Business rules — session window, biometric re-open, email pre-fill, OTP branch, edge cases |
| API | [Page_003_API.md](Page_003_API.md) | The backend endpoints + DTOs that serve this page (authoritative contract) |
| Design | [Page_003_Design.md](Page_003_Design.md) | Flutter screen design — layout, data binding, states, RTL, localization |

## Identity
| | |
|---|---|
| Mockup page | **3** (`Mockup.html`, owner page 003) |
| Route | `RouteNames.signIn` → `/sign-in` |
| Titles | AR **تسجيل الدخول** · EN **Sign in** |
| Section | 1 — Entry / auth screens |
| Nature | **Authentication** (email + password, biometric re-open, forgot-password OTP) |
| App privilege | **Guest** (unauthenticated entry point; promotes to Visitor/Admin on success) |
| Status | **Built** (sign-in + email-OTP + forgot/reset + biometric device-key, wired to the live API; biometric uses a Dart ES256 client — **.NET ↔ Dart crypto interop proven by a backend golden-vector test, D-266**; `local_auth` native config + secure-enclave hardening land in simf-run); app-bar carries **dark/light + language placeholder buttons** (UI only, no wiring — D-272); API **built** (contract doc reconciled to the shipped `SIMF.Contracts.Authentication` records — D-279) |

## Owner reference
This is owner page **003** "signIn", path `/sign-in`. Email field UI cap **50**, password
field UI cap **32** (client caps only — see Logic D2). Biometric (face) re-opens an
existing session inside a **5-day** window (config-bound device-key refresh, D1); when the
window has expired the email is pre-filled from the local store and the user re-enters the
password. Forgot-password emails a one-time code (OTP), verified then used to reset.
**Nafath is dropped for this screen (D4).**

## Sources of truth
`Mockup.html` (visual) · `SIMF_Screen_Guide_and_User_Journey` (narrative, Screen 3) ·
SIMF-MOB-API-001 (shared API conventions + auth) · SIMF-MAA-001 (mobile architecture) ·
SIMF-API-001 (`ApiResult<T>` envelope + error model).

> This folder follows the per-page documentation structure (`docs/App/Page_NNN/`)
> established by Page_014. It is the authoritative detail for the sign-in screen.
