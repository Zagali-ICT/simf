# Feature Design Specification — B2B / B2C Business Meetings

| Field | Value |
|-------|-------|
| Document ID | SIMF-FDS-013 |
| Title | Feature Design Specification — B2B / B2C Business Meetings (+ flexible hall configuration & allocation) |
| Version | 1.0 (BUILT — D-248, 2026-06-03) |
| Status | Built and verified — shipped as one end-to-end commit (D-248) |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team |
| Owner | Product Owner |
| Approver | Product Owner |
| Date issued | 2026-06-03 |
| Related documents | SIMF-FDS-004 (Forum Programme — Hall), SIMF-FDS-005 (Bookings — the seat-reservation pattern reused here), SIMF-FDS-006 (Exhibition — Company, Booth), SIMF-FDS-008 (Networking — the audience→speaker `MeetingRequest`, and the V2-01 boundary), SIMF-DAT-001, the App `Page_014` dashboard dependency |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 0.1 | 2026-06-03 | Engineering & Architecture Team | First draft. Scope/spec for the admin-arranged B2B/B2C bilateral-meeting module. No code. |
| 0.2 | 2026-06-03 | Engineering & Architecture Team | Owner resolved the open items (§12): company-type party (OI-1); **flexible per-hall configuration + allocation** — purpose (booth / session / meeting), allocation by whole / random-by-count / by-row-column pre-reserved, each over a from–to time-slot (OI-2/OI-3); **group** meetings, not strictly bilateral (OI-5); free from–to time-slot (OI-8); **freeze lifted** for this module's tables (OI-7, "no freeze"). Two follow-on questions raised (OI-9, OI-10). Still no code — pending §13 build-plan approval. |
| 1.0 | 2026-06-03 | Engineering & Architecture Team | **Built (D-248)** as one end-to-end commit (owner: "do all", "generalize across all three", "one big commit"). OI-9 + OI-10 resolved; see §14 As-built. |
| 1.1-DRAFT | 2026-07-08 | Engineering & Architecture Team | **DRAFT amendment (D0-1) — pending owner sign-off; NO code until approved.** Adds the owner's "meeting-in-hall" request→review→speaker-approval workflow (owner batch item 7) + the app "real available slots" fix (item 6) on top of the built 1.0 core. The 1.0 admin-arranged path is unchanged; this adds the **attendee-request → admin-review-vs-hall-availability → speaker double-opt-in email → surfaces in اللقاءات الثنائية** flow that §2 of 1.0 explicitly excluded. Grounded in the shipped code (§15.2); open items in §15.9 need owner decisions before build. |
| 1.1-DRAFT (rev 2) | 2026-07-08 | Engineering & Architecture Team | **Owner resolved the three blocking open items (§15.9):** OI-A → **reuse the 3 built types** (g2b/g2vip are display labels on a BusinessMeeting, no new workflow); OI-D/OI-I → **speaker double-opt-in email with a signed single-use 72h token** (the new `AllowAnonymous` exception is **owner-approved**); OI-B → **new `HallAvailabilityWindow`** (symmetric with speaker availability). The remaining OIs (C/E/F/G/H) proceed on the documented recommendation unless the owner flags them. The spec is **build-ready pending the owner's final "go"**; the build follows the §15.10 DoD as one wave (Wave C, item 7 + item 6). |

---

> **Status note.** This is the documented-but-not-built dependency from
> `docs/App/Page_014/Page_014_Logic.md` (L-7); the dashboard `meetingsCount` already
> unions speaker meetings with these B2B/B2C meetings. The owner **lifted the freeze**
> for this module's new tables (OI-7, 2026-06-03 — consistent with the D-219
> directive). This draft still commits **no schema** — code follows the §13 plan once
> approved.

## 1. Purpose

This is the design specification for **admin-arranged B2B/B2C business meetings** at
the forum, and the **flexible hall configuration & allocation** they sit on. A
Control Panel operator configures each hall (its purpose and layout), reserves space
within a hall over a time-slot, and schedules a meeting between two or more named
parties at a reserved table. Meetings are confirmed on save — there is no
attendee-facing request queue. Confirmed meetings surface on each participant's
mobile dashboard (the `Page_014` meeting union that already reserves space for them).

## 2. Scope

The feature covers:

- **Per-hall configuration (flexible).** Each hall has **its own config** (owner:
  *"each hall has their own config — the system must be flexible"*). A hall is
  designated by **purpose** — a **booth** hall, a **session** hall, or a **meeting**
  hall (extensible). The hall's interior layout follows its purpose:
  - *Session* → seat grid (rows × columns, char-labelled — the existing
    `HallSeatLayout`, SIMF-FDS-005).
  - *Meeting* → a set of **meeting tables** (§5.3, new).
  - *Booth* → booths (the existing `Booth`, SIMF-FDS-006).
- **Flexible allocation / reservation.** Space in a hall is reserved by one of these
  modes, each over a **time-slot (from–to)**:
  - **Whole** — the entire hall.
  - **Random by count** — N units (tables/seats) allocated randomly, stopping at the
    count or the hall capacity.
  - **By row / column (pre-reserved)** — specific rows/columns reserved.
- **Scheduling a meeting** — an admin picks the **participants** (two or more), a
  reserved table, and a time-slot; the meeting is created **Confirmed**.
