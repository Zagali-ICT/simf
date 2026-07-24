# SIMF Owner-Acceptance Test Round (2026-07)

| | |
|--|--|
| **Title** | Owner-Acceptance Test Round - the owner's prioritised production-readiness journeys |
| **Status** | Living catalogue (not a controlled `SIMF-XXX-NNN` deliverable) |
| **Parent** | [`SIMF-TST-001-Test-Plan.md`](../SIMF-TST-001-Test-Plan.md) (strategy) |
| **Companions** | [`SIMF-Business-Flows.md`](SIMF-Business-Flows.md) (15 cross-page journeys) - [`e2e/README.md`](e2e/README.md) (per-page catalogue) |
| **Ref under test** | `origin/main` @ `c544881c` |
| **Created** | 2026-07-24 |

> **What this is.** The owner supplied a prioritised list of nine acceptance
> journeys that must be proven before hand-over. This document turns each into a
> concrete, tool-agnostic Gherkin scenario **grounded in the real routes,
> endpoints, worker constants, enum values and email paths** (each cited to the
> source), threads them across the Control Panel, the mobile app and the backend
> workers, and records the **observations / candidate defects** found while
> grounding them. Each journey cross-references the existing per-page E2E files
> and the xUnit suites that already cover parts of it, so this round is additive,
> not a duplicate.
>
> **No literal secret appears.** Admin TOTP uses the `Get-Totp` helper; visitor /
> badge / meeting OTP codes are read from `SIMF_Identity.AccountCodes` at run time.
> The live E2E round runs against a **local** stack (API `:5275`, CP `:5278`,
> Website `:5280`) on throwaway LocalDB databases - never the production host.

## How the owner's list maps to journeys

