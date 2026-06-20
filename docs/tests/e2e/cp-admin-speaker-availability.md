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

## Scenarios

### E2E-SAV-001/002 — Define + remove a window

```gherkin
Feature: Speaker availability windows
Background:
  Given an Administrator has signed in to the Control Panel
  And they are on /admin/speaker-availability

Scenario: Add a window and see its free slots
  When they select a speaker and add a window 2030-01-01 10:00–11:00 UTC with 30-minute slots
  Then POST /account/api/admin/speakers/{id}/availability-windows returns 200
  And the window appears in the list
  And GET /app/speakers/{id}/available-slots returns two 30-minute slots

Scenario: Delete a window
  When they delete the window
  Then DELETE /account/api/admin/speaker-availability-windows/{id} returns 200
  And the window leaves the list and the speaker has no free slots
```

---

_Last reviewed:_ 2026-06-20 by SIMF Team — D-476 (#11) new speaker-availability admin page (Group G phase 1c).