- **The four party pairings** (§5.5): B2B company↔company; B2C company/sponsor↔visitor;
  B2C sponsor↔visitor; visitor↔visitor. **Group meetings (more than two participants)
  are allowed** (OI-5).
- **Conflict rules** — one active reservation per unit per overlapping time-slot; a
  participant is not double-booked across overlapping meetings.
- **Cancellation** of a confirmed meeting.
- **The Control Panel module** — hall config/allocation + a Business Meetings page
  (list / schedule / cancel).
- **Feeding the mobile dashboard** meeting union (contract already shaped — no
  app-side schema change).

It does **not** cover:

- **Party self-service requests** — meetings are **admin-arranged only** (owner,
  2026-06-03). No request-and-approve queue.
- **Redesigning the existing session seat-booking or booth placement** — those keep
  working as built (SIMF-FDS-005 / FDS-006). Whether the new allocation layer is
  *generalised* to also drive them is **OI-9** (recommend: no, for V1).
- **The audience→speaker interview request** (`MeetingRequest`, D-174/D-183,
  SIMF-FDS-008) — separate, untouched.
- **The V2-01 attendee discovery + chat** feature — attendee-initiated, deferred to
  V2; this module's visitor↔visitor pairing is *admin-arranged* and distinct (OI-4).

## 3. Requirements and source

No SRS line exists yet (SIMF-SRS-001 is gate-blocked). Authority is the **owner
baseline**:

| Source | What it establishes |
|--------|---------------------|
| `Page_014_Logic.md` L-7 | "CP-managed, pre-reserved hall/table booking for bilateral meetings between company / visitor / sponsor — define halls/tables, then the reservation tables for the session/meeting, managed in the CP." |
| `Page_014_API.md` | Dashboard `meetingsCount` / today's schedule **already union** speaker meetings with B2B/B2C meetings; reflects speaker meetings only until this ships. |
| SIMF-Concept-Summary §7 / Appendix A | "one-to-one meetings" confirmed in scope (2026-05-20 meeting). |
| Owner answers, 2026-06-03 | Company-type party; flexible per-hall config + allocation (whole / random-by-count / row-column pre-reserved + time-slot from-to); purpose booth/session/meeting; **group** meetings; **no freeze**; free from–to time-slot. |

## 4. Feature overview

A business meeting is created already confirmed (admin-arranged) and can be cancelled:

```mermaid
stateDiagram-v2
    [*] --> Confirmed : admin schedules the meeting (participants + table + slot)
    Confirmed --> Cancelled : admin cancels
```

No `Pending` state — unlike booking approval (SIMF-FDS-005 §5.2). The booking
workflow's *mechanics* (resource hold, filtered-unique-index, race guard, Identity
round-trip for names, audit, bilingual notifications) are still reused.

## 5. Detailed behaviour

### 5.1 Per-hall configuration

- When defining/editing a hall, the admin sets its **purpose** — `Booth`, `Session`,
  or `Meeting` (extensible) — and the layout config appropriate to that purpose. Each
  hall is configured independently ("flexible").
- A `Session` hall uses the existing seat grid (`HallSeatLayout`). A `Meeting` hall
  uses **meeting tables** (§5.3). A `Booth` hall hosts booths.
- A hall holds one purpose at a time (OI-3).

### 5.2 Allocation / reservation (whole / random-by-count / row-column, over a time-slot)

- Space in a hall is reserved for a **time-slot (from–to)** by one of:
  - **Whole** — reserve the whole hall for the slot.
  - **Random by count** — allocate N units randomly; generation **stops at the count
    or the hall capacity** (whichever is smaller) — mirrors
    `SeatReservationService.ReserveRandomAsync` / `EnsureSessionHasCapacityAsync`.
  - **By row / column (pre-reserved)** — reserve named rows/columns — mirrors
    `AdminReserveRowAsync`.
- The same hall can therefore serve different slots flexibly; the §5.6 conflict rules
  forbid double-allocating a unit/time.

### 5.3 Defining meeting tables

- A `MeetingTable` belongs to a `Meeting`-purpose hall (App FK), carries a stable
  **label/code** (entered or random-generated), an optional location (row/column or
  free label), and a **capacity** (seats at the table — group meetings, OI-5).
- Soft-delete + bilingual conventions, as the rest of the codebase.

### 5.4 Scheduling a meeting

- **Trigger:** a CP operator with the meetings permission opens the meetings page and
  chooses *Schedule meeting*.
- **Inputs:** the **participants** (two or more), a reserved **table**, and a
  **time-slot** (from–to). The admin sets the **meeting type** (B2B / B2C).
- Each participant is a **Company** (`Company`, `Type` Exhibitor or Sponsor — OI-1) or
  a **Visitor** (Identity user, bare `Guid`).
- **Processing:** validate participants (≥ 2, active/approved, distinct); validate the
  table belongs to a meeting hall and is free for the slot; check §5.6 conflicts;
  create the meeting **Confirmed**; audit; notify all participants (§5.7).
- **Failure:** a participant inactive/not found, table taken for an overlapping slot,
  a participant already in an overlapping meeting → a clear bilingual error.

### 5.5 Party pairings

