# SIMF E2E test catalogue

| | |
|--|--|
| **Authority** | D-133 slice 7 (2026-05-28); full rebuild 2026-06-02 (D-245) |
| **Format** | Gherkin-style scenarios, runner-agnostic (Chrome DevTools MCP today, Playwright after adoption) |
| **Purpose** | After a batch of fixes, an agent reads **every** case here and drives each page — enters real data, performs each CRUD action, asserts each expected outcome — as a full regression pass that proves the system is **production-ready with no bugs**. |
| **Coverage gate** | every ✅ Real page in [`docs/pages/PAGE-INDEX.md`](../../pages/PAGE-INDEX.md) has a per-page catalogue file here with ≥1 P0 scenario |
| **Companion** | [`Test-Guide.md`](../../manuals/Test-Guide.md) (how to run + how to extend) |
| **Execution plan** | [`E2E-TEST-PLAN.md`](E2E-TEST-PLAN.md) — how / when / who / pass-fail for running this catalogue (subordinate to `SIMF-TST-001`) |

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
| `/admin/admins` | [`cp-admin-admins.md`](cp-admin-admins.md) | E2E-USR-001..024 |
| `/admin/admins/pending` | [`cp-admin-admins-pending.md`](cp-admin-admins-pending.md) | E2E-APN-001..015 |
| `/admin/others` | [`cp-admin-others.md`](cp-admin-others.md) | E2E-OTH-001..024 |
| `/admin/others/pending` | [`cp-admin-others-pending.md`](cp-admin-others-pending.md) | E2E-OPN-001..016 |
| `/admin/visitors` | [`cp-admin-visitors.md`](cp-admin-visitors.md) | E2E-VIS-001..025 |
| `/admin/visitors/pending` | [`cp-admin-visitors-pending.md`](cp-admin-visitors-pending.md) | E2E-VPN-001..025 |
| `/admin/visitors/vip` | [`cp-vip-registration.md`](cp-vip-registration.md) | E2E-VIPR-001..007 |
| `/admin/visitors/vip/export` | [`cp-vip-export.md`](cp-vip-export.md) | E2E-VIPX-001..008 |
| `/admin/delegates` | [`cp-admin-delegates.md`](cp-admin-delegates.md) | E2E-DLG-001..009 |
| `/admin/attendees` | [`cp-admin-attendees.md`](cp-admin-attendees.md) | E2E-ATT-001..016 |
| `/admin/print-bag` | [`cp-admin-print-bag.md`](cp-admin-print-bag.md) | E2E-PRT-001..011 |
| `/admin/interests` | [`cp-admin-interests.md`](cp-admin-interests.md) | E2E-INT-001..013 |
| `/admin/profile-types/visitor` | [`cp-admin-profile-types-visitor.md`](cp-admin-profile-types-visitor.md) | E2E-VPT-001..014 |
| `/admin/profile-types/other` | [`cp-admin-profile-types-other.md`](cp-admin-profile-types-other.md) | E2E-OPT-001..015 |
| `/admin/organisations` | [`cp-admin-organisations.md`](cp-admin-organisations.md) | E2E-ORG-001..019 |
| `/admin/regions` | [`cp-admin-regions.md`](cp-admin-regions.md) | E2E-REGION-001..016 |
| `/admin/contacts` | [`cp-admin-contacts.md`](cp-admin-contacts.md) | E2E-CON-001..020 |
| `/admin/countries` | [`cp-admin-countries.md`](cp-admin-countries.md) | E2E-CTY-001..020 |
| `/admin/vips` | [`cp-admin-vips.md`](cp-admin-vips.md) | E2E-VIP-001..013 |
| `/admin/invitations` | [`cp-admin-invitations.md`](cp-admin-invitations.md) | E2E-INV-001..018 |
| `/admin/reset-2fa` | [`cp-admin-reset-2fa.md`](cp-admin-reset-2fa.md) | E2E-R2F-001..012 |
| `/admin/roles` | [`cp-admin-roles.md`](cp-admin-roles.md) | E2E-ROL-001..024 |
| `/admin/roles/{id}/permissions` | [`cp-admin-roles-permissions.md`](cp-admin-roles-permissions.md) | E2E-RPM-001..013 |

