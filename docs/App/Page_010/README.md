# Page 010 — تم التسجيل بنجاح · Registration success

Per-page documentation folder. Everything about this app page lives here.

> Last updated: 2026-06-13 — conformance pass to the as-built code (D-366 / D-369 / D-373).

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_010_Function.md](Page_010_Function.md) | What the page does — the confirmation message, user actions, navigation, acceptance criteria |
| Logic | [Page_010_Logic.md](Page_010_Logic.md) | Business rules — when it shows, the reference-number rule, the contact-tile gating, edge cases |
| API | [Page_010_API.md](Page_010_API.md) | The backend endpoints this page may call (authoritative contract) |
| Design | [Page_010_Design.md](Page_010_Design.md) | Flutter screen design — layout, components, states, RTL, localization |

## Identity
| | |
|---|---|
| Mockup page | **10** (`Mockup.html`) |
| Route | `RouteNames.registrationSuccess` → `/registration/success` |
| Titles | AR **تم التسجيل بنجاح** · EN **Registration success** (header band: AR **تم التسجيل** · EN **Registered**) |
| Section | 1 — Onboarding / sign-up |
| Nature | **Transitional confirmation** (terminal step of the sign-up; "wait for approval") |
| App privilege | **Signed-in, pending approval** (account just created, not yet Approved); route 10 is in the router's `_authenticatedRoutes` gate |
| Status | **🟢 Screen rebuilt to the KSA-Project design** (D-366, Figma 505:1451) — contact tiles wired via config (D-369); reference card shows the real DB-issued reference (D-373). **Zero API calls from this screen.** |

## As built (Flutter — D-366 redesign + D-369 tiles + D-373 reference)
`RegistrationSuccessScreen` (`lib/features/registration/registration_success_screen.dart`,
a `StatelessWidget`, route `registrationSuccess` → `/registration/success`, auth-gated)
is the KSA-Project success frame: navy `navySurface` surface + decorative sweep, a
custom header band (back chevron + centred **تم التسجيل / Registered** — no Material
app bar), a 104 px `navyDeep` circle with a `#22C55E` green ring + check, the success
headline + two-line review copy, the **reference card** (`#01132D` at 80%, radius 8) —
it renders the **real DB-issued `SIMF-YYYY-NNNNNNNN` registration reference** carried
from the Page 007-01 save as the route **extra** (D-373); the literal mask
`SIMF-2026-xxxx` renders only as the no-data fallback (offline / out-of-flow arrival) —
a gold **حالة التسجيل / Registration status** `FilledButton` (→ Page_011) and an
accent-outlined **الانتقال للرئيسية / Go to home** `OutlinedButton` (→ home), the
**تواصل معانا / Contact us** phone + mail tiles — **wired via config** (D-369):
`BuildConfig.supportPhone` / `supportEmail` (`--dart-define` `SIMF_SUPPORT_PHONE` /
`SIMF_SUPPORT_EMAIL`, empty defaults) gate them; a non-empty value opens the OS
dialer / mail app through `url_launcher` (best-effort, failures swallowed), an empty
value keeps the tile **inert** — and the `@SIMF_RSNF` footer. The screen is reached
via **`goNamed` replacement** navigation from the interests-step save, so the sign-up
form is off the back stack; the header chevron pops if possible, otherwise goes home.
It owns **no API** (Page_011 owns the status read). The pre-redesign screen is parked
in `lib/features/_legacy_mockup/registration_success_screen.dart`.

## Sources of truth
KSA-Project Figma frame **505:1451** (visual, D-366) · `docs/SIMF-App-Redesign-Program.md`
(board row 6 + the D-369 contact-tiles item) · `SIMF_Screen_Guide_and_User_Journey`
(narrative, Screen 10) · SIMF-MOB-API-001 (shared API conventions + auth) ·
SIMF-MAA-001 (mobile architecture).

> **Owner reference:** owner page **010** "registrationSuccess". This screen is the
> success/confirmation shown immediately after the profile save (Page 007-01
> interests step), telling the user their registration was received and is under
> review, and showing their registration reference number.
