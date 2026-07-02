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

## Scenarios

### E2E-MOBMTG-001 — Golden path: meetings list + counts

```gherkin
Scenario: The my-meetings screen lists speaker + delegation meetings with counts
  Given an approved attendee has 1 accepted speaker meeting and 1 pending delegation meeting
  And the attendee opens My-Area
  When they tap the "مقابلات" counter
  Then the "المقابلات" screen opens (Figma 1701:9406)
  And the chips read "الكل (2)", "مكتملة (1)", "قيد الانتظار (1)", "مرفوضة (0)"
  And the section header reads "جميع المقابلات (2)"
  And both meeting cards are listed with their counterpart name + meeting type
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

---

_Last reviewed:_ `2026-07-02` by `SIMF Team` — D-587: new المقابلات screen (Figma 1701:9406)
over the existing `GET /app/my-requests` feed; the My-Area "مقابلات" counter opens it.