| Pairing | Type | Participant kinds |
|---------|------|-------------------|
| company ↔ company | B2B | Company + Company |
| company/sponsor ↔ visitor | B2C | Company + Visitor |
| sponsor ↔ visitor | B2C | Company(Type=Sponsor) + Visitor |
| visitor ↔ visitor | B2C | Visitor + Visitor (admin-arranged; ≠ V2-01) |
| group (≥ 3) | B2B / B2C (admin-set) | any mix (OI-10) |

### 5.6 Conflict rules

- **One active reservation per unit (table/seat/whole-hall) per overlapping
  time-slot** — filtered unique index + the `DbUpdateException` race guard from
  `SeatReservationService.PersistWithUniquenessGuardAsync`.
- **No participant double-booking** — a participant may not hold two confirmed
  meetings whose time windows overlap (mirrors `EnsureNoOverlapAsync`).

### 5.7 Cancellation & notifications

- An admin may cancel a confirmed meeting; the table/slot is released (soft-release,
  `Status = Cancelled`) and all participants are notified.
- On schedule and on cancel, notify each participant: a **visitor** directly; a
  **company/sponsor** via its `CompanyMembership` account(s). New additive
  `NotificationKind` values (e.g. `MeetingScheduled`, `MeetingCancelled`) persisted by
  name (append-only; preserves the frozen enum *wire* contract). Bilingual EN/AR.
  Notification failure never rolls back the meeting (swallow-and-log).

## 6. Data (additive on `SIMF_App` — freeze lifted, OI-7)

> New tables live on `SimfAppDbContext`. Visitor references are **bare `Guid`** logical
> FKs to `SimfUser.Id` on SIMF_Identity — **no EF navigation, no DB FK, no cross-DB
> join** (D-157/D-246); company refs are real App FKs. **No Identity-owned data is
> duplicated**; participant display names resolve on read via the Identity round-trip.
> The only allowed copy is the immutable audit snapshot.

| Entity / change | Key fields (proposed) |
|-----------------|------------------------|
| `Hall` (extend) | `Purpose {Booth, Session, Meeting}` + per-purpose config signal |
| `MeetingTable` (new) | `Id`, `HallId` (App FK), `Label`/`Code`, optional location (row/col/label), `Capacity` (group), `IsActive`, timestamps |
| `HallAllocation` (new) | `Id`, `HallId` (App FK), `Mode {Whole, RandomByCount, RowColumn}`, the allocated units, `Start`, `End`, `CreatedByUserId`, `ReleasedAt?`, timestamps |
| `BusinessMeeting` (new) | `Id`, `MeetingType {B2B, B2C}`, `MeetingTableId` (App FK), `Start`, `End`, `Status {Confirmed, Cancelled}`, `ScheduledByUserId`, `CancelledByUserId?`, `CancelledAt?`, timestamps |
| `BusinessMeetingParticipant` (new) | `Id`, `BusinessMeetingId` (App FK), `Kind {Company, Visitor}`, `PartyId` (Company App FK **or** visitor bare Guid), immutable display-name audit snapshot |
| New enums (additive) | `HallPurpose`, `HallAllocationMode`, `MeetingPartyKind`, `BusinessMeetingType`, `BusinessMeetingStatus` |

## 7. Surfaces

| Surface | Screens |
|---------|---------|
| Control Panel | Hall config (purpose + layout + allocation modes), **Business Meetings** page (list with type/participant/table/slot filters; *Schedule* modal; *Cancel*) |
| Mobile app | No new screen — confirmed meetings flow into the existing `Page_014` dashboard meeting union |
| Public website | None |

## 8. Validation rules

| Item | Rule |
|------|------|
| Participants | ≥ 2, distinct, each active/approved |
| Group size | ≤ table `Capacity` |
| Table | Required; must belong to a Meeting-purpose, active hall; free for the slot |
| Time-slot | `Start < End`; within event bounds; no unit or participant overlap (§5.6) |
| Meeting type | Admin-set B2B / B2C |
| Random-by-count allocation | Stops at the count or hall capacity, whichever is smaller |
| Cancellation reason | Optional (OI-6) |

## 9. Security and privacy

- **Permission (HARD RULE).** New permission codes in `PermissionCatalog` — e.g.
  `Meetings.View/Schedule/Cancel` and `Halls.Configure`/`MeetingTables.*` — seeded
  `AdminOnly`, gating **both** the API (`Policies(PolicyFor(...),
  nameof(AuthorizationPolicies.RequireApprovedAccount))`) **and** the CP page
  (`[RequirePermission]` + nav `RequiredPermission` + `<AuthorizedAction>`). Guard
  tests must pass.
- **Audit.** Hall-config / allocation / schedule / cancel write `OperationLog`;
  participant display-names captured as immutable audit snapshots (the only allowed
  Identity-data copy, D-246).
- **NCA / privacy.** A meeting exposes participant identity + presence — NCA
  data-handling posture; only needed fields shown.

## 10. Acceptance criteria

1. An admin can configure a hall's purpose (booth/session/meeting) and its layout.
2. An admin can allocate hall space by whole / random-by-count / row-column over a
   from–to time-slot; random allocation stops at count or capacity.
3. An admin can schedule a meeting (incl. group ≥ 3) between any pairing at a table
   for a time-slot; it is created Confirmed and all participants are notified.