### Control Panel — Programme & sessions

| Page | File | Scenarios |
|------|------|-----------|
| `/admin/themes` | [`cp-admin-themes.md`](cp-admin-themes.md) | E2E-THM-001..024 |
| `/admin/halls` | [`cp-admin-halls.md`](cp-admin-halls.md) | E2E-HAL-001..022 |
| `/admin/halls/seat-layouts` | [`cp-admin-halls-seat-layouts.md`](cp-admin-halls-seat-layouts.md) | E2E-HSL-001..015 |
| `/admin/speakers` | [`cp-admin-speakers.md`](cp-admin-speakers.md) | E2E-SPK-001..022 |
| `/admin/speaker-presentations` | [`cp-admin-speaker-presentations.md`](cp-admin-speaker-presentations.md) | E2E-SPP-001..017 |
| `/admin/sessions` | [`cp-admin-sessions.md`](cp-admin-sessions.md) | E2E-SES-001..030 |
| `/admin/sessions/seat-plans` | [`cp-admin-sessions-seat-plans.md`](cp-admin-sessions-seat-plans.md) | E2E-SSP-001..014 |
| `/admin/session-categories` | [`cp-admin-session-categories.md`](cp-admin-session-categories.md) | E2E-SCT-001..021 |
| `/admin/programme-days` | [`cp-admin-programme-days.md`](cp-admin-programme-days.md) | E2E-PGD-001..018 |
| `/admin/session-moderators` | [`cp-admin-session-moderators.md`](cp-admin-session-moderators.md) | E2E-SMD-001..018 |
| `/admin/programme/timeline` | [`cp-admin-programme-timeline.md`](cp-admin-programme-timeline.md) | E2E-PTL-001..011 |
| `/admin/bookings` | [`cp-admin-bookings.md`](cp-admin-bookings.md) | E2E-BKG-001..013 |
| `/admin/speaker-meeting-requests` | [`cp-admin-speaker-meeting-requests.md`](cp-admin-speaker-meeting-requests.md) | E2E-SMR-001..015 |
| `/admin/speaker-availability` | [`cp-admin-speaker-availability.md`](cp-admin-speaker-availability.md) | E2E-SAV-001..006 |
| `/admin/delegation-meetings` | [`cp-admin-delegation-meetings.md`](cp-admin-delegation-meetings.md) | E2E-DLM-001..006 |
| `/admin/document-requests` | [`cp-document-requests.md`](cp-document-requests.md) | E2E-CPDR-001..008 |
| `/admin/badge-requests` | [`cp-badge-requests.md`](cp-badge-requests.md) | E2E-CPBR-001..008 |
| `/admin/meeting-tables` | [`cp-meeting-tables.md`](cp-meeting-tables.md) | E2E-MHT-001..013 |
| `/admin/business-meetings` | [`cp-business-meetings.md`](cp-business-meetings.md) | E2E-BMT-001..016 |

### Control Panel — Engagement, Q&A & attendance

| Page | File | Scenarios |
|------|------|-----------|
| `/admin/question-queue` | [`cp-admin-question-queue.md`](cp-admin-question-queue.md) | E2E-QQU-001..015 |
| `/sessions/{id}/moderate` | [`cp-session-moderate.md`](cp-session-moderate.md) | E2E-MOD-001..012 |
| `/admin/ratings` | [`cp-admin-ratings.md`](cp-admin-ratings.md) | E2E-RAT-001..012 |
| `/admin/rating-config` | [`cp-admin-rating-config.md`](cp-admin-rating-config.md) | E2E-RCFG-001..015 |
| `/admin/session-summaries` | [`cp-admin-session-summaries.md`](cp-admin-session-summaries.md) | E2E-SUM-001..022 |
| `/admin/hall-arrivals` | [`cp-admin-hall-arrivals.md`](cp-admin-hall-arrivals.md) | E2E-HAR-001..014 |

