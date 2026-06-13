# Page 003 — تسجيل الدخول · Sign in

Per-page documentation folder. Everything about this app page lives here.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_003_Function.md](Page_003_Function.md) | What the page does — elements, user actions, navigation, acceptance criteria |
| Logic | [Page_003_Logic.md](Page_003_Logic.md) | Business rules — boot/session restore, biometric re-open, remember-me email pre-fill, OTP branch, post-auth profile gate, edge cases |
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
| Status | **Built — KSA-Project redesign promoted (D-358/D-360, Figma node 168:2800)**: navy screen + beige card, remember-me checkbox gating the email prefill store, Face-ID button always visible (silent fallback when unavailable), back chevron, post-sign-in best-effort device-key enrolment; the previous mockup screen is parked in `lib/features/_legacy_mockup/`. Sign-in + email-OTP + forgot/reset + biometric device-key wired to the live API (Dart ES256 client — **.NET ↔ Dart interop proven by a backend golden-vector test, D-266**; `local_auth` native config lands in simf-run). The frame's 2026-06-11 update (D-363) added a **globe language toggle** top-right (40×40 `navyDeep` square, gold globe — **wired** to `LocaleController`, AR ↔ EN persisted, superseding the old D-272 unwired placeholders) and made the guest entry design-native: the underlined **"الدخول كزائر"** link under the Face-ID button (only guest entry — D-325/D-360/D-363). The 2FA email-OTP step (`/auth/verify-otp`) was restyled to the shared KSA OTP frame — `OtpCodeBoxes`/`OtpMark` widgets — with the verify contract untouched (D-369). Post-login routing now rides the server-computed `profileComplete` flag on the sign-in hydration (`routeAfterAuth`, D-374 — replaced the old D-288 client probe); the forgot/reset screens were rebuilt on the shared `KsaAuthScaffold` chrome (D-374). API **built** (contract reconciled — D-279) |

## Owner reference
This is owner page **003** "signIn", path `/sign-in`. Email field UI cap **50**, password
field UI cap **32** (client caps only — see Logic D2). Biometric (face) signs the user in
via the **device-key challenge** path (tap the Face-ID button → on-device prompt →
challenge → ES256 signature → fresh tokens, no typed password); the owner's original
**5-day window** is **not implemented as-built** — the button is always rendered per the
KSA design (D-360) and the device-key stays usable until revoked (Logic D1). The email is
pre-filled from the local store when the **remember-me** checkbox stored it on the last
successful sign-in. Forgot-password emails a one-time code (OTP), verified then used to
reset. **Nafath is dropped for this screen (D4).**

## Sources of truth
`Mockup.html` (visual) · `SIMF_Screen_Guide_and_User_Journey` (narrative, Screen 3) ·
SIMF-MOB-API-001 (shared API conventions + auth) · SIMF-MAA-001 (mobile architecture) ·
SIMF-API-001 (`ApiResult<T>` envelope + error model).

> This folder follows the per-page documentation structure (`docs/App/Page_NNN/`)
> established by Page_014. It is the authoritative detail for the sign-in screen.

*Last updated: 2026-06-13 — as-built conformance pass against the shipped KSA-Project
sign-in (D-360/D-363) + restyled 2FA OTP step (D-369) + D-374 post-auth profile gate.*
