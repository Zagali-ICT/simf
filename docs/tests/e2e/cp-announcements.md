# E2E test catalogue — Announcements (broadcast) desk (`/admin/announcements`)

| | |
|--|--|
| **Page** | [`cp/announcements.md`](../../pages/cp/announcements.md) |
| **Route** | `/admin/announcements` (`Announcements.razor`) |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@simrsnf.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-07-25 (D-132) |

> **Decision:** D-132 — the admin broadcast-Notifications module. The Announcements
> desk lets an administrator compose one bilingual message and **send it as BOTH an
> in-app notification AND an email** to either **a specific session's registered
> attendees** or **a broad audience**. The server inserts a single `Pending`
> `NotificationBroadcast` job; the `NotificationBroadcastWorker` hosted worker fans
> it out (one in-app notification of kind `AdminAnnouncement` per recipient + a
> bilingual email to every recipient who has an email on file). The compose form
> carries a **live recipient-count** ("This will reach N recipient(s).") powered by
> a separate estimate endpoint, and a history grid shows every past broadcast with
> its delivery status. Backed by the **NEW additive `NotificationBroadcasts` table**
> (migration `AddNotificationBroadcast`).

> **Page permission:** `@attribute [RequirePermission(PermissionCatalog.Announcements.Send)]`
> (`"Announcements.Send"`). The **create** + **estimate** API endpoints enforce
> `Announcements.Send`; the **history list** + **detail** endpoints enforce
> `Announcements.View`. Both codes are baselined `AdminOnly` (Administrator via the
> `"*"` wildcard) and seeded idempotently — no migration for the permission rows.
> All four endpoints also require `RequireApprovedAccount`; the create endpoint is
> additionally rate-limited on the `auth` limiter. The **Send broadcast** button is
> wrapped in `<AuthorizedAction Permission="Announcements.Send">`. The nav item
> (`Module.Announcements`, "Public relations" group) carries
> `RequiredPermission = Announcements.Send`.

> **Compose + history, no CRUD.** This page has no Add/Edit/Delete grid actions. A
> broadcast is **created** from the compose form and, once queued, is immutable —
> the history grid is read-only and the worker is the only writer after submit. The
> golden path is **compose → Send broadcast → success toast + a new "Queued" history
> row**. Delivery is asynchronous, so the row starts `Queued` (Pending) and the
> worker moves it `Sending` (Processing) → `Sent` (Completed) or `Failed`.

> **D-157 boundary + PII.** Recipients are resolved at send time and never copied
> across the two-DB boundary: a session's active seat-holders come from the App DB
> (`SeatReservations`), a broad audience from the Identity DB (`Users`), and their
> emails via `IIdentityUserDirectory` — no cross-DB JOIN, no cross-DB transaction.
> The history grid projects only the composer's display name + the target session
> title; it carries **no recipient list and no recipient emails**.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-ANN-001 | Golden path — session-scoped broadcast → success toast + a new "Queued" history row | happy | P0 | _to author_ |
| E2E-ANN-002 | Audience broadcast — "All approved app users" (ApprovedAppUsers) | happy | P0 | _to author_ |
| E2E-ANN-003 | Audience broadcast — "Event attendees (booked a seat)" (EventAttendees) | happy | P1 | _to author_ |
| E2E-ANN-004 | Audience broadcast — "Everyone (including pending)" (EveryoneIncludingPending) | happy | P1 | _to author_ |
| E2E-ANN-005 | Live recipient-count estimate updates as the target changes | happy | P1 | _to author_ |
| E2E-ANN-006 | History grid columns + status mapping (Queued / Sending / Sent / Failed) | happy | P1 | _to author_ |
| E2E-ANN-007 | Empty history state renders `SimfEmptyState` ("No broadcasts sent yet.") | empty | P1 | _to author_ |
| E2E-ANN-008 | Auth gate — non-admin → `/not-permitted`; a `View`-only role reads history but cannot send | auth | P0 | _to author_ |
| E2E-ANN-009 | Validation — blank title/message (EN or AR) → 400 | error | P1 | _to author_ |
| E2E-ANN-010 | Validation — oversize title (> 200) / message (> 2000) → 400 | error | P1 | _to author_ |
| E2E-ANN-011 | Session mode with no session selected → 400 `BROADCAST_INVALID` | error | P1 | _to author_ |
| E2E-ANN-012 | Unknown session id → 404 `SESSION_NOT_FOUND` | error | P1 | _to author_ |
| E2E-ANN-013 | Invalid audience scope → 400 `BROADCAST_INVALID` | error | P1 | _to author_ |
| E2E-ANN-014 | Server 500 on send / on history load → red fallback toast | resilience | P2 | _to author_ |
| E2E-ANN-015 | RTL / Arabic render (compose form + history grid) | i18n | P1 | _to author_ |
| E2E-ANN-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-ANN-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-ANN-001 — Golden path (session broadcast → queued)

