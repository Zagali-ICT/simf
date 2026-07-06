# About the app — عن التطبيق (#207, `aboutApp`)

- **Route:** `/about-app` (`RouteNames.aboutApp`). **Public** (any account, incl. a guest).
- **Reached from:** the side-drawer end group (Contact us → **About** → Sign out). D-668.
- **Data:** static app version / date / organizer + the edition org profile
  (`GET /app/organization-profile`) for the support contact.

## Coverage

| Case | Scenario |
|------|----------|
| Golden path | Version / release date / organizer + links render |
| Support | Org-profile contact rows shown when set, hidden when absent |
| Public | A not-signed-in guest can open it (no sign-in redirect) |

### E2E-MOB207-001 — App info + links

```gherkin
Scenario: The About-the-app page shows version, date, organizer and links
  Given any account opens the side drawer
  When the user taps "عن التطبيق" (About the app) in the end group
  Then the About-the-app page opens
  And it shows the app version "SIMF 2026 · v1.0.0"
  And it shows the release date "2026-07-06"
  And it shows the organizer "القوات البحرية الملكية السعودية"
  And it shows "تواصل معنا" (Contact us) and "الشروط والأحكام" (Terms) link rows
  When the user taps "تواصل معنا"
  Then the Contact-us page opens
```

### E2E-MOB207-002 — Support contact from the org profile

```gherkin
Scenario: Support rows follow the edition org profile
  Given the edition org profile has a contact email set
  When the About-the-app page renders
  Then a "التواصل" (Contact) card shows the email
  And when no contact fields are set, the Contact card is not shown
```

### E2E-MOB207-003 — Public reachability

```gherkin
Scenario: A not-signed-in guest can open About-the-app
  Given a guest (not signed in) opens the side drawer
  Then "عن التطبيق" (About the app) and "تواصل معنا" (Contact us) are shown
  And tapping About-the-app opens the page with no sign-in redirect
  # Covered by about_app_screen_test.dart + more_drawer_test.dart (D-668).
```

---

_Last reviewed:_ `2026-07-06` by `SIMF Team` (D-668 — new About-the-app screen).
