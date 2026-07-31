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
| Tests | `test/features/sessions/session_detail_screen_test.dart` (+ `session_detail_body_test.dart`, `session_detail_models_test.dart`, `seat_map_models_test.dart`, `widgets/session_header_card_test.dart`, `widgets/session_speaker_card_test.dart`); golden `test/golden/session_detail_golden_test.dart` (`goldens/session_detail_889-2450.png`); E2E [`mobile-session-detail.md`](../../../tests/e2e/mobile-session-detail.md) |
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

**DEF-MOD-003 / DEF-MOD-004 / DEF-MOD-008 (2026-07-26) — the affordances now match
the router.** The ask card and the join / my-seat section open **attendee-only**
routes (#26 send-question, #18 my-seat, #109 seat picker are gated to
Visitor+Exhibitor in `_routeRoles`), so a signed-in **Staff / Moderator** used to be
shown enabled controls that the role gate then bounced back to Home. The screen now
asks `routeAllowsRole(...)` — the router's own table — before offering either, and
skips the seat-map fetch entirely for a role that cannot join. A **guest** still sees
the ask card DISABLED (that is the sign-in nudge, not a dead control). The moderator
Q&A action reads **`effectiveAppRole`** (the role the router gates on) instead of the
raw `appRole`, so an **unapproved** moderator — who presents as a guest under D-666 —
is no longer offered a desk entry that bounces.

## 3. UI & behaviour
- Loading spinner → content; 404 → `SimfEmptyState` (not-found); other failures
  → `SimfErrorState` + retry. All states sit in an always-scrollable list so
  **pull-to-refresh** (`SimfPullToRefresh`) works everywhere.
