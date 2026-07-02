# My meetings (المقابلات) — `/my-meetings`

| | |
|--|--|
| **Route** | `/my-meetings` (route name `myMeetings`, `RouteNames.myMeetings` → `MyMeetingsScreen`, route 115) — Figma node `1701:9406` |
| **Layout** | SIMF app shell (`SimfPageShell`), back chevron + centred title, no header-action cluster |
| **Surface** | Mobile App (Flutter) |
| **Audience** | Visitor / Exhibitor (approved) — meetings are an approved-attendee concept |
| **Auth** | **Approved-only** — gated as an attendee route (`_routeRoles[115] = {Visitor, Exhibitor}`); a guest deep-link is redirected to sign-in |
| **Pattern** | Client-side reuse — the caller's **meeting** requests (speaker + delegation), computed by filtering the الطلبات feed (`myRequestsProvider`). **No new endpoint.** |
| **Status** | ✅ Screen built (D-587, Figma `1701:9406`) |
| **Implements use case(s)** | See my speaker/delegation meetings; filter them by status (الكل / مكتملة / قيد الانتظار / مرفوضة); read each meeting's counterpart, type, slot date and status badge |
| **Backend endpoints** | `GET /api/v1/app/my-requests` (the unified requests feed, **approved-only**) — the screen filters it to the two meeting kinds. No new endpoint added. |
| **Source file** | Flutter `features/requests/my_meetings_screen.dart` (reuses `myRequestsProvider`, `AppRequestItem`, the shared `SimfTokens`, and `gregorianMonthName`). |
| **Tests** | [`docs/tests/e2e/mobile-my-meetings.md`](../../tests/e2e/mobile-my-meetings.md) (`E2E-MOBMTG-001..007`); widget test `test/features/requests/my_meetings_screen_test.dart` |
| **Last reviewed** | 2026-07-02 |

---

## 1. Purpose

My meetings (المقابلات) is the approved attendee's list of their **meeting** requests —
the speaker meetings (D-269) and delegation meetings (D-478) they submitted — presented
as person cards. It is reached from the My-Area "مقابلات" counter (D-587), whose number
is the meetings count. The screen shows four status filter chips (الكل / مكتملة / قيد
الانتظار / مرفوضة) with counts, a "جميع المقابلات (N)" section header, and one card per
meeting.

It reuses the same feed as the الطلبات screen (`GET /app/my-requests`, `myRequestsProvider`),
filtered to the two meeting kinds — so it is a focused, read-only view of the requests a
user already has, not a new data source. This is **not** the old read-only "My meetings"
screen (D-479), which was removed when the unified الطلبات feed (D-500) superseded it; المقابلات
is a new presentation over that same feed.

## 2. Audience + permissions

- **Who can reach it:** an **approved** attendee (Visitor / Exhibitor), from the My-Area
  meetings counter.
- **Authorisation gates:** the backing feed (`GET /app/my-requests`) is **approved-only**;
  the route is gated `_routeRoles[115] = {Visitor, Exhibitor}` (a guest tapping the deep
  link is redirected; a signed-in Staff/Moderator is redirected home).
- **What a guest / non-attendee sees:** nothing — the route is attendee-gated.

## 3. Screenshots

| State | File | Captured |
|-------|------|----------|
| Default (with meeting cards, الكل) | `docs/screenshots/my-meetings-default.png` | _pending on-device capture_ |
| Status-filtered (e.g. مكتملة) | `docs/screenshots/my-meetings-filtered.png` | _pending_ |
| Empty state | `docs/screenshots/my-meetings-empty.png` | _pending_ |
| RTL (Arabic) | `docs/screenshots/my-meetings-rtl.png` | _pending_ |

> Figma reference frame: `1701:9406`.

## 4. UI affordances

### 4.1 Header

Back chevron + centred title **المقابلات** ("My meetings"). No header-action cluster
(sub-page default).

### 4.2 Status filter chips

Four equal-width chips (right→left, RTL): **الكل** (gold when selected), **مكتملة**
(green, accepted), **قيد الانتظار** (amber, pending), **مرفوضة** (red, rejected). Each
reads "{label} ({count})"; the selected chip is a solid colour fill with white text, the
rest are the colour at 12% fill + 20% border. Selecting one filters the list; a chip that
has dropped to zero falls back to الكل so the user is never stranded.

