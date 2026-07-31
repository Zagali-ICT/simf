# geofence-self-checkin — the attendee-facing hall check-in had a backend and no app screen

Item ref: `geofence-self-checkin` (Track D-b, fix-all run 2026-07-30).
Files touched (all **new** except the docs):
`src/Mobile/simf_app/lib/core/location/device_location.dart` ·
`src/Mobile/simf_app/lib/features/sessions/data/hall_attendance_repository.dart` ·
`src/Mobile/simf_app/lib/features/sessions/widgets/session_arrival_action.dart` ·
`src/Mobile/simf_app/lib/app/localization/app_l10n.dart` (Track D-b block) ·
`src/Mobile/simf_app/test/features/sessions/widgets/session_arrival_action_test.dart` ·
`docs/tests/e2e/mobile-session-detail.md` · `docs/pages/mobile/session-detail/README.md`.

## DECISIONS_LOG

### D-NEXT — geofence-self-checkin: the attendee half of the D-241 hall arrival chain is built, and stays inert until a hall is given a boundary

D-241 shipped `POST /app/sessions/{id}/arrival`, `POST …/departure` and
`GET …/attendance` (all `RequireApprovedAccount`, self-service). **No Flutter
caller ever existed** — a repo-wide grep for `arrival|departure|geofence` across
the app returned only unrelated doc comments and the delegation
`arrivalDate`/`departureDate` fields. The feature had been deferred behind the
D-211 **G-OI-2** venue-boundary decision.

**Owner decision Q6 (2026-07-30): build it CP-configurable, so the app half is
unconditional.** G-OI-2 blocks *seeding* a boundary, not *writing the client*.
The app half is now built and the feature is inert by data, not by dead code:

- `HallAttendanceRepository` — `getStatus` / `recordArrival(lat, lon)` /
  `recordDeparture`, plus a tolerant `HallAttendanceStatus` decode (`method`
  accepts the int **or** the name wire form and degrades to null on an unknown
  value, matching how `SessionType` already decodes).
- `SessionArrivalAction` — "أنا هنا / I'm here", flipping to
  "تسجيل المغادرة / Check out" once an arrival exists, with the recorded arrival
  time rendered on the Saudi wall clock via `formatSaudiTime12` (never
  `toLocal()`, D-770).
- **The server decides.** The client reports a position; the server checks it
  against the hall geofence and either opens the attendance row or refuses with
  a coded error. Raw coordinates are never persisted (FDS-003 §10).
- **A hall with no boundary is not an error.** `HALL_GEOFENCE_NOT_CONFIGURED`
  renders as a plain "no boundary set yet" message and leaves the CTA in place.
  `NOT_AT_VENUE` reads "outside the hall boundary"; every other refusal (e.g.
  `SESSION_NOT_LIVE`) shows the server's own bilingual message verbatim, so a
  new server-side rule needs no client change.

**Two gaps recorded rather than papered over.**

1. **Not mounted.** The action lives in its own widget file; mounting it needs
   one line in `features/sessions/widgets/session_detail_body.dart`, which is
   another track's file this round. Reported in that run's `filesOutsideScope`.
2. **No location plugin in this build.** `pubspec.yaml` carries no geolocation
   package and neither platform declares a location permission. The read is
   therefore behind an overridable seam, `DeviceLocation` /
   `deviceLocationProvider`, whose default answers `unavailable` — so the whole
   path (request → wire call → every server outcome → every rendered state) is
   built and tested now, and supplying a real reader later is a one-provider
   override. Adding the plugin is **not** a code decision: it adds
   `ACCESS_FINE_LOCATION` to the Android manifest and
   `NSLocationWhenInUseUsageDescription` to `Info.plist`, both store-review and
   NCA-disclosure surface. Shipping a fake fix (e.g. a hardcoded coordinate) was
   rejected outright — it would have produced a green screen that records
   attendance for someone who is not in the room.

**Why `unavailable` and `denied` are distinct outcomes.** A missing capability
must never render as "you are outside the hall": that would send an attendee
walking towards a hall they are already standing in. `DeviceLocationOutcome`
separates granted / denied / unavailable and the action never posts a position
it does not have.

**Tests:** `session_arrival_action_test.dart` — 8 widget cases (arrival posts the
exact position and renders the returned status; check-out; an already-arrived
mount opens on Check out; the three server refusals; the two location-less
paths post nothing; Arabic wording) + 3 wire-decode cases. All fail on the
pre-fix tree, where none of these classes existed.

## PAGE-INDEX

Replace the `#17 sessionDetail` row (line ~251) with — **note:** Track D-a also
rewrites this row for `#29` / `PAR-D3` / `PAR-P4a`; merge the two clauses rather
than replacing one with the other.

| #17 `sessionDetail` (`GET /app/programme/sessions/{id}` + `…/sessions/{id}/seats`; `POST …/sessions/{id}/arrival` + `…/departure` + `GET …/attendance` — geofence self check-in) | ✅ Real — Figma 889:2450; **clean-code frozen (D-597)**. **geofence-self-checkin (2026-07-30):** the attendee "أنا هنا" arrival/departure action is built (`SessionArrivalAction`); inert until a hall is given a geofence, and behind a `DeviceLocation` seam until a location plugin is approved | Guest+ (seat card + check-in: approved account) | [mobile/session-detail/](mobile/session-detail/README.md) _(legacy: [App/Page_017](../App/Page_017/README.md))_ | [e2e/mobile-session-detail.md](../tests/e2e/mobile-session-detail.md) |

## E2E-README

Replace the `#17 sessionDetail` row (line ~255) with — same merge note as above;
Track D-a's rewrite extends the range to `-034`, this item adds `-035`:

| #17 `sessionDetail` (`GET /app/programme/sessions/{id}` + `…/sessions/{id}/seats` + `POST …/arrival` / `…/departure` / `GET …/attendance`) | [`mobile-session-detail.md`](mobile-session-detail.md) | E2E-MOB017-001..035 |

**Roll-up:** this item adds **+1** Coverage-matrix row (`E2E-MOB017-035`) — Track
D-a's rows in the same file are counted separately by that track.
`E2eCatalogueIntegrityTests.The_index_roll_up_matches_the_catalogue_it_describes`
asserts `**Total scenarios:** N` equals the real row count, so bump it by 1 when
merging (Track D-b contributes **+10** in total).
