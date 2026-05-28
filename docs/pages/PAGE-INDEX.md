# SIMF Page Index

The master cross-reference for every page in the SIMF system. Each row links to
its per-page reference document (under `docs/pages/{cp,web,mobile}/`), names the
route, the audience, the canonical pattern it implements, and the test catalogue
entry that proves it works.

> **Authority:** D-133 (2026-05-28). This index is the source of truth — every
> manual (User / Admin / Developer), every test plan, and every use-case spec
> cross-references rows here by route. When a new page ships, add a row here
> first, then everything else (per-page doc, manuals, tests, UCS) hangs off it.

Status legend:

- ✅ **Real** — dedicated `@page` route, real implementation
- 🚧 **Stub** — placeholder (`ModulePlaceholder` or single-line stub), nav SHOWS the route but the content is not built yet
- 🔒 **Auth-only** — reached via redirect or auth flow only, not in main nav

---

## Control Panel (CP) — http://localhost:5158

Navigation grouped per `CpNavigation.cs` (9 groups). 14 real pages + 22 stubs after D-132.

| Route | Module key | Status | Audience | Doc | Test |
|-------|------------|--------|----------|-----|------|
| **Overview** | | | | | |
| `/` | `Module.Dashboard` | ✅ Real | Any signed-in CP user | [cp/dashboard.md](cp/dashboard.md) | [e2e/cp-dashboard.md](../tests/e2e/cp-dashboard.md) |
| **People** | | | | | |
| `/m/registration-requests` | `Module.RegistrationRequests` | 🚧 Stub | Administrator | — | — |
| `/admin/attendees` | `Module.Attendees` | ✅ Real (D-134 Sprint A) | Administrator | [cp/admin-attendees.md](cp/admin-attendees.md) | [e2e/cp-admin-attendees.md](../tests/e2e/cp-admin-attendees.md) |
| `/admin/print-bag` | `Module.PrintBag` | ✅ Real | Administrator | [cp/admin-print-bag.md](cp/admin-print-bag.md) | [e2e/cp-admin-print-bag.md](../tests/e2e/cp-admin-print-bag.md) |
| `/admin/roles` | `Module.Roles` | ✅ Real (D-134 Sprint A) | Administrator | [cp/admin-roles.md](cp/admin-roles.md) | [e2e/cp-admin-roles.md](../tests/e2e/cp-admin-roles.md) |
| **Programme** | | | | | |
| `/admin/themes` | `Module.Themes` | ✅ Real (D-134 Sprint B) | Administrator | [cp/admin-themes.md](cp/admin-themes.md) | [e2e/cp-admin-themes.md](../tests/e2e/cp-admin-themes.md) |
| `/m/sessions` | `Module.Sessions` | 🚧 Stub | Administrator | — | — |
| `/admin/halls` | `Module.Halls` | ✅ Real (D-134 Sprint B) | Administrator | [cp/admin-halls.md](cp/admin-halls.md) | [e2e/cp-admin-halls.md](../tests/e2e/cp-admin-halls.md) |
| `/m/speakers` | `Module.Speakers` | 🚧 Stub | Administrator | — | — |
| `/m/bookings` | `Module.Bookings` | 🚧 Stub | Administrator | — | — |
| **Exhibition** | | | | | |
| `/m/exhibitors` | `Module.Exhibitors` | 🚧 Stub | Administrator | — | — |
| `/m/booths` | `Module.Booths` | 🚧 Stub | Administrator | — | — |
| `/m/sponsors` | `Module.Sponsors` | 🚧 Stub | Administrator | — | — |
| `/m/venue-map` | `Module.VenueMap` | 🚧 Stub | Administrator | — | — |
| **Engagement** | | | | | |
| `/m/live-sessions` | `Module.LiveSessions` | 🚧 Stub | Administrator | — | — |
| `/m/moderation` | `Module.Moderation` | 🚧 Stub | Administrator | — | — |
| **Knowledge & AI** | | | | | |
| `/m/faq` | `Module.Faq` | 🚧 Stub | Administrator | — | — |
| `/m/ai-settings` | `Module.AiSettings` | 🚧 Stub | Administrator | — | — |
| **Content** | | | | | |
| `/m/media` | `Module.Media` | 🚧 Stub | Administrator | — | — |
| `/m/news` | `Module.News` | 🚧 Stub | Administrator | — | — |
| `/m/previous-editions` | `Module.PreviousEditions` | 🚧 Stub | Administrator | — | — |
| **Communications** | | | | | |
| _(broadcast Notifications removed in D-132; operator inbox lives at `/account/notifications`)_ | | | | | |
| **System** | | | | | |
| `/admin/admins` | `Module.AdminAdmins` | ✅ Real | Administrator | [cp/admin-admins.md](cp/admin-admins.md) | [e2e/cp-admin-admins.md](../tests/e2e/cp-admin-admins.md) |
| `/admin/admins/pending` | `Module.AdminAdminsPending` | ✅ Real | Administrator | [cp/admin-admins-pending.md](cp/admin-admins-pending.md) | [e2e/cp-admin-admins-pending.md](../tests/e2e/cp-admin-admins-pending.md) |
| `/admin/others` | `Module.AdminOthers` | ✅ Real | Administrator | [cp/admin-others.md](cp/admin-others.md) | [e2e/cp-admin-others.md](../tests/e2e/cp-admin-others.md) |
| `/admin/others/pending` | `Module.AdminOthersPending` | ✅ Real | Administrator | [cp/admin-others-pending.md](cp/admin-others-pending.md) | [e2e/cp-admin-others-pending.md](../tests/e2e/cp-admin-others-pending.md) |
| `/admin/visitors` | `Module.AdminVisitors` | ✅ Real | Administrator | [cp/admin-visitors.md](cp/admin-visitors.md) | [e2e/cp-admin-visitors.md](../tests/e2e/cp-admin-visitors.md) |
| `/admin/visitors/pending` | `Module.AdminVisitorsPending` | ✅ Real | Administrator | [cp/admin-visitors-pending.md](cp/admin-visitors-pending.md) | [e2e/cp-admin-visitors-pending.md](../tests/e2e/cp-admin-visitors-pending.md) |
| `/admin/interests` | `Module.AdminInterests` | ✅ Real | Administrator | [cp/admin-interests.md](cp/admin-interests.md) | [e2e/cp-admin-interests.md](../tests/e2e/cp-admin-interests.md) |
| `/admin/profile-types/visitor` | `Module.AdminVisitorProfileTypes` | ✅ Real | Administrator | [cp/admin-profile-types-visitor.md](cp/admin-profile-types-visitor.md) | [e2e/cp-admin-profile-types-visitor.md](../tests/e2e/cp-admin-profile-types-visitor.md) |
| `/admin/profile-types/other` | `Module.AdminOtherProfileTypes` | ✅ Real | Administrator | [cp/admin-profile-types-other.md](cp/admin-profile-types-other.md) | [e2e/cp-admin-profile-types-other.md](../tests/e2e/cp-admin-profile-types-other.md) |
| `/admin/reset-2fa` | `Module.AdminResetTwoFactor` | ✅ Real | Administrator | [cp/admin-reset-2fa.md](cp/admin-reset-2fa.md) | [e2e/cp-admin-reset-2fa.md](../tests/e2e/cp-admin-reset-2fa.md) |
| `/admin/logs` | `Module.AdminLogs` | ✅ Real | Administrator | [cp/admin-logs.md](cp/admin-logs.md) | [e2e/cp-admin-logs.md](../tests/e2e/cp-admin-logs.md) |
| `/m/configuration` | `Module.Configuration` | 🚧 Stub | Administrator | — | — |
| `/admin/operation-log` | `Module.OperationLog` | ✅ Real (D-134 Sprint A) | Administrator | [cp/admin-operation-log.md](cp/admin-operation-log.md) | [e2e/cp-admin-operation-log.md](../tests/e2e/cp-admin-operation-log.md) |
| `/m/settings` | `Module.Settings` | 🚧 Stub | Administrator | — | — |

