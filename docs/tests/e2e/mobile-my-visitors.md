# E2E test catalogue — `My Booth Visitors` (`myVisitors`)

> **Authority:** SIMF E2E test catalogue (D-133). Mobile catalogue — the
> exhibitor "زوار جناحي / My Booth Visitors" list (D-426). Reached from the
> exhibitor side-drawer entry, the exhibitor home's tools row, and after a
> successful visitor-badge scan. Backend:
> `GET /app/exhibitor/my-visitors` (`ExhibitorRepository.listMyVisitors`),
> resolving each captured visitor's card live (no PII snapshot). App tests:
> `src/Mobile/simf_app/test/features/exhibitor/my_visitors_screen_test.dart`
> (widget, 4 cases) + the render-lock golden
> `test/golden/my_visitors_golden_test.dart` (`goldens/my_visitors.png`
> @375×812). Clean-code reviewed + frozen (D-642, 2026-07-04); per-page doc
> [`docs/pages/mobile/my-visitors/`](../../pages/mobile/my-visitors/README.md).
> **BUG-025 (2026-07-26):** the screen was renamed زوار جناحي / My Booth
> Visitors and carries a `SimfPageNote` stating it is separate from
> [My Contacts](mobile-my-contacts.md) — the two features are deliberately NOT
> merged, pending an owner ruling.

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
> `SimfPullToRefresh`) and its first row is the BUG-025 explanatory note.

---

### E2E-MOBMYVIS-001 — Golden path (captured visitors list)

```gherkin
Scenario: An exhibitor sees the visitors they captured
  Given a signed-in approved exhibitor opens "زوار جناحي" from the drawer
  When GET /app/exhibitor/my-visitors returns their captured visitors (newest first)
  Then each visitor renders as a ContactCard (name, job title, organisation,
    country, email, mobile) with gold RTL field icons
  And the app bar title reads "زوار جناحي / My Booth Visitors"
```

### E2E-MOBMYVIS-002 — Empty state

```gherkin
Scenario: No visitors captured yet
  Given the exhibitor has captured no visitors
  When GET /app/exhibitor/my-visitors returns an empty list
  Then the message "No booth visitors yet. Scan a visitor badge at your booth to
    capture them here." ("لم تقم بمسح أي زائر بعد…") shows
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

### E2E-MOBMYVIS-007 — Bilingual job title (2026-07-20)

```gherkin
Scenario: A captured visitor's job title localizes per language
  Given a captured visitor whose profile has an Arabic job title (JobTitleArabic)
  And the app language is Arabic
  Then their ContactCard shows the Arabic job title
  When the app language is English
  Then the same card shows the English JobTitle
  # VisitorCard.jobTitleArabic + localizedJobTitle(isArabic): Arabic primary in
  # ar, English fallback, nothing shown when neither is set. Backend flow covered
  # by VisitorContactSharingTests; getter by contact_models_test.localizedJobTitle.
```

### E2E-MOBMYVIS-008 — Booth title + "not My Contacts" note (BUG-025, 2026-07-26)

```gherkin
Scenario: The exhibitor list names the booth and says what it is not
  Given a signed-in approved exhibitor opens the list with at least one capture
  Then the app bar title reads "زوار جناحي" (ar) / "My Booth Visitors" (en)
  And the first row of the list is a SimfPageNote reading
      "بطاقات الزوار التي مسحتها في جناحك. قائمة منفصلة عن «جهات اتصالي»." (ar) /
      "Badges you scanned at your booth. This list is separate from My Contacts."
  And the note scrolls with the list (it never steals viewport height)

Scenario: The two lists stay separate
  Given the same account also has saved cards in My Contacts (/contacts)
  Then a badge scanned at the booth appears ONLY in My Booth Visitors
  And a card saved by visitor-to-visitor sharing appears ONLY in My Contacts
  # Deliberate: merging the two features needs an owner ruling. See
  # docs/decisions/DECISIONS_LOG.md D-771.
```

**Evidence:** `my_visitors_screen_test` case "titles the booth and explains it is
not My Contacts"; render-lock golden `goldens/my_visitors.png` re-locked with the
new title + note.

---

_Last reviewed:_ 2026-07-26 by Claude — BUG-025: renamed زوار جناحي / My Booth
Visitors, added the `SimfPageNote` separating it from My Contacts, refreshed the
empty-state copy and re-locked the golden; E2E-MOBMYVIS-008. Earlier:
`2026-07-20` (bilingual job title, E2E-MOBMYVIS-007) and `2026-07-04` by
`SIMF Team`.
