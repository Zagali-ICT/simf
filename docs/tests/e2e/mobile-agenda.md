# E2E test catalogue — `Sessions` (`sessions`, renamed from `agenda` — D-276)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7). Mobile
> catalogue — the public programme API is built (D-199) + enriched (D-252); the
> API implementation lives in `tests/SIMF.Api.Tests/ProgrammeSessionsTests.cs`.
> The **Flutter screen is built (D-299)** and was **rebuilt to KSA Wave-2
> frame 215:767 "الأجندة"** (D-378 batch) — same fetch-once + client-side
> filtering contract, new chrome: bordered search field, gold/navy view pills
> (أجندة الفعالية / الأجندة القادمة), the **white day strip** (selected day
> inverts to navy; weekend Fri/Sat weekday labels red; **re-tapping the
> selected day clears to all days** — the frame carries no all-days pill),
> and المواعيد rows with the two-line time chip + gold numbered title.
> Widget/model tests in
> `src/Mobile/simf_app/test/features/sessions/sessions_screen_test.dart`
> (chrome + numbered rows, search filter, Event-agenda-pill reveal, day-strip
> filter + re-tap clear, selected-cell inversion, row-tap → detail, empty,
> error→retry, RTL) and `…/session_models_test.dart` (tolerant int-enum
> decode, the real wire field names incl. the D-271 speaker country+photo,
> the client-side filter + day-strip helpers). The old mockup screen + test
> are parked in `_legacy_mockup/`.
>
> **Re-laid out to the LIVE frame 883:2308 (D-452, app phase):** the screen now
> reads the **day-grouped** programme (`GET /app/programme/days`) and renders
> the header **برنامج الملتقى**, the day strip (the programme days), the selected
> day's **own title + logo banner** ("تفاصيل اليوم" carries the day title — owner:
> not a static label), the **type tabs** (الكل / جلسات / ورش العمل — client-side
> filter on `SessionType`; the احداث tab was dropped to match the 3-tab frame,
> owner 2026-07-03 D-598 — event sessions show under الكل; the **day strip
> follows the ambient text direction** — Arabic (RTL) orders the days
> right→left with the earliest programme day on the **right** and the scroll
> leading from the right edge, English (LTR) stays left→right as the frame
> renders (owner 2026-07-22, overriding the earlier 883:2327 LTR pin)), then
> the **المواعيد** list with the
> first session **featured** (expanded with the day banner). Widget tests cover:
> header + day title + tabs + timeline rows; search filter; **type-tab filter**;
> day-strip day switch; selected-cell inversion; row→detail; empty; error→retry;
> RTL. Backed by D-452 phase 1 (`ProgrammeDay` + `Session.Type` + the days
> endpoint). The CP admin to author days/types/logos is the D-452 CP phase. The
> old view pills (أجندة الفعالية / الأجندة القادمة) were replaced by the type tabs.
>
> **Filename note:** this catalogue keeps its legacy `mobile-agenda.md` name; the
> screen/route is renamed **Sessions** (D-276). A rename to `mobile-sessions.md`
> is deferred (needs owner sign-off — it is referenced from PAGE-INDEX + the
> e2e README).
>
> **Public again (D-750, 2026-07-20, REVERSES D-576):** the Sessions/Agenda
> *screen* (#16) is **public** — a signed-out guest can open `/sessions` and browse
> the programme without signing in (restoring the D-199 public design). The public
> reads (`GET /app/programme/sessions[/{id}]`) stay `AllowAnonymous`, so the API
> scenarios below remain valid. The now-public access is covered by the updated
> E2E-MOB016-015. Separately, the **bottom-nav program tab label** changed from
> "الجلسات" (`sessionsTitle`) to "الأجندة" (`agendaTitle`) — the screen title
> "برنامج الملتقى" and the `sessionsTitle` string on other surfaces are unchanged
> (E2E-MOB016-018).

| | |
|--|--|
| **Page** | [`Page_016`](../../App/Page_016/README.md) (App page docs) |
| **Route** | `GET /api/v1/app/programme/sessions` (+`?day=`) · `GET /api/v1/app/programme/sessions/{id}` · app screen #16 `/sessions` |
| **Surface** | Mobile (Flutter) + App API |
| **Test runner** | xUnit + `WebApplicationFactory` (API) · Flutter widget/integration test (screen) |
| **Auth setup** | **Public screen (D-750, reverses D-576)** — the Sessions/Agenda screen is open to a signed-out guest (no redirect). The public reads stay `AllowAnonymous`. Admin token only to seed sessions/speakers/themes. **No literal secrets.** |
| **Last reviewed** | 2026-07-22 (owner — agenda day strip orders RTL in Arabic, overriding the 883:2327 LTR pin) |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB016-001 | Anonymous list returns the active programme (hall, title, theme) | happy | P0 | authored ✓ (`Public_list_returns_active_sessions_to_an_anonymous_caller`) |
| E2E-MOB016-002 | Each list item carries body + ordered speaker cards (cached payload drives detail) | happy | P0 | authored ✓ (`Public_list_item_carries_the_body_and_speaker_cards`) |
| E2E-MOB016-003 | List is ordered by start time | happy | P1 | authored ✓ (`Public_list_is_ordered_by_start_time`) |
| E2E-MOB016-004 | `?day=` restricts to one UTC calendar day (thin-client filter) | happy | P1 | authored ✓ (`Day_filter_restricts_to_that_utc_calendar_day`) |
| E2E-MOB016-005 | Malformed `?day=` → 400 | error | P1 | authored ✓ (`Malformed_day_filter_is_rejected_with_400`) |
| E2E-MOB016-006 | Soft-deleted session drops from the list | edge | P1 | authored ✓ (covered by the delete test) |
| E2E-MOB016-007 | Tap a row → Session detail (17) route opens; "main session" = category tag | happy | P1 | authored ✓ (screen — `tapping a row navigates to the session detail`) |
| E2E-MOB016-008 | Client filters (Upcoming/Forum pills, day strip, search) slice the cache — no refetch | happy | P0 | authored ✓ (screen — `Forum pill reveals…`, `search box filters…`) |
| E2E-MOB016-009 | RTL render + day-strip order/scroll — Arabic orders the days right→left (earliest programme day on the right; scroll leads from the right edge), English stays left→right | i18n | P1 | authored ✓ (screen RTL-primary; `sessions_screen_test` RTL day-order assertion — the earliest day is right of the latest; the English counterpart keeps it on the left) |
| E2E-MOB016-010 | Empty programme → empty state (not a blank list) | edge | P1 | authored ✓ (screen — `an empty programme shows the empty state`) |
| E2E-MOB016-011 | Fetch fails → error + Retry that re-runs the read | resilience | P0 | authored ✓ (screen — `a load failure shows the error + retry`) |
| E2E-MOB016-012 | `status` / speaker `role` decode tolerantly (int **or** name; unknown → default) | contract | P0 | authored ✓ (model — `SessionStatus.fromJson` / `SessionSpeakerRole.fromJson`) |
| E2E-MOB016-013 | List item binds the real wire names incl. the D-271 speaker country+photo | contract | P0 | authored ✓ (model — `SessionListItem.fromJson`) |
| E2E-MOB016-014 | **Full-width calendar (#4):** the day strip is a WHITE band over the FULL event date range (first→last programme day, empty in-between days filled), **ordered by the ambient text direction** (owner 2026-07-22, overriding the earlier 883:2327 LTR pin) — Arabic (RTL) orders the days right→left with the earliest programme day on the **right** and the scroll leading from the right edge, English (LTR) stays left→right; full-width (cells distributed, scroll fallback when long); a day **with** sessions = navy text ("active"), the **selected** day = navy pill/white text, an empty day = muted grey and **not** selectable; weekend labels red; the weekday labels stay English 3-letter (open owner question — whether Arabic weekday names are also wanted) | happy/visual | P1 | authored ✓ (screen — `ProgrammeDayStrip`/`_calendarRange`; selected-cell-navy + switch-day tests + `sessions_screen_test` RTL/LTR day-order assertions; Arabic golden `sessions_883-2308.png` re-locked) |
| E2E-MOB016-015 | **Public screen (D-750, reverses D-576):** a signed-out guest navigating to the `/sessions` screen sees the agenda (no redirect); My seat (18) stays attendee-gated | auth | P0 | authored ✓ (router-gate `D-750 — a signed-out guest hitting /sessions or a session detail is NOT redirected`; `routePathRequiresAuth('/sessions')` is FALSE) |
| E2E-MOB016-018 | **Program tab label (D-750):** the bottom-nav program/agenda tab reads "الأجندة" / "Agenda" (`agendaTitle`) — the screen header "برنامج الملتقى" and the `sessionsTitle` "الجلسات" on other surfaces (home tile, etc.) are unchanged | i18n/visual | P1 | authored ✓ (`sessions_screen_test` RTL: active bottom-nav label = "الأجندة"; `simf_page_shell_test`: the "Agenda" tab navigates to /sessions; goldens re-locked) |
| E2E-MOB016-016 | **Time-rail from→to connector (D-705):** every المواعيد row shows the vertical beige line between its start and end time — including a **collapsed/short row** (title only, no banner/description) where it previously collapsed to zero (Figma 1310:3243/3244) | visual | P1 | authored ✓ (golden `sessions_883-2308.png` — the connector renders on the featured AND the collapsed row) |
| E2E-MOB016-017 | **State chips (owner 2026-07-14):** each timeline row shows a state chip derived from its phase + flags — `مباشر الآن` (live, red), `الملخص متاح` (a published summary, gold outline), `مسجّل` (recorded, gold); an upcoming session shows no chip | visual | P1 | authored ✓ (`session_state_chip_test.dart` unit + golden `session_state_chips.png`; shared `SessionStateChipRow`) |
| E2E-MOB016-019 | **Header back chevron → Home (bug fix):** on the Agenda tab of the bottom-nav shell the header back chevron returns to the **Home** tab. Previously it was a dead no-op — an in-shell tab never leaves the shell's `/` location, so `backOrHome`'s `goNamed(home)` navigated to `/` while already there | nav | P1 | authored ✓ (`simf_page_shell_test` — `backOrHome on an in-shell tab (nothing to pop) switches the shell to the Home tab`) |

## Scenarios

### E2E-MOB016-001 — Anonymous full programme

```gherkin
Feature: Agenda (public programme)
  As a guest (not logged in)
  I want the whole programme in one cacheable call
  So that I can browse and filter it offline

Scenario: The programme is readable without a token
  Given an active session "Opening Keynote" in "Main Hall" exists
  When an anonymous client calls GET /api/v1/app/programme/sessions
  Then the response is 200
  And the item carries title, hallName (EN+AR) and the primary theme
```

**Evidence:** `ProgrammeSessionsTests.Public_list_returns_active_sessions_to_an_anonymous_caller` (green).

### E2E-MOB016-002 — Cached payload carries body + speakers (D-252)

```gherkin
Scenario: Each list row carries the body and the ordered speaker cards
  Given an active session with a description and one speaker
  When an anonymous client calls GET /api/v1/app/programme/sessions
  Then the item.description and item.descriptionArabic are present
  And item.speakers has one card with name + rank (title)
  And the app can render the session preview from this cached item without a second fetch
```

**Evidence:** `ProgrammeSessionsTests.Public_list_item_carries_the_body_and_speaker_cards` (green).

### E2E-MOB016-003 — Ordering

```gherkin
Scenario: Sessions are ordered ascending by start time
  Given two sessions on the same day at 09:00 and 14:00
  When the programme is fetched
  Then the 09:00 session appears before the 14:00 session
```

**Evidence:** `ProgrammeSessionsTests.Public_list_is_ordered_by_start_time` (green).

### E2E-MOB016-004 — Day filter (thin client)

```gherkin
Scenario: ?day= restricts to one UTC calendar day
  Given sessions on day D and day D+1
  When the client calls GET /api/v1/app/programme/sessions?day={D}
  Then only the day-D session is returned
```

> The **app** does not use this — it caches the whole programme and filters the
> day strip client-side (Page_016_Logic L-1). The server filter serves thin clients.

**Evidence:** `ProgrammeSessionsTests.Day_filter_restricts_to_that_utc_calendar_day` (green).

### E2E-MOB016-005 — Malformed day

```gherkin
Scenario: A bad day filter is rejected
  When the client calls GET /api/v1/app/programme/sessions?day=not-a-date
  Then the response is 400 (SessionInvalid)
```

**Evidence:** `ProgrammeSessionsTests.Malformed_day_filter_is_rejected_with_400` (green).

### E2E-MOB016-006 — Soft-delete drops from list

```gherkin
Scenario: A soft-deleted session disappears from the programme
  Given an active session that is then deleted by an admin
  When the programme is fetched
  Then the deleted session is not in the list
  And its detail returns 404
```

### E2E-MOB016-007 — Tap-through + type tag

```gherkin
Scenario: Tapping a row opens the detail from cache with the category tag
  Given the cached programme contains a session whose category is "Main Session" (جلسة رئيسية)
  When the user taps the row
  Then Session detail (17) renders immediately from the cached item
  And it shows the hall tag + the "جلسة رئيسية" / "Main Session" category tag
  And the live seat count refreshes in the background
```

**Evidence:** screen test `tapping a row navigates to the session detail`
(asserts the `/sessions/:id` route opens with the tapped id).

### E2E-MOB016-008 — Client-side filters, no refetch

```gherkin
Scenario: The pills, day strip and search slice the cache
  Given the whole programme is cached from one fetch
  When the user switches Upcoming/Forum, picks a day, or types in search
  Then the visible list updates instantly from the cache
  And no new GET /app/programme/sessions request is made
```

**Evidence:** screen tests `the Forum pill reveals past sessions hidden by Upcoming`
(Upcoming = `start >= now`, L-2) and `the search box filters the list`. The one
fetch is held in screen state; filters run over it via `filterSessions` (no repo
call) — `session_models_test.dart` covers the pure filter + `sessionDays`.

### E2E-MOB016-009 — RTL render

```gherkin
Scenario: The agenda renders right-to-left in Arabic
  Given the device locale is Arabic
  When the agenda renders
  Then the layout and day strip are right-to-left
  And the day cells order right-to-left — the earliest programme day sits on the right, the latest on the left
  And the day strip's horizontal scroll leads from the right edge
  And times render in the device timezone

Scenario: The agenda keeps left-to-right day order in English
  Given the device locale is English
  When the agenda renders
  Then the day cells order left-to-right — the earliest programme day sits on the left (unchanged from before the RTL override)
```

**Evidence:** the screen is RTL-primary (Arabic default); the day strip inherits
the ambient text direction (the earlier LTR pin was removed, owner 2026-07-22), so
its `Row` + horizontal `SingleChildScrollView` order the days and lead the scroll
right→left under RTL and left→right under LTR. Covered by `sessions_screen_test.dart`
— `the day strip orders days right-to-left in Arabic` (the earliest day is right of
the latest) and `the day strip keeps left-to-right order in English`. (The
active/next-session brass highlight is deferred to the SIMF-VID-001 visual pass —
interim UI.)

### E2E-MOB016-010 — Empty state

```gherkin
Scenario: An empty programme shows the empty state
  Given GET /app/programme/sessions returns an empty list
  When the sessions screen opens
  Then an empty-state message is shown, not a blank list
```

**Evidence:** screen test `an empty programme shows the empty state`.

### E2E-MOB016-011 — Error + retry

```gherkin
Scenario: A failed read offers a working retry
  Given GET /app/programme/sessions fails (transport / 5xx)
  When the sessions screen opens
  Then an error message + Retry are shown
  And tapping Retry re-runs the read
```

**Evidence:** screen test `a load failure shows the error + retry, which re-fetches`.

### E2E-MOB016-012 — Tolerant enum decode (int wire, D-299)

```gherkin
Scenario: status and speaker role decode whether int or name
  Given SessionStatus / SessionSpeakerRole serialise as ints today (no string converter)
  When the client decodes status=3 / role=1 (or "Published" / "Host")
  Then it resolves the known values, and an unknown value falls back to a safe default
```

**Evidence:** model tests `SessionStatus.fromJson` + `SessionSpeakerRole.fromJson`
(int, name, unknown→default).

### E2E-MOB016-013 — Wire-contract field names

```gherkin
Scenario: The list item binds the real wire names incl. the D-271 speaker cluster
  Given PublicSessionListItem ships title/hallName/start/status + speakers[]
  And each speaker carries countryId / countryNameEn / countryNameAr / photoRelativePath
  When the client decodes a session
  Then it binds those camelCase names and a missing speakers array decodes to []
```

> Reality note: an earlier draft of `Page_016_API.md` showed `status`/`role` as the
> enum **names** (`"Scheduled"`, `"Speaker"`); the shipped wire is an **int** (no
> `JsonStringEnumConverter` in `SIMF.Api`) → corrected with D-299; the client
> decodes tolerantly either way.

**Evidence:** model test `SessionListItem.fromJson binds the real wire field names…`.

### E2E-MOB016-015 — Public screen (D-750, reverses D-576)

```gherkin
Feature: Sessions screen — public access (D-750)
  As a signed-out guest
  I want to open the agenda and browse the programme without signing in
  So that the programme is browsable before login (owner, D-750; reverses D-576)

Scenario: A guest opening /sessions sees the agenda (no redirect)
  Given the app is signed out (a guest)
  When the app navigates to the /sessions screen (tab, deep link or cold start)
  Then the Sessions screen renders the programme (there is no redirect to sign-in)
  And tapping a row opens the session detail (also public, E2E-MOB017-025)
  # The public reads (GET /app/programme/sessions[/{id}]) stay AllowAnonymous.
  # My seat (18) stays attendee-gated, so a guest still cannot open the seat map.
```

**Evidence:** router-gate test `D-750 — a signed-out guest hitting /sessions or a
session detail is NOT redirected`; `routePathRequiresAuth('/sessions')` is FALSE.

### E2E-MOB016-018 — Program tab label (D-750)

```gherkin
Feature: Bottom-nav program tab label (D-750)
  As a user on any tab
  I want the program/agenda tab labelled "الأجندة"
  So that the tab reads "Agenda", not "Sessions" (owner, D-750)

Scenario: The active program tab reads الأجندة, not الجلسات
  Given the app is on the Sessions/Agenda tab (Arabic)
  Then the active bottom-nav program tab label reads "الأجندة" (agendaTitle)
  And the screen header still reads "برنامج الملتقى"
  And the "الجلسات" (sessionsTitle) string is unchanged on other surfaces
    (e.g. the home programme tile)
  When the program tab is tapped from another tab
  Then it navigates to /sessions (the route/path is unchanged)
```

**Evidence:** `sessions_screen_test.dart` RTL — the active bottom-nav label is
"الأجندة"; `simf_page_shell_test.dart` — the "Agenda" tab navigates to /sessions;
the 6 sessions-tab goldens re-locked to the new label.

### E2E-MOB016-019 — Header back chevron returns to Home

```gherkin
Scenario: The back chevron on the Agenda tab returns to Home
  Given the user is on the Agenda tab of the bottom-nav shell
  When they tap the back chevron in the header
  Then the shell switches to the Home tab
```

> The five bottom-nav tabs render inside `SimfAppShell`'s IndexedStack at the
> shell's `/` location, so `context.canPop()` is false and the old
> `goNamed(home)` was a no-op (navigating to `/` while already at `/`). The
> shared `backOrHome` now switches the shell tab to Home when it can't pop.

**Evidence:** `simf_page_shell_test.dart` — `backOrHome on an in-shell tab
(nothing to pop) switches the shell to the Home tab`.

---

_Last reviewed:_ `2026-07-24` by `SIMF Team` — **bug fix: the header back
chevron on the in-shell Agenda tab was a dead no-op; the shared `backOrHome` now
switches the shell to the Home tab when there is nothing to pop (E2E-MOB016-019).**
_Prior:_ `2026-07-22` by `Apexium` — **owner 2026-07-22: the agenda day
strip now orders the days by the ambient text direction — in Arabic (RTL) the
earliest programme day sits on the right and the scroll leads from the right edge,
overriding the earlier 883:2327 LTR pin; English stays left→right. Intro +
E2E-MOB016-009 + E2E-MOB016-014 updated; RTL/LTR day-order assertions added and the
Arabic golden `sessions_883-2308.png` re-locked. The weekday labels stay English
3-letter (open owner question — whether Arabic names are also wanted).** _Prior:_
`2026-07-20` by `Apexium` — **D-750: the Sessions/Agenda screen is
public again (reverses the D-576 login-gate) — a guest browses the programme without
signing in; the bottom-nav program tab label changed "الجلسات" → "الأجندة"
(`agendaTitle`), leaving `sessionsTitle` + the screen header unchanged.
E2E-MOB016-015 reworded, E2E-MOB016-018 added.** _Prior:_ `2026-07-14` by `SIMF Team`
— **owner state chips: each timeline row shows a `مباشر الآن` / `الملخص متاح` /
`مسجّل` chip from its `SessionPhase` + flags (E2E-MOB016-017).** _Prior:_ `2026-07-01`.
