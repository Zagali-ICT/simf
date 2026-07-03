# Session detail (تفاصيل الجلسة) — mobile `/sessions/:sessionId`

| Field | Value |
|---|---|
| Route | `/sessions/:sessionId` (`RouteNames.sessionDetail`, page #17) · public / Guest+ |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/sessions/session_detail_screen.dart` (`SessionDetailScreen`) |
| Widgets | `lib/features/sessions/widgets/` — `session_detail_header` · `session_detail_body` · `session_header_card` · `session_text_sections` · `session_speaker_card` · `ask_host_card` · `session_reservation_card` · `session_booking_actions` |
| Figma node | `889:2450` (KSA-Project, file `PSXHhY0UVTAPSaIOf9uNKd`); sub-frames 889:2716 header card · 889:2715 action buttons · 1056:12876 ask-host · 894:2779 seat marker · 897:2872 CTA row |
| Shell | `SimfPageShell` (`SimfTab.sessions`) with a custom header (`SessionDetailHeader`: circled back + centred title + moderator Q&A action) |
| API | `GET /app/programme/sessions/{id}` (anonymous) · `GET /app/programme/sessions/{id}/seats` (approved only) · `POST …/seats/join` · `DELETE …/seats/mine` (D-485) |
| Providers | `sessionDetailRepositoryProvider` · `seatMapRepositoryProvider` · `sessionCalendarProvider` · `authControllerProvider` |
| Tests | `test/features/sessions/session_detail_screen_test.dart` (+ `session_detail_models_test.dart`, `seat_map_models_test.dart`); golden `test/golden/session_detail_golden_test.dart` (`goldens/session_detail_889-2450.png`); E2E [`mobile-session-detail.md`](../../../tests/e2e/mobile-session-detail.md) |
| Legacy detail | `docs/App/Page_017/` (Function / Logic / API / Design) — retained as the detailed historical spec |
| Status | ✅ Real — D-300 (built) → D-485 (join/cancel) → D-567/D-572/D-593 (Figma parity) → **clean-code frozen (D-597)** |

## 1. Purpose
The full detail of one programme session: header card (gold day-ordinal badge,
title, time/date meta, ملخص الجلسة + رابط الجلسة actions), description,
speakers, the اسأل المحاور card, the join/booking section (D-485) and the
reminder / add-to-calendar CTA row.

## 2. Audience & access
Guest+ for the detail itself (anonymous endpoint). The seat map / join / cancel
are **approved-account only** — a guest never calls the seat endpoint and a
pending account's 403 hides the join section (L-3). Moderators additionally get
the Q&A-desk action in the header (UX gate; the server enforces the per-session
SessionModerator grant).

## 3. UI & behaviour
- Loading spinner → content; 404 → `SimfEmptyState` (not-found); other failures
  → `SimfErrorState` + retry. All states sit in an always-scrollable list so
  **pull-to-refresh** (`SimfPullToRefresh`) works everywhere.
- Header card (889:2716): gold `02` day-ordinal badge (falls back to the session
  code, D-567), 16px SemiBold title, LTR meta line (clock + `09:00 — 10:30` ·
  calendar + weekday/day/month via the shared `gregorianWeekdayName` /
  `gregorianMonthName` helpers — frame spelling `الاثنين`), and the two
  always-shown action buttons (ملخص الجلسة gold hairline → AI summary #34,
  رابط الجلسة beige hairline → live #25).
- Speakers (889:2722…): `SessionSpeakerCard` — 40×40 SpeakerPhoto asset
  (D-357, person-glyph fallback), name + country-flag emoji, rank · host line;
  tap → speaker profile #20.
- اسأل المحاور (1056:12876): enabled only once the caller holds a booking
  (join-gated, #3); disabled shows the "join first" hint only when a Join CTA
  is actually visible.
- Join section (D-485): no booking → gold full-width الانضمام إلى الجلسة
  (`SessionJoinButton`; open-seating = confirm + one-tap join, assigned-seat →
  seat picker #109). Booked → مقعدي `SessionReservationCard` (seat or general
  admission; approved swaps the pending hint for the show-your-badge hint,
  D-572; seat-specific bookings open my-seat #18).
- CTA row (897:2872): أضف إلى تقويمي (gold, device calendar via
  `sessionCalendarProvider`, E4) + تذكير (outlined; **deferred toast — D-300**,
  the notifications-platform pass owns real reminders).
- Cancel (booked only): plain white text line under the CTA row (owner
  2026-06-30); failures surface the backend's localized reason.

## 4. Button / action audit (Level F, 2026-07-03)
| Control | Handler | Backend |
|---|---|---|
| Back (circled) | `backOrHome` | — |
| Moderator Q&A (moderator only) | push `sessionModerate` #104 | server-gated |
| ملخص الجلسة | push `aiSummary?sessionId=` #34 | `GET …/summary` (on that screen) |
| رابط الجلسة | push `liveBroadcast?sessionId=` #25 | — |
| Speaker card | push `speakerProfile` #20 | — |
| اسأل المحاور (join-gated) | push `sendQuestion?sessionId=` #26 | — |
| الانضمام إلى الجلسة | confirm → `POST …/seats/join` or seat picker #109 | D-485 |
| مقعدي card (seat-specific) | push `mySeat` #18 | — |
| إلغاء (booked) | confirm → `DELETE …/seats/mine` | D-485 |
| أضف إلى تقويمي | device calendar insert (E4) | client-local |
| تذكير | deferred-notice toast (**intentional stub — D-300**) | — |
| Pull-to-refresh / retry | `_load()` re-fetch | both GETs |

All data on the page is repo-backed; no hardcoded content.

## 5. Clean-code freeze (D-597)
1,375 → 346-line screen + 8 widget files (all <400): state/composition stays in
the screen; every section widget is stateless and receives data + callbacks.
Figma pixel pass vs 889:2450: fixed the weekday spelling (`الإثنين`→`الاثنين`,
now via the shared core helpers — also killed the local weekday/month array
copies) and the shared bottom-nav sessions label (`الأجندة`→`الجلسات`, nav
component 206:1732 — chrome fix, applies app-wide). Golden re-locked at the
frame; behaviour byte-identical otherwise (83 module tests green).
