# SIMF E2E test catalogue

| | |
|--|--|
| **Authority** | D-133 slice 7 (2026-05-28); full rebuild 2026-06-02 (D-245) |
| **Format** | Gherkin-style scenarios, runner-agnostic (Chrome DevTools MCP today, Playwright after adoption) |
| **Purpose** | After a batch of fixes, an agent reads **every** case here and drives each page — enters real data, performs each CRUD action, asserts each expected outcome — as a full regression pass that proves the system is **production-ready with no bugs**. |
| **Coverage gate** | every ✅ Real page in [`docs/pages/PAGE-INDEX.md`](../../pages/PAGE-INDEX.md) has a per-page catalogue file here with ≥1 P0 scenario |
| **Companion** | [`Test-Guide.md`](../../manuals/Test-Guide.md) (how to run + how to extend) |

## HARD RULE — build one-by-one as the system grows

A new CP page, app screen, Website page, or admin API **is not done** until its
per-page catalogue file exists here (authored, not a stub), is listed in the
index below, and is cross-linked from [`PAGE-INDEX.md`](../../pages/PAGE-INDEX.md).
This is the same discipline as the per-page permission rule — see the project
`CLAUDE.md` (§ "E2E test-case catalogue"). The catalogue is the executable
source of truth for "does every page still work".

## How this folder is organised

- **`_TEMPLATE.md`** — copy this when you add a new per-page catalogue file.
- **`{cp|web|mobile}-{slug}.md`** — one file per page with the per-page
  Coverage matrix + concrete, data-bearing Gherkin scenarios.
- **Gold-standard examples:** [`cp-admin-interests.md`](cp-admin-interests.md),
  [`cp-auth-flow.md`](cp-auth-flow.md), [`cp-admin-hall-arrivals.md`](cp-admin-hall-arrivals.md).
- **This README** maps every page to its file + scenario id range so the
  catalogue is browsable without opening 70+ files.

`E2E-XXX-NNN` ids are stable — if a scenario is removed, the id retires and is
not reused. Each page owns a unique 3–4 letter namespace.

## Per-page coverage index

### Control Panel — Overview

| Page | File | Scenarios |
|------|------|-----------|
| `/` (Dashboard) | [`cp-dashboard.md`](cp-dashboard.md) | E2E-DSH-001..013 |

### Control Panel — People & accounts

| Page | File | Scenarios |
|------|------|-----------|
| `/admin/admins` | [`cp-admin-admins.md`](cp-admin-admins.md) | E2E-USR-001..021 |
| `/admin/admins/pending` | [`cp-admin-admins-pending.md`](cp-admin-admins-pending.md) | E2E-APN-001..015 |
| `/admin/others` | [`cp-admin-others.md`](cp-admin-others.md) | E2E-OTH-001..021 |
| `/admin/others/pending` | [`cp-admin-others-pending.md`](cp-admin-others-pending.md) | E2E-OPN-001..016 |
| `/admin/visitors` | [`cp-admin-visitors.md`](cp-admin-visitors.md) | E2E-VIS-001..020 |
| `/admin/visitors/pending` | [`cp-admin-visitors-pending.md`](cp-admin-visitors-pending.md) | E2E-VPN-001..016 |
| `/admin/attendees` | [`cp-admin-attendees.md`](cp-admin-attendees.md) | E2E-ATT-001..016 |
| `/admin/print-bag` | [`cp-admin-print-bag.md`](cp-admin-print-bag.md) | E2E-PRT-001..011 |
| `/admin/interests` | [`cp-admin-interests.md`](cp-admin-interests.md) | E2E-INT-001..007 |
| `/admin/profile-types/visitor` | [`cp-admin-profile-types-visitor.md`](cp-admin-profile-types-visitor.md) | E2E-VPT-001..014 |
| `/admin/profile-types/other` | [`cp-admin-profile-types-other.md`](cp-admin-profile-types-other.md) | E2E-OPT-001..015 |
| `/admin/organisations` | [`cp-admin-organisations.md`](cp-admin-organisations.md) | E2E-ORG-001..015 |
| `/admin/countries` | [`cp-admin-countries.md`](cp-admin-countries.md) | E2E-CTY-001..017 |
| `/admin/delegations` | [`cp-admin-delegations.md`](cp-admin-delegations.md) | E2E-DLG-001..016 |
| `/admin/vips` | [`cp-admin-vips.md`](cp-admin-vips.md) | E2E-VIP-001..012 |
| `/admin/invitations` | [`cp-admin-invitations.md`](cp-admin-invitations.md) | E2E-INV-001..014 |
| `/admin/reset-2fa` | [`cp-admin-reset-2fa.md`](cp-admin-reset-2fa.md) | E2E-R2F-001..012 |
| `/admin/roles` | [`cp-admin-roles.md`](cp-admin-roles.md) | E2E-ROL-001..018 |
| `/admin/roles/{id}/permissions` | [`cp-admin-roles-permissions.md`](cp-admin-roles-permissions.md) | E2E-RPM-001..013 |

