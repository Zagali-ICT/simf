# Prep — shared catalogue files (permissions, error codes, notification kinds, nav)

Item ref: `prep-shared-catalogues` (Prep agent, fix-all run 2026-07-30).
Files touched: `PermissionCatalog.cs`, `ErrorCodes.cs`, `NotificationKind.cs`, `CpNavigation.cs`.

## DECISIONS_LOG

### D-NEXT — shared catalogue codes reserved up-front for the fix-all run; the last CP stub is permission-gated

The fix-all run splits 34 defects across six parallel tracks. Four files are
edited by almost every track — `PermissionCatalog.cs`, `ErrorCodes.cs`,
`NotificationKind.cs` and `CpNavigation.cs` — and two of them allocate scarce
shared values (a permission code string, an enum integer). Parallel agents
editing them collide, and two agents independently picking the next free
`NotificationKind` integer produces a silent value clash rather than a merge
conflict (that already happened once: `ExhibitorLeadCaptured` and
`SessionCancelled` both took 57, see the merge note in the enum). So the four
files were assigned to a single prep pass that runs before the tracks and
reserves everything they need.

Reserved this pass:

- `PermissionCatalog.HallGeofence.View` / `.Manage` — Q6 (build the geofence
  CP-configurable). Its own page rather than reusing `Halls.Edit` because a
  boundary change silently decides who can mark themselves present at a hall:
  `View` reads the configured boundaries, `Manage` sets or clears one. Both
  `AdminOnly`, so the frozen Security / Scientific baseline sets (D-752) are
  untouched. The underlying columns already exist — `Hall.GeofenceCenterLat` /
  `GeofenceCenterLon` / `GeofenceRadiusMeters` — so **no migration**, and the
  seeder is idempotent so **no schema change** for the `Permission` rows either.
- `PermissionCatalog.DelegationMeetings.Export` — OA-D5. Mirrors the existing
  `SpeakerMeetingRequests.Export`; the delegation grid had `View` + `Manage`
  only, so its XLSX export had no gate to hang on.
- `ErrorCodes.AuthTwoFactorEnrolmentRequired` (`AUTH_TWO_FACTOR_ENROLMENT_REQUIRED`)
  — A1 / `#2` under Q1 (enrolment-first). Raised on the sign-in path when a CP
  password sign-in succeeds against an account with no second factor: no token
  is issued, the CP routes to enrolment. Distinct from `TOTP_NOT_ENABLED`, which
  marks a TOTP *action* attempted on an unenrolled account.
- `ErrorCodes.ContentMarkdownUnsafe` (`CONTENT_MARKDOWN_UNSAFE`) — E4 /
  `FR-1203-markdown-render`. Raised on the admin write path when the sanitizing
  pipeline rejects submitted markup, so an admin-editable field can never reach
  the public surface as unsanitised HTML.
- `NotificationKind.SessionNotAttended = 59` (FR-903) and
  `NotificationKind.MatchRecommended = 60` (FR-803). Additive values appended
  after `SessionCancelled = 58`; persisted by NAME, so no schema or wire change
  (D-110 additive rule, Q8).

The **security half of `cp-stub-modules` (Q4) is discharged in the same pass**:
`Module.LiveSessions` was the last `IsStub` nav entry and carried
`RequiredPermission: null`, which made it visible to *every* signed-in admin
regardless of role — the "Soon" badge is a label, not a gate. It now sits behind
`Sessions.View`, the programme-read permission the console it stands in for would
need. Building a Live Sessions console stays out of scope this round (Q4), so the
entry remains a stub; the assistant directory already excludes `IsStub` items, so
nothing starts advertising a page that does not exist.

Two catalogue additions are deliberately **not** made:

- No new movement/dwell report permission for C8 (`FR-1103`). The read surface
  belongs behind the existing `Attendance.View`, which already gates the
  session-attendance dashboard over the same `HallAttendance` records.
- No SMS/WhatsApp channel error code for C7. The gateways are deferred to
  procurement, and a reserved code for an unbuilt channel is dead vocabulary.

Also recorded, because it changes what the geofence work should build: the
per-hall geofence is **already CP-editable today** inside the hall form
(`HallsAddEdit.razor:54-66`, three fields gated by `Halls.Create` / `Halls.Edit`),
and the whole server-side GPS arrival path already exists
(`HallAttendanceService.RecordGeofenceArrivalAsync`, raising
`HALL_GEOFENCE_INVALID` / `HALL_GEOFENCE_NOT_CONFIGURED` / `NOT_AT_VENUE`). The
new page is therefore an **overview** — every hall's boundary in one grid, with
which halls have none — not a re-implementation of the per-hall fields.

## PAGE-INDEX

No row is added by this pass. The nav entry
`/admin/halls/geofence` → `Module.HallGeofence` is reserved in `CpNavigation`, but
the page itself is built by the geofence item; the PAGE-INDEX row must be added
**by that change, not before it**, because
`E2eCatalogueIntegrityTests.Every_route_PAGE_INDEX_calls_Real_actually_has_a_page`
fails the build on a route marked `✅ Real` that no `@page` declares.

Row for the geofence item to add once its page exists:

| `/admin/halls/geofence` | ✅ Real (D-NEXT) | Hall geofence boundaries — per-hall centre coordinate + radius for GPS self-check-in | `docs/pages/cp/halls-geofence.md` | `docs/tests/e2e/cp-admin-halls-geofence.md` |

## E2E-README

No registry row is added by this pass — it authors no catalogue file. The
geofence item registers `cp-admin-halls-geofence.md`, and the `cp-stub-modules`
item registers whatever file it authors for the gated stub; both must pick a
scenario-id namespace not already claimed
(`E2eCatalogueIntegrityTests.No_scenario_id_is_claimed_by_two_different_catalogue_files`)
and must update the `**Pages catalogued:**` / `**Total scenarios:**` roll-up,
which `The_index_roll_up_matches_the_catalogue_it_describes` pins to the real
counts.