### Control Panel — Exhibition

| Page | File | Scenarios |
|------|------|-----------|
| `/admin/companies` | [`cp-admin-companies.md`](cp-admin-companies.md) | E2E-CMP-001..016 |
| `/admin/exhibitors` | [`cp-admin-exhibitors.md`](cp-admin-exhibitors.md) | E2E-EXH-001..023 |
| `/admin/booths` | [`cp-admin-booths.md`](cp-admin-booths.md) | E2E-BTH-001..023 |
| `/admin/sponsors` | [`cp-admin-sponsors.md`](cp-admin-sponsors.md) | E2E-SPN-001..023 |
| `/admin/media-partners` | [`cp-admin-media-partners.md`](cp-admin-media-partners.md) | E2E-MPR-001..019 |
| `/admin/venue-map` | [`cp-admin-venue-map.md`](cp-admin-venue-map.md) | E2E-VMP-001..024 |

### Control Panel — Content & media

| Page | File | Scenarios |
|------|------|-----------|
| `/admin/news` | [`cp-admin-news.md`](cp-admin-news.md) | E2E-NWS-001..021 |
| `/admin/media` | [`cp-admin-media.md`](cp-admin-media.md) | E2E-MED-001..022 |
| `/admin/archive` | [`cp-admin-archive.md`](cp-admin-archive.md) | E2E-ARC-001..023 |
| `/admin/banners` | [`cp-admin-banners.md`](cp-admin-banners.md) | E2E-BNR-001..022 |
| `/admin/content-blocks` | [`cp-admin-content-blocks.md`](cp-admin-content-blocks.md) | E2E-CNT-001..020 |
| `/admin/media-library` | [`cp-admin-media-library.md`](cp-admin-media-library.md) | E2E-MLIB-001..010 |
| `/api/v1/files` (centralized file store, D-568) | [`cp-files.md`](cp-files.md) | E2E-FILE-001..012 |

### Control Panel — AI

| Page | File | Scenarios |
|------|------|-----------|
| `/admin/ai` | [`cp-admin-ai-dashboard.md`](cp-admin-ai-dashboard.md) | E2E-AID-001..007 |
| `/admin/ai/services` | [`cp-admin-ai-services.md`](cp-admin-ai-services.md) | E2E-AIS-001..014 |
| `/admin/ai/services/{feature}` | [`cp-admin-ai-service-detail.md`](cp-admin-ai-service-detail.md) | E2E-AISD-001..011 |
| `/admin/ai/prompts` | [`cp-admin-ai-prompts.md`](cp-admin-ai-prompts.md) | E2E-AIP-001..022 |
| `/admin/ai/invocations` | [`cp-admin-ai-invocations.md`](cp-admin-ai-invocations.md) | E2E-AIV-001..012 |

### Control Panel — Access control & system

