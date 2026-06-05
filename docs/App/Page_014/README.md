# Page 014 — منطقتي · My Area (dashboard)

Per-page documentation folder. Everything about this app page lives here.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_014_Function.md](Page_014_Function.md) | What the page does — elements, user actions, navigation, acceptance criteria |
| Logic | [Page_014_Logic.md](Page_014_Logic.md) | Business rules — counter definitions, role gating, data sources, edge cases, dependencies |
| API | [Page_014_API.md](Page_014_API.md) | The backend endpoints + DTOs that serve this page (authoritative contract) |
| Design | [Page_014_Design.md](Page_014_Design.md) | Flutter screen design — layout, data binding, states, share intents, localization |

## Identity
| | |
|---|---|
| Mockup page | **14** (`Mockup.html`) |
| Route | `RouteNames.myArea` → `/my-area` |
| Titles | AR **منطقتي** · EN **My Area** |
| Section | 2 — Core screens |
| Nature | **Personal dashboard** (identity + counters + today's schedule + share) |
| App privilege | **Visitor** and above (signed-in-pending = limited) |
| Status | **Flutter screen BUILT (D-297)**; API **BUILT (D-249)** — dashboard + `.ics`/`.vcf` exports |

## Sources of truth
`Mockup.html` (visual) · `SIMF_Screen_Guide_and_User_Journey` (narrative, Screen 14) ·
SIMF-MOB-API-001 (shared API conventions + auth) · SIMF-MAA-001 (mobile architecture).

> This folder is the first instance of the per-page documentation structure
> (`docs/App/Page_NNN/`). It supersedes the per-screen detail that previously sat
> inside the monolithic SIMF-MOB-API-001 §6 / SIMF-MOB-SDS-001, which now point here.

## As-built (D-297)

The Flutter `MyAreaScreen` (`features/myarea/my_area_screen.dart`) replaces the
`ComingSoonScreen` placeholder. An **Approved** user loads
`GET /app/account/dashboard` (`MyAreaRepository.getDashboard`) and sees the
identity card, the two counters, today's merged schedule, the two share tiles,
and the Badge/Settings utility links. A signed-in **pending/rejected** user — and
the 403 edge — falls back to the **limited card** from the cached identity with no
dashboard call (Logic L-5). **Share** is wired for real: the `.vcf`/`.ics` exports
are fetched as **raw text** via the new additive `SimfApiClient.getText()`, written
to a `Directory.systemTemp` temp file, and handed to the native share sheet
(`share_plus`). Schedule **Session** rows route to Session detail (17); **Meeting**
rows are non-tappable (no detail page yet). **Interim UI:** the avatar is rendered
as **initials** and the `pageColor` tier accent uses the token accent — the carried
`avatarUrl`/`pageColor` are deferred to SIMF-VID-001 to keep the skeleton free of a
network-image fetch. Tests: `my_area_screen_test.dart` (7) +
`myarea_models_test.dart` (2).
