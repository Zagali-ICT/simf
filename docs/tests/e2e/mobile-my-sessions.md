# E2E test catalogue — `My sessions` (`myAreaSessions`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue —
> data-driven from `GET /app/account/sessions` (`RequireApprovedAccount`), a
> read-only aggregate of the caller's booked / joined sessions enriched with the
> per-user attended flag (from `HallAttendance`) and المفضلة heart (from
> `SessionFavourite`). Built to KSA Figma frame **`1388:9067`** (تفاصيل الجلسات).
> Reached from the My-Area "my sessions" counter (Wave 2). Tested in
> `src/Mobile/simf_app/test/features/myarea/my_sessions_screen_test.dart` +
> `my_sessions_models_test.dart`; backend in
> `tests/SIMF.Api.Tests/MyAreaDashboardTests.cs`
> (`My_sessions_lists_the_booked_session_with_per_user_flags`,
> `My_sessions_is_empty_for_a_visitor_with_no_bookings`,
> `My_sessions_without_a_token_returns_401`).

| | |
|--|--|
| **Page** | app screen #113 `myAreaSessions` |
| **Route** | `/my-area/sessions` (`GET /app/account/sessions`) |
| **Surface** | Mobile (Flutter) |
| **Figma** | `1388:9067` |
| **Auth setup** | **Approved account** — `RequireApprovedAccount`. Sign in as an approved visitor (`Get-Totp` for the OTP step, never a literal secret). |
| **Last reviewed** | 2026-06-26 |

## Layout

- **Header**: back chevron + centred title **تفاصيل الجلسات**.
- **Tabs** (scrollable pills, RTL): القادمة (upcoming) · حضرتها (attended) ·
  فاتتني (missed) · الأرشيف (archive). The active pill is gold.
- **Count subtitle**: `{n} · {tab label}`.
- **Cards** (navy-deep, beige hairline): title; clock line `time · {duration}`
  with the bordered category chip; primary speaker `name · rank` with the hall;
  the المفضلة heart (gold = favourited) on the trailing edge. Tapping a card opens
  the session detail; tapping the heart toggles the favourite (optimistic; reverts
  + toasts on a server error).
- **Tab rules** (client-side, device clock): القادمة = `startUtc` in the future;
  حضرتها = `attended`; فاتتني = ended & not attended; الأرشيف = Recorded/Published.
- **States**: spinner while loading; retry surface on a wire error; an empty
  message (`No sessions in this list.`) per tab.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB113-001 | Upcoming tab lists the booked session; tap opens its detail | happy | P0 | authored ✓ (screen `the Upcoming tab lists the session; tap opens its detail`) |
| E2E-MOB113-002 | Tabs partition on the attended flag + the clock | data | P0 | authored ✓ (screen `the Attended tab partitions on the attended flag`) |
| E2E-MOB113-003 | Empty tab → empty message | empty | P1 | authored ✓ (screen `shows the empty state when a tab has no sessions`) |
| E2E-MOB113-004 | `GET /app/account/sessions` returns booked session + attended/favourite flags | happy | P0 | authored ✓ (API `My_sessions_lists_the_booked_session_with_per_user_flags`) |
| E2E-MOB113-005 | No bookings → empty list | empty | P1 | authored ✓ (API `My_sessions_is_empty_for_a_visitor_with_no_bookings`) |
| E2E-MOB113-006 | Anonymous read → 401 | auth | P0 | authored ✓ (API `My_sessions_without_a_token_returns_401`) |
| E2E-MOB113-007 | RTL — Arabic title / category / speaker from the same item | rtl | P2 | covered (models `localized*` getters) |

## Scenarios

```gherkin
Feature: My sessions (approved account, Figma 1388:9067, GET /app/account/sessions)

Scenario: The booked session shows with per-user flags
  Given an approved visitor has booked the session "Booked Talk"
  And the visitor attended it and favourited it
  When the visitor GETs /api/v1/app/account/sessions
  Then it returns 200 with "Booked Talk"
  And attended is true and isFavourite is true

Scenario: The four tabs partition the list
  Given the visitor has a future not-attended session and a past attended session
  When the visitor opens /my-area/sessions
  Then the القادمة tab shows only the future session
  And the حضرتها tab shows only the attended session

Scenario: An empty tab
  Given the visitor has no sessions in the selected tab
  When the visitor opens /my-area/sessions
  Then the screen shows "No sessions in this list."

Scenario: The read requires an approved account
  Given no bearer token
  When a client GETs /api/v1/app/account/sessions
  Then it returns 401
```

**Evidence:** screen tests (3 — upcoming list+nav, attended partition, empty);
models test (3 — decode + flags, upcoming/ended derivation, empty); API tests
(3 — booked+flags, empty, 401).

---

_Last reviewed:_ `2026-06-26` by `SIMF Team`.
