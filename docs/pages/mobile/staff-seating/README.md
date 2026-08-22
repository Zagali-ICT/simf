# Guest seating desk (إرشاد الضيوف للمقاعد) — mobile `/staff/seating/:sessionId`

| Field | Value |
|---|---|
| Route | `/staff/seating/:sessionId` (`RouteNames.staffSeating`, page #118) · role-gated to `{AppRole.staff}` in `_routeRoles`. The server independently enforces the `Seating.Assist` permission (D-563) — an operational grant carried by the Staff app role, **not** an admin RBAC role — so the app gate is only a UX guard. |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/staff/staff_seating_screen.dart` (`StaffSeatingScreen`, 300 lines, `ConsumerStatefulWidget`) |
| Widgets | `features/staff/widgets/desk_card.dart` (`DeskCard`) · `desk_row.dart` (`DeskRow`) · `occupant_header.dart` (`OccupantHeader`); shared `SimfScannerBody`, `SeatMapAsyncView`, `HallSeatMapCard`, `MaxWidthBody`, `WindowSize` |
| Figma node | **None.** No frame exists — owner request 2026-07-26; the layout is derived from the visitor seat picker (#109) |
| Shell | `SimfPageShell` (title إرشاد الضيوف للمقاعد, `tab: SimfTab.sessions`) |
| API | `POST /app/staff/sessions/{id}/seating/by-badge` · `GET /app/staff/sessions/{id}/seating/seat?rowLabel=&seatNumber=` · `GET /app/staff/sessions/{id}/seating/occupant/{userId}/photo` (bytes) · `GET /app/sessions/{id}/seats` for the hall plan |
| Providers | `staffSeatingRepositoryProvider` (`features/staff/data/staff_seating_repository.dart`) · `seatMapProvider(sessionId)` (`features/sessions/data/seat_map_repository.dart`) |
| Tests | `test/features/staff/staff_seating_screen_test.dart` (4). No golden (no bound frame). E2E [`mobile-staff-seating.md`](../../../tests/e2e/mobile-staff-seating.md) |
| Status | ✅ Real — built D-771 |

## 1. Purpose

An on-floor desk for staff guiding guests to their seats. It answers the same
question from both ends:

- **(a) scan a badge QR → where does this guest sit?**
- **(b) tap a seat on the hall plan → who sits there?**

> **Contract (D-771).** This is the visitor seat picker turned into a desk.
> Tapping a seat asks *who sits there* instead of selecting it, and the badge
> scanner answers the opposite question. The guest photo comes through the
> authenticated Dio bytes path (D-422) — **never** `Image.network`, which cannot
> carry the bearer token.

## 2. Audience & access

Staff only in the router. The server is the real authority: every one of the three
seating reads is gated on `Seating.Assist`.

## 3. Entry point

Session detail (#17) — for a Staff user the header's trailing action becomes
`Icons.event_seat_outlined` (instead of the moderator's `Icons.forum_outlined`),
tooltipped `staffSeatingTitle`, pushing this route with the session id
(`session_detail_screen.dart:268`, gated by `canAssistSeating(role)`).

## 4. Layout

`SeatMapAsyncView` owns the load/error/refresh of the hall plan and hands the
resolved `SessionSeatMap` to `_desk`, which wraps everything in an explicit
`Directionality` and a `MaxWidthBody(maxWidth: SimfTokens.staffSeatingMaxWidth)`:

1. The session title (or the page title when the map carries none), centred.
2. `l10n.staffSeatingIntro` — "امسح بطاقة الضيف لمعرفة مقعده، أو اضغط على أي مقعد
   لمعرفة صاحبه."
3. **Scanner card** and **result card**, side by side on a tablet and stacked on a
   phone. The bucket comes from `WindowSize.of(context).isCompact` — the shared
   responsive API, never a hardcoded pixel test.
4. **Hall plan card** below.

## 5. The two lookups

Both funnel through `_lookup`, which is single-flighted on `_busy`:

| Path | Repository call | Notes |
|---|---|---|
| (a) badge | `lookupByBadge(sessionId, qrId)` → `POST …/seating/by-badge` body `{qrId}` | `SimfScannerBody` supplies the same viewfinder + manual-entry field every other SIMF scanner uses (D-737), so the desk needs no bespoke reader |
| (b) seat | `lookupSeat(sessionId, rowLabel:, seatNumber:)` → `GET …/seating/seat` with both as query parameters | `HallSeatMapCard` is passed `inspectMode: true` so **every** seat is tappable — reserved, own, VVIP and VIP alike — because the desk is inspecting occupants, not reserving |

`_busy` is cleared in a `finally`, so any throw un-freezes the desk; every
post-await `setState` is `mounted`-guarded, so backing out mid-request is safe.

On a hit with `hasPhoto && userId != null`, `_lookup` then fetches
`occupantPhoto(sessionId, userId)` — `GET …/seating/occupant/{userId}/photo`
through `SimfApiClient.getBytes`. A 404 returns `null` rather than throwing, so
the card falls back to the placeholder avatar.

## 6. Result card

One `DeskCard` renders every outcome:

| Case | Render |
|---|---|
| Error set | The error text alone |
| Nothing looked up yet | `l10n.staffSeatingIntro` as a hint |
| `found == false`, `rowLabel == null` | `staffSeatingNoSeat` — a valid badge holding no seat in this session |
| `found == false`, `rowLabel != null` | `staffSeatingSeatEmpty` — the tapped seat is free |
| `found == true` | `OccupantHeader` (name + photo) then `DeskRow`s: seat (`rowLabel` + `seatNumber`), reservation reference, guest, and a tier-labelled check-in line |

The **guest** row prefers `localizedGuestHint` over `localizedName`: a VVIP
protocol seat has no registration, so the administrator's manual note **is** the
occupant record. The tier row's *label* is the tier (`seatTierVvip` /
`seatTierVip` / `seatTierNormal`) and its *value* is the check-in state
(`staffSeatingCheckedIn` / `staffSeatingNotCheckedIn`).

A `TextButton` labelled `staffSeatingClear` resets `_result` / `_photo` /
`_error`; it is disabled while `_busy`.

## 7. Error mapping

`_lookup` catches `ApiFailure` and keys off `failure.code`:

| Code | Message |
|---|---|
| `ATTENDEE_QR_UNKNOWN` | `l10n.staffSeatingUnknownBadge` — the badge is not recognised |
| anything else | `l10n.staffSeatingLookupFailed` |

Note that **`found: false` is not an error** — an empty seat and a seatless badge
are valid answers and are rendered as messages inside the result card, not as
failures.

## 8. Data contract (`StaffSeatOccupant`)

Wire keys (D-219 frozen), mirroring `SIMF.Contracts.Sessions.StaffSeatOccupant`:
`found` · `rowLabel` · `seatNumber` · `tier` · `reservationId` · `kind` ·
`status` · `userId` · `displayName` · `displayNameArabic` · `guestHint` ·
`guestHintArabic` · `hasPhoto` · `qrId` · `checkedIn`.

Both lookups return this one shape, which is why one result card serves both.

## 9. States

| State | Render |
|---|---|
| Seat map loading / error / empty | Owned by the shared `SeatMapAsyncView`, with `refreshAsync(ref, seatMapProvider(sessionId).future)` as its pull |
| Desk idle | Scanner card + hint card + hall plan |
| Busy | The plan's taps and the clear button are disabled |
| Result / no-result / error | The single result card above |

Pull-to-refresh is present but lives on `SeatMapAsyncView` rather than in this
file — which is exactly the delegated-`onRefresh:` case
`test/repo/pull_to_refresh_coverage_test.dart` documents as load-bearing.

## 10. i18n / RTL

`AppL10n`: `staffSeatingTitle` · `staffSeatingIntro` · `staffSeatingScanLabel` /
`staffSeatingScanCta` / `staffSeatingScanHint` · `staffSeatingUnknownBadge` /
`staffSeatingLookupFailed` · `staffSeatingNoSeat` / `staffSeatingSeatEmpty` ·
`staffSeatingSeat` / `staffSeatingSeatValue(row, seat)` /
`staffSeatingReference` / `staffSeatingGuest` · `staffSeatingCheckedIn` /
`staffSeatingNotCheckedIn` · `staffSeatingClear` · `seatTierVvip` / `seatTierVip`
/ `seatTierNormal`. The desk sets its own `Directionality` from `l10n.isArabic`
so the hall plan mirrors with the rest of the page.

## 11. Findings (recorded, not changed)

1. **`kind`, `status` and `qrId` are decoded and never rendered.** The result card
   shows seat, reference, guest and check-in only, so the reservation kind
   (self-booked vs. protocol) and its booking status are invisible to the
   operator even though the server sends them.
2. **A failed photo fetch is indistinguishable from "no photo"** — `occupantPhoto`
   swallows every `ApiFailure`, so a transient 500 renders the same placeholder
   as a guest who never uploaded a picture.
