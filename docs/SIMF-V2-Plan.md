# SIMF — V2 Plan (next-version features)

| Field | Value |
|-------|-------|
| Document ID | SIMF-V2P-001 |
| Title | SIMF V2 Plan — next-version features |
| Version | 0.1 (DRAFT) |
| Status | Draft — backlog of features deferred to V2 (after the V1 event delivery) |
| Owner | SIMF Programme Owner |
| Date issued | 2026-06-02 |

This plan holds features intended for **V2** — beyond the V1 scope (the Login
API, Control Panel, Website, Flutter app, and the P1–P5 Completion Programme).
Items here are **not** built in V1; each is recorded with its intent, the gates
it must respect, what it builds on, and the open decisions still owed.

> When a V2 item is scheduled, it follows the same Definition of Done as V1
> (per-page/per-action permission + per-page E2E catalogue file + unit/integration
> tests + docs, in the same changeset) and the permanent Data ↔ Identity DB
> separation rule (see the project `CLAUDE.md`).

---

## V2-01 — Attendee discovery & in-app chat at the exhibition

**Owner request (2026-06-02).** A signed-in attendee can browse the other people
attending the exhibition, search them by shared interest, see a match's profile
photo, and chat with them — all behind strict presence + privacy gates.

### Behaviour

- **Discover attendees.** A user can view the people attending the exhibition
  (an attendee directory / "people like you" view).
- **Gate 1 — viewer must be checked in.** The directory is available **only when
  the viewing user is checked in** (physically present / attended). A user who is
  not checked in cannot browse other attendees.
- **Gate 2 — viewee must have opted in.** A person appears in the directory
  **only if their profile setting allows others to view their status/presence**
  (an explicit privacy opt-in; default off). Attendees who have not opted in are
  never listed or searchable.
- **Search by interest.** The viewer searches by interest (reusing the existing
  Interests picker). Matches = checked-in, opted-in attendees who share the
  selected interest(s).
- **Result card.** Each found person shows their **profile image** (+ name /
  interests as agreed).
- **Chat.** From a match the viewer can **start a 1:1 chat** with that person
  (in-app real-time messaging).

### Builds on (existing V1 surface)

- **Networking connections** (D-224, `Connection` entity, request/accept) — the
  connect relationship; chat may require an accepted connection (see OI-V2-01a).
- **Interests** (the visitor Interests picker + `Interest` lookup) — the search
  dimension.
- **Check-in / attendance** — `HallAttendance` + the venue/gate check-in (P5.1
  geofence/QR, gate scans) supply the "attending / checked-in" presence signal.
- **Profile + avatar** — `UserProfile` (a new privacy flag) + the existing avatar
  storage for the profile image.
- **SignalR** — the stack already runs hubs (e.g. `LiveAiHub`); the chat is a new
  hub/transport.

### New work V2-01 implies

- A new **profile privacy flag** on `UserProfile` (e.g. `IsDiscoverable` /
  "allow others to view my status") — opt-in, default off. (Additive column on
  `SIMF_App` — never an Identity-side change; the Identity schema is frozen and
  the two DBs stay separate.)
- A **presence/attendance read** that lists checked-in + opted-in attendees,
  filterable by interest, returning name + avatar (resolving the display name via
  the Identity round-trip — no cross-DB join, per D-157/D-246).
- A **1:1 chat** capability — a `ChatThread` / `ChatMessage` model on `SIMF_App`
  (user refs are bare `Guid`s, no cross-DB FK), a SignalR chat hub, app + (maybe)
  CP moderation surface, push/notification integration, and message retention +
  block/report controls.
- App screens: attendee directory, interest search, profile-card, chat thread.
- Per-page E2E catalogue files + permissions + tests for every new page/API.

### Privacy / NCA

This feature exposes **presence + identity + interests + messaging** — sensitive
personal data. It must be **strictly opt-in** (Gate 2), the presence signal must
not leak for non-opted-in or non-checked-in users, and chat content needs a
retention policy + moderation/abuse controls. Treat the whole feature under the
NCA data-handling posture (cf. FDS-003 §10 for presence).

### Open items (owner decisions owed before build)

- **OI-V2-01a — Chat eligibility.** Can a viewer chat any discoverable attendee
  directly, or only after a mutual **connection** is accepted (reuse D-224)?
- **OI-V2-01b — "Status" meaning.** Does "allowed to view status" mean *present
  now* (live, while checked in) or *attending this edition* (registered)? And is
  the directory live-presence or roster-based?
- **OI-V2-01c — What the card shows.** Profile image + name only, or also
  organisation / title / interests? (Mind privacy — only opted-in fields.)
- **OI-V2-01d — Chat scope + retention.** Real-time only vs persisted history;
  message retention window; block / report / mute; admin moderation visibility.
- **OI-V2-01e — Surface.** App-only (Flutter), or also a Website attendee
  directory? (V1 networking, D-224, is app-facing.)

---

## V2-02 — Configurable session lifetime (5-day biometric session)

**Owner request (2026-06-03).** "Session valid 5 days; within that window only
the biometric (face) is required to re-open — no re-login." Captured as **D1** in
the App page-spec resolution (`DECISIONS_LOG` D-249) and then **deferred to V2** by
the owner ("ignore … push for V2 plan D1") rather than bundled into the V1
App-page push.

