# E2E — My meetings (المقابلات) · `/my-meetings`

- **Surface:** Mobile App (Flutter)
- **Route:** `/my-meetings` (`RouteNames.myMeetings`, route 115) — Figma `1701:9406`
- **Audience:** Visitor / Exhibitor (approved); reached from the My-Area "مقابلات" counter
- **Backing read:** `GET /app/my-requests` (approved-only), filtered client-side to the two meeting kinds (speaker + delegation), cancelled excluded
- **Auth setup:** an approved attendee token (via `Get-Totp` for the admin steps that seed data — never a literal secret)

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOBMTG-001 | Golden path — open from the My-Area counter; only speaker/delegation meetings show, chip counts correct | happy | P1 | authored ✓ (widget — list + counts) |
| E2E-MOBMTG-002 | Status chips filter (الكل → مكتملة / قيد الانتظار / مرفوضة) | happy | P1 | authored ✓ (widget — chip filter) |
| E2E-MOBMTG-003 | Non-meeting kinds (document/badge/session-attendance) are excluded | happy | P1 | authored ✓ (widget — exclusion) |
| E2E-MOBMTG-004 | Cancelled meetings are excluded (they stay on الطلبات) | happy | P2 | authored ✓ (widget — exclusion) |
| E2E-MOBMTG-005 | An accepted meeting shows the "مؤكدة" badge | happy | P2 | authored ✓ (widget — badge) |
| E2E-MOBMTG-006 | Empty state when there are no live meetings | empty | P2 | authored ✓ (widget — empty) |
| E2E-MOBMTG-007 | Auth-gate + RTL — a guest deep-link redirects to sign-in; Arabic mirrors RTL | auth/i18n | P2 | authored ✓ (router gate + RTL render) |
| E2E-MOBMTG-008 | Empty status bucket hides its chip (Figma 3-chip parity, D-590) | happy | P1 | authored ✓ (widget — hidden-when-zero) |
| E2E-MOBMTG-009 | Card secondary line shows the speaker's rank; delegation / no-rank falls back to meeting-type (D-590) | happy | P1 | authored ✓ (widget + api — subtitle) |
| E2E-MOBMTG-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOBMTG-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-MOBMTG-001 — Golden path: meetings list + counts

```gherkin
Scenario: The my-meetings screen lists speaker + delegation meetings with counts
  Given an approved attendee has 1 accepted speaker meeting and 1 pending delegation meeting
  And the attendee opens My-Area
  When they tap the "مقابلات" counter
  Then the "المقابلات" screen opens (Figma 1701:9406)
  And the chips read "الكل (2)", "مكتملة (1)", "قيد الانتظار (1)"
  And no "مرفوضة" chip is shown (its bucket is empty — D-590)
  And the section header reads "جميع المقابلات (2)"
  And both meeting cards are listed with their counterpart name + secondary line
```

### E2E-MOBMTG-002 — Status chips filter

```gherkin
Scenario: Selecting a status chip filters the meetings
  Given the list has an accepted, a pending and a rejected meeting
  And the "الكل" chip is selected (all three shown)
  When the user taps "مكتملة (1)"
  Then only the accepted meeting remains (the header keeps the total "جميع المقابلات (3)")
  When the user taps "مرفوضة (1)"
  Then only the rejected meeting remains
```

### E2E-MOBMTG-003 — Non-meeting kinds excluded

```gherkin
Scenario: Document / badge / session-attendance requests are not shown here
  Given the الطلبات feed also contains a participation-document request and a seat booking
  When the user opens /my-meetings
  Then only the speaker + delegation meetings appear
  And the document request and the seat booking are absent (they remain on الطلبات)
```

### E2E-MOBMTG-004 — Cancelled meetings excluded

```gherkin
Scenario: A cancelled meeting does not appear on the meetings screen
  Given the feed contains a cancelled speaker meeting
  When the user opens /my-meetings
  Then the cancelled meeting is absent (it stays visible on الطلبات)
  And الكل counts only the accepted + pending + rejected meetings
```

### E2E-MOBMTG-005 — Confirmed badge

```gherkin
Scenario: An accepted meeting shows the confirmed badge
  Given the list shows an accepted meeting
  Then that card's status badge reads "مؤكدة"
  # pending → "قيد الانتظار", rejected → "مرفوضة"
```

### E2E-MOBMTG-006 — Empty state

```gherkin
Scenario: No live meetings shows the empty state
  Given the approved attendee has no speaker/delegation meetings
  When they open /my-meetings
  Then the empty message "لا توجد مقابلات بعد." is shown
```

### E2E-MOBMTG-007 — Auth-gate + RTL

```gherkin
Scenario: A guest cannot open the my-meetings deep link; Arabic mirrors RTL
  Given no attendee is signed in (guest)
  When a guest navigates to /my-meetings
  Then the router redirects to sign-in (route 115 is attendee-gated, _routeRoles)

Scenario: Arabic layout
  Given the app locale is Arabic and an approved attendee has a meeting
  When the my-meetings screen is shown
  Then the back chevron, title, chips and cards are laid out right-to-left
       (the gold initial avatar at the inline end, the "مؤكدة" badge on the row)
```

### E2E-MOBMTG-008 — Empty status bucket hides its chip (D-590)

```gherkin
Scenario: A status with no meetings shows no chip (Figma three-chip row)
  Given an approved attendee has 4 accepted and 2 pending meetings, and 0 rejected
  When they open /my-meetings
  Then exactly three chips show: "الكل (6)", "مكتملة (4)", "قيد الانتظار (2)"
  And no "مرفوضة" chip is rendered (empty bucket hidden — matches Figma 1701:9406)
  And the chip row does not overflow or truncate any label

Scenario: The rejected chip appears once a rejected meeting exists
  Given the attendee also has 1 rejected meeting
  When they open /my-meetings
  Then a "مرفوضة (1)" chip is now shown and filters to the rejected meeting
```

### E2E-MOBMTG-009 — Speaker rank as the card secondary line (D-590)

```gherkin
Scenario: A speaker meeting shows the speaker's rank under the name
  Given an approved attendee has an accepted meeting with a speaker whose Rank is "باحث بيئي"
  When they open /my-meetings
  Then that card's secondary line reads "باحث بيئي" (not the meeting-type text)
  # Backend: GET /app/my-requests returns Subtitle = the speaker's Rank (append-only field),
  # resolved via the existing speaker join — no schema change.

Scenario: No rank falls back to the meeting-type line
  Given the speaker has no Rank recorded
  Then the card's secondary line reads the meeting type "طلب لقاء مع متحدث"

Scenario: A delegation meeting always shows the meeting-type line
  Given the attendee has a delegation meeting (no speaker rank exists)
  Then that card's secondary line reads "طلب اجتماع وفد"
```

---

_Last reviewed:_ `2026-07-02` by `SIMF Team` — D-590: Figma-parity pass (hide-when-empty
status chips + speaker-rank card subtitle) on the D-587 المقابلات screen (Figma 1701:9406).
