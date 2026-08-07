# #40-residual — two hardcoded "23–25 November 2026" strings survived the dynamic-dates conversion

Item ref: `#40-residual` (Track D-b, fix-all run 2026-07-30).
Files touched:
`src/Mobile/simf_app/lib/features/splash/splash_screen.dart` ·
`src/Mobile/simf_app/lib/app/localization/app_l10n.dart` (Track D-b block) ·
`src/Mobile/simf_app/test/features/splash/splash_screen_test.dart` ·
`src/Mobile/simf_app/test/golden/splash_golden_test.dart` ·
**deleted** `src/Website/SIMF.Web/wwwroot/speakers.html` ·
`tests/SIMF.Web.Tests/StaticLegacyPagesTests.cs` (new) ·
`docs/tests/e2e/mobile-splash.md` · `docs/pages/mobile/splash/README.md`.

## DECISIONS_LOG

### D-NEXT — #40-residual: the splash edition line reads the configured edition, and the legacy static speakers page is deleted

The dynamic-dates conversion left two literals behind. Both are closed here.

**(a) The app splash.** `AppL10n.splashEventLine` was the literal
`'النسخة الرابعة\n23–25 نوفمبر 2026 · الرياض'` / `'4th Edition\n23–25 Nov 2026
· Riyadh'`, rendered live at `splash_screen.dart` — so the **first screen of
every launch** hardcoded the 2026 edition. It now renders the CP-configured
`OrganizationProfile`: `eventStartDate`/`eventEndDate` through the shared
bilingual `formatEventDateRange` (the same formatter the Home hero, the Website
and the seeder use, so the four can never disagree), plus `locationText`.

- The profile hydrates **synchronously from the on-device cache**
  (`OrgProfileController.build`), so the dates are already present on the first
  splash frame of every launch after the first — no flash, no extra await. The
  splash controller's existing `warm()` refreshes it for next time.
- `splashEventLine` is **retained as the fallback only** (first-ever run, or an
  edition whose dates are not set), so the slot is never blank. A literal kept
  as an offline fallback is not the defect; a literal kept as the source is.
- The edition ordinal is split out as `AppL10n.splashEditionLine` and stays a
  bundled literal: the Organization Profile carries **no** edition-ordinal
  field, so there is nothing to bind it to. Recorded here so the next reader
  does not mistake it for an oversight.

**(b) The Website.** `wwwroot/speakers.html` was a 627-line hand-written static
page carrying `23–25 نوفمبر 2026`. `wwwroot` is served verbatim, so it stayed
publicly reachable at `/speakers.html` beside the Blazor `/speakers` route
(`Components/Pages/Speakers.razor`) that superseded it — a second, stale
speakers page nobody was maintaining. **Deleted, not edited**: patching the date
would have preserved the duplicate. Nothing in the project links to it (the only
references were its own nav `<a href="speakers.html">` and a comment in
`wwwroot/js/site-content.js`).

**Ratchet, not a one-off.** `tests/SIMF.Web.Tests/StaticLegacyPagesTests.cs`
fails the build if `wwwroot/speakers.html` returns, **or** if any authored static
page under `SIMF.Web/wwwroot` hardcodes an event-date range in either language.
Both assertions fail on the pre-fix tree.

**Follow-up reported, not actioned:** `wwwroot/js/site-content.js` is now
orphaned — `speakers.html` was its only consumer in the whole Website project
(its `hydrateSpeakers` targets the `spk-grid-1` / `spk-grid-2` ids that existed
only there). Deleting it is a separate change on another track's file.

**Tests:** `splash_screen_test.dart` — four new cases (configured dates in
English; in Arabic; dates without a location drop the ` · ` separator; no dates
falls back to the literal). The first asserts both the new value **and** the
absence of `23–25 Nov 2026`, so it fails on the pre-fix tree. The splash tests
and golden now override `orgProfileProvider` (the screen reads it), with the
golden pinned to "no profile" so the frame it was locked against is unchanged.

## PAGE-INDEX

Replace the `#1 splash` row (line ~231) with:

| #1 `splash` (`GET /app/version-policy` launch update check — D-736; `GET /app/organization-profile` for the edition line) | ✅ Real — Figma `159:573`; **clean-code frozen (D-641)**; render-lock golden (logo precached + `SplashLoading` pinned so no boot timers fire); **D-736** server version-policy gate (forced/soft update dialogs, 5 s fail-open cap, 3-day soft snooze); **#40-residual (2026-07-30):** the edition date/location line renders the CP-configured `OrganizationProfile` dates, with the bundled literal as the offline/first-run fallback only | Guest | [mobile/splash/](mobile/splash/README.md) | [e2e/mobile-splash.md](../tests/e2e/mobile-splash.md) |

The Website `/speakers` row is unchanged (the Blazor page is untouched); no row
existed for the static `/speakers.html`, which is why it survived unnoticed.

## E2E-README

Replace the `#1 splash` row (line ~229) with:

| #1 `splash` (`POST /app/auth/refresh` + `GET /app/users/me` + `GET /app/version-policy` — D-736; `GET /app/organization-profile` — #40-residual) | [`mobile-splash.md`](mobile-splash.md) | E2E-MOB001-001..020 |

**Roll-up:** this item adds **+3** Coverage-matrix rows
(`E2E-MOB001-018/019/020`).
`E2eCatalogueIntegrityTests.The_index_roll_up_matches_the_catalogue_it_describes`
asserts `**Total scenarios:** N` equals the real row count, so bump it by 3 when
merging (Track D-b contributes **+10** in total).