4. Unit/time and participant/time conflicts are rejected with a clear bilingual error.
5. An admin can cancel a confirmed meeting; the slot frees and all participants are
   notified.
6. Confirmed meetings appear in each participant's mobile dashboard meeting union.
7. Every API + CP surface is permission-gated; guard tests pass.

## 11. Test scenarios (become the E2E catalogue at build)

| # | Scenario | Expected |
|---|----------|----------|
| T-01 | Configure a hall as Meeting + generate tables randomly | tables created up to count/capacity; reset clears + regenerates |
| T-02 | Allocate a hall whole / by row-column / random-by-count for a slot | allocation recorded; conflicting slot rejected |
| T-03 | Schedule a B2B company↔company meeting | Confirmed; both companies notified |
| T-04 | Schedule a B2C sponsor↔visitor meeting | Confirmed; both notified |
| T-05 | Schedule a group meeting (3+ participants) | Confirmed if within table capacity; rejected if over |
| T-06 | Two meetings, same table, overlapping slot | second rejected (unit conflict) |
| T-07 | Same participant, two overlapping meetings | second rejected (participant conflict) |
| T-08 | Cancel a confirmed meeting | Cancelled; slot freed; all notified |
| T-09 | Non-permitted admin opens the page/endpoint | 403 / nav hidden |

At build, this becomes `docs/tests/e2e/cp-business-meetings.md` (+ README index +
PAGE-INDEX cross-ref + per-page reference doc), per the project DoD.

## 12. Open items

**Resolved (owner, 2026-06-03):**

| # | Item | Resolution |
|---|------|-----------|
| OI-1 | "Sponsor"/company party | **Company** (`Type` Exhibitor/Sponsor) — the account-bearing entity; not the display-only `Sponsor`. |
| OI-2 | Hall purpose + allocation | **Per-hall flexible config**: purpose booth/session/meeting; allocation by **whole / random-by-count / row-column pre-reserved**, each over a **from–to time-slot**. |
| OI-3 | Layout coexistence | One purpose per hall; each hall its own config. |
| OI-5 | Table capacity | **Group** meetings allowed (table capacity ≥ 2). |
| OI-7 | Freeze-lift | **Granted** ("no freeze") for this module's additive tables. |
| OI-8 | Time-slot model | **Free from–to** start/end. |

**Still using my recommendation unless you say otherwise:**

| # | Item | Default |
|---|------|---------|
| OI-4 | visitor↔visitor scope | In scope as admin-arranged; V2-01 stays the attendee-initiated feature. |
| OI-6 | Cancellation reason | Optional. |

**Resolved (owner, 2026-06-03 — "do all"):**

| # | Item | Resolution |
|---|------|-----------|
| OI-9 | **Allocation-layer scope** — meeting-only vs generalise across all three. | **Generalise across booth + session + meeting.** As-built (§14): `HallAllocation` accepts any `HallPurpose` and `Hall.Purpose` unifies the three; the shipped session seat-booking + booth code paths were **preserved** (not destructively rewritten) to keep their tested wire contracts intact — see §14. |
| OI-10 | **Group meeting type.** | **Admin-set** B2B/B2C per meeting; participants any mix of companies + visitors. |

## 14. As-built (D-248, 2026-06-03)

Built and verified as **one end-to-end commit** (owner directive "do all" +
"generalize across all three" + "one big commit"). Delivered:

- **Enums** `HallPurpose`, `HallAllocationMode`, `MeetingPartyKind`,
  `BusinessMeetingType`, `BusinessMeetingStatus`; additive `NotificationKind`
  `MeetingScheduled=43`, `MeetingCancelled=44` (persisted by name; wire contract
  append-only).
- **Schema** (additive migration `D248_AddBusinessMeetings`): `Hall.Purpose` column
  (default 0 = General — every existing hall keeps its un-specialised behaviour) +
  tables `MeetingTables`, `HallAllocations`, `BusinessMeetings`,
  `BusinessMeetingParticipants`. No shipped column altered; the **Identity** schema
  untouched.
- **Service** `BusinessMeetingService` with the table-conflict, participant-conflict,
  capacity and allocation-overlap guards; Identity round-trip for visitor names (no
  cross-DB join, D-157); audit + bilingual in-app notifications.
- **API** 13 endpoints, all gated; **permissions** `MeetingTables.*`,
  `HallAllocations.*`, `BusinessMeetings.*` (seeded `AdminOnly`).
- **CP** `/admin/meeting-tables` (purpose + tables + generate + allocations) and
  `/admin/business-meetings` (schedule + cancel + detail), nav items,
  `<AuthorizedAction>` per-action gating, EN/AR resx.
- **Tests** `tests/SIMF.Api.Tests/BusinessMeetingsTests.cs` (10 cases) + the existing
  permission-enforcement and CP-navigation guard tests stay green.
- **Docs** E2E catalogue (`cp-business-meetings.md`, `cp-meeting-tables.md`) + README +
  PAGE-INDEX + per-page reference docs.