```gherkin
Feature: Announcements desk — session-scoped broadcast happy path
  As an Administrator with Announcements.Send
  I want to broadcast a bilingual message to a session's registered attendees
  So that everyone holding a seat gets it in-app and by email

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp using the Get-Totp helper
  And a session "S-101 · Opening Plenary" has at least 3 active seat reservations
      (SeatReservations where ReleasedAt is null and ReservedForUserId is set)
  And they have landed on /admin/announcements

Scenario: Compose and send a session broadcast
  Given the "Send to" select defaults to "A specific session"
  And the "Session" select is shown (the audience select is hidden)
  When the administrator picks "S-101 · Opening Plenary" from the Session select
  Then POST /account/api/admin/notifications/broadcast/estimate fires with TargetMode=Session and the session id
  And the info line reads "This will reach 3 recipient(s)."

  When the administrator leaves "Importance" on "Info"
  And types "Hall change" into Title (English)
  And types "تغيير القاعة" into Title (Arabic)
  And types "The opening plenary has moved to Hall B." into Message (English)
  And types "انتقلت الجلسة الافتتاحية إلى القاعة ب." into Message (Arabic)
  And clicks "Send broadcast"
  Then POST /account/api/admin/notifications/broadcast fires with
       { TargetMode: "Session", SessionId: <id>, AudienceScope: null,
         Title, TitleArabic, Body, BodyArabic, Severity: "Info" }
  And the API returns HTTP 200 with ApiResult.Data = { Id, EstimatedRecipients: 3 }
  And a green success toast reads
      "Broadcast queued: delivering to 3 recipient(s)." /
      "تم إدراج الإعلان: يُرسل إلى 3 مستلماً."
  And the four message fields (Title EN/AR, Message EN/AR) clear, while the target stays selected
  And the history grid reloads
  And the newest row shows When = now, Target = "S-101 · Opening Plenary" (the session title),
      Message = "Hall change", Status = "Queued", Recipients = 0, Emails = 0

Scenario: The worker delivers the queued broadcast
  Given the broadcast row is "Queued" (Pending)
  When the NotificationBroadcastWorker processes the next pending job
  Then the row moves to "Sending" (Processing) then "Sent" (Completed)
  And each of the 3 seat-holders has one AdminAnnouncement in-app notification
  And every seat-holder with an email on file has a bilingual email enqueued
  And the row's Recipients = 3 and Emails = (count with an email)
```

**Evidence captured:**
- Screenshot after: `docs/screenshots/cp-announcements-001-after.png`
- Console errors: 0 expected · Network failures: 0 expected
- `OperationLog` / audit: a `BroadcastQueued` entry on submit and a `BroadcastSent`
  entry once the worker completes, both with the actor id.
- API tests: `SIMF.Api.Tests/NotificationBroadcastTests.cs` (create → Pending job +
  estimate; worker fan-out → in-app + email counts).

