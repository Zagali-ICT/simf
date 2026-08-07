# E2E test catalogue — Hall availability (`/admin/hall-availability`)

| | |
|--|--|
| **Route** | `/admin/hall-availability` |
| **Surface** | Control Panel |
| **Auth setup** | `superadmin@simrsnf.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-07-09 (D-715 — item 7, FDS-013 §15 GAP-1) |

> **What this page does (grounded in `HallAvailabilityPage.razor`, D-715).**
> The team defines a **hall's meeting time** — availability windows (Start/End UTC +
> slot length) for the halls used to host business meetings. The meeting-review flow
> (GAP-2) chops each window into free slots and binds an accepted request to one. The
> page: a hall `<select>` (Meeting/General halls only), an **add-window** form
> (Start, End, slot minutes), and the selected hall's window list with delete.
> **Gated by `HallAvailability.Manage`** (page + nav + the delete action); the two
> reads carry `HallAvailability.View`. **QA A36** replaced the previous
> `SpeakerMeetingRequests.*` gate: the windows are a property of the *hall* and
> their free slots are read by BOTH meeting Approve modals (speaker AND
> delegation), so borrowing the speaker desk's code locked a delegation-only or
> halls-only operator out of a surface they legitimately run. A meeting-desk role
> now needs exactly one extra grant — `HallAvailability.View` — to read slots.
> API: `GET`/`POST /admin/halls/{id}/availability-windows`,
> `DELETE /admin/hall-availability-windows/{id}`, `GET /admin/halls/{id}/available-slots`
> — covered by `tests/SIMF.Api.Tests/HallAvailabilityTests.cs` (create→list→2 slots;
> invalid 400 + unknown-hall 404; delete clears slots). Symmetric with the
> speaker-availability page (`cp-admin-speaker-availability.md`).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-HAV-001 | Pick a hall, add a 60-min window @ 30-min slots → it lists; the free-slots read yields 2 slots | happy | P0 | authored ✓ (HallAvailabilityTests, API) |
| E2E-HAV-002 | Delete a window → it leaves the list and its slots disappear | happy | P1 | authored ✓ (HallAvailabilityTests, API) |
| E2E-HAV-003 | Invalid window (end ≤ start, or shorter than one slot) → 400, no row added; unknown hall → 404 | error | P1 | authored ✓ (HallAvailabilityTests, API) |
| E2E-HAV-004 | A slot already bound to a meeting is not offered (GAP-2) | edge | P0 | authored ✓ (HallAvailabilityTests `A_bound_meeting_removes_its_slot_from_available_slots`, D-716) |
| E2E-HAV-005 | Auth gate — admin lacking `HallAvailability.Manage` → `/not-permitted`; nav item hidden | auth | P0 | authored ✓ (gate verified by CpNavigationPermissionTests + PermissionEnforcementTests) |
| E2E-HAV-006 | Only Meeting/General halls appear in the picker | edge | P1 | authored ✓ (browser) |
| E2E-HAV-007 | RTL / Arabic render — page + add form mirror | i18n | P1 | authored ✓ (browser) |
| E2E-HAV-008 | QA A36 — the gate is the hall-scoped `HallAvailability` pair, not the speaker desk | auth | P0 | authored ✓ (HallAvailabilityTests `A36_hall_availability_is_gated_by_its_own_permission_not_the_speaker_desk`) |
| E2E-HAV-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-HAV-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-HAV-001/002 — Define + remove a window

```gherkin
Feature: Hall availability windows (meeting time)
Background:
  Given an Administrator has signed in to the Control Panel
  And they are on /admin/hall-availability

Scenario: Add a window and see its free slots
  When they select a Meeting hall and add a window 2030-01-01 10:00–11:00 UTC with 30-minute slots
  Then POST /account/api/admin/halls/{id}/availability-windows returns 200
  And the window appears in the list
  And GET /admin/halls/{id}/available-slots returns two 30-minute slots

