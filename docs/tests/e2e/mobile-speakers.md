# E2E test catalogue — `Speakers list` (`speakers`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> public speakers list is an **already-built, anonymous** read (D-199): the list
> endpoint `GET /api/v1/app/speakers` returns the active speakers ordered for the
> mockup grid. The "login only" rule (D-269) applies **only** to the meeting
> request on the profile (20), not to this list. API implementation lives in
> [`tests/SIMF.Api.Tests/PublicSpeakersTests.cs`](../../../tests/SIMF.Api.Tests/PublicSpeakersTests.cs).

| | |
|--|--|
| **Page** | [`Page_019`](../../App/Page_019/README.md) (App page docs) |
| **Route** | `GET /api/v1/app/speakers` (list, **anonymous**) · app screen #19 `RouteNames.speakers` → `/speakers` (guest+) |
| **Surface** | Mobile (Flutter) + App API |
| **Test runner** | xUnit + `WebApplicationFactory` (API) · Flutter widget/integration test (screen) |
| **Auth setup** | **None** — the list is `AllowAnonymous` (guest+). An **Admin** token is used **only** to seed the speakers (and to soft-delete one). **No literal secrets** (admin TOTP via the `Get-Totp` helper). |
| **Last reviewed** | 2026-06-03 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB019-001 | Anonymous caller gets the active speakers as `ApiResult<PublicSpeakers>` (`items`) | happy | P0 | authored ✓ (`PublicSpeakersTests`) |
| E2E-MOB019-002 | Items are ordered by `displayOrder` then `name` | happy | P0 | authored ✓ (`PublicSpeakersTests`) |
| E2E-MOB019-003 | Each card carries avatar (`photoRelativePath`), rank (`rank`) and name (`name` / `nameArabic`) | happy | P1 | authored (screen) |
| E2E-MOB019-004 | Tapping a card → Speaker profile (20) with that `id` | happy | P0 | authored (screen) |
| E2E-MOB019-005 | A soft-deleted speaker drops out of the list | edge | P1 | authored ✓ (`PublicSpeakersTests`) |
| E2E-MOB019-006 | No speakers → empty `items` → list placeholder | edge | P2 | authored (screen) |
| E2E-MOB019-007 | RTL render; rank/name right-to-left, avatar leading | i18n | P1 | authored (screen) |

## Scenarios

### E2E-MOB019-001 — Anonymous list of active speakers

```gherkin
Feature: Speakers list (المتحدثون)
  As any visitor (guest or signed-in)
  I want to see every active speaker
  So that I can open a speaker's profile

Scenario: The list returns the active speakers without a token
  Given two active speakers have been seeded by an admin
  When an anonymous client calls GET /api/v1/app/speakers with no token
  Then the response is 200
  And the body is ApiResult<PublicSpeakers> with items holding both speakers
  And each item carries id, name, nameArabic, rank, countryNameEn, countryNameAr, photoRelativePath and displayOrder
```

**Evidence:** `PublicSpeakersTests` (the anonymous list returns the seeded active speakers) — `tests/SIMF.Api.Tests/PublicSpeakersTests.cs` (green).

### E2E-MOB019-002 — Ordering

```gherkin
Scenario: The speakers come back in display order
  Given speaker "Beta" has displayOrder 2 and speaker "Alpha" has displayOrder 1
  And two speakers share displayOrder 3 with names "Mike" and "Adam"
  When the list is fetched
  Then items are ordered by displayOrder ascending
  And ties are broken by name ascending (so "Adam" precedes "Mike")
```

**Evidence:** `PublicSpeakersTests` (items ordered by displayOrder then name) — `tests/SIMF.Api.Tests/PublicSpeakersTests.cs` (green).

### E2E-MOB019-003 — Card content

```gherkin
Scenario: Each speaker card shows the avatar, rank and name
  Given the list returned a speaker with rank "القبطان البحري", a name and a photoRelativePath
  When the grid renders the sp-card
  Then the card shows the avatar from photoRelativePath (the ⚓/★ avatar placeholder when absent)
  And the rank line shows "القبطان البحري"
  And the name shows below the rank with a "المزيد"/More affordance
```

### E2E-MOB019-004 — Open a profile

```gherkin
Scenario: Tapping a card opens that speaker's profile
  Given the list shows a speaker whose id is the seeded speakerId
  When the user taps the card (or "المزيد"/More)
  Then the Speaker profile (20) opens at /speakers/:speakerId with that speakerId
```

### E2E-MOB019-005 — Soft-deleted speaker drops out

```gherkin
Scenario: A soft-deleted speaker is not listed
  Given two active speakers are seeded
  And an admin soft-deletes one of them
  When the anonymous list is fetched again
  Then items holds only the remaining active speaker
  And the soft-deleted speaker does not appear
```

**Evidence:** `PublicSpeakersTests` (a soft-deleted speaker is excluded from the list) — `tests/SIMF.Api.Tests/PublicSpeakersTests.cs` (green).

### E2E-MOB019-006 — Empty list

```gherkin
Scenario: No speakers shows the empty-list placeholder
  Given no active speakers exist
  When the anonymous list is fetched
  Then items is empty
  And the screen shows the "no speakers yet" placeholder rather than an error
```

### E2E-MOB019-007 — RTL render

```gherkin
Scenario: The speakers grid renders right-to-left in Arabic
  Given the device locale is Arabic
  When the speakers list renders
  Then the sp-list and each sp-card are right-to-left
  And the avatar leads and the rank/name lines read right-to-left
  And the "المزيد"/More affordance sits at the natural RTL end of the card
```

---

_Last reviewed:_ `2026-06-03` by `SIMF Team`.
