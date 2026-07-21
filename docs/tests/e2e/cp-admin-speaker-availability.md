# E2E test catalogue — Speaker availability (`/admin/speaker-availability`)

| | |
|--|--|
| **Route** | `/admin/speaker-availability` |
| **Surface** | Control Panel |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-20 (D-476 #11, Group G phase 1c) |

> **What this page does (grounded in `SpeakerAvailabilityPage.razor`, D-474/D-476).**
> The team defines a speaker's **availability windows** (Start/End UTC + slot length);
> the VIP-meeting flow (D-475) chops each window into free slots a VIP can book. The
> page: a speaker `<select>`, an **add-window** form (Start, End, slot minutes), and the
> selected speaker's window list with delete. Gated by `SpeakerMeetingRequests.Manage`
> (page + nav + each action). API: `GET`/`POST /admin/speakers/{id}/availability-windows`,
> `DELETE /admin/speaker-availability-windows/{id}` — covered by
> `tests/SIMF.Api.Tests/SpeakerAvailabilityTests.cs` (4/4: create→list→2 slots;
> accepted-slot excluded; invalid 400 + unknown-speaker 404; delete clears slots).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-SAV-001 | Pick a speaker, add a 60-min window @ 30-min slots → it lists; the free-slots read yields 2 slots | happy | P0 | authored ✓ (SpeakerAvailabilityTests, API) |
| E2E-SAV-002 | Delete a window → it leaves the list and its slots disappear | happy | P1 | authored ✓ (SpeakerAvailabilityTests, API) |
| E2E-SAV-003 | Invalid window (end ≤ start, or shorter than one slot) → 400, no row added | error | P1 | authored ✓ (SpeakerAvailabilityTests, API) |
| E2E-SAV-004 | A slot already taken by an accepted meeting is not offered (D-475) | edge | P0 | authored ✓ (SpeakerAvailabilityTests, API) |
| E2E-SAV-005 | Auth gate — admin lacking `SpeakerMeetingRequests.Manage` → `/not-permitted`; nav item hidden | auth | P0 | _to author_ (gate verified by CpNavigationPermissionTests) |
| E2E-SAV-006 | RTL / Arabic render — page + add form mirror | i18n | P1 | _to author_ |
| E2E-SAV-007 | Forum-day bound - a window outside the event days is rejected; the Start/End pickers carry the forum min/max (D-753) | error | P0 | authored ✓ (SpeakerAvailabilityTests, API) |

## Scenarios

### E2E-SAV-001/002 — Define + remove a window

```gherkin
Feature: Speaker availability windows
Background:
  Given an Administrator has signed in to the Control Panel
  And they are on /admin/speaker-availability

Scenario: Add a window and see its free slots
  When they select a speaker and add a window 2026-11-20 10:00-11:00 UTC with 30-minute slots
  Then POST /account/api/admin/speakers/{id}/availability-windows returns 200
  And the window appears in the list
  And GET /app/speakers/{id}/available-slots returns two 30-minute slots

Scenario: Delete a window
  When they delete the window
  Then DELETE /account/api/admin/speaker-availability-windows/{id} returns 200
  And the window leaves the list and the speaker has no free slots
```

### E2E-SAV-007 - Forum-day bound (D-753)

```gherkin
Feature: Availability windows are bounded to the forum days
Background:
  Given the programme has authored days 2026-11-20..22 (the forum window)
  And an Administrator is on /admin/speaker-availability with a speaker selected

Scenario: A window outside the event days is rejected
  When they add a window on 2026-12-15 (a future day AFTER the last forum day)
  Then POST /account/api/admin/speakers/{id}/availability-windows returns 400 VALIDATION_FAILED
    (bilingual toast: "Availability windows can only be set within the forum days
    (2026-11-20 to 2026-11-22)." /
    "لا يمكن تحديد فترات التوفّر إلا خلال أيام الملتقى (2026-11-20 إلى 2026-11-22).")
  And no window is added

Scenario: The Start / End pickers advertise the forum-day min/max
  When the add-window form renders
  Then GET /account/api/admin/programme/forum-window returns MinDate 2026-11-20 and MaxDate 2026-11-22
  And the Start and End datetime-local fields carry Min="2026-11-20T00:00" and Max="2026-11-22T23:59"
  # Replaces the former hardcoded 2026-11-23..25 window. The forum-window read is gated
  # by the existing SpeakerMeetingRequests.Manage-adjacent BusinessMeetings.View
  # permission (no new permission code); when no programme days exist the bound is skipped
  # and the pickers carry no min/max (the server still enforces on submit).

Scenario: The client-side out-of-range toast names the live forum window (Item #40)
  Given the forum window read from the backend is 2026-11-23..25
  When they enter a Start / End on 2026-12-01 (after the last forum day) and click Add
  Then no POST is sent and an error toast renders the dynamic window, not a hardcoded literal
    (EN "Dates must be within 23-25 November 2026." /
     AR RTL "يجب أن تكون التواريخ ضمن 23-25 نوفمبر 2026.")
  # The toast text is Admin.SpeakerAvailability.BadDateRange = "Dates must be within {0}." /
  # "يجب أن تكون التواريخ ضمن {0}." with {0} built from _forumMinDate/_forumMaxDate via
  # SIMF.Common.EventDateRange; Arabic vs English follows CultureInfo.CurrentUICulture text
  # direction. A cross-month / cross-year window spells out both endpoints
  # (e.g. "30 November - 2 December 2026").
```

**Evidence:** `SpeakerAvailabilityTests.Create_window_outside_the_forum_window_is_400`
(and the in-window `Create_window_then_it_lists_and_yields_slots`, now anchored to
2026-11-20) for the server rule; `SpeakerAvailabilityBadDateRangeTests` (CP bUnit)
for the dynamic client-side toast rendering the formatted range in EN + AR.

---

_Last reviewed:_ 2026-07-22 by Claude: Item #40 (the client-side out-of-range toast now renders the live forum window via SIMF.Common.EventDateRange instead of the hardcoded "23-25 November 2026"; Admin.SpeakerAvailability.BadDateRange is now a "{0}" format string; added the dynamic-toast scenario + SpeakerAvailabilityBadDateRangeTests evidence). Prior: 2026-07-20 by Claude — D-753 (forum-day bound on window creation via ProgrammeDay MIN/MAX; Start/End pickers fed by GET /admin/programme/forum-window, replacing the hardcoded 2026-11-23..25 window; added E2E-SAV-007). Prior: 2026-06-20 by SIMF Team — D-476 (#11) new speaker-availability admin page (Group G phase 1c).
