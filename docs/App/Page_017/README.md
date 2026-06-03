# Page 017 — تفاصيل الجلسة · Session detail

Per-page documentation folder. Everything about this app page lives here.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_017_Function.md](Page_017_Function.md) | What the user does — read the session, view my seat, add to calendar, set a reminder, open a speaker |
| Logic | [Page_017_Logic.md](Page_017_Logic.md) | The render-from-cache model, the my-seat card rule, the two client-local functions, the speaker country+photo, the related live/Q&A/comments surfaces, field mapping |
| API | [Page_017_API.md](Page_017_API.md) | The backend endpoints + DTOs this page reads (authoritative contract) — **all already built** |
| Design | [Page_017_Design.md](Page_017_Design.md) | Flutter screen design — layout, the seat card, CTAs, RTL, states |

## Identity
| | |
|---|---|
| Mockup page | **17** (`Mockup.html`, line ~1230) |
| Route | `RouteNames.sessionDetail` → `/agenda/:sessionId` |
| Titles | AR **تفاصيل الجلسة** · EN **Session detail** |
| Section | 2 — Core screens |
| Nature | **Full detail for one session** + my-seat card + add-to-calendar / reminder |
| App privilege | **Guest and above** — the detail is anonymous (a guest can read it). The **my-seat card is login-only** (an approved account with an active reservation). |
| Status | API **BUILT** (reuses existing endpoints — D-265; speaker country+photo + live-stream URLs appended, D-271); Flutter screen is a mockup |

## Sources of truth (read first)
`Mockup.html` screen 17 (the visual) · `SIMF_Screen_Guide_and_User_Journey`
SCREEN17 (the narrative) · SIMF-MOB-API-001 (shared API conventions + auth) ·
`DECISIONS_LOG` D-199 (the public programme reads) + **D-252** (the cached
list now carries body + speakers) + **D-175** (per-session seat reservations,
`MyCell`) + **D-265** (this page reuses those endpoints — no new API) + **D-271**
(speaker **country flag + photo** on the session; the session **live-stream URLs**;
and the related Q&A / live / comments cross-references).

> **Related screens (D-271):** the session's **questions** belong to **screen 26**
> (open only 5 min before start → end, arrived attendees only), and the **live
> broadcast / recording / AI summary (محضر) / comments** belong to **screen 25**.
> The standalone comments screen (28) is **removed** — comments live inside the
> live screen. Screen 17 only links out to them — see
> [Page_017_Logic.md](Page_017_Logic.md) L-9 and [Page_017_API.md](Page_017_API.md) E5.

## Headline (owner directive, 2026-06-03)
> "Page 17 shows detail about **my reservation if it exists** and details about
> the session (**can be got from p16**). If logged in, show my reservation data:
> **seat number, location (row / seat)**. And it has **two functions** —
> add-to-calendar (standard) + reminder."

The session body comes from the **p16 cached programme** (no re-fetch); the
my-seat card is the caller's own seat from the seat-map's `MyCell`; the two
functions are **client-local OS actions** (device calendar + a local reminder).
See [Page_017_Logic.md](Page_017_Logic.md) and [Page_017_API.md](Page_017_API.md).
