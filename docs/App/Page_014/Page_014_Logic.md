# Page 014 — Logic (منطقتي · My Area)

Business rules behind the page. Verified against the domain model on 2026-06-02;
re-verified against the as-built KSA-redesign screen on 2026-06-13 (D-378).

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
| **B2B / B2C** | the **Control Panel** | `BusinessMeeting` + `BusinessMeetingParticipant` (D-248) | the caller is a `Kind == Visitor` participant **and** the meeting `Status == Confirmed` |

Both are scoped to the caller and **both are now built** — the counter (and the
schedule) union them. A business meeting carries its **own** `Start`/`End`
(it is not tied to a `Session`), so its schedule item uses that time directly and
its hall name comes from `MeetingTable.Hall`.

## L-4 Today's schedule
A single list, ordered by start time, **today only**, merging:
- **Session items** — the user's held bookings (L-2) joined to `Session`; time = `Session.Start`, plus title (AR/EN) and `Hall.Name`.
- **Meeting items** — the user's accepted meetings (L-3) joined to their parent `Session`; **time = the parent `Session.Start`** (meetings carry no own time). Subject from the meeting.

Each item carries its `status` so the UI can badge a still-Pending booking.

## L-5 Role gating
- Reachable only when signed-in. Full dashboard at **Visitor** and above.
- A signed-in **pending/rejected** user is effective **Guest**: the screen makes
  **no dashboard call** (it is Approved-only and would 403) and renders the limited
  view — the identity card from the **cached** account (display name + the
  under-review note) plus only the اعدادات الحساب and تسجيل الخروج rows. No badge
  QR, counters, schedule, or share actions.
- An unexpected **403** on the dashboard (approval revoked mid-session) falls back
  to the same limited view; any other failure shows the error + retry surface.
- App authorization is expressed in the four app roles only (Guest/Visitor/Moderator/Staff),
  never the CP `UserType` or the permission catalogue.

## L-6 Edge cases
- `avatarUrl` null (or the image fails to load) → initials fallback rendered from
  the name (`ksaInitials` — first letters of the first + last name parts).
- `QrId` null (not yet approved) → hide the `#…` reference (badge QR lives on Page 32).
- Empty counters → show `0`; empty today's schedule → "لا يوجد لديك مواعيد اليوم" placeholder.
- Single aggregate call → one retry surface on error (`KsaErrorState`).
- A failed `.vcf`/`.ics` fetch → "تعذّرت المشاركة. حاول مرة أخرى." snackbar (the page stays).
- `identity.pageColor` and each schedule item's `status`/`end` are decoded but
  **unused** in the KSA design — the accent is the token gold and rows carry no
  pending badge.
- Sign-out is **confirm-first** (D-373) and **best-effort** on the wire: the local
  session is cleared and the app lands on `/sign-in` even if the revoke call fails.

## L-7 Dependencies
- **B2B/B2C meeting module is BUILT (D-248).** It is the CP-managed, pre-reserved
  hall/table booking the owner described: `Hall.Purpose` + `MeetingTable` +
  `HallAllocation` + `BusinessMeeting`/`BusinessMeetingParticipant` (companies = FK,
  visitors = bare Guid resolved on read, D-157). The dashboard unions confirmed
  business meetings (caller as a Visitor participant) with accepted speaker meetings.
- The QR is rendered **client-side** from `QrId`; there is no server QR-image endpoint.

## L-8 Localization
Arabic primary (RTL), English secondary. Bilingual data comes paired from the API
(`fullNameAr`/`fullNameEn`, `titleAr`/`titleEn`, `tierNameAr`/`tierNameEn`); the app
selects per active locale with cross-language fallback when one side is empty (a
title-less business meeting falls back to its `subject`). The page's **العربية ·
English tile** flips the locale via `LocaleController.toggle()` (persisted to prefs;
Arabic default). Times rendered in the device timezone from UTC, 12-hour `hh:mm a`,
LTR-pinned (as is the `#qrId` reference).
