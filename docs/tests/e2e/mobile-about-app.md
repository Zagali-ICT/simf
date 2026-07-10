# About the app — عن التطبيق (#207, `aboutApp`)

- **Route:** `/about-app` (`RouteNames.aboutApp`). **Public** (any account, incl. a guest).
- **Reached from:** the side-drawer end group (Contact us → **About** → Sign out). D-668.
- **Data:** the REAL installed app version (`package_info_plus`, D-736) + static
  date / organizer, the edition org profile (`GET /app/organization-profile`)
  for the support contact, and the server update policy
  (`GET /app/version-policy`) for the manual "Check for updates" action (D-736).

## Coverage

| Case | Scenario |
|------|----------|
| Golden path | Version / release date / organizer + links render |
| Support | Org-profile contact rows shown when set, hidden when absent |
| Public | A not-signed-in guest can open it (no sign-in redirect) |
| Manual check — up to date | Check for updates → "أنت على أحدث إصدار" one-button dialog (D-736) |
| Manual check — update available | Check for updates → "يتوفر تحديث" two-button dialog → store (D-736) |
| Manual check — offline | Airplane mode / server down → honest "حدث خطأ" error, never a fake result (D-736) |

### E2E-MOB207-001 — App info + links

```gherkin
Scenario: The About-the-app page shows version, date, organizer and links
  Given any account opens the side drawer
  When the user taps "عن التطبيق" (About the app) in the end group
  Then the About-the-app page opens
  And the version row shows the REAL installed app version, e.g. "1.0.0"
      (from package_info_plus, D-736 — no longer the literal 'SIMF 2026 · v1.0.0';
      "—" when the version is unresolved)
  And it shows the release date "2026-07-06"
  And it shows the organizer "القوات البحرية الملكية السعودية"
  And a "التحقق من التحديثات" (Check for updates) row sits below the app-info card
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

### E2E-MOB207-004 — Manual check: up to date (D-736)

```gherkin
Scenario: Checking for updates on the latest version confirms it explicitly
  Given the server policy's latestVersion for this platform is not above the
        installed version (or is unset)
  When the user taps the "التحقق من التحديثات" (Check for updates) row
  Then the row shows the busy state "جارٍ التحقق…" / "Checking…"
  And a one-button dialog opens titled "أنت على أحدث إصدار" / "You're up to date"
  And its body reads "الإصدار الحالي: 1.0.0" / "Current version: 1.0.0"
        (the real installed version)
  And the only button is "حسناً" / "OK"
  # The manual check calls GET /app/version-policy and IGNORES the splash's
  # 3-day soft-update snooze — it always reports honestly.
  # Covered by about_app_screen_test.dart ("up to date → an explicit
  # confirmation with the version", D-736).
```

### E2E-MOB207-005 — Manual check: update available → store (D-736)

```gherkin
Scenario: A newer server version offers the store
  Given appUpdate.android.latestVersion = "1.1.0" is above the installed "1.0.0"
  And a valid store URL is configured
  When the user taps "التحقق من التحديثات" (Check for updates)
  Then after "جارٍ التحقق…" / "Checking…" a two-button dialog opens titled
        "يتوفر تحديث" / "Update available"
  And its body reads "يتوفر إصدار جديد (1.1.0). ننصح بالتحديث للحصول على أحدث التحسينات." /
        "A new version (1.1.0) is available. We recommend updating for the latest improvements."
  And "لاحقاً" / "Later" closes the dialog and the page stays usable
  And "تحديث الآن" / "Update now" opens the configured store listing URL
  # A version snoozed on the splash is still offered here — the manual check
  # ignores the snooze.
  # Covered by about_app_screen_test.dart ("newer version on the server → the
  # update offer", D-736).
```

### E2E-MOB207-006 — Manual check: honest network error (D-736)

```gherkin
Scenario: Airplane mode / server down yields an honest error, never a fake result
  Given the device is in airplane mode (or the API is stopped)
  When the user taps "التحقق من التحديثات" (Check for updates)
  Then after the busy state a dialog opens titled "حدث خطأ" / "Something went wrong"
  And its body reads "تعذر الاتصال بالخادم. تحقق من الاتصال بالإنترنت وحاول مرة أخرى." /
        "Could not reach the server. Check your internet connection and try again."
  And it does NOT claim the app is up to date
  # Only the automatic launch check fails open (silently); the user-initiated
  # check must surface the truth.
  # Covered by about_app_screen_test.dart ("server unreachable → an honest
  # error, never a fake result", D-736).
```

---

_Last reviewed:_ `2026-07-10` by `SIMF Team` (D-736 — real installed version + the manual Check-for-updates row; appended E2E-MOB207-004..006).