### Control Panel — Programme & sessions

| Page | File | Scenarios |
|------|------|-----------|
| `/admin/themes` | [`cp-admin-themes.md`](cp-admin-themes.md) | E2E-THM-001..018 |
| `/admin/halls` | [`cp-admin-halls.md`](cp-admin-halls.md) | E2E-HAL-001..016 |
| `/admin/halls/seat-layouts` | [`cp-admin-halls-seat-layouts.md`](cp-admin-halls-seat-layouts.md) | E2E-HSL-001..015 |
| `/admin/speakers` | [`cp-admin-speakers.md`](cp-admin-speakers.md) | E2E-SPK-001..016 |
| `/admin/speaker-presentations` | [`cp-admin-speaker-presentations.md`](cp-admin-speaker-presentations.md) | E2E-SPP-001..016 |
| `/admin/sessions` | [`cp-admin-sessions.md`](cp-admin-sessions.md) | E2E-SES-001..017 |
| `/admin/sessions/seat-plans` | [`cp-admin-sessions-seat-plans.md`](cp-admin-sessions-seat-plans.md) | E2E-SSP-001..014 |
| `/admin/session-categories` | [`cp-admin-session-categories.md`](cp-admin-session-categories.md) | E2E-SCT-001..016 |
| `/admin/session-moderators` | [`cp-admin-session-moderators.md`](cp-admin-session-moderators.md) | E2E-SMD-001..017 |
| `/admin/programme/timeline` | [`cp-admin-programme-timeline.md`](cp-admin-programme-timeline.md) | E2E-PTL-001..011 |
| `/admin/bookings` | [`cp-admin-bookings.md`](cp-admin-bookings.md) | E2E-BKG-001..012 |
| `/admin/meeting-requests` | [`cp-admin-meeting-requests.md`](cp-admin-meeting-requests.md) | E2E-MTR-001..013 |
| `/admin/meeting-tables` | [`cp-meeting-tables.md`](cp-meeting-tables.md) | E2E-MHT-001..012 |
| `/admin/business-meetings` | [`cp-business-meetings.md`](cp-business-meetings.md) | E2E-BMT-001..015 |

### Control Panel — Engagement, Q&A & attendance

| Page | File | Scenarios |
|------|------|-----------|
| `/admin/question-queue` | [`cp-admin-question-queue.md`](cp-admin-question-queue.md) | E2E-QQU-001..014 |
| `/sessions/{id}/moderate` | [`cp-session-moderate.md`](cp-session-moderate.md) | E2E-MOD-001..012 |
| `/admin/comments-moderation` | [`cp-admin-comments-moderation.md`](cp-admin-comments-moderation.md) | E2E-CMT-001..017 |
| `/admin/ratings` | [`cp-admin-ratings.md`](cp-admin-ratings.md) | E2E-RAT-001..012 |
| `/admin/session-summaries` | [`cp-admin-session-summaries.md`](cp-admin-session-summaries.md) | E2E-SUM-001..017 |
| `/admin/hall-arrivals` | [`cp-admin-hall-arrivals.md`](cp-admin-hall-arrivals.md) | E2E-HAR-001..014 |

