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
- **Signed-in but pending/rejected** (effective Guest) — limited view: identity card
  (cached name + under-review note) plus only the Account-settings and Sign-out rows;
  no dashboard call, no counters/schedule/badge/share.
- Anonymous Guests do not reach this page (they get Guest mode, Page 12).

## Functional elements (as built, D-378 — KSA frame 512:1780)
| # | Element | Behaviour |
|---|---------|-----------|
| FE-1 | Identity card | Avatar 64 (photo / initials fallback), localized full name, line "{tier} · مسجّل في N جلسات" (tier omitted when none), gold reference `#{qrId}` (only when present), bordered gold **مشاركة** button → contact vCard share. |
| FE-2 | Language tile | **العربية · English** — toggles the app language AR ↔ EN, persisted to prefs (wired, D-378). |
| FE-3 | Theme tile | **المظهر · ليلي/نهاري** — visible but **disabled** (no light theme exists; owner decision, D-378). |
| FE-4 | Share-profile tile | مشاركة ملفي → the share-my-contact QR screen (`/contacts/share`). |
| FE-5 | Share-contact tile | مشاركة جهة اتصال — share contact (vCard) via the native share sheet. |
| FE-6 | Stat tiles | مقابلات مؤكدة (`meetingsCount`) · جلسات محفوظة (`bookedSessionsCount`) — display-only. |
| FE-7 | Today's schedule | جدولي اليوم — a single time-ordered list of the user's sessions **and** meetings for today; empty → "لا يوجد لديك مواعيد اليوم". |
| FE-8 | المزيد rows | بطاقتي الذكية → Badge QR (Page 32) · اعدادات الحساب → More (Page 41) · مشاركة جدولي → `.ics` share · تسجيل الخروج → confirm dialog then sign out (D-373). |

## User actions & navigation
| Action | Result |
|--------|--------|
| Tap مشاركة (card) / مشاركة جهة اتصال | Native share sheet with the vCard (`simf.vcf`). |
| Tap مشاركة جدولي | Native share sheet with the calendar (`simf.ics`). |
| Tap مشاركة ملفي | Share-my-contact QR screen (`/contacts/share`). |
| Tap the language tile | UI flips AR ↔ EN immediately; choice persists across launches. |
| Tap a schedule row | Session → Session detail (Page 17); Meeting rows are non-tappable (no meeting detail page). |
| Tap بطاقتي الذكية | Badge QR (Page 32). |
| Tap اعدادات الحساب | More (Page 41). |
| Tap تسجيل الخروج | Confirm dialog (إلغاء / تسجيل الخروج); confirming revokes the session and lands on sign-in. |
| Bottom-nav Profile tab (any page) | Arrives at this page (Profile tab renders active here). |

## Acceptance criteria (functional)
- AC-1 The card shows the user's own localized name, tier·enrolled line, reference, avatar.
- AC-2 The two counters reflect the user's own confirmed meetings and booked sessions.
- AC-3 Today's schedule merges sessions + meetings, ordered by time, today only.
- AC-4 Share produces a vCard (contact) and an `.ics` (calendar) through the OS share sheet; a failed share shows the "تعذّرت المشاركة…" snackbar.
- AC-5 Smart Badge and Account-settings rows route to Pages 32 and 41; Share-my-profile routes to `/contacts/share`.
- AC-6 A pending (unapproved) user — and an unexpected 403 — sees the limited card + Account-settings + Sign-out only; no dashboard call is made when pending.
- AC-7 The language tile toggles and persists the locale; the theme tile renders disabled.
- AC-8 Sign-out requires an explicit confirmation before the session is revoked (D-373).
