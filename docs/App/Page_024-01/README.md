# Page 024-01 — تفاصيل النسخة · Past-edition detail

Per-page documentation folder. Everything about this app page lives here.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_024-01_Function.md](Page_024-01_Function.md) | What the user does — open one past edition, read its title/summary/place/date/counts |
| Logic | [Page_024-01_Logic.md](Page_024-01_Logic.md) | The visibility gate, the deferred rich lists, the list → detail hop |
| API | [Page_024-01_API.md](Page_024-01_API.md) | The backend endpoint + DTO this page reads (authoritative contract) — **NEW (D-273)** |
| Design | [Page_024-01_Design.md](Page_024-01_Design.md) | Flutter screen design — cover, title, place/date, counters, deferred sections, RTL, states |

## Identity
| | |
|---|---|
| Mockup page | **24-01** (`Mockup.html`, line ~1678 — `تفاصيل النسخة`) |
| Route | `RouteNames.archiveDetail` *(planned)* → `/archive/:editionId` *(planned)* (**anonymous** — public). The constant/path do **not** exist in `route_names.dart` yet — *Flutter wiring deferred, todo #9* |
| Titles | AR **تفاصيل النسخة** (app bar shows the edition, e.g. **أرشيف 2024**) · EN **Past-edition detail** |
| Section | 3 — المحتوى والفعاليات (Content & events): Booths (22) · Sponsors (23) · Archive (24) · **Archive Detail (24-01)** |
| Nature | **Read-only public detail** of one past forum edition |
| App privilege | **Anonymous — public.** No login; reads are open like the Archive list (24). Gated only by the archive-visibility operations toggle (D-166). |
| Status | API **BUILT — NEW endpoint (D-273)**; Flutter screen is a mockup (wiring deferred to the coordinated Flutter pass) |

## Sources of truth (read first)
`Mockup.html` screen 24-01 (the visual, line ~1678) · the Archive list screen 24
(line ~1619, the parent) · SIMF-MOB-API-001 (shared API conventions + the public
read posture) · `DECISIONS_LOG` **D-199** (the `ArchiveEdition` entity + the public
list + the archive-visibility gate D-166) + **D-273** (this page — the per-edition
detail read + the `LocationEn/Ar` + `DateLabelEn/Ar` columns).

## Headline (§9, owner directive)
> Screen 24-01 "تفاصيل النسخة" — tapping a past edition on the Archive list opens
> its detail: cover + **title** + **نبذة** (summary) + **المكان** (place) +
> **الزمن** (date label) + the three counters (الفعاليات / الحضور / المتحدثون).

The detail comes from **one** anonymous call
(`GET /app/archive/{id}` → `PublicArchiveEditionDetail`, D-273). The rich lists the
mockup also sketches — **الصور والفيديو** (gallery), **عناوين الجلسات** (session
titles), **المتحدثون السابقون** (past speakers) — are **deferred** (the
`ArchiveEdition` entity does not yet model them; §9 / D-273). See
[Page_024-01_Logic.md](Page_024-01_Logic.md) and [Page_024-01_API.md](Page_024-01_API.md).