### E2E-ANN-002 — Audience broadcast: All approved app users

```gherkin
Scenario: Broadcast to every approved app user
  Given the administrator is on /admin/announcements
  When they change "Send to" to "A broad audience"
  Then the Session select is replaced by the "Audience" select
  And the Audience select defaults to "All approved app users"
  And POST .../broadcast/estimate fires with TargetMode=Audience and AudienceScope=ApprovedAppUsers
  And the info line reads "This will reach N recipient(s)."
      # N = distinct non-Admin users whose AccountState == Approved
  When they fill Title EN/AR + Message EN/AR, set Importance = "Warning", and click "Send broadcast"
  Then POST .../broadcast fires with TargetMode="Audience", AudienceScope="ApprovedAppUsers", Severity="Warning"
  And the API returns HTTP 200
  And the success toast reads "Broadcast queued: delivering to N recipient(s)."
  And the newest history row shows Target = "All approved app users"
```

### E2E-ANN-003 — Audience broadcast: Event attendees (booked a seat)

```gherkin
Scenario: Broadcast to everyone who booked a seat
  Given the administrator is on /admin/announcements with "A broad audience" selected
  When they change the Audience select to "Event attendees (booked a seat)"
  Then POST .../broadcast/estimate fires with AudienceScope=EventAttendees
  And the info line shows the distinct count of users with any active seat reservation
      (SeatReservations where ReleasedAt is null and ReservedForUserId is set, distinct)
  When they compose a valid bilingual message and click "Send broadcast"
  Then POST .../broadcast fires with AudienceScope="EventAttendees"
  And the API returns HTTP 200 and the success toast shows that same count
  And the newest history row shows Target = "Event attendees (booked a seat)"
```

### E2E-ANN-004 — Audience broadcast: Everyone (including pending)

```gherkin
Scenario: Broadcast to all non-admin users regardless of state
  Given the administrator is on /admin/announcements with "A broad audience" selected
  When they change the Audience select to "Everyone (including pending)"
  Then POST .../broadcast/estimate fires with AudienceScope=EveryoneIncludingPending
  And the info line shows the count of ALL non-Admin users (Approved AND Pending AND Rejected)
  When they compose a valid bilingual message and click "Send broadcast"
  Then POST .../broadcast fires with AudienceScope="EveryoneIncludingPending"
  And the API returns HTTP 200
  And the newest history row shows Target = "Everyone (including pending)"
```

### E2E-ANN-005 — Live recipient-count estimate

```gherkin
Scenario: The recipient line tracks the chosen target without sending
  Given the administrator has just opened /admin/announcements
  And "Send to" defaults to "A specific session" with no session chosen
  Then the recipient line reads "This will reach 0 recipient(s)."
      # An unpicked session estimates as 0 (fail-soft), it does not error
  When the administrator picks a session with 5 active reservations
  Then POST .../broadcast/estimate fires and the line updates to "This will reach 5 recipient(s)."
  When they switch "Send to" to "A broad audience"
  Then a fresh estimate fires for AudienceScope=ApprovedAppUsers and the line updates to that count
  When they switch the Audience select between the three options
  Then the estimate refetches on each change and the line reflects each scope's count
  # No POST /broadcast is ever sent during this scenario — estimate only.
```

### E2E-ANN-006 — History grid columns + status mapping

```gherkin
Scenario: The history grid shows the six columns and maps the backing statuses
  Given several past broadcasts exist across every backing status
  When the administrator opens /admin/announcements
  Then POST /account/api/admin/notifications/broadcasts/list fires (newest first, server-paged, 20/page)
  And the grid shows the columns: When, Target, Message, Status, Recipients, Emails
  And the Status cell maps the backing enum to the display label:
      Pending -> "Queued", Processing -> "Sending", Completed -> "Sent", Failed -> "Failed"
  And the Target cell reads the session title for a session broadcast, or the audience
      label ("All approved app users" / "Event attendees (booked a seat)" /
      "Everyone (including pending)") for an audience broadcast
  And the Message cell reads the English title (Arabic title under an Arabic UI)
  And the Recipients / Emails cells read the worker's final TotalRecipients / EmailsEnqueued counters
  When the administrator pages with Next / Prev
  Then a fresh /broadcasts/list request fires with the updated Skip and the page summary updates
```

