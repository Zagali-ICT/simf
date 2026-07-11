# About the app — عن التطبيق (#207, `aboutApp`)

- **Route:** `/about-app` (`RouteNames.aboutApp`). **Public** — any account, incl. a guest.
- **Added:** D-668 (2026-07-06). Reached from the side-drawer end group (Contact us → About → Sign out).

## Purpose

The app's own about page — **distinct** from #37 "عن الملتقى / About the forum" (event
info). Shows the REAL installed app version (D-736), release date and organizer, a manual
"Check for updates" action against the server version policy (D-736), the edition's
support contact, and quick links to Contact us + Terms.

## Structure

| File | Holds |
|------|-------|
| `about_app_screen.dart` | `AboutAppScreen` (ConsumerWidget) — composes `AboutDetailsCard` + `CheckForUpdatesRow` + `MoreSection`/`MoreRow` + the org profile |
| `widgets/check_for_updates_row.dart` | `CheckForUpdatesRow` (D-736) — the manual update check + its three outcome dialogs |

- **App info** (`AboutDetailsCard`): Version — the REAL installed version from
  `installedAppVersionProvider` (`package_info_plus`, D-736; e.g. `1.0.0`, "—" when
  unresolved; no longer the `moreVersion` literal "SIMF 2026 · v1.0.0"), Release
  date (`aboutAppReleaseDate`, a **maintained constant** — there is no build-date source in
  the app), Organizer (RSNF).
- **Check for updates** (`CheckForUpdatesRow`, below the app-info card — D-736): tapping
  "التحقق من التحديثات / Check for updates" shows the busy state "جارٍ التحقق… /
  Checking…" then fetches `GET /app/version-policy` and lands on one of three outcomes —
  (a) up to date: a one-button dialog "أنت على أحدث إصدار / You're up to date" with
  "الإصدار الحالي: {v} / Current version: {v}" (OK = حسناً); (b) update available: a
  two-button dialog "يتوفر تحديث / Update available" with "يتوفر إصدار جديد ({v})… /
  A new version ({v}) is available…" (لاحقاً/Later · تحديث الآن/Update now → opens the
  store URL); (c) honest network error: "حدث خطأ / Something went wrong" +
  "تعذر الاتصال بالخادم… / Could not reach the server…" — never a fake "up to date".
  The manual check **ignores** the splash's 3-day soft-update snooze.
- **Support** (`AboutDetailsCard`, shown only when set): email / phone / website from
  `orgProfileProvider` — the same source the forum-About page uses (no second hardcoded copy).
- **Links** (`MoreRow`): Contact us (→ #203) + Terms (→ #9).

## Level-F

Wired: Contact us + Terms links; the Check-for-updates row (busy → 3 outcomes → store
launch). Reads `orgProfileProvider` (support), `installedAppVersionProvider` (version)
and `GET /app/version-policy` (manual check, D-736 — the same anonymous endpoint the
splash launch gate uses); date/organizer are static l10n.

> **Configuring / operating the app-update gate** (the six `appUpdate.*` policy keys,
> semver rules, and the release runbook): see
> [`docs/manuals/SIMF-App-Update-Dev-Guide.md`](../../../manuals/SIMF-App-Update-Dev-Guide.md).

## Tests

`test/features/about/about_app_screen_test.dart` (incl. the D-736 manual-update-check
group: up-to-date confirmation / update offer / honest offline error) + the drawer
end-group tests in `test/app/widgets/more_drawer_test.dart`. E2E:
`docs/tests/e2e/mobile-about-app.md`.
