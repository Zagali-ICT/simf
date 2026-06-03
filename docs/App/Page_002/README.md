# Page 002 — التهيئة · Onboarding (intro videos)

Per-page documentation folder. Everything about this app page lives here.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_002_Function.md](Page_002_Function.md) | What the page does — loading image, the 3 intro videos, user actions, first-run gate, acceptance criteria |
| Logic | [Page_002_Logic.md](Page_002_Logic.md) | Business rules — first-run gate, media naming, playback flow, edge cases, dependencies |
| API | [Page_002_API.md](Page_002_API.md) | The backend endpoints this page makes (**none** required) + the optional future CMS read |
| Design | [Page_002_Design.md](Page_002_Design.md) | Flutter screen design — layout, components, states, RTL, localization |

## Identity
| | |
|---|---|
| Mockup page | **2** (`Mockup.html`) — owner page 002 |
| Route | `RouteNames.onboarding` → `/onboarding` |
| Titles | AR **التهيئة** · EN **Onboarding** |
| Section | 1 — Start & entry |
| Nature | **First-run intro** (loading image → 3 intro videos, preferred YouTube, shown once) |
| App privilege | **Guest / not-logged-in** (runs before any sign-in; no auth gate) |
| Status | **Built** (Flutter slide carousel + first-run gate → sign-in); **No API** (owner); intro videos are deferred media bound by stable names (`introd_001..`) |

## Sources of truth
`Mockup.html` (visual) · `docs/App/SIMF-APP-Page-Requirements.md` Page 002 (owner capture) ·
`src/Mobile/simf_app/lib/app/router.dart` (route number / path / labels) ·
SIMF-MOB-API-001 (shared API conventions) · SIMF-MAA-001 (mobile architecture).

## Owner-ref note
This page is **owner-captured** in `docs/App/SIMF-APP-Page-Requirements.md` Page 002:
"loading image, then 3 videos (preferred from a YouTube channel), shown first-time only,
stable media names `introd_001..`, **has NO API**."

The decision is locked in `docs/decisions/DECISIONS_LOG.md` **D-249** — the App-page
documentation programme that models every owner-named page on the shipped `Page_014`.
Per D-249 the App build added **no API** for this page; the only optional future remote
swap is the existing **read-only** CMS surface `GET /app/content/{key}` (see
[Page_002_API.md](Page_002_API.md)).