### E2E-ANN-007 — Empty history state

```gherkin
Scenario: Empty history renders SimfEmptyState
  Given the database has no NotificationBroadcast rows
  When the administrator opens /admin/announcements
  Then POST /account/api/admin/notifications/broadcasts/list returns an empty page
  And the grid body renders the SimfEmptyState via the grid's EmptyTemplate
  And it shows the bilingual copy "No broadcasts sent yet." / "لم يُرسل أي إعلان بعد."
  And no error toast appears
  And the compose form above the grid is still fully usable
```

### E2E-ANN-008 — Auth gate (non-admin can't open; View-only can't send)

```gherkin
Scenario: A role without Announcements.Send cannot open the page
  Given a signed-in admin whose role does NOT grant Announcements.Send
  When they navigate to /admin/announcements
  Then they land on /not-permitted with HTTP 200
  And no compose/estimate/list request fires
  # The nav item carries RequiredPermission = Announcements.Send, so it is hidden from the rail.

Scenario: The create + estimate endpoints reject a caller without Announcements.Send
  Given a signed-in admin whose role grants Announcements.View but NOT .Send
  When POST /account/api/admin/notifications/broadcast is issued directly
  Then the API returns HTTP 403
  And no NotificationBroadcast row is inserted
  When POST /account/api/admin/notifications/broadcast/estimate is issued directly
  Then the API returns HTTP 403

Scenario: The history endpoints require only Announcements.View
  Given a signed-in admin whose role grants Announcements.View
  When POST /admin/notifications/broadcasts/list is issued
  Then the API returns HTTP 200 (View is enough to read the history)
```

**Evidence:** `SIMF.Api.Tests/NotificationBroadcastTests.cs` — the admin desk
endpoints reject a caller missing the required permission with 403;
`SIMF.Api.Tests/PermissionEnforcementTests.cs` guards that every admin endpoint is
policy-gated.

### E2E-ANN-009 — Validation: blank title / message

```gherkin
Scenario: A blank English or Arabic title/message is rejected
  Given the administrator is composing a broadcast
  When they leave Title (English) blank and click "Send broadcast"
  Then POST .../broadcast returns HTTP 400 (FluentValidation)
  And the failure message reads "The English title is required." / "العنوان بالإنجليزية مطلوب."
  And no row is inserted
  # Same for a blank Title (Arabic), Message (English), or Message (Arabic):
  #   "The Arabic title is required." / "The English message is required." / "The Arabic message is required."
  # On any failure the CP shows the red toast "The broadcast could not be sent." /
  #   "تعذّر إرسال الإعلان." (Admin.Announcements.Failed)
```

### E2E-ANN-010 — Validation: oversize title / message

```gherkin
Scenario: An over-length title or message is rejected
  Given the administrator is composing a broadcast
  When they enter a Title (English) of 201 characters and click "Send broadcast"
  Then POST .../broadcast returns HTTP 400
  And the failure message reads
      "The English title must be at most 200 characters." /
      "يجب ألا يتجاوز العنوان بالإنجليزية 200 حرف."
  # The two Message textareas are hard-capped at MaxLength=2000 in the UI, so a > 2000
  # body is only reachable via a scripted client; the service re-validates the 1..2000
  # bound and throws BROADCAST_INVALID (400) as defence-in-depth if the FluentValidation
  # layer is bypassed. Title max = 200, Body max = 2000, all four fields required.
```

### E2E-ANN-011 — Session mode with no session → BROADCAST_INVALID

