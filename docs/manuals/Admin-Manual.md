# SIMF — Administrator Manual

| | |
|--|--|
| **Audience** | SIMF Control Panel administrators (operators of `/admin/*` and `/m/*`) |
| **Scope** | Every CP module + the auth + account-area surfaces |
| **Authority** | D-133 (2026-05-28) — vertical slice; remaining chapters listed in §1.4 |
| **Bilingual** | Yes — Arabic translations land alongside each chapter (translator-pending) |
| **Companion docs** | [`User-Manual.md`](User-Manual.md) (Website + Mobile), [`Developer-Guide.md`](Developer-Guide.md), [`docs/pages/PAGE-INDEX.md`](../pages/PAGE-INDEX.md) |

This manual is **the daily operator playbook**. Every module the administrator
can reach in the Control Panel has its own chapter that explains: what the
module is for, how to do the most common tasks, what each control does, and
what to do when something goes wrong. The chapters track 1-to-1 against
[`docs/pages/PAGE-INDEX.md`](../pages/PAGE-INDEX.md) — when a row there
shows ✅ Real, this manual has a chapter for it. The single remaining 🚧 Stub
route (`/m/live-sessions`) has no chapter, because it has no page to operate.

> **Reading this manual:** start with §2 if you've never signed in before;
> jump to a specific module chapter (§4 onwards) when you have a job to do.
> Every section ends with a **Troubleshooting** subsection that covers the
> top 3 things that go wrong.

---

## 1. Contents

1. **Introduction** (this section)
    - 1.4 Coverage status
2. **Sign in + first-time setup**
    - 2.1 What you need
    - 2.2 First sign-in (TOTP pairing)
    - 2.3 Subsequent sign-ins
    - 2.4 Lost your phone
    - 2.5 Troubleshooting
3. **Daily walkthrough: the Control Panel shell**
    - 3.1 The canonical CRUD list pattern
    - 3.2 Troubleshooting
3A. **Overview modules**
    - 3A.1 Dashboard — `/`
    - 3A.2 Session attendance — `/admin/attendance`
    - 3A.3 Live hall — `/admin/sessions/live-hall`
    - 3A.4 Statistics — `/admin/statistics`
4. **People modules**
    - 4.1 Attendees — `/admin/attendees` (D-134 Sprint A)
    - 4.2 Print badge desk — `/admin/print-bag`
    - 4.3 Roles & permissions — `/admin/roles` (D-134 Sprint A)
4A. **People modules (remaining)**
    - 4A.1 VIP registration — `/admin/visitors/vip`
    - 4A.2 VIP welcome export (Mawj) — `/admin/visitors/vip/export`
    - 4A.3 Delegates — `/admin/delegates`
    - 4A.4 Badge batches — `/admin/visitors/badge-batches`
5. **Programme modules (D-134 Sprint B / D-135)**
    - 5.1 Themes & pillars — `/admin/themes`
    - 5.2 Halls & seating — `/admin/halls` (D-134 Sprint B / D-135)
    - 5.3 Speakers — `/admin/speakers`
    - 5.4 Sessions — `/admin/sessions`
    - 5.5 Booking monitor — `/admin/bookings`
5A. **Programme modules (remaining)**
    - 5A.1 Session categories — `/admin/session-categories`
    - 5A.2 Programme days — `/admin/programme-days`
    - 5A.3 Run of Show — `/admin/programme/timeline`
    - 5A.4 Hall seat layouts — `/admin/halls/seat-layouts`
    - 5A.5 Session seat plans — `/admin/sessions/seat-plans`
    - 5A.6 Speaker presentations — `/admin/speaker-presentations`
5B. **Meetings & availability**
    - 5B.1 Speaker meeting requests — `/admin/speaker-meeting-requests`
    - 5B.2 Speaker availability — `/admin/speaker-availability`
    - 5B.3 Hall availability — `/admin/hall-availability`
    - 5B.4 Delegation meetings — `/admin/delegation-meetings`
    - 5B.5 Delegation availability — `/admin/delegation-availability`
    - 5B.6 Document requests — `/admin/document-requests`
    - 5B.7 Badge requests — `/admin/badge-requests`
    - 5B.8 Meeting Tables & Hall Allocation — `/admin/meeting-tables`
    - 5B.9 Business Meetings — `/admin/business-meetings`
6. **Scientific committee & Exhibition modules**
    - 6.1 Session moderators — `/admin/session-moderators`
    - 6.2 Question queue — `/admin/question-queue`
    - 6.3 Session summaries — `/admin/session-summaries`
    - 6.4 Exhibitors — `/admin/exhibitors`
    - 6.5 Booths — `/admin/booths`
    - 6.6 Sponsors — `/admin/sponsors`
    - 6.7 Venue map — `/admin/venue-map`
7. **Engagement & Knowledge modules**
    - 7.1 Ratings — `/admin/ratings`
    - 7.2 Rating configuration — `/admin/rating-config`
    - 7.3 FAQ groups & entries — `/admin/faq`
    - 7.4 AI dashboard — `/admin/ai`
    - 7.5 AI services — `/admin/ai/services`
    - 7.6 AI prompts — `/admin/ai/prompts`
    - 7.7 AI invocations — `/admin/ai/invocations`
8. **Content & Public relations modules**
    - 8.1 Media library — `/admin/media-library`
    - 8.2 Content blocks — `/admin/content-blocks`
    - 8.3 Banners — `/admin/banners`
    - 8.4 Media Center — `/admin/media`
    - 8.5 News — `/admin/news`
    - 8.6 Media Partners — `/admin/media-partners`
    - 8.7 Previous editions — `/admin/archive`
    - 8.8 Invitations — `/admin/invitations`
    - 8.9 VIPs — `/admin/vips`
    - 8.10 Announcements — `/admin/announcements`
    - 8.11 Contact inquiries — `/admin/contact-inquiries`
9. **Gates, Reference data & System modules**
    - 9.1 Gates — `/admin/gates`
    - 9.2 Gate operator console — `/admin/gates/operator`
    - 9.3 Hall arrivals (door scan) — `/admin/hall-arrivals`
    - 9.4 Gates operations dashboard — `/admin/gates/dashboard`
    - 9.5 Countries — `/admin/countries`
    - 9.6 Organisations — `/admin/organisations`
    - 9.7 Regions — `/admin/regions`
    - 9.8 System configuration — `/admin/configuration`
    - 9.9 Site Settings — `/admin/site-settings`
    - 9.10 Email templates — `/admin/email/templates`
    - 9.11 Organization Profile — `/admin/organization-profile`
    - 9.12 Background services — `/admin/ops/services`
    - 9.13 Operations toggles — `/admin/operations`
10. **System modules**
    - 10.1 Admins — `/admin/admins`
    - 10.2 Pending admins — `/admin/admins/pending`
    - 10.3 Others — `/admin/others`
    - 10.4 Pending others — `/admin/others/pending`
    - 10.5 Visitors — `/admin/visitors`
    - 10.6 Pending visitors — `/admin/visitors/pending`
    - 10.7 Interests — `/admin/interests`
    - 10.8 Visitor profile types — `/admin/profile-types/visitor`
    - 10.9 Other profile types — `/admin/profile-types/other`
    - 10.10 Reset user 2FA — `/admin/reset-2fa`
    - 10.12 Operation log viewer — `/admin/operation-log` (D-134 Sprint A)
    - 10.11 Logs viewer — `/admin/logs`
11. **Reporting modules**
    - 11.1 Reports hub — `/admin/reports`
    - 11.2 Attendance report — `/admin/reports/attendance`
    - 11.3 Registrations report — `/admin/reports/registrations`
    - 11.4 Gate activity report — `/admin/reports/gates`
    - 11.5 Sessions report — `/admin/reports/sessions`
    - 11.6 Ratings report — `/admin/reports/ratings`
    - 11.7 Partners report — `/admin/reports/partners`
    - 11.8 Meetings report — `/admin/reports/meetings`
    - 11.9 Engagement report — `/admin/reports/engagement`
12. **Account-area surfaces**
    - 12.1 My profile — `/account/profile`
    - 12.2 Notifications inbox — `/account/notifications`
    - 12.3 TOTP pairing — `/account/totp-pairing`
13. **Security boundaries**
14. **Troubleshooting index**
15. **Glossary**

### 1.4 Coverage status

**Every Control Panel module now has a chapter.** The manual covers all 91
modules the Control Panel navigation exposes, across its 14 groups, checked
against `CpNavigation.cs` rather than against a plan document.

Two things are deliberately absent, and neither is an oversight:

- **Live sessions (`/m/live-sessions`)** has no chapter. It is the only route
  `CpNavigation` still declares as a stub, so it renders "Coming soon" and
  there is nothing for an operator to do there yet.
- **Registration requests (`/m/registration-requests`)** had a placeholder in
  earlier revisions of this manual. That route has no page at all and now
  returns the not-found screen, so the placeholder was removed rather than
  filled in: a manual that sends an operator to a dead link is worse than one
  that stays silent.

Each chapter was written against the page's own Razor markup, its code-behind,
the resource strings and the backing service, then re-checked against the same
sources. Where a chapter quotes an on-screen message it is the literal string
the operator sees, which is not always the message the API returns: several
surfaces show a client-side fallback instead.

The contents list in §1 is generated from this document's own headings, so it
cannot drift out of step with the chapters again.

## 2. Sign in + first-time setup

### 2.1 What you need

- Your CP email address (provisioned by another administrator or self-registered
  + approved).
- Your password (sent to you out-of-band by the inviting admin, or set during
  self-registration).
- An authenticator app on your phone (e.g. Google Authenticator, Authy,
  Microsoft Authenticator). SIMF uses TOTP (Time-based One-Time Password) for
  the second factor — your authenticator generates a fresh 6-digit code every
  30 seconds.

### 2.2 First sign-in (TOTP pairing)

1. Open the Control Panel URL (provided by your team — `https://cp.simrsnf.com`
   on the production estate, `http://localhost:5158` for a local run).
2. Enter your email + password → **Sign in**.
3. The browser sends you to `/account/totp-pairing`. The page shows a QR code
   and a manual-entry secret.
4. Open your authenticator app → **Add account** → **Scan QR code** → point
   the camera at the screen. The app starts generating 6-digit codes for SIMF.
5. Type the current 6-digit code into the Verify field on the page → **Pair**.
6. The page shows your 10 single-use **recovery codes**. Save them in a
   password manager or print them and store offline. Each recovery code works
   once and is your only way back in if you lose your phone.
7. The page sends you to the Dashboard.

### 2.3 Subsequent sign-ins

1. Email + password → **Sign in**.
2. The browser sends you to `/login/totp`. Open your authenticator, read the
   current 6-digit code, type it in → **Verify**.
3. You land on the Dashboard or wherever you were trying to go.

### 2.4 Lost your phone

Click **Use a recovery code instead** on the TOTP page. Enter one of your
saved recovery codes → **Verify**. The code burns on use. As soon as you're
in, go to **My profile → Reset 2FA** (or have another administrator do it
via **System → Reset user 2FA**) to re-pair.

### 2.5 Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Invalid email or password" | Typo / Caps Lock / account not yet approved | Re-type carefully; if the account is still PendingApproval the message will say so explicitly |
| "Invalid verification code" | Phone clock drift (>30 s off) OR code expired before you submitted | Open your phone settings → set time automatically; type the next 6-digit code |
| Stuck on `/login/totp` after refresh | Cookie session expired during pairing | Click **Back to sign in**, re-enter email + password |
| Browser says "Account pending approval" | Another admin hasn't approved your account yet | Reach out to the admin team via the channel listed in your invite email |

---

## 3. Daily walkthrough: the Control Panel shell

When you sign in, the shell layout has four regions:

```
┌────────────────────────────────────────────────────────────────────┐
│ ☰ SIMF │ 2026                  العربية  Dark  🔔  You   [Sign out] │ ← header
├────────┬───────────────────────────────────────────────────────────┤
│ Nav    │                                                           │
│ rail   │             Page content (banner + body)                  │
│        │                                                           │
└────────┴───────────────────────────────────────────────────────────┘
```

**Header:**
- **☰** — collapse/expand the left nav.
- **SIMF / 2026** — home link → Dashboard.
- **العربية** — switch language (round-trips the same page in the other locale).
- **Dark / Light** — theme toggle. Persists per user.
- **🔔** — your notification bell. Number badge = unread count. Click → menu
  with the latest notifications + **View all** → `/account/notifications`.
- **You** — links to **My profile** (`/account/profile`).
- **Sign out** — ends the session.

**Left nav rail (9 groups):**
- Each group has a header (Overview, People, Programme, Exhibition, Engagement,
  Knowledge & AI, Content, Communications, System).
- A grey **SOON** tag next to a menu entry means the module is not built yet —
  clicking it shows a "Coming soon" placeholder. _(D-132)_
- The page you're currently on is highlighted with the brand accent.

**Page content:**
- Every page that's not a stub starts with a **branded banner** (page title
  on a sunken-surface strip).
- CRUD list pages render the canonical grid (Select-all toolbar, per-row
  checkboxes, Add / Edit / Details / Delete buttons, full pager).
- Modals overlay the page when you click Add / Edit / Details; the rest of
  the page dims and is inert until you close them.

### 3.1 The canonical CRUD list pattern

You'll meet the same grid pattern on every CRUD page (Admins, Others, Visitors,
Interests, Profile types, etc.). Once you know it, you know all of them:

| Affordance | Where | What it does |
|------------|-------|--------------|
| **Select all** | toolbar | Tick every row on the current page |
| **+ Add** | toolbar | Opens a modal to create a new row |
| **✎ Edit** | per-row | Opens a modal to edit that row |
| **ⓘ Details** | per-row | Opens a read-only modal with every field of that row |
| **🗑 Delete / Deactivate** | per-row | Removes the row (soft-delete in most cases) |
| Per-column **▲▼** | header | Sort the table |
| Per-column **Search** | under header | Filter by that column |
| **« ‹ 1 2 3 › »** | pager | First / Prev / numbered / Next / Last page |
| **Show 10/20/50/100** | pager | Page size |
| **Showing X–Y of Z** | pager | Where you are in the result set |

> **Pattern reference:** [`docs/dev/SIMF_TABLE_PATTERN.md`](../dev/SIMF_TABLE_PATTERN.md)
> is the authoritative spec.

### 3.2 Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Menu item shows SOON tag | Module not built yet (22 of 36 modules as of D-132) | Ask the team for an ETA; check [`PAGE-INDEX.md`](../pages/PAGE-INDEX.md) |
| Page shows "Coming soon" | Same — you clicked a stub | Same |
| "Authentication is required" toast | Cookie / token expired (rare — D-121 handles this) | Refresh the page; if it persists, sign out and back in |
| Browser tab title is wrong | Manual chapter / page doc out of date | File a ticket with the page route |

---

## 3A. Overview modules

The four modules in the left nav's **Overview** group are the ones you watch,
not the ones you edit. None of them creates, changes or deletes anything: every
number on them is counted live from the real data at the moment you open the
page. Two of them (Dashboard, Statistics) answer "how big is the event"; two of
them (Session attendance, Live hall) answer "who is in the room right now".

### 3A.1 Dashboard — `/`

> Page reference: [`docs/pages/cp/dashboard.md`](../pages/cp/dashboard.md)
> · E2E catalogue: [`docs/tests/e2e/cp-dashboard.md`](../tests/e2e/cp-dashboard.md)

The landing page after sign-in, reached again from the **Dashboard** item at the
top of the **Overview** nav group. Every signed-in administrator sees the
welcome panel. The live figures below it, the stat cards
and the day-by-day programme chart, appear only if you hold the **Statistics
View** permission. Each stat card is a link into the module that owns the number,
so the dashboard doubles as a shortcut rail.

#### Most common tasks

##### Read the event at a glance

1. Sign in. You land here. The **Event at a glance** panel holds thirteen cards.
2. Click any card to jump to the module behind it (Visitors goes to
   `/admin/visitors`, Sessions to `/admin/sessions`, Pending approvals to the
   pending-visitors list, and so on).
3. What the headline cards actually count:
   - **Current users** is every account on the system, admins included.
   - **Visitors**, **Staff**, **Moderators** are active profiles, grouped by
     the app role their profile type carries. A profile with no type set yet
     counts as a Visitor.
   - **Pending approvals** is visitor accounts still waiting for someone to
     approve them.
   - **Exhibitors**, **Sponsors**, **Booths**, **Speakers**, **Sessions** are
     the active (not deactivated) rows in those modules.
   - **Total attended** is the number of distinct people who have arrived at
     any hall at any point in the event. Arriving twice counts once.
   - **Total ratings** and **Average rating** come from the ratings module. The
     average is shown to one decimal and reads 0.0 when nobody has rated yet.

##### Read the programme, day by day

1. Scroll to **The programme, day by day**. The bar chart carries three series
   per forum day: **Registered**, **Present** and **Attended**.
2. Below it, one card per forum day repeats the same three figures as bars on a
   shared scale, so day 1 and day 3 are directly comparable, plus the number of
   sessions scheduled that day.
3. What each series means, per day:
   - **Registered** is visitor accounts created on that day.
   - **Present** is distinct people let in through a gate that day.
   - **Attended** is distinct people who arrived at a hall that day.
   - Present and Attended are counted from different records, so one is not a
     subset of the other. Do not read Attended as a slice of Present.
4. Days are Saudi calendar days, not UTC days. An evening arrival is filed
   against the Saudi day the operator lived through, which is what you want.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| You only see the welcome panel, no figures at all | You do not hold the Statistics View permission | Ask an administrator to grant your role Statistics, View |
| "The live figures could not be loaded. Refresh the page to try again." | Both figure reads failed (API unreachable, or the permission was revoked mid-session) | Refresh the page; if it repeats, sign out and back in so a fresh token is issued |
| "No programme days have been set up yet." | No active forum day has been created | Add the forum days under Programme, Programme days |
| A day card shows zeros everywhere | The counters are real, that day simply has no registrations, gate scans or hall arrivals yet | Nothing to fix. Check again once the day starts |
| Sessions on a day card disagrees with the Sessions module | The day card counts only active sessions that START on that Saudi day | Open Sessions and check the start times of the ones you expected |

#### What you cannot do here yet

- Change a date range. The figures are always "right now" and the day cards are
  always every active forum day.
- Export the dashboard. Use the Reports group for anything you need in Excel.

### 3A.2 Session attendance — `/admin/attendance`

> E2E catalogue: [`docs/tests/e2e/cp-admin-attendance.md`](../tests/e2e/cp-admin-attendance.md)

The turnout board. For every active session it shows how many distinct people
arrived at its hall and how many are still inside. The figures are built from
the hall-arrival records, whichever way the arrival was captured: an operator's
QR door scan or the app's automatic geofence arrival. Read-only, so the grid has
no Add, Edit or Delete, only filter, sort and the pager. Needs the **Attendance
View** permission, which the Security team role holds by default.

#### Most common tasks

##### See how the event is doing right now

1. **Overview → Session attendance**. Three cards sit above the grid:
   - **Live attendees now** is the number of distinct people currently inside
     any hall, that is, people with an arrival and no departure recorded.
   - **Sessions with attendance** is how many active sessions have had at least
     one arrival.
   - **Total arrivals** is the sum, across active sessions, of each session's
     distinct attendees.

##### Check one session's turnout

1. Type the session code into the **Search** box under the **Code** column
   heading, or part of the title under **Session**. The grid refilters as you
   type, after a short pause.
2. Read the row:
   - **Hall** is the room, **Start (Saudi time)** the scheduled start.
   - **Total attendees** is distinct people who arrived at that session's hall.
     Someone who stepped out and came back counts once.
   - **Live now** is how many of them are still inside. A number above zero is
     highlighted so you can spot the running rooms at a glance.
3. Sort by clicking the **Code**, **Session** or **Start (Saudi time)** headers.
4. The pager at the bottom moves through the result set and lets you show 10, 20,
   50 or 100 rows. The default is 20 rows sorted by start time.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Could not load attendance. Please try again." | The API was unreachable, or it refused the call | Refresh. If it persists, check with an administrator that your role still holds Attendance, View |
| "You do not have permission to open this page. If you believe this is a mistake, contact your administrator." | Your role does not hold the Attendance View permission, so the page never opens and you land on the Not permitted page instead | Ask an administrator to grant Attendance, View |
| "You do not have permission to perform this action." | Narrower case: the page had already opened and the Attendance View permission was taken away during the session, so the API refuses the next read | Sign out and back in. If the message returns, ask an administrator to grant Attendance, View |
| "No attendance has been recorded yet." | The grid lists sessions, not arrivals, so this only appears when no active session matches your filter. If nobody has arrived, every active session still lists with zeros | Clear the Code and Session filters and look again; if it stays empty, check that active sessions exist under Programme, Sessions |
| A session you expected is missing | Only active sessions are listed. A deactivated session drops out | Reactivate it under Programme, Sessions if it should be running |
| Live now stays above zero long after the session ended | Attendees never scanned out | A background job closes stale arrivals once the session's end time has passed and runs every minute, so the count falls on its own shortly after the end time |
| Clicking Start (Saudi time) a second time does not reverse the order | The grid always returns start-time order ascending | Sort by Code or Session instead, or filter down to the sessions you care about, and report it to the team |

#### What you cannot do here yet

- See WHO attended. This page counts people, it does not name them. For names,
  use **Live hall** (people inside right now) or **Reports → Attendance report**.
- Export the grid. There is no Export button on this page.

### 3A.3 Live hall — `/admin/sessions/live-hall`

> E2E catalogue: [`docs/tests/e2e/cp-admin-session-live-hall.md`](../tests/e2e/cp-admin-session-live-hall.md)

The door-side view of one room. Pick a session and you get its seat map in four
colours plus a named list of everyone currently inside the hall, with their
organisation, job title, seat and the time they entered. It is the page to have
open on a screen at the back of the room. It refreshes itself every 15 seconds
while a session is selected, so a scan at the door appears without anyone
touching the keyboard. Needs the **Attendance View** permission, and nothing
else: the session picker reads the attendance session list, which carries the
same permission. It used to need **Sessions View** as well, which meant the
security team could open the page and then find an empty picker.

#### Most common tasks

##### Watch a room fill up

1. **Overview → Live hall**.
2. Open **Select a session**. Each option shows the session code followed by its
   title. Only active sessions are listed.
3. Pick the session. The page loads the seat map and the present list, then keeps
   both current on its own. Click **Refresh** if you want it immediately.
4. **Seat map** colours, explained in the legend under the grid:
   - **Available**: nobody holds this seat.
   - **Unavailable**: an administrator blocked the row or seat, so no attendee
     can take it.
   - **Reserved**: someone booked it but has not been checked in yet.
   - **Confirmed (checked in)**: the holder is checked in.
   Hover any seat to see its row and seat number with its state.
5. **In the hall now** lists each person present, oldest arrival first:
   - **Name**, **Organisation**, **Type** (their profile type) and **Job title**
     come from the person's profile. Name shows the English name, or the Arabic
     name when the English one is blank.
   - **Seat** is their row and seat number, or **General admission** when the
     session is open seating or they hold no reservation.
   - **Entered (Saudi time)** is when the arrival was recorded.
   - **Method** is **QR scan** for an operator door scan, **Geofence** for an
     automatic arrival detected by the app.
6. The line under the table gives the head count currently in the room.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "No sessions available." | There are no active sessions, or the session list was refused because your role lacks Sessions, View | Check under Programme, Sessions that active sessions exist; otherwise ask for the Sessions, View permission |
| "Could not load the live hall. Please try again." | The seat map or the present list call failed | Click Refresh. The page retries on its own every 15 seconds, so a passing network glitch clears itself |
| "This hall has no seat layout, so there is no seat map to show." | The hall has no seat rows defined | Define the layout under Programme, Hall seat layouts. The present list still works without one |
| An active session is missing from the picker | The picker loads the first 200 active sessions only | Work from Session attendance for the wider list, and raise the 200 limit with the team if the programme has outgrown it |
| "No one is inside the hall yet." | Nobody has been scanned in or detected arriving | Nothing to fix. Confirm the door operator is scanning |
| A person is on the list with no organisation or job title | Their profile has those fields empty, or they are an admin account with no attendee profile | Complete the profile under People if the person should carry one |
| Someone left but is still listed | Their departure was never recorded | They drop off automatically once the session's end time passes and the closeout job runs |

#### What you cannot do here yet

- Check someone in or out from this page. It only shows what the doors recorded.
- Assign, move or release a seat. Seating is managed from the booking and seat
  modules.
- Export the present list.

### 3A.4 Statistics — `/admin/statistics`

> E2E catalogue: [`docs/tests/e2e/cp-admin-statistics.md`](../tests/e2e/cp-admin-statistics.md)

The standing count of everything the event holds: people, content and feedback.
Eleven cards, each one a live count taken when you open the page, and each one a
link into the module that owns it. Where the Dashboard adds the day-by-day
programme view, this page is the flat, single-screen answer to "how many of X do
we have". Needs the **Statistics View** permission, the same one that reveals the
figures on the Dashboard.

#### Most common tasks

##### Read the counts

1. **Overview → Statistics**.
2. What each card counts:
   - **Total attendees**: every non-admin account on the system, in any state.
   - **Approved attendees**: those of them approved.
   - **Pending approvals**: those still waiting for a decision. Click through to
     the pending-visitors list to act on them.
   - **Sessions**, **Speakers**, **Booths**, **Sponsors**, **News articles**,
     **Media items**: active (not deactivated) rows in each module.
   - **Total ratings**: active rating responses submitted by attendees.
   - **Average rating**: the mean overall score across those responses, to one
     decimal. It reads 0.0 when nobody has given an overall score yet.
3. Click any card to open the module behind it.

##### Reconcile a count with a module list

1. Click the card. You land on that module's list page.
2. If the list total looks smaller than the card, check whether the list is
   filtered. If it looks bigger, check whether it is showing deactivated rows:
   the cards count active rows only.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Could not load statistics. Please try again." | The API was unreachable, or it refused the call | Refresh the page. If it repeats, sign out and back in |
| "You do not have permission to open this page. If you believe this is a mistake, contact your administrator." | Your role does not hold the Statistics View permission, so the page never opens and you land on the Not permitted page instead | Ask an administrator to grant Statistics, View |
| The Statistics entry is missing from the Overview group | Same cause. The nav hides what you cannot open | Same fix |
| "No statistics are available yet." | The page loaded but got nothing back | Refresh. If it persists, raise it with the team, this is not a normal state on a running system |
| It sits on "Loading statistics…" | The call has not come back yet | Wait a moment, then refresh. Every card is counted from live data, so a very large database takes a little longer |
| A card disagrees with a report | The cards are counted at the instant you opened the page and count active rows only; a report may cover a date range or include deactivated rows | Compare like for like, or use the Reports group when you need a defined period |

#### What you cannot do here yet

- Filter by date, day or hall. The cards are always the current whole-event total.
- Export. Use **Reports** for anything that has to leave the Control Panel.

---

## 4. People modules

### 4.1 Attendees — `/admin/attendees` (D-134 Sprint A)

> Page reference: [`docs/pages/cp/admin-attendees.md`](../pages/cp/admin-attendees.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-attendees.md`](../tests/e2e/cp-admin-attendees.md)

Combined **read-only roster** of every event attendee — Visitors and
Others in one place. Admins are excluded. This is the fastest answer to
"is X registered?" and the eyes-on view for counting heads.

#### Most common tasks

- **Is this person registered?** — type their email or display name into
  the search field → Apply.
- **How many Approved Visitors do we have?** — Kind=Visitors only +
  State=Approved → Apply → the pager total is your count.
- **Find profiles with no profile-type set yet** — search by email, the
  ProfileType column shows "—" for incomplete registrations.

#### What you cannot do here

- Edit a row — go to `/admin/visitors` or `/admin/others`.
- Approve / Reject — those happen on the matching Pending pages.
- Export to XLSX — coming in a follow-up.

### 4.2 Print badge desk — `/admin/print-bag`

> Page reference: [`docs/pages/cp/admin-print-bag.md`](../pages/cp/admin-print-bag.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-print-bag.md`](../tests/e2e/cp-admin-print-bag.md)

#### What it's for

The print desk reprints visitor badges by **QR id**. Visitors who lost or
damaged their badge come here; you look them up by QR id (scan or type),
the page renders the same colour-coded badge they had originally, and you
click Print.

#### Most common tasks

##### Reprint a badge

1. **People → Print badge** in the left nav (or paste `/admin/print-bag`).
2. Plug in your USB barcode scanner OR be ready to type the 12-character
   QR id.
3. Place the QR (or type it) into the **QR id** input.
4. Click **Search** (or press Enter).
5. The badge renders with the visitor's profile-type colour, name, QR SVG,
   and QR id.
6. Click **Print**. The browser print dialog opens with only the badge
   visible (the page header, nav, and toolbar are hidden by the print CSS).
7. Click **Reset** to clear the form for the next visitor.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Visitor not found" | QR id typo OR visitor was deleted | Re-scan; if the QR is damaged, look the visitor up by email on `/admin/visitors` and read their QR id from the Details modal |
| Print dialog shows the whole page instead of just the badge | Browser blocked the print stylesheet | Use Chrome or Edge; check **Print preview → More settings → Background graphics** is on |
| QR id input doesn't accept input | Browser autocomplete grabbed focus | Click directly in the input; the input has `autocomplete="off"` but some scanners send a leading tab |

#### What you cannot do here

- **Bulk-reprint** — one visitor at a time.
- **Edit the visitor while you're here** — go to `/admin/visitors` → find
  → Edit (when User Management ships).