### Control Panel — Exhibition

| Page | File | Scenarios |
|------|------|-----------|
| `/admin/companies` | [`cp-admin-companies.md`](cp-admin-companies.md) | E2E-CMP-001..016 |
| `/admin/booths` | [`cp-admin-booths.md`](cp-admin-booths.md) | E2E-BTH-001..017 |
| `/admin/sponsors` | [`cp-admin-sponsors.md`](cp-admin-sponsors.md) | E2E-SPN-001..017 |
| `/admin/media-partners` | [`cp-admin-media-partners.md`](cp-admin-media-partners.md) | E2E-MPR-001..013 |
| `/admin/venue-map` | [`cp-admin-venue-map.md`](cp-admin-venue-map.md) | E2E-VMP-001..018 |

### Control Panel — Content & media

| Page | File | Scenarios |
|------|------|-----------|
| `/admin/news` | [`cp-admin-news.md`](cp-admin-news.md) | E2E-NWS-001..015 |
| `/admin/media` | [`cp-admin-media.md`](cp-admin-media.md) | E2E-MED-001..016 |
| `/admin/archive` | [`cp-admin-archive.md`](cp-admin-archive.md) | E2E-ARC-001..013 |
| `/admin/banners` | [`cp-admin-banners.md`](cp-admin-banners.md) | E2E-BNR-001..016 |
| `/admin/content-blocks` | [`cp-admin-content-blocks.md`](cp-admin-content-blocks.md) | E2E-CNT-001..014 |

### Control Panel — AI

| Page | File | Scenarios |
|------|------|-----------|
| `/admin/ai/prompts` | [`cp-admin-ai-prompts.md`](cp-admin-ai-prompts.md) | E2E-AIP-001..016 |
| `/admin/ai/invocations` | [`cp-admin-ai-invocations.md`](cp-admin-ai-invocations.md) | E2E-AIV-001..012 |

### Control Panel — Access control & system

| Page | File | Scenarios |
|------|------|-----------|
| `/admin/gates` | [`cp-admin-gates.md`](cp-admin-gates.md) | E2E-GAT-001..015 |
| `/admin/gates/operator` | [`cp-admin-gates-operator.md`](cp-admin-gates-operator.md) | E2E-GOP-001..013 |
| `/admin/gates/dashboard` | [`cp-admin-gates-dashboard.md`](cp-admin-gates-dashboard.md) | E2E-GDS-001..011 |
| `/admin/configuration` | [`cp-admin-configuration.md`](cp-admin-configuration.md) | E2E-CFG-001..017 |
| `/admin/operations` | [`cp-admin-operations.md`](cp-admin-operations.md) | E2E-OPS-001..011 |
| `/admin/operation-log` | [`cp-admin-operation-log.md`](cp-admin-operation-log.md) | E2E-OPL-001..018 |
| `/admin/logs` | [`cp-admin-logs.md`](cp-admin-logs.md) | E2E-LOG-001..013 |
| `/admin/statistics` | [`cp-admin-statistics.md`](cp-admin-statistics.md) | E2E-STA-001..012 |

### Control Panel — Account & auth (not in main nav)

| Page(s) | File | Scenarios |
|---------|------|-----------|
| `/login` + `/login/totp` + `/login/recovery` + `/forgot-password` + `/auth/pending` + `/auth/rejected` | [`cp-auth-flow.md`](cp-auth-flow.md) | E2E-AUTH-001..010 |
| `/account/profile` | [`cp-account-profile.md`](cp-account-profile.md) | E2E-PRF-001..016 |
| `/account/notifications` | [`cp-account-notifications.md`](cp-account-notifications.md) | E2E-NTF-001..012 |
| `/account/totp-pairing` | [`cp-account-totp-pairing.md`](cp-account-totp-pairing.md) | E2E-TPP-001..010 |

### Website