```gherkin
Scenario: A session broadcast without a session id is rejected
  Given a create request with TargetMode="Session" and SessionId=null
  When POST /admin/notifications/broadcast is issued
  Then the API returns HTTP 400
  And ApiResult.Error.Code = "BROADCAST_INVALID"
  And the message reads "Select a session for a session broadcast." / "اختر جلسة لبثّ خاص بجلسة."
  And no NotificationBroadcast row is inserted
  # The compose form only enables Send with a session selected, so this is reached
  # via a scripted client or a race — assert at the API layer.
```

### E2E-ANN-012 — Unknown session → SESSION_NOT_FOUND

```gherkin
Scenario: A session broadcast for an unknown session id is rejected
  Given a create request with TargetMode="Session" and SessionId = a random Guid not in Sessions
  When POST /admin/notifications/broadcast is issued
  Then the API returns HTTP 404
  And ApiResult.Error.Code = "SESSION_NOT_FOUND"
  And the message reads "The session was not found." / "لم يتم العثور على الجلسة."
  And no NotificationBroadcast row is inserted
```

### E2E-ANN-013 — Invalid audience scope → BROADCAST_INVALID

```gherkin
Scenario: An audience broadcast with an unrecognised scope is rejected
  Given a create request with TargetMode="Audience" and AudienceScope="Nobody"
  When POST /admin/notifications/broadcast is issued
  Then the API returns HTTP 400
  And ApiResult.Error.Code = "BROADCAST_INVALID"
  And the message reads "Choose a valid audience." / "اختر جمهوراً صحيحاً."
  # The estimate endpoint is fail-soft — an unrecognised scope estimates as 0 rather
  # than erroring — so /broadcast/estimate with AudienceScope="Nobody" returns
  # HTTP 200 with EstimatedRecipients = 0.
```

### E2E-ANN-014 — Server 500 / load failure

```gherkin
Scenario: A 500 on send surfaces the failure toast, no row inserted
  Given the notification-broadcast service is forced to throw on create (fault injection)
  When the administrator composes a valid message and clicks "Send broadcast"
  Then POST .../broadcast returns HTTP 500
  And a red toast reads "The broadcast could not be sent." / "تعذّر إرسال الإعلان."
  And the history grid is unchanged

Scenario: A 500 on the history load surfaces the load-failed toast
  Given /admin/notifications/broadcasts/list is forced to 500
  When the administrator opens /admin/announcements
  Then a red toast reads "The broadcasts could not be loaded." / "تعذّر تحميل الإعلانات."
  And no rows render and the empty-state does NOT show (the load failed, it did not return empty)
```

### E2E-ANN-015 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the compose form + history grid
  Given the administrator is on /admin/announcements in English
  When they switch the language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the banner title reads "الإعلانات"
  And the "Send to" label reads with options "جلسة محددة" / "جمهور واسع"
  And the Importance options read Info / Success / Warning / Critical in Arabic
      (the fourth option displays "Critical"; its wire value is Severity="Error")
  And the recipient line reads "سيصل هذا إلى N مستلماً."
  And the "Send broadcast" button reads "إرسال إعلان"
  And the history grid headers (When / Target / Message / Status / Recipients / Emails) mirror for RTL
  And the status labels read قيد الانتظار (Queued) / قيد الإرسال (Sending) / تم الإرسال (Sent) / فشل (Failed)
  And the Message column shows the Arabic title under the Arabic UI
