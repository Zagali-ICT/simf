# Page 018 — مقعدي · خريطة الجلوس · My Seat map

Per-page documentation folder. Everything about this app page lives here.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_018_Function.md](Page_018_Function.md) | What the user does — read the grid, find my seat, navigate, share, (later) reserve |
| Logic | [Page_018_Logic.md](Page_018_Logic.md) | The seat-status model (available / reserved / mine), the grid source, reserve-later, nav + share |
| API | [Page_018_API.md](Page_018_API.md) | The backend endpoints + DTOs this page reads (authoritative contract) — **all already built** |
| Design | [Page_018_Design.md](Page_018_Design.md) | Flutter screen design — banner, hall grid, legend, actions, RTL, states |

## Identity
| | |
|---|---|
| Mockup page | **18** (`Mockup.html`, line ~1284) |
| Route | `RouteNames.mySeat` → `/sessions/:sessionId/my-seat` (**auth-gated**) |
| Titles | AR **مقعدي · خريطة الجلوس** · EN **My Seat map** |
| Section | 2 — Core screens |
| Nature | **Visual hall seat-map** — all seats + status + my seat highlighted; navigate + share |
| App privilege | **Visitor (approved) — login-only.** The seat-map endpoint requires an approved account; the route is auth-gated (D-254). |
| Status | API **BUILT** (reuses existing endpoints — D-267); **Flutter screen BUILT (D-301)** — read-only grid + derived status + navigate + share (picker is the later mode, L-4) |

## Sources of truth (read first)
`Mockup.html` screen 18 (the visual) · `SIMF_Screen_Guide_and_User_Journey`
SCREEN18 (the narrative) · SIMF-MOB-API-001 (shared API conventions + auth) ·
`DECISIONS_LOG` **D-175** (per-session seat reservations — the `SessionSeatMap`
grid + `MyCell`) + **D-267** (this page reuses those endpoints — no new API).

## Headline (owner directive, 2026-06-03)
> "Page 18, for a logged-in person, shows **all seats in the hall**, the selected
> one and the **status (available, reserved)**, and has an **input to point to a
> specific one (Main)** — or can be used later on **for reserve** also — with a
> **navigation** option and **share**."

The whole grid + status + the user's own seat come from **one** call
(`GET /app/sessions/{id}/seats` → `SessionSeatMap`); the reserve path reuses the
**existing** reserve endpoints; navigation opens Map (15) and share is a native
sheet. See [Page_018_Logic.md](Page_018_Logic.md) and
[Page_018_API.md](Page_018_API.md).