### 4.3 "جميع المقابلات (N)" section header

A 16px white line above the list; N is the count of currently-shown meetings.

### 4.4 Meeting card (one per meeting)

| Element | Source field(s) | Notes |
|---------|-----------------|-------|
| Initial avatar | first letter of the counterpart name | 38px gold square, white initial |
| Name | `title` / `titleArabic` | the speaker name (speaker meeting) or target-country name (delegation meeting), Bold 15 |
| Meeting type | `kind` | "طلب لقاء مع متحدث" / "طلب اجتماع وفد" — the real available secondary line (the data carries no per-person role) |
| Date line | `eventDateUtc` (device-local) | clock glyph + "12 يناير – 10:30 PM" (bilingual month name + 12-hour time, pinned LTR) |
| Status badge | `status` | neutral beige badge: accepted → "مؤكدة", pending → "قيد الانتظار", rejected → "مرفوضة" |

The card is read-only (no tap navigation, matching the frame).

## 5. Data flow

```
User opens /my-meetings (from the My-Area مقابلات counter)
  → screen watches myRequestsProvider (GET /app/my-requests, approved-only)
  → meetings = feed.where(kind ∈ {speaker, delegation} AND status ≠ cancelled)
  → counts = {all, accepted, pending, rejected}; chips render the counts
  → filtered by the selected chip → cards
Pull-to-refresh → invalidate + await myRequestsProvider
```

Cancelled meetings are **excluded** (they remain visible on الطلبات), so الكل equals
accepted + pending + rejected and every card is reachable by a chip. No new endpoint —
the screen is a client-side filter of one existing read.

## 6. States (loading / error / empty)

- **Loading:** a gold spinner while the feed is in flight.
- **Error:** a pull-to-refreshable error surface (with Retry) on a feed load failure
  (shares the الطلبات error string).
- **Empty:** the empty state ("لا توجد مقابلات بعد." / "No meetings yet.") when the caller
  has no live meeting requests.

## 7. i18n + RTL

All visible strings are localized (AR / EN): title المقابلات / My meetings, the four chip
labels, the "جميع المقابلات (N)" header, the badge labels, and the empty message. The date
line uses the shared bilingual month-name helper (`gregorianMonthName`) and is pinned LTR
so "12 يناير – 10:30 PM" keeps the frame's reading order. Under Arabic the header, chips
and cards mirror right-to-left (avatar at the inline end, badge/date on the row).

## 8. Edge cases + known limitations

- **Meetings ≠ all requests.** This list is the meeting subset (speaker + delegation) of
  the الطلبات feed; document / badge / session-attendance requests stay on الطلبات.
- **No per-person role.** The feed carries only the counterpart name (speaker/country); the
  card's secondary line therefore shows the meeting **type**, not a professional role (the
  Figma "باحث بيئي" was placeholder text).
- **Cancelled meetings** are excluded here (visible on الطلبات); delegation meetings are not
  user-cancellable and speaker cancellations move the item off this list.

## 9. Related E2E test scenarios

See [`docs/tests/e2e/mobile-my-meetings.md`](../../tests/e2e/mobile-my-meetings.md)
(`E2E-MOBMTG-001..007`): golden path (open from the counter, see the meeting cards +
counts), status-chip filtering, the excludes (non-meeting kinds + cancelled), the confirmed
badge, the empty state, the auth-gate, and RTL.

## 10. Related docs

- Decisions log: **D-587** (this screen + the My-Area "مقابلات" counter repoint). Related:
  D-500 (the الطلبات feed it reuses), D-479 (the removed read-only my-meetings it is **not**),
  D-133/D-245/D-246 (E2E + docs DoD), D-519 (attendee route gating).
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) — `app` endpoint group,
  `ApiResult<T>` envelope.

## 11. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-07-02 | D-587 | New المقابلات screen (Figma `1701:9406`) over the existing `GET /app/my-requests` feed (no new endpoint); My-Area "مقابلات" counter repointed from `/requests` to `/my-meetings`. |

---

_Last reviewed:_ 2026-07-02 by SIMF Team (D-587 — my-meetings screen reference doc).
