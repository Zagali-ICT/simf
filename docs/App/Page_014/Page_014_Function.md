# Page 014 — Function (منطقتي · My Area)

Functional specification: what the page does for the user. Logic rules are in
[Page_014_Logic.md](Page_014_Logic.md); the backend contract is in
[Page_014_API.md](Page_014_API.md); the visual design is in
[Page_014_Design.md](Page_014_Design.md).

## Purpose
A **personal dashboard** — the signed-in attendee's hub for their identity, their
participation counters, their schedule for today, and quick share actions. It is **not**
an editable profile form (editing lives under Settings / More, Page 41).

## Actors
- **Visitor** (approved) — full dashboard.
- **Signed-in but pending** (effective Guest) — identity card + notifications only; no
  badge QR, no counters/schedule actions.
- Anonymous Guests do not reach this page (they get Guest mode, Page 12).

## Functional elements
| # | Element | Behaviour |
|---|---------|-----------|
| FE-1 | Profile card | Shows avatar, full name (AR/EN), tier word (e.g. "VIP"), the reference id, and a count "enrolled in N sessions". |
| FE-2 | Share button (on card) | Opens the native share intent for the user's contact (vCard). |
| FE-3 | Share-contact tile | مشاركة جهة اتصال — share contact (vCard) via native intent. |
| FE-4 | Share-profile tile | مشاركة ملفي — share the user's profile/calendar via native intent. |
| FE-5 | Stat tile — booked sessions | Count of sessions the user has booked (جلسات محفوظة). |
| FE-6 | Stat tile — meetings | Count of the user's confirmed meetings (مقابلات مؤكدة). |
| FE-7 | Today's schedule | جدولي اليوم — a single time-ordered list of the user's sessions **and** meetings for today. |
| FE-8 | Utility link — Smart Badge | بطاقتي الذكية → Badge QR (Page 32). |
| FE-9 | Utility link — Account settings | إعدادات الحساب → More / settings (Page 41). |

## User actions & navigation
| Action | Result |
|--------|--------|
| Tap Share / share tiles | Native share sheet with vCard (contact) and/or `.ics` (calendar). |
| Tap a schedule row | Session → Session detail (Page 17); Meeting → meeting detail (TBD page). |
| Tap Smart Badge | Badge QR (Page 32). |
| Tap Account settings | More (Page 41). |
| Tap an avatar elsewhere (Home) | Arrives at this page. |

## Acceptance criteria (functional)
- AC-1 The card shows the user's own name (AR + EN), tier, reference, avatar.
- AC-2 The two counters reflect the user's own booked sessions and confirmed meetings.
- AC-3 Today's schedule merges sessions + meetings, ordered by time, today only.
- AC-4 Share produces a vCard (contact) and an `.ics` (calendar) through the OS share intent.
- AC-5 Smart Badge and Account-settings links route to Pages 32 and 41.
- AC-6 A pending (unapproved) user sees the card but not the badge/counters/schedule actions.
