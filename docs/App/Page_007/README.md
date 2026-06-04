# Page 007 — إنشاء حساب · زائر · Sign up — visitor (profile completion)

Per-page documentation folder. Everything about this app page lives here.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_007_Function.md](Page_007_Function.md) | What the page does — elements, user steps, the interests sub-step, navigation, acceptance criteria |
| Logic | [Page_007_Logic.md](Page_007_Logic.md) | Business rules — auth gate, lookup sources, validation, state transitions, edge cases, dependencies |
| API | [Page_007_API.md](Page_007_API.md) | The backend endpoints + DTOs that serve this page (authoritative contract) |
| Design | [Page_007_Design.md](Page_007_Design.md) | Flutter screen design — layout, components, states, RTL, localization |

## Identity
| | |
|---|---|
| Mockup page | **05 (+ 5-01 interests + 06 ID photo)** (`Mockup.html`) — owner page **007** (mockup screen **07** is a different, seat/media-row screen) |
| Route | `RouteNames.signUpVisitor` → `/sign-up/visitor` |
| Titles | AR **إنشاء حساب · زائر** · EN **Sign up — visitor** |
| Section | 1 — Onboarding / account |
| Nature | **Profile completion** (lookups + the interests sub-step → mark complete → wait for approval) |
| App privilege | **AUTH-only** — any signed-in account; **no role / no permission code** (D7) |
| Status | API **built** (profile upsert + four lookups); design **drafted** |

## Owner reference note
The interests picker (cards, min 1 / max 10) is a **sub-step of this screen**, not a
separate route. The owner called it "Page 008", but there is **no standalone
`/sign-up/visitor/interests` route** — it renders inside this page's flow (**D12**).
"COMPANY" was dropped from the form: the الجهة field is the **organisation lookup**
(**D-220 / D-221 / D6**). Interests persist through the **profile upsert**, not a
separate write; the app supplies the actor from the cached sign-in (userId / email),
so the body carries **no** user id (**D7**).

## Sources of truth
`Mockup.html` (visual) · `SIMF_Screen_Guide_and_User_Journey` (narrative, Screen 05 + 5-01 interests + 06 ID photo) ·
SIMF-MOB-API-001 (shared API conventions + auth) · SIMF-MAA-001 (mobile architecture) ·
DECISIONS_LOG D-046/D-049/D-050/D-186/D-190/D-220/D-221.

> This folder follows the per-page documentation structure (`docs/App/Page_NNN/`).
> It supersedes any per-screen sign-up detail that previously sat inside the
> monolithic SIMF-MOB-API-001, which now points here.