### CP auth + account pages (not in main nav)

| Route | Status | Audience | Doc | Test |
|-------|--------|----------|-----|------|
| `/login` | 🔒 Auth-only | Anyone | [cp/login.md](cp/login.md) | [e2e/cp-login.md](../tests/e2e/cp-login.md) |
| `/login/totp` | 🔒 Auth-only | Mid-sign-in | [cp/login-totp.md](cp/login-totp.md) | [e2e/cp-login-totp.md](../tests/e2e/cp-login-totp.md) |
| `/login/recovery` | 🔒 Auth-only | Mid-sign-in | [cp/login-recovery.md](cp/login-recovery.md) | — |
| `/forgot-password` | 🔒 Auth-only | Anyone | [cp/forgot-password.md](cp/forgot-password.md) | — |
| `/auth/pending` | 🔒 Auth-only | Pending account | [cp/auth-pending.md](cp/auth-pending.md) | — |
| `/auth/rejected` | 🔒 Auth-only | Rejected account | [cp/auth-rejected.md](cp/auth-rejected.md) | — |
| `/account/profile` | 🔒 Bell / user menu | Any signed-in | [cp/account-profile.md](cp/account-profile.md) | — |
| `/account/notifications` | 🔒 Bell | Any signed-in | [cp/account-notifications.md](cp/account-notifications.md) | [e2e/cp-account-notifications.md](../tests/e2e/cp-account-notifications.md) |
| `/account/totp-pairing` | 🔒 First-time login | Any signed-in | [cp/account-totp-pairing.md](cp/account-totp-pairing.md) | — |
| `/admin/admins/new` | 🔒 Deep-link fallback | Administrator | (covered by `/admin/admins` doc) | — |
| `/admin/others/new` | 🔒 Deep-link fallback | Administrator | (covered by `/admin/others` doc) | — |
| `/admin/visitors/new` | 🔒 Deep-link fallback | Administrator | (covered by `/admin/visitors` doc) | — |

