# E2E test catalogue — `Forum guide` (`forum-guide`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue —
> this screen has **no API** (static in-app copy). Built to KSA Figma frame
> **`1388:7493`** — a gold intro banner over five numbered step cards. Tested in
> `src/Mobile/simf_app/test/features/forum_guide/forum_guide_screen_test.dart`.
> Reached from the المزيد hub (معلومات الملتقى → دليل الملتقى); previously a
> ComingSoon placeholder (D-464).

| | |
|--|--|
| **Page** | app screen #200 `forumGuide` |
| **Route** | `/forum-guide` (no API) |
| **Surface** | Mobile (Flutter) |
| **Figma** | `1388:7493` |
| **Auth setup** | **None** — the screen is public (anonymous), like its المزيد siblings. |
| **Last reviewed** | 2026-06-26 |

## Layout

- **Header**: circled back chevron + centred title **دليل الملتقى**.
- **Intro banner** (gold `#C9A84C`): the welcome paragraph
  "مرحبًا بك في ملتقى SIMF 2026…" with a guide glyph at the inline end.
- **Five step cards** (navy-deep `#192B41`, beige hairline): each a gold index
  badge (1–5), a white title and a muted (`#C2B8A2`) description, with a
  decorative gold caret at the inline end.

> **Copy note:** the Figma leaves steps 3 & 5 with placeholder/duplicate text
> (step 5 repeats step 1). Reproduced verbatim pending the owner's final copy.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB200-001 | Title + intro banner + five numbered steps render | happy | P0 | authored ✓ (screen `renders the title, intro banner and five numbered steps`) |
| E2E-MOB200-002 | RTL — the index badge sits to the right of its step title | happy | P1 | authored ✓ (screen `Arabic: the step number sits to the right of its title`) |
| E2E-MOB200-003 | Opened from المزيد → دليل الملتقى (no longer ComingSoon) | happy | P1 | covered (router maps `forumGuide` → `ForumGuideScreen`; `more_screen` row) |
| E2E-MOB200-004 | Public — reachable signed-out (no auth gate) | auth | P1 | covered (route 200 not in `_authenticatedRoutes`) |
| E2E-MOB200-005 | Back chevron returns to المزيد | nav | P2 | covered (`ksaBackOrHome`) |

## Scenarios

```gherkin
Feature: Forum guide (static, Figma 1388:7493)

Scenario: The guide renders its banner and five steps
  When the user opens /forum-guide
  Then the header shows "دليل الملتقى"
  And the gold intro banner shows the "مرحبًا بك في ملتقى SIMF 2026" welcome text
  And five step cards are shown, numbered 1 through 5
  And step 2 reads "استكشاف الجلسات"

Scenario: Arabic layout places the number badge at the inline start
  Given the app locale is Arabic
  When the user opens /forum-guide
  Then each step's gold index badge sits to the right of its title

Scenario: Reached from the More hub
  Given the user is on /more
  When the user taps "دليل الملتقى" under معلومات الملتقى
  Then /forum-guide opens with the five-step guide (not the ComingSoon placeholder)

Scenario: Public access
  Given the user is signed out
  When the user opens /forum-guide
  Then the guide renders without a sign-in redirect

Scenario: Back returns to the hub
  Given the user opened /forum-guide from /more
  When the user taps the back chevron
  Then the app returns to /more
```

**Evidence:** screen test (2 cases — render + RTL); router mapping
(`router.dart` `forumGuide` → `ForumGuideScreen`); public (route 200 absent from
`_authenticatedRoutes`).

---

_Last reviewed:_ `2026-06-26` by `SIMF Team`.
