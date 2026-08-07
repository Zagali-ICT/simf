# E2E — Saved sessions (الجلسات المحفوظة) · `/saved-sessions`

- **Surface:** Mobile App (Flutter)
- **Route:** `/saved-sessions` (`RouteNames.savedSessions`, route 205) — Figma `1701:8928`
- **Audience:** Visitor / Exhibitor (approved); reached from the My-Area "جلسات محفوظة" counter
- **Backing reads:** `GET /app/programme/sessions` (cached programme) ∩ `GET /app/sessions/favourites` (approved-only); un-save `DELETE /app/sessions/{id}/favourite`
- **Auth setup:** an approved attendee token (via `Get-Totp` for the admin steps that seed data — never a literal secret)

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOBSAVED-001 | Golden path — open from the My-Area counter; only the favourited sessions show, count row = the saved count | happy | P1 | authored ✓ (widget — list + count) |
| E2E-MOBSAVED-002 | Category chips filter the saved list (الكل → a specific category) | happy | P1 | authored ✓ (widget — chip filter) |
| E2E-MOBSAVED-003 | Tapping a saved card opens the session detail (`/sessions/{id}`) | happy | P1 | authored ✓ (widget — card tap) |
| E2E-MOBSAVED-004 | Un-save via the bookmark removes the card (and decrements the count) | happy | P2 | authored (bookmark → `DELETE …/favourite`) |
| E2E-MOBSAVED-005 | Empty state when nothing is saved; count row = 0 | empty | P2 | authored ✓ (widget — empty) |
| E2E-MOBSAVED-006 | Auth-gate — a guest deep-linking `/saved-sessions` is redirected to sign-in | auth | P2 | authored (route `_routeRoles[205]`) |
| E2E-MOBSAVED-007 | RTL — the header, count row, chips and cards mirror right-to-left under Arabic | i18n | P2 | authored (RTL render) |
| E2E-MOBSAVED-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOBSAVED-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-MOBSAVED-001 — Golden path: saved list + count

```gherkin
Scenario: The saved-sessions screen lists only favourited sessions with the count
  Given an approved attendee has favourited 2 of the programme's sessions
  And the attendee opens My-Area
  When they tap the "جلسات محفوظة" counter (which reads "2")
  Then the "الجلسات المحفوظة" screen opens (Figma 1701:8928)
  And the gold count row reads "★ 2 جلسة محفوظة"
  And exactly the 2 favourited sessions are listed (non-favourited sessions are absent)
```

### E2E-MOBSAVED-002 — Category chips filter

```gherkin
Scenario: Selecting a category chip filters the saved list
  Given the saved list has sessions in the "بيئة" and "طاقة" categories
  And the "الكل" chip is selected (both are shown)
  When the user taps the "طاقة" chip
  Then only the "طاقة" sessions remain in the list
  And tapping "الكل" again restores the full saved list
```

### E2E-MOBSAVED-003 — Card → session detail

```gherkin
Scenario: Tapping a saved card opens its session detail
  Given the saved list shows a session
  When the user taps the card body
  Then the session detail screen opens for that session id (/sessions/{id}, route 17)
```

### E2E-MOBSAVED-004 — Un-save via the bookmark

```gherkin
Scenario: Un-saving a session removes it from the list
  Given the saved list shows a favourited session
  When the user taps the session's filled bookmark
  Then DELETE /app/sessions/{id}/favourite is sent
  And the card leaves the list
  And the count row decrements by one
  # A failed toggle reverts and shows "تعذر تحديث المفضلة".
```

### E2E-MOBSAVED-005 — Empty state

```gherkin
Scenario: No saved sessions shows the empty state
  Given the approved attendee has favourited nothing
  When they open /saved-sessions
  Then the count row reads "★ 0 جلسة محفوظة"
  And the empty message "لا توجد جلسات محفوظة بعد." is shown
```

### E2E-MOBSAVED-006 — Auth-gate

```gherkin
Scenario: A guest cannot open the saved-sessions deep link
  Given no attendee is signed in (guest)
  When a guest navigates to /saved-sessions
  Then the router redirects to sign-in (route 205 is attendee-gated, _routeRoles)
```

### E2E-MOBSAVED-007 — RTL

```gherkin
Scenario: The screen mirrors right-to-left in Arabic
  Given the app locale is Arabic
  When the saved-sessions screen is shown
  Then the back chevron, title, count row (★ + label at the start), chips and cards
       are laid out right-to-left
```

---

_Last reviewed:_ `2026-07-02` by `SIMF Team` — D-584: new الجلسات المحفوظة screen
(Figma 1701:8928) over the existing favourites + programme reads; the My-Area
"جلسات محفوظة" counter opens it and shows the saved (favourites) count.