### CP framework / error pages

| Route | Status | Notes |
|-------|--------|-------|
| `/Error` | ✅ Real | Framework error |
| `/not-found` | ✅ Real | 404 page |
| `/not-permitted` | ✅ Real | 403 page |

---

## Website (Web) — http://localhost:5115

No public nav per D-064 — every page is reached via direct URL or auth redirect.

| Route | Status | Audience | Doc | Test |
|-------|--------|----------|-----|------|
| `/account` | ✅ Real | Any signed-in | [web/home.md](web/home.md) | [e2e/web-home.md](../tests/e2e/web-home.md) |
| `/login` | 🔒 Auth-only | Anyone | [web/login.md](web/login.md) | [e2e/web-login.md](../tests/e2e/web-login.md) |
| `/forgot-password` | 🔒 Auth-only | Anyone | [web/forgot-password.md](web/forgot-password.md) | — |
| `/reset-password` | 🔒 Auth-only | After ForgotPassword | [web/reset-password.md](web/reset-password.md) | — |
| `/login/verify` | 🔒 Auth-only | Mid-sign-in | [web/otp-verify.md](web/otp-verify.md) | — |
| `/account/profile` | ✅ Real (interactive) | Any signed-in | [web/account-profile.md](web/account-profile.md) | [e2e/web-account-profile.md](../tests/e2e/web-account-profile.md) |
| `/account/notifications` | ✅ Real | Any signed-in (linked from UserProfile after D-132) | [web/account-notifications.md](web/account-notifications.md) | — |
| `/account/pending` | 🔒 State-banner | Pending account | [web/account-pending.md](web/account-pending.md) | — |
| `/account/rejected` | 🔒 State-banner | Rejected account | [web/account-rejected.md](web/account-rejected.md) | — |

---

## Mobile App (Flutter) — DEFERRED

The Flutter app build has not started — schedule TBD per the SIMF Program Plan.
Per-page docs land under `docs/pages/mobile/` when the app build begins. Empty
inventory rows are intentional placeholders so other manuals can already link
forward.

| Route | Status | Audience | Doc | Test |
|-------|--------|----------|-----|------|
| _to be filled when the Flutter App build starts_ | | | | |

---

## How to use this index

- **Reading the system:** start at a route → click the **Doc** column → see what
  the page does, who can use it, what API calls it makes, what tests cover it.
- **Adding a new page:**
  1. Add a row here (route, module key, status, audience, doc + test paths).
  2. Author the per-page doc from `docs/pages/_TEMPLATE.md`.
  3. Author the E2E test catalogue entry from `docs/tests/e2e/_TEMPLATE.md`
     (covers golden path + branches).
  4. Add the page to the relevant manual chapter (Admin / User / Developer).
  5. Add or update the use-case in `SIMF-UCS-001`.
  6. Commit all six artefacts in one changeset (page + index + doc + test + manual + UCS).

The reverse rule: **a page that exists in code but not on this index has not
shipped.** Quality gate at PR review time: search this file for the route; if
the row is missing, the PR is incomplete.