- Header card (889:2716): gold `02` day-ordinal badge (falls back to the session
  code, D-567), 16px SemiBold title, the **category tag pill** (PAR-D3 — a small
  gold-hairline pill bound to `localizedCategory(isArabic)`, rendered only when
  the session carries a category; the `SessionCategory` lookup ships empty
  pending the client's list, OI-2 / D-226), LTR meta line (clock + `09:00 — 10:30` ·
  calendar + weekday/day/month via the shared `gregorianWeekdayName` /
  `gregorianMonthName` helpers — frame spelling `الاثنين`), and the two
  always-shown action buttons (ملخص الجلسة gold hairline → AI summary #34,
  رابط الجلسة beige hairline → live #25).
- Speakers (889:2722…): `SessionSpeakerCard` — 40×40 SpeakerPhoto asset
  (D-357, person-glyph fallback), name + country-flag emoji, rank · host line —
  a **host** (per-session `SessionSpeakerRole.host`) carries the gold **star**
  glyph beside المضيف (PAR-P4a, the marker `speaker_list_card`'s own D-432 note
  already promised lives here); tap → speaker profile #20.
- **Workshop reduction (#29, owner Q10 2026-07-30):** when the detail's `type` is
  `SessionType.workshop` the body renders the header card **title + time block
  only** — no description, speakers, ask card, seat/join section, CTA row or
  live/summary actions. Any other type, and a `null` type from an older API,
  renders the full detail. The CP half reuses the existing session admin, so
  there is no new Control-Panel surface.
- اسأل المحاور (1056:12876): enabled only once the caller holds a booking
  (join-gated, #3); disabled shows the "join first" hint only when a Join CTA
  is actually visible.
- Join section (D-485): no booking → gold full-width الانضمام إلى الجلسة
  (`SessionJoinButton`; open-seating = confirm + one-tap join, assigned-seat →
  seat picker #109). Booked → مقعدي `SessionReservationCard` (seat or general
  admission; approved swaps the pending hint for the show-your-badge hint,
  D-572; seat-specific bookings open my-seat #18).
- Seat-map load failure (#18, owner 2026-07-21): an **approved** attendee whose
  seat-map fetch fails (`_seatMapError`) gets a `seatMapError` message + **Retry**
  where the Join CTA would be — never a silently-absent button; Retry re-runs
  `_load()`. A guest/pending null map still legitimately hides the join (that is
  not an error, so no retry is shown). Not offered on an ended session.
- CTA row (897:2872): أضف إلى تقويمي (gold, device calendar via
  `sessionCalendarProvider`, E4) + تذكير (outlined; **deferred toast — D-300**,
  the notifications-platform pass owns real reminders).
- Cancel (booked only): plain white text line under the CTA row (owner
  2026-06-30); failures surface the backend's localized reason. **A13
  (2026-07-27):** the line reads **إلغاء الحجز / Cancel booking**
  (`cancelBookingCta`), not the bare إلغاء — it must agree with the dialog it
  opens, which is titled إلغاء الحجز (`cancelBookingConfirmTitle`). As a
  side-effect the screen's line and the dialog's own dismiss button (إلغاء) are
  no longer the same words, so they are unambiguous to find and to read out.

## 4. Button / action audit (Level F, 2026-07-03)
| Control | Handler | Backend |
|---|---|---|
| Back (circled) | `backOrHome` | — |
| Moderator Q&A (moderator only, `effectiveAppRole`) | push `sessionModerate` #104 | server-gated |
| ملخص الجلسة | push `aiSummary?sessionId=` #34 | `GET …/summary` (on that screen) |
| رابط الجلسة | push `liveBroadcast?sessionId=` #25 | — |
| Speaker card | push `speakerProfile` #20 | — |
| اسأل المحاور (join-gated; attendee roles only — DEF-MOD-003) | push `sendQuestion?sessionId=` #26 | — |
| الانضمام إلى الجلسة (attendee roles only — DEF-MOD-004) | confirm → `POST …/seats/join` or seat picker #109 | D-485 |
| مقعدي card (seat-specific) | push `mySeat` #18 | — |
| إلغاء الحجز (booked — A13) | confirm → `DELETE …/seats/mine` | D-485 |
| أضف إلى تقويمي | device calendar insert (E4) | client-local |
| تذكير | deferred-notice toast (**intentional stub — D-300**) | — |
| Pull-to-refresh / retry | `_load()` re-fetch | both GETs |

All data on the page is repo-backed; no hardcoded content.

## 4b. Geofence self check-in (`geofence-self-checkin`, 2026-07-30)

The attendee-facing half of D-241 (FR-305/506). The backend shipped the arrival
/ departure / status endpoints with D-241 and **nothing in the app ever called
them**; this closes that.

| Piece | File |
|---|---|
| Repository | `lib/features/sessions/data/hall_attendance_repository.dart` — `getStatus` / `recordArrival(lat, lon)` / `recordDeparture`, the `HallAttendanceStatus` decode, and the three error codes the UI branches on |
| Action widget | `lib/features/sessions/widgets/session_arrival_action.dart` — `SessionArrivalAction` ("أنا هنا / I'm here" ↔ "تسجيل المغادرة / Check out" + the recorded arrival time on the Saudi clock) |
| Location seam | `lib/core/location/device_location.dart` — `DeviceLocation` / `deviceLocationProvider` |
| API | `POST /app/sessions/{id}/arrival` · `POST /app/sessions/{id}/departure` · `GET /app/sessions/{id}/attendance` (all `RequireApprovedAccount`; self-service, no admin permission) |
| Tests | `test/features/sessions/widgets/session_arrival_action_test.dart` (8 widget + 3 decode cases); E2E `E2E-MOB017-035` |

- The **server decides**: it checks the reported point against the hall geofence
  and either opens an attendance row or refuses with a coded error. The raw
  coordinates are never persisted (FDS-003 §10) — only enter/leave instants.
- **Inert until a hall has a boundary.** `HALL_GEOFENCE_NOT_CONFIGURED` renders
  as a plain "no boundary set yet" message, not an error. That is the expected
  state until the CP geofence page is populated (owner Q6, 2026-07-30).
- `NOT_AT_VENUE` reads "outside the hall boundary"; every other refusal (e.g.
  `SESSION_NOT_LIVE`) shows the server's own bilingual message verbatim.
- **Two follow-ups, reported rather than silently shipped:** (1)
  `SessionArrivalAction` needs one line in `widgets/session_detail_body.dart` to
  appear — that file belongs to another track this round; (2) this build carries
  **no** location plugin, so `DeviceLocation` answers `unavailable` and the
  action takes the "location required" path on a device. Supplying a real reader
  is a single `deviceLocationProvider` override, but it also adds
  `ACCESS_FINE_LOCATION` to the Android manifest and
  `NSLocationWhenInUseUsageDescription` to `Info.plist` — store-review and
  NCA-disclosure surface, so an owner decision, not a code one.

## 5. Clean-code freeze (D-597)
1,375 → 346-line screen + 8 widget files (all <400): state/composition stays in
the screen; every section widget is stateless and receives data + callbacks.
Figma pixel pass vs 889:2450: fixed the weekday spelling (`الإثنين`→`الاثنين`,
now via the shared core helpers — also killed the local weekday/month array
copies) and the shared bottom-nav sessions label (`الأجندة`→`الجلسات`, nav
component 206:1732 — chrome fix, applies app-wide). Golden re-locked at the
frame; behaviour byte-identical otherwise (83 module tests green).
