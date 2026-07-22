# E2E test catalogue — `Delegations` (`delegations`)

> **Authority:** SIMF E2E test catalogue (D-133 / D-245). Mobile catalogue —
> data-driven from `GET /app/delegations` (`AppDelegations`, anonymous), the
> **Wave 4** delegations screen (الوفود, D-499, Figma node **`1426:10771`**).
> Mockup screen **#21** — **restored** (it was removed in D-277) and rebuilt as
> a public screen reached from a home tile + the direct `/delegations` route. It
> lists the **invited** countries' delegations: a stats strip (participating
> countries + total participants), a search box, and one card per invited
> country (flag, bilingual country name, head of delegation + initial avatar, a
> date range, and a member count). The head is resolved from the new
> `Country.HeadOfDelegationUserProfileId` pointer; the member count is the active
> delegate `UserProfile`s (`IsDelegate && IsActive`) with that `NationalityId`.

| | |
|--|--|
| **Page** | [`mobile/delegations/`](../../pages/mobile/delegations/README.md) (app screen #21 `delegations`) |
| **Route** | `/delegations` (`GET /app/delegations`) |
| **APIs** | `GET /api/v1/app/delegations` (`AllowAnonymous`) → `AppDelegations { countryCount, totalParticipants, items[] }`, each `AppDelegationItem { countryId, countryCode, countryName, countryNameArabic, headName?, headNameArabic?, headTitle?, arrivalDate?, departureDate?, memberCount }`. |
| **Surface** | Mobile (Flutter) |
| **Figma** | `1426:10771` |
| **Auth setup** | **None** — `GET /app/delegations` is anonymous (public delegations content); a guest can open the screen. For the tappable-card path (below) an approved account with `allowsDelegationMeeting = true`. |
| **Last reviewed** | 2026-07-22 |

> **⚑ Bi-meeting rework update (2026-07-22).** For an account whose profile has
> `allowsDelegationMeeting = true` (`currentUserMeetingAccessProvider.delegation`), each
> **delegation card becomes tappable** (a `GestureDetector`, opaque) and opens the delegation
> meeting request sheet for that country —
> [`mobile-delegation-request.md`](mobile-delegation-request.md). A guest / non-entitled user
> keeps the plain, non-tappable info cards (the screen stays fully public for reading). The
> read-only content, stats strip, search, and flag filter below are unchanged. Widget test:
> `delegations_screen_test.dart` pumps with `currentUserMeetingAccessProvider = MeetingAccess.none`.

## Layout

- **Header**: back chevron + centred title **الوفود** ("Delegations").
- **Stats strip**: two figures — the **participating-countries** count
  ("دولة مشاركة" / "Participating countries", from `countryCount`) and the
  **total-participants** count ("إجمالي المشاركين" / "Total participants", from
  `totalParticipants`) — over a faint gold grid with the invited-country
  **flags scattered** across it. Each scattered flag is a **tap target**:
  tapping one filters the list below to that country (the tapped flag is ringed
  in gold); tapping it again clears. Only the first 8 flags are placed (the
  strip is a fixed-size decorative map).
- **Search box**: a single filter input with the hint
  "ابحث عن دولة أو وفد..." / "Search for a country or delegation...", filtering
  the cards by country name (ar/en) and head name (ar/en). **Composes** with the
  flag filter (both narrow the list).
- **Active-filter chip**: shown below the search box only while a flag filter is
  active — the selected country's name + a close glyph ("عرض كل الدول" / "Show
  all countries" as its assistive label); tapping it clears the flag filter.
- **Country card** (one per invited country): the **flag** + the bilingual
  **country name**; the **head of delegation** row — label "رئيس الوفد" / "Head
  of delegation" over the head's name + job title, with an **initial avatar**
  (first letter, shown only when a head is set); the **date range**
  (`arrivalDate` → `departureDate`); and a **member count** (`memberCount`).
- **States**: spinner while loading; an inline retry surface with
  "تعذر تحميل الوفود." / "Could not load delegations." on a wire error; and the
  empty state "لا توجد وفود بعد." / "No delegations yet." when no invited
  countries are returned.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-DEL-001 | Golden path — a guest opens the screen; invited countries render (flag + bilingual name + head + dates + member count) and the two stats show `countryCount` / `totalParticipants` | happy | P0 | _to author_ |
| E2E-DEL-002 | Search filters by country name (ar/en) | happy | P0 | _to author_ |
| E2E-DEL-003 | Search filters by head-of-delegation name (ar/en) | happy | P1 | _to author_ |
| E2E-DEL-004 | Empty state — no invited countries → "لا توجد وفود بعد." / "No delegations yet." | empty | P1 | _to author_ |
| E2E-DEL-005 | Head omitted when `HeadOfDelegationUserProfileId` is unset (no head row, no initial avatar) | data | P1 | _to author_ |
| E2E-DEL-006 | `memberCount` excludes inactive / non-delegate profiles (only `IsDelegate && IsActive` with the country's `NationalityId`) | data | P0 | _to author_ |
| E2E-DEL-007 | Public / anonymous access — a guest (no token) can open the screen and load `GET /app/delegations` | auth | P0 | _to author_ |
| E2E-DEL-008 | Wire error → inline retry surface "تعذر تحميل الوفود." / "Could not load delegations." | resilience | P2 | _to author_ |
| E2E-DEL-009 | RTL render (Arabic) — header, stats, search hint, cards mirror right-to-left; head label "رئيس الوفد" | i18n | P1 | _to author_ |
| E2E-DEL-010 | Tap a stats-strip flag → list narrows to that country; tapped flag ringed; active-filter chip appears; tapping the chip (or the flag again) restores every country; flag + search filters compose | happy | P1 | _to author_ |
| E2E-DEL-011 | Tappable cards — an entitled account (`allowsDelegationMeeting`) taps a card → the delegation request sheet opens with that country fixed; a guest / non-entitled user's cards are plain (not tappable) (bi-meeting rework) | happy | P0 | authored ✓ (`delegations_screen_test.dart`, widget — guest = non-tappable) |

## Scenarios

### E2E-DEL-001 — Golden path: invited delegations render with the two stats

```gherkin
Feature: Delegations golden path (public, Figma 1426:10771, GET /app/delegations)

Background:
  Given the countries "Egypt" / "مصر" and "France" / "فرنسا" are marked invited (Country.IsInvited) and active
  And "Egypt" has a head of delegation set (a UserProfile via Country.HeadOfDelegationUserProfileId)
    with name "Ahmed Salah" / "أحمد صلاح" and job title "Ambassador" / "سفير"
  And "Egypt" has a delegation arrival date 2026-09-01 and a departure date 2026-09-05
  And "Egypt" has 4 active delegate profiles (IsDelegate && IsActive) with NationalityId = Egypt

Scenario: A guest opens the delegations screen
  Given no session (a guest)
  When the user opens /delegations
  Then the screen calls GET /api/v1/app/delegations
  And the stats strip shows the participating-countries count ("دولة مشاركة" / "Participating countries")
    equal to countryCount
  And the stats strip shows the total-participants count ("إجمالي المشاركين" / "Total participants")
    equal to totalParticipants
  And a card for "Egypt" shows the Egypt flag, the bilingual country name,
    the head row "رئيس الوفد" / "Head of delegation" with "Ahmed Salah" + "Ambassador" and an initial avatar,
    the date range 2026-09-01 → 2026-09-05,
    and the member count 4
```

### E2E-DEL-002 — Search filters by country name

```gherkin
Scenario: The search box narrows the cards by country name
  Given the screen lists "Egypt" / "مصر" and "France" / "فرنسا"
  When the user types "France" into the search box ("Search for a country or delegation...")
  Then only the "France" card is shown
  When the user clears the search and types "مصر" (Arabic)
  Then only the "Egypt" card is shown (the filter matches the Arabic name too)
  And an empty match shows the empty/no-results surface, never a blocking error
```

### E2E-DEL-003 — Search filters by head-of-delegation name

```gherkin
Scenario: The search box matches the head's name
  Given "Egypt" has a head "Ahmed Salah" / "أحمد صلاح"
  When the user types "Ahmed" into the search box
  Then the "Egypt" card is shown (matched on the head name)
  And typing "أحمد" (Arabic) likewise matches the "Egypt" card
```

### E2E-DEL-004 — Empty state

```gherkin
Scenario: No invited countries renders the empty state
  Given no country is marked invited (Country.IsInvited is false for every active country)
  When a guest opens /delegations
  Then GET /app/delegations returns countryCount 0, totalParticipants 0 and an empty items list
  And the screen shows "لا توجد وفود بعد." / "No delegations yet."
  And no error surface appears
```

### E2E-DEL-005 — Head of delegation omitted when none is set

```gherkin
Scenario: A country with no head shows no head row
  Given "France" is invited and active but Country.HeadOfDelegationUserProfileId is null
  When the user opens /delegations
  Then the "France" card renders the flag, the bilingual name, the date range and the member count
  And it shows no "رئيس الوفد" / "Head of delegation" row and no initial avatar
  And the API item carries headName / headNameArabic / headTitle as null
```

### E2E-DEL-006 — Member count excludes inactive / non-delegate profiles

```gherkin
Scenario: memberCount only counts active delegates of the country
  Given "Egypt" has 4 active delegate profiles (IsDelegate = true, IsActive = true, NationalityId = Egypt)
  And "Egypt" also has 1 inactive delegate profile (IsActive = false)
  And "Egypt" also has 1 ordinary visitor profile with NationalityId = Egypt (IsDelegate = false)
  When GET /app/delegations is called
  Then the "Egypt" item's memberCount is 4
  And neither the inactive delegate nor the non-delegate visitor is counted
```

### E2E-DEL-007 — Public / anonymous access

```gherkin
Scenario: A guest can open the delegations screen
  Given no session (no bearer token)
  When a client GETs /api/v1/app/delegations
  Then it returns 200 with the AppDelegations payload (no auth challenge)
  And the screen renders for the guest from a home tile and from the direct /delegations route
```

### E2E-DEL-008 — Wire error → inline retry

```gherkin
Scenario: A failed load shows the retry surface
  Given GET /app/delegations fails (network / 5xx)
  When the user opens /delegations
  Then the screen shows "تعذر تحميل الوفود." / "Could not load delegations." with a Retry affordance
  When the user taps Retry and the call succeeds
  Then the cards and the stats strip render
```

### E2E-DEL-009 — RTL render (Arabic)

```gherkin
Scenario: The screen mirrors under Arabic
  Given the app language is Arabic
  Then the header title reads "الوفود"
  And the stats strip, the search hint "ابحث عن دولة أو وفد..." and the cards mirror right-to-left
  And each card's head label reads "رئيس الوفد" and the country/head names render in Arabic
  When the user switches to English
  Then the labels flip to "Delegations" / "Head of delegation" / "Search for a country or delegation..."
```

### E2E-DEL-010 — Flag filter (stats-strip flags narrow the list)

```gherkin
Scenario: Tapping a country's flag isolates its delegation, and the chip clears it
  Given the screen lists "United States" / "الولايات المتحدة" and "Saudi Arabia" / "السعودية"
  And no flag filter is active (no active-filter chip is shown)
  When the user taps the United States flag in the stats strip
  Then only the "United States" card is shown
  And the tapped flag is ringed in gold
  And an active-filter chip shows "United States" with a close glyph
  When the user also types "Saud" into the search box
  Then no card is shown (the flag filter and the search compose) and the empty/no-results surface appears
  When the user clears the search
  Then the "United States" card returns (the flag filter still applies)
  When the user taps the active-filter chip (or the ringed flag again)
  Then the flag filter clears, the chip disappears, and both "United States" and "Saudi Arabia" are shown
```

**Evidence:** the screen renders the public `GET /app/delegations` feed; the API
projects only invited + active countries, resolves the head from
`Country.HeadOfDelegationUserProfileId`, and computes `memberCount` from active
delegate profiles (`IsDelegate && IsActive`) on the country's `NationalityId`.
The CP head-of-delegation picker is fed by `GET /admin/countries/{id}/delegates`.

### E2E-DEL-011 — Tappable cards → delegation request sheet (bi-meeting rework)

```gherkin
Scenario: An entitled account taps a delegation card to request a meeting
  Given I am signed in and my profile has allowsDelegationMeeting = true
  When I open /delegations and tap the "France" card
  Then the delegation meeting request sheet opens with France as the fixed target
      (mobile-delegation-request.md)

Scenario: A guest sees plain, non-tappable cards
  Given no session (a guest) or an account with allowsDelegationMeeting = false
  When I open /delegations
  Then the cards render the public read-only content and are NOT tappable
      (currentUserMeetingAccessProvider = MeetingAccess.none → no GestureDetector)
```

**Evidence:** `delegations_screen_test.dart` (widget) pumps the screen with
`currentUserMeetingAccessProvider` overridden to `MeetingAccess.none` (guest — cards stay
plain); the entitled tap-through is on-device / `_to author_`.

---

_Last reviewed:_ `2026-07-22` by `Claude` — bi-meeting rework: delegation cards are
**tappable** for an entitled account (`allowsDelegationMeeting`) → the delegation request
sheet (`E2E-DEL-011`); guests keep plain non-tappable cards. Prior: `2026-07-13` by `SIMF
Team` — the stats-strip **flag filter** (`E2E-DEL-010`). Originating: D-499 Wave 4
delegations screen (الوفود, Figma 1426:10771): public invited-country delegations with head
+ arrival/departure dates + member count; screen #21 restored from D-277.
