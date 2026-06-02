# Page 014 — Logic (منطقتي · My Area)

Business rules behind the page. Verified against the domain model on 2026-06-02.

## L-1 Reference id
The card reference is the existing **`UserProfile.QrId`** (a 12-char Crockford code).
There is **no** separate `#SIMF-2026-V-#####` field — that mockup string was filler.
`QrId` is **null until the account is Approved**; show the reference only when present.

## L-2 Counter 1 — booked sessions (جلسات محفوظة / "enrolled in N")
`COUNT(SeatReservation)` where:
- `ReservedForUserId == caller` (own bookings), and
- `Kind ∈ { UserBooking, RandomAssignment }` (exclude `AdminReservedRow`), and
- `ReleasedAt IS NULL` (a **held** seat — `BookingStatus` Pending **or** Approved).

Owner decision: the counter is **"booked"** (held), not approved-only. The same number
feeds the card's "enrolled in N sessions" line and the stat tile.

## L-3 Counter 2 — meetings (مقابلات مؤكدة)
Union of two kinds of "meeting with others":
| Kind | Created via | Backing entity | Counted when |
|------|-------------|----------------|--------------|
| Speaker / interview | the **App** (`POST /sessions/{id}/meeting-requests`) | `MeetingRequest` | `Status == Accepted` |
| **B2B / B2C** | the **Control Panel** | *not built yet* (see L-7) | when the entity ships |

Both are scoped to the caller. Until the B2B/B2C entity exists, the counter reflects
**speaker meetings only** — the contract already unions both so it does not change shape later.

## L-4 Today's schedule
A single list, ordered by start time, **today only**, merging:
- **Session items** — the user's held bookings (L-2) joined to `Session`; time = `Session.StartUtc`, plus title (AR/EN) and `Hall.Name`.
- **Meeting items** — the user's accepted meetings (L-3) joined to their parent `Session`; **time = the parent `Session.StartUtc`** (meetings carry no own time). Subject from the meeting.

Each item carries its `status` so the UI can badge a still-Pending booking.

## L-5 Role gating
- Reachable only when signed-in. Full dashboard at **Visitor** and above.
- A signed-in **pending/rejected** user is effective **Guest**: render the identity card
  (name/avatar) but hide/disable the badge QR, counters, schedule, and share actions.
- App authorization is expressed in the four app roles only (Guest/Visitor/Moderator/Staff),
  never the CP `UserType` or the permission catalogue.

## L-6 Edge cases
- `avatarUrl` null → initials placeholder (mockup shows "RS").
- `QrId` null (not yet approved) → hide reference + badge actions.
- Empty counters → show `0`; empty today's schedule → "no items today" placeholder.
- Single aggregate call → one retry surface on error.

## L-7 Dependencies
- **B2B/B2C meeting module is NOT built.** Per owner it is a **CP-managed, pre-reserved
  hall/table booking** for bilateral meetings between **company / visitor / sponsor** —
  define halls/tables, then the reservation tables for the session/meeting, managed in
  the CP. Scoped to the **running programme-plan session**, not the App-API review.
- The QR is rendered **client-side** from `QrId`; there is no server QR-image endpoint.

## L-8 Localization
Arabic primary (RTL), English secondary. Bilingual data comes paired from the API
(`fullNameAr`/`fullNameEn`, `titleAr`/`titleEn`, `tierNameAr`/`tierNameEn`); the app
selects per active locale. Times rendered in the device timezone from UTC.
