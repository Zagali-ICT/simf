# About the app — عن التطبيق (#207, `aboutApp`)

- **Route:** `/about-app` (`RouteNames.aboutApp`). **Public** — any account, incl. a guest.
- **Added:** D-668 (2026-07-06). Reached from the side-drawer end group (Contact us → About → Sign out).

## Purpose

The app's own about page — **distinct** from #37 "عن الملتقى / About the forum" (event
info). Shows the app version, release date and organizer, the edition's support contact,
and quick links to Contact us + Terms.

## Structure

| File | Holds |
|------|-------|
| `about_app_screen.dart` | `AboutAppScreen` (ConsumerWidget) — composes `AboutDetailsCard` + `MoreSection`/`MoreRow` + the org profile |

- **App info** (`AboutDetailsCard`): Version (`moreVersion` = "SIMF 2026 · v1.0.0"), Release
  date (`aboutAppReleaseDate`, a **maintained constant** — there is no build-date source in
  the app), Organizer (RSNF).
- **Support** (`AboutDetailsCard`, shown only when set): email / phone / website from
  `orgProfileProvider` — the same source the forum-About page uses (no second hardcoded copy).
- **Links** (`MoreRow`): Contact us (→ #203) + Terms (→ #9).

## Level-F

Wired: Contact us + Terms links. Reads `orgProfileProvider` (support). No missing API
(reuses `GET /app/organization-profile`); version/date/organizer are static l10n.

## Tests

`test/features/about/about_app_screen_test.dart` + the drawer end-group tests in
`test/app/widgets/more_drawer_test.dart`. E2E: `docs/tests/e2e/mobile-about-app.md`.
