# E2E test catalogue — Hall availability (`/admin/hall-availability`)

| | |
|--|--|
| **Route** | `/admin/hall-availability` |
| **Surface** | Control Panel |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-07-09 (D-715 — item 7, FDS-013 §15 GAP-1) |

> **What this page does (grounded in `HallAvailabilityPage.razor`, D-715).**
> The team defines a **hall's meeting time** — availability windows (Start/End UTC +
> slot length) for the halls used to host business meetings. The meeting-review flow
> (GAP-2) chops each window into free slots and binds an accepted request to one. The
> page: a hall `<select>` (Meeting/General halls only), an **add-window** form
> (Start, End, slot minutes), and the selected hall's window list with delete. Gated
> by `SpeakerMeetingRequests.Manage` (page + nav + each action — the shared
> meeting-management permission). API: `GET`/`POST /admin/halls/{id}/availability-windows`,
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
| E2E-HAV-004 | A slot already bound to a meeting is not offered (GAP-2) | edge | P0 | _to author_ (lands with the accept-binds-slot flow, Slice B) |
| E2E-HAV-005 | Auth gate — admin lacking `SpeakerMeetingRequests.Manage` → `/not-permitted`; nav item hidden | auth | P0 | authored ✓ (gate verified by CpNavigationPermissionTests + PermissionEnforcementTests) |
| E2E-HAV-006 | Only Meeting/General halls appear in the picker | edge | P1 | _to author_ (page filters `HallPurpose.Meeting or General`) |
| E2E-HAV-007 | RTL / Arabic render — page + add form mirror | i18n | P1 | _to author_ |

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

---

_Last reviewed:_ 2026-07-09 by Claude — D-715 (item 7, FDS-013 §15 GAP-1) new hall-availability admin page.