- **Re-issue a different QR id** — the QR is minted at registration and is
  permanent. If a QR is compromised, the visitor must be re-registered.

#### Cross-references

- Page reference: [`docs/pages/cp/admin-print-bag.md`](../pages/cp/admin-print-bag.md)
- Walk-in registration (where the QR is minted): [`docs/pages/cp/admin-visitors.md`](../pages/cp/admin-visitors.md)
- Decision: D-130.

### 4.3 Roles & permissions — `/admin/roles` (D-134 Sprint A)

> Page reference: [`docs/pages/cp/admin-roles.md`](../pages/cp/admin-roles.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-roles.md`](../tests/e2e/cp-admin-roles.md)

#### What it's for

Roles are how SIMF expresses **who-can-do-what**. The system ships a small
set of **built-in (baseline) roles** that always exist — most importantly
`Administrator`. You use this page to **add your own custom roles** (e.g.
`Scientific Committee`, `Press`, `Security`), **rename** them, and
**delete** ones you no longer need.

#### Most common tasks

##### Create a custom role

1. **People → Roles & permissions** → **+ Add role**.
2. Type a name (1–64 characters; must be unique across all roles).
3. Click **Create role**.
4. The grid shows your new row with the **Custom** pill, 0 users, 0
   permissions.

##### Rename a custom role

1. Click the **Edit** icon on a custom row.
2. Change the name → **Save changes**.
3. Baseline rows can't be renamed — the Edit modal shows a notice
   instead of the form.

##### Delete a custom role

1. Click the **Delete** (trash) icon on a custom row.
2. The server refuses if any user still holds the role (you'll see the
   exact user count in the bilingual error toast). Unassign those users
   first (UI for that ships in a follow-up).
3. Baseline roles are protected — Delete on those returns the bilingual
   "Baseline roles cannot be deleted" toast.

##### See the users + permissions in a role

Click the **Details** icon — the modal shows Name, Type (Built-in /
Custom), Users count, Permissions count. The per-permission grant
editor and the assign-to-user surface ship in a follow-up commit; for
now this page is about the role itself.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Toast: "A role named '…' already exists" | Duplicate name | Pick a different name; role names are case-insensitive unique |
| Toast: "The role cannot be deleted while N user(s) hold it" | Role still assigned | Unassign users from the role (UI in follow-up) or rename rather than delete |
| Toast: "Baseline roles cannot be renamed/deleted" | Acting on a built-in role | Built-in roles are protected by design; choose a custom row instead |
| Modal saves but the row doesn't update | Auto-reload race | Refresh the page; if it persists, check the API log |

#### What you cannot do here yet

- **Edit per-permission grants** (which pages + actions a role can do).
  Coming in a follow-up.
- **Assign / unassign users to a role.** Coming in a follow-up — until
  then, the User Management module assigns the `Administrator` role at
  invite time only.
- **Bulk delete** — one role at a time.

#### Cross-references

- Page reference: [`docs/pages/cp/admin-roles.md`](../pages/cp/admin-roles.md)
- Authority spec: SIMF-RPM-001 §8 (page-and-action model).
- Decisions: D-134 plan + Sprint A commit.

---

## 4A. People modules (remaining)

### 4A.1 VIP registration — `/admin/visitors/vip`

> Page reference: [`docs/pages/cp/vip-registration.md`](../pages/cp/vip-registration.md)
> · E2E catalogue: [`docs/tests/e2e/cp-vip-registration.md`](../tests/e2e/cp-vip-registration.md)

The dedicated desk for VVIP and VIP guests. It is the ordinary visitor list narrowed
to the VIP tiers, plus a registration wizard that captures the extra Mawj (موج)
welcome data the technical teams use to greet the guest. Everything you enter here
feeds the VIP welcome export (§4A.2). Needs the Visitors → Register on-site
permission to open; the per-row pencil also needs Visitors → Edit.

#### Most common tasks

##### Register a VVIP / VIP

1. **People → VIP registration** → **New VIP**. The grid is replaced by the
   **Register a VVIP / VIP** wizard.
2. **Badge type** — only the **VVIP** and **VIP** tiers are offered here. If only one
   of them is seeded and active it is already selected for you.
3. **VIP details (Mawj)** — all optional: **Mawj system ID** (up to 64 characters),
   **Honorific / title** and **Honorific / title in Arabic** (up to 64 each, both
   stored), **Preferred language** (Arabic or English, blank means not specified).
4. **Identity** — **Name on badge** (up to 128, this is what prints), **Date of
   birth**, **Full name (English)** and **Full name (Arabic)** (up to 128 each, both
   stored), **Job title** (up to 128) and **Job title in Arabic** (up to 100), **Place
   of birth** (a Saudi guest picks one of the 13 regions, a non-Saudi types it as in
   the passport), **Gender**, and the optional **Plate number** (three letter pickers
   plus 1 to 4 digits).
5. **Organisation** — required. Type to search, then click a result. The helper line
   changes to "Selected: …" once it takes.
6. **Nationality and ID** — the **Saudi / Non-Saudi** toggle. A Saudi guest supplies
   the **Saudi national ID** (10 digits starting with 1). A non-Saudi picks the
   country, then **Iqama** (10 digits starting with 2) or **Passport** (up to 20
   characters).
7. **Contact** — a mobile number is required here. The field follows the toggle in the
   previous section: a Saudi guest gets **Saudi mobile**, a non-Saudi gets
   **International mobile** (up to 32 characters). **Email (optional)** (up to 256)
   may be left blank, and then the QR badge is the guest's only access key.
8. **ID document** — optional images: **ID document image** (up to 5 MB), **Profile
   photo** (up to 2 MB) and **VIP welcome photo** (up to 2 MB, this page only). PNG,
   JPEG or WebP.
9. **Interests** — up to 10 topic chips.
10. **Register**. The confirmation reads **"Registered, pending approval"**. There is
    no QR and no Print button yet: the badge is minted when the account is approved on
    **People → Pending visitors**.

##### Edit a VIP

Click the **Edit** pencil on the row. The **Edit VIP** modal carries Email, Display
name, Profile type, Nationality, Saudi mobile, International mobile, the two
Bi-Meeting access checkboxes, and a **Photo & ID** section with Profile photo, ID
document and **VIP welcome photo**. Leave an image empty to keep the current one.
**Save changes** shows "VIP updated." Changing the email signs that account out of
all its sessions.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Toast: "You do not have permission to perform this action." on opening the page | The grid reads the shared VIP list, which needs the VIPs → View permission; the page itself only asks for Visitors → Register on-site, so it opens and the list call is refused | Ask for the VIPs → View permission |
| Toast: "The VIP list could not be loaded." | The list call came back with nothing to report, so the page fell back to its own wording. Usually the API is unreachable | Check the API is up, then reload the page |
| "No active profile types are defined yet. Configure them under Lookups → Profile types first." | Neither VVIP nor VIP is seeded and active. The message names a menu that no longer exists | Add or reactivate the tier under Reference data → Visitor profile types |
| No **Edit** pencil on any row | The account lacks the Visitors → Edit permission | Ask for Visitors → Edit; VIP registration alone only allows adding |
| The **Edit** button in the toolbar stays greyed out | This grid has no row checkboxes, so the toolbar Edit never enables | Use the per-row pencil, or right-click the row |
| "Pick an organisation." | The organisation typeahead was typed in but no result was clicked | Click an entry in the dropdown so "Selected: …" appears |
| "A mobile number is required." | The Contact section was left empty | Type the guest's number in the mobile field the Contact section shows |
| "Saudi national ID must be 10 digits and start with 1." | Wrong length or leading digit | Re-key from the ID card |
| "The Iqama number is 10 digits starting with 2." | Wrong length or leading digit | Re-key, or switch the toggle to Passport |
| Registered, but no QR and no **Print badge** button | Walk-in accounts are created pending approval | Approve the guest on People → Pending visitors, then reprint from People → Print badge |

#### What you cannot do here yet

- **Search or sort the grid.** There is no search box and no sortable header on this
  list; page through it, or use `/admin/visitors` for a searchable view.
- **Delete a VIP.** Use the Visitors module.
- **Register a Gold guest.** The wizard only offers VVIP and VIP, although Gold rows
  do appear in the grid.

### 4A.2 VIP welcome export (Mawj) — `/admin/visitors/vip/export`

> Page reference: [`docs/pages/cp/vip-export.md`](../pages/cp/vip-export.md)
> · E2E catalogue: [`docs/tests/e2e/cp-vip-export.md`](../tests/e2e/cp-vip-export.md)

The read-only roster you hand to the Mawj (موج) welcome team. It lists every **VVIP**
and **VIP** visitor with the welcome-message fields captured in §4A.1. On screen the
grid opens sorted by tier name in A-to-Z order, which puts the VIP rows before the
VVIP rows; the CSV, Excel and JSON feeds are ordered the other way round, VVIP rows
first and then alphabetical by English name. Because it carries VIP personal data for
external sharing it has its own permission: Visitors → Export VIP roster. Without it
the menu entry does not appear and the page will not open.

#### Most common tasks

##### Read the roster on screen

1. **People → VIP welcome export**.
2. Columns, left to right: **Photo** (thumbnail, click it to open or download the
   full image), the tier pill, the guest's name, **Honorific / title (optional)**,
   **Job title (optional)**, **Mawj system ID (optional)**, **Preferred language
   (optional)**, **Email (optional)**, **Saudi mobile**, and **Reference number**. The
   headers are the walk-in form's own field labels, so five of them carry an
   "(optional)" suffix. A dash means the field was never filled in.
3. The tier and name headers sort. Only the tier column has a search box under it.
4. The tier column's header repeats the page title; the cells in it hold the VVIP or
   VIP pill.

##### Send the roster to the welcome team

1. **Download CSV** or **Download Excel** in the toolbar. Both download the whole
   roster, not just the page you are looking at.
2. The file has more columns than the screen: it adds Display name, Job title
   (Arabic), Arabic name, account State, whether a welcome photo exists, and the
   registration timestamp.
3. **API (JSON)** opens the same roster as raw JSON in a new tab, for a team that
   wants to consume it directly.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "No VVIP or VIP visitors have been registered yet." | Nobody holds a VVIP or VIP tier | Register them on People → VIP registration, or change an existing visitor's profile type |
| A guest you registered is missing | The roster covers VVIP and VIP only, Gold is excluded | Change the guest's tier to VVIP or VIP |
| The Photo cell shows a blank placeholder | No **VIP welcome photo** was uploaded for that guest | Open People → VIP registration, Edit the guest, upload under Photo & ID |
| The page will not open, or the menu entry is absent | The account lacks the Visitors → Export VIP roster permission | Ask for that permission; the ordinary Visitors permissions do not cover it |
| A download starts but the file is empty of a guest you expect | The roster reads live data at click time | Confirm the guest's profile type first, then download again |

#### What you cannot do here yet

- **Edit anything.** This page is read-only by design; corrections are made on
  §4A.1 or the Visitors module.
- **Filter the download.** CSV and Excel always contain the full roster.

### 4A.3 Delegates — `/admin/delegates`

> E2E catalogue: [`docs/tests/e2e/cp-admin-delegates.md`](../tests/e2e/cp-admin-delegates.md)

The desk for delegation (وفد) members. A delegate is an ordinary visitor carrying a
delegation flag, and their nationality must be a country marked as invited to send a
delegation. The page has two stacked sections: a single-person registration form, and
a bulk badge generator that mints placeholder badges by tier and count. Every batch
you generate here is listed afterwards on Badge batches (§4A.4). Needs the Visitors →
Register on-site permission; the bulk section additionally needs Visitors →
Bulk-generate badges and is hidden without it.

#### Most common tasks

##### Register one delegate

1. **People → Delegates** → the **Register a delegate** section.
2. Fill the same walk-in form the visitor desk uses: **Badge type**, **Identity**
   (badge name, date of birth, full name in English and Arabic, job title in both,
   place of birth, gender, plate number), **Organisation** (required),
   **Nationality and ID**, **Contact**, **ID document** images, and **Interests**.
3. The nationality you pick must be an invited country. Set that flag with the
   **Invited to send a delegation (وفد)** checkbox on the Countries lookup
   (`/admin/countries`).
4. **Register**. The confirmation reads **"Registered, pending approval"**: the QR
   badge is minted when the delegate is approved on **People → Pending visitors**.
   **Register another** clears the form for the next person in the queue.

##### Bulk-generate a set of badges

1. Scroll to **Bulk-generate badges**.
2. Pick a **Profile type**, type a **Count** (up to 4 digits), press **Add**. Repeat
   for each tier. Adding a tier twice merges into one line. The running **Total** is
   shown under the list; the small cross on a line removes it.
3. **Flag the generated badges as delegates** is ticked for you on this page. Untick
   it to produce plain visitor badges.
4. **Generate badges** opens a confirmation showing the batch summary and its total,
   for example "VIP × 5 + Normal × 3 → 8 badge(s)", and a field labelled **Organiser
   email (optional)**. Leave the email blank to skip sending.
5. **Generate badges** again in the dialog. You get "N badge(s) generated." or
   "N badge(s) generated and emailed to …".
6. Unlike a single registration, bulk badges are approved immediately and already
   carry a QR. When an organiser email is supplied they are sent as a ZIP of QR
   images plus a printable contact-sheet PDF.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "A delegate's nationality must be a country invited to send a delegation." | The picked country is not flagged as invited. A Saudi delegate also fails if Saudi Arabia is not flagged | Tick **Invited to send a delegation (وفد)** on that country under `/admin/countries`, then register again |
| "Pick an organisation." | Nothing was chosen from the organisation typeahead | Click a result so "Selected: …" appears |
| "Choose a profile type and a count above zero." | **Add** was pressed with no tier picked, or a count of 0 or non-numeric | Pick a tier and type a positive whole number |
| "At most 1000 badges can be generated per request." | The batch total exceeds the safety cap | Split it into several runs of 1000 or fewer |
| "Bulk-generate is only available for audience (visitor) profile types." | A partner or staff tier was smuggled into the request | Use an audience tier; only those appear in the picker |
| "The organiser email address is not valid." | Typo, or a domain with no dot | Correct the address. Nothing is generated and nothing is sent when this appears |
| "No active profile types to generate badges for." | No audience tier is seeded and active | Add one under Reference data → Visitor profile types |
| The **Bulk-generate badges** section is missing | The account lacks the Visitors → Bulk-generate badges permission | Ask for that permission |

#### What you cannot do here yet

- **List or search existing delegates here.** This page only registers them; browse
  them on `/admin/visitors`.
- **Print from the single registration.** There is no badge until the delegate is
  approved.
- **Undo a bulk run from this page.** Use **Revoke batch** on Badge batches (§4A.4).

### 4A.4 Badge batches — `/admin/visitors/badge-batches`

> Page reference: [`docs/pages/cp/admin-badge-batches.md`](../pages/cp/admin-badge-batches.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-badge-batches.md`](../tests/e2e/cp-admin-badge-batches.md)

The history of every bulk badge run made from the Delegates desk (§4A.3) or the
Visitors list, newest first. It exists so a generated set stays manageable after the
fact: you can send the QR pack again to a different organiser, or revoke the whole set
if the badges went astray. Seeing the list needs Visitors → View badge batches; the
two row actions need Visitors → Manage badge batches, so a read-only viewer sees rows
with no buttons.

#### Most common tasks

##### Read the list

**People → Badge batches**. Columns: **Contents** (the summary, for example
"VIP × 5 + Normal × 3"), **Total**, **Delegation** (a "Delegates" pill, or a dash for
a plain visitor batch), **Emailed to** (the last organiser address used), **Generated**
(Saudi local date and 12-hour time), and **Status** (Active or Revoked). A revoked
batch keeps its row so the history stays complete.

##### Re-email a QR pack

1. Click the **Re-email QR pack** (envelope) icon on an active row.
2. The dialog repeats the batch **Contents** and **Total**, and pre-fills
   **Organiser email** with the address last used. Change it if the pack is going to
   somebody new.
3. **Send**. You get "Emailed N badge(s) to …". The badges themselves are unchanged,
   only a fresh copy of the pack is sent, and the row's **Emailed to** updates.

##### Revoke a batch

1. Click the **Revoke batch** (power) icon on an active row.
2. Read the confirmation: "This disables all N account(s) in this batch (…). This
   cannot be undone."
3. **Revoke**. You get "Revoked N account(s)." Every account the batch minted is
   disabled and its QR stops working at the gate; the row flips to **Revoked** and
   loses both buttons.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "No badge batches yet — generate a set from the Delegates or Visitors desk." | Nothing has been bulk-generated yet | Generate a set on People → Delegates |
| "Could not load the badge batches." | The list call failed, usually a missing Visitors → View badge batches permission or an API outage | Ask for the permission; if you hold it, check the API is up |
| No envelope or power icons on any row | The account lacks Visitors → Manage badge batches, or the rows are already revoked | Ask for that permission; a revoked batch has no actions by design |
| "The organiser email address is not valid." | Typo, over 256 characters, or a domain with no dot | Correct the address. Nothing is sent when this appears |
| "This batch has no badges to email." | No visitor record carrying a QR is still linked to this batch, so there is nothing to render. Revoking a batch does not cause this: revoking disables the accounts but leaves their records and QR codes in place | Generate a fresh batch |
| "The badge batch was not found or is already revoked." | Somebody revoked it in another tab while your dialog was open | Refresh the page and re-read the Status column |
| "The badge batch was not found." | The batch row was removed under you | Refresh the page |

#### What you cannot do here yet

- **Reinstate a revoked batch.** Revoking is permanent; generate a new batch instead.
- **Open the individual badges from here.** The row shows counts only, not the people
  it minted.
- **Search, sort or filter.** The list is paged newest-first with no controls.

---

## 5. Programme modules (D-134 Sprint B / D-135)

### 5.1 Themes & pillars — `/admin/themes`