| # | Owner request (paraphrased) | Journey | Grounded surface | Existing coverage |
|---|-----------------------------|---------|------------------|-------------------|
| 1 | Welcome message shows the wrong first name | [OA-01](#oa-01-welcome--greeting-name) | `greeting_header.dart:26` (app) - email templates use full `{DisplayName}` | mobile-home.md |
| 2 | Sign-in, sign-out, and staying signed in - for the report | [OA-02](#oa-02-sign-in--sign-out--session-persistence) | `SignInEndpoint` / `SignOutEndpoint`; audit `SignIn.Succeeded` / `SignOut.Succeeded`; JWT 5 min / session 24 h | cp-auth-flow.md, BF-13 |
| 3 | Ratings at end-of-session / end-of-day / end-of-forum + report shape | [OA-03](#oa-03-rating-triggers--report) | `SessionRatingPromptWorker`, `ProgrammeRatingPromptWorker`; `/admin/ratings`; `POST /app/feedback/submit` | cp-admin-ratings.md, mobile-rate.md, BF-08 |
| 4 | Move forum dates today→Monday, create sessions, confirm all read from server | [OA-04](#oa-04-dynamic-forum-dates--sessions-read-from-server) | `OrganizationProfile.EventStartDate/EndDate`; `EventDateRange`; independent `Session.StartUtc/EndUtc` | cp-organization-profile.md, cp-admin-sessions.md |
| 5 | Meeting request, edit speaker email, confirm two emails (confirmation + notice) | [OA-05](#oa-05-meeting-request--speaker-email--the-two-emails) | speaker vs delegation flows; `Contact.Email`; `MeetingActionTokenService` | cp-admin-speaker-meeting-requests.md, cp-admin-delegation-meetings.md, mobile-meetings.md |
| 6 | 15-minute reminder + in-hall meeting check-in + reports | [OA-06](#oa-06-15-minute-reminder--hall-check-in) | `MeetingReminderWorker.ReminderLeadTime = 15 min`; check-in `Accepted→Done` | cp-business-meetings.md, cp-admin-delegation-meetings.md |
| 7 | Session check-in / out / return; seat available ~2 min before; seat-state + real-time | [OA-07](#oa-07-session-attendance--seat-reservation) | `HallAttendance`; `SeatReservationService` `NoShowReleaseGrace = 3 min`; poll-based refresh | mobile-my-seat.md, mobile-seat-picker.md, BF-05 |
| 8 | Data entry (forum + tracks), filter mechanism, does the AI work | [OA-08](#oa-08-data-entry--filter--ai) | `/admin/themes` (المحاور), `/admin/sessions`; `?day=` filter only; AI **Echo stub** by default | cp-admin-sessions.md, cp-admin-themes.md, mobile-chatbot.md |
| 9 | Live video stream + rating after | [OA-09](#oa-09-live-stream--post-session-rating) | `Session.LiveStreamUrl` (YouTube); `SessionPhase`; rating fires on `EndUtc` | mobile-live.md, cp-admin-session-live-hall.md, mobile-rate.md |

---

## OA-01 - Welcome / greeting name

**Grounded facts.** Backend account emails greet the whole display name: `WelcomeEn` = `"Hello {DisplayName},"` and `WelcomeAr` = `"مرحباً {DisplayName}،"` at [NotificationEmailTemplates.cs:116-170](../../src/Backend/SIMF.Application/Notifications/NotificationEmailTemplates.cs#L116); the token is populated from `user.DisplayName ?? user.Email` at [AdminAccountService.cs:708](../../src/Backend/SIMF.Infrastructure/Identity/AdminAccountService.cs#L708). `DisplayName` is a single free-text field ([SimfUser.cs:22](../../src/Backend/SIMF.Domain/IdentityAccess/SimfUser.cs#L22)); there is no first/last split in the email path. The mobile home greeting DOES derive a first name: `final firstName = name.trim().split(' ').first;` at [greeting_header.dart:26](../../src/Mobile/simf_app/lib/features/home/widgets/greeting_header.dart#L26), fed the full localized name from [home_screen.dart:131-137](../../src/Mobile/simf_app/lib/features/home/home_screen.dart#L131).

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-OA-01-001 | Email greeting renders the full display name (EN + AR) | happy | P1 |
| E2E-OA-01-002 | App home greeting shows only the first whitespace token of the name | happy | P1 |
| E2E-OA-01-003 | Arabic compound given name is truncated by the split (candidate defect) | error | P0 |
| E2E-OA-01-004 | Placeholder guard: unfinished profile shows the icon, not the email local-part | error | P2 |

### Scenarios

```gherkin
Feature: Welcome / greeting name
  Background:
    Given the API is reachable on http://localhost:5275

  Scenario: E2E-OA-01-001 - Account welcome email uses the full display name
    Given an admin creates an account with DisplayName "Khalid Al Otaibi"
    When the AccountWelcome email is sent
    Then the English body contains "Hello Khalid Al Otaibi,"
    And the Arabic body contains "مرحباً Khalid Al Otaibi،"
    And no first-name split is applied in the email

  Scenario: E2E-OA-01-003 - Arabic compound given name is truncated in the app greeting
    Given a signed-in visitor whose localized name is "عبد الله محمد"
    When the home screen renders the greeting header
    Then the greeting shows "عبد 👋"
    # EXPECTED by design intent (comment at greeting_header.dart:23-25) is the given name,
    # but split(' ').first drops "الله" from the compound given name -> candidate defect OA-D1.
```

**Observation / candidate defect OA-D1.** `split(' ').first` is unsafe for Arabic compound given names (`عبد الله`, `عبد الرحمن`) and for family-name-first data. Suggested fixes to discuss with the owner: (a) greet the full localized name (drop the split); (b) add an explicit `FirstName`/`GivenName` field captured at sign-up; (c) keep the split but special-case the `عبد ...` construction. This is report-only until the owner chooses.

---

## OA-02 - Sign-in / sign-out / session persistence

**Grounded facts.** Password step `POST /app/auth/sign-in` ([SignInEndpoint.cs:15](../../src/Backend/SIMF.Api/Endpoints/Auth/SignInEndpoint.cs#L15)); visitor OTP `POST /app/auth/verify-otp`; CP TOTP `POST /app/auth/verify-totp`; sign-out `POST /app/auth/sign-out` ([SignOutEndpoint.cs:19](../../src/Backend/SIMF.Api/Endpoints/Auth/SignOutEndpoint.cs#L19)) revokes every refresh token and rolls the security stamp ([SessionService.cs:175-177](../../src/Backend/SIMF.Application/IdentityAccess/SessionService.cs#L175)). JWT access-token lifetime `= 5 min`, absolute session `= 24 h` ([JwtOptions.cs:22,29](../../src/Shared/SIMF.Common/Options/JwtOptions.cs#L22)); refresh rotation keeps the original deadline (no sliding window). Audit keys: `SignIn.Succeeded`, `SignIn.SecondFactorIssued`, `SignOut.Succeeded` ([AuditEvents.cs](../../src/Backend/SIMF.Application/Auditing/AuditEvents.cs)); sessions live in the `RefreshToken` table.

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-OA-02-001 | CP sign-in (password + TOTP) writes `SignIn.Succeeded` | happy | P0 |
| E2E-OA-02-002 | App sign-in (password + OTP from AccountCodes) succeeds | happy | P0 |
| E2E-OA-02-003 | Sign-out revokes all sessions; the old access token is rejected within its 5-min window after stamp roll | auth | P0 |
| E2E-OA-02-004 | "Stay signed in": without sign-out the refresh token rotates and the session survives up to the 24-h cap | happy | P1 |
| E2E-OA-02-005 | Report source: the sign-in / sign-out audit rows feed the operation-log report | happy | P1 |

```gherkin
  Scenario: E2E-OA-02-003 - Sign-out ends every session
    Given a signed-in CP administrator with a valid access token A and refresh token R
    When they POST /app/auth/sign-out
    Then an audit row "SignOut.Succeeded" is written for the actor
    And R is revoked so a refresh with R returns AUTH_INVALID_CREDENTIALS
    And the security stamp roll invalidates A on its next validation

  Scenario: E2E-OA-02-004 - Not signing out keeps the session alive to the cap
    Given a signed-in account that never signs out
    When 5 minutes pass and the app rotates the refresh token
    Then a fresh 5-minute access token is issued
    And the rotated token keeps the ORIGINAL 24-hour session deadline (no sliding extension)
    And after 24 hours from first sign-in a rotation is refused
```

**Observation.** For the report the owner wants, the authoritative source is the CP operation-log / audit view (`SignIn.Succeeded` vs `SignOut.Succeeded` rows), not a client-side flag - an account that "did not sign out" has no `SignOut.Succeeded` row and a live `RefreshToken`.

---

## OA-03 - Rating triggers + report

**Grounded facts.** Three prompt paths, all polling once per minute:
- **End-of-session**: `SessionRatingPromptWorker` fires when `IsActive && RatingPromptSentUtc == null && EndUtc <= now && EndUtc >= now-6h`, audience = active `SeatReservation` holders, emits `NotificationKind.SessionRatingRequest` (=45) ([SessionRatingPromptWorker.cs:121-149](../../src/Backend/SIMF.Infrastructure/Operations/SessionRatingPromptWorker.cs#L121)). Also triggered on hall departure (D-713), sharing one dedup guard.
- **End-of-day**: `ProgrammeRatingPromptWorker` on `DayEndUtc`, audience = accounts who checked in that day, emits `DayRatingRequest` (=46).
- **End-of-forum**: last active day + 1 h grace, once-only via SystemSetting marker, dispatches the trio `EventRatingRequest` (=47, "قيّم الملتقى"), `ExhibitionRatingRequest` (=49), `AppRatingRequest` (=48) ([ProgrammeRatingPromptWorker.cs:233-289](../../src/Backend/SIMF.Infrastructure/Operations/ProgrammeRatingPromptWorker.cs#L233)).

Submit `POST /app/feedback/submit`; `OverallStars` 1-5, `Comment` max 2000 ([FeedbackEndpoints.cs:74](../../src/Backend/SIMF.Api/Endpoints/Feedback/FeedbackEndpoints.cs#L74)). Scopes = Global / PerSession / PerDay ([RatingScope.cs](../../src/Shared/SIMF.Common/Enums/RatingScope.cs)); 5 seeded types (App, Session, Day, Event, Exhibition). Report surface: `/admin/ratings` list + `GET /admin/feedback/ratings/kpi` (average overall + per-question averages).

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-OA-03-001 | End-of-session prompt reaches every seat holder once | happy | P0 |
| E2E-OA-03-002 | End-of-day prompt reaches that day's checked-in attendees once | happy | P0 |
| E2E-OA-03-003 | End-of-forum trio (event + exhibition + app) fires exactly once | happy | P0 |
| E2E-OA-03-004 | Submit a session rating (4 stars + comment) and see it in `/admin/ratings` | happy | P0 |
| E2E-OA-03-005 | KPI aggregate matches the submitted scores | happy | P1 |
| E2E-OA-03-006 | Validation: 0 or 6 stars, comment > 2000 chars → `VALIDATION_FAILED` | error | P1 |

```gherkin
  Scenario: E2E-OA-03-001 - End-of-session rating prompt
    Given a session with EndUtc 2 minutes in the past and 3 active seat reservations
    When SessionRatingPromptWorker polls
    Then each of the 3 holders receives one SessionRatingRequest notification
    And Session.RatingPromptSentUtc is stamped so a second poll sends nothing

  Scenario: E2E-OA-03-004 - Submit and read back a session rating
    Given the visitor opens the rating screen from the session notification
    When they submit OverallStars=4 and comment "جلسة ممتازة"
    Then POST /app/feedback/submit returns ApiResult.Ok
    And /admin/ratings lists the response under the Session rating type
    And the KPI average reflects the new score
```

---

## OA-04 - Dynamic forum dates + sessions read from server

**Grounded facts.** `OrganizationProfile.EventStartDate` / `EventEndDate` (singleton row) at [OrganizationProfile.cs:79-82](../../src/Backend/SIMF.Domain/Organization/OrganizationProfile.cs#L79); edited via `PUT /admin/organization-profile` ([AdminOrganizationProfileEndpoints.cs:38](../../src/Backend/SIMF.Api/Endpoints/Admin/AdminOrganizationProfileEndpoints.cs#L38)) on the `/admin/organization-profile` page. Read publicly via `GET /app/organization-profile` (anonymous, 304-capable). Shared `EventDateRange.Format(...)` ([EventDateRange.cs:39](../../src/Shared/SIMF.Common/EventDateRange.cs#L39)) drives the website ([ForumDates.cs](../../src/Website/SIMF.Web/Content/ForumDates.cs)) and app ([organization_profile.dart:101](../../src/Mobile/simf_app/lib/core/organization_profile/organization_profile.dart#L101)). **Sessions carry their own `StartUtc`/`EndUtc`** ([Session.cs:68-72](../../src/Backend/SIMF.Domain/Programme/Session.cs#L68)) - independent of the event range - served by `GET /app/programme/sessions?day=yyyy-MM-dd` and `GET /app/programme/days`.

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-OA-04-001 | Change event dates to this coming Mon-Wed; website + app render the new range | happy | P0 |
| E2E-OA-04-002 | Create three sessions on Monday; they appear under `?day=<Monday>` | happy | P0 |
| E2E-OA-04-003 | App programme reads the new session times from the server (no stale cache) | happy | P0 |
| E2E-OA-04-004 | Session outside the event range still renders (dates are independent) | edge | P1 |
| E2E-OA-04-005 | Public read honours `If-Modified-Since` → 304 after no change | perf | P2 |

```gherkin
  Scenario: E2E-OA-04-001 - Shift the forum to next week and confirm every surface
    Given the administrator opens /admin/organization-profile
    When they set EventStartDate to next Monday and EventEndDate to next Wednesday and save
    Then GET /app/organization-profile returns the new range
    And the website hero and the app home render "{Mon}-{Wed} {Month} {Year}" via EventDateRange
    And the same Western-digit range shows in Arabic and English

  Scenario: E2E-OA-04-002 - Create Monday sessions and read them back
    Given three sessions are created with StartUtc on next Monday
    When the app calls GET /app/programme/sessions?day=<next-Monday>
    Then all three are returned with their server StartUtc/EndUtc
    And GET /app/programme/days lists Monday with a session count of 3
```

**Observation.** Because session dates are independent columns, moving the event range does NOT move existing sessions; the owner journey must re-create or re-time sessions after shifting the range. The server is the single source of truth for both (no client date synthesis).

---

## OA-05 - Meeting request + speaker email + the two emails

**Grounded facts (important nuance).** A `Speaker` has no `Email`; the speaker's email is the linked `Contact.Email` resolved via `Speaker.ContactId` ([Speaker.cs:86](../../src/Backend/SIMF.Domain/Programme/Speaker.cs#L86), [SpeakerMeetingRequestService.cs:610-625](../../src/Backend/SIMF.Infrastructure/MeetingRequests/SpeakerMeetingRequestService.cs#L610)). App submit `POST /app/speakers/{id}/meeting-requests` is **audit-only (no email on submit)**. On admin **Approve-with-hall**, the service mints two 72-h single-use tokens and emails **the speaker** a confirm/decline link (purpose `SpeakerMeetingConfirm`) - **skipped silently if the linked Contact has no Email** ([SpeakerMeetingRequestService.cs:575-604](../../src/Backend/SIMF.Infrastructure/MeetingRequests/SpeakerMeetingRequestService.cs#L575)). When the speaker taps Approve, the requester gets `MeetingRequestConfirmed` **in-app only** (`SendEmail=false`).

The **literal "two emails" pattern is the delegation flow**: on admin Approve the requester gets `MeetingScheduled` (=43, `SendEmail=true`) and each eligible target member gets `MeetingRequested` (=54, `SendEmail=true`, the confirm-link email); on other-party confirm the requester gets `MeetingRequestConfirmed` (=50, `SendEmail=true`) ([DelegationMeetingRequestService.cs:368-486](../../src/Backend/SIMF.Infrastructure/MeetingRequests/DelegationMeetingRequestService.cs#L368)). Confirm-on-tap tokens: anonymous `GET/POST /app/meeting-actions/{token}` ([MeetingActionEndpoints.cs:21-51](../../src/Backend/SIMF.Api/Endpoints/Programme/MeetingActionEndpoints.cs#L21)).

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-OA-05-001 | Set the speaker's Contact.Email, approve a request → speaker receives the confirm-link email | happy | P0 |
| E2E-OA-05-002 | Speaker taps Approve via the anonymous token → requester sees in-app `MeetingRequestConfirmed` | happy | P0 |
| E2E-OA-05-003 | Delegation flow: requester email (scheduled) + target-member email (confirm) = two emails | happy | P0 |
| E2E-OA-05-004 | Precondition gap: approve with a speaker Contact that has NO email → confirm email is silently skipped (candidate defect) | error | P0 |
| E2E-OA-05-005 | Token is single-use / 72-h: reuse or expiry → neutral 404 `MEETING_ACTION_TOKEN_INVALID` | error | P1 |

```gherkin
  Scenario: E2E-OA-05-001 - Edit the speaker email then approve, and the confirm email arrives
    Given a speaker linked to a Contact whose Email was blank
    When the admin edits that Contact and sets Email "speaker@example.com"
    And approves a pending meeting request with a hall + slot
    Then the speaker receives one email (purpose SpeakerMeetingConfirm) with Approve/Decline links
    When the tester opens the Approve link and POSTs /app/meeting-actions/{token}
    Then the request flips AwaitingSpeaker -> Accepted
    And the requester receives an in-app MeetingRequestConfirmed (no email in the speaker flow)

  Scenario: E2E-OA-05-004 - Missing speaker email swallows the confirmation email
    Given a speaker whose linked Contact has no Email
    When the admin approves the request with a hall
    Then the request still moves to AwaitingSpeaker
    But no confirm email is sent (skipped at SpeakerMeetingRequestService.cs:585-589)
    # Candidate defect OA-D2: the desk gives no visible warning that the email could not be sent.
```

**Observation / candidate defect OA-D2.** If the owner means "the requester also gets an email", note the speaker flow sends the requester an **in-app** notice only; use the delegation flow for a true two-email journey, or raise an enhancement. Separately, the silent skip when the speaker Contact has no email is a usability defect worth a visible CP warning.

---

## OA-06 - 15-minute reminder + hall check-in

**Grounded facts.** `MeetingReminderWorker.ReminderLeadTime = TimeSpan.FromMinutes(15)` ([MeetingReminderWorker.cs:34](../../src/Backend/SIMF.Infrastructure/Operations/MeetingReminderWorker.cs#L34)); polls once per minute, scans `Status == Accepted` requests whose slot is inside `(now, now+15m]` with `ReminderSentUtc == null`, stamps the guard before dispatch, emits `NotificationKind.MeetingReminder` (=55, `SendEmail=true`) to the requester and eligible target members. Meeting check-in: `POST /admin/speaker-meeting-requests/{id}/check-in` and `POST /admin/delegation-meeting-requests/{id}/check-in` flip `Accepted → Done` and stamp `CheckedInAt` + `CheckedInByUserId` ([SpeakerMeetingRequestService.cs:498-527](../../src/Backend/SIMF.Infrastructure/MeetingRequests/SpeakerMeetingRequestService.cs#L498)). CP CheckIn buttons on `DelegationMeetingsList.razor` and `SpeakerMeetingRequestsList.razor`.

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-OA-06-001 | Accepted meeting 14 min out → one reminder email + notification; not re-sent | happy | P0 |
| E2E-OA-06-002 | Meeting > 15 min out or not Accepted → no reminder | edge | P1 |
| E2E-OA-06-003 | Operator checks the party in → status `Done`, `CheckedInAt`/`CheckedInBy` stamped | happy | P0 |
| E2E-OA-06-004 | Check-in requires `Accepted`; a Pending/Done request refuses | error | P1 |
| E2E-OA-06-005 | Reporting: the check-in stamps are visible on the desk (export gap noted) | happy | P2 |

```gherkin
  Scenario: E2E-OA-06-001 - 15-minute reminder fires once
    Given an Accepted speaker meeting whose SlotStartUtc is 14 minutes from now
    And ReminderSentUtc is null
    When MeetingReminderWorker polls
    Then the requester and eligible members receive a MeetingReminder email + in-app notice
    And ReminderSentUtc is stamped so the next poll sends nothing

  Scenario: E2E-OA-06-003 - In-hall check-in
    Given an Accepted meeting at its slot time
    When the operator clicks Check-in on /admin/delegation-meetings
    Then the request status becomes Done
    And CheckedInAt and CheckedInByUserId are recorded for the operator
```

**Observation.** No dedicated meeting-check-in **report/export** endpoint was found (the request-list Excel export exists, but a check-in attendance export was not confirmed). Flag as a possible reporting gap for the owner's "reports for that" ask.

---

## OA-07 - Session attendance + seat reservation

**Grounded facts.** Attendance is recorded in `HallAttendance` via `POST /app/sessions/{id}/arrival` (GPS geofence) and operator badge-QR door scans; departure `POST /app/sessions/{id}/departure`; status `GET /app/sessions/{id}/attendance` ([HallAttendanceEndpoints.cs:21-69](../../src/Backend/SIMF.Api/Endpoints/Sessions/HallAttendanceEndpoints.cs#L21)). Check-in and check-out are idempotent; **return (re-check-in) opens a fresh row** after departure. Seat endpoints: `GET /app/sessions/{id}/seats`, `.../reserve`, `.../reserve-random`, `.../join`, `DELETE .../seats/mine` ([SeatReservationEndpoints.cs](../../src/Backend/SIMF.Api/Endpoints/Sessions/SeatReservationEndpoints.cs)). **The no-show release grace is 3 minutes**, not 2: `NoShowReleaseGrace = FromMinutes(3)`, `ExpiresUtc = StartUtc - 3min` ([SeatReservationService.cs:36](../../src/Backend/SIMF.Infrastructure/SeatReservations/SeatReservationService.cs#L36)); `ReservationNoShowReleaseWorker` polls once per minute and frees held seats whose `ExpiresUtc <= now` where the holder has no `HallAttendance`. Reserving is allowed until the session **ends** (a live session is still bookable). The four visible seat states (available / unavailable / reserved / confirmed) are derived, `CheckedIn` = holder has an open `HallAttendance`. **Real-time is pull-based** (the app re-GETs the seat map; no SignalR / no `SIMF.RealTime`).

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-OA-07-001 | Check-in → check-out → return opens a new attendance row each arrival | happy | P0 |
| E2E-OA-07-002 | Reserve a specific seat; the map shows it "reserved" for the holder | happy | P0 |
| E2E-OA-07-003 | Seat availability ~3 min before start: an unclaimed no-show hold is released to others | happy | P0 |
| E2E-OA-07-004 | Holder who has checked in keeps the seat past the release window (state → confirmed) | happy | P0 |
| E2E-OA-07-005 | Seat-state refresh: re-GET reflects another user's reservation (poll, not push) | happy | P1 |
| E2E-OA-07-006 | Release own seat is blocked once the session has started | error | P1 |

```gherkin
  Scenario: E2E-OA-07-003 - No-show seat is freed 3 minutes before start
    Given a visitor reserved seat A-5 for a session starting in 4 minutes
    And the visitor has no HallAttendance for that session
    When the clock passes StartUtc - 3min and ReservationNoShowReleaseWorker polls
    Then seat A-5 is released and becomes available to others
    And the original holder receives a BookingReleased notification
    # NOTE: the owner asked about "2 minutes"; the code releases at 3 minutes (StartUtc - 3min).

  Scenario: E2E-OA-07-004 - Checked-in holder keeps the seat
    Given the same reservation but the holder recorded an arrival (HallAttendance open)
    When the release worker polls
    Then seat A-5 is NOT released and the map shows it as "confirmed"
```

**Observation / candidate defect OA-D3.** Spec-vs-code mismatch: the requirement says a seat frees "2 minutes before"; the implemented grace is **3 minutes** (`StartUtc - 3min`). Either the spec text or the constant should be reconciled with the owner. Real-time seat updates are polling-based, so two users can briefly see the same seat as free until the next re-GET; the reserve call is the authority (a lost race returns a conflict).

---

## OA-08 - Data entry + filter + AI

**Grounded facts.** Programme data entry: `POST/PUT /admin/sessions` ([SessionEndpoints.cs:60,126](../../src/Backend/SIMF.Api/Endpoints/Admin/SessionEndpoints.cs#L60)), `POST/PUT /admin/speakers`, and **"المحاور" = Themes** via `POST/PUT /admin/themes` ([ThemeEndpoints.cs:62,98](../../src/Backend/SIMF.Api/Endpoints/Admin/ThemeEndpoints.cs#L62); Arabic error `"لم يتم العثور على المحور."`). The public programme filter is **`?day=yyyy-MM-dd` only** ([PublicSessionEndpoints.cs:32-55](../../src/Backend/SIMF.Api/Endpoints/Programme/PublicSessionEndpoints.cs#L32)); any theme/track grouping is client-side, not a server filter param. AI: the two anonymous endpoints are `POST /app/ai/faq` and `POST /app/ai/translate` ([AiFeatureEndpoints.cs:61,155](../../src/Backend/SIMF.Api/Endpoints/Ai/AiFeatureEndpoints.cs#L61)); the chatbot `POST /app/ai/assistance` requires an approved account. **The default provider is the Echo stub** (`DefaultProvider = Echo`, all keys blank in [appsettings.json:88-103](../../src/Backend/SIMF.Api/appsettings.json#L88)); `EchoAiProvider` returns `"[echo] " + prompt`. A real answer needs `DefaultProvider` + `SIMF_Ai__<Provider>__ApiKey` set; otherwise concrete providers throw `AI_PROVIDER_NOT_CONFIGURED` (503).

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-OA-08-001 | Create a Theme (المحور), a Speaker and a Session in the CP | happy | P0 |
| E2E-OA-08-002 | App programme filters by day and shows the new session | happy | P0 |
| E2E-OA-08-003 | AI with committed config returns the Echo stub, not a real answer (candidate defect for "does AI work") | error | P0 |
| E2E-OA-08-004 | AI with a provider key set returns a real grounded answer | happy | P1 |
| E2E-OA-08-005 | Anonymous FAQ / translate endpoints answer without auth; assistance requires approval | auth | P1 |

```gherkin
  Scenario: E2E-OA-08-003 - AI works only when a provider is configured
    Given the API runs with the committed Ai config (DefaultProvider=Echo, blank keys)
    When a signed-in visitor asks the chatbot via POST /app/ai/assistance
    Then the response is the deterministic Echo stub, not a real answer
    # Candidate observation OA-D4: "does the AI work" is FALSE under the shipped config;
    # it requires DefaultProvider + a provider ApiKey via SIMF_Ai__<Provider>__ApiKey.

  Scenario: E2E-OA-08-002 - Day filter returns the new session
    Given a Session created on next Monday under a new Theme
    When the app calls GET /app/programme/sessions?day=<Monday>
    Then the session is listed with its Theme and Speaker
    And no server-side theme filter param is required (grouping is client-side)
```

**Observation OA-D4.** Under the committed configuration the AI does not produce real answers (Echo stub). This is expected pre-production, but the owner's "does the AI work" acceptance requires a provider key in the test/staging environment. There is also no server-side theme/category filter param on the public programme endpoint - only `?day=`; if a theme filter is expected, it is a gap.

---

## OA-09 - Live stream + post-session rating

**Grounded facts.** `Session.LiveStreamUrl` + optional `Session.LiveSignLanguageUrl` ([Session.cs:143-149](../../src/Backend/SIMF.Domain/Programme/Session.cs#L143)); accepted URLs validated by `LiveStreamUrlPolicy.IsAllowed` (https YouTube 11-char id, or `.m3u8`/`.mp4`) ([LiveStreamUrlPolicy.cs:38](../../src/Shared/SIMF.Common/LiveStreamUrlPolicy.cs#L38)); app plays via `youtube_player_iframe`. Phase gating is the Flutter `SessionPhase { upcoming, live, ended }` ([session_lifecycle.dart:20](../../src/Mobile/simf_app/lib/features/sessions/data/session_lifecycle.dart#L20)); the live button shows only when `hasLiveStream && phase == live`. The post-session rating fires on `EndUtc` regardless of whether the session was live (shared with OA-03).

### Coverage matrix

| ID | Scenario | Type | Priority |
|----|----------|------|----------|
| E2E-OA-09-001 | Admin sets a valid YouTube live URL; app shows the live player when phase == live | happy | P0 |
| E2E-OA-09-002 | Invalid / http URL is rejected by `LiveStreamUrlPolicy` | error | P1 |
| E2E-OA-09-003 | Sign-language companion URL toggles a second feed | happy | P2 |
| E2E-OA-09-004 | After `EndUtc` the session rating prompt fires (ties to OA-03) | happy | P0 |
| E2E-OA-09-005 | Emulator caveat: YouTube playback needs a real device / cert-trust | env | P2 |

```gherkin
  Scenario: E2E-OA-09-001 - Live playback then rating
    Given a session with a valid https YouTube LiveStreamUrl and StartUtc <= now < EndUtc
    When a visitor opens the session in the app
    Then SessionPhase resolves to live and the live player renders
    When the session EndUtc passes and SessionRatingPromptWorker polls
    Then the visitor receives a SessionRatingRequest and can rate the (formerly live) session
```

**Observation.** YouTube playback fails on the Android emulator (cert-trust) - live-video acceptance must use a real device or a real-device staging pass; the backend URL-policy and rating trigger are testable without a device.

---

## Consolidated observations / candidate defects (for the QA report)

| Ref | Area | Type | Summary |
|-----|------|------|---------|
| OA-D1 | 1 | Correctness (app) | Home greeting `split(' ').first` truncates Arabic compound given names (`عبد الله` → `عبد`) and mishandles family-name-first data. Report-only; owner to pick a fix. |
| OA-D2 | 5 | UX / resilience | Speaker confirm email is silently skipped when the linked Contact has no Email (and when `PublicWebBaseUrl` is unset); no CP warning. |
| OA-D3 | 7 | Spec vs code | No-show seat release is 3 minutes before start (`StartUtc - 3min`), not the "2 minutes" the owner stated. Reconcile spec or constant. |
| OA-D4 | 8 | Config / acceptance | AI ships on the Echo stub (blank keys); "does the AI work" is false under committed config until a provider key is set in test/staging. |
| OA-D5 | 6 | Reporting gap | No dedicated meeting hall-check-in report/export endpoint confirmed. |
| OA-D6 | 8 | Feature gap | Public programme endpoint filters by `?day=` only; no server-side theme/category filter. |

_Last reviewed:_ `2026-07-24` by the QA automation pass. Grounded against `origin/main` @ `c544881c`; all routes / constants / enum values cited to source above.
