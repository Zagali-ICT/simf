# Page 018 — Function (مقعدي · خريطة الجلوس · My Seat map)

What the user does on this screen. Grounded in `Mockup.html` screen 18 (line
~1284) and the Screen Guide SCREEN18 ("Visual seat-map showing where in the hall
the user's assigned seat is, with one seat highlighted in brass").

## Privilege / auth gate
**Visitor (approved) — login-only.** The seat-map endpoint requires an approved
account (`RequireApprovedAccount`) and the route `/agenda/:sessionId/my-seat` is
auth-gated (D-254). A guest / pending account cannot open this screen.

## Elements (top → bottom, from the mockup)
1. **Header** — back chevron + title `مقعدي`.
2. **Banner** (`seat-banner`) — `الجلسة` + the session name, and a **seat badge**
   `صف B · مقعد 12` (the user's row + seat from `MyCell`).
3. **Hall plan** (`hall`) — a **stage** strip at the top (`المسرح · STAGE`) and a
   **grid of rows** (mockup A–H), each row a strip of seat cells. Each cell is one
   of three states:
   - **mine** (the user's own seat — brass, glowing),
   - **available** (free),
   - **taken / reserved** (occupied).
4. **Legend** (`seat-leg`) — `مقعدك` (mine) · `متاح` (available) · `محجوز` (taken).
5. **Two actions** (`seat-actions`) —
   - `إرشادي إلى مقعدي` (Guide me to my seat),
   - `مشاركة الموقع` (Share location).
6. **Bottom nav** — the five-slot bar (Agenda active).

## What the user does
1. **See the whole hall** — every seat with its status, drawn from the one
   `SessionSeatMap` (Page_018_Logic L-1/L-2). The user's own seat is highlighted
   ("Main") and named in the banner.
2. **Find / focus my seat** — the highlighted cell + the banner badge point the
   user to the exact row + seat (the "input to point to a specific one"). The grid
   can scroll/zoom to the highlighted seat.
3. **Navigate** → `إرشادي إلى مقعدي` → opens **Map (15, Page_015)** to guide the
   user toward the seat (Screen Guide: "→ opens the Map (15) with directions").
4. **Share** → `مشاركة الموقع` → the **native share sheet** (Screen Guide: "→
   native share sheet").
5. **(Later) reserve / change a seat** — the same grid can act as a **picker**: tap
   a free cell to book it, or release the current one — reusing the **existing**
   reserve endpoints (Page_018_Logic L-4). Screen-18-as-drawn is the read-only
   "where is my seat" view; the interactive booking mode is the Page 7 seat-pick
   surface reusing this grid.

## Acceptance criteria
- Only an **approved, signed-in** account can open the screen; a guest / pending
  account is gated out.
- The grid renders **every seat** with the correct status — **available**,
  **reserved/taken** (incl. an admin-blocked row), and the user's **own** seat
  highlighted — from **one** `GET …/seats` call.
- The banner shows the user's **row + seat** when they hold one; when they have no
  seat, the screen still renders the hall (no highlight, a "no seat yet" banner).
- `إرشادي إلى مقعدي` opens Map (15); `مشاركة الموقع` opens the native share sheet.
- Reserving / releasing (when the interactive mode is used) goes through the
  existing reserve endpoints and re-reads the grid.

## Where it fits in the journey
**End of Journey E — Agenda planning**: Home (13) → Agenda (16) → Session detail
(17) → **My Seat map (18)**. Reached from the `عرض ←` link on the session-detail
`مقعدي` card (Page_017) or from My Area (14) "today's schedule".