```

---

## Implementation notes

- **API integration tests** at
  [`tests/SIMF.Api.Tests/NotificationBroadcastTests.cs`](../../../tests/SIMF.Api.Tests/NotificationBroadcastTests.cs)
  cover the same surface at a lower layer: create → a `Pending` job + the estimate,
  the recipient-resolution rules per target/scope, the worker fan-out (in-app +
  email counts, session vs audience), the `BROADCAST_INVALID` / `SESSION_NOT_FOUND`
  guards, the `View` / `Send` permission split, and the history list projection
  (composer display name + session title, no recipient PII). During the Playwright
  transition keep both layers; the browser E2E adds the compose form, the live
  estimate, the status labels, the toast text, and RTL coverage the API tests can't
  assert.
- **Backing surface:**
  - Create/queue — `POST /api/v1/admin/notifications/broadcast`
    (`AdminCreateBroadcastRequest` → `ApiResult<AdminBroadcastResult { Id, EstimatedRecipients }>`),
    gate `Announcements.Send`, rate-limited (`auth`).
  - Estimate — `POST /api/v1/admin/notifications/broadcast/estimate`
    (`AdminBroadcastEstimateRequest` → `ApiResult<AdminBroadcastEstimateResult { EstimatedRecipients }>`),
    gate `Announcements.Send`, **fail-soft** (unpicked session / bad scope → 0).
  - History list — `POST /api/v1/admin/notifications/broadcasts/list`
    (`GridQuery` → `ApiResult<GridPage<AdminBroadcastSummary>>`), gate `Announcements.View`,
    newest-first, page 25/200-clamped.
  - Detail — `GET /api/v1/admin/notifications/broadcasts/{id:guid}`
    (`ApiResult<AdminBroadcastSummary>`), gate `Announcements.View`; unknown id →
    404 `BROADCAST_NOT_FOUND`. (Exists in the API; the current CP grid has no
    per-row detail modal.)
  - CP calls all four via the same-origin BFF proxies under
    `/account/api/admin/notifications/...`.
  - Permissions — `PermissionCatalog.Announcements.Send` (page + create + estimate) /
    `.View` (list + detail), both baselined `AdminOnly`.
  - Error codes — `BROADCAST_INVALID` (400 — bad/missing target, session-missing on
    a session broadcast, bad audience scope, length out-of-range as defence-in-depth),
    `SESSION_NOT_FOUND` (404 — unknown session on create), `BROADCAST_NOT_FOUND` (404
    — unknown id on detail). FluentValidation returns 400 for blank/oversize
    title (≤ 200) / body (≤ 2000), all four EN+AR required.
  - Status — `BroadcastStatus` = Pending / Processing / Completed / Failed, shown as
    Queued / Sending / Sent / Failed. The worker is claim-first (at-most-once): a
    restart mid-send leaves the row `Processing` and it is never re-picked.
  - Notification kind — `NotificationKind.AdminAnnouncement` (persisted by name);
    session-scoped rows group under the app's Sessions chip.
- **Recipient rules** (resolved at send time, no cross-DB copy — D-157):
  - Session target → distinct `SeatReservations.ReservedForUserId` where
    `ReleasedAt` is null and `ReservedForUserId` is set, for that session.
  - `ApprovedAppUsers` → non-Admin `Users` with `AccountState == Approved`.
  - `EventAttendees` → distinct users with any active seat reservation.
  - `EveryoneIncludingPending` → all non-Admin `Users` regardless of state.
  - Each recipient gets one `AdminAnnouncement` in-app notification and, if they
    have an email on file, one bilingual email.
- **Seeding rows for the E2E run.** The CP can create a broadcast directly from the
  compose form (unlike the response-only request desks). Seed recipients by giving
  users active `SeatReservations` (session / EventAttendees targets) or an
  `Approved` / `Pending` `AccountState` (audience targets); let (or force) the
  `NotificationBroadcastWorker` run to move a row past `Queued`.
- **Mirror.** The desk is modelled on `AdminInvitationService.NotifyVipsAsync`
  (Invitations "bulk-notify"), made durable + paced; it sits in the same
  "Public relations" nav group as
  [`admin-invitations.md`](../../pages/cp/admin-invitations.md) and
  [`admin-vips.md`](../../pages/cp/admin-vips.md).

---

_Last reviewed:_ `2026-07-25` by `SIMF Team` — D-132 (Announcements): admin
broadcast-Notifications desk (in-app + email; session attendees or a broad audience;
background-processed).
