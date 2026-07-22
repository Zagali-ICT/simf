# E2E test catalogue — `اللقاءات الثنائية / Bilateral meetings` (`/meetings`)

> **Authority:** SIMF E2E test catalogue (D-133 / D-245). This page was split
> from the requests-history feed by **D-745** (owner 2026-07-11): the Home
> "اللقاءات الثنائية" tile now opens this **VIP-only** meetings page; the full
> requests log stays on [`mobile-requests.md`](mobile-requests.md) (طلباتي, My
> Area). Namespace `MOBMEET` is fresh — the retired `MOBMTG`/`MMM` ids are not
> reused (stable-id rule).

| | |
|--|--|
| **Page** | [`mobile/meetings.md`](../../pages/mobile/meetings.md) |
| **Route** | `/meetings` (route #116; `RouteNames.meetings`) |
| **Surface** | Mobile (Flutter) |
| **Test runner** | Flutter `flutter test` (widget + golden) + on-device manual E2E |
| **Auth setup** | An approved app account whose profile has **`allowsSpeakerMeeting`** and/or **`allowsDelegationMeeting`** = true (bi-meeting rework — replaces the VIP gate); a non-entitled approved account for the gate cases. TOTP via `Get-Totp` — never a literal secret. |
| **Last reviewed** | 2026-07-22 |

> **⚑ Bi-meeting rework update (2026-07-22).** The page is no longer "VIP-only": access is
> gated by the two per-user flags via `currentUserMeetingAccessProvider`
> (`MeetingAccess { speaker, delegation, any }`, from `allowsSpeakerMeeting` /
> `allowsDelegationMeeting`). The single "طلب جديد" button is **replaced by two flag-gated
> buttons** in `MeetingActionRow` — **"طلب مقابلة متحدث"** (`requestSpeakerMeeting`, shown when
> `access.speaker`, opens the speaker `MeetingRequestSheet`) and **"طلب اجتماع وفد"**
> (`requestDelegationMeeting`, shown when `access.delegation`, opens the delegation sheet —
> [`mobile-delegation-request.md`](mobile-delegation-request.md)) — with **السجل** (Log)
> below. The Home tile shows when `access.any`. The in-screen no-access text is now
> **"اللقاءات الثنائية متاحة للحسابات المصرَّح لها فقط"** / "Bilateral meetings are available to
> authorised accounts only" (`meetingAccessRequired`), replacing the old VIP-only copy. Where
> the scenarios below say "طلب جديد" / "VIP" / the old copy, read them per this banner. Widget
> tests: `meetings_screen_test.dart` (8/8) asserts the two buttons + the gate + the new copy.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOBMEET-001 | Golden: VIP sees approved + upcoming meetings, newest first | happy | P0 | _to author_ |
| E2E-MOBMEET-002 | Create a new meeting (طلب جديد → speaker picker → slot → send) | happy | P0 | _to author_ |
| E2E-MOBMEET-003 | Speaker picker shows photo + name + country flag; selection loads slots | happy | P0 | _to author_ |
| E2E-MOBMEET-004 | "السجل" opens the requests-history page (طلباتي) | nav | P1 | _to author_ |
| E2E-MOBMEET-005 | Only approved + upcoming meetings appear (pending/rejected/past excluded) | filter | P0 | _to author_ |
| E2E-MOBMEET-006 | Speaker-meeting card taps through to the speaker profile | nav | P2 | _to author_ |
| E2E-MOBMEET-007 | Empty state (no upcoming approved meetings) | happy | P1 | _to author_ |
| E2E-MOBMEET-008 | Flag gate — the Home tile is hidden when neither meeting flag is set (bi-meeting rework) | auth | P0 | authored ✓ (`meetings_screen_test.dart` / `home_screen_test.dart`, widget) |
| E2E-MOBMEET-009 | Flag gate — a non-entitled account on `/meetings` sees "…متاحة للحسابات المصرَّح لها فقط" (bi-meeting rework) | auth | P0 | authored ✓ (`meetings_screen_test.dart`, widget) |
| E2E-MOBMEET-010 | Server 500 on the feed → error state + retry | resilience | P2 | _to author_ |
| E2E-MOBMEET-011 | RTL render (Arabic) matches Figma 1408:9726 | i18n | P1 | _to author_ |
| E2E-MOBMEET-012 | Picker search filters speakers by name/rank; no-match hint (D-746) | filter | P1 | _to author_ |
| E2E-MOBMEET-013 | Two flag-gated buttons — "طلب مقابلة متحدث" (speaker flag) + "طلب اجتماع وفد" (delegation flag); a single-flag account sees only its button (bi-meeting rework) | happy | P0 | authored ✓ (`meetings_screen_test.dart`, widget) |

## Scenarios

### E2E-MOBMEET-001 — Golden path

```gherkin
Feature: Bilateral meetings list
  As an approved VIP attendee
  I want to see my confirmed, upcoming bilateral meetings
  So that I know who I am meeting and when

Background:
  Given I am signed in as an approved VIP account
  And I have an ACCEPTED speaker meeting with "د. محمد العمري" at a future slot
  And I have an ACCEPTED delegation meeting with "France" at a future slot

Scenario: The meetings page lists my approved upcoming meetings
  When I tap the Home "اللقاءات الثنائية" tile
  Then the page header reads "اللقاءات الثنائية"
  And I see a card for the speaker meeting with the speaker photo, name (gold),
      rank sub-line, the nationality flag badge, and the slot time with a clock
  And I see a card for the delegation meeting with the target-country flag badge
  And the cards are ordered newest-first
  And no status-filter chips are shown (the list is single-status by design)
```

**Evidence captured:**
- Golden: `test/golden/goldens/meetings_screen_1408-9726.png` (Arabic, 375×760).
- Console errors: 0 expected. Network failures: 0 (photos 404 → anchor placeholder only).

### E2E-MOBMEET-002 — Create a new meeting

```gherkin
Scenario: طلب مقابلة متحدث opens the speaker meeting sheet and submits
  Given I am on /meetings and my account has allowsSpeakerMeeting = true
  When I tap "طلب مقابلة متحدث" (Request a speaker meeting)
  Then the "طلب مقابلة" sheet opens with a speaker picker
  # The sibling "طلب اجتماع وفد" button opens the delegation sheet (mobile-delegation-request.md).
  When I select the speaker "د. محمد العمري"
  And I enter the subject "تعاون في الأبحاث البحرية"
  And I pick an available day and time slot
  And I tap "ارسال الطلب"
  Then the sheet closes with the "تم إرسال الطلب" confirmation
  And the meetings feed refreshes
```

### E2E-MOBMEET-003 — Rich speaker picker

```gherkin
Scenario: The picker shows identity + loads slots on selection
  Given the "طلب مقابلة" sheet is open with no fixed speaker
  Then each speaker row shows the photo tile, the name with the country flag
      inline, and the rank sub-line
  When I tap a speaker row
  Then that row shows the gold selected border + check
  And the subject field and the speaker's real availability day-cards appear
```

### E2E-MOBMEET-004 — "السجل" → history

```gherkin
Scenario: The log button opens the requests history
  Given I am on /meetings
  When I tap "السجل"
  Then the requests-history page opens with header "طلباتي"
  And it lists all my requests across every kind and status
```

### E2E-MOBMEET-005 — Approved + upcoming filter

```gherkin
Scenario: Only approved, not-past meetings are shown
  Given I have a PENDING speaker meeting
  And a REJECTED speaker meeting
  And an ACCEPTED speaker meeting whose slot is in the past
  And an ACCEPTED speaker meeting whose slot is in the future
  When I open /meetings
  Then I see only the future ACCEPTED meeting
  And the pending, rejected and past meetings do not appear here
  And all of them still appear on the "السجل" history page
```

### E2E-MOBMEET-006 — Card → speaker profile

```gherkin
Scenario: A speaker-meeting card opens the speaker profile
  Given a speaker-meeting card is visible (it shows a chevron)
  When I tap the card
  Then the speaker profile for that speaker opens
  And a delegation-meeting card (no speaker) shows no chevron and is not tappable
```

### E2E-MOBMEET-007 — Empty state

```gherkin
Scenario: No upcoming approved meetings
  Given I am a VIP with no approved upcoming meetings
  When I open /meetings
  Then the "طلب جديد / السجل" row still shows
  And below it the empty state "لا توجد مقابلات بعد." is shown
  And pull-to-refresh works
```

### E2E-MOBMEET-008 — Flag gate (tile hidden)

```gherkin
Scenario: A non-entitled account does not see the Home tile
  Given I am signed in as an approved account with allowsSpeakerMeeting = false AND allowsDelegationMeeting = false
  When I view the Home page
  Then the "اللقاءات الثنائية" tile is not shown (currentUserMeetingAccessProvider.any is false)
  And the "الأرشيف" tile fills the news-tiles row on its own
```

### E2E-MOBMEET-009 — Flag gate (in-screen)

```gherkin
Scenario: A non-entitled account who reaches /meetings sees the no-access state
  Given I am an approved account with neither meeting flag set
  When I navigate directly to /meetings
  Then the body shows
      "اللقاءات الثنائية متاحة للحسابات المصرَّح لها فقط" /
      "Bilateral meetings are available to authorised accounts only"
  And neither request button nor the list is shown
  # A load error also falls back to this no-access state (safe default).

Scenario: A single-flag account sees only its button
  Given allowsSpeakerMeeting = true AND allowsDelegationMeeting = false
  When I open /meetings
  Then only "طلب مقابلة متحدث" is shown (not "طلب اجتماع وفد")
  # And vice-versa for a delegation-only account.
```

### E2E-MOBMEET-010 — Server 500

```gherkin
Scenario: The feed fails to load
  Given GET /app/my-requests returns 500
  When I open /meetings as a VIP
  Then the error state "تعذّر تحميل طلباتك" with a retry is shown
  When I tap retry (or pull to refresh) and the feed recovers
  Then the meetings list renders
```

### E2E-MOBMEET-011 — RTL render

```gherkin
Scenario: Arabic RTL parity
  Given the app locale is Arabic
  When I open /meetings
  Then the header, the طلب جديد/السجل row, the cards (headline right, flag badge
      inline-end, speaker photo inline-start) and the time row match Figma
      1408:9726
  And there is no horizontal overflow
```

### E2E-MOBMEET-012 — Picker search (type-to-filter, D-746)

```gherkin
Scenario: Searching the speaker picker filters by name or rank
  Given the "طلب مقابلة" sheet is open with no fixed speaker
  And the picker lists multiple speakers
  When I type part of a speaker's name (or rank) into the picker search field
  Then only the speakers whose name or rank contains the query remain
  And the match is case-insensitive (Arabic and English names both filter)
  When I type a query that matches no speaker
  Then the "لا نتائج مطابقة" hint is shown in place of the list
  When I clear the query
  Then the full speaker list is restored

Scenario: The selected speaker is never hidden by the filter
  Given the "طلب مقابلة" sheet is open with no fixed speaker
  When I select a speaker
  And I then search for a DIFFERENT speaker
  Then the selected speaker stays visible alongside the matching one
  So the picker can never contradict the speaker the request is submitted to
```

---

_Last reviewed:_ `2026-07-22` by `Claude` — bi-meeting rework: the page is flag-gated (`currentUserMeetingAccessProvider`, not VIP); the single "طلب جديد" button becomes two flag-gated buttons ("طلب مقابلة متحدث" / "طلب اجتماع وفد"); the no-access copy is "اللقاءات الثنائية متاحة للحسابات المصرَّح لها فقط" (E2E-MOBMEET-008/009/013 now widget-backed by `meetings_screen_test.dart`). Prior: `2026-07-11` by `SIMF Team` (D-745 page split).
