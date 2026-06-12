# Page 016 — الجلسات · Sessions (daily schedule)

Per-page documentation folder. Everything about this app page lives here.

> **Rename (D-271 → completed D-276):** this screen was previously titled
> **الأجندة · Agenda**. It is renamed to **الجلسات · Sessions** — the title, the
> bottom-nav label, and the two filter pills (now **الجلسات القادمة** /
> **جلسات الفعالية**). The **API route is unchanged** (`/app/programme/sessions`).
> The Flutter route + nav-constant rename **is now done** (D-276):
> `RouteNames.agenda` → `RouteNames.sessions`, and the path `/agenda` → `/sessions`
> (the session-detail + my-seat sub-routes follow: `/sessions/:sessionId[/my-seat]`).

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_016_Function.md](Page_016_Function.md) | What the user does — filter pills, day strip, search, tap-through |
| Logic | [Page_016_Logic.md](Page_016_Logic.md) | The cached-full-programme model, client-side filters, field mapping, the category/"main session" type, the speaker country+photo, status |
| API | [Page_016_API.md](Page_016_API.md) | The backend endpoints + DTOs (authoritative contract) |
| Design | [Page_016_Design.md](Page_016_Design.md) | Flutter screen design — layout, list rows, the speaker flag+avatar, RTL, states |

## Identity
| | |
|---|---|
| Mockup page | **16** (`Mockup.html`, line ~1193) |
| Route | `RouteNames.sessions` → `/sessions` (renamed from `agenda` / `/agenda`, D-276) |
| Titles | AR **الجلسات** · EN **Sessions** *(renamed from الأجندة · Agenda, D-271)* |
| Section | 2 — Core screens |
| Nature | **Filterable schedule of all sessions** (day selector + search) |
| App privilege | **Guest and above** — anonymous; a guest can browse the sessions list (Screen Guide Journey C) |
| Status | API **BUILT** (enriched, D-252; speaker country+photo, D-271); **Flutter screen BUILT (D-299), redesigned to KSA Wave-2 frame 215:767 (D-378)** — same fetch-once + client-side pills/day-strip/search contract on the shared shell (white day strip with re-tap-to-clear, two-line time chips, gold numbered titles; old screen parked in `_legacy_mockup/`) |

## Sources of truth (read first)
`Mockup.html` screen 16 (the visual) · `SIMF_Screen_Guide_and_User_Journey`
SCREEN16/17 (the narrative) · SIMF-MOB-API-001 (shared API conventions + auth) ·
`DECISIONS_LOG` D-199 (the public programme reads) + **D-252** (the agenda
enrichment) + **D-271** (the **الجلسات · Sessions** rename + the speaker
country-flag + photo on the session).

## Headline (owner directive, 2026-06-03)
> "We must have a **calendar to filter in the UI** — only open / all-active and
> remaining days, with a session preview. But the **API must return the full
> programme and [the app] can cache it**. The API returns each item as: Date,
> Code, Title, Body, Hall, is-main-session-or-not / type, and the speaker list.
> **Allowed for guest / not-logged-in.**"

The filters live in the **UI**; the API returns the **whole programme once** and
the app caches it (see [Page_016_Logic.md](Page_016_Logic.md)).
