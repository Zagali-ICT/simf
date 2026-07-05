# E2E test catalogue — `My visitors` (`myVisitors`)

> **Authority:** SIMF E2E test catalogue (D-133). Mobile catalogue — the
> exhibitor "زواري / My Visitors" list (D-426). Reached from the exhibitor
> side-drawer entry and after a successful visitor-badge scan. Backend:
> `GET /app/exhibitor/my-visitors` (`ExhibitorRepository.listMyVisitors`),
> resolving each captured visitor's card live (no PII snapshot). App tests:
> `src/Mobile/simf_app/test/features/exhibitor/my_visitors_screen_test.dart`
> (widget, 3 cases) + the render-lock golden
> `test/golden/my_visitors_golden_test.dart` (`goldens/my_visitors.png`
> @375×812). Clean-code reviewed + frozen (D-642, 2026-07-04); per-page doc
> [`docs/pages/mobile/my-visitors/`](../../pages/mobile/my-visitors/README.md).

| | |
|--|--|
| **Page** | mobile exhibitor captured-visitor list (no Figma frame — functional page) |
| **Route** | app screen `/exhibitor/visitors` (`RouteNames.myVisitors`) |
| **Surface** | Mobile (Flutter); single-column list |
| **Role/gate** | Exhibitor (approved, non-visitor). A visitor-tier caller → server 403 → the forbidden surface |
| **Test runner** | Flutter widget/unit test + device manual |

> **Notes:** each row is the shared `ContactCard` with the visitor's card
> resolved on read; a visitor who has hidden their card renders the "no longer
> available" state instead of details. The list is pull-to-refresh (branded
> `SimfPullToRefresh`).

---

### E2E-MOBMYVIS-001 — Golden path (captured visitors list)

```gherkin
Scenario: An exhibitor sees the visitors they captured
  Given a signed-in approved exhibitor opens "زواري" from the drawer
  When GET /app/exhibitor/my-visitors returns their captured visitors (newest first)
  Then each visitor renders as a ContactCard (name, job title, organisation,
    country, email, mobile) with gold RTL field icons
  And the app bar title reads "زواري / My visitors"
```

### E2E-MOBMYVIS-002 — Empty state

```gherkin
Scenario: No visitors captured yet
  Given the exhibitor has captured no visitors
  When GET /app/exhibitor/my-visitors returns an empty list
  Then the message "No visitors yet. Scan a visitor badge to capture them here."
    ("لا زوار بعد…") shows
  And no ContactCard is rendered
```

### E2E-MOBMYVIS-003 — Auth gate (visitor-tier → 403 forbidden)

```gherkin
Scenario: A non-exhibitor account is refused
  Given a signed-in visitor-tier account reaches the screen
  When GET /app/exhibitor/my-visitors returns 403
  Then the forbidden message "Only exhibitor accounts can scan visitor badges."
    ("يمكن لحسابات العارضين فقط…") shows
  And no visitor list is rendered
```

### E2E-MOBMYVIS-004 — Server error + retry

```gherkin
Scenario: A transport / 5xx failure shows error + retry
  Given the list load fails (non-403 ApiFailure)
  Then the shared error surface shows the message + a "Retry" button
  When the exhibitor taps Retry
  Then GET /app/exhibitor/my-visitors is re-fetched
  And on success the captured-visitor list renders
```

### E2E-MOBMYVIS-005 — Pull-to-refresh

```gherkin
Scenario: Pull-to-refresh re-fetches the list
  Given the captured-visitor list is shown
  When the exhibitor pulls down (SimfPullToRefresh — gold accent spinner)
  Then GET /app/exhibitor/my-visitors is re-fetched
  And a newly-captured visitor (e.g. one scanned since) appears
```

### E2E-MOBMYVIS-006 — Unavailable subject + RTL

```gherkin
Scenario: A visitor who hid their card
  Given a captured visitor has set their card unavailable
  Then that row shows the "no longer available" state ("هذه الجهة لم تعد متاحة")
    instead of the contact details

Scenario: RTL
  Given the app language is Arabic
  Then the app bar, the ContactCards (gold avatar right, chevron/icons mirrored)
    and the empty/forbidden messages render right-to-left, no tofu
```

---

_Last reviewed:_ `2026-07-04` by `SIMF Team`.
