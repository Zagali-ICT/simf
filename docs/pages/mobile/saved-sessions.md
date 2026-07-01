# Saved sessions (الجلسات المحفوظة) — `/saved-sessions`

| | |
|--|--|
| **Route** | `/saved-sessions` (route name `savedSessions`, `RouteNames.savedSessions` → `SavedSessionsScreen`, route 205) — Figma node `1701:8928` |
| **Layout** | SIMF app shell (`SimfPageShell`), back chevron + centred title, no header-action cluster |
| **Surface** | Mobile App (Flutter) |
| **Audience** | Visitor / Exhibitor (approved) — favourites are approved-only |
| **Auth** | **Approved-only** — the backing favourites endpoint requires an approved token; gated as an attendee route (`_routeRoles[205] = {Visitor, Exhibitor}`) |
| **Pattern** | Client-side reuse — the caller's **favourited** sessions (المفضلة = محفوظة), computed by intersecting the cached programme with the shared favourites set. **No new endpoint.** |
| **Status** | ✅ Screen built (D-584, Figma `1701:8928`) |
| **Implements use case(s)** | See the sessions I have saved/favourited; filter them by category; open a saved session's detail; un-save from the bookmark |
| **Backend endpoints** | `GET /api/v1/app/programme/sessions` (cached programme, anonymous) · `GET /api/v1/app/sessions/favourites` (the caller's favourite ids, **approved-only**) · `DELETE /api/v1/app/sessions/{id}/favourite` (un-save via the bookmark). No new endpoint added. |
| **Source file** | Flutter `features/sessions/saved_sessions_screen.dart` (+ the shared `sessionFavouritesProvider`, `aiSummarySessionsProvider`, `SessionFilterTabs`, and `FavouriteHeartButton` with the bookmark icon pair). |
| **Tests** | [`docs/tests/e2e/mobile-saved-sessions.md`](../../tests/e2e/mobile-saved-sessions.md) (`E2E-MOBSAVED-001..006`); widget test `test/features/sessions/saved_sessions_screen_test.dart` |
| **Last reviewed** | 2026-07-02 |

---

## 1. Purpose

Saved sessions (الجلسات المحفوظة) is the approved attendee's list of the sessions
they have **saved** (favourited). It is reached from the My-Area "جلسات محفوظة"
counter (D-584), whose number is the saved count. The screen shows a gold
"★ N جلسة محفوظة" header, a row of category chips (الكل + each distinct session
category present in the saved set), and one card per saved session. It reuses the
same favourites set as the heart on the summaries (`1388:8392`) and my-sessions
(`1388:9067`) screens, so a bookmark toggled here is reflected everywhere.

## 2. Audience + permissions

- **Who can reach it:** an **approved** attendee (Visitor / Exhibitor), from the
  My-Area saved-sessions counter.
- **Authorisation gates:** the favourites endpoint (`GET /app/sessions/favourites`)
  is **approved-only**; the route is gated `_routeRoles[205] = {Visitor, Exhibitor}`
  (a guest tapping the deep link is redirected). The cached programme itself is
  anonymous, but the saved intersection is empty without an approved favourites load.
- **What a guest / non-attendee sees:** nothing — the route is attendee-gated; a
  signed-in Staff/Moderator is redirected home.

## 3. Screenshots

| State | File | Captured |
|-------|------|----------|
| Default (with saved cards) | `docs/screenshots/saved-sessions-default.png` | _pending on-device capture_ |
| Category-filtered | `docs/screenshots/saved-sessions-filtered.png` | _pending_ |
| Empty state | `docs/screenshots/saved-sessions-empty.png` | _pending_ |
| RTL (Arabic) | `docs/screenshots/saved-sessions-rtl.png` | _pending_ |

> Figma reference frame: `1701:8928`.

## 4. UI affordances

### 4.1 Header

Back chevron + centred title **الجلسات المحفوظة** ("Saved sessions"). No
header-action cluster (sub-page default).

### 4.2 Count row

A gold-hairline box: a ★ star + the unit label "جلسة محفوظة" at the inline start
(right, RTL) and the saved **count** at the end. The count = the number of
favourited sessions present in the programme.

### 4.3 Category chips

A horizontally scrollable pill row (`SessionFilterTabs`): **الكل** (All) followed by
each distinct category among the saved sessions (e.g. بيئة / طاقة / تقنية). Selecting
one filters the list to that category; الكل clears the filter. The row is hidden when
no saved session carries a category.

### 4.4 Saved-session card (one per session)

| Element | Source field(s) | Notes |
|---------|-----------------|-------|
| Title | `title` / `titleArabic` | localized |
| Category · hall line | `categoryName(Arabic)` · `hallName(Arabic)` | beige meta line; omitted parts collapse |
| Date · time line | `startUtc` (device-local) | "12 يناير 2026 · 08:00 PM" — bilingual month name + 12-hour time |
| Bookmark | shared favourites set | filled bookmark; tapping un-saves (removes the card) |

Tapping the card body opens the **session detail** (`/sessions/{id}`, route 17).

## 5. Data flow

```
User opens /saved-sessions (from the My-Area جلسات محفوظة counter)
  → screen watches aiSummarySessionsProvider (cached programme)
       + sessionFavouritesProvider (GET /app/sessions/favourites, approved-only)
  → saved = programme.where(id ∈ favourites), sorted by startUtc
  → count row = saved.length; chips = distinct categories(saved)
  → filtered by the selected chip → cards

Tap bookmark → sessionFavouritesProvider.toggle(id) (DELETE …/favourite) → card leaves
Tap card    → push /sessions/{id} (session detail)
Pull-to-refresh → invalidate programme + favourites, await programme
```

No new endpoint — the screen is a client-side intersection of two existing reads.

## 6. States (loading / error / empty)

- **Loading:** a spinner while the cached programme is in flight.
- **Error:** a pull-to-refreshable error surface (with Retry) on a programme
  load failure.
- **Empty:** the empty state ("لا توجد جلسات محفوظة بعد." / "No saved sessions yet.")
  when the caller has favourited nothing (or nothing that is still in the programme);
  the count row shows 0.

## 7. i18n + RTL

All visible strings are localized (AR / EN): title "الجلسات المحفوظة" / "Saved
sessions", the count unit "جلسة محفوظة" / "saved sessions", the الكل / All chip and
the category names, and the empty message. The date line uses a bilingual month-name
list ("يناير" / "January") so it renders without an intl locale. Under Arabic the
header, count row, chips and cards mirror right-to-left.

## 8. Edge cases + known limitations

- **Saved ≠ booked.** The counter and this list are the **favourited** set
  (`GET /app/sessions/favourites`), not the booked/reserved sessions. The booked
  set stays on the summaries "جلساتي" tab / the seat screens.
- **Orphan favourites.** A favourited id whose session is no longer in the programme
  is simply not shown; the My-Area counter uses the raw favourites-set size, so it
  can momentarily exceed the visible count if a saved session was removed.
- **Bookmark = the favourites toggle.** Un-saving here calls the same
  `DELETE /app/sessions/{id}/favourite` as the heart elsewhere; a failed toggle
  reverts and shows the "تعذر تحديث المفضلة" / "Could not update favourites" toast.
- **My-sessions (1388:9067) is now unreferenced.** Repointing the My-Area counter to
  this screen left the dedicated 4-tab my-sessions screen with no entry point (the
  booked view remains reachable from the summaries "جلساتي" tab) — flagged for the
  owner to keep, relink, or retire.

## 9. Related E2E test scenarios

See [`docs/tests/e2e/mobile-saved-sessions.md`](../../tests/e2e/mobile-saved-sessions.md)
(`E2E-MOBSAVED-001..006`): golden path (open from the counter, see the saved cards +
count), category-chip filtering, open a card → detail, un-save via the bookmark, the
empty state, the auth-gate, and RTL.

## 10. Related docs

- Decisions log: **D-584** (this screen + the My-Area counter repoint to the saved
  count). Related: D-133/D-245/D-246 (E2E + docs DoD), D-519 (attendee route gating),
  the favourites heart (summaries `1388:8392` / my-sessions `1388:9067`).
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) — `app`
  endpoint group, `ApiResult<T>` envelope.

## 11. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-07-02 | D-584 | New الجلسات المحفوظة screen (Figma `1701:8928`) over the existing favourites + programme reads (no new endpoint); My-Area "جلسات محفوظة" counter repointed to the saved (favourites) count and to `/saved-sessions`; `FavouriteHeartButton` gains an optional bookmark icon pair. |

---

_Last reviewed:_ 2026-07-02 by SIMF Team (D-584 — saved-sessions screen reference doc).