**Generalise interpretation (OI-9).** The unification is the **`Hall.Purpose` +
`HallAllocation` layer**, which expresses booth / session / meeting allocation through
one mechanism (`CreateAllocation` accepts any `HallPurpose`, surfaced on
`AdminHallSummary`). The **shipped session seat-booking (FDS-005) and booth placement
(FDS-006) code paths were deliberately preserved** rather than destructively rewritten
onto the new tables — this honours "generalise across all three" (one umbrella
allocation concept) **without** risking the shipped, tested mobile/public wire
contracts in a single big-bang commit (the phasing trade-off the owner was advised of
twice). Migrating the legacy session/booth mechanics fully onto `HallAllocation`
remains a clean follow-up under the umbrella.

**Not built (no surface exists yet):** the Page_014 mobile dashboard meeting-union is
not yet implemented anywhere; when that aggregate read ships it must union confirmed
`BusinessMeeting`s (where the caller is a participant) with speaker `MeetingRequest`s,
as the contract already anticipates.

## 13. Definition of Done (build checklist — pending your approval)

When approved and built, the same changeset must include:

1. Domain entities + enums (§6); one consolidated **additive** migration on
   `SimfAppDbContext` (freeze lifted, OI-7).
2. New permission codes in `PermissionCatalog`, seeded `AdminOnly` (idempotent — no
   Identity change).
3. API endpoints gated; CP pages gated (`[RequirePermission]` + nav + `<AuthorizedAction>`).
4. Bilingual EN/AR resx for every new string + notification.
5. Unit + integration tests; permission guard tests green.
6. `docs/tests/e2e/cp-business-meetings.md` authored + README index + PAGE-INDEX rows
   + per-page reference doc.
7. The mobile dashboard union flipped on; shipped wire contract preserved (append-only).
8. `DECISIONS_LOG.md` entry (next free `D-NNN`) recording the build + the freeze-lift +
   the OI resolutions.

---

# §15 — v1.1 (DRAFT, D0-1): meeting-in-hall requests, hall availability & speaker double-opt-in

> **STATUS: DRAFT — the three blocking open items are owner-resolved (§15.9,
> 2026-07-08); build-ready pending the owner's final "go".** This amendment is
> additive to the built 1.0 core (§1–§14): the admin-arranged path stays exactly
> as shipped; §15 adds the *attendee-initiated request* path the owner asked for.
> It is authored the same way 1.0 was — drafted with open items for the owner to
> resolve, then built. It also governs the app-side "real available slots" fix
> (owner item 6). Cross-references SIMF-FDS-008 §5.3 (the one-to-one meeting
> request / PR approval) and SIMF-FDS-004 (Hall).
>
> **Owner resolutions (2026-07-08):** reuse the 3 built meeting types (g2b/g2vip =
> labels, OI-A); **speaker double-opt-in email with a signed single-use 72h token,
> AllowAnonymous exception approved** (OI-D/OI-I); **new `HallAvailabilityWindow`**
> (OI-B). Remaining OIs proceed on recommendation.

## 15.1 Why this amendment (owner items 6 & 7)

Owner directive (2026-07-08, verbatim intent):

> *"bi-meeting is done in hall, meeting is g2g الوفود مع بعض, or speaker with VIP
> visitor (b2b). In CP must manage this hall — control the hall, how many persons
> it can hold, and the available time. In CP there must be a clear UI/UX to manage
> hall time for meetings. For speaker/VIP, admin must review the meeting request
> from the app before approving; before approval, check the available time in the
> hall; admin can also reject. If accepted, send an email to the speaker (from
> their profile email in the system) with two buttons/links — approve or reject.
> If the speaker approves, it shows in 'اللقاءات الثنائية' (my meetings)."* Plus
> item 6: *"the app must show the speaker's REAL available slots"* (reverting the
> D-703 free client-side picker).

The 1.0 core is **admin-arranged only** and explicitly excludes an attendee request
queue (§2, "It does not cover: Party self-service requests"). The owner now wants
that queue for the *speaker/VIP* and *delegation* cases, with a **hall-availability
gate** and a **second approval by the speaker over email**. Much of this is already
built (§15.2); §15 defines only the **delta**.

## 15.2 As-is — what is already built (grounded; do not rebuild)

