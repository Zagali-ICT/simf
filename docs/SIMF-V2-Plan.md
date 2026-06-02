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
