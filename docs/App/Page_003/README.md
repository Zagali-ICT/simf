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
| Status | **Built — KSA-Project redesign promoted (D-358/D-360, Figma node 168:2800)**: navy screen + beige card, remember-me checkbox gating the email prefill store, Face-ID button always visible (silent fallback when unavailable), back chevron, post-sign-in best-effort device-key enrolment; the previous mockup screen is parked in `lib/features/_legacy_mockup/`. Sign-in + email-OTP + forgot/reset + biometric device-key wired to the live API (Dart ES256 client — **.NET ↔ Dart interop proven by a backend golden-vector test, D-266**; `local_auth` native config lands in simf-run). The app-bar dark/light + language placeholder buttons (D-272) were **dropped with the redesign** (owner decision, D-360 — the design has no app bar). The **"Browse without signing in"** guest link is kept below the Face-ID button (owner-approved addition to the frame; only guest entry — D-325/D-360). API **built** (contract reconciled — D-279) |

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