> Page reference: [`docs/pages/cp/admin-themes.md`](../pages/cp/admin-themes.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-themes.md`](../tests/e2e/cp-admin-themes.md)

Programme themes (a.k.a. pillars) are the top-level grouping the agenda
uses. Sessions belong to a theme; the visitor agenda groups by theme
heading. Each theme has a stable **Code** (e.g. "DEF", "TECH" — your
choice), bilingual Name + Description, a sort key, and an accent colour
that drives the agenda chip.

#### Most common tasks

##### Add a new theme

1. **Programme → Themes & pillars** → **+ Add theme**.
2. Fill:
   - **Code** — 2–16 chars, your stable identifier; uppercased on save.
   - **Name (English)** + **Name (Arabic)** — 1–128 chars each.
   - **Description (English)** + **Description (Arabic)** — optional.
   - **Display order** — integer ≥ 0; lower numbers come first.
   - **Page color** — hex (e.g. `#244A77`) or a CSS variable.
3. **Create theme**.

##### Edit a theme

Per-row **Edit** icon → adjust → **Save changes**. The Edit modal adds
an **Active** checkbox; untick to deactivate without using the Delete
button.

##### Deactivate a theme

Per-row **Deactivate** (trash) icon. Soft-delete only — existing
sessions linked to the theme keep their link; the theme stops appearing
in the visitor agenda picker on next load.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "A theme with code 'X' already exists" | Code is case-insensitive unique | Pick a different code |
| Save rejected — "Code must be between 2 and 16 characters" | Code too short / long | Adjust |
| Color shows literal text in the grid | PageColor isn't a valid hex / CSS value | Open Edit and fix |

#### What you cannot do here yet

- Reorder by drag — use the Display order field.
- Permanently delete — Deactivate is soft-only.
- Sessions in-use guard — wired when the Sessions module ships in a
  later Sprint B commit.

#### Cross-references

- Page reference: [`docs/pages/cp/admin-themes.md`](../pages/cp/admin-themes.md)
- Authority spec: SIMF-FDS-004 §5.1.
- Decisions: D-134 plan + D-135 freeze-lift + this commit.

### 5.2 Halls & seating — `/admin/halls` (D-134 Sprint B / D-135)

> Page reference: [`docs/pages/cp/admin-halls.md`](../pages/cp/admin-halls.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-halls.md`](../tests/e2e/cp-admin-halls.md)

Halls (rooms) host sessions. Define each hall once — code, bilingual
name, capacity, optional floor + equipment notes — and the Sessions
module's hall picker reads from this list.

#### Most common tasks

##### Add a hall

1. **Programme → Halls & seating** → **+ Add hall**.
2. **Code** (2–16 chars; venue team's identifier; uppercased on save).
3. **Name (English)** + **Name (Arabic)**.
4. **Capacity** — seating + standing total. Drives the Sessions booking
   cap. `0` is allowed for placeholder halls.
5. **Floor** (optional, e.g. "Ground", "Level 2").
6. **Equipment + accessibility notes** (optional, ≤ 1024 chars).
7. **Create hall**.

##### Edit / Deactivate

Standard per-row Edit (with Active checkbox) and Deactivate (soft-delete).

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "A hall with code 'X' already exists" | Codes are unique (case-insensitive) | Pick a different code |
| Capacity rejected | Negative number | Use 0 or positive |
| Won't deactivate | (Future) hall referenced by an active session | Reassign or cancel the session first |

#### What you cannot do here yet

- Floor-plan view — see Venue map module (later in Sprint C).
- Sessions in-use guard — wired when Sessions ships in this sprint.

### 5.3 Speakers — `/admin/speakers`

> Page reference: [`docs/pages/cp/admin-speakers.md`](../pages/cp/admin-speakers.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-speakers.md`](../tests/e2e/cp-admin-speakers.md)

The speaker directory. Every speaker here appears in the **Add speaker** picker
on the Session form (§5.4), on the public speaker list, and behind the app's
speaker profile. The grid shows the speaker's photo, both names and rank in one
cell, their country, code, order and status.

#### Most common tasks

##### Add a speaker

1. **Programme → Speakers** → **Add speaker**. The form runs in five numbered
   steps.
2. **Identity** — **Code** (2–16 characters, unique, uppercased on save, e.g.
   `SPK-001`), **Rank / title** and **Rank / title (Arabic)** (≤ 256 each),
   **Name (English)** and **Name (Arabic)** (both required, ≤ 128 each),
   **Country** (from the country lookup; `— No country —` leaves it unset), and
   **Display order** (0 or higher).
3. **Biography & credentials** — Bio (≤ 2048), Qualifications, Training &
   experience and Awards (≤ 1024 each), every one paired English and Arabic.
   All optional; both languages are stored.
4. **Links & contact** — Website, LinkedIn, X, Facebook and Instagram URLs
   (≤ 256 each).
5. **Contact information** — Email (≤ 320), two phone numbers (≤ 32 each),
   City and City (Arabic) (≤ 128 each), Latitude and Longitude.
6. **Visibility & consent** — **Allow meeting requests** and **Allow data
   sharing** tickboxes.
7. **Create speaker** → *"Speaker "X" was created."*

##### Add the photo

The photo control only appears when editing. On a new speaker the form says
*"Save the speaker first to add a photo."* Save, then re-open with **Edit** and
use the upload control at the top of step 1. A speaker with no photo shows an
initials tile in the grid, never a broken image.

##### Jump to a speaker's sessions

The per-row **Sessions** (calendar) icon opens the Sessions grid filtered to
that speaker.

##### Deactivate a speaker

Per-row **Deactivate** (trash) icon → the confirmation reads *"Deactivate the
speaker "X"? They will be hidden from the public speaker list and the Session
editor picker. You can reactivate them later by editing."* Sessions that already
list the speaker keep the link.

#### Field validation reference

| Field | Rule (rejected otherwise) |
|-------|---------------------------|
| Code | 2–16 characters; unique, case-insensitive |
| Name (English) | required; ≤ 128 characters |
| Name (Arabic) | required; ≤ 128 characters |
| Display order | 0 or a positive whole number |
| Country | must be an entry chosen from the picker |

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Code must be between 2 and 16 characters." | Code too short or too long | Adjust |
| "English name is required (1–128 characters)." | Blank or over-long English name | Fill it in |
| "Arabic name is required (1–128 characters)." | Blank or over-long Arabic name | Both languages are required |
| "Display order must be zero or a positive integer." | A negative number or text | Enter 0 or more |
| "A speaker with code 'X' already exists." | Codes are unique, case ignored | Pick a different code |
| "The speakers could not be loaded." | The list call failed | Reload; if it persists, report it with the time |
| Country dropdown is empty | The country lookup did not load | You can still save with no country; reload to retry |
| The menu row is missing | You do not hold **Speakers → View** | Ask an administrator |

### 5.4 Sessions — `/admin/sessions`

> Page reference: [`docs/pages/cp/admin-sessions.md`](../pages/cp/admin-sessions.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-sessions.md`](../tests/e2e/cp-admin-sessions.md)

A **session** is one scheduled run-of-show talk: a time window in a
specific **Hall** (§5.2), presented by one or more **Speakers** (§5.3),
optionally tagged with **Themes** (§5.1) and a **Category**. Sessions are
what the mobile-app agenda, the seat-booking flow, and the **live
broadcast** player all read from. The list follows the canonical CRUD
pattern (§3.1): filter, select-all, per-row **Details / Edit / Deactivate**,
plus **Add session**, **Import**, and **Export**.

#### Most common tasks

##### Add a session

1. **Programme → Sessions** → **Add session**.
2. Fill the form (required fields marked ✱):
   - **Code** ✱ — 2–16 chars, unique; uppercased on save. Used in the
     public session URL (e.g. `SES-001`).
   - **Title (English)** ✱ + **Title (Arabic)** ✱ — ≤ 256 chars each.
   - **Description (English / Arabic)** — optional, ≤ 2048 each.
   - **Live stream URL (live broadcast)** — see *Set the live broadcast
     URL* below. Leave blank for a non-live session.
   - **Sign-language stream URL** — optional alternate feed (same
     accepted formats); shows the sign-language toggle on the player.
   - **AI live captions (English / Arabic)** — optional caption /
     running-transcript text shown under the player; blank falls back to
     the player's own captions.
   - **Hall** ✱ — pick from the Halls list (§5.2).
   - **Category** — optional (from Session categories); `— No category —`
     leaves it unset.
   - **Type** — `Workshop` / `Session` / `Event`, driving the app's
     agenda type tabs; `— No type —` leaves it untyped.
   - **Seat selection (override)** — `— Inherit from hall —` (default),
     `Assigned seat`, or `Open seating (general admission)`.
   - **Start (Saudi time)** ✱ + **End (Saudi time)** ✱ — **enter Saudi local time**.
     End must be after Start.
   - **Capacity override** — blank inherits the hall's seat count; set a
     value only when the room is reconfigured for this session.
   - **Add speaker** — pick a speaker to append; reorder with **Up / Down**
     (row 1 is the primary speaker), set each one's **Role** (Speaker /
     Host), or **Remove**.
   - **Add theme** — append one or more themes; **Remove** to drop one.
3. **Create session**.

##### Edit a session

Per-row **Edit** (pencil) icon → the **Edit session** dialog opens
pre-filled with every current value → adjust → **Save changes**. The
dialog adds an **Active — show in the public agenda** checkbox (untick to
hide the session from the agenda without deleting it). A green
*"Session '…' was updated."* banner confirms the save.

> The Edit dialog round-trips **all** fields, so editing a session never
> silently drops its speakers, themes, live URLs, type, or seat-mode
> override.

##### Set the live broadcast URL (what makes a session "LIVE")

The **Live stream URL** field is what flips a session to a live broadcast:
when it is set, the mobile app shows the LIVE player for that session;
when blank, the session is recorded/scheduled only.

Accepted values (validated in the CP **and** the API by the same rule):

- A **YouTube** link with a real 11-character video id — `watch?v=…`,
  `youtu.be/…`, `/live/…`, `/embed/…`, or `/shorts/…`
  (e.g. `https://www.youtube.com/watch?v=jfKfPfyJRdk`).
- **or** a direct **`https`** HLS/MP4 stream URL (ending `.m3u8` / `.mp4`).

Rules to know:

- **`https` only** — a plain `http://` link is rejected (no downgrade).
- **A bare channel/handle link is rejected** —
  `youtube.com/@channel` or `/channel/UC…` has no video id and will fail
  validation with *"Enter a valid YouTube link or HLS/MP4 stream URL."*
  Use the specific video/live watch URL.

Steps:

1. Open the session's **Edit** dialog (above).
2. Paste the watch/live URL into **Live stream URL (live broadcast)**.
3. **Save changes** → the update banner confirms it.
4. *(Optional)* re-open **Edit** to read the value back, or check the app's
   session screen, to confirm the LIVE player appears.

> **Worked example (done 2026-06-30):** to give the team a feed to test
> against, the **earliest** session — **S-TODAY · "Today's Welcome
> Session" / "جلسة اليوم الترحيبية"** (Hall A, starts 2026-06-14 11:00
> UTC) — had its **Live stream URL** set to a public 24/7 test stream,
> `https://www.youtube.com/watch?v=jfKfPfyJRdk`. The public app API
> (`GET /api/v1/app/programme/sessions/{id}`) then returned that
> `liveStreamUrl`, so the app's live player plays it. Clear the field the
> same way to stop showing the test feed to users.

##### Deactivate a session

Per-row **Deactivate** (trash) icon — soft-delete only. The session stops
appearing in the public agenda on next load; its bookings, questions, and
ratings are retained.

#### Field validation reference

| Field | Rule (rejected otherwise) |
|-------|---------------------------|
| Code | 2–16 chars; unique (case-insensitive) |
| Title (English / Arabic) | required; ≤ 256 chars |
| Hall | must be selected |
| Start / End | both required; End strictly after Start; entered as **UTC** |
| Capacity override | blank, or an integer ≥ 0 |
| Live stream / Sign-language URL | blank, or a YouTube video URL or `https` HLS/MP4 stream |

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "A session with code 'X' already exists" | Codes are unique (case-insensitive) | Pick a different code |
| "Enter a valid YouTube link or HLS/MP4 stream URL." | A channel/handle link, an `http://` link, or a malformed id | Use an `https` YouTube **watch/live** URL (with an 11-char id) or an `https` `.m3u8`/`.mp4` URL |
| "End time must be after the start time." | End ≤ Start, or times read as local | Re-enter both as **UTC**, End after Start |
| App doesn't show the LIVE player after saving | Field saved blank, or the app screen wasn't refreshed | Re-open Edit to confirm the URL; pull-to-refresh the app session screen |
| Times look an hour off | The columns/inputs are **UTC**, not the venue's local time | Convert to UTC when entering |

#### Cross-references

- Page reference: [`docs/pages/cp/admin-sessions.md`](../pages/cp/admin-sessions.md)
- E2E catalogue: [`docs/tests/e2e/cp-admin-sessions.md`](../tests/e2e/cp-admin-sessions.md)
- Authority spec: SIMF-FDS-004 §5.3 (PDF §2.9).
- Decisions: D-165 (sessions), D-349 (live = YouTube POC + HLS/MP4
  fallback), D-439 (live section + captions round-trip on edit).

### 5.5 Booking monitor — `/admin/bookings`

> Page reference: [`docs/pages/cp/admin-bookings.md`](../pages/cp/admin-bookings.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-bookings.md`](../tests/e2e/cp-admin-bookings.md)

A read-only list of every seat visitors are currently holding across all
sessions. There is deliberately no approve or reject step: the banner at the
top states *"Bookings confirm instantly — there is no approval step. A reserved
seat is released automatically if the visitor has not checked in 3 minutes
before the session starts."* Use it to see demand per session and to hand a
list to the door team. To act on an individual seat, go to **Session seat
plans** (§5.5).

#### Most common tasks

##### Read the list

**Programme → Bookings**. Five columns: **Session**, **Starts (Saudi time)**,
**Seat**, **Attendee**, **Booked (Saudi time)**. Session and Seat can be
filtered; Session, Starts, Seat and Booked can be sorted. A booking made
without a specific seat reads **General admission**. Times are shown in Saudi
local time in 12-hour format, not UTC.

##### Export to Excel

Tick the rows you want and press **Export**, or press it with nothing ticked to
export the whole current filtered list. Exporting needs the **Bookings →
Export** permission.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "No active bookings." | Nobody is holding a seat right now | Nothing to do |
| A booking you saw earlier has vanished | The visitor did not check in and the seat was auto-released 3 minutes before the session started, or an admin released it | Check **Session seat plans** (§5.5) for that session |
| "The action could not be completed. Please try again." | The list call failed | Reload; if it persists, report it with the time |
| "The export could not be generated. Please try again." | The export was refused or failed. Where the server sent its own message, that message is shown instead | Confirm you hold **Bookings → Export**, then retry |
| The menu row is missing | You do not hold **Bookings → View** | Ask an administrator |

#### What you cannot do here yet

- Approve, reject or cancel a booking. Bookings auto-confirm; release a seat on
  **Session seat plans** (§5.5) instead.
- Book a seat on a visitor's behalf from this page. Use the seat plan.

---

## 5A. Programme modules (remaining)

### 5A.1 Session categories — `/admin/session-categories`

> Page reference: [`docs/pages/cp/admin-session-categories.md`](../pages/cp/admin-session-categories.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-session-categories.md`](../tests/e2e/cp-admin-session-categories.md)

A small bilingual lookup that fills the **Category** dropdown on the Session
form (§5.4). It is deliberately a lookup, not a fixed list in code, so the
programme team can name the categories the client actually wants. The table
ships empty: until you add rows, every session shows `— No category —`.

#### Most common tasks

##### Add a category

1. **Programme → Session categories** → **Add**.
2. **Name (English)** and **Name (Arabic)** — both required, 1–128 characters
   each. Both are stored; the app and the CP each show the one matching the
   reader's language.
3. **Display order** — a whole number, lowest first, controls the order in the
   Session form's Category dropdown.
4. **Save** → *"Category saved."*

##### Edit / view / deactivate

Per-row **Edit**, **Details** and **Delete** icons. Edit adds an **Active**
tickbox. Delete is a deactivation, not a wipe, and it takes two steps: the
**Delete** icon first opens the category's details with a **Deactivate** button,
and only when you press that does the confirmation appear, reading *"Deactivate
"X"? It will be hidden from the session category picker. You can re-activate it
later via Edit."* Sessions already tagged with the category keep their tag.

##### Export / import

The toolbar **Export** button writes the selected rows (or the whole filtered
list when nothing is ticked) to Excel; **Import** reads a file back and reports
*"Import complete."*

> The toolbar's **Open as full page** / **Open as dialog** button switches how
> the Add / Edit / Details forms appear. Your choice is remembered per page.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Both names are required." | **Name (English)** or **Name (Arabic)** is blank | Fill both in. Each box stops you at 128 characters, so an over-long name cannot be typed |
| "Could not load categories." | The API call failed | Reload the page; if it persists, report it with the time |
| The menu row is missing | You do not hold the **Session categories → View** permission | Ask an administrator to add it to your role |
| Save is refused although you can see the button | The grid always shows Add / Edit / Delete; the API still checks **Session categories → Create / Edit / Delete** | Ask for the matching permission |

#### What you cannot do here yet

- Reorder by dragging. Use **Display order**.
- Delete permanently. Deactivation is the only removal.

### 5A.2 Programme days — `/admin/programme-days`

> Page reference: [`docs/pages/cp/admin-programme-days.md`](../pages/cp/admin-programme-days.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-programme-days.md`](../tests/e2e/cp-admin-programme-days.md)

The days that head the mobile app's Sessions screen. Each row is one calendar
date with a bilingual title and an optional day logo. One row per date: the
system refuses a second day on the same date.

#### Most common tasks

##### Add a programme day

1. **Programme → Programme days** → **Add**.
2. **Date** — a date picker. Required, and unique across active days.
3. **Title (English)** and **Title (Arabic)** — both required, 1–128 characters
   each. Both are stored and both are shown, per the reader's language.
4. **Display order** — 0 to 99999, lowest first.
5. **Save** → *"Programme day saved."*

##### Attach the day logo

The logo can only be attached after the row exists, so:

1. Save the day first.
2. Re-open it with the per-row **Edit** icon.
3. Use the **Day logo** upload control at the bottom of the form.

The grid's **Logo** column then shows **Set** instead of **None**.

##### Deactivate a day

Per-row **Delete** icon → the day's details open with a **Deactivate** button →
press it and the confirmation reads *"Deactivate the programme day "X"? It will
no longer appear in the app."* → **Deactivate** again. The toast confirms
*"Programme day deactivated."* Re-activate later by ticking **Active** in Edit.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "An English and Arabic title (1–128 characters) are required." | One of the two titles is blank or too long | Fill both |
| "A date is required." | The date box was left empty | Pick a date |
| "A programme day already exists for that date." | Another active day already holds that date | Edit the existing day instead, or deactivate it first |
| No **Day logo** control on the form | You are adding a new day; the logo needs an existing row | Save, then re-open with Edit |
| The menu row is missing | You do not hold **Programme days → View** | Ask an administrator |

#### What you cannot do here yet

- Excel export / import. This list is a handful of rows and carries neither.

### 5A.3 Run of Show — `/admin/programme/timeline`

> E2E catalogue: [`docs/tests/e2e/cp-admin-programme-timeline.md`](../tests/e2e/cp-admin-programme-timeline.md)

The whole agenda on one screen, read only. It reads the same sessions the
Sessions module (§5.4) owns and groups them by the calendar day of their start
time, day headings in date order, sessions inside a day in start-time order.
Use it as the printable-looking overview before a briefing. Nothing can be
created, edited or deleted here; go to **Programme → Sessions** for that.

#### Most common tasks

##### Read the run of show

1. **Programme → Run of Show**.
2. Two tiles at the top show **Days** and **Sessions** counts.
3. Each day renders as a heading plus a four-column table: **Time**, **Code**,
   **Session**, **Hall**. The Session and Hall names follow the language you
   are signed in with.
4. Under each table, a line reads *"N session(s) on this day"*.

##### Narrow to one day

Use the **Day** dropdown above the tables. It lists every day heading plus
**All days** (the default).

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "No sessions have been scheduled yet." | Nothing in the Sessions module yet | Add sessions in **Programme → Sessions** (§5.4) |
| "Could not load the programme. Please try again." | The sessions list call failed | Reload; if it persists, report it with the time |
| A session you just created is missing | The page loads once, on open | Reload the page |
| The menu row is missing | You do not hold **Programme timeline → View** | Ask an administrator |

#### What you cannot do here yet

- Create, edit, move or cancel a session. This page is a viewer.
- Print or export. Use the browser's own print.
- Filter by hall, theme or speaker. Only the day filter exists.

### 5A.4 Hall seat layouts — `/admin/halls/seat-layouts`

> Page reference: [`docs/pages/cp/admin-halls-seat-layouts.md`](../pages/cp/admin-halls-seat-layouts.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-halls-seat-layouts.md`](../tests/e2e/cp-admin-halls-seat-layouts.md)

One hall at a time: you define its rows, how many seats each row holds, and
each row's tier. This layout is the seat map visitors see when they book a
session in that hall, and it is what the Session seat plans page (§5.5) paints
reservations on top of. A hall with no layout is general admission: visitors
join with one tap and pick no seat.

#### Most common tasks

##### Define a layout

1. **Programme → Hall seat layouts**, then pick the hall from **Select a hall**.
   The dropdown shows `CODE — Name (cap N)` for each active hall. Arriving from
   the Halls grid's seat-layout row action opens the right hall already selected.
2. **Row labels (comma-separated, 1–26 entries, e.g. A,B,C,VIP)** — up to 26
   labels, each 8 characters or fewer, all different, 256 characters in total.
3. **Seats in each row (1–80)** — one number box appears per label you typed.
   Renaming a label keeps that position's count.
4. **Seat tier of each row** — one dropdown per row: **Normal**, **VIP**, or
   **VVIP (reserved)**. A brand-new row starts at **VVIP (reserved)** on
   purpose. VVIP seats cannot be booked by anyone and are handed out from the
   session seat plan with a guest note; VIP seats are for VIP guests only;
   Normal seats are open to every visitor.
5. Watch the **Hall capacity** vs **Layout capacity** meter. It turns amber and
   blocks **Save layout** while the layout is over capacity or a row is outside
   1–80.
6. **Save layout** → *"Layout saved."*

The **Layout preview** below mirrors what the visitor's seat picker will look
like, with a **Front / Stage** marker at the top. Every seat is drawn as free:
this page defines the map, it does not show bookings.

##### Remove a layout

The **Remove layout** button appears only once a layout exists. It sends the
hall back to general admission and is confirmed first. Saving needs the
**Seat layouts → Edit** permission; removing needs **Seat layouts → Delete**,
so if a button is missing that is why.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Enter between 1 and 26 row labels. To leave the hall with no seat map at all, use Remove layout." | No labels typed, or more than 26 | Fix the label list, or use Remove layout |
| "Each row label must be 8 characters or fewer." | A label is too long | Shorten it |
| "Row labels must be unique." | The same label twice (case is ignored) | Rename one |
| "All row labels together must be 256 characters or fewer." | The whole comma list is too long | Use shorter labels |
| "Each row's seat count must be between 1 and 80." | A seat box is 0, blank or above 80 | Correct that row |
| "Layout capacity (X) exceeds hall capacity (Y)." | Total seats beat the hall's capacity | Reduce seats, or raise the hall's capacity in **Programme → Halls** (§5.2) |
| "Removing this layout would strand N active seat reservation(s). Release them before removing the layout." | Visitors already hold seats in this hall | Release them on **Session seat plans** (§5.5), then remove |
| "This layout change would strand active seat reservations. Release the affected seats before changing the layout." | Your edit deletes or shrinks a row that someone is sitting in | Release those seats first, then re-save |
| No **Save layout** button | You lack **Seat layouts → Edit** | Ask an administrator |

### 5A.5 Session seat plans — `/admin/sessions/seat-plans`

> Page reference: [`docs/pages/cp/admin-sessions-seat-plans.md`](../pages/cp/admin-sessions-seat-plans.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-sessions-seat-plans.md`](../tests/e2e/cp-admin-sessions-seat-plans.md)

The live seating chart for one session. It draws the hall's layout (§5.4) and
paints every active reservation onto it, so you can see who is sitting where,
hold seats for protocol guests, and release a seat. This is the desk you work
from when a VIP arrives unbooked.

#### Most common tasks

##### Open a session's plan

**Programme → Session seat plans** → pick from **Select a session** (shown as
`CODE — Title`, active sessions only). The grid, the legend (**Free**,
**Reserved (attendee)**, **Reserved (admin)**, **Random**), the count line and
the holder roster all load together.

##### Hold a single seat for a VIP or VVIP guest

1. Type the guest's name into **Guest note (Arabic)** and **Guest note
   (English)** (up to 256 characters each; both are stored).
2. Tap the free seat on the grid. One tap holds it: *"Seat reserved for a VIP."*
3. The note travels with the seat and is what the app and the staff seating desk
   show for it. The boxes clear after each successful hold.

##### Block a whole row

Type the row's label into **Row to reserve (must exist in the hall layout)** and
press **Reserve row**. Every free seat in that row becomes an admin block, which
takes the row out of visitor self-pick. Seats already held by attendees are left
alone.

##### Release a seat

Tapping a held seat does not release it immediately. It opens a confirmation
that names the seat, the holder and the state, and reads *"Seat B7 is held by
… . Releasing it frees the seat for someone else and cannot be undone. The
attendee is notified."* Choose **Release** or **Keep the seat**.

##### Read the roster

Under the grid, a table lists every held seat with **Seat**, **Held by**,
**Kind** and **State**. State is **Unavailable** for an admin block,
**Confirmed (checked in)** once the holder has scanned in at the gate, and
**Reserved** otherwise. Under **Held by**, a seat held with no attendee and no
guest note reads **Admin block (no attendee)**. Under **Seat**, an open-seating
join that holds no specific seat reads **General admission**.

Holding, blocking and releasing all need the **Seat plans → Edit** permission.
With **Seat plans → View** only, you can read the plan but the buttons are gone.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Row 'X' is not in the hall layout." | The label you typed is not one of the hall's rows | Check the exact label on **Hall seat layouts** (§5.4) |
| "That seat is already reserved." | Someone booked it between your page load and your tap | Reload the session and pick another seat |
| "No active reservations on this session." | Nobody has booked yet | Nothing to do |
| No seat grid, only a list of reservations | The hall has no seat layout | Define one on **Hall seat layouts** (§5.4) |
| "Could not load session seat plan." | A call failed | Reload; if it persists, report it with the session code |
| No **Reserve row** or **Release** buttons | You lack **Seat plans → Edit** | Ask an administrator |

### 5A.6 Speaker presentations — `/admin/speaker-presentations`

> Page reference: [`docs/pages/cp/admin-speaker-presentations.md`](../pages/cp/admin-speaker-presentations.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-speaker-presentations.md`](../tests/e2e/cp-admin-speaker-presentations.md)

Where a speaker's slide decks and papers live, one file per session. You pick a
speaker from a wall of profile cards, then upload, download or remove their
files. The page shares the Speakers permissions: **Speakers → View** to read,
**Speakers → Edit** to upload and delete.

#### Most common tasks

##### Upload a presentation

1. **Programme → Speaker presentations** → click the speaker's card. Use
   **← All speakers** to go back.
2. **Session** — pick the session the file belongs to. **Upload** stays disabled
   until you do.
3. **Presentation file** — choose the file. PDF, PowerPoint (`.pptx` / `.ppt`),
   Word (`.docx` / `.doc`) and Excel (`.xlsx` / `.xls`) are accepted, up to
   50 MB. The file is checked by its real content, not just its extension.
4. **Upload** → *"Presentation uploaded."* The grid reloads with the new row.

##### Download or remove a file

The grid lists **File**, **Session** and **Size**, sortable, with **File** and
**Session** filterable. Per row:

- **Download** (arrow icon) saves the file without leaving the page.
- **Delete** (trash icon) asks *"Remove this presentation file?"* first, then
  reports *"Presentation removed."*

The toolbar **Export** button writes the current speaker's list to Excel.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "The presentation must be a PDF, PowerPoint, or Word document." | Wrong file type, or a file renamed to look like one | Upload the real document |
| "The presentation file must be 50 MB or smaller." | File too big | Compress it or split the deck |
| "No file was uploaded." | You pressed Upload with no file chosen | Choose a file first |
| "The speaker was not found." | The speaker was deactivated while you had the page open | Reload and pick an active speaker |
| "The session was not found." | The chosen session was removed meanwhile | Reload and pick again |
| "This speaker has no presentation files yet." | Nothing uploaded for them | Upload one |
| The Session box and Upload button are missing | You lack **Speakers → Edit** | Ask an administrator |

#### What you cannot do here yet

- Replace a file in place. Delete the old one and upload the new one.
- Upload without choosing a session. Every file is tied to one session.

---

## 5B. Meetings & availability

The nine modules in this section are one workflow, not nine. The team first
declares **when** a speaker, a delegation or a hall is free (the availability
pages), the app then lets an attendee raise a request against those free slots,
and the review desks turn a request into a booked meeting in a real hall at a
real table. All nine live under the **Programme** nav group.

### 5B.1 Speaker meeting requests — `/admin/speaker-meeting-requests`

> Page reference: [`docs/pages/cp/admin-speaker-meeting-requests.md`](../pages/cp/admin-speaker-meeting-requests.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-speaker-meeting-requests.md`](../tests/e2e/cp-admin-speaker-meeting-requests.md)

The review desk for meeting requests an attendee raised **against a speaker**
from the mobile app. A speaker only appears in that app flow when their Speakers
record has **Allow meeting requests (السماح بطلب مقابلة)** ticked under
Programme → Speakers, and the attendee's account needs **Can request speaker
meetings** on their
profile. Approving a request books a free hall slot, so the hall's meeting time
must already be defined in **Hall availability** (5B.3). Opening the page needs
the Speaker meeting requests → View permission; every action button needs
Speaker meeting requests → Manage.

Columns: **Speaker**, **Requester**, **Subject**, **Status**, **Submitted**,
**Responded**. Status shows one of six chips: *Pending*, *Awaiting
confirmation*, *Confirmed*, *Done*, *Rejected*, *Cancelled*. The toolbar has
**Export** (Excel of the selected rows, or the whole filtered set when nothing
is ticked). There is no Add: requests are only created from the app.

#### Most common tasks

##### Decide a pending request

1. **Programme → Speaker meeting requests** → the **Respond** (reply arrow) icon
   on a *Pending* row. The requester's email loads into the dialog a moment
   after it opens.
2. **Hall** — pick from the meeting halls. Only active halls whose purpose is
   *Meeting* or *General* are offered.
3. **Hall slot** — pick one of the free slots the hall's availability windows
   produced. If it reads *"This hall has no free slots. Add hall availability
   first."* go and add a window (5B.3).
4. **Meeting table (optional)** — appears only when the hall has tables. Leave
   on *"— No table —"* to book the room without a specific table.
5. **Note / justification (≤2000 chars)** — free text sent back to the requester.
6. Choose one of the four decision buttons:
   - **Approve** emails the speaker their own confirm/reject link (valid 72
     hours) and parks the request on *Awaiting confirmation*. Needs a hall slot.
   - **Confirm** books it straight away, for when you already hold the speaker's
     verbal agreement. Needs a hall slot.
   - **Accept without a hall** agrees to the meeting when no slot is available.
     No room is booked and the team arranges the place by hand.
   - **Decline** closes the request and requires a justification in the note.

##### Check a meeting in, resend, or reopen

- A *Confirmed* row carries **Check in**: confirm the prompt and the meeting
  flips to *Done*. Use it at the hall door.
- An *Awaiting confirmation* row carries **Resend speaker confirmation**, which
  emails a fresh 72-hour link.
- A *Rejected* or *Cancelled* row carries **Reopen request**. It returns to a
  clean *Pending* and clears the earlier decision, note and hall booking, so a
  mistaken decline is recoverable.

> An *Awaiting confirmation* request that the speaker never answers is swept
> back to *Pending* automatically once its 72-hour link has expired, and the held
> hall slot is released. It reappears in your queue with no hall bound.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Select a hall and a free slot to approve or confirm." | Approve / Confirm both book a room | Pick a hall and slot, or use **Accept without a hall** |
| "This hall has no free slots. Add hall availability first." | No availability window on that hall, or every slot is taken | Add a window in 5B.3, or pick another hall |
| "A justification is required to decline or cancel." | Decline was pressed with an empty note | Type the reason in **Note / justification** |
| "That hall slot is no longer available." | Someone booked the same slot while the dialog was open | Re-open Respond and pick another slot |
| "The speaker already has a meeting at that time." | Double-booking the speaker | Pick a different slot |
| "The requester already has a meeting booked at that time." | Double-booking the attendee | Pick a different slot |
| "That meeting table is already booked at that time." | The table is held by another meeting | Choose another table, or leave it as "— No table —" |
| "This meeting request has already been responded to." | Another admin decided it first | Refresh the page and read the new status |
| "This speaker has no contact email, so the confirmation link cannot be sent. Add the speaker's email, or use Confirm when you already have their verbal agreement." | Approve needs somewhere to email the link | Add the email on the Speakers record, or press **Confirm** |
| "Only a confirmed meeting can be checked in." | Check-in pressed on a row that is not *Confirmed* | Nothing to do; the status is already past check-in |
| "Only a declined or cancelled request can be reopened." | Reopen pressed on a live row | Nothing to do |
| No action icon on any row | Missing the Speaker meeting requests → Manage permission | Ask an administrator to add it to your role |

### 5B.2 Speaker availability — `/admin/speaker-availability`

> E2E catalogue: [`docs/tests/e2e/cp-admin-speaker-availability.md`](../tests/e2e/cp-admin-speaker-availability.md)

Where the team declares the hours a speaker is free to take meetings. Each
window is chopped into bookable slots of the length you set, and those slots are
what an attendee sees in the app when asking for a meeting. A speaker with no
window here cannot be asked for a meeting at all. Needs the Speaker meeting
requests → Manage permission.

#### Most common tasks

##### Add an availability window

1. **Programme → Speaker availability** → pick the person in **Speaker**
   (the list reads *"Select a speaker…"* until you do). The rest of the page
   appears only once a speaker is chosen.
2. **Start (Saudi time)** and **End (Saudi time)** — enter venue local time, not
   UTC. The date pickers are clamped to the forum days.
3. **Slot length (minutes)** — defaults to `30`. Accepted range is 5 to 480. The
   window must be long enough to hold at least one whole slot.
4. **Add window** → a green *"Window added."* confirms it, and the window joins
   the **Windows** list below as `start – end · N min slots`.

##### Remove a window

Press the bin icon on the window, then confirm *"Delete this availability
window? Its bookable slots will no longer be offered."* A green *"Availability
window deleted."* confirms it.

Nothing on this page is bilingual: a window is a pair of timestamps.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Enter a valid start and end." | One of the two date boxes is empty or unreadable | Re-enter both |
| "Enter a positive slot length." | Slot length is blank, zero or negative | Use a whole number of minutes |
| "Dates must be within …" | The window falls outside the forum days | Move it inside the dates shown in the message |
| "Slot length must be between 5 and 480 minutes." | Server-side range check | Pick a length inside that range |
| "The window must end after it starts and fit at least one slot." | End is before Start, or the window is shorter than one slot | Widen the window or shorten the slot |
| "The speaker was not found." | The speaker you picked is deactivated. Deactivated speakers stay in the picker, but no window can be saved against one | Reactivate the speaker under Programme → Speakers, then add the window again |
| The speaker is missing from the picker | The picker lists every speaker on the Speakers page, active or not, so the person has not been created yet | Create the speaker under Programme → Speakers |

### 5B.3 Hall availability — `/admin/hall-availability`

> E2E catalogue: [`docs/tests/e2e/cp-admin-hall-availability.md`](../tests/e2e/cp-admin-hall-availability.md)

Banner reads **Hall availability (meeting time)**. This is where the team says
which hours a room is open for meetings. It is the single source of the free
slots offered on **both** review desks, 5B.1 and 5B.4, so a hall with no window
here can never be bound to an approved meeting. Needs the Hall availability →
Manage permission.

#### Most common tasks

##### Add an availability window

1. **Programme → Hall availability** → pick the room in **Hall**. Only active
   halls whose purpose is *Meeting* or *General* are listed; set a hall's
   purpose on 5B.8 if it is missing.
2. **Start (Saudi time)** and **End (Saudi time)** — venue local time.
3. **Slot length (minutes)** — defaults to `30`, accepted range 5 to 480.
4. **Add window** → *"Window added."* The window appears in the **Windows**
   list as `start – end · N min slots`.

##### Remove a window

Bin icon on the window, then confirm *"Delete this availability window? Its
bookable slots will no longer be offered."*

Unlike the speaker and delegation availability pages, the date boxes here are
**not** clamped to the forum days, so double-check the date before you save.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Enter a valid start and end." | A date box is empty or unreadable | Re-enter both |
| "Enter a positive slot length." | Slot length is blank, zero or negative | Use a whole number of minutes |
| "Slot length must be between 5 and 480 minutes." | Server-side range check | Pick a length inside that range |
| "The window must end after it starts and fit at least one slot." | End is before Start, or shorter than one slot | Widen the window or shorten the slot |
| The hall is missing from the picker | The hall is inactive, or its purpose is not *Meeting* or *General* | Reactivate it under Programme → Halls & seating, or set its purpose on 5B.8 |
| A slot you added never appears on a review desk | The slot is already taken by another meeting | Add more meeting time, or use a different hall |

### 5B.4 Delegation meetings — `/admin/delegation-meetings`

> E2E catalogue: [`docs/tests/e2e/cp-admin-delegation-meetings.md`](../tests/e2e/cp-admin-delegation-meetings.md)

The review desk for country-to-country (G2G) meeting requests: a delegate of one
invited country asks to meet another invited country's delegation. The requester
needs **Can request delegation meetings** on their account, both countries must
be flagged **Invited to send a delegation (وفد)** under Reference data →
Countries, and the target delegation needs availability (5B.5). Opening the page
needs Delegation meetings → View; every action needs Delegation meetings →
Manage.

Columns: **From delegation**, **To delegation**, **Attendees**, **Subject**,
**Status** (filterable), **Submitted** (sortable). Rows carry no tick boxes and
the page has no Add or Export: requests come from the app only.

#### Most common tasks

##### Decide a pending request

1. **Programme → Delegation meetings** → the **Respond** (reply arrow) icon on a
   *Pending* row.
2. **Hall**, then **Hall slot**, then the optional **Meeting table (optional)**,
   exactly as on the speaker desk (5B.1).
3. **Note / justification (≤2000 chars)**.
4. Press one of:
   - **Approve** binds the slot and notifies the other delegation to confirm, by
     email and in the app. The row moves to *Awaiting confirmation*.
   - **Confirm** books it now, for when you already hold their verbal agreement.
   - **Decline** closes the request and requires a justification.

##### Finish or cancel an approved request

Press **Respond** on an *Awaiting confirmation* row. The dialog shows the slot
already bound and offers only **Confirm** (finalise it, keeping that slot) and
**Cancel meeting** (which requires a justification and releases the hall).

##### Check a meeting in

A *Confirmed* row carries **Check in**. Confirm the prompt and the meeting flips
to *Done*.

> An approved meeting the other delegation never confirms is swept back to
> *Pending* once its 72-hour confirm link expires, and the hall slot it was
> holding is released.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Select a hall and a free slot to approve or confirm." | Approve / Confirm both book a room | Pick a hall and a free slot |
| "This hall has no free slots. Add hall availability first." | No meeting time defined on that hall | Add a window in 5B.3 |
| "A justification is required to decline or cancel." | Decline / Cancel pressed with an empty note | Type the reason |
| "That hall slot is no longer available." | Taken while your dialog was open | Re-open Respond and pick another slot |
| "One of the delegations already has a meeting at that time." | Either side is double-booked | Pick a different slot |
| "That meeting table is already booked at that time." | The table is held by another meeting | Pick another table, or none |
| "The meeting slot is in the past." | The chosen slot has already passed | Pick a future slot |
| "This meeting request has already been responded to." | Another admin decided it first | Refresh and read the new status |
| "This meeting is not awaiting confirmation." | Confirm pressed on a row that has moved on | Refresh the page |
| "Only a confirmed meeting can be checked in." | Check-in pressed too early or too late | Nothing to do |

#### What you cannot do here yet

- There is no **Reopen** on a declined delegation request (the speaker desk has
  one). A declined request has to be raised again from the app.
- There is no Excel export on this desk.

### 5B.5 Delegation availability — `/admin/delegation-availability`

> E2E catalogue: [`docs/tests/e2e/cp-admin-delegation-availability.md`](../tests/e2e/cp-admin-delegation-availability.md)

The delegation twin of 5B.2: the hours a country's delegation is free to take
meetings, sliced into bookable slots. A delegation with no window here cannot be
asked for a meeting. Needs the Delegation meetings → Manage permission.

#### Most common tasks

##### Add an availability window

1. **Programme → Delegation availability** → pick the country in **Delegation
   (country)**. The picker reads *"Select an invited country…"* and lists only
   active countries flagged **Invited to send a delegation (وفد)**.
2. **Start (Saudi time)** and **End (Saudi time)** — clamped to the forum days.
3. **Slot length (minutes)** — defaults to `30`, accepted range 5 to 480.
4. **Add window** → *"Window added."*

##### Remove a window

Bin icon, then confirm *"Delete this availability window? Its bookable slots
will no longer be offered."*

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| The country is missing from the picker | It is not flagged as invited, or is inactive | Tick **Invited to send a delegation (وفد)** under Reference data → Countries |
| "The delegation is not an invited country." | The invited flag was cleared after the page loaded | Reload the page and re-check the country |
| "Enter a valid start and end." | A date box is empty or unreadable | Re-enter both |
| "Enter a positive slot length." | Slot length is blank, zero or negative | Use a whole number of minutes |
| "Dates must be within …" | The window falls outside the forum days | Move it inside the dates shown in the message |
| "Slot length must be between 5 and 480 minutes." | Server-side range check | Pick a length inside that range |
| "The window must end after it starts and fit at least one slot." | End before Start, or shorter than one slot | Widen the window or shorten the slot |

### 5B.6 Document requests — `/admin/document-requests`

> Page reference: [`docs/pages/cp/document-requests.md`](../pages/cp/document-requests.md)
> · E2E catalogue: [`docs/tests/e2e/cp-document-requests.md`](../tests/e2e/cp-document-requests.md)

The review desk for participation-document requests raised from the app: an
**Official attendance certificate**, a **Participation letter** or an
**Invitation letter**. Your decision is recorded against the request and the
requester is notified in the app. Opening the page needs Participation document
requests → View; the Respond button needs Participation document requests →
Manage.

Columns: **Requester**, **Document**, **Note**, **Status** (sortable,
filterable), **Submitted**, **Responded**. Status reads *Under review*,
*Accepted*, *Rejected* or *Cancelled*. There is no Add, no Edit and no Export.

#### Most common tasks

##### Respond to a request

1. **Programme → Document requests** → the **Respond** (reply arrow) icon. It
   appears only on rows still *Under review*.
2. The dialog shows the requester, their email once it loads, the document type,
   and the note they typed. All read-only.
3. **Decision** — choose **Accept** or **Reject**. Accept is preselected.
4. **Response note (optional, ≤2000 chars)**.
5. **Send response** → a green *"Response sent."* and the row's status updates.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Could not load document requests." | The API call failed | Refresh; if it persists, check the API is up |
| "This request has already been responded to." | Another admin decided it first | Refresh and read the new status |
| "Response note must be 2000 characters or fewer." | The note is too long | Shorten it |
| No Respond icon on any row | Every row is already decided, or you lack Participation document requests → Manage | Filter Status to *Under review*; if the icon is still absent, ask for the permission |

#### What you cannot do here yet

- Accepting does **not** generate or attach the document itself. It records the
  decision and notifies the requester; producing and delivering the certificate
  or letter is still a manual step.

### 5B.7 Badge requests — `/admin/badge-requests`

> Page reference: [`docs/pages/cp/badge-requests.md`](../pages/cp/badge-requests.md)
> · E2E catalogue: [`docs/tests/e2e/cp-badge-requests.md`](../tests/e2e/cp-badge-requests.md)

The review desk for attendees asking to change the job title printed on their
badge. **Accepting a request writes the requested title straight onto the
person's profile**, so it is what a reprinted badge will show. Opening the page
needs Badge update requests → View; the Respond button needs Badge update
requests → Manage.

Columns: **Requester**, **Requested title** (sortable, filterable), **Current
title**, **Status** (sortable, filterable), **Submitted**, **Responded**. Status
reads *Under review*, *Accepted*, *Rejected* or *Cancelled*.

#### Most common tasks

##### Respond to a request

1. **Programme → Badge requests** → the **Respond** (reply arrow) icon on a row
   still *Under review*.
2. The dialog puts **Current title** next to **Requested title** so you can see
   exactly what changes, plus the requester's email and their **Reason** if they
   gave one.
3. **Decision** — **Accept** or **Reject**.
4. **Response note (optional, ≤2000 chars)**.
5. **Send response** → *"Response sent."* On Accept the profile job title is
   updated immediately and the requester is told the new title in the app.

> Requested titles are capped at 128 characters when the attendee submits them
> from the app, and they are stored as a single value: the badge job title is
> not a bilingual pair.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Could not load badge requests." | The API call failed | Refresh; if it persists, check the API is up |
| "This request has already been responded to." | Another admin decided it first | Refresh and read the new status |
| "Response note must be 2000 characters or fewer." | The note is too long | Shorten it |
| The badge still prints the old title after Accept | The badge was printed before you accepted | Reprint from the badge desk |
| No Respond icon on any row | Nothing is under review, or you lack Badge update requests → Manage | Filter Status to *Under review*, then ask for the permission |

### 5B.8 Meeting Tables & Hall Allocation — `/admin/meeting-tables`

> Page reference: [`docs/pages/cp/meeting-tables.md`](../pages/cp/meeting-tables.md)
> · E2E catalogue: [`docs/tests/e2e/cp-meeting-tables.md`](../tests/e2e/cp-meeting-tables.md)

The room-configuration page. It does three things for one hall at a time: sets
what the hall is **for**, defines the physical **meeting tables** in it, and
**reserves** part or all of the hall for a purpose over a time-slot. Every other
meeting module depends on it: a hall is only offered on 5B.1, 5B.3, 5B.4 and
5B.9 when its purpose is *Meeting* or *General*, and the table pickers on those
pages read the tables defined here. Opening the page needs Meeting tables →
View.

Nothing on this page is bilingual. A table code is one venue-side identifier.

#### Most common tasks

##### Set what a hall is for

1. **Programme → Meeting Tables** → choose the room in **Hall** (each entry
   reads `English name — Arabic name`). Everything below appears once you pick.
2. **Purpose** — *General*, *Booth*, *Session* or *Meeting*.
3. **Set purpose** → *"Hall purpose saved."* This button needs the Halls → Edit
   permission.

##### Add or generate meeting tables

- One at a time: **Add table** in the **Meeting tables** grid, then **Code**
  (required, up to 16 characters), **Row** (up to 8 characters), **Column** (a
  whole number from 1) and **Capacity** (2 to 100). **Save**.
- In bulk: **Generate tables** on the grid toolbar. Choose **Mode**:
  - *Random by count* then **Number of tables**, or
  - *By row/column* then **Row/column codes (CSV)**, for example `A1,A2,B3`.
  Set **Capacity**, optionally tick **Reset — remove existing tables first**,
  then **Generate** → *"Tables generated."*
- Per-row **Edit** and **Delete** work as usual; Delete asks *"Delete this
  table?"* first. **Export** downloads the hall's tables as Excel and needs the
  Meeting tables → Export permission. Creating, editing, generating and deleting
  all need Meeting tables → Edit.

##### Reserve hall space for a time-slot

1. **Reserve hall** on the **Hall allocations** grid.
2. **Purpose** — *Meeting*, *Session*, *Booth* or *General*.
3. **Mode** — *Whole hall*, *Random by count* (then **Number of tables**) or
   *By row/column* (then the CSV spec).
4. **Start** and **End** in Saudi time.
5. **Save** → *"Saved."* Release one later with the **Release** action, which
   asks *"Release this allocation?"* and confirms with *"Allocation released."*
   Allocations need the Hall allocations → View and Edit permissions.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "A table code is required." | The Code box was left empty | Type a code, up to 16 characters |
| "A table with code 'X' already exists in this hall." | Codes are unique per hall | Pick a different code |
| "Meeting tables require a Meeting or General hall." | The hall's purpose is *Booth* or *Session* | Set the purpose to *Meeting*, then add tables |
| "Enter a valid start and end time (end after start)." | An allocation date box is empty, or End is not after Start | Re-enter both |
| "Random allocation needs a count; row/column allocation needs a spec." | The mode-specific box is empty | Fill **Number of tables** or **Row/column codes (CSV)** |
| "The hall is already reserved for an overlapping time-slot." | Two allocations collide | Shift the times, or release the existing allocation first |
| "Could not load the page." | The halls or tables call failed | Refresh; if it persists, check the API is up |
| A hall you configured never appears on a meeting desk | It is inactive, or its purpose is not *Meeting* or *General* | Reactivate it, or set the purpose here |

### 5B.9 Business Meetings — `/admin/business-meetings`

> Page reference: [`docs/pages/cp/business-meetings.md`](../pages/cp/business-meetings.md)
> · E2E catalogue: [`docs/tests/e2e/cp-business-meetings.md`](../tests/e2e/cp-business-meetings.md)

Admin-arranged business meetings: you schedule two or more parties, companies
and visitors, onto a specific **meeting table** for a from-to time-slot. Unlike
the two review desks this is not a queue of app requests, you create the meeting
yourself, and it is confirmed the moment you save. Companies come from the
Exhibition → Exhibitors list and visitors from People → Attendees; the halls and
tables come from 5B.8. Opening the page needs Business meetings → View.

Columns: **Hall**, **Table**, **Type**, **Start**, **End**, **Participants**
(a count) and **Status** (*Confirmed* or *Cancelled*). The toolbar carries
**Schedule meeting**, **View** and **Export**.

#### Most common tasks

##### Schedule a meeting

1. **Programme → Business Meetings** → **Schedule meeting**.
2. **Hall** — only active *Meeting* and *General* halls are listed.
3. **Table** — unlocks once a hall is chosen, and shows each table's capacity in
   brackets.
4. **Type** — `B2B`, `B2C` or `G2B`.
5. **Start (Saudi time)** and **End (Saudi time)**, clamped to the forum days.
6. **Participants** — choose *Company* or *Visitor* in the first box, pick the
   party in the second, then **Add**. Repeat. **Remove** drops one. At least two
   participants are required.
7. **Notes** — optional, up to 1024 characters.
8. **Schedule** → *"Meeting scheduled."*

##### Read a meeting back

**View** on the row opens **Meeting detail**: hall, table, type, start, end, the
named participants, the notes, and the cancellation reason when it was
cancelled.

##### Cancel a meeting

**Cancel** (the × icon) on a *Confirmed* row asks *"Cancel the meeting in {hall}
at table {table}?"*. Add an optional **Reason (optional)**, up to 512
characters, then **Cancel meeting**, or back out with **Keep meeting**. A green
*"Meeting cancelled."* confirms it. This needs the Business meetings → Cancel
permission; scheduling needs Business meetings → Schedule and Export needs
Business meetings → Export.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Choose a table." | Saved without picking a table | Pick a hall first, then a table |
| "Enter a valid start and end time (end after start)." | A date box is empty, or End is not after Start | Re-enter both |
| "Add at least two participants." | Fewer than two parties in the list | Add another company or visitor |
| "The table seats N; M participants were given." | More parties than the table holds | Remove participants, or pick a bigger table |
| "That meeting table is already booked at that time." | The table is held by another meeting or request | Pick another table or another time |
| "The hall is reserved for another purpose at this time." | A hall allocation from 5B.8 covers the slot | Release the allocation, or move the meeting |
| "A participant already has a meeting at this time." | One party is double-booked | Move the meeting, or drop that party |
| "This meeting is not confirmed." | Cancel pressed on a row already cancelled | Refresh the page |
| The hall list is empty | No active hall has purpose *Meeting* or *General* | Set a hall's purpose on 5B.8 |
| The table list is empty | That hall has no tables yet | Add or generate tables on 5B.8 |

---

## 6. Scientific committee & Exhibition modules

### 6.1 Session moderators — `/admin/session-moderators`

> Page reference: [`docs/pages/cp/admin-session-moderators.md`](../pages/cp/admin-session-moderators.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-session-moderators.md`](../tests/e2e/cp-admin-session-moderators.md)

A **grant** on this page gives one named person the right to run the live Q&A
desk of **one** session. It is not a job title: it is a per-session key. Once
granted, that person works the questions on the session's own desk
(`/sessions/{id}/moderate`, opened from the Sessions grid, §5.4). The list shows
Session (code + title), Moderator (name + email), Assigned by, and the assign
time on the Saudi clock.

#### Most common tasks

##### Assign a moderator

1. **Scientific committee → Session moderators** → toolbar **Add**.
2. **Session** — pick from the active sessions, listed as `CODE — Title`.
3. **Moderator** — the list holds only approved accounts whose profile type
   sets the mobile app role to Moderator. Nobody else can appear here.
4. **Assign**. A green *"Moderator assigned."* confirms it.

Leaving either box empty stops the submit with *"Pick a session and a
moderator."* inside the dialog.

##### Revoke a grant

Per-row **Revoke** icon → a confirmation names the person and the session and
warns that during a live session they lose the moderation controls immediately
→ confirm → *"Moderator grant revoked."* The icon is hidden unless you hold the
Session moderators → Revoke permission.

##### Export the grants

Toolbar **Export** writes an Excel file of the ticked rows, or of the current
filtered list when nothing is ticked. Needs Session moderators → Export. There
is no import: grants are made one at a time.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "No eligible moderator yet. Assign an approved account a profile type whose mobile app role is Moderator first." | The picker only lists approved accounts whose profile type is a Moderator type | Set that profile type on the account first, then reopen **Add** |
| "The account is not eligible to moderate — assign it a profile type whose mobile app role is Moderator first." | The account's profile type changed after the picker loaded | Fix the profile type and assign again |
| "Moderator must be an approved account." | The account is still pending or was rejected | Approve the account, then assign |
| "This user is already a moderator of the session." | The grant already exists | Nothing to do; it is already in the list |
| "Cannot assign a moderator to an inactive session." | The session was deactivated | Reactivate the session, or pick another one |
| "The sessions and eligible moderators could not be loaded." | The pickers need the Session moderators → Assign permission | Retry once; if it repeats, ask for that permission |

### 6.2 Question queue — `/admin/question-queue`

> Page reference: [`docs/pages/cp/admin-question-queue.md`](../pages/cp/admin-question-queue.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-question-queue.md`](../tests/e2e/cp-admin-question-queue.md)

The committee's central triage desk for audience questions, across every
session at once. It shows the **pending** questions only, oldest first, capped
at the 200 oldest. Approving a question is what pushes it through to the
session's own moderation desk (§6.1), so this page is the gate between the
audience and the stage. Columns: Session, Question, Submitter, Phase (a **Pre**
or **Live** pill) and the **AI verdict** the automatic filter recorded.

Sorting, column filters and paging work on the already-loaded queue, so they are
instant and do not re-read the server. Refresh the page to pull new arrivals.

#### Most common tasks

##### Work the queue

1. **Scientific committee → Question queue**.
2. Per row, choose one:
   - **Approve** (tick icon) → *"Question approved."* The question moves on to
     the session's moderation desk.
   - **Hide** (crossed-eye icon) → *"Question hidden."* It leaves the queue and
     is not shown to anyone.
   - **Escalate** (share icon) → the **Escalate to a role** dialog. Type the
     **Role** to route it to, then **Escalate** → *"Question escalated."*
3. Approve and Hide need the Questions → Moderate permission; Escalate needs
   Questions → Escalate. If you cannot see those icons, that is why.

##### Export the queue

Toolbar **Export** writes the ticked rows, or the whole pending queue when
nothing is ticked.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Could not load the question queue." | The read failed, or you no longer hold Questions → View | Refresh; if it repeats, check your permissions |
| "The escalation role must be between 1 and 64 characters." | The Role box was blank or too long | Type a role name of 1 to 64 characters |
| "The question was not found." | Someone else already approved or hid it | Refresh; the queue reloads without it |
| "The action could not be completed." | The approve / hide / escalate call failed | Refresh and try the action again |
| A question you expect is not listed | Only pending questions appear, and only the 200 oldest | Work down the queue; approved and hidden ones leave it by design |

#### What you cannot do here yet

- No reject-with-a-reason: the choices are approve, hide, escalate.
- The question text cannot be edited or shortened here.
- Approved and hidden questions have no history view on this page.

### 6.3 Session summaries — `/admin/session-summaries`

> Page reference: [`docs/pages/cp/admin-session-summaries.md`](../pages/cp/admin-session-summaries.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-session-summaries.md`](../tests/e2e/cp-admin-session-summaries.md)

The محضر desk: one row per active session, showing whether that session has a
summary and how far it has got. A summary is drafted (by AI or by hand),
submitted for review, approved by the scientific team, and only then published
for the mobile app to read. The **Status** column shows **No summary**,
**Draft**, **In review** or **Approved**, plus a **Published** pill once it is
live; **Source** says **AI-drafted** or **Hand-written**.

#### Most common tasks

##### Draft with AI, then correct it

1. **Scientific committee → Session summaries** → row **AI draft**.
2. The draft is built from what was actually said in the session (its live
   captions), falling back to the session abstract when no transcript exists.
   The editor opens with *"This draft was generated by AI — review and edit it
   before publishing."*
3. The AI writes into the **Arabic** full text. The English column is left for
   the committee to fill.
4. Up to three read-only boxes appear, each only when that material exists:
   **AI source subtitle (English)**, **AI source subtitle (Arabic)** and
   **Original AI draft (read-only)**, whose label carries the time the draft
   was captured. They survive your edits so the trail is auditable, and a
   hand-written summary shows none of them.

##### Fill in the sections

Every content field is bilingual and both languages are stored:

- **Key points (English / Arabic)**, one per line, up to 4000 characters each.
- **Recommendations (English / Arabic)**, up to 4000 each.
- **Speakers (English / Arabic)**, up to 1000 each.
- **Full text (English / Arabic)**, up to 8000 each.
- **Summary video URL** (optional, up to 1024): a YouTube link or a direct
  HLS/MP4 link to the team's short summary video. The app shows it beside the
  full recording.

**Save** confirms with *"Summary saved."*

> If the summary was already published, approved or in review and you changed
> the content, a confirmation appears first: **"Saving will withdraw this
> summary"**. It spells out that the summary is pulled from the app until it is
> approved and published again. Continue with **Save and withdraw**.

##### Move it through review and publish

**Submit for review** → **Approve (ready for the host)** → **Publish**.
**Return to draft** sends it back a step. The Publish button stays visible but
disabled until the summary is approved, and its tooltip says so. Editing needs
SessionSummaries → Edit, approving needs → Approve, publishing needs → Publish.

Once the summary is live that same row button becomes **Unpublish**, so a
summary published too early can be pulled back out of the app: press it and a
green *"Summary unpublished."* confirms. It needs the same SessionSummaries →
Publish permission, and the summary keeps its approval, so **Publish** puts it
back.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "This summary must be reviewed and approved by the scientific team before it can be published." | Publish was attempted on a draft | Submit for review, get it approved, then publish |
| "The session has not started yet — its summary cannot be published before it begins." | The session's start time is in the future | Wait for the session to start |
| "Submit the summary for review before approving it." | Approve was attempted on a draft | Use **Submit for review** first |
| "This summary is already approved — return it to draft before resubmitting." | Submit was attempted on an approved summary | **Return to draft**, edit, resubmit |
| "This summary still contains placeholder text from the offline AI stub provider. Replace it with the real minutes before approving or publishing it." | The AI seam returned stub text, not real minutes | Replace the body with the real محضر, then approve |
| "The summary video URL must be a valid YouTube or direct HLS/MP4 link." | A channel link, a plain `http://` link, or a malformed URL | Use an `https` YouTube watch/live URL or an `https` `.m3u8` / `.mp4` URL |

### 6.4 Exhibitors — `/admin/exhibitors`

> Page reference: [`docs/pages/cp/admin-exhibitors.md`](../pages/cp/admin-exhibitors.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-exhibitors.md`](../tests/e2e/cp-admin-exhibitors.md)

The register of exhibiting companies. Exhibitors are created here and nowhere
else: there is no self-signup. Booths (§6.5) point at an exhibitor, and this is
also where a booth's staff get the app login that lets them scan visitor badges.
The list shows the English name with its logo, the Arabic name, how many
**Accounts** the exhibitor has, and Active.

The toolbar carries the usual **Add / Edit / Details / Delete / Export /
Import**, plus a toggle that switches those forms between a popup and a full
page (**Open as full page** / **Open as dialog**). Your choice is remembered per
page in your browser.

#### Most common tasks

##### Add an exhibitor

1. **Exhibition → Exhibitors** → **Add**.
2. **Name (English)** and **Name (Arabic)**, both required, up to 256 each.
   Both are stored and the app shows the one matching the reader's language.
3. **Contact email** (up to 320), **Contact phone** (up to 32), **Website**
   (up to 512).
4. **Tier** (Premium, Gold, Silver, Bronze, or leave it at `— none —`).
5. **Contact information** section: secondary phone, country, city in English
   and Arabic, Facebook / X / LinkedIn / Instagram URLs, and Latitude +
   Longitude. The two coordinates are all or nothing.
6. **Save**.
7. Re-open **Edit** to upload the **logo**. The uploader appears only on a row
   that already exists.

##### Give a booth team their app logins

1. Per-row **Accounts** icon (needs Exhibitors → Edit). The modal lists the
   current accounts: contact name, email, role, active.
2. **Provision an account** creates a brand-new login: **Contact name** and
   **Email** are required, **Role label** is optional. It lands pending
   approval, confirmed by *"Account provisioned. It is pending approval."*
3. **Link an existing account** attaches a login somebody already created (for
   example on the Others page) to this exhibitor. Only the **Account email** is
   required. This needs the Exhibitors → Link account permission, because it
   hands that person the booth's badge-scanning and visitor-card tools.

##### Deactivate an exhibitor

**Delete** opens the read-only detail with a **Deactivate** button and a
confirmation. It is a soft delete: the exhibitor leaves the public list at once
and can be brought back by editing it and ticking Active.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Both the English and Arabic names are required." | One of the two name boxes is empty | Fill both |
| "The contact name and email are both required." | Provisioning was submitted with a gap | Fill both boxes and provision again |
| "No account is registered under this email." | The Link form was given an unknown address | Check the spelling, or create the account first |
| "This account does not carry an exhibitor profile type. Assign it an exhibitor profile type before linking it to a booth." | The account exists but is not an exhibitor-type account | Set an exhibitor profile type on it, then link again |
| "This account already belongs to an exhibitor. Remove it from that exhibitor before linking it here." | One account cannot serve two exhibitors | Unlink it from the other exhibitor first |
| "No active exhibitor profile type exists. Create a partner profile type whose mobile app role is Exhibitor before provisioning booth accounts." | The system has no exhibitor profile type to assign | Create one under profile types, then provision |
| "The exhibitor is not active; reactivate it before adding accounts." | The exhibitor was deactivated | Edit it, tick Active, then add the account |

### 6.5 Booths — `/admin/booths`

> Page reference: [`docs/pages/cp/admin-booths.md`](../pages/cp/admin-booths.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-booths.md`](../tests/e2e/cp-admin-booths.md)

The exhibition floor. Each booth carries a code, a bilingual name, the exhibitor
staffing it (§6.4), a sector, an optional hall (§5.2) and an optional map
position. The public exhibition list and the app's booth cards read this page,
and a venue-map node (§6.7) can point at a booth. The page title reads
**Exhibition Booths**.

#### Most common tasks

##### Add a booth

1. **Exhibition → Booths** → **Add**.
2. **Code** (2 to 16 characters, unique, stored uppercase). This is the value
   an Excel import matches on, so keep it stable.
3. **Name (English)** and **Name (Arabic)**, both required, up to 128 each.
   Both are stored.
4. **Exhibitor** — only active exhibitors are listed; `— No exhibitor —` leaves
   it unassigned.
5. **Booth officer** block: name, Arabic name, phone, email, secondary phone,
   city in English and Arabic, country, website, social links, and the
   latitude / longitude pair. This is the person to call when the booth needs
   something.
6. **Sector (English / Arabic)** up to 128 each, **Description (English /
   Arabic)** up to 2048 each.
7. **Hall** (`— No hall —` if none), then **Map X position** and
   **Map Y position**, the 2D position the app uses to place the booth.
8. **Create booth**. Re-open **Edit** to upload the booth logo.

##### Bulk load booths from a spreadsheet

**Export** writes a sheet with Code, Name, NameArabic, Exhibitor (its English
name), Sector, Hall (its code) and IsActive. **Import** reads the same sheet
back and needs the **Code**, **Name** and **NameArabic** columns. Import only
creates new booths; it never updates an existing one, and it deliberately
leaves the officer fields and the map position blank. Set those with Edit.

##### Deactivate a booth

**Delete** opens the detail with a Delete button and a confirmation. It is a
soft delete: the booth disappears from the public exhibition list and the venue
map straight away.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "A booth with code 'X' already exists." | Codes are unique, ignoring case | Pick a different code |
| "Code and both names (English and Arabic) are required." | A required box is empty | Fill code and both names |
| "Booth code must be between 2 and 16 characters." | The code is too short or too long | Shorten or lengthen it |
| "Booth officer email is not a valid email address." | The officer email is malformed | Correct the address, or clear the box |
| "This booth is still marked on the venue map and cannot be deactivated. Remove its venue-map node first." | A venue-map node points at this booth | Delete that node on Venue map (§6.7), then deactivate |
| "Exhibitor id '…' is not an active exhibitor." | The chosen exhibitor was deactivated meanwhile | Reactivate the exhibitor, or pick another |
| Import row error "No active exhibitor named 'X' was found." | The Exhibitor cell does not match an active exhibitor's English name exactly | Correct the cell, or leave it blank and set it with Edit |

### 6.6 Sponsors — `/admin/sponsors`

> Page reference: [`docs/pages/cp/admin-sponsors.md`](../pages/cp/admin-sponsors.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-sponsors.md`](../tests/e2e/cp-admin-sponsors.md)

The sponsor wall. The public screen groups the logos by tier, Platinum first,
then Gold, Silver and Bronze, and inside a tier orders them by **Display
order** and then by Arabic name. So the two fields that decide where a logo
lands are Tier and Display order.

#### Most common tasks

##### Add a sponsor

1. **Exhibition → Sponsors** → **Add**.
2. **Name (English)** and **Name (Arabic)**, both required, up to 256 each.
   Both are stored.
3. **Tier** (Platinum, Gold, Silver, Bronze).
4. **Link** (up to 512), the sponsor's own site, shown as a clickable link in
   the grid.
5. **Tagline (Arabic / English)** up to 256 each, and **About (Arabic /
   English)** up to 2048 each, both optional and both bilingual.
6. **Display order**, 0 to 99999. Lower sorts first inside the tier.
7. **Contact information** section, all optional: **Country**, **Email** (320),
   **Phone** and **Secondary phone** (32 each), **City** and **City (Arabic)**
   (128 each), **Facebook URL**, **X (Twitter) URL**, **LinkedIn URL** and
   **Instagram URL** (256 each), and **Latitude** + **Longitude**. The two
   coordinates are all or nothing: fill both or neither.
8. **Create sponsor**, then re-open **Edit** to upload the logo image.

##### Bulk load sponsors

**Export** writes the sponsor sheet; **Import** reads it back and needs the
**NameEn**, **NameAr** and **Tier** columns. Import only creates new sponsors.

##### Deactivate a sponsor

**Delete** opens the detail with a Delete button. The confirmation says the
sponsor leaves the public sponsors list immediately and can be reactivated
later by editing it.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "An active sponsor named 'X' already exists in this tier." | Arabic name plus tier must be unique among active sponsors | Change the tier, or check whether the sponsor is already there |
| "Both the English and Arabic names are required." | One name box is empty | Fill both |
| "Sponsor tier is not a recognised value." | The tier was not one of the four | Pick a tier from the dropdown |
| "Display order must be zero or a positive integer." | A negative number was typed | Use 0 or higher |
| "URL must be 512 characters or fewer." | The link is too long | Shorten it, or use a redirect |
| The grid shows initials instead of the logo | The thumbnail comes from the uploaded logo image, not from the **Logo path** text box | Open **Edit** and upload the image file |

### 6.7 Venue map — `/admin/venue-map`

> Page reference: [`docs/pages/cp/admin-venue-map.md`](../pages/cp/admin-venue-map.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-venue-map.md`](../tests/e2e/cp-admin-venue-map.md)

The 2D map the app draws. Each row is one **node**: a bilingual label, a kind, a
position, and optionally the hall or booth it marks. The page ships empty and
the logistics team places the nodes. The grid shows Label, Kind, Position as
`x, y`, and Active.

#### Most common tasks

##### Place a node

1. **Exhibition → Venue map** → **Add**.
2. **Label (English)** and **Label (Arabic)**, both required, up to 128 each.
   Both are stored and the app shows the reader's language.
3. **Kind** — `Hall`, `Zone`, `Booth` or `PointOfInterest`.
4. **X position** and **Y position** in relative units, which the app scales to
   its own canvas. Keep one scale across all nodes or the map will look wrong.
5. **Linked hall (optional)** or **Linked booth (optional)**. Link a hall only
   on a Hall node and a booth only on a Booth node, and never both on one node.
6. **Create node**.

##### Move or retire a node

**Edit** changes the position or the link; the Edit form also carries the
**Active** tick. **Delete** opens the detail with a Delete button and a
confirmation that says the node disappears from the app's 2D map immediately
and can be brought back by editing it.

##### Bulk load nodes

**Export** writes the node sheet; **Import** reads it back and needs the
**Label**, **LabelArabic** and **Kind** columns. Import only creates new nodes.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Both labels are required." | One label box is empty | Fill both labels |
| "Both labels are required and must be 1–128 characters." | A label is too long | Shorten it to 128 characters |
| "A hall reference requires kind Hall, a booth reference requires kind Booth, and a node cannot reference both." | The kind and the link disagree, or both links were set | Match the kind to the link, and clear the other one |
| "The referenced hall was not found." | The linked hall was deleted meanwhile | Pick another hall, or clear the link |
| "The referenced booth was not found." | The linked booth was deleted meanwhile | Pick another booth, or clear the link |
| A booth on §6.5 refuses to deactivate | A node here still points at that booth | Delete the node, then deactivate the booth |

#### What you cannot do here yet

- No floor-plan canvas: positions are typed as numbers, not clicked or dragged.
- There is no preview of how the app will draw the map, so check the app after
  a batch of changes.

---

## 7. Engagement & Knowledge modules

The **Engagement** sidebar group holds what attendees send back to you (ratings and
the questions they are asked). The **Knowledge & AI** group holds the FAQ the app
shows and the AI control centre that runs every AI feature in the platform.

### 7.1 Ratings — `/admin/ratings`

> Page reference: [`docs/pages/cp/admin-ratings.md`](../pages/cp/admin-ratings.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-ratings.md`](../tests/e2e/cp-admin-ratings.md)

Every rating an attendee submits from the mobile app lands here. The page is
**read-only**: a response belongs to the attendee who submitted it, so there is no
Add, Edit or Delete. What attendees are asked, and which things can be rated at
all, is set on **Rating configuration** (§7.2). Needs the **Ratings → View**
permission; **Export** additionally needs **Ratings → Export**.

#### Most common tasks

##### Read the headline numbers

1. **Engagement → Ratings**.
2. **Average rating** is the mean overall score, to one decimal, over the
   responses currently matched by your filter. **Total ratings** is how many
   responses that is.
3. **Ratings by type** below shows one card per active rating type with that
   type's average and an "N responses" line. These cards are loaded once when the
   page opens and do **not** follow your grid filter.

##### Find one response

1. Columns are **Type**, **Target**, **Overall**, **Answers**, **Comment**,
   **Active** and **Submitted at**.
2. **Overall** and **Submitted at** sort. **Comment** carries a column search box,
   type a word and the grid keeps only responses whose comment contains it.
3. **Target** is the id of the session or programme day being rated. Types with
   global scope (App, Event, Exhibition) show a dash, they rate the whole event
   and have nothing to point at.
4. **Submitted at** is Saudi local time, 12-hour clock.

##### Export the responses to Excel

1. Tick the rows you want, or leave every row unticked to export the whole
   current filtered set.
2. **Export**. The spreadsheet downloads through the browser.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Could not load ratings. Please try again." | The ratings request failed | Reload the page; if it repeats, the API is unreachable, tell the team |
| "The export could not be generated (HTTP 403)." after clicking Export | You have Ratings → View but not Ratings → Export | Ask an administrator for the Ratings → Export permission |
| "The export could not be generated (HTTP 500)." | The spreadsheet could not be built | Narrow the selection and retry |
| Target column is a long id, not a session name | The grid stores the target id only | Match the id on `/admin/sessions`, or use the Ratings report |
| Every row's Active column says Active | The list returns active responses only | Expected, not a fault |
| A rating type is missing from "Ratings by type" | The type has been deactivated. A type nobody has rated still gets a card, reading 0.0 with "0 responses" | Reactivate the type on `/admin/rating-config` |

#### What you cannot do here yet

- Create, edit or delete a response.
- See who submitted a response, the grid carries no attendee column.
- Change the questions or the star scale, that is §7.2.

### 7.2 Rating configuration — `/admin/rating-config`

> Page reference: [`docs/pages/cp/admin-rating-config.md`](../pages/cp/admin-rating-config.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-rating-config.md`](../tests/e2e/cp-admin-rating-config.md)

This is where you decide what the app asks attendees. It has three levels: a
**rating type** (what is being rated), its **question groups** (headings on the
form), and its **questions** (the individual star rows). Everything you build here
appears on the app's rating form, and every response then shows up on §7.1. Needs
**RatingConfig → View** to open; Add needs **Create**, Edit needs **Edit**, Delete
needs **Delete**.

Five types ship built in and are marked **Built-in = Yes**: App, Session, Day,
Event and Exhibition. Built-in types cannot be deleted and their Code and Scope
are locked, but you can still rename them and change their questions.

#### Most common tasks

##### Add a rating type

1. **Engagement → Rating configuration** → **Add** on the **Rating types** grid.
2. **Code (slug)** is your internal identifier, up to 64 characters, must be
   unique, and can never be changed after you save.
3. **Name (English)** and **Name (Arabic)**, both required, up to 128 characters
   each. Both are stored, the app shows whichever matches the attendee's language.
4. **Scope** decides what the type attaches to and cannot be changed later:
   **Global (once per user)**, **Per session**, or **Per programme day**.
5. **Show overall star rating** puts the big 1 to 5 star row at the top of the form.
6. **Allow a comment** adds the free-text box; **Comment label (English)** and
   **Comment label (Arabic)** caption it, up to 128 characters each, optional.
7. **Display order** sorts the types, zero or higher.
8. **Save**. A green *"Saved."* confirms it.

##### Add question groups and questions

1. On the type's row click the **Manage** icon. Two more grids appear underneath,
   headed with that type's name.
2. **Add group** on the groups grid: **Name (English)** and **Name (Arabic)**,
   both required, up to 128 characters, plus **Display order**.
3. **Add question** on the questions grid: **Question (English)** and **Question
   (Arabic)**, both required, up to 512 characters, a **Group** (or *(no group)*
   to leave it ungrouped), **Required to submit**, and **Display order**.
4. Reload the app's rating form to see the new rows.

##### Edit or deactivate

Per-row **Edit** re-opens the same dialog with an **Active** checkbox, so you can
retire a question without losing its answers. Per-row **Delete** asks first:
*"Delete this rating type?"* warns that the type *"will be deactivated, together
with its question groups and their questions."* Groups and questions get their own
confirmations. Everything is a deactivation, nothing is erased, and existing
responses stay on §7.1.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "A rating type with this code already exists." | Codes must be unique | Pick another code |
| "Built-in rating types can't be deleted." | The row shows Built-in = Yes | Leave the type in place and deactivate its questions instead |
| "A code and both names (English and Arabic) are required." | A field was left blank in the type dialog | Fill Code, Name (English) and Name (Arabic) |
| "Both group names (English and Arabic) are required." | Blank group name | Fill both names |
| "Both question texts (English and Arabic) are required." | Blank question text | Fill both texts |
| "The English name must be 128 characters or fewer." | Over the field limit | Shorten it |
| "Display order must be zero or a positive integer." | A negative order | Use 0 or higher |
| The Code and Scope boxes are greyed out | You are editing, not creating, both are fixed at creation | Create a new type if you need a different code or scope |
| No Active checkbox in the type dialog | Built-in types cannot be deactivated | Expected |
| "Could not load rating configuration. Please try again." | The request failed | Reload; a repeat means the API is down |

### 7.3 FAQ groups & entries — `/admin/faq`

> E2E catalogue: [`docs/tests/e2e/cp-admin-faq.md`](../tests/e2e/cp-admin-faq.md)

The frequently-asked-questions accordion in the mobile app is built from this
page. It has two levels: a **group** (the accordion heading) and the **entries**
(question and answer pairs) inside it. Only active groups and active entries are
published, and they come out ordered by Display order. Needs **FAQ → View** to
open; Add needs **Create**, Edit needs **Edit**, Delete needs **Delete**.

#### Most common tasks

##### Add a group

1. **Knowledge & AI → FAQ groups & entries** → **Add** on the **FAQ groups** grid.
2. **Name (English)** and **Name (Arabic)**, both required, up to 128 characters
   each. Both are stored, the app shows the one matching the reader's language.
3. **Display order** sorts the accordion, zero or higher.
4. **Save**.

##### Add a question and answer

1. Click the **Manage entries** icon on the group's row. The entries grid appears
   below, headed with the group name.
2. **Add entry**.
3. **Question (English)** and **Question (Arabic)**, both required, up to 512
   characters each.
4. **Answer (English)** and **Answer (Arabic)**, both required, up to 4000
   characters each.
5. **Display order**, then **Save**.

##### Hide something without deleting it

Per-row **Edit** shows an **Active (visible)** checkbox. Untick it and the entry
or the whole group disappears from the app on its next load. The per-row **Delete**
icon does the same thing but asks first: *"Delete this FAQ group?"* warns that the
group *"and every question inside it will be deactivated."*

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "An English and an Arabic group name are both required." | One of the two names is blank | Fill both |
| "The question and the answer are required in both English and Arabic." | One of the four entry fields is blank | Fill all four |
| "FAQ English answer must be 4000 characters or fewer." | The answer is too long | Shorten it, or split it into two entries |
| "Display order must be zero or a positive integer." | A negative order | Use 0 or higher |
| "The FAQ group was not found." | The group was deleted in another tab | Reload the page |
| "Could not complete the request. Please try again." | The request failed | Reload; a repeat means the API is unreachable |
| A new entry does not show in the app | The entry or its group is inactive, or the app has a cached copy | Check both Active flags, then pull to refresh the app |
| The entries grid disappeared | You deactivated the group you had selected | Pick another group's Manage entries icon |

#### What you cannot do here yet

- Move an entry from one group to another. Recreate it under the target group.
- Attach an image or a link, the answer is plain text.

### 7.4 AI dashboard — `/admin/ai`

> Page reference: [`docs/pages/cp/admin-ai-dashboard.md`](../pages/cp/admin-ai-dashboard.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-ai-dashboard.md`](../tests/e2e/cp-admin-ai-dashboard.md)

The health check for everything AI in SIMF. It is read-only and always covers the
**last 24 hours**, marked on the page itself. Open it first when someone reports
that translation, the assistant, or the session summary has stopped working. Needs
the **AI dashboard → View** permission.

#### Most common tasks

##### Check whether AI is healthy right now

1. **Knowledge & AI → AI dashboard**.
2. Five cards: **Calls**, **Error rate**, **Avg latency**, **Tokens** and **Active
   services** (shown as active over total).
3. The table underneath breaks the same window down per service, one row per AI
   feature, with **Calls**, **Errors** (count and percentage), **Avg latency** and
   **Tokens**.
4. A service with a high error count is your culprit. Take its name to §7.5 to see
   which provider and model it is pointed at, and to §7.7 to read the actual
   failures.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "No AI calls in this window yet." | Nothing has called AI in the last 24 hours | Normal outside the event; run a prompt test from §7.6 to prove the plumbing works |
| "Could not load the AI dashboard." | The dashboard request failed | Reload; a repeat means the API is unreachable |
| Error rate is 100% for one service | That service's provider is unreachable or unconfigured | Open §7.7, read the error code on the failing rows, then check the routing in §7.5 |
| Numbers look stale | The page loads once, it does not auto-refresh | Reload the page |

#### What you cannot do here yet

- Change the 24-hour window, it is fixed.
- Drill into an individual call, use §7.7.

### 7.5 AI services — `/admin/ai/services`

> Page reference: [`docs/pages/cp/admin-ai-services.md`](../pages/cp/admin-ai-services.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-ai-services.md`](../tests/e2e/cp-admin-ai-services.md)

One row per AI service, answering "what runs the translation AI, and where does the
data go?" from a single screen. Each row shows the service, the prompt currently
serving it, the provider and model behind that prompt, and a hosting badge. Needs
**AI prompts → View** to open; the routing edit needs **AI prompts → Edit**.

The **Hosting** badge is the data-residency signal: **Offline** for the built-in
echo provider (no outbound call at all), **OpenAI API** for OpenAI (which may be
the on-premises endpoint, so it is never flagged), and **Cloud** for the external
services. A red **Residency risk** badge appears when a service that handles real
session or defence content (session summary, assistance, live translation, live
sign language) is pointed at an external cloud provider.

#### Most common tasks

##### See which provider a service uses

1. **Knowledge & AI → AI services**.
2. Read the row: **Active prompt** shows the prompt key and its version, or a grey
   **None active** pill when the service has prompts but none switched on.
3. **Service** sorts and has a search box, **Provider** and **Prompts** sort.

##### Repoint a service at a different provider or model

1. Click the **Configure routing** (pencil) icon on the row. It only appears when
   the service has an active prompt and you hold **AI prompts → Edit**.
2. In the dialog set **Provider**, **Model**, **Temperature (0.0 to 2.0)** and
   **Max output tokens (1 to 8000)**.
3. **Save routing**. A green *"Routing updated."* confirms it and the grid reloads.

##### Open a service's own page

Click the **Open** (eye) icon to reach `/admin/ai/services/{feature}`, which has
three tabs: **Routing** (the same editor inline), **Prompts** (every prompt for
this service with key, provider, model, version and active state) and
**Analytics** (that service's 24-hour figures from §7.4).

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "None active" in the Active prompt column | The service has prompts but none is marked Active | Open §7.6, edit one of its prompts and tick Active |
| No Configure routing icon on a row | Either the service has no active prompt, or you lack the AI prompts → Edit permission | Activate a prompt first, or ask for the permission |
| "This service has no active prompt to configure." on the Routing tab | Same cause, seen from the detail page | Same fix |
| A red "Residency risk" badge | A sensitive service is routed to an external cloud provider | Repoint it at the on-premises provider unless the routing was signed off |
| "Unknown AI service." | The address bar carries a service name that does not exist | Go back to the services list and click through |
| "Could not load AI services." | The prompt catalogue request failed | Reload the page |

#### What you cannot do here yet

- Create or delete a prompt, that is §7.6.
- Edit the prompt wording, routing only changes provider, model, temperature and
  the token cap.

### 7.6 AI prompts — `/admin/ai/prompts`

> Page reference: [`docs/pages/cp/admin-ai-prompts.md`](../pages/cp/admin-ai-prompts.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-ai-prompts.md`](../tests/e2e/cp-admin-ai-prompts.md)

The catalogue of every instruction SIMF sends to an AI provider. One prompt drives
one service, and the services console (§7.5) simply groups this list by service.
Editing a prompt changes live behaviour for the app and the Control Panel, so treat
it as a production change. Needs **AI prompts → View** to open, plus **Create**,
**Edit**, **Delete**, **Test**, **Export** or **Import** for each action.

#### Most common tasks

##### Create a prompt

1. **Knowledge & AI → AI prompts** → **Add**.
2. **Key (kebab-case slug, immutable)** is how code asks for this prompt: 2 to 64
   characters, lowercase letters, digits and hyphens only. It can never be changed.
3. **Feature** picks which service the prompt serves.
4. **Display name (English)** and **Display name (Arabic)**, both required, up to
   128 characters each. Both are stored.
5. **Provider** and **Model** decide where the call goes. Model is up to 64
   characters and must match a model the provider actually offers.
6. **System prompt** and **User prompt template** are the instructions themselves,
   1 to 8000 characters each, so neither may be left empty. The user template
   takes `{placeholder}` substitutions, filled in at call time.
7. **Temperature (0.0 to 2.0)** and **Max output tokens (1 to 8000)**.
8. **Save**. A green *"Prompt saved."* confirms it.

The Add, Edit, Details and Deactivate forms open either as a popup or as a full
page. The toggle sits in the grid toolbar and your choice is remembered.

##### Test a prompt before trusting it

1. Click the **Test** (flask) icon on the row. The icon is hidden unless you hold
   **AI prompts → Test**.
2. Type the placeholder values into **Inputs**, one per line, as `key=value`.
3. **Run test**. The dialog shows **Output**, **Latency** and **Tokens**. Nothing
   is saved to the prompt, but the call is recorded in §7.7.

##### See what changed and when

Click the **History** (clock) icon for the append-only version list: version,
provider, model, whether it was active, when it was authored, and a shortened
content hash you can compare against another version.

##### Deactivate, export, import

Per-row **Delete** opens the details with a **Deactivate** button and a
confirmation: *"Deactivate the AI prompt 'key'? It will no longer be served to
features."* **Export** downloads the catalogue as a spreadsheet and **Import**
takes one back in, reporting *"N created, N updated, N skipped."*

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "A key is required." | The Key box was left empty when creating | Type a key |
| "Key must be 2–64 chars, kebab-case (a-z, 0-9, -)." | Capitals, spaces, underscores or a bad length | Rewrite as `question-filter` style |
| "AI prompt key 'x' is already in use." | Keys are unique | Choose a different key |
| "Display name (English and Arabic) is required." | One of the two names is blank | Fill both |
| "SystemPrompt must be between 1 and 8000 characters." | The system prompt is blank or too long | Enter text, keep it under 8000 characters |
| "Temperature must be between 0 and 2." | Out of range | Use a value from 0.0 to 2.0 |
| The Key box is greyed out | You are editing, keys are permanent | Create a new prompt if you need a different key |
| Test returns "OpenAI provider is not configured." | The provider has no API key on this deployment | Route the prompt at a configured provider, or ask the team to configure it |
| Test returns "AI provider 'X' is not registered." | The prompt points at a provider this build does not run | Pick another provider in the Edit form |
| No Test icon on any row | You lack the AI prompts → Test permission | Ask an administrator |
| "The file is not a valid Excel workbook." | The uploaded file is not a readable .xlsx | Export a fresh file, edit that, and import it again |

### 7.7 AI invocations — `/admin/ai/invocations`

> E2E catalogue: [`docs/tests/e2e/cp-admin-ai-invocations.md`](../tests/e2e/cp-admin-ai-invocations.md)

The append-only log of every AI call the platform has made, from the app, from the
Control Panel and from the Test button on §7.6. It is the evidence trail when
someone says "the AI gave a wrong answer", and the fastest way to see why a service
on the dashboard is failing. Nothing here can be edited or deleted. Needs the
**AI invocations → View** permission.

#### Most common tasks

##### Find the failures

1. **Knowledge & AI → AI invocations**.
2. Tick **Errors only** in the toolbar. The grid reloads showing only calls that
   failed, and the toggle survives sorting and paging.
3. The **Error** column carries the error code, for example
   `AI_PROVIDER_NOT_CONFIGURED`, which tells you whether the provider was
   unreachable, unconfigured, or refused the request.

##### Trace one call

1. Columns are **Time**, **Prompt key**, **Feature**, **Provider**, **Caller**,
   **Latency**, **Tokens (in/out)** and **Error**. Time is Saudi local, 12-hour
   clock, and every column except Tokens and Error sorts.
2. **Prompt key** and **Caller** have search boxes, so you can pull up every call
   made by one prompt or one kind of caller.
3. Click the **Detail** (eye) icon for the full record: prompt key, feature,
   provider and model, caller, latency, tokens, the error code if any, the
   **Input (redacted)** payload and the **Output**.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "No invocations recorded yet." | Nothing has called AI, or your Errors only filter excludes everything | Untick Errors only; if still empty, run a test from §7.6 |
| "Could not load invocations." | The log request failed | Reload the page |
| Parts of the Input are masked | The panel is labelled "Input (redacted)", secrets and personal data are stripped before the payload is stored | Expected, the raw values are not recoverable from the log |
| Output is a dash | The call failed before the provider answered | Read the error code in the same panel |
| A row you expected is missing | The log is written after the call completes, and the grid does not auto-refresh | Reload the page |

#### What you cannot do here yet

- Edit or delete a log row, the log is append-only by design.
- Re-run a failed call from this page, re-run it from the Test dialog in §7.6.

---

## 8. Content & Public relations modules

The eleven modules below sit in two nav groups: **Content** (what the public
website and the mobile app display) and **Public relations** (who you invite,
who you broadcast to, and who wrote in). All of them follow the canonical CRUD
list pattern (§3.1) unless the chapter says otherwise, and every bilingual field
stores **both** the English and the Arabic value: the app and website pick the
one that matches the reader's language, so leaving the Arabic side blank leaves
Arabic readers with an empty box.

### 8.1 Media library — `/admin/media-library`

> Page reference: [`docs/pages/cp/media-library.md`](../pages/cp/media-library.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-media-library.md`](../tests/e2e/cp-admin-media-library.md)

One place to see **every** image the system holds, no matter which module
uploaded it: speaker photos, company and sponsor logos, media-partner logos,
archive covers, news images, booth logos, banner heroes. You do not create rows
here. Each row already belongs to a record somewhere else, and this page exists
so you can find a wrong or unwanted picture quickly and take it off the public
site without hunting through the owning module.

#### Most common tasks

##### Find a picture and see where it came from

1. **Content → Media library**.
2. The grid shows **Category** (which module owns it), **Owner** (the record's
   name), **Preview**, **Kind**, **Source** and **Active**.
3. **Source** reads either **Uploaded file** (bytes stored by SIMF) or
   **External link** (a URL someone pasted).
4. Click **Manage** on the row to open **Asset details**, which shows a large
   preview and, for an external link, the clickable URL.

##### Take a picture off the public site

1. Open **Manage** on the row.
2. **Deactivate**. A confirmation asks *"Deactivate the asset for "X"? It stops
   appearing on the public site until it is restored."*
3. Confirm. A green *"Asset deactivated."* appears and the row turns **Inactive**.

##### Put it back

Open **Manage** on the inactive row and press **Restore**. You get
*"Asset restored."*

Deactivate and Restore need the **Media library → Manage** permission. With
**Media library → View** only, you can browse and open the details, but the
Deactivate and Restore buttons are not rendered.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Could not load media assets." | The list, the details fetch, the deactivate or the restore call failed | Reload the page. If it repeats, the API is unreachable, tell the team |
| Preview box is empty but the row exists | The record has an asset row with no readable bytes, or the external link is dead | Re-upload the picture from the owning module's Edit form |
| No Deactivate or Restore button in the details | Your role has Media library → View but not Manage | Ask an administrator for the Manage permission |
| The picture is still on the public site | The app or website page was cached in the visitor's client | Pull to refresh in the app, hard-refresh the browser |

#### What you cannot do here yet

- Upload a new picture. Uploads always happen on the owning record's Edit form
  (a speaker, a news article, a banner, and so on).
- Replace one picture with another from this page.
- Permanently erase bytes. Deactivate is a soft hide, not a delete.

### 8.2 Content blocks — `/admin/content-blocks`

> Page reference: [`docs/pages/cp/admin-content-blocks.md`](../pages/cp/admin-content-blocks.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-content-blocks.md`](../tests/e2e/cp-admin-content-blocks.md)

The public website's editable text. Each block is a **Key** (a dotted name such
as `home.welcome.title`) plus its English and Arabic text. The website's landing
hero and its editorial sections read these keys by name, so a block only shows
up on the site if the site is already asking for that exact key. A key the site
does not ask for is stored harmlessly and displayed nowhere.

#### Most common tasks

##### Change a piece of website copy

1. **Content → Content blocks**.
2. Find the row by its **Key** (the Key column is sortable and filterable).
3. Per-row **Edit**. The **Key** field is greyed out on Edit, you can only change
   the text.
4. **Content (English)** and **Content (Arabic)**: up to 8000 characters each.
   Where the site has both, it shows the Arabic to Arabic readers and the English
   to English readers. If one side is empty, the site falls back to the other.
5. **Save**. A green *"Content block saved."* confirms it.

##### Add a new block

**+ Add** opens **New content block**. Type the **Key (e.g. home.welcome.title)**,
2 to 128 characters, then the two texts, then **Save**. Keys are stored trimmed
and lower-cased, so `Home.Welcome` and `home.welcome` are the same block. Saving
an existing key overwrites that block rather than creating a duplicate.

##### Remove a block

Per-row **Delete** opens the details with a **Delete** button and a confirmation
reading *"Delete the content block "X"? This removes it from the site."* The
website then falls back to its built-in wording for that key.

Everything on this page is gated: **Content blocks → View** to open it, **Edit**
to add or change, **Delete** to remove, **Export** and **Import** for the
spreadsheet buttons.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Content block key must be between 2 and 128 characters." | Key blank, too short or too long | Shorten the key, keep it dotted and descriptive |
| "Content cannot exceed 8000 characters." | English or Arabic text too long | Trim the text, long copy belongs in a News article |
| "A key is required (up to 128 characters)." | You pressed Save with an empty Key on Add | Fill the Key |
| "Content block not found." | Someone deleted the row while you had the form open | Close the form, reload the list |
| "The content blocks could not be loaded." | The list call failed | Reload; if it repeats the API is unreachable |
| Saved text does not appear on the website | The site does not read that key | Check the key spelling against an existing working block |

### 8.3 Banners — `/admin/banners`

> Page reference: [`docs/pages/cp/admin-banners.md`](../pages/cp/admin-banners.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-banners.md`](../tests/e2e/cp-admin-banners.md)

Time-windowed hero announcements. Each banner has a bilingual title and body, a
start and an end, and a display order. The public surfaces only show banners that
are **Active** and inside their window right now, so a banner you create for next
week stays invisible until its start time passes. The app's home hero rotates
through the uploaded banner images in display order.

#### Most common tasks

##### Create a banner

1. **Content → Banners** → **+ Add**.
2. **Title (English)** and **Title (Arabic)**: both required, up to 256
   characters each.
3. **Body (English)** and **Body (Arabic)**: both required, up to 2000
   characters each.
4. **Image URL**: optional. A link to a picture hosted elsewhere. Leave it blank
   if you intend to upload the image instead (see below).
5. **Click-through URL**: optional. Where a tap on the banner takes the reader.
6. **Start (Saudi time)** and **End (Saudi time)**: enter Saudi local time, not
   UTC. A new banner defaults to now until this time tomorrow.
7. **Display order**: 0 or higher, lower numbers appear first.
8. **Save**.

##### Attach the hero image

The image upload only appears once the banner row exists, so create it first,
then re-open it with **Edit**. Under **Image** you get two buttons,
**Upload file** and **External link**:

- **Upload file**: pick **Kind** (Image or Document), choose a `.png`, `.jpg`,
  `.webp` or `.pdf` file, then **Upload**.
- **External link**: pick **Kind**, paste an `https://...` **URL**, then
  **Save link**.

##### Take a banner down early

Per-row **Delete** shows the details with a **Deactivate** button and the
confirmation *"Deactivate the banner "X"? It will stop showing on the public
site."* This is a soft hide, the row stays in the list marked Inactive and the
**Active** checkbox on the Edit form brings it back.

Gates: **Banners → View** to open, **Create**, **Edit**, **Delete**, **Export**
and **Import** for the matching buttons.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Banner title (EN + AR) must be between 1 and 256 characters." | One title missing or too long | Fill both titles |
| "Banner body (EN + AR) must be between 1 and 2000 characters." | One body missing or too long | Fill both bodies |
| "Banner end must be after its start." | End is on or before Start | Re-enter the window, both in Saudi time |
| "Display order must be zero or a positive integer." | A negative order in an imported spreadsheet. The Add and Edit form cannot raise this, it turns any unreadable or negative entry into 0 before sending | Correct the display-order column in the workbook and import again |
| "Please complete the required fields." | Titles blank, or a date the browser could not read | Re-pick the dates from the picker |
| "Banner not found." | The row was deleted while your form was open | Reload the list |
| Banner is Active but nobody sees it | Today is outside its Start to End window | Widen the window |
| No image upload box on the form | You are on **Add**, not **Edit** | Save the banner, re-open it with Edit |

### 8.4 Media Center — `/admin/media`

> Page reference: [`docs/pages/cp/admin-media.md`](../pages/cp/admin-media.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-media.md`](../tests/e2e/cp-admin-media.md)

The public photo and video gallery. Each item is either an **Image** (a picture
you upload into SIMF) or a **Video** (a link to a video hosted elsewhere), with
an optional bilingual title and an optional bilingual **Album** name that groups
items together in the gallery.

#### Most common tasks

##### Add a video

1. **Content → Media Center** → **+ Add**.
2. **Type**: **Video**.
3. **Title (English)** / **Title (Arabic)** and **Album (English)** /
   **Album (Arabic)**: all optional, up to 200 characters each.
4. **Video URL**: required for a video, up to 2048 characters.
5. **Display order**: 0 or higher.
6. **Save**. The form closes and the row appears.

##### Add an image

Same form with **Type: Image**. Because bytes cannot be attached before the row
exists, the form shows *"Save the item first, then attach its image from the
Edit screen."* and **Save** keeps the form open in edit mode instead of closing
it. You then get an **Image file** picker that accepts `.png`, `.jpg` and
`.webp`, and an **Upload image** button that enables once you have chosen a file.
The hint above the picker reads *"No image attached yet."* before the upload and
*"An image is attached to this item."* after it.

The grid's **Image** column shows Active for an item that has bytes and Inactive
for one that does not, which is the fastest way to spot an image tile that will
render empty in the app.

**Type** cannot be changed after the item is created, the selector is disabled on
Edit. Create a new item instead.

##### Remove an item

Per-row **Delete** confirms with *"Delete "X"? It will be removed from the public
gallery."*

Gates: **Media → View**, **Create**, **Edit**, **Delete**, **Export**, **Import**.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "A video item requires a playback URL." | Type is Video and the URL box is empty | Paste the playback link |
| "A video media item requires a URL." | Same, rejected by the server | Same |
| "Display order must be zero or positive." | A negative order | Use 0 or higher |
| "English title must be 200 characters or less." | A title, album or URL over its limit (200 / 200 / 2048) | Shorten it |
| "The image could not be uploaded." | The file was rejected or the connection dropped | Re-pick the file, use PNG / JPG / WEBP |
| "The media list could not be loaded." | The list call failed | Reload the page |
| Image tile is blank in the app | The item has no bytes yet | Check the **Image** column, re-open Edit and upload |
| Upload button stays greyed | No file picked yet | Choose a file first |

### 8.5 News — `/admin/news`

> Page reference: [`docs/pages/cp/admin-news.md`](../pages/cp/admin-news.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-news.md`](../tests/e2e/cp-admin-news.md)

The public news feed, read by the website and by the app's media-coverage screen.
Each article carries a bilingual title, category, short excerpt and full body,
plus a publish date and a display order.

#### Most common tasks

##### Publish an article

1. **Content → News** → **+ Add**.
2. **Title (English)** and **Title (Arabic)**: both required, up to 200
   characters each. The English title must be unique.
3. **Category (English)** and **Category (Arabic)**: both required, up to 100
   characters each. This is free text, it is what the app shows as the article's
   tag.
4. **Excerpt (English)** and **Excerpt (Arabic)**: optional, up to 500 each. The
   short line under the headline on the card.
5. **Body (English)** and **Body (Arabic)**: both required, up to 8000 each.
6. **Image path**: optional, up to 512 characters. A relative path to an existing
   picture. Prefer the upload described below.
7. **Publish date** and **Display order**.
8. **Save**.

##### Attach the article picture

Re-open the article with **Edit**. Under **Image** you get the same **Upload
file** / **External link** pair described in §8.3. Upload accepts `.png`, `.jpg`,
`.webp` and `.pdf`; the link mode takes an `https://...` URL. The box is not on
the Add form because the article row must exist first.

##### Pull an article

Per-row **Delete** confirms with *"Delete "X"? It will be removed from the public
feed immediately."* Alternatively, untick **Active** on the Edit form to hide it
while keeping the text.

Gates: **News → View**, **Create**, **Edit**, **Delete**, **Export**, **Import**.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Please fill in the required fields: title, body and category (English and Arabic)." | One of the six required boxes is empty | Fill all six |
| "A news article with the English title 'X' already exists." | Duplicate English title | Change the title, or edit the existing article |
| "News English title is required." | The server rejected a blank required field | Fill it |
| "News English body must be 8000 characters or fewer." | Text over the limit | Shorten it |
| "Display order must be zero or a positive integer." | A negative order | Use 0 or higher |
| "The news article was not found." | Deleted while your form was open | Reload the list |
| Article shows with no picture | No image attached, or the **Image path** points nowhere | Re-open Edit and use the Image upload |

### 8.6 Media Partners — `/admin/media-partners`

> Page reference: [`docs/pages/cp/admin-media-partners.md`](../pages/cp/admin-media-partners.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-media-partners.md`](../tests/e2e/cp-admin-media-partners.md)

The publications and channels covering the forum. The app's media-coverage screen
shows them as a logo grid, and tapping a card opens the logo full size. Each
partner also carries an optional contact card (country, email, phones, city and
social links) so the PR desk has the details in one place.

#### Most common tasks

##### Add a partner

1. **Content → Media Partners** → **+ Add**.
2. **Name (English)** and **Name (Arabic)**: both required, up to 256 characters
   each. The English name must be unique.
3. **Logo path**: optional, up to 512 characters. Prefer the logo upload below.
4. **Link**: optional, up to 512 characters. The partner's own site.
5. **Display order**: 0 or higher.
6. Under **Contact information**, all optional: **Country**, **Email** (up to
   320), **Phone** and **Secondary phone** (32 each), **City** and
   **City (Arabic)** (128 each), **Instagram URL**, **Facebook URL**,
   **X (Twitter) URL** and **LinkedIn URL** (256 each), plus **Latitude** and
   **Longitude**.
7. **Save**.

##### Upload the logo

Re-open with **Edit**. The **Image** box (upload file or external link, see §8.3)
appears only in edit mode. A partner with no logo renders as its initials on the
app card, which is the tell-tale sign the upload never happened.

##### Remove a partner

Per-row **Delete** confirms with *"Delete "X"? It will be removed from the public
list immediately."*

Gates: **Media partners → View**, **Create**, **Edit**, **Delete**, **Export**,
**Import**.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Both the English and Arabic names are required." | One name box is empty | Fill both |
| "Media partner English name must be between 1 and 256 characters." | Name blank or too long | Shorten it |
| "A media partner named 'X' already exists." | Duplicate name | Use a distinct name, or edit the existing partner |
| "Logo path must be 512 characters or fewer." | Path too long | Shorten it, or upload the logo instead |
| "URL must be 512 characters or fewer." | Link too long | Shorten it |
| "The media partner was not found." | Deleted while your form was open | Reload the list |
| "Could not load media partners. Please try again." | The list call failed | Reload the page |
| Card shows initials instead of a logo | No logo bytes attached | Re-open Edit and upload the logo |

### 8.7 Previous editions — `/admin/archive`

> Page reference: [`docs/pages/cp/admin-archive.md`](../pages/cp/admin-archive.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-archive.md`](../tests/e2e/cp-admin-archive.md)

The public archive of past forums, one row per year, and there is exactly one
edition per year. Each carries a bilingual title and summary, the three headline
counters, a place and date label, and three rich lists: the gallery, the session
titles and the past speakers.

#### Most common tasks

##### Archive the year that just finished

1. **Content → Previous editions** → **Make this year history**.
2. The dialog explains: *"This creates a past-edition snapshot for the current
   year. Attendees (gate-scan arrivals), sessions and speakers are counted
   automatically from live data."* You do not type the counters.
3. Leave **Show in the archive now** ticked to publish it immediately, untick it
   to prepare it quietly.
4. **Create snapshot**. You get *"Archived the current event as 2026."*

This button needs the **Archive → Snapshot** permission. The attendee count is
the number of distinct people whose badge was scanned in at a gate, not the
number of registrations.

##### Add or correct an edition by hand

**+ Add**, or per-row **Edit**:

- **Year**: 2000 to 2100, one edition per year.
- **Title (English)** and **Title (Arabic)**: both required, up to 200 each.
- **Summary (English)** and **Summary (Arabic)**: optional, up to 1024 each.
- **Attendees**, **Sessions**, **Speakers**: 0 or higher.
- **Cover image path**: optional, up to 512. The **Image** upload box (see §8.3)
  appears on Edit only.
- **Location (English / Arabic)**: up to 256 each. **Date label
  (English / Arabic)**: up to 128 each. These are the free-text place and date
  line on the edition page.
- **Gallery**, **Session titles** and **Past speakers**: one entry per line, with
  fields separated by a vertical bar. The label above each box states its
  grammar: gallery rows are `url | image or video | caption`, session titles are
  `arabic | english`, past speakers are `arabic | english | photo path` with an
  optional fourth field for the numeric country code. Picture and photo links
  must be full `https` URLs the app can load directly.
- **Active (visible in public archive)** on Edit gates public visibility.

When you open **Edit**, the three list boxes are filled from the saved edition.
If that fetch fails they come up empty, and saving without touching them leaves
the stored lists alone rather than wiping them.

##### Remove an edition

Per-row **Delete** shows the details with a **Deactivate** button, which confirms
with *"Deactivate "X"? It will be removed from the public archive immediately."*
The edition stays in the list marked Inactive.

Gates: **Archive → View**, **Create**, **Edit**, **Delete**, **Snapshot**,
**Export**, **Import**.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "An archive edition for year 2026 already exists." | One edition per year, and the snapshot uses the current year | Edit the existing row instead of creating a second |
| "The English and Arabic titles are both required." | A title box is empty | Fill both |
| "English title must be between 1 and 200 characters." | Title blank or too long | Shorten it |
| "Year must be between 2000 and 2100." | Year outside the allowed range | Correct the year |
| "English summary must be 1024 characters or fewer." | Summary too long | Shorten it |
| "Attendees, sessions and speakers must be zero or positive." | A negative counter | Use 0 or higher |
| "Could not load archive editions. Please try again." | The list or snapshot call failed | Reload the page |
| No **Make this year history** button | Your role lacks Archive → Snapshot | Ask an administrator |
| Gallery pictures do not load in the app | A relative path instead of a full `https` link | Re-enter each gallery line with the full URL |

### 8.8 Invitations — `/admin/invitations`

> Page reference: [`docs/pages/cp/admin-invitations.md`](../pages/cp/admin-invitations.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-invitations.md`](../tests/e2e/cp-admin-invitations.md)

The PR desk's invitation register: who was invited, by whom, and whether they
accepted. Each row records a recipient, the admin who sent it, the state
(**Pending**, **Confirmed** or **Declined**) and free-text notes. The grid shows
the recipient's profile type, so you can see at a glance how many VIPs are still
unanswered.

#### Most common tasks

##### Send an invitation

1. **Public relations → Invitations** → **New invitation**.
2. **Recipient (UserProfile id)**: paste the recipient's profile id. This is an
   id, not a name or an email. Get it from the person's row in the People
   modules.
3. **Notes**: optional, up to 1000 characters.
4. **Send**. You get *"Invitation sent."* The state starts at **Pending** and the
   sender is stamped automatically, so neither is on this form.

##### Record a reply

Per-row **Edit** shows **State** and **Notes**, not the recipient, because the
recipient is fixed once sent. Set the state to **Confirmed** or **Declined** and
**Save changes**. You get *"Invitation updated."*

An invitation that has already been answered cannot be moved back to **Pending**.

##### Cancel an invitation

Per-row **Cancel invitation** confirms with *"Cancel the invitation to "X"? It
will be removed from the list immediately. You can reactivate it later by editing
it."*

Gates: **Invitations → View** to open the page, **Invitations → Manage** for
send, edit and cancel, **Invitations → Export** for the spreadsheet.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Enter a valid recipient UserProfile id." | The box is empty or is not an id | Copy the profile id from the person's record |
| "Recipient profile 'X' does not exist." | The id is well-formed but matches nobody | Re-copy the id, the person may have been removed |
| "Cannot move an invitation back to Pending once it has been settled." | You tried to undo a Confirmed or Declined reply | Leave it settled, add a note explaining the change |
| "Invitation notes cannot exceed 1000 characters." | Notes too long | Shorten them |
| "The invitation was not found." | Cancelled by someone else while your form was open | Reload the list |
| "The invitations could not be loaded." | The list call failed | Reload the page |
| "The operation could not be completed." | The send or save call failed | Retry; if it repeats the API is unreachable |

#### What you cannot do here yet

- Pick the recipient from a searchable name list. The form takes a profile id.
- Import invitations from a spreadsheet. Export only.

### 8.9 VIPs — `/admin/vips`

> Page reference: [`docs/pages/cp/admin-vips.md`](../pages/cp/admin-vips.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-vips.md`](../tests/e2e/cp-admin-vips.md)

The VIP roster: every person whose profile type is **VVIP**, **VIP** or **Gold**.
The list is derived, you do not add or edit people here, you manage them in the
People modules by changing their profile type. What this page adds is a bulk
message: tick the guests you want and send them one bilingual announcement, in
the app and by email.

#### Most common tasks

##### Message a group of VIPs

1. **Public relations → VIPs**.
2. Tick the rows you want, or use **Select all** for the whole page.
3. **Notify selected (N)** in the toolbar. The button stays disabled until at
   least one row is ticked, and the count in its label is how many you picked.
4. In **Notify VIPs**, fill **Title (English)**, **Title (Arabic)**,
   **Body (English)** and **Body (Arabic)**. All four are required.
5. **Send**. A green *"Sent to 12 VIPs (9 emails enqueued)."* tells you how many
   received the in-app notification and how many also had an email address on
   file. The tick boxes clear afterwards.

##### Export the roster

**Export** downloads the ticked rows, or the whole filtered list when nothing is
ticked.

Gates: **VIPs → View** to open, **VIPs → Notify** for the broadcast, **VIPs →
Export** for the download. **Notify selected** is not hidden by the permission:
anyone who can open the page sees the button and it enables as soon as a row is
ticked. A role without **VIPs → Notify** only finds out at the **Send**, which is
refused by the server. Emails only go to guests who have an email address
recorded, which is why the two numbers in the success line often differ.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Select at least one VIP." | Nothing ticked when the send was submitted | Tick at least one row |
| "Cannot dispatch to more than 500 VIPs in one batch." | Over 500 rows selected | Send in batches of 500 or fewer |
| "Message title (EN + AR) must be between 1 and 200 characters each." | A title box is empty or too long | Fill both, keep each under 200 |
| "Message body (EN + AR) must be between 1 and 2000 characters each." | A body box is empty or too long | Fill both, keep each under 2000 |
| "You do not have permission to perform this action." after pressing Send | Your role has VIPs → View but not VIPs → Notify. The button is not hidden, only the send is refused | Ask an administrator for VIPs → Notify |
| "The broadcast could not be sent." | The send call failed | Retry; the message text is still in the dialog |
| "The VIP list could not be loaded." | The list call failed | Reload the page |
| "No VIPs match the filter." | Nobody carries a VVIP, VIP or Gold profile type, or your column filter excludes them | Clear the filter, or set the profile type on the person's record |
| Notify button is greyed out | No rows ticked | Tick a row |
| Fewer emails than recipients | Some guests have no email address on file | Add the address on their profile |

#### What you cannot do here yet

- Add, edit or remove a VIP from this page. Change the person's profile type in
  the People modules instead.
- Select guests across several pages at once. Ticks apply to the page you are on.

### 8.10 Announcements — `/admin/announcements`

> Page reference: [`docs/pages/cp/announcements.md`](../pages/cp/announcements.md)
> · E2E catalogue: [`docs/tests/e2e/cp-announcements.md`](../tests/e2e/cp-announcements.md)

The broadcast desk. Write one bilingual message and send it, in the app and by
email, either to the people registered for a specific session or to a broad
audience. Delivery runs in the background, so the page confirms the message was
queued and the history grid below tells you what happened to it.

#### Most common tasks

##### Send a broadcast

1. **Public relations → Announcements**.
2. Under **Recipients**, set **Send to**: **A specific session** or **A broad
   audience**.
   - For a session, pick it from **Session** (listed as code then title).
   - For an audience, pick **Audience**: **All approved app users**, **Event
     attendees (booked a seat)** or **Everyone (including pending)**.
3. **Importance**: **Info**, **Success**, **Warning** or **Critical**. This
   drives the colour of the notification the reader sees.
4. Read the blue line: *"This will reach 428 recipient(s)."* It updates every
   time you change the target. If it says *"Choose a target to see how many
   recipients it reaches."*, the count could not be worked out yet.
5. Under **Message**, fill **Title (English)**, **Title (Arabic)**, **Message
   (English)** and **Message (Arabic)**. All four are required, titles up to 200
   characters, messages up to 2000.
6. **Send broadcast**. You get *"Broadcast queued: delivering to 428
   recipient(s)."* The four message boxes clear, the target stays, so a follow-up
   is quick.

##### Check that a broadcast went out

**Recent broadcasts** below shows **When**, **Target**, **Message**, **Status**,
**Recipients** and **Emails**. Status moves **Queued** to **Sending** to **Sent**,
or to **Failed**. Reload the page to refresh it, it does not update by itself.

The compose panel needs the **Announcements → Send** permission and is hidden
without it. **Announcements → View** alone still shows the history grid, which is
deliberate: supervisors can audit what was sent without being able to send.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "The English title is required." | A title box is empty | Fill both titles |
| "The English message must be at most 2000 characters." | Message too long | Shorten it |
| "A target is required." | No target chosen | Pick a session or an audience |
| "Target must be a session or an audience." | The target selector was left in an odd state | Re-pick from the dropdown |
| "Importance must be Info, Success, Warning or Error." | An unrecognised importance | Re-pick from the dropdown |
| "The broadcast could not be sent." | The send call failed | Retry, nothing was queued |
| "The broadcasts could not be loaded." | The history call failed | Reload the page |
| Status stuck on **Queued** | The background sender has not picked it up | Wait, then reload. If it stays queued for long, tell the team |
| Status is **Failed** | Delivery could not complete | Re-send, and report it |
| No compose panel, only the history | Your role has View but not Send | Ask an administrator for Announcements → Send |
| Recipient count reads "Choose a target..." | No session picked yet, or the estimate call failed | Pick the session again |

### 8.11 Contact inquiries — `/admin/contact-inquiries`

> E2E catalogue: [`docs/tests/e2e/cp-contact-inquiries.md`](../tests/e2e/cp-contact-inquiries.md)

The inbox for the "Contact us" form in the mobile app. Anyone, signed in or not,
can send a message, so this list is public-facing and worth checking daily. It is
a triage list, not a mailbox: you read the message here and reply outside SIMF
using the sender's email.

#### Most common tasks

##### Work through the inbox

1. **Public relations → Contact inquiries**. Open items are listed first, newest
   first.
2. The grid shows **Name**, **Email**, **Message** (shortened to about 80
   characters in the cell), **Status** and **Received** (Saudi time). The
   **Received** column is sortable.
3. **Status** is either **Open** (amber) or **Handled** (green).

##### Mark an inquiry handled

Once you have replied by email, press the tick icon on the row, labelled **Mark
handled**. You get *"Inquiry marked handled."* and the pill turns green.

##### Reopen one

Press the circular-arrow icon on a handled row, labelled **Reopen**. You get
*"Inquiry reopened."*

Both row actions need the **Contact inquiries → Manage** permission. With
**Contact inquiries → View** only, you can read the inbox but the two icons are
not rendered.

The app-side form limits a submission to a 120-character name, a valid email of
up to 256 characters and a 4000-character message. A submission that breaks one
of those limits is rejected outright in the app, nothing is trimmed on the way
in, so every message that reaches this list is complete.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "The inquiries could not be loaded." | The list call failed, or the mark-handled call failed | Reload the page; if it repeats the API is unreachable |
| "No inquiries yet." | Nobody has written in, or your column filter excludes everything | Clear the filter |
| No tick or reopen icon on the rows | Your role has View but not Manage | Ask an administrator for Contact inquiries → Manage |
| The full message is cut off | The grid cell shortens long text | Widen the Message column, or read the message in the export from the team |
| A handled row keeps reappearing | Someone reopened it | Check with the other PR operators |

#### What you cannot do here yet

- Reply from inside the Control Panel. Use the sender's email address.
- Assign an inquiry to a specific operator, or add internal notes.
- Delete an inquiry. Marking it handled is the only close-out.

---

## 9. Gates, Reference data & System modules

### 9.1 Gates — `/admin/gates`

> Page reference: [`docs/pages/cp/admin-gates.md`](../pages/cp/admin-gates.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-gates.md`](../tests/e2e/cp-admin-gates.md)

A **gate** is a scanning point: a venue entrance, or a hall door. Each gate holds
its own direction policy, its allow-list of profile types, and the list of
operators who may scan there. Everything the gate operator console (§9.2) and the
gates dashboard (§9.4) show comes from this list, so a gate that is missing or
inactive here simply cannot be scanned at. The page follows the canonical CRUD
pattern (§3.1) with **Add gate**, per-row **Edit / Details / Deactivate**, plus
**Export** and **Import**. It needs the **Gates → Manage** permission; without it
the menu entry does not appear and opening the route lands you on *"Not
permitted"*.

#### Most common tasks

##### Add a gate

1. **Gates & arrivals → Gates** → **Add gate**.
2. **Identification**
   - **Code** (2 to 16 characters, unique): the venue team's stable identifier,
     for example `G-MAIN-1` or `VIP-1`.
   - **Name (English)** and **Name (Arabic)**, up to 128 characters each. Both are
     stored; the operator and the dashboard show the one matching the reader's
     language.
3. **Direction & hall**
   - **Direction policy**: *In (check-in only)*, *Out (check-out only)*, or
     *Both (inferred direction)*. On *Both*, the system works out the direction
     from the visitor's last allowed scan at that gate.
   - **Hall (door gate)**: leave it on *None — perimeter gate* for a venue
     entrance. Bind it to a hall to make it a hall-door gate, so an allowed
     check-in also records hall attendance for the session live in that hall.
4. **Descriptions** (optional, up to 1024 characters each, English and Arabic).
5. **Access & operators**
   - **Allowed profile types**: leave every box unticked to admit everyone (the
     list then shows *All*). Tick one or more to restrict the gate.
   - **Assigned operators**: type a name or email into **Search operators**, press
     **Search**, then tick each person. Only approved app accounts whose profile
     type is operational (non-visitor) **and** carries the Staff or Moderator app
     role, or approved Control Panel admins, can be assigned. An operational
     profile type on any other app role, an exhibitor type for example, is
     refused.
6. **Create gate**. A green *"Gate "…" was created."* banner confirms it.

##### Edit or deactivate

Per-row **Edit** reopens the same form with an extra **Active — available for
scanning** checkbox. Per-row **Deactivate** opens the details card and asks to
confirm; deactivating is a soft-delete, and a scan attempted at an inactive gate
is still recorded, as a denial. The **Details** card also lists every assigned
operator by name and email, so an assignment can be audited without opening Edit.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "A gate with code 'X' already exists." | Gate codes are unique | Pick a different code |
| "Code must be between 2 and 16 characters." | Code too short or too long | Shorten or lengthen the code |
| "English name is required (1–128 characters)." / "Arabic name is required (1–128 characters)." | One of the bilingual names is blank | Fill both names |
| "These accounts cannot be assigned as gate operators: …" | The person is not approved, or their profile type is a visitor type, or it is operational but does not carry the Staff or Moderator app role | Approve the account first, or move them to an operational profile type whose app role is Staff or Moderator |
| "The selected hall was not found or is inactive." | The bound hall was deactivated | Pick another hall, or reactivate it on Halls & seating (§5.2) |
| "A lookup list could not be loaded. Check your permissions and try again." | The profile-type or hall lookup could not be read | Confirm you hold Gates → Manage, then reopen the form |

### 9.2 Gate operator console — `/admin/gates/operator`

> E2E catalogue: [`docs/tests/e2e/cp-admin-gates-operator.md`](../tests/e2e/cp-admin-gates-operator.md)

The scanning desk. An operator picks one of the gates they are assigned to,
scans or types a badge QR id, and is told immediately whether the holder is
allowed in. Every scan is recorded, allowed or denied. The page needs the
**Gates → Operate** permission, and it only ever offers gates you personally are
assigned to on `/admin/gates` (§9.1).

#### Most common tasks

##### Scan a badge

1. **Gates & arrivals → Gate operator**.
2. Pick the gate from the **Gate** dropdown. It lists code, name and direction
   policy, and the first active assignment is chosen for you.
3. Put the cursor in **QR id** and scan the badge, or type the 12-character id.
4. Click **Scan**.
5. A green **Allowed** banner shows the holder's name, profile type and the
   direction recorded. A red **Denied** banner shows the reason code and the
   reason in plain language.
6. The field clears itself, ready for the next badge.

##### Check your own tally

**My day so far** under the scanner shows *"N allowed · M denied"* for the
selected gate plus the last 50 scans (time, outcome, direction, visitor, reason).
It refreshes after every scan.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "You have no active gate assignments. Please contact an administrator." | Nobody has assigned you to a gate | An administrator adds you under Assigned operators on `/admin/gates` |
| Denied: "This QR code is not recognised." | Typo, damaged QR, or the badge belongs to a deleted account | Re-scan; look the person up on `/admin/visitors` and read their QR id |
| Denied: "This visitor's account has not been approved." | Registration is still pending | Approve them on the matching Pending page, then re-scan |
| Denied: "This visitor's account is locked." / "…is disabled." | Account state blocks entry | Resolve the account state first; do not wave them through |
| Denied: "This gate is not open to this visitor's profile type." | The gate's allow-list excludes their type | Send them to a gate that admits their type, or widen the allow-list |
| Denied: "This gate is currently inactive." | The gate was deactivated | Reactivate it on `/admin/gates`, or move to an active gate |
| Warning: "Entry allowed, but no session attendance was recorded for this scan." | Hall-door gate with no session live in that hall right now | Entry is fine; record hall attendance on Hall arrivals (§9.3) if it is needed |
| "You are not assigned to this gate." | The assignment was removed while the page was open | Reload the page and pick a gate you still hold |
| "This gate is temporarily refusing scans due to a failure-rate threshold." | Too many denials in a short window tripped the gate's safety cut-out | Wait for the lockout to lift and tell a supervisor; something is wrong upstream |

### 9.3 Hall arrivals (door scan) — `/admin/hall-arrivals`

> E2E catalogue: [`docs/tests/e2e/cp-admin-hall-arrivals.md`](../tests/e2e/cp-admin-hall-arrivals.md)

The hall door desk. An operator picks the session running in the room and scans
each attendee's badge to record that they went in, and scans again to record that
they left. This is what makes a booked seat show as confirmed, and what clears it
again on the way out. Opening the page needs **Hall arrivals → View**; the QR
field and both buttons only appear if you also hold **Hall arrivals → Record**.

#### Most common tasks

##### Record an arrival

1. **Gates & arrivals → Hall arrivals**.
2. Pick the **Session**. The list shows title and code, with the sessions that
   are live now at the top and the recently ended ones below. Sessions that have
   not opened yet are not offered.
3. Scan or type the badge into **Attendee badge QR**.
4. Click **Record arrival**. A green *"Arrival recorded: <name>"* confirms it and
   the field clears for the next person.

##### Record a departure

Same steps, then **Record departure** instead. A departure has no time window: an
attendee who is inside can always be checked out, even after the session ended.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Select a session first." | No session picked | Choose the session, then scan |
| "That badge QR was not recognised." | Typo, damaged QR, or a deleted account | Re-scan; confirm the person on `/admin/attendees` |
| "This attendee's account is not approved for entry." | Their account is not approved, is locked, or their profile type was deactivated | Fix the account first; the hall door applies the same admission rules as a perimeter gate |
| "This session is not open for arrivals right now." | Arrivals are accepted from 15 minutes before the start to 15 minutes after the end | Check you picked the right session; a departure still works |
| "This hall is at capacity." | Every seat the session allows is already occupied | Check people out as they leave, or raise the session's capacity override (§5.4) |
| "No active sessions to record arrivals for." | Nothing has started yet today | Wait for the first session, or check the session's start time on `/admin/sessions` |
| The QR box and both buttons are missing | You hold Hall arrivals → View but not Record | Ask an administrator for the Hall arrivals → Record permission |

### 9.4 Gates operations dashboard — `/admin/gates/dashboard`

> E2E catalogue: [`docs/tests/e2e/cp-admin-gates-dashboard.md`](../tests/e2e/cp-admin-gates-dashboard.md)

Read-only headcount. Two stat cards, **Currently inside** and **Gates**, over a
table of everyone who has checked in and not yet checked out (name, profile type,
the gate they came through, and when), plus the gate roster with its active or
inactive status. This is the fastest answer to "how many people are in the venue
right now?". It needs the **Gates → Manage** permission.

#### Most common tasks

- **Read the headcount**: the **Currently inside** card, and the *"N currently
  inside"* line under the table, are the same number.
- **Refresh**: the page does not poll. Click **Refresh** to re-read; the button
  reads *"Refreshing…"* while it works.
- **Spot a gate that is down**: the Gates table marks each gate **Active** or
  **Inactive**. An inactive gate cannot admit anyone.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "No one is currently inside the venue." | Nobody has checked in, or everybody checked out | Correct if the venue is empty; if not, check the operators are scanning on `/admin/gates/operator` |
| "No gates have been configured." | The gate list is empty | Create the gates on `/admin/gates` (§9.1) |
| "Could not load the gates dashboard." | The gate reports could not be read | Click Refresh; if it persists, check the API is up on Background services (§9.12) |
| The count looks too high | People left without scanning out | Only a check-out clears someone from this list; a gate with direction policy *In* never checks anyone out |

#### What you cannot do here yet

- Create, edit or delete anything. Gate configuration is §9.1, scanning is §9.2.
- Export the list. Use the gate reports under the Reporting module instead.

### 9.5 Countries — `/admin/countries`

> Page reference: [`docs/pages/cp/admin-countries.md`](../pages/cp/admin-countries.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-countries.md`](../tests/e2e/cp-admin-countries.md)

The country lookup behind the nationality picker on every registration form and
the speaker's country field. It also carries the **delegation** (وفد) data: which
countries were invited, when their delegation arrives and leaves, and who heads
it. The app's Delegations card reads those dates. Standard CRUD list (§3.1) with
**Export** and **Import**; it needs **Countries → View** to open.

#### Most common tasks

##### Add a country

1. **Reference data → Countries** → **+ Add country**.
2. **ISO 3166-1 numeric id**: assigned by hand, for example 682 for Saudi Arabia
   or 784 for the UAE. It is fixed once saved and is read-only when editing.
3. **ISO alpha-2 code**: exactly 2 letters (SA, AE, EG), unique across all rows.
4. **Name (English)** and **Name (Arabic)**, 1 to 128 characters each. Both are
   stored and the picker shows the one matching the user's language.
5. **Dial code** (optional): the E.164 prefix with its leading `+`, up to 8
   characters, for example `+966`.
6. **Display order**: zero or a positive number; the nationality picker sorts
   ascending on it.
7. **Invited to send a delegation (وفد)**, plus **Delegation arrival date** and
   **Delegation departure date** when it applies.
8. **Create country**.

##### Name the head of delegation

Head of delegation can only be set on an existing row, because the person must
already be one of that country's delegates. Per-row **Edit** → **Head of
delegation (رئيس الوفد)** → pick from the list → **Save changes**. The Edit form
also carries the **Active — show in the nationality picker** checkbox.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "A country with code 'SA' already exists." | Alpha-2 codes are unique | Check whether the country is already there but deactivated, and reactivate it instead |
| "A country with id 682 already exists." | The numeric id is already taken | Use the country's real ISO 3166-1 numeric id; each is used once |
| "Code must be exactly 2 letters." | Three-letter or one-letter code entered | Use the alpha-2 code, not alpha-3 |
| "Id must be a positive integer (1–999, ISO 3166-1 numeric)." | Blank or non-numeric id | Enter the numeric ISO id |
| "The departure date must be on or after the arrival date." | Dates entered the wrong way round | Swap them |
| "The head of delegation must be an active delegate of this country." | The person is not registered as a delegate of that country, or was deactivated | Register or reactivate them first, then set the head |
| A country is missing from the sign-up nationality list | The row is deactivated | Edit it and tick Active |

### 9.6 Organisations — `/admin/organisations`

> Page reference: [`docs/pages/cp/admin-organisations.md`](../pages/cp/admin-organisations.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-organisations.md`](../tests/e2e/cp-admin-organisations.md)

The Saudi companies lookup that backs the الجهة (employer) picker a visitor
chooses from during registration. Rows can be typed in one at a time or bulk
loaded from a government Excel sheet. It needs **Organisations → View** to open.
Above the grid there is a **Search** box that queries the server across name,
commercial registration, sector and city, so it finds rows on any page, not just
the one you are looking at.

#### Most common tasks

##### Add one organisation

1. **Reference data → Organisations** → **Add**.
2. **Name (Arabic)** is the required one, up to 150 characters. **Name (English)**
   is optional, also up to 150. Both are stored.
3. Optional: **Commercial registration** (32), **Sector** (128), **City** (128),
   **Phone** (32), **Email** (320), **Website** (512).
4. **Save**. A *"Organisation saved."* toast confirms it.

##### Bulk load from the government sheet

1. **Import Excel** in the grid toolbar.
2. **Excel file (.xlsx)** → pick the sheet. Existing rows are matched by
   commercial registration and updated; rows with a new CR are inserted.
   An import only **fills** columns: a cell you leave blank is read as "not
   supplied", so a partial correction sheet cannot wipe details already held on
   those rows. To clear a field, edit that organisation directly.
3. **Upload**. The modal reports *"Rows read: N · Inserted: N · Updated: N ·
   Skipped: N"*, and lists any per-row errors underneath.
4. Close the modal; the grid reloads with the new rows.

##### Export

Tick the rows you want and click **Export** for just those, or click **Export**
with nothing ticked to export everything matching the current filter.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Arabic name is required." | The Arabic name was left blank | Fill Name (Arabic); English alone is not enough |
| "The uploaded file could not be read as an Excel workbook." | Not a real .xlsx, or the file is corrupt | Re-save the sheet as .xlsx and upload again |
| "Excel import failed." | The upload was rejected by the server | Check the per-row errors listed in the modal, fix the sheet, retry |
| "Organisation Arabic name must be between 1 and 150 characters." | A sheet row has a blank or over-long Arabic name | Fix that row in the sheet and re-import |
| Rows import as Updated when you expected Inserted | Those CR numbers already exist | That is correct behaviour: CR is the match key |
| "You do not have permission to perform this action." on Import | You hold View but not Organisations → Import | Ask an administrator for the Import permission |

### 9.7 Regions — `/admin/regions`

> Page reference: [`docs/pages/cp/admin-regions.md`](../pages/cp/admin-regions.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-regions.md`](../tests/e2e/cp-admin-regions.md)

The administrative-regions lookup, the 13 official Saudi regions, used by the
region picker in the app. The rows are seeded for you; this page exists so a
name can be corrected, a region hidden, or a missing one added by hand. It needs
**Regions → View** to open. There is a server-side **Search** box above the grid
(search by code or name).

#### Most common tasks

##### Add or correct a region

1. **Reference data → Regions** → **Add**.
2. **Code**, up to 16 characters, unique.
3. **Name (Arabic)** is required, up to 256 characters. **Name (English)** is
   optional, up to 256. Both are stored; the app shows the reader's language.
4. **Sort order**: the number that fixes where the region sits in the picker.
5. **Save**. A *"Region saved."* toast confirms it.

##### Hide a region

Per-row **Delete** opens *"Deactivate "…"? It will be removed from the public
lookup."*. Confirm and the region disappears from the app picker while the row
stays in the database; Edit it and tick **Active** to bring it back.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Code and Arabic name are required." | One of the two mandatory fields is blank | Fill both, then Save |
| "A region with code 'X' already exists." | Region codes are unique | Search for the code first: the existing row may just be deactivated |
| "Region Arabic name must be between 1 and 256 characters." | Blank or over-long Arabic name | Shorten it, or fill it in |
| "Could not load regions." | The lookup could not be read | Reload the page; if it persists check the API on Background services (§9.12) |
| A region is missing from the app picker | The row is deactivated | Edit it and tick Active |

#### What you cannot do here yet

- Import or export regions. The list is small and fixed, so there is no Excel
  round-trip; the Organisations page (§9.6) is the one with Excel.

### 9.8 System configuration — `/admin/configuration`

> Page reference: [`docs/pages/cp/admin-configuration.md`](../pages/cp/admin-configuration.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-configuration.md`](../tests/e2e/cp-admin-configuration.md)

The platform's key and value store: the odd settings that are neither content nor
a lookup. Each row is a **Key**, its **Value**, and an optional **Description**
that explains what it does. It ships empty and the team seeds the keys, so an
empty grid on a fresh install is normal. Do not invent keys here: a key nothing
reads changes nothing. Opening the page needs **Configuration → View**; note that
the registration open and close switch is not here, it lives on Operations
toggles (§9.13).

#### Most common tasks

##### Change a setting's value

1. **System → Configuration**.
2. Find the row (the Key, Value and Description columns are all searchable).
3. Per-row **Edit** → change **Value** → **Save changes**. The **Key** is
   read-only on an existing row.

##### Add a setting

**Add** → **Key** (up to 128 characters, unique, for example `registration.open`;
it cannot be changed later) → **Value** (up to 1024 characters) →
**Description** (optional, up to 512) → **Create setting**.

##### Retire a setting

Per-row **Delete** shows *"Delete the setting "…"? This deactivates it."*. It is
a soft-delete: the row is kept and marked inactive.

##### Export or import the settings

The toolbar also carries **Export** and **Import**. **Export** downloads the
current list as a spreadsheet with the columns **Key**, **Value**, **Description**
and **IsActive**; it needs **Configuration → Export**. **Import** takes a
spreadsheet whose sheet is named *Configuration* and which must carry at least the
**Key** and **Value** columns; it needs **Configuration → Import**. Import is
insert-only, so it adds new keys and never overwrites an existing one; a row whose
key is already in use comes back as a per-row error and the rest of the sheet
still loads.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "A setting with the key 'x' already exists." | Keys are unique | Edit the existing row instead of adding a second one |
| "Enter a key (up to 128 characters)." | Key blank or too long | Shorten the key |
| "Enter a value (up to 1024 characters)." | Value blank or too long | Shorten the value |
| "The description must be 512 characters or fewer." | Description too long | Trim it |
| "No system settings yet." | Nothing has been seeded | Correct on a fresh install; ask the team which keys this deployment needs |
| Changing a value has no visible effect | Nothing in the product reads that key | Confirm the key name with the team before adding rows here |

### 9.9 Site Settings — `/admin/site-settings`

> Page reference: [`docs/pages/cp/site-settings.md`](../pages/cp/site-settings.md)
> · E2E catalogue: [`docs/tests/e2e/cp-site-settings.md`](../tests/e2e/cp-site-settings.md)

Two switches that the mobile app and the public website both read: the message a
visitor sees the moment their registration completes, and whether the **Meet
People Like You** partner directory appears in the app. It is one form with one
**Save**. Unlike most System pages this one needs the **Configuration → Edit**
permission just to open, because everything on it writes.

#### Most common tasks

##### Change the registration welcome message

1. **System → Site Settings**.
2. **Welcome message (Arabic)** and **Welcome message (English)**, up to 2048
   characters each. Both are stored, and the visitor is shown the one matching
   their language, so fill both.
3. **Save**. A *"Site settings saved."* banner confirms it.

##### Turn the partner directory on or off

Under **Meet People Like You**, tick or untick **Show the "Meet People Like You"
directory in the app**, then **Save**. It controls the app's directory of
sponsors, speakers, exhibition companies and opted-in members. Unticking it hides
the whole directory for everyone.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Couldn't load the site settings." | The settings could not be read | Reload the page; if it persists check the API on Background services (§9.12) |
| "Couldn't save the site settings. Try again." | The save was rejected | Try again; if it keeps failing, shorten the message and retry |
| The menu entry is missing | You hold Configuration → View but not Configuration → Edit | Ask an administrator for Configuration → Edit; this page is edit-only by design |
| The app still shows the old welcome text | The app caches the settings | Pull to refresh in the app, or restart it |

#### What you cannot do here yet

- Edit the social-media links. They moved to Organization Profile (§9.11), which
  is now the only place they are set.

### 9.10 Email templates — `/admin/email/templates`

> Page reference: [`docs/pages/cp/email-templates.md`](../pages/cp/email-templates.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-email-templates.md`](../tests/e2e/cp-admin-email-templates.md)

The wording of the automatic emails the system sends: sign-in OTP, email
verification, "an account already exists", password reset, badge activation,
biometric step-up, bulk badge delivery, email-change verification, email-changed
notice, and the exhibitor lead card. The set is fixed in code, so the grid always
lists all of them and there is no Add and no Delete. A row is either **Default**
(the built-in wording) or **Customised** (your override, with a version number
and a last-updated stamp). Opening the list needs **Email templates → View**;
saving or resetting needs **Email templates → Edit**.

#### Most common tasks

##### Reword an email

1. **System → Email Templates** → per-row **Edit** (pencil).
2. **Subject**, up to 256 characters.
3. **Body (English)** and **Body (Arabic)**, up to 8000 characters each. Both are
   stored, and the recipient gets the one matching their language. The English box
   is pinned left-to-right and the Arabic box right-to-left even in the Arabic
   Control Panel.
4. **Available tokens**: click a chip to drop the placeholder into whichever body
   box you last typed in. Hover a chip to see what it stands for.
5. Click **Preview** to render the subject and the composed body as a recipient
   would see them.
6. Tick **Serve this customised template** so your version is the one actually
   sent, then **Save**. A *"Template saved."* toast confirms it.

##### Go back to the built-in wording

Open the template, click **Reset to default** (only offered on a customised row),
and confirm *"Reset "…" to the built-in default? Your customised subject and body
will be removed."*. The grid's Customised column flips back to **Default**.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Unknown tokens must be removed before saving: {…}" and Save is greyed out | The body contains a placeholder that is not on the token list, usually a typo or one pasted from another template | Delete the offending token, or insert the right one from the chips, then Preview again |
| Save is greyed out | The last preview reported unknown tokens | Fix the tokens and re-run Preview |
| No Save and no Reset button | You hold Email templates → View but not Edit | Ask an administrator for the Edit permission |
| "Edit the template to see a live preview." | You have not previewed yet | Click Preview |
| The recipient still gets the old wording | The template is saved but "Serve this customised template" is unticked | Reopen it, tick the box, Save |
| "Could not load email templates." | The catalogue could not be read | Reload the page |

### 9.11 Organization Profile — `/admin/organization-profile`

> Page reference: [`docs/pages/cp/organization-profile.md`](../pages/cp/organization-profile.md)
> · E2E catalogue: [`docs/tests/e2e/cp-organization-profile.md`](../tests/e2e/cp-organization-profile.md)

The forum's own identity, in one long form of eight numbered steps: name, title,
slogan and bio; the edition year, status and dates; the venue and its map pin;
contact details; the hero and live-stream video; the social links; the About
cards; and the Details fact rows. The mobile app and the public website both read
it, so a save here changes what the public sees straight away. Opening it needs
**Organization profile → View**; the **Save** button and the video buttons only
appear if you also hold **Organization profile → Manage**.

#### Most common tasks

##### Update the forum identity and dates

1. **System → Organization Profile**.
2. **1 Identity**: **Name (EN/AR)** and **Title (EN/AR)** are required, up to 256
   characters each. **Slogan (EN/AR)** up to 512, **Bio (EN/AR)** up to 4000. Each
   pair is bilingual and both halves are stored.
3. **2 Edition**: **Current year**, **Status** (*Soon*, *Open*, *Archived
   (closed)*), **Event start (YYYY-MM-DD)** and **Event end (YYYY-MM-DD)**,
   **Version**, **System version**.
4. **3 Location**: **Location (EN/AR)**, plus **Latitude** and **Longitude** for
   the map pin.
5. **4 Contact**: **Phone**, **Email**, **Website**.
6. **Save** at the bottom. A *"Saved."* banner confirms it.

##### Set the hero background video

Under **5 Media** you have two mutually exclusive routes. Either paste a YouTube,
MP4 or HLS link into **Hero background video** and Save, or pick a file under the
upload field and click **Upload video**: MP4, M4V or WebM up to 200 MB, served
from SIMF as an MP4 so it plays on Android, where a YouTube hero cannot render in
the band. Once a file is uploaded, **Remove uploaded video** appears and asks to
confirm, because *"the public site and the mobile app stop showing it
immediately"*.

##### Edit the About cards and the Details rows

**7 About items** and **8 Details** are repeaters. **+ Add about item** or
**+ Add detail** appends a card; each card has arrows to move it up or down and
a Remove button, and the order top to bottom is the order the public sees. Every
field on an About card is required. In Details, **Name (EN)**, **Name (AR)** and
**Value (EN)** are required, but you may leave **Value (AR)** blank for a
language-neutral value such as a year or a URL, and the app then shows
**Value (EN)**.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "The name is required." (or title, about text, detail value) | A required field in that step or repeater card is blank | Scroll to the numbered step named in the message and fill it; both language halves are required |
| "The Facebook link must be an absolute http(s) URL." (or any other link) | The link is missing its `https://`, or is a handle rather than a URL | Paste the full absolute URL, or clear the field to hide that icon |
| "The latitude is out of range." / "The longitude is out of range." | Latitude outside ±90 or longitude outside ±180 | Re-copy the coordinates from the map |
| "The status is invalid." | The status value was not one of the three offered | Re-pick from the Status dropdown |
| "The video exceeds the maximum upload size of … bytes." | The file is over the limit | Compress it, or host it and paste a link instead |
| "The hero video must be an mp4, m4v or webm file." | Wrong file type | Convert it to MP4 |
| "Set OrganizationHeroVideo:PublicApiBaseUrl to the public https API base…" | The deployment is not configured to serve uploaded video | Hand this to the deployment team; it is a server setting, not something you can fix here |
| "Could not save the profile." | The save was rejected | Read the message above the form for the offending field, fix it, Save again |
| There is no Save button | You hold View but not Organization profile → Manage | Ask an administrator for the Manage permission |

### 9.12 Background services — `/admin/ops/services`

> Page reference: [`docs/pages/cp/ops-services.md`](../pages/cp/ops-services.md)
> · E2E catalogue: [`docs/tests/e2e/cp-ops-services.md`](../tests/e2e/cp-ops-services.md)

Read-only health of the jobs that run behind the product: session reminders, the
registration auto-close, and the rest. Three stat cards (**Up**, **Stale**,
**Faulted**) sit over a table of every worker with its status, last run, last
success, run and failure counts, and its last error. The page refreshes itself
every 15 seconds, so leaving it open on a second screen during the event is a
reasonable thing to do. It needs the **Services monitor → View** permission.

#### Most common tasks

- **Check everything is healthy**: **Up** should equal the number of rows, with
  **Stale** and **Faulted** both at zero.
- **Force a re-read**: click **Refresh**. The line above the table reads
  *"Last refreshed at hh:mm:ss."* so you can tell live data from a frozen page.
- **Diagnose a failure**: read the **Last error** column on the faulted row and
  give that text to the team verbatim.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| A worker shows **Stale** | It has not reported in recently; it may be stuck or the server is under load | Watch it for a minute; if it stays stale, escalate with the worker name |
| A worker shows **Faulted** | It threw and stopped | Copy the Last error text and escalate; the job is not running |
| A worker shows **Starting** for a long time | The API was just restarted, or start-up is blocked | Give it a minute; if it does not turn Up, escalate |
| "Could not load the background-services status." | The API could not be reached, or your session lapsed | The page retries on the next tick; if the toast persists, sign out and back in |
| "No background services are registered." | The API is running without its workers | Escalate: scheduled jobs such as session reminders are not running |

#### What you cannot do here yet

- Start, stop or restart a worker. This page reports; it does not control.

### 9.13 Operations toggles — `/admin/operations`

> E2E catalogue: [`docs/tests/e2e/cp-admin-operations.md`](../tests/e2e/cp-admin-operations.md)

Two switches with real-world consequences, each with its own **Save** button: the
**registration gate**, which opens or closes public sign-up, and **archive
visibility**, which shows or hides the past-events archive on the public website
and in the app. Each section shows when it was last changed. Opening the page
needs **Operations → View**; saving needs **Operations → Edit**.

#### Most common tasks

##### Close or reopen public registration

1. **System → Operations toggles**.
2. Under **Registration gate**, tick or untick **Registration is open**.
3. Optionally set **Auto-close (Saudi time)**: enter the moment in Saudi local
   time and a background worker flips the gate to closed in the first minute
   after it. Leave it blank for no scheduled close.
4. Click the section's **Save**. A *"Registration gate updated."* toast confirms
   it, and **Last changed** updates.

While the gate is closed, sign-up is refused and no account row is created, so a
visitor who tries gets an error rather than a half-made account.

##### Show or hide the past-events archive

Under **Archive visibility**, tick or untick **Archive is visible to the public**
and click that section's **Save**. A *"Archive visibility updated."* toast
confirms it. This one is read by the public with no sign-in at all, so the change
is visible to anyone immediately.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Auto-close must be a valid date and time." | The auto-close box holds something the browser could not read as a date and time | Re-pick it from the date-time control, or clear it |
| "The operations toggles could not be loaded." with a **Retry** button | That section could not be read; the other section may still have loaded | Click Retry; if it persists, check the API on Background services (§9.12) |
| "The change could not be saved." | The save was rejected | Try again; confirm you hold Operations → Edit |
| Registration is still open past the auto-close time | The scheduler worker is stale or faulted | Untick **Registration is open** and Save by hand, then check the worker on `/admin/ops/services` (§9.12) |
| Times look wrong by a few hours | Auto-close is entered and shown in **Saudi time**, not UTC | Re-enter it as Saudi local time |

#### What you cannot do here yet

- Set the two switches in one action. Each section saves on its own button.

---

## 10. System modules

### 10.1 Admins — `/admin/admins`

> Page reference: [`docs/pages/cp/admin-admins.md`](../pages/cp/admin-admins.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-admins.md`](../tests/e2e/cp-admin-admins.md)

#### What it's for

Lists every account with the `Administrator` role. This is where you onboard
a new admin colleague, view someone's details, deactivate a departing admin,
or pull the admin roster into Excel.

#### Most common tasks

##### Invite a new administrator

1. **System → Admins** → **+ Add**.
2. Fill the modal: Email, Display name, Password (min 12 chars + 1 digit +
   1 upper + 1 lower + 1 special), TOTP-on-first-login (leave on).
3. Click **Create administrator**. The new row appears Approved.
4. Send the new admin their email + password out-of-band; they'll go through
   first-time TOTP pairing on first sign-in (§2.2).

##### View an admin's details

Click the **ⓘ Details** icon on the row → read-only modal with email,
display name, state, role.

##### Deactivate a departing admin

1. Either: select the row + **Delete** in the toolbar (bulk-delete modal
   asks for a 10–500 character reason)
2. Or: click the **🗑 Delete** icon on the row (same reason modal).
3. Type the reason (this is preserved in the audit log).
4. Click **Delete**. The row vanishes. Self-delete is silently skipped — you
   cannot delete your own account.

##### Bulk-delete several admins

Tick the rows you want → **Delete** in the toolbar → reason → Submit.
The toast tells you how many were deleted vs skipped (self-delete or
unknown id).

##### Duplicate an admin

Useful when you're standing up a sister account (e.g. shared service account).
Per-row **Duplicate** icon → enter the new email → Submit. The new account
has the same role + state, fresh QR.

##### Import / Export

- **Export** — select rows + Export, OR export the entire current query
  (no selection) → XLSX downloads.
- **Import** — Import → XLSX upload (≤ 5 MB) → review the result modal
  showing created / skipped / errors per row.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| The Edit dialog does not offer the field you want | Edit on this page manages the account's **roles** — it is not a stub. An administrator's own details are edited from the list for their account type | Change roles here; edit identity fields from Visitors or Others |
| Bulk-delete reports "Deleted N, skipped 1" | Your own row was in the batch | Expected — self-delete is silently skipped |
| Import shows 50 errors | XLSX header row missing or wrong column names | Open the Export sample as the template, re-fill, re-upload |
| Toast: "Email already exists" | Trying to invite a duplicate | Find the existing admin first; if Deactivated, ask a developer (no re-activate exists yet) |

#### What you cannot do here

- **Edit an administrator's email or display name** — the Edit action on this
  page manages roles, not the account's own details.
- **Reset their 2FA** — go to `/admin/reset-2fa` (per-target reset).

#### What you can do that this manual used to say you could not

- **Change their roles.** Edit opens a role editor: tick and untick, then save.
  Administrators are not role-pinned at creation, and deleting and recreating
  an account to change a role has never been necessary. The action is gated by
  `Admins.AssignRoles`, which is a different permission from the one that lets
  you add an account at all — so somebody who may invite administrators cannot
  quietly make one powerful. The last remaining Administrator cannot have that
  role removed; the server refuses rather than leaving the system with nobody
  who can administer it.

#### Cross-references

- Page reference: [`docs/pages/cp/admin-admins.md`](../pages/cp/admin-admins.md)
- Pattern: [`SIMF_TABLE_PATTERN.md`](../dev/SIMF_TABLE_PATTERN.md)
- API: `/admin/admins/*` endpoint group in [`SIMF-API-001`](../SIMF-API-001-API-Specification.md)
- Sibling: §10.2 Pending admins (approval queue for self-registered admins).

### 10.2 Pending admins — `/admin/admins/pending`

> Page reference: [`docs/pages/cp/admin-admins-pending.md`](../pages/cp/admin-admins-pending.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-admins-pending.md`](../tests/e2e/cp-admin-admins-pending.md)

Queue of self-registered admin candidates. Per-row **Approve** (one-click,
no preview today — parity gap with the Visitor/Other equivalents) and
**Reject** (10–500 char reason). Always cross-check the candidate offline
before approving since there's no preview modal yet.

### 10.3 Others — `/admin/others`

> Page reference: [`docs/pages/cp/admin-others.md`](../pages/cp/admin-others.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-others.md`](../tests/e2e/cp-admin-others.md)

Walk-in registration for non-visitor non-admin attendees (sponsor staff,
exhibitor reps, press, contractors). Same wizard as `/admin/visitors`,
except: no Interests section, and the profile-type tiles come from
**Other profile types** (not Visitor). Make sure at least one Other
profile-type is seeded under §10.9 before run-time.

### 10.4 Pending others — `/admin/others/pending`

> Page reference: [`docs/pages/cp/admin-others-pending.md`](../pages/cp/admin-others-pending.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-others-pending.md`](../tests/e2e/cp-admin-others-pending.md)

Approval queue for Other-typed self-registrations. Same View / Approve-with-
review / Reject-with-reason flow as `/admin/visitors/pending` (§10.6).

### 10.5 Visitors — `/admin/visitors`

> Page reference: [`docs/pages/cp/admin-visitors.md`](../pages/cp/admin-visitors.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-visitors.md`](../tests/e2e/cp-admin-visitors.md)

#### What it's for

The **event-day workhorse**. On the day of the forum, exhibition staff at the
registration desk use this page to register walk-in visitors face-to-face.
Off-day, admins use it to audit the visitor roster, view full profiles (with
ID-document image), export the attendee list, and reach the reprint desk.

#### Most common tasks

##### Register a walk-in visitor (event day)

1. **System → Visitors** → **+ Add**.
2. The walk-in wizard opens with **6 numbered sections** (D-131):
   1. **Badge type** — click the colour-coded tile for the visitor's
      category (General, VIP, Press, etc. — managed under
      **Visitor profile types**).
   2. **Identity** — Name on badge first, then Date of birth, then full
      English + Arabic names, then Place of birth. (Order tuned for desk
      conversation flow.)
   3. **Nationality and ID** — toggle Saudi / Non-Saudi.
      - Saudi → 10-digit national ID starting with 1.
      - Non-Saudi → pick country, then toggle Iqama (10 digits starting
        with 2) or Passport (≤ 20 chars).
   4. **Contact** — Saudi mobile (`+9665XXXXXXXX`) or international mobile,
      optional email. Email is OK to leave blank — the QR badge is the
      access key.
   5. **ID document** — optional photo of national ID / Iqama / passport
      (PNG/JPEG/WebP, ≤ 5 MB). Stored encrypted at rest (D-129).
   6. **Interests** — pick up to 10 topics the visitor cares about (drives
      the visitor's profile picker).
3. Click **Register**.
4. The success modal pops with the freshly minted badge: profile-type colour
   stripe, name, QR code, QR id. Click **Print badge** to send to the
   printer; click **Register another** to clear and continue.

##### View a visitor's full profile + ID image

Click the **ⓘ Details** icon on the row → modal shows every field including
the inline ID document image (decrypted on demand). Close when done.

##### Reprint a lost badge

Go to **People → Print badge** (`/admin/print-bag`) — see §4.2.

##### Bulk-delete + Duplicate + Import + Export

Same shape as Admins (§10.1).

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Invalid national ID" toast | Saudi ID doesn't start with 1 OR isn't 10 digits | Re-check from the physical ID; the regex is strict |
| "Invalid Iqama number" toast | Iqama doesn't start with 2 OR isn't 10 digits | Same |
| Walk-in succeeds but ID image not on Details | Upload failed silently (network blip) | Re-open Details after 5s; if still missing, the registration is still good — image upload is best-effort |
| Walk-in form shows wrong language | Browser language toggle | Use the `العربية` link in the header to flip |
| Visitor not found on Print badge desk | Maybe registered under wrong Kind (Other instead of Visitor) | Check `/admin/others` |

#### What you cannot do here

- **Edit every identity field after walk-in.** Edit is not a stub: it opens the
  shared account form, where the email address, display name, profile type,
  nationality, both mobile numbers, the meeting preferences and the pictures are
  all editable. What it does not reach is the identity document and the
  interests, which are captured at registration.
- **Mass-register from XLSX while populating profile fields** — the import
  XLSX covers email + display name + profile-type, not the full profile.
  Use the walk-in form for profile-complete registrations.

#### Cross-references

- Page reference: [`docs/pages/cp/admin-visitors.md`](../pages/cp/admin-visitors.md)
- Print desk: §4.2 + [`admin-print-bag.md`](../pages/cp/admin-print-bag.md)
- Walk-in wizard component: `WalkInRegistrationForm.razor`
- Decisions: D-114, D-127, D-128, D-129, D-130, D-131.

### 10.6 Pending visitors — `/admin/visitors/pending`

> Page reference: [`docs/pages/cp/admin-visitors-pending.md`](../pages/cp/admin-visitors-pending.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-visitors-pending.md`](../tests/e2e/cp-admin-visitors-pending.md)

#### What it's for

Queue of self-registered visitors waiting for your approval. Approval mints
the QR badge and unlocks event entry; rejection records a reason for audit.

#### Approve a visitor (review-before-approve, D-128)

1. **System → Pending visitors**.
2. Click **View** OR **Approve** on the row. Both open the same modal
   preloaded with the visitor's full profile (Name EN/AR, nationality,
   DOB, place of birth, identity type + number, mobile, interests count,
   ID-document image inline if uploaded).
3. Read carefully — this is the moment to catch fraud / typos / wrong
   profile-type.
4. If everything checks: click **Confirm and Approve**. The modal closes,
   row vanishes, toast confirms `Approved {email}`. Visitor can now sign
   in + their QR badge is live.
5. If something's wrong: click **Cancel** and either Edit (when available)
   or Reject with a reason.

#### Reject a visitor

1. Click **Reject** on the row.
2. Type a clear reason (10–500 chars) — the visitor reads this verbatim on
   `/account/rejected` and the audit log keeps it forever.
3. Click **Reject**. Toast confirms `Rejected {email}`.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Approve button shows "Visitor not found" | Another admin already approved/rejected | Refresh the list |
| Reject Submit disabled | Reason < 10 or > 500 chars | Type more / less |
| View modal shows "no profile filled yet" | Visitor created account but didn't open the profile page | Reach out + ask them to complete `/account/profile` first |

#### What you cannot do here

- **Bulk-approve / bulk-reject** — the toolbar checkboxes render per D-132
  for consistency, but no bulk endpoint exists yet. One row at a time.
- **Edit the visitor's profile** — that's a User Management feature, not
  shipped.

### 10.7 Interests — `/admin/interests`

> Page reference: [`docs/pages/cp/admin-interests.md`](../pages/cp/admin-interests.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-interests.md`](../tests/e2e/cp-admin-interests.md)

#### What it's for

Interests are the topics visitors pick from when they fill their profile
(e.g. "Naval engineering", "Maritime law", "Logistics"). The list shows in
the visitor self-service profile picker. Keeping it accurate is your job:
add a topic when a new stream is announced, deactivate one that stopped
being relevant, reorder them so the most popular sit at the top.

#### Most common tasks

##### Add a new interest

1. **System → Interests** in the left nav (or paste `/admin/interests` in
   the URL bar).
2. Click **+ Add interest** in the toolbar.
3. Fill the modal:
   - **Name (English)** — the visitor-facing label, 1–128 characters, must
     be unique across the system.
   - **Name (Arabic)** — the Arabic translation, 1–128 characters.
   - **Display order** — a number ≥ 0; lower numbers sort first in the
     visitor picker.
4. Click **Create interest**.
5. The modal closes, the grid reloads, and a green toast says
   `Interest "..." was created.` The new row appears in the list.

##### Edit an interest

1. Find the row (use the column filters if the list is long).
2. Click the **✎ Edit** icon in the row's Actions column.
3. Adjust the fields. Edit also lets you tick / untick **Active — show in
   the visitor picker** to deactivate without deleting.
4. Click **Save changes**. Toast: `Interest "..." was updated.`

##### View an interest's full details (read-only)

1. Click the **ⓘ Details** icon in the row.
2. Modal lists Name, Name (Arabic), Display order, and Active state.
3. Click **Close** when done.

##### Deactivate an interest

1. Click the **🗑 Deactivate** icon in the row.
2. The action is immediate (no confirm modal — soft-delete only, no data is
   destroyed). Toast: `Interest "..." was deactivated.`
3. The row now shows a grey **Inactive** pill. Visitors will not see this
   interest in their picker on next load, but visitors who had already
   linked to it keep the link.
4. To reactivate: Edit the row → tick **Active** → Save.

##### Reorder the visitor picker

1. Decide the order you want.
2. Edit each row and adjust **Display order** to a number that places it
   correctly (e.g. set the top-of-list interest to `0`, the next to `10`,
   the next to `20` — leave gaps so future interests can slot in
   without renumbering everything).
3. The next time a visitor opens their profile picker, they see the new order.

#### What the page looks like

![Interests canonical view](../screenshots/d132-interests-canonical.png)

![Add interest modal](../screenshots/d132-interests-add-modal.png)

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `Interest "X" was created.` doesn't appear in the list | The list filter is hiding it | Clear the column filters; reload the page if needed |
| Toast: "An interest with this name already exists" (or Arabic equivalent) | The English name is not unique (case-insensitive) | Pick a different name; you can still set the existing one's Display order if it should sort higher |
| Toast: "The operation could not be completed." | Server or network error | Reload; if it persists, check the [Logs viewer](#108-logs-viewer) |
| Add modal won't submit (button stays disabled) | One of the three fields failed client validation | The form shows the field-level error in red under the field |
| `/admin/interests/new` shows 404 | This deep-link was deleted in D-132 | Use the **+ Add interest** button on the list page |

#### What you cannot do here

- **Bulk-deactivate** — the checkboxes render for consistency but no bulk
  action is wired. Deactivate one row at a time.
- **Reorder by drag-and-drop** — use the Display order field.
- **Delete permanently** — Deactivate is soft-only. Permanent delete is not
  exposed; if a row must vanish, ask a developer.
- **Translate to a third language** — only English + Arabic are supported.

#### Cross-references

- Page reference: [`docs/pages/cp/admin-interests.md`](../pages/cp/admin-interests.md)
- API spec: [`SIMF-API-001`](../SIMF-API-001-API-Specification.md) — `/admin/interests` endpoints
- E2E tests: [`docs/tests/e2e/cp-admin-interests.md`](../tests/e2e/cp-admin-interests.md) _(pending)_
- Pattern: [`docs/dev/SIMF_TABLE_PATTERN.md`](../dev/SIMF_TABLE_PATTERN.md)
- Use cases: `SIMF-UCS-001 § UC-INT-*` _(pending)_

### 10.8 Visitor profile types — `/admin/profile-types/visitor`

> Page reference: [`docs/pages/cp/admin-profile-types-visitor.md`](../pages/cp/admin-profile-types-visitor.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-profile-types-visitor.md`](../tests/e2e/cp-admin-profile-types-visitor.md)

#### What it's for

Visitor profile types are the **colour-coded tiles** that appear at the top
of the walk-in registration wizard on `/admin/visitors`. Each row has a
bilingual name, a PageColor (the tile + badge stripe), and an active flag.
Add a new type before run-time when a new attendee category is announced
(e.g. "Press", "VIP", "Speaker").

#### Most common tasks

1. **System → Visitor profile types** → **+ Add**.
2. Fill: Name (EN), Name (AR), PageColor (paired text + colour-picker swatch
   — pick from the picker or type `#rrggbb` / `var(--brand-blue)`).
3. Save. The new tile appears in the walk-in wizard on next page load.
4. Edit / Deactivate identically to the canonical CRUD pattern (§3.1).

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Tile color shows navy in walk-in despite saving "red" | PageColor isn't a valid `#rrggbb` | Use the picker; or paste a 6-digit hex |
| Delete fails with "Profile type in use" | Visitors are linked to it | Deactivate instead (soft); the existing visitors keep their link, new walk-ins won't see it |
| "Duplicate name" | Same EN name as an existing type (case-insensitive) | Use a slightly different name |

### 10.9 Other profile types — `/admin/profile-types/other`

> Page reference: [`docs/pages/cp/admin-profile-types-other.md`](../pages/cp/admin-profile-types-other.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-profile-types-other.md`](../tests/e2e/cp-admin-profile-types-other.md)

Identical to §10.8 but for the Other-typed walk-in wizard at `/admin/others`.
Both pools are completely separate (a "Press" Visitor tile and a "Press" Other
tile are independent rows; that's intentional so the two walk-in flows can
diverge over time).

### 10.10 Reset user 2FA — `/admin/reset-2fa`

> Page reference: [`docs/pages/cp/admin-reset-2fa.md`](../pages/cp/admin-reset-2fa.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-reset-2fa.md`](../tests/e2e/cp-admin-reset-2fa.md)

When a user loses both their authenticator and all 10 recovery codes:

1. **System → Reset user 2FA**.
2. Type the user's email substring → pick the match → click **Reset 2FA**.
3. Confirm in the modal.
4. The server wipes their authenticator + recovery codes + active sessions
   and emails them out-of-band. They re-pair on next sign-in.

You cannot self-reset here — use **My profile → Reset my 2FA** instead.

### 10.12 Operation log viewer — `/admin/operation-log` (D-134 Sprint A)

> Page reference: [`docs/pages/cp/admin-operation-log.md`](../pages/cp/admin-operation-log.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-operation-log.md`](../tests/e2e/cp-admin-operation-log.md)

The **business + security audit trail**. Browse + filter every event
the system has audited: sign-ins (success + failure), password resets,
registrations, approvals, 2FA resets, walk-ins, role changes, etc.
Distinct from §10.11 — that page tails the raw Serilog files for
debugging; this page is the structured durable audit table.

#### Most common tasks

- **Find someone's last activity:** type their email into "Subject
  email contains" → Apply filters → newest row first.
- **See every failure today:** Outcome = Failure → Apply → Details on
  the latest row shows CorrelationId + ErrorCode + the Detail blob.
- **Trace a single request across services:** open Details on one row,
  copy the CorrelationId, paste it into the technical Logs viewer
  (§10.11) — every Serilog line with the same id is the same request.

#### What you cannot do here

- Edit or delete an audit entry — the table is append-only by design.
- Export to XLSX — coming in a follow-up.
- Filter by date range from the page — coming in a follow-up; the API
  already accepts `from`+`to` filters.

### 10.11 Logs viewer — `/admin/logs`

> Page reference: [`docs/pages/cp/admin-logs.md`](../pages/cp/admin-logs.md)
> · E2E catalogue: [`docs/tests/e2e/cp-admin-logs.md`](../tests/e2e/cp-admin-logs.md)

Read-only tail of every project's log files. Pick **Project** (Api,
ControlPanel, Website) → **File** (per day) → **Lines** (50/100/500/1000)
→ optionally tick **Auto-refresh** (5 s poll). The body shows the tail in
a monospaced `<pre>` block. **Download** streams the full file to disk.

Use this when:

- An admin reports an error → find the matching timestamp in the Api log.
- Investigating a sign-in failure → look for `401` / `Authentication is required`
  in the corresponding project log.
- A walk-in registration failed mysteriously → check Api log for
  `AdminWalkInRegistrationRequestValidator` errors.

---

---

## 11. Reporting modules

### 11.1 Reports hub — `/admin/reports`

> Page reference: [`docs/pages/cp/reports.md`](../pages/cp/reports.md)
> · E2E catalogue: [`docs/tests/e2e/cp-reports.md`](../tests/e2e/cp-reports.md)

The **Reports** group in the sidebar holds eight read-only, date-ranged views over
records the rest of the Control Panel already owns: sessions, hall arrivals,
accounts, gate scans, ratings, partners, meeting requests and audience questions.
Reporting stores nothing of its own and never writes, so nothing you do here can
change a booking, an account or a scan. The hub is the landing page: a grid of
cards, one per report. You only see a card for a report you are allowed to open,
so the hub never sends you to a page that will bounce you.

#### Most common tasks

##### Open a report

1. **Reports → All reports** (or the report's own sidebar entry: *Attendance
   report*, *Registrations report*, *Gate activity report*, *Sessions report*,
   *Ratings report*, *Partners report*, *Meetings report*, *Engagement report*).
2. Click the card. Each card carries the report's one-line description.

##### Set the period (every report except Partners)

1. **From** and **To** are **Saudi calendar days** and the range is **inclusive at
   both ends**: "6 to 8 November" includes all of the 8th, to the last minute of
   the Saudi evening (D-805). Both ends are optional; leave one blank for no bound
   in that direction.
2. **Apply** reloads the grid and jumps back to page 1.
3. **Clear** appears once either end is set; it empties both ends and reloads.

##### Export a report

1. **Export to Excel** in the toolbar strip, to the right of the dates.
2. The workbook contains the **whole filtered set**, not just the page on screen,
   capped at 20,000 rows. Past the cap you get the first 20,000 rows in the
   report's current order, so narrow the dates instead of trusting a capped file.
3. The file is named `simf-<report>-<date>-<time>.xlsx`, stamped in Saudi time.

#### Permissions: two separate gates

Each report has its **own** permission (Reports → Attendance, Registrations,
Gates, Sessions, Ratings, Partners, Meetings, Engagement) so security staff can be
given the gate log without the attendee roster. The hub itself needs Reports →
View. **Export is a further, separate permission** shared by all eight reports:
taking a spreadsheet of attendees off site is a bigger act than reading a page of
it on screen. Without it you can read every report you are entitled to and the
**Export to Excel** button simply is not rendered.

#### What every report page has, and what it does not

The strip above the grid is the same everywhere: **From**, **To**, **Apply**,
**Clear**, **Export to Excel**, then the headline figures. Those figures describe
the **whole filtered set**, not the visible page, so they do not move when you
turn the page. The grid has sortable column headers (click to sort, click again
to reverse), and the full pager: **First page / Previous / numbered pages / Next /
Last page**, plus **Show 10 / 20 / 50 / 100**. Only the numbered page buttons
carry visible text; First page, Previous, Next and Last page are chevron icons,
and those names are their hover tooltips, so do not hunt for a button captioned
"First page".

There is deliberately no Add, Edit, Details or Delete, no row checkboxes, no
per-column filter boxes and no free-text search box. The date range and the column
sort are the only controls.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "You do not have permission to open this page. If you believe this is a mistake, contact your administrator." | You opened a report route without that report's permission | Ask for the matching Reports permission; the hub only lists reports you already hold |
| No **Export to Excel** button on any report | You hold the per-report permissions but not Reports → Export | Ask for the export permission separately |
| "The start date is after the end date." under **From** | The two ends are the wrong way round | Fix either end; **Apply** does nothing until the message clears |
| "No records match this period." | The range genuinely contains nothing | Widen the range, or clear it to see everything |
| "The report could not be loaded. Try again, or narrow the date range." | The query failed or timed out | Reload, then narrow the range; a very wide range over a busy day is the usual cause |
| **Export to Excel** produces no file and no message | The download failed. A dropped session sends you to the sign-in page; other failures are not reported on the page | Sign in again, narrow the range, retry |
| Sorting a date column looks backwards | On Registrations, Gate activity, Ratings and Engagement the grid opens **newest first**, but nothing is marked as sorted yet: every header shows the neutral ↕ until you click one. The first click on the date column is still newest first, now marked ▲ | Click the header a second time for oldest first |
| A report shows a session or account that another page does not | Reports read the live tables and are not cached | Re-check the source module; the report is not the stale one |

### 11.2 Attendance report — `/admin/reports/attendance`

> Page reference: [`docs/pages/cp/reports.md`](../pages/cp/reports.md)
> · E2E catalogue: [`docs/tests/e2e/cp-reports.md`](../tests/e2e/cp-reports.md)

Answers "was this session attended". One row per active session whose **start**
falls inside the period, with how many distinct people arrived and how many are
still in the hall. It reads the hall-arrival records, so a session with no
check-in scanning shows zero. Needs the Reports → Attendance permission.

#### Most common tasks

##### Read the grid

1. **Code** and **Session** identify the session as the Sessions module (§5.4)
   named it. Sortable.
2. **Hall** is the room from Halls and seating (§5.2). Sortable.
3. **Start** is the session start in Saudi time. Sortable, and the default order.
4. **Attendees** is a **distinct person** count: someone who steps out and comes
   back counts once, not twice.
5. **Inside now** is arrivals with no departure recorded. A non-zero figure shows
   as a green pill, zero as plain text. On a finished session a stubborn non-zero
   figure usually means people left without being scanned out.

##### Read the headline figures

**Sessions**, **Distinct attendees** and **Inside now**, all for the whole period,
not the page.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| A session you expected is missing | Its start is outside the range, or it was deactivated in the Sessions module | Widen the range; re-check the session is still active |
| **Attendees** is lower than the badges you scanned | The figure counts distinct people, and gate scans are a different record from hall arrivals | Compare against the Gate activity report (§11.4), which counts scans |
| **Inside now** stays high after the session ended | Nobody scanned those attendees out | Expected. The figure is arrivals without a departure, not a live head count |

#### What you cannot do here yet

- No per-hall or per-day breakdown; the grid is one row per session.
- No attendee names. Use the Registrations report (§11.3) for people.

### 11.3 Registrations report — `/admin/reports/registrations`

> Page reference: [`docs/pages/cp/reports.md`](../pages/cp/reports.md)
> · E2E catalogue: [`docs/tests/e2e/cp-reports.md`](../tests/e2e/cp-reports.md)

Who signed up, and where each account stands. One row per **attendee** account
created inside the period. Administrator accounts are excluded on purpose: this
reports who registered for the forum, not who runs it. Needs the Reports →
Registrations permission.

#### Most common tasks

##### Read the grid

1. **Name** and **Email** come from the account. Both sortable.
2. **Profile type** is resolved from the attendee's profile (§10.8). It is blank
   when the account has no profile row yet or no type was chosen. Not sortable.
3. **Account state** is the approval state you act on from Visitors and Pending
   visitors (§10.5, §10.6). Sortable.
4. **Registered** is the Saudi date and time the account was created. Sortable,
   and the default order, **newest first**.

##### Read the headline figures

**Registrations**, **Approved** and **Pending**, for the whole period.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| **Profile type** is blank on some rows | The account has no profile row, or its profile has no type assigned | Open the attendee in Visitors (§10.5) and set the type; the report reads it live |
| An admin you just created is missing | Admin accounts are excluded by design | Use Admins (§10.1) |
| **Pending** does not match the Pending visitors list | The report counts the period; the pending list counts everything still waiting | Clear the date range to compare like with like |

### 11.4 Gate activity report — `/admin/reports/gates`

> Page reference: [`docs/pages/cp/reports.md`](../pages/cp/reports.md)
> · E2E catalogue: [`docs/tests/e2e/cp-reports.md`](../tests/e2e/cp-reports.md)

Every recorded gate scan in the period, allowed and denied, with the reason for
each refusal. This is the report a security lead reads after a shift, and the one
an auditor asks for. Needs the Reports → Gates permission, which is separate from
the Gates module's own operate and manage permissions.

#### Most common tasks

##### Read the grid

1. **Gate** is the gate the scan was taken at. Sortable.
2. **Scanned** is the **server's** time for the scan in Saudi time, not the
   handset's clock. Sortable, default order, newest first.
3. **Visitor** and **Profile type** are the values captured **at the moment of the
   scan**, not a live lookup. A historic row therefore still reads correctly after
   the account is renamed or removed, which is the point of an audit trail.
   **Visitor** is sortable.
4. **Direction** is the check-in or check-out sense of the scan.
5. **Outcome** is a green pill for an allowed scan and a grey pill for **Denied**.
   Sortable.
6. **Denial reason** carries the refusal code, and is blank on an allowed scan.

##### Read the headline figures

**Scans**, **Allowed**, **Denied** and **Distinct admitted**. The last one counts
distinct people let in on a check-in scan, which is the figure operations
actually want: a raw scan count double-counts anyone who re-enters.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| A scan is missing from the range you picked | The row is stamped with the server time, and the range is inclusive Saudi days | Re-check the dates; a scan just before midnight belongs to that Saudi day |
| **Visitor** is blank | The scan recorded no linked person, for example an unrecognised badge | Expected on a denied scan; check **Denial reason** |
| A renamed attendee still shows the old name | Correct. The row is the snapshot taken at scan time | Use the Registrations report (§11.3) for current names |
| **Distinct admitted** is far below **Allowed** | People re-entered, and each entry is a scan | Expected. Compare **Distinct admitted** between days, not against **Scans** |

### 11.5 Sessions report — `/admin/reports/sessions`

> Page reference: [`docs/pages/cp/reports.md`](../pages/cp/reports.md)
> · E2E catalogue: [`docs/tests/e2e/cp-reports.md`](../tests/e2e/cp-reports.md)

Answers "how did this session do". The same session set as the Attendance report,
plus the speaker line-up, the number of audience questions and the audience score.
Needs the Reports → Sessions permission.

#### Most common tasks

##### Read the grid

1. **Code**, **Session**, **Hall** and **Start** behave exactly as in the
   Attendance report (§11.2). All four are sortable; **Start** is the default.
2. **Speakers** is the session's speakers joined into one line, primary speaker
   first, in the same order the public agenda shows them.
3. **Attendees** is the same distinct-person count as §11.2.
4. **Questions** counts every audience question asked in the session, including
   hidden ones. The Engagement report (§11.9) lists them.
5. **Average rating** is the mean overall star score to one decimal. A session
   nobody rated shows **blank**, not `0.0`, because a zero would read as a
   unanimously terrible session.

##### Read the headline figures

**Sessions** and **Questions**, for the whole period.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| **Average rating** is blank | Nobody submitted a star score for that session | Expected. Check the Ratings report (§11.6) for what was submitted |
| **Speakers** is empty | No speaker is attached to the session | Add one on the session form (§5.4) |
| **Questions** is higher than what the audience saw | Hidden questions are counted here | Open the Engagement report (§11.9); its export marks which were hidden |

### 11.6 Ratings report — `/admin/reports/ratings`

> Page reference: [`docs/pages/cp/reports.md`](../pages/cp/reports.md)
> · E2E catalogue: [`docs/tests/e2e/cp-reports.md`](../tests/e2e/cp-reports.md)

Every rating submitted in the period, with its comment. **The respondent is not
reported, on purpose.** Ratings carry free text and this is an anonymous feedback
channel; putting a name beside a comment would change what the feature is. Needs
the Reports → Ratings permission.

#### Most common tasks

##### Read the grid

1. **Rating type** is the feedback form that was answered. Sortable.
2. **Scope** tells you what that form rates: a session, a day, or the whole forum.
   It is what tells you how to read the row.
3. **Stars** is the overall score, blank when the respondent left only a comment.
   Sortable.
4. **Comment** is the free text as written.
5. **Submitted** is the Saudi date and time. Sortable, default order, newest first.

##### Read the headline figures

**Ratings**, **Average rating** (blank when nothing in the period carried a score)
and **With a comment**.

##### Know what the export adds

The workbook carries one extra column the screen does not: **Target**, the
internal id of the thing rated. It is an id rather than a name because what it
points at depends on the scope, so use it to group rows in Excel, not to read.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| A rating you know was submitted is missing | It was deactivated, or its submission date is outside the range | Widen the range; deactivated ratings are excluded |
| **Average rating** is blank while rows are listed | Every row in the period carried a comment but no star score | Expected |
| You need to know who said something | The report withholds the respondent by design | There is no way to attribute a rating from this page |

### 11.7 Partners report — `/admin/reports/partners`

> Page reference: [`docs/pages/cp/reports.md`](../pages/cp/reports.md)
> · E2E catalogue: [`docs/tests/e2e/cp-reports.md`](../tests/e2e/cp-reports.md)

Exhibitors, sponsors and booths flattened into a single contact directory, so you
can work the whole partner surface from one list instead of three pages. Only
active records appear. Needs the Reports → Partners permission.

**This report ignores the date range.** A partner directory answers "who is
participating", not "what happened this week", so **From**, **To** and **Apply**
have no effect on what you see. That is deliberate, not a fault.

#### Most common tasks

##### Read the grid

1. **Kind** is `Exhibitor`, `Sponsor` or `Booth`. Sortable, and the default order
   is by kind then name so the three lists stay grouped.
2. **Name** is the English name. Sortable. Partner names are **bilingual**: both
   the English and the Arabic name are stored, and the Arabic one is in the export.
3. **Tier** is the sponsorship or exhibitor tier. For a **booth** row this column
   carries the booth's **sector** instead.
4. **Email**, **Phone** and **Website** are the contact details. For a booth row
   these are the **booth officer's** details.

##### Read the headline figures

**Partners**, **Exhibitors**, **Sponsors** and **Booths**.

##### Know what the export adds

The workbook adds **Name (Arabic)** and **Active** to the six columns on screen.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Changing the dates changes nothing | Correct. This report has no period | Use the range on the other reports |
| A partner is missing | The record is inactive in its own module | Re-activate it in Exhibitors, Sponsors or Booths |
| **Tier** shows something that is not a tier | The row is a booth, and the column carries its sector | Check the **Kind** column first |
| Contact details are blank | The source record has none | Fill them in on the partner's own page |

### 11.8 Meetings report — `/admin/reports/meetings`

> Page reference: [`docs/pages/cp/reports.md`](../pages/cp/reports.md)
> · E2E catalogue: [`docs/tests/e2e/cp-reports.md`](../tests/e2e/cp-reports.md)

Speaker meeting requests and delegation meeting requests in one list. They are
managed on separate pages but have the same operational shape, and an organiser
chasing unanswered requests wants one list, not two. Needs the Reports → Meetings
permission.

The date range filters on **when the request was made**, not on the meeting slot.
"What came in this week" is the question this report answers.

#### Most common tasks

##### Read the grid

1. **Kind** is `Speaker` or `Delegation`. Sortable.
2. **Requester** and **Target** read differently per kind: on a speaker row the
   requester is the person who asked and the target is the speaker; on a
   delegation row both are country names.
3. **Subject** is the request's subject line as written.
4. **Slot** is the agreed date and start time, followed by the end time. It is
   **blank** when no slot has been agreed yet, rather than showing an invented one.
5. **Status** is the request's state, for example `Pending`. Sortable.
6. **Requested** is when the request came in.

##### Read the headline figures

**Meeting requests**, **Pending** and **Checked in**.

##### Know what the export adds

The workbook adds a **Checked in** column, which the screen does not show per row.
Use the export when you need to know which specific meetings were attended.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| The list is not in date order | The default order sorts on the requested date as displayed text, so it groups by day of the month rather than strictly newest first | Narrow the range, or sort by **Kind** or **Status**; the export carries the exact dates |
| **Slot** is blank on many rows | Those requests have no agreed slot yet | Expected. Check **Status** |
| A request is missing | It was made outside the range. The range is the **request** date, not the meeting date | Widen the range |
| You need to see who attended | Per-row check-in is in the export only | Export to Excel and read the **Checked in** column |

### 11.9 Engagement report — `/admin/reports/engagement`

> Page reference: [`docs/pages/cp/reports.md`](../pages/cp/reports.md)
> · E2E catalogue: [`docs/tests/e2e/cp-reports.md`](../tests/e2e/cp-reports.md)

The audience questions asked in sessions, with their moderation state.
**Hidden questions are included.** This is a moderation report: reviewing what was
asked means seeing what was suppressed, which is exactly what a public-facing view
filters out. **The asker is not reported**, for the same reason as the Ratings
report: the moderation decision is about the question, not the person. Needs the
Reports → Engagement permission.

#### Most common tasks

##### Read the grid

1. **Code** and **Session** identify the session the question was asked in.
   **Session** is sortable.
2. **Question** is the text as the attendee typed it.
3. **Recipient** is who the question was aimed at.
4. **Status** is the moderation state. Sortable.
5. **Phase** is the point in the session the question belongs to.
6. **Asked** is the Saudi date and time. Sortable, default order, newest first.

##### Read the headline figures

**Questions**, **Hidden** and **Pushed to speaker**.

##### Find the hidden questions

The grid has **no Hidden column**, so a suppressed question sits in the list
looking like any other. The **Hidden** headline figure tells you how many there
are; **Export to Excel** and read the workbook's **Hidden** column to see which.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| A question the audience never saw is listed | Hidden questions are included on purpose | Expected. Export to identify them |
| The **Hidden** figure is above zero but no row looks hidden | The screen has no Hidden column | Export to Excel; the workbook has one |
| **Questions** here does not match the Sessions report | The Sessions report counts per session over its own period; this one counts questions over the question's period | Match the ranges, then compare |
| You need to know who asked | The report withholds the asker by design | There is no way to attribute a question from this page |

---

## 12. Account-area surfaces

### 12.1 My profile — `/account/profile`

> Page reference: [`docs/pages/cp/account-profile.md`](../pages/cp/account-profile.md)
> · E2E catalogue: [`docs/tests/e2e/cp-account-profile.md`](../tests/e2e/cp-account-profile.md)

Reach via the **You** link in the top header.

| Card | What you can do |
|------|-----------------|
| Identity | Read your email (locked) + edit your display name |
| Avatar | Upload + crop a new avatar (PNG/JPEG/WebP, ≤ 2 MB) |
| Security | Reset your own 2FA (re-pair flow) + regenerate your 10 recovery codes |
| Sessions | See every active session + revoke any of them |

### 12.2 Notifications inbox — `/account/notifications`

> Page reference: [`docs/pages/cp/account-notifications.md`](../pages/cp/account-notifications.md)
> · E2E catalogue: [`docs/tests/e2e/cp-account-notifications.md`](../tests/e2e/cp-account-notifications.md)

#### What it's for

Your personal inbox. The header **🔔 bell** shows the unread count;
clicking it opens a small menu with the latest few + a **View all** link
that lands here.

#### Most common tasks

| Want to | Do |
|---------|----|
| See every notification | Just open the page |
| Read the full body of one | Click the ⓘ **Details** icon |
| Dismiss one | Click the 🗑 **Delete** icon |
| Dismiss several at once | Select the rows + **Delete** in the toolbar |
| Mark every unread as read (but keep them) | **Mark all as read** below the grid |

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Bell shows N unread but page is empty | Filter or pager hides them | Clear filters; check page 1 |
| Bulk-delete is slow | No bulk endpoint — fires N delete requests | Select fewer rows at once (≤ 25 = visible page) |
| Notification body is `??????` for one language | Translation missing on that row | Open Details to see the other-language variant |

### 12.3 TOTP pairing — `/account/totp-pairing`

> Page reference: [`docs/pages/cp/account-totp-pairing.md`](../pages/cp/account-totp-pairing.md)
> · E2E catalogue: [`docs/tests/e2e/cp-account-totp-pairing.md`](../tests/e2e/cp-account-totp-pairing.md)

You only land here at first sign-in OR after Reset-my-2FA. Walkthrough is
in §2.2 above — scan QR / manual-entry secret / 6-digit verify / save the
10 recovery codes.

---

## 13. Security boundaries

- Every `/admin/*` page requires the `Administrator` role AND an `Approved`
  account state. Pending or Rejected admins cannot reach admin pages even
  with a valid cookie.
- Every state-changing API call is row-audited (D-109) — the actor, the
  before/after row, the timestamp.
- TOTP is mandatory for every CP user — there is no "remember this device"
  bypass for admin accounts.
- Recovery codes are one-time-use. If you use one, generate fresh codes via
  **My profile → Recovery codes** on your next session.
- The session refresh is automatic up to the cookie's 8-hour lifetime
  (D-121); after that you sign in again.

## 14. Troubleshooting index

| Looking for | Section |
|-------------|---------|
| Can't sign in | §2.5 |
| Stuck on TOTP page | §2.5 |
| Lost phone | §2.4 |
| Menu item shows SOON | §3.2 |
| Add interest fails | §10.7 |
| Where's the logs viewer | §10.11 |

## 15. Glossary

- **TOTP** — Time-based One-Time Password. The 6-digit code your authenticator
  generates every 30 seconds.
- **PendingApproval** — account state for self-registered users awaiting
  admin approval.
- **Approved** — fully signed-off account; the only state that can sign in.
- **Rejected** — admin actively turned the account down; can sign in to see
  the rejection reason but nothing else.
- **Soft-delete** — sets `IsActive = false` on the row; data is preserved.
- **Modal** — overlay dialog over the page; ESC or the × button closes it.
- **BFF** — Backend-For-Frontend; the `/account/api/*` routes that proxy
  to the real `/api/v1/*` API.
- **QR badge** — the encrypted QR code minted for each Approved visitor.
  Scanning it at the venue gate proves identity.
- **Walk-in** — a visitor registered at the on-site registration desk by
  staff (`/admin/visitors` → Add).
- **Lookup table** — a small reference list (e.g. Interests, Profile types)
  managed via simple CRUD; no workflow.

---

_Last reviewed:_ 2026-05-28 by Claude (D-133 vertical slice).
_Next milestone:_ author one more module chapter (Admins or Visitors)
per follow-up commit so the operator has a multi-module reference before the
remaining 25+ chapters land.