| Page | File | Scenarios |
|------|------|-----------|
| `/account` | [`web-home.md`](web-home.md) | E2E-WHM-001..010 |
| `/programme` | [`web-programme.md`](web-programme.md) | E2E-WPG-001..010 |
| `/visit` | [`web-visit.md`](web-visit.md) | E2E-WVS-001..011 |
| `/login` | [`web-login.md`](web-login.md) | E2E-WLG-001..017 |
| `/login/verify` | [`web-otp-verify.md`](web-otp-verify.md) | E2E-WOT-001..010 |
| `/forgot-password` | [`web-forgot-password.md`](web-forgot-password.md) | E2E-WFP-001..014 |
| `/reset-password` | [`web-reset-password.md`](web-reset-password.md) | E2E-WRS-001..014 |
| `/account/profile` | [`web-account-profile.md`](web-account-profile.md) | E2E-WPR-001..016 |
| `/account/notifications` | [`web-account-notifications.md`](web-account-notifications.md) | E2E-WNT-001..012 |
| `/account/pending` | [`web-account-pending.md`](web-account-pending.md) | E2E-WPN-001..010 |
| `/account/rejected` | [`web-account-rejected.md`](web-account-rejected.md) | E2E-WRJ-001..010 |

### Mobile App (Flutter)

The Flutter app screens are catalogued under `mobile-*.md` as their backing App
API endpoints land (D-249). The per-screen design docs live under
[`docs/App/Page_NNN/`](../../App/).

| App screen | File | Scenarios |
|------------|------|-----------|
| #1 `splash` (`POST /app/auth/refresh` + `GET /app/users/me`) | [`mobile-splash.md`](mobile-splash.md) | E2E-MOB001-001..013 |
| #11 `registrationStatus` (`GET /app/users/me`) | [`mobile-registration-status.md`](mobile-registration-status.md) | E2E-MOB011-001..007 |
| #13 `home` (`GET /app/bootstrap`) | [`mobile-home.md`](mobile-home.md) | E2E-MOB013-001..007 |
| #14 `myArea` (`GET /app/account/dashboard` + `.ics` + `.vcf`) | [`mobile-my-area.md`](mobile-my-area.md) | E2E-MOB014-001..008 |
| #16 `agenda` (`GET /app/programme/sessions`) | [`mobile-agenda.md`](mobile-agenda.md) | E2E-MOB016-001..009 |

## How to add a new catalogue file

1. Copy `_TEMPLATE.md` → `{cp|web|mobile}-{slug}.md`.
2. Fill the front-matter (Page link, route, surface, runner, auth-setup via the
   `Get-Totp` helper — never a literal secret).
3. Fill the Coverage matrix — one row per scenario with a stable id
   `E2E-{NS}-{NNN}`, a fresh 3–4 letter namespace per page.
4. Author each scenario in tool-agnostic Gherkin with **concrete data** (real
   field names, realistic values, the exact bilingual toast/error text). Cover
   the golden CRUD path, every distinct function/action on the page, empty
   state, the auth gate, validation, conflict/duplicate, server-500, and RTL.
5. Add the row to the index above.
6. Cross-link from `docs/pages/PAGE-INDEX.md` (route → doc + test) and the
   per-page reference doc under `docs/pages/{cp|web}/{slug}.md`.

## Coverage status snapshot — 2026-06-02 (D-245)

- **Pages catalogued:** 74 (62 Control Panel + 11 Website + 1 mobile-pending).
- **Total scenarios:** ~1044 across all pages (7–21 per page); +61 added 2026-06-03
  (D-258) reconciling the SimfDataGrid conversion's per-column filter / sort
  affordances on the grid-converted CP pages.
- **Authored:** all pages (the D-133 "pending" stubs are now fully authored, and
  every event-module + P2–P5 page added since has its own file).
- **Execution:** the canonical run today is a Chrome DevTools MCP browser pass
  driven from these scenarios; many scenarios are also covered at the API layer
  by `tests/SIMF.Api.Tests/*` (each per-page file's "Implementation notes"
  cross-reference the covering xUnit cases).
