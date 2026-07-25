# Announcements (broadcast desk) — `/admin/announcements`

| | |
|--|--|
| **Route** | `/admin/announcements` (`Announcements.razor`) |
| **Audience** | Administrator ("Public relations" nav group) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Announcements.Send)]` (page); API endpoints gated `Announcements.Send` (create + estimate) / `Announcements.View` (history list + detail), all `+ RequireApprovedAccount`; the create endpoint is additionally rate-limited on the `auth` limiter |
| **Pattern** | D-132 compose-and-send desk (NOT a CRUD grid) — a compose form + live recipient estimate + a read-only history grid (`SimfDataGrid`, server-paged, newest-first). Delivery is background-processed by a hosted worker. **Modelled on** `AdminInvitationService.NotifyVipsAsync`, made durable + paced. |
| **Status** | ✅ Real (D-132) |
| **Backend endpoints** | BFF passthroughs `/account/api/admin/notifications/...` → API: `POST /admin/notifications/broadcast`, `POST /admin/notifications/broadcast/estimate`, `POST /admin/notifications/broadcasts/list`, `GET /admin/notifications/broadcasts/{id}`. |
| **Backed by** | `dbo.NotificationBroadcasts` table (additive, migration `AddNotificationBroadcast`) + the `NotificationBroadcastWorker` hosted worker. |
| **Tests** | [`docs/tests/e2e/cp-announcements.md`](../../tests/e2e/cp-announcements.md) (`E2E-ANN-001..015`) · API `tests/SIMF.Api.Tests/NotificationBroadcastTests.cs` |
| **Last reviewed** | 2026-07-25 |

## 1. Purpose

The admin broadcast-Notifications desk (D-132). An administrator composes one
bilingual (EN + AR) message and **sends it as BOTH an in-app notification AND an
email** to one of two kinds of target:

- **A specific session** — everyone with an active seat reservation in that
  session.
- **A broad audience** — one of three scopes: **All approved app users**, **Event
  attendees (booked a seat)**, or **Everyone (including pending)**.

Sending inserts a single `Pending` `NotificationBroadcast` job; the
`NotificationBroadcastWorker` hosted worker then fans it out — one in-app
notification of kind `AdminAnnouncement` per recipient, plus a bilingual email to
each recipient who has an email on file. Because delivery is asynchronous the page
shows a **history grid** with each past broadcast's status and counters. A **live
recipient-count** line under the compose form ("This will reach N recipient(s).")
is powered by a separate estimate endpoint so the admin sees the reach before
sending.

This is a **compose-and-send desk, not a CRUD page** — there is no Add / Edit /
Delete grid action. A broadcast is created from the form and, once queued, is
immutable; the worker is the only writer after submit. The golden path is
**compose → Send broadcast → success toast + a new "Queued" history row**.

It sits in the **"Public relations"** nav group alongside
[`admin-invitations.md`](admin-invitations.md) (whose "bulk-notify" it generalises)
and [`admin-vips.md`](admin-vips.md).

## 4. UI

- `SimfBanner` ("Announcements") + two `simf-surface` cards: **Compose a broadcast**
  and **Recent broadcasts**.
- A page-level `SimfAlert` shows the success / failure / load-failed toast; a second
  `SimfAlert` (variant `info`) shows the live recipient-count line.
- **Compose form** (`SimfSelect` / `SimfTextField` / `SimfTextarea`), all disabled
  while a send is in flight (`_busy`):
  - **Send to** (`Target`) — "A specific session" / "A broad audience".
  - **Session** (shown when Target = session) — a `SimfSelect` over the admin
    session list; option label = `Code · Title` (Arabic title under an Arabic UI).
  - **Audience** (shown when Target = audience) — "All approved app users" /
    "Event attendees (booked a seat)" / "Everyone (including pending)".
  - **Importance** (`Severity`) — Info / Success / Warning / **Critical**. (The
    fourth option displays "Critical"; its wire value is `Severity = "Error"`, i.e.
    `NotificationSeverity.Error`.)
  - **Title (English)**, **Title (Arabic)** — required, ≤ 200.
  - **Message (English)**, **Message (Arabic)** — required, ≤ 2000, `MaxLength`-capped
    textareas (4 rows).
  - **Recipient line** — "This will reach N recipient(s)." once a target is chosen,
    else "Choose a target to see how many recipients it reaches."
  - **Send broadcast** button, wrapped in
    `<AuthorizedAction Permission="Announcements.Send">`, shows a loading state
    while sending.
- **History grid** — a single read-only `SimfDataGrid` (server-paged, newest-first,
  20/page) with the six columns in §4.5; empty state via `SimfEmptyState` ("No
  broadcasts sent yet."). No per-row action, no add toolbar button.

### 4.5 History grid columns

| Column | Source | Notes |
|--------|--------|-------|
| When | `CreatedAt` | Rendered in the operator's local time (`g` format) |
| Target | `SessionTitle` or `AudienceScope` | Session broadcast → the session title; audience broadcast → the audience label |
| Message | `Title` / `TitleArabic` | The English title (Arabic title under an Arabic UI) |
| Status | `Status` | Pill/label: Pending → **Queued**, Processing → **Sending**, Completed → **Sent**, Failed → **Failed** |
| Recipients | `TotalRecipients` | The worker's resolved distinct-recipient count (0 until the worker runs) |
| Emails | `EmailsEnqueued` | How many recipients had an email and got one enqueued |

**Actions on this page (no create/edit/delete grid actions):**

- **Send broadcast** — `POST /admin/notifications/broadcast`. On 200: clears the
  four message fields (keeps the target for a quick follow-up), reloads the history,
  and shows the success toast "Broadcast queued: delivering to {N} recipient(s)."
- The composer also fires **estimate** (`POST /admin/notifications/broadcast/estimate`)
  on load and on every Target / Session / Audience change to keep the recipient
  line live. Estimate never sends.

## 5. Data flow + endpoints

1. **Sessions for the picker** — on init the page posts a `GridQuery` to
   `/account/api/admin/sessions/list` (top 200) to populate the Session select.
2. **Estimate** — `POST /account/api/admin/notifications/broadcast/estimate`
   (`{ TargetMode, SessionId?, AudienceScope? }`) → `{ EstimatedRecipients }`.
   **Fail-soft:** an unpicked session or an unrecognised scope estimates as `0`
   rather than erroring.
3. **Create/queue** — Send fires `POST /account/api/admin/notifications/broadcast`
   (`AdminCreateBroadcastRequest`) → `{ Id, EstimatedRecipients }`. The server
   inserts one `Pending` `NotificationBroadcast` and writes a `BroadcastQueued`
   audit entry.
4. **History** — the grid posts `GridQuery` to
   `/account/api/admin/notifications/broadcasts/list` →
   `GridPage<AdminBroadcastSummary>` (newest-first). A `GET .../broadcasts/{id}`
   detail endpoint exists for a single broadcast's status but the current grid has
   no per-row detail modal.
5. **Fan-out (worker)** — `NotificationBroadcastWorker` claims the next `Pending`
   row (claim-first → at-most-once), resolves recipients, dispatches an
   `AdminAnnouncement` in-app notification + a bilingual email each, records the
   counters, moves the row `Processing → Completed` (or `Failed` with the captured
   error), and writes a `BroadcastSent` audit entry.

The actor id is resolved from the access-token `sub` claim on create for the audit
entries. **D-157:** session recipients come from the App DB (`SeatReservations`), a
broad audience from the Identity DB (`Users`), and emails via
`IIdentityUserDirectory` — no cross-DB JOIN, no cross-DB transaction, no recipient
data copied across the boundary.

### Recipient rules (resolved at send time)

- **Session** → distinct `SeatReservations.ReservedForUserId` for that session where
  `ReleasedAt` is null and `ReservedForUserId` is set.
- **ApprovedAppUsers** → non-Admin `Users` with `AccountState == Approved`.
- **EventAttendees** → distinct users with any active seat reservation.
- **EveryoneIncludingPending** → all non-Admin `Users` regardless of state.

## 6. Validation + error handling

- **Blank / oversize fields.** FluentValidation (`AdminCreateBroadcastValidator`)
  requires all four of Title EN/AR (≤ 200) and Message EN/AR (≤ 2000) and rejects a
  bad target mode → **400** with a bilingual message. The two Message textareas are
  `MaxLength`-capped at 2000 in the UI, so an over-length body is only reachable via
  a scripted client; the service re-validates the `1..200` / `1..2000` bounds and
  throws `BROADCAST_INVALID` (400) as defence-in-depth.
- **Session-missing.** A session broadcast with no `SessionId` → **400
  `BROADCAST_INVALID`** ("Select a session for a session broadcast."). The compose
  form only enables Send with a session chosen, so this is reachable via a scripted
  client / race.
- **Unknown session.** A session broadcast for an unknown `SessionId` → **404
  `SESSION_NOT_FOUND`**.
- **Invalid audience scope.** An audience broadcast with an unrecognised scope →
  **400 `BROADCAST_INVALID`** ("Choose a valid audience."). Note the **estimate**
  endpoint is fail-soft and returns `0` for the same bad scope rather than erroring.
- **Unknown broadcast (detail).** `GET .../broadcasts/{id}` for an unknown id → **404
  `BROADCAST_NOT_FOUND`**.
- **Send failure / 500.** A failed create shows the red toast "The broadcast could
  not be sent." / "تعذّر إرسال الإعلان."; the history grid is unchanged.
- **Load failure / 500 on `/list`.** A red toast "The broadcasts could not be
  loaded." / "تعذّر تحميل الإعلانات."; no rows render and no empty-state shows (the
  load failed rather than returning an empty page).
- **Worker failure.** A fan-out exception moves the row to `Failed` with the captured
  error (≤ 1024 chars) and a `BroadcastSent` audit entry with a `Failure` outcome;
  the claim-first guard means a `Processing` row is never re-picked (at-most-once).

## 7. Edge cases + known limitations

- **Asynchronous delivery.** The success toast is "queued", not "sent" — a fresh row
  starts `Queued` (Recipients / Emails = 0) and only reflects real counts once the
  worker runs. The submit-time `EstimatedRecipients` in the toast is an estimate; the
  worker stamps the authoritative `TotalRecipients`.
- **At-most-once, not exactly-once.** A restart mid-send leaves the row `Processing`
  and it is never retried; individual dispatch failures increment `Skipped` and are
  logged, they don't fail the whole broadcast.
- **Estimate is fail-soft.** An unpicked session or a bad audience scope estimates as
  `0`; only the create path enforces the target/session/audience validity.
- **No recipient PII in the grid.** The history summary carries only the composer's
  display name + the target session title; it never carries the recipient list or
  recipient emails.
- **Severity label vs value.** The fourth Importance option renders "Critical" but
  travels as `Severity = "Error"` (`NotificationSeverity.Error`); an unparseable
  value falls back to `Info`.

## 8. i18n + RTL

`Admin.Announcements.*` keys (banner title, compose labels, target / audience /
importance options, the recipient line, the Send button, the six grid column
headers, the four status labels, and the loading / empty / toast copy) with full
EN ↔ AR parity. RTL mirrors the compose form and the history grid (headers, cells,
status labels). Under an Arabic UI the Session picker and the Message column show the
Arabic title, and the status labels read قيد الانتظار / قيد الإرسال / تم الإرسال / فشل.
Validation + error messages are bilingual server-side (`.Bilingual(...)` /
`ApiException`).

## 10. Use cases

- Broadcast a message to a session's registered attendees (in-app + email).
- Broadcast a message to a broad audience (all approved app users / event attendees /
  everyone including pending).
- Preview the reach of a target before sending (the live recipient estimate).
- Review the history of past broadcasts and their delivery status + counters.

(No create/edit/delete-of-history use case — a queued broadcast is immutable and the
grid is read-only.)

## 11. E2E

See [`docs/tests/e2e/cp-announcements.md`](../../tests/e2e/cp-announcements.md):
E2E-ANN-001 golden session send, 002–004 the three audience scopes, 005 live
estimate, 006 grid columns + status mapping, 007 empty state, 008 permission gate
(non-admin → `/not-permitted`; View-only reads but can't send; POST 403), 009 blank
title/message, 010 oversize title/message, 011 session-missing → `BROADCAST_INVALID`,
012 unknown session → `SESSION_NOT_FOUND`, 013 invalid audience → `BROADCAST_INVALID`,
014 server-500 send / load, 015 RTL.

## 12. Related docs

- Same "Public relations" nav group / lineage:
  [`admin-invitations.md`](admin-invitations.md) (bulk-notify),
  [`admin-vips.md`](admin-vips.md).
- App side (recipients see the in-app row): the notifications inbox
  [`docs/pages/mobile/notifications/`](../mobile/notifications/README.md) — kind
  `AdminAnnouncement`.
- Permissions: `PermissionCatalog.Announcements.{Send, View}` (baselined `AdminOnly`);
  guide `docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`
  (catalogue `docs/SIMF-Permission-Catalogue.md`).
- Decisions: D-132 (admin broadcast-Notifications module).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-07-25 | D-132 | Original — `NotificationBroadcast` entity + additive `NotificationBroadcasts` table (migration `AddNotificationBroadcast`) + `NotificationBroadcastWorker` hosted worker + CP Announcements desk (compose form + live estimate + history grid) + four admin endpoints. Notification kind `AdminAnnouncement`. Permissions `Announcements.{Send, View}`. |

_Last reviewed:_ 2026-07-25 by SIMF Team (D-132 — reference doc authored).