| Page | File | Scenarios |
|------|------|-----------|
| `/admin/gates` | [`cp-admin-gates.md`](cp-admin-gates.md) | E2E-GAT-001..021 |
| `/admin/gates/operator` | [`cp-admin-gates-operator.md`](cp-admin-gates-operator.md) | E2E-GOP-001..013 |
| `/admin/gates/dashboard` | [`cp-admin-gates-dashboard.md`](cp-admin-gates-dashboard.md) | E2E-GDS-001..011 |
| `/admin/configuration` | [`cp-admin-configuration.md`](cp-admin-configuration.md) | E2E-CFG-001..023 |
| `/admin/site-settings` | [`cp-site-settings.md`](cp-site-settings.md) | E2E-CPSET-001..006 |
| `/admin/organization-profile` | [`cp-organization-profile.md`](cp-organization-profile.md) | E2E-ORGP-001..008 |
| `/admin/contact-inquiries` | [`cp-contact-inquiries.md`](cp-contact-inquiries.md) | E2E-CINQ-001..008 |
| `/admin/operations` | [`cp-admin-operations.md`](cp-admin-operations.md) | E2E-OPS-001..011 |
| `/admin/operation-log` | [`cp-admin-operation-log.md`](cp-admin-operation-log.md) | E2E-OPL-001..018 |
| `/admin/logs` | [`cp-admin-logs.md`](cp-admin-logs.md) | E2E-LOG-001..013 |
| `/admin/statistics` | [`cp-admin-statistics.md`](cp-admin-statistics.md) | E2E-STA-001..012 |
| `/admin/attendance` | [`cp-admin-attendance.md`](cp-admin-attendance.md) | E2E-ATT-001..014 |

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
| `/` (marketing landing) | [`web-landing.md`](web-landing.md) | E2E-WLD-001..008 |
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
| #2 `onboarding` (no API) | [`mobile-onboarding.md`](mobile-onboarding.md) | E2E-MOB002-001..008 |
| #3 `signIn` (+ verify-otp / forgot / reset) | [`mobile-sign-in.md`](mobile-sign-in.md) | E2E-MOB003-001..017 |
| ~~#4 `signUpType`~~ **REMOVED (D-332)** — invented; not in the mockup | [`mobile-sign-up-type.md`](mobile-sign-up-type.md) | _(retired — E2E-MOB004-* removed)_ |
| #5 `signUpForm` (`POST /app/auth/sign-up`) | [`mobile-sign-up-form.md`](mobile-sign-up-form.md) | E2E-MOB005-001..008 |
| #6 `emailOtp` (`verify-email` + `resend-code`) | [`mobile-email-otp.md`](mobile-email-otp.md) | E2E-MOB006-001..008 |
| #7 `signUpVisitor` — **profile data** (Visitor/Other + ProfileType + 3 lookups; Next→interests) | [`mobile-sign-up-visitor.md`](mobile-sign-up-visitor.md) | E2E-MOB007-001..022 |
| #7‑01 `signUpInterests` — **interests (1–10) + the single `POST /app/account/user-profile` save** | [`mobile-sign-up-interests.md`](mobile-sign-up-interests.md) | E2E-MOB7A-001..008 |
| #9 `terms` (`GET /app/content/terms`) | [`mobile-terms.md`](mobile-terms.md) | E2E-MOB009-001..008 |
| #10 `registrationSuccess` (no API — static confirmation) | [`mobile-registration-success.md`](mobile-registration-success.md) | E2E-MOB010-001..007 |
| #11 `registrationStatus` (`GET /app/users/me`) | [`mobile-registration-status.md`](mobile-registration-status.md) | E2E-MOB011-001..007 |
| #13 `home` (`GET /app/bootstrap`) | [`mobile-home.md`](mobile-home.md) | E2E-MOB013-001..007 |
| #14 `myArea` (`GET /app/account/dashboard` + `.ics` + `.vcf`) | [`mobile-my-area.md`](mobile-my-area.md) | E2E-MOB014-001..012 |
| #103 `identityVerification` (`POST /app/account/avatar`) | [`mobile-identity-verification.md`](mobile-identity-verification.md) | E2E-MOBIDV-001..004 |
| #104 `sessionModerate` (`GET/PUT /app/sessions/{id}/questions/moderate·push·hide`) | [`mobile-session-moderate.md`](mobile-session-moderate.md) | E2E-MOBMOD-001..004 |
| #105 `gateScanner` (`GET /app/gates/my-assignments` · `POST /app/gates/{id}/scans`) | [`mobile-gate-scan.md`](mobile-gate-scan.md) | E2E-MOBGATE-000..004 |
| #114 `staffRegisterVisitor` (`POST /app/staff/visitors/register-onsite` · `…/{id}/id-document` · `…/{id}/avatar`) | [`mobile-staff-register-visitor.md`](mobile-staff-register-visitor.md) | E2E-MOBSTAFFREG-001..004 |
| #15 `venueMap` (`GET /app/venue-map` + `/app/booths` + `/{id}`) | [`mobile-venue-map.md`](mobile-venue-map.md) | E2E-MOB015-001..008 |
| #16 `sessions` (`GET /app/programme/sessions`) | [`mobile-agenda.md`](mobile-agenda.md) | E2E-MOB016-001..013 |
| #17 `sessionDetail` (`GET /app/programme/sessions/{id}` + `…/sessions/{id}/seats`) | [`mobile-session-detail.md`](mobile-session-detail.md) | E2E-MOB017-001..011 |
| #18 `mySeat` (`GET /app/sessions/{id}/seats` + reserve/release) | [`mobile-my-seat.md`](mobile-my-seat.md) | E2E-MOB018-001..017 |
| `seatPicker` (`GET …/seats` + `POST …/seats/reserve` / `reserve-random`) — D-485 | [`mobile-seat-picker.md`](mobile-seat-picker.md) | E2E-MOBPICK-001..007 |
| `joinSessionHub` (`GET /app/programme/sessions`) — D-485 | [`mobile-join-hub.md`](mobile-join-hub.md) | E2E-MOBHUB-001..005 |
| `savedSessions` (`GET /app/sessions/favourites` ∩ programme) — #8, D-584 | [`mobile-saved-sessions.md`](mobile-saved-sessions.md) | E2E-MOBSAVED-001..007 |
| #19 `speakers` (`GET /app/speakers`) | [`mobile-speakers.md`](mobile-speakers.md) | E2E-MOB019-001..007 |
| #20 `speakerProfile` (`GET /app/speakers/{id}` + meeting-request) | [`mobile-speaker-profile.md`](mobile-speaker-profile.md) | E2E-MOB020-001..010 |
| #22 `booths` (`GET /app/booths` + `/{id}`) — #9: country name + أرشدني→map | [`mobile-booths.md`](mobile-booths.md) | E2E-MOB022-001..013 |
| `boothMap` (`/booths/:id/map` → venue map focused on the booth) — #9 | [`mobile-booths.md`](mobile-booths.md) | E2E-MOB022-013 |
| #23 `sponsors` (`GET /app/sponsors`) | [`mobile-sponsors.md`](mobile-sponsors.md) | E2E-MOB023-001..004 |
| #21 `delegations` (`GET /app/delegations`) — Wave 4, Figma `1426:10771` (restored from D-277) | [`mobile-delegations.md`](mobile-delegations.md) | E2E-DEL-001..009 |
| #220 `exhibitorDetail` (`GET /app/booths/{id}`) — Wave 3, Figma `1439:11881` | [`mobile-exhibitor-detail.md`](mobile-exhibitor-detail.md) | E2E-MOB220-001..007 |
| #221 `sponsorDetail` (`GET /app/sponsors/{id}`) — Wave 3, Figma `1439:11826` | [`mobile-sponsor-detail.md`](mobile-sponsor-detail.md) | E2E-MOB221-001..007 |
| `myVisitors` (`GET /app/exhibitor/my-visitors`) — D-426 exhibitor captured-visitor list | [`mobile-my-visitors.md`](mobile-my-visitors.md) | E2E-MOBMYVIS-001..006 |
| `scanVisitor` (`scanByBadge` — exhibitor lead-capture scan) — D-426 | [`mobile-scan-visitor.md`](mobile-scan-visitor.md) | E2E-MOBSCANVIS-001..005 |
| #24 `archive` (`GET /app/archive` + `/{id}`) | [`mobile-archive.md`](mobile-archive.md) | E2E-MOB024-001..005 |
| #29 `news` (`GET /app/news` + `/{id}`) | [`mobile-news.md`](mobile-news.md) | E2E-MOB029-001..005 |
| #30 `gallery` (`GET /app/media`) | [`mobile-gallery.md`](mobile-gallery.md) | E2E-MOB030-001..004 |
| #37 `aboutForum` (`GET /app/content/about`) | [`mobile-about.md`](mobile-about.md) | E2E-MOB037-001..003 |
| #40 `rate` (`GET/POST /app/feedback/form|submit`) | [`mobile-rate.md`](mobile-rate.md) | E2E-MOB040-001..011 |
| #31 `mediaPartners` (`GET /app/media-partners`) | [`mobile-media-partners.md`](mobile-media-partners.md) | E2E-MOB031-001..003 |
| #12 `guestMode` (no API) | [`mobile-guest-mode.md`](mobile-guest-mode.md) | E2E-MOB012-001..004 |
| #33 `notifications` (`POST /app/account/notifications/list` · `/{id}/read` · `/read-all`) | [`mobile-notifications.md`](mobile-notifications.md) | E2E-MOB033-001..006 |
| #35 `meetPeople` (`GET /app/account/recommendations/meet-like-you`) | [`mobile-meet-people.md`](mobile-meet-people.md) | E2E-MOB035-001..005 |
| #38 `accessibility` (no API) | [`mobile-accessibility.md`](mobile-accessibility.md) | E2E-MOB038-001..004 |
| #41 `more` (no API) | [`mobile-more.md`](mobile-more.md) | E2E-MOB041-001..003 |
| #34 `aiSummary` (`GET /app/programme/sessions/{id}/summary`) | [`mobile-ai-summary.md`](mobile-ai-summary.md) | E2E-MOB034-001..006 |
| #111 `sessionSummaryList` (cached programme + favourites/booked overlay) — Wave 2 pixel pass, Figma `1388:8392` | [`mobile-session-summaries.md`](mobile-session-summaries.md) | E2E-MOB111-001..008 |
| #26 `sendQuestion` (`POST /app/sessions/{id}/questions`) | [`mobile-send-question.md`](mobile-send-question.md) | E2E-MOB026-001..006 |
| #32 `badge` (`GET /app/account/dashboard`) | [`mobile-badge.md`](mobile-badge.md) | E2E-MOB032-001..004 |
| #25 `liveBroadcast` (`GET /app/programme/sessions/{id}`) | [`mobile-live.md`](mobile-live.md) | E2E-MOB025-001..007 |
| #36 `chatbot` (interim shell — no API) | [`mobile-chatbot.md`](mobile-chatbot.md) | E2E-MOB036-001..006 |
| #24-01 `archiveDetail` (`GET /app/archive/{id}`) | [`mobile-archive-detail.md`](mobile-archive-detail.md) | E2E-MOB024D-001..009 |
| `My Contacts` / `Share my contact` (`/app/account/share-token` + `/app/contacts/*`) | [`mobile-my-contacts.md`](mobile-my-contacts.md) | E2E-MMC-001..011 |
| `requests` (`GET /app/my-requests` + `POST …/document-requests` · `…/badge-requests` · `…/my-requests/cancel`) — Wave 5 (D-500), الطلبات, Figma `1408:9726`; supersedes `My meetings` | [`mobile-requests.md`](mobile-requests.md) | E2E-REQ-001..011 |
| `myMeetings` (`GET /app/my-requests`, filtered to meetings) — المقابلات (D-587), Figma `1701:9406`; speaker + delegation meetings over status chips; reached from the My-Area "مقابلات" counter | [`mobile-my-meetings.md`](mobile-my-meetings.md) | E2E-MOBMTG-001..007 |
| `Confirm Face ID` step-up (`POST /app/auth/device-keys/step-up` + gated register) — #7a biometric-enable | [`mobile-biometric-step-up.md`](mobile-biometric-step-up.md) | E2E-MBSU-001..011 |
| `Badge activation` (`POST /app/auth/badge/activation/start` · `…/complete`) — Part B passwordless badge | [`mobile-badge-activation.md`](mobile-badge-activation.md) | E2E-MOBBADGE-001..007 |
| #200 `forumGuide` (no API — static guide) — built from ComingSoon, Figma `1388:7493` | [`mobile-forum-guide.md`](mobile-forum-guide.md) | E2E-MOB200-001..005 |
| #201 `faq` (`GET /app/faq` — public) — built from ComingSoon, Figma `1388:7567` | [`mobile-faq.md`](mobile-faq.md) | E2E-MOB201-001..006 |
| #203 `contactUs` (`POST /app/contact-inquiry` + `GET /app/organization-profile`) — built from ComingSoon, Figma `1388:7711` | [`mobile-contact-us.md`](mobile-contact-us.md) | E2E-MOB203-001..007 |
| #202 `sessionPresentations` (`GET /app/presentations` + `/{id}/file`) — built from ComingSoon, Figma `1388:7621` | [`mobile-session-presentations.md`](mobile-session-presentations.md) | E2E-MOB202-001..006 |
| #113 `myAreaSessions` (`GET /app/account/sessions`) — Wave 2 my-sessions, titled "عروض الجلسات" (Figma `1388:9067`; retitled + reached from the More "عروض الجلسات" row, D-588) | [`mobile-my-sessions.md`](mobile-my-sessions.md) | E2E-MOB113-001..007 |

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