### Intent

Make the refresh-token lifetime **config-bound to a single shared value** and set
it to **5 days**, so the password session and the biometric re-open window stay
consistent and are tunable without a code change.

### Current state (V1)

The refresh-token lifetime is hardcoded `TimeSpan.FromDays(30)` in **three**
sites, all of which must move together:

- `SIMF.Application.IdentityAccess.SignInService` — mints the refresh token on
  password sign-in.
- `SIMF.Application.IdentityAccess.SessionService` — re-issues it on
  `POST /auth/refresh` (rotation).
- `SIMF.Infrastructure.IdentityAccess.DeviceKeyService` — mints it on biometric /
  device-key sign-in.

### New work V2-02 implies

- One **strongly-typed Options** key (e.g. `Auth:RefreshTokenLifetimeDays`) bound
  in DI and consumed by all three services (the Options pattern, per `CLAUDE.md`
  §18) — set to **5**.
- The key **mirrored in both `appsettings` files** so the base config and the
  env-override never drift (§18).
- Tests asserting the new lifetime **and** that all three issue paths agree
  (a split value would mean a password session and a biometric session expire on
  different days).

### Gates

- **No schema change** — the lifetime sets a stored `ExpiresAt` `DateTime`, not a
  column; the D-110 / Identity freeze is untouched.
- **Security-sensitive + behaviour change** — dropping 30→5 days makes every user
  re-authenticate sooner; needs explicit owner sign-off and its own reviewable
  diff (the reason it is its own V2 item, not a V1 bundle).
- No test currently asserts the 30-day value, so the change is test-safe to make.

### Open items (owner decisions owed before build)

- **OI-V2-02a — One value or two?** Is "5 days" the target for **both** the
  password-session refresh token and the biometric device-key window, or should
  the biometric window differ?
- **OI-V2-02b — Remember-me.** Should the app expose a "keep me signed in" toggle
  that picks between two configured lifetimes, or is the 5-day window fixed?

---

## V2-03 — freezed for the wire models (deferred from the 2026-08 clean-code programme)

Adopt `freezed` across the 30 `lib/features/*/data/*_models.dart` files (63 classes
carrying a `fromJson`), generating `copyWith`, `==` and `hashCode`.

### Why it was deferred rather than built

It was in scope for the clean-code programme, chosen on the premise that it would
deliver Flutter's "strongly recommend immutable data models". Measuring the tree
before starting showed that premise does not hold: **839 final fields and 0 mutable
fields across the 30 model files.** The models are already immutable. What freezed
would actually add is `copyWith` — hand-written 3 times in the entire app — and
`==`/`hashCode`, of which there are currently none.

### Why the timing was wrong specifically

Riverpod 3 (landed 2026-08-18) filters provider updates by `==`. Today no model
defines `==`, so every rebuild yields a non-equal instance and every listener fires,
exactly as it did on Riverpod 2. Adding `==` to 63 wire classes switches app-wide
update filtering on in a single step: a refresh that returns byte-identical data
would silently stop notifying. That is usually the desired behaviour and occasionally
not, and `test/repo/wire_keys_ratchet_test.dart` guards key names, not rebuild
semantics. Doing it in the same window as the major-version bump would have made a
red suite impossible to attribute.

### How to build it when it is taken up

- **freezed WITHOUT `json_serializable`.** Keep every existing `fromJson`/`toJson`
  body verbatim as a manual factory. The decoders are tolerant by design
  (`json['x'] as String? ?? ''`), carry int-or-string enum readers, and
  `parseWireDateTime(value, 'fieldName')` passes the field NAME to its parser — a
  `JsonConverter` never receives the key, so porting would mean one converter class
  per field or losing the error message. Keeping the bodies means no `.g.dart` is
  generated at all, only `.freezed.dart`, and the wire literals never move.
- **Three config gaps must land first** (all measured 2026-08-18): both
  `packages/*/analysis_options.yaml` have **no `exclude:` block**, and CI analyses
  packages with infos fatal; the app-root `analysis_options.yaml` lacks
  `invalid_annotation_target: ignore`, which both packages already have; and
  `.gitignore` excludes `*.freezed.dart`. On that last one, read the comment already
  in `.gitignore` — the rule has silently swallowed generated source twice in this
  repo, once leaving `flutter build apk` broken from a clean clone. Either commit the
  generated files or add a `build_runner` step to `azure-pipelines.yml` before
  `flutter analyze`; do not leave it implicit.
- **Exclude the four device-persisted models permanently** —
  `gates/data/gate_scan_queue.dart`, `gates/data/gate_offline_config.dart`,
  `core/organization_profile/organization_profile.dart` and the auth cached-user map.
  Their JSON is written to device storage, so a renamed key breaks shipped installs
  at upgrade with no server involved; `PendingGateScan` holds attendance records that
  never reached the server.
- **Sequence in four batches**, one commit each, full gate between: config + one
  trivial model end-to-end, then read-only `fromJson` models, then models with domain
  methods, then the two `toJson` pairs. A red batch is reverted, not debugged forward.