Scenario: Delete a window
  When they delete the window
  Then DELETE /account/api/admin/hall-availability-windows/{id} returns 200
  And the window leaves the list and the hall has no free slots
```

### E2E-HAV-004 — A bound meeting removes its slot (GAP-2, D-716)

```gherkin
Feature: Free slots exclude bound meetings
Background:
  Given a Meeting hall with a 60-minute window @ 30-minute slots (two free slots)

Scenario: Binding a meeting drops its slot from the free set
  Given a speaker meeting request is accepted and bound to the hall's first slot
  And its status is AwaitingSpeaker
  When GET /admin/halls/{id}/available-slots is read
  Then only the second slot is returned (the bound slot is filtered out)
```

### E2E-HAV-006 — Only Meeting/General halls appear in the picker

```gherkin
Feature: The hall picker is limited to meeting-capable halls
Background:
  Given halls exist with purposes General, Meeting, Booth and Session

Scenario: Booth and Session halls are not offered
  When an Administrator opens /admin/hall-availability and expands the hall <select>
  Then only the General and Meeting halls are listed
  And the Booth hall and the Session hall are absent
  # grounded in HallAvailabilityPage.razor.cs — the filter is `HallPurpose.Meeting or General`
  # (HallPurpose: General=0, Booth=1, Session=2, Meeting=3)
```

### E2E-HAV-007 — RTL / Arabic render

```gherkin
Scenario: The page and add-window form mirror in Arabic
  Given the Administrator switches the CP language to العربية
  When they open /admin/hall-availability
  Then the page direction is RTL and the labels (hall, start, end, slot minutes, add) are Arabic
  And the window list and its delete action mirror to the right edge
  And no element overflows horizontally (scrollWidth == clientWidth)
```

### E2E-HAV-008 — the permission model (QA A36)

```gherkin
Feature: Hall availability carries its own hall-scoped permission
  # QA A36: the page and its four endpoints were gated by
  # SpeakerMeetingRequests.Manage/.View, so an operator holding only
  # DelegationMeetings.Manage — or the whole Halls.* set — could never create the
  # windows that EVERY meeting Approve modal depends on.

Scenario: The speaker desk alone no longer reaches the hall's windows
  Given an admin holds only SpeakerMeetingRequests.View + .Manage
  When they POST /admin/halls/{id}/availability-windows
  Then the API returns HTTP 403

Scenario: A hall-availability operator can define windows and read slots
  Given an admin holds only HallAvailability.View + .Manage (no meeting-desk code)
  When they POST /admin/halls/{id}/availability-windows
  Then the API returns HTTP 200
  And GET /admin/halls/{id}/available-slots returns HTTP 200

Scenario: View reads but never writes
  Given an admin holds only HallAvailability.View
  Then GET /admin/halls/{id}/availability-windows returns HTTP 200
  And POST /admin/halls/{id}/availability-windows returns HTTP 403

Scenario: Both meeting desks read the slots through the one shared code
  Given a custom role runs the delegation-meeting desk (DelegationMeetings.View + .Manage)
  Then it needs HallAvailability.View — and only that — to populate the Approve
      modal's hall-slot picker, instead of the unrelated SpeakerMeetingRequests.View
  # Administrator is unaffected: the wildcard "*" satisfies every code.
```

**Evidence:** `tests/SIMF.Api.Tests/HallAvailabilityTests.cs` →
`A36_hall_availability_is_gated_by_its_own_permission_not_the_speaker_desk`.

---

_Last reviewed:_ 2026-07-26 by Claude — QA A36 (hall-scoped `HallAvailability.View/Manage` gate; E2E-HAV-008). Prior: 2026-07-09 by Claude — D-720 (item 7 DoD close — E2E-HAV-006/007 authored). Earlier: D-716 (item 7, FDS-013 §15 GAP-2) taken-slot filter (E2E-HAV-004).