### Update — 2026-06-10 (D-356 Phase 5 — Uniform CRUD: Excel + Page⇄Popup toggle)

- **New page catalogued:** `/admin/exhibitors` (`cp-admin-exhibitors.md`,
  E2E-EXH-001..023) — its first authored E2E file.
- **+~130 scenarios** appended across 37 changed CP pages to cover the D-356
  generic Excel **export** (and **import** where a create/upsert path exists)
  and the D-353 **Page⇄Popup presentation toggle** + CrudShell `SimfConfirm`
  delete gate. Ranges in the index above reflect the new maxima. Every added id
  is contiguous and stable; no existing scenario was renumbered.
- **Reference docs:** 8 existing per-page docs updated + 7 authored for the full
  CrudShell conversions (Sponsors, Exhibitors, Speakers, Booths, Venue-map,
  Invitations, Sessions). Remaining `docs/pages/cp/*` "—" debt is unchanged
  (export-only/lookup pages) and stays tracked in `PAGE-INDEX.md`.

### Update — 2026-06-26 (D-500 Wave 5 — الطلبات unified requests feed)

- **New mobile catalogue:** `requests` (`mobile-requests.md`, E2E-REQ-001..011) —
  the Wave-5 unified الطلبات feed (Figma `1408:9726`): five request kinds
  (`SpeakerMeeting`, `DelegationMeeting` read-only, `SessionAttendance` from seat
  bookings, `ParticipationDocument` new, `BadgeUpdate` new), document/badge submit,
  status-chip filter, and self-cancel of own pending speaker/document/badge
  requests.
- **Two new CP desks catalogued:** `/admin/document-requests`
  (`cp-document-requests.md`, E2E-CPDR-001..008) and `/admin/badge-requests`
  (`cp-badge-requests.md`, E2E-CPBR-001..008) — both mirror
  `/admin/speaker-meeting-requests` (SimfDataGrid + Respond modal; Accept/Reject +
  note; Pending→Pending 400; list-omits-email PII; permission-gated). Accepting a
  badge request applies the requested title to the user's profile `JobTitle`.
- **Removed:** `mobile-my-meetings.md` (`E2E-MMM-*`, D-479) — the read-only
  My-meetings screen is superseded by the requests feed; its ids retire and are
  not reused.
