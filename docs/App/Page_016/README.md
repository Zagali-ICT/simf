# Page 016 — الأجندة · Sessions (agenda)

Per-page documentation folder. Everything about this app page lives here.

Last updated: 2026-06-13 — KSA Wave-2 redesign (D-378).

> **Rename (D-271 → completed D-276):** this screen was previously titled
> **الأجندة · Agenda**. It was renamed to **الجلسات · Sessions** — the title, the
> bottom-nav label, and the two filter pills. The **API route is unchanged**
> (`/app/programme/sessions`). The Flutter route + nav-constant rename was done
> (D-276): `RouteNames.agenda` → `RouteNames.sessions`, and the path
> `/agenda` → `/sessions` (the session-detail + my-seat sub-routes follow:
> `/sessions/:sessionId[/my-seat]`).
>
> **KSA redesign (D-378, 2026-06-13, commit `8a0387f`):** the screen was rebuilt
> to the KSA-Project Figma frame **215:767 "Calander"** on the shared `KsaPage`
> shell. The visible header title and the bottom-nav label are now
> **الأجندة · Agenda** again (l10n `navAgenda`), and the two pills carry the
> frame copy **أجندة الفعالية / الأجندة القادمة** ("Event agenda" /
> "Upcoming agenda"). The route name/path stay `sessions` / `/sessions` and the
> API is untouched — the D-378 re-title is UI strings only.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_016_Function.md](Page_016_Function.md) | What the user does — search, view pills, day strip, tap-through |
| Logic | [Page_016_Logic.md](Page_016_Logic.md) | The cached-full-programme model, client-side filters, field mapping, the category/"main session" type, the speaker country+photo, status |
| API | [Page_016_API.md](Page_016_API.md) | The backend endpoints + DTOs (authoritative contract) |
| Design | [Page_016_Design.md](Page_016_Design.md) | Flutter screen design — KSA frame 215:767 layout, day strip, time-chip rows, RTL, states |

## Identity
| | |
|---|---|
| Mockup page | **16** (`Mockup.html`, line ~1193) — superseded visually by KSA frame **215:767** (D-378) |
| Route | `RouteNames.sessions` → `/sessions` (renamed from `agenda` / `/agenda`, D-276) |
| Titles | Header + nav label: AR **الأجندة** · EN **Agenda** (`l10n.navAgenda`, D-378) — the D-271 **الجلسات · Sessions** name survives as the route-table label only |
| Section | 2 — Core screens |
| Nature | **Filterable schedule of all sessions** (view pills + day selector + search) |
| App privilege | **Guest and above** — anonymous; a guest can browse the sessions list (Screen Guide Journey C) |
| Status | API **BUILT** (enriched, D-252; speaker country+photo, D-271); **Flutter screen BUILT (D-299), redesigned to KSA Wave-2 frame 215:767 (D-378, commit `8a0387f`)** — same fetch-once + client-side pills/day-strip/search contract on the shared shell (bordered search field, gold/navy view pills with **Upcoming default**, white day strip with re-tap-to-clear + red Fri/Sat weekday labels, two-line time chips, gold zero-padded row numbers; old screen parked in `_legacy_mockup/`) |

## Sources of truth (read first)
KSA-Project Figma frame **215:767** + `docs/SIMF-App-Redesign-Program.md` W2-7
(the visual, D-378) · `Mockup.html` screen 16 (historical) ·
`SIMF_Screen_Guide_and_User_Journey` SCREEN16/17 (the narrative) ·
SIMF-MOB-API-001 (shared API conventions + auth) · `DECISIONS_LOG` D-199 (the
public programme reads) + **D-252** (the agenda enrichment) + **D-271** (the
الجلسات rename + the speaker country-flag + photo) + **D-378** (the KSA
Wave-2 rebuild).

## Headline (owner directive, 2026-06-03)
> "We must have a **calendar to filter in the UI** — only open / all-active and
> remaining days, with a session preview. But the **API must return the full
> programme and [the app] can cache it**. The API returns each item as: Date,
> Code, Title, Body, Hall, is-main-session-or-not / type, and the speaker list.
> **Allowed for guest / not-logged-in.**"

The filters live in the **UI**; the API returns the **whole programme once** and
the app caches it (see [Page_016_Logic.md](Page_016_Logic.md)).
