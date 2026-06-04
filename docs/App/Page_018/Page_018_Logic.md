# Page 018 — Logic (مقعدي · خريطة الجلوس · My Seat map)

Business rules behind the seat map. Verified against the per-session seat
reservations (D-175). **No new backend behaviour** — this page composes endpoints
that already exist (D-267); it is the grid twin of Page_017's one-line card.

## L-1 One call draws the whole grid
The screen renders from a **single** read:
`GET /app/sessions/{sessionId}/seats` → `SessionSeatMap`:

| Field | Drives |
|-------|--------|
| `rowLabels` (`["A","B",…]`) | the row labels down the side of the grid |
| `seatsPerRow` (e.g. 12) | the number of cells per row |
| `reservedCells[]` (`{rowLabel, seatNumber, kind}`) | the **occupied** seats |
| `myCell` (`{rowLabel, seatNumber, kind}?`) | the user's **own** seat (highlight + banner) |
| `activeReservedCount` | a count for the banner / capacity readout |
| `hallCapacity`, `sessionCapacity` | effective capacity context |

The app caches this across the 17 → 18 hop, so opening My Seat from the session
detail's `عرض ←` needs **no** second fetch (Page_017_Logic L-4).

## L-2 Seat status = derived from the payload (no per-seat field)
For each grid cell `(row, seat)`:
- **mine** — it equals `myCell` (brass, glowing). One per user per session.
- **reserved / taken** — it appears in `reservedCells`. The `kind` distinguishes:
  - `UserBooking` / `RandomAssignment` — another visitor's seat,
  - `AdminReservedRow` — an admin-blocked row (the whole row is materialised as
    reserved cells; a visitor cannot pick any of them).
- **available** — it is **not** in `reservedCells` (and is within
  `rowLabels × seatsPerRow`). Free to book.

So "available vs reserved" is a **client derivation** over `reservedCells`; the
server returns only the occupied cells + the layout, not a per-seat boolean. The
legend maps to: `مقعدك` = mine, `متاح` = available, `محجوز` = reserved.

## L-3 "Point to a specific one (Main)" = the highlighted `myCell`
The user's **own** seat is the "Main" the screen points to: it is rendered brass +
glowing and named in the banner (`صف {RowLabel} · مقعد {SeatNumber}`). As in
Page_017 there is **no separate column axis** — the location is `RowLabel` +
`SeatNumber` (the 1-based seat within the row). If `myCell` is null (the user
holds no seat), nothing is highlighted and the banner shows a "no seat yet" state.

## L-4 "Can be used later for reserve" = the existing reserve endpoints
Screen-18-as-drawn is **read-only**. The same grid can later run as an interactive
**picker** reusing the surface that already backs the Page 7 sign-up seat-pick:
- `POST /app/sessions/{id}/seats/reserve` — self-pick a free `{rowLabel, seatNumber}`,
- `POST /app/sessions/{id}/seats/reserve-random` — let the server allocate a free seat,
- `DELETE /app/sessions/{id}/seats/mine` — release the held seat.

A successful pick / release re-reads `GET …/seats` to repaint the grid. **These
endpoints already exist** — no new build is required to add the interactive mode
later. Guards already enforced server-side: seat-already-taken (409), one-seat-per-
session (409), capacity-full (409), admin-blocked row (409).

## L-5 Navigation + share are client-side
- **`إرشادي إلى مقعدي` (guide me to my seat)** → the app navigates to **Map (15,
  Page_015)** to orient the user toward the seat (Screen Guide: "→ opens the Map
  (15) with directions"). Real turn-by-turn seat routing (GPS → arrival →
  in-venue directions) is **deferred** (D-211) — screen 18 performs a **screen
  navigation** to the venue map, not a directions API call.
- **`مشاركة الموقع` (share location)** → the **native share sheet** (Screen Guide:
  "→ native share sheet"). Client-local, no server call.

## L-6 Edge cases
- **No seat layout for the hall** → `rowLabels` is empty and `seatsPerRow` is 0;
  the grid cannot be drawn → show a "seat map not available yet" state (an admin
  has not configured the hall's layout).
- **User has no reservation** → `myCell` null → the grid renders with no highlight;
  the banner shows "no seat yet" and (if the interactive mode is on) invites a pick.
- **Session soft-deleted / missing** → the seat-map read fails (the session load
  404s) → "session removed" state.
- **Unauthenticated / pending** → 401 / 403 → the screen is gated out (the route is
  auth-gated, D-254).

## L-7 Localization
Arabic primary (RTL), English secondary. The hall plan mirrors RTL but the
**stage stays at the top**; the row letters and seat numbers render `dir="ltr"`
inside Arabic labels (per the mockup, e.g. `صف B · مقعد 12`). The legend and action
labels are bilingual.