| Capability | Where (file:line) | State |
|-----------|-------------------|-------|
| **Speaker meeting request** (app → admin) | app submit `POST /app/speakers/{id}/meeting-requests` (`SpeakerMeetingRequestEndpoints.cs:33`); entity `SpeakerMeetingRequest.cs:17` (Subject, `SlotStart?`/`SlotEnd?`, `AvailabilityWindowId?`, Status); service `SpeakerMeetingRequestService.cs` | **Built.** Status `Pending→Accepted/Rejected/Cancelled` (`MeetingRequestStatus.cs`). |
| **Admin review + respond** | `PUT /admin/speaker-meeting-requests/{id}/respond` (`…Endpoints.cs:123`), Accept/Reject/**Accept-with-hall** (D-716) | **Built.** GAP-2 shipped (D-716): an accept may bind a free **hall slot** (+ optional table) → `AwaitingSpeaker`; the picked hall slot is the meeting time (Option A). |
| **Accept already emails the speaker** | `SpeakerMeetingRequestService.cs:421-441` (purpose `"SpeakerMeetingAccepted"`, plain HTML: topic + proposed slot), speaker email resolved via `Speaker.ContactId` | **Built** — but the email is an FYI with **no approve/reject links** (GAP-3). |
| **VIP gate on slot picking** | `SpeakerMeetingRequestService.IsVipAsync` reads `ProfileType.AllowsVipMeetingSlots` (`…:382-388`); non-VIP slot → 403 | **Built.** |
| **Delegation (g2g) request** — country↔country, الوفود مع بعض | `POST /app/delegation-meeting-requests` (`DelegationMeetingRequestEndpoints.cs:15`); entity `DelegationMeetingRequest.cs:14` (RequestingCountryId, TargetCountryId, AttendeeCount, slot); admin respond `…/respond` | **Built.** Same `MeetingRequestStatus` flow. |
| **Speaker availability → REAL derived slots** | entity `SpeakerAvailabilityWindow.cs:12` (team-defined window + `SlotMinutes`); `GET /app/speakers/{id}/available-slots` (`SpeakerAvailabilityEndpoints.cs:84`); `SpeakerAvailabilityService.GetAvailableSlotsAsync` (`…:113`) chops windows into slots, drops past + Accepted-overlapping; CP window CRUD `/admin/speaker-availability` | **Built.** The app repo method `getAvailableSlots()` (`speakers_repository.dart:39`) exists **and is tested** but the production sheet ignores it (GAP-4 / item 6). |
| **Hall config + tables + allocations** (admin-arranged) | `Hall.Purpose` (`Hall.cs:43`, `HallPurpose{General,Booth,Session,Meeting}`); `MeetingTable.cs:13` (Code, Row/Col, Capacity); `HallAllocation.cs:18` (Whole/RandomByCount/RowColumn over Start/End); endpoints `BusinessMeetingEndpoints.cs:24-249`; CP `/admin/meeting-tables` | **Built** — but there is **no dedicated "hall-time" management page**; allocations are edited through the meeting-tables tooling (GAP-1). |
| **Admin-arranged business meeting** | `BusinessMeeting.cs:12` (Confirmed on create), participants `BusinessMeetingParticipant.cs:18`; `POST /admin/business-meetings` (`…:306`); CP `/admin/business-meetings` | **Built** (1.0 core). In-app-only notifications (`SendEmail=false`). |
| **App "اللقاءات الثنائية" feed** | `RequestsScreen` (`requests_screen.dart:27`, D-500 unified feed); "طلب جديد" → `MeetingRequestSheet` (`meeting_request_sheet.dart:30`) | **Built.** The sheet currently uses a **free client-side date/time pick**, explicitly **not** tied to speaker slots (`…dart:26-29,67-96`) — this is the D-703 behaviour item 6 reverts. |
| **Outbound email** | `IEmailSender.SendAsync` (`IEmailSender.cs:4`); MailKit `SmtpEmailSender.cs:11`; async `IEmailQueue`→`EmailBackgroundService.cs:22`; `EmailMessage(To,Subject,HtmlBody)` (`EmailMessage.cs:4`) | **Built** — but `EmailMessage` is **HTML-body-only**; **no token / action-link / one-click-callback pattern exists anywhere** in the backend (GAP-3). |
| **Meeting NotificationKinds** | `MeetingScheduled=43`, `MeetingCancelled=44` (`NotificationKind.cs:109/114`); reused for speaker/delegation accept/decline. **No** "request received" kind (submission is audit-only). | **Built** (append-only frozen enum). |

## 15.3 Meeting-type taxonomy (g2g / g2b / g2vip) — mapping + OPEN ITEM

The owner's shorthand maps onto the built entities as follows (**to be confirmed —
OI-A**):

| Owner term | Meaning (owner) | Built entity today |
|-----------|-----------------|--------------------|
| **g2g** | group↔group — delegation ↔ delegation (الوفود مع بعض) | `DelegationMeetingRequest` (country↔country + AttendeeCount) |
| **speaker / VIP (b2b)** | a VIP visitor ↔ a speaker | `SpeakerMeetingRequest` (VIP-gated slot) |
| **g2b** | *ambiguous* — group↔booth/business? | *no exact entity; possibly `BusinessMeeting` B2B (company↔company)* |
| **g2vip** | *ambiguous* — group↔VIP? | *no exact entity* |

`g2b`/`g2vip` do not map cleanly to a built entity. **OI-A (owner):** confirm the
exact taxonomy — do we (a) keep the three built types (Speaker / Delegation /
admin-arranged BusinessMeeting) and treat g2b/g2vip as *labels* on a BusinessMeeting,
or (b) introduce new request types? **Recommendation:** (a) — reuse the built types;
add a display label only. No new entity unless the owner names a distinct workflow.

## 15.4 To-be — the deltas (the only new work)

- **GAP-1 — CP hall-time management UI.** A clear CP surface, per **Meeting** hall,
  to set **capacity** ("how many persons") and the **available meeting time-slots**
  (from–to windows), so a reviewer can see and pick a free hall slot. Backing exists
  (`HallAllocation` for reserved slots, `SpeakerAvailabilityWindow` for speaker
  windows, `MeetingTable.Capacity`) — the gap is a **dedicated, clear page**
  (owner: "clear UI/UX to manage hall time for meeting"). **OI-B:** is "available
  time" a property of the **hall** (a new hall-availability window, parallel to
  `SpeakerAvailabilityWindow`) or expressed purely through existing `HallAllocation`
  rows? **Recommendation:** add a `HallAvailabilityWindow` (mirrors
  `SpeakerAvailabilityWindow`) so a hall's bookable meeting times are first-class and
  the reviewer picks from real free slots — symmetric with the speaker side.
- **GAP-2 — admin review checks hall availability + binds a slot on accept. ✅ BUILT
  (D-716, Slice B).** When the admin accepts a speaker request, the review modal shows
  the hall's free meeting slots (GAP-1) and the admin **assigns a hall + optional table
  + slot**; the request carries that binding and moves to `AwaitingSpeaker` pending the
  speaker's confirmation (Slice C). **OI-C resolved:** the binding is **stamped on the
  request** (`HallId`/`MeetingTableId`/`SlotStart`/`SlotEnd`), not materialised as
  a `BusinessMeeting` — smaller and reversible for v1.1 (converge later). **Option A**
  chosen: the picked **hall** slot is the meeting time of record (one time grid). The
  hall double-booking backstop is a filtered-unique `(HallId, SlotStart)` index over
  the live states. *(Delegation requests are not hall-bound in Slice B — speaker only.)*
- **GAP-3 — speaker double-opt-in email (approve/reject links). ✅ BUILT (D-717,
  Slice C).** After accept-with-hall the request is **AwaitingSpeaker** and the
  speaker gets an HTML email with **Approve / Reject** links. Clicking is the final
  gate: Approve → `Accepted` + the requester gets `MeetingRequestConfirmed`; Reject →
  `Rejected` + `MeetingCancelled`. Built as: (a) a **persisted single-use, 72h,
  action-bound `MeetingActionToken`** (two per request, keyed-HMAC hash at rest —
  **OI-G resolved: persisted, not stateless**); (b) two **`AllowAnonymous`** endpoints
  `GET/POST /api/v1/app/meeting-actions/{token}` (GET previews without consuming, POST
  applies — §15.7), unusable token → neutral 404; (c) the HTML links email +
  (d) a public **Website landing page** `/meeting/confirm`. **OI-D resolved: yes** —
  admin-accept is not final; the speaker must approve. **OI-I resolved:** 72h TTL, new
  AllowAnonymous exception owner-approved. **On reject the admin is NOT separately
  notified** (owner decision C) — the CP row flipping to `Rejected` is the admin
  signal.
- **GAP-4 (owner item 6) — app shows REAL available slots.** Rewire
  `MeetingRequestSheet` to call the built `getAvailableSlots()`
  (`GET /app/speakers/{id}/available-slots`) and present the **real** derived slots
  instead of the D-703 free 7-day/hourly picker; submit the chosen real slot. When a
  speaker has **no** availability windows, show a clear empty state ("no slots — the
  team will schedule"), not a fabricated grid. This is an **app-only** change
  (backend + repo method already exist) and reverts D-703. Delegation requests keep
  their own slot model (they target a hall, not a speaker) — **OI-E:** confirm
  delegation requests pick from **hall** slots (GAP-1) the same way.

## 15.5 Proposed workflow (speaker / VIP path)

```mermaid
stateDiagram-v2
    [*] --> Pending : visitor (VIP) submits a request for a speaker + a REAL slot (§15.4 GAP-4)
    Pending --> Rejected : admin rejects (stage 1)
    Pending --> AwaitingSpeaker : admin accepts + assigns a free hall/table/slot (§15.4 GAP-1/2)
    AwaitingSpeaker --> Confirmed : speaker clicks APPROVE in the email (§15.4 GAP-3)
    AwaitingSpeaker --> Rejected : speaker clicks REJECT in the email
    AwaitingSpeaker --> Rejected : token expires / admin cancels
    Confirmed --> [*] : shows in اللقاءات الثنائية for the requester
```

The delegation (g2g) path is the same minus the speaker email — the head-of-delegation
confirmation channel is **OI-F** (email the target delegation head? admin-only
confirm?). **Recommendation:** admin-confirm for g2g in v1.1 (no external delegation
email until an owner decision), with the email double-opt-in reserved for the speaker
path where the owner explicitly asked for it.

## 15.6 Data deltas (additive; freeze already lifted for this module, OI-7)

| Change | Detail |
|--------|--------|
| `MeetingRequestStatus` (extend) | **append** `AwaitingSpeaker` (new integer, no reorder/rename — enum is append-only per the freeze). Applies to `SpeakerMeetingRequest`; delegation reuse per OI-F. |
| `SpeakerMeetingRequest` (extend) | additive nullable binding fields — `HallId?`, `MeetingTableId?`, assigned `SlotStart/End` confirmed at accept, `SpeakerDecisionAt?`. |
| Speaker action token | a **signed, single-use, expiring** token for the two email links. **OI-G:** persisted token entity (revocable, one-shot) **vs** a stateless HMAC/JWT with a short TTL. **Recommendation:** a small persisted `MeetingActionToken` (single-use, revocable, audited) — safer than a stateless link that can be replayed until expiry. |
| `HallAvailabilityWindow` (new, if OI-B=yes) | mirrors `SpeakerAvailabilityWindow` — `HallId`, Start/End, `SlotMinutes`, soft-delete. |
| `EmailMessage`/email path | render the two action links into the HTML body (the field is already HTML). No new email transport — reuses `IEmailQueue`. |
| Notifications | reuse `MeetingScheduled`/`MeetingCancelled`; **OI-H:** add a `MeetingAwaitingSpeaker`/`MeetingConfirmedBySpeaker` kind (append-only) for the requester, or reuse existing. **Recommendation:** add `MeetingRequestConfirmed` (append-only) so the requester's "your meeting is confirmed" is distinct from the admin-arranged `MeetingScheduled`. |

## 15.7 Security — the email action links (HARD)

- The two links hit **`AllowAnonymous`** endpoints (the speaker is not signed in).
  This is a **new AllowAnonymous exception** beyond the global §4 baseline
  (SignIn/SignUp/ForgotPassword) and **must be owner-approved** — it is justified only
  because a speaker approving their own meeting over email cannot be authenticated.
- Token rules: **single-use**, **short TTL** (e.g. 72h — OI-I), **high-entropy**,
  **bound to the specific request + action**, **revoked** once used or once the admin
  cancels, and **rate-limited**. No PII in the URL. The landing is a minimal
  **public confirmation page** (website) showing the outcome — no data entry.
- Every token mint / click / outcome writes `OperationLog`. Reusing an expired/used
  token shows a neutral "this link is no longer valid" page, never an error leaking
  state.
- The admin API (accept/reject/assign) stays **permission-gated** exactly as the 1.0
  endpoints (`PermissionCatalog` policy + `RequireApprovedAccount`).

## 15.8 Surfaces

| Surface | Screens / changes |
|---------|-------------------|
| Control Panel | **GAP-1** hall-time management page (capacity + available meeting slots, per Meeting hall); the speaker-meeting-request **review** modal gains a hall/table/free-slot picker (GAP-2); nav + `[RequirePermission]` + `<AuthorizedAction>` per the CP hard rule. |
| Mobile app | **GAP-4** `MeetingRequestSheet` uses real speaker slots (+ empty state); the اللقاءات الثنائية feed shows the new `AwaitingSpeaker`/`Confirmed` states. No wire-contract break (append-only fields). |
| Email (speaker) | HTML email with **Approve** / **Reject** links (GAP-3). |
| Public website | a minimal **token landing page** (approve/reject outcome) — new. |

## 15.9 Open items

**Resolved (owner, 2026-07-08):**

| # | Item | Resolution |
|---|------|-----------|
| **OI-A** | g2g/g2b/g2vip taxonomy | **Reuse the 3 built types** (Speaker / Delegation(g2g) / admin-arranged BusinessMeeting); g2b/g2vip are **display labels** on a BusinessMeeting — no new request workflow. |
| **OI-B** | Hall "available time" model | **Add `HallAvailabilityWindow`** (symmetric with `SpeakerAvailabilityWindow`); the reviewer picks a real free hall slot. |
| **OI-D** | Speaker double-opt-in | **Yes** — admin-accept is **not** final; the speaker must also approve (over the email links). |
| **OI-I** | Email-link TTL + `AllowAnonymous` exception | **72h, single-use token; the new `AllowAnonymous` exception is APPROVED** (§4) — justified because the speaker cannot be authenticated over an email link. |

**Proceeding on the documented recommendation (owner did not flag; may still override):**

| # | Item | Default taken |
|---|------|---------------|
| **OI-C** | On accept — materialise a `BusinessMeeting` vs stamp the binding on the request | Stamp hall/table/slot on the request for v1.1; converge onto `BusinessMeeting` later. |
| **OI-E** | Delegation requests pick from **hall** slots too | Yes — from `HallAvailabilityWindow` (OI-B). |
| **OI-F** | g2g confirmation channel | Admin-confirm in v1.1 (no external delegation-head email yet). |
| **OI-G** | Action token — persisted vs stateless | Persisted single-use, revocable, audited `MeetingActionToken`. |
| **OI-H** | Requester notification kind | Add `MeetingRequestConfirmed` (append-only). |

## 15.10 Definition of Done (when approved & built — same changeset)

1. Domain: extend `MeetingRequestStatus` (+`AwaitingSpeaker`) + additive fields /
   `MeetingActionToken` / `HallAvailabilityWindow` (per the resolved OIs); **one
   additive migration** on `SimfAppDbContext` (Identity untouched; enum append-only).
2. New `PermissionCatalog` codes for the hall-time page + review-assign action, seeded
   `AdminOnly`; gates on **both** API and CP; guard tests green.
3. The `AllowAnonymous` token endpoints (owner-approved exception) — single-use,
   TTL, rate-limited, audited.
4. App: `MeetingRequestSheet` on real slots (item 6) + empty state; اللقاءات الثنائية
   shows the new states; wire contract append-only.
5. Bilingual EN/AR for every new string, email, and the website landing page.
6. Unit + integration tests (the state machine, the token single-use/expiry, the
   hall-availability conflict, the app slot rewire) + permission guard tests.
7. E2E catalogue: update `cp-business-meetings.md` + author `cp-hall-time.md` +
   `mobile-meeting-request.md` (real slots) + a website token-landing E2E; README +
   PAGE-INDEX rows + per-page reference docs.
8. `DECISIONS_LOG.md` entries; this §15 flipped from DRAFT to built with an As-built
   note; SIMF-FDS-008 §5.3 cross-referenced.
