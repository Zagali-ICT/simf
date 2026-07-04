# SIMF Page Index

The master cross-reference for every page in the SIMF system. Each row links to
its per-page reference document (under `docs/pages/{cp,web,mobile}/`, where one
exists), names the route, the audience, and the E2E test catalogue entry that
proves it works.

> **Authority:** D-133 (2026-05-28); refreshed 2026-06-02 (D-245) to reflect the
> event-module + P2–P5 pages that shipped since. This index is the source of
> truth — every manual, test plan, and use-case spec cross-references rows here
> by route. When a new page ships, add a row here first, then the per-page doc,
> the E2E catalogue file, the manuals, and the UCS.

Status legend:

- ✅ **Real** — dedicated `@page` route, real implementation
- 🚧 **Stub** — placeholder (`ModulePlaceholder` / generic `/m/{Module}`), not built
- 🔒 **Auth-only** — reached via redirect or auth flow only, not in main nav

`Doc` = per-page reference doc (`docs/pages/...`); "—" means the reference doc is
not yet authored (the page is still covered by its E2E catalogue file). `Test` =
the per-page E2E catalogue file under `docs/tests/e2e/` (all ✅ Real pages now
have one — see [`docs/tests/e2e/README.md`](../tests/e2e/README.md)).

---

## Control Panel (CP) — http://localhost:5158

> The **config pages that back the documented app pages** also have an app-style
> per-page 5-file documentation set (README + Function/Logic/API/Design) under
> [`../CP/`](../CP/README.md) — D-380. The `Doc` column below stays the canonical
> single-file reference; the `docs/CP/<slug>/` set supplements it.

| Route | Status | Audience | Doc | Test |
|-------|--------|----------|-----|------|
| **Overview** | | | | |
| `/` | ✅ Real | Any signed-in CP user | [cp/dashboard.md](cp/dashboard.md) | [e2e/cp-dashboard.md](../tests/e2e/cp-dashboard.md) |
| **People & accounts** | | | | |
| `/admin/admins` | ✅ Real | Administrator | [cp/admin-admins.md](cp/admin-admins.md) | [e2e/cp-admin-admins.md](../tests/e2e/cp-admin-admins.md) |
| `/admin/admins/pending` | ✅ Real | Administrator | [cp/admin-admins-pending.md](cp/admin-admins-pending.md) | [e2e/cp-admin-admins-pending.md](../tests/e2e/cp-admin-admins-pending.md) |
| `/admin/others` | ✅ Real | Administrator | [cp/admin-others.md](cp/admin-others.md) | [e2e/cp-admin-others.md](../tests/e2e/cp-admin-others.md) |
| `/admin/others/pending` | ✅ Real | Administrator | [cp/admin-others-pending.md](cp/admin-others-pending.md) | [e2e/cp-admin-others-pending.md](../tests/e2e/cp-admin-others-pending.md) |
| `/admin/visitors` | ✅ Real | Administrator | [cp/admin-visitors.md](cp/admin-visitors.md) | [e2e/cp-admin-visitors.md](../tests/e2e/cp-admin-visitors.md) |
| `/admin/visitors/pending` | ✅ Real (D-385/386/387: all-data + tier-on-approve + photo viewer) | Administrator | [cp/admin-visitors-pending.md](cp/admin-visitors-pending.md) | [e2e/cp-admin-visitors-pending.md](../tests/e2e/cp-admin-visitors-pending.md) |
| `/admin/visitors/vip` | ✅ Real (D-429: VVIP/VIP registration + Mawj fields + VIP photo; creates pending) | Administrator | [cp/vip-registration.md](cp/vip-registration.md) | [e2e/cp-vip-registration.md](../tests/e2e/cp-vip-registration.md) |
| `/admin/visitors/vip/export` | ✅ Real (D-429: VVIP/VIP welcome roster — JSON API + CSV + Excel for Mawj) | Administrator | [cp/vip-export.md](cp/vip-export.md) | [e2e/cp-vip-export.md](../tests/e2e/cp-vip-export.md) |
| `/admin/delegates` | ✅ Real (D-473 #10: delegate = visitor + IsDelegate + invited country; single add + bulk-generate badges) | Administrator | _(see e2e)_ | [e2e/cp-admin-delegates.md](../tests/e2e/cp-admin-delegates.md) |
| `/admin/attendees` | ✅ Real | Administrator | [cp/admin-attendees.md](cp/admin-attendees.md) | [e2e/cp-admin-attendees.md](../tests/e2e/cp-admin-attendees.md) |
| `/admin/print-bag` | ✅ Real | Administrator | [cp/admin-print-bag.md](cp/admin-print-bag.md) | [e2e/cp-admin-print-bag.md](../tests/e2e/cp-admin-print-bag.md) |
| `/admin/interests` | ✅ Real | Administrator | [cp/admin-interests.md](cp/admin-interests.md) | [e2e/cp-admin-interests.md](../tests/e2e/cp-admin-interests.md) |
| `/admin/profile-types/visitor` | ✅ Real | Administrator | [cp/admin-profile-types-visitor.md](cp/admin-profile-types-visitor.md) | [e2e/cp-admin-profile-types-visitor.md](../tests/e2e/cp-admin-profile-types-visitor.md) |
| `/admin/profile-types/other` | ✅ Real | Administrator | [cp/admin-profile-types-other.md](cp/admin-profile-types-other.md) | [e2e/cp-admin-profile-types-other.md](../tests/e2e/cp-admin-profile-types-other.md) |
| `/admin/organisations` | ✅ Real (D-220) | Administrator  | [cp/admin-organisations.md](cp/admin-organisations.md) | [e2e/cp-admin-organisations.md](../tests/e2e/cp-admin-organisations.md) |
| `/admin/regions` | ✅ Real (D-547) | Administrator  | [cp/admin-regions.md](cp/admin-regions.md) | [e2e/cp-admin-regions.md](../tests/e2e/cp-admin-regions.md) |
| `/admin/contacts` | ✅ Real (D-281) | Administrator  | [cp/admin-contacts.md](cp/admin-contacts.md) | [e2e/cp-admin-contacts.md](../tests/e2e/cp-admin-contacts.md) |
| `/admin/countries` | ✅ Real | Administrator  | [cp/admin-countries.md](cp/admin-countries.md) | [e2e/cp-admin-countries.md](../tests/e2e/cp-admin-countries.md) |
| `/admin/vips` | ✅ Real | Administrator  | [cp/admin-vips.md](cp/admin-vips.md) | [e2e/cp-admin-vips.md](../tests/e2e/cp-admin-vips.md) |
| `/admin/invitations` | ✅ Real | Administrator | [cp/admin-invitations.md](cp/admin-invitations.md) | [e2e/cp-admin-invitations.md](../tests/e2e/cp-admin-invitations.md) |
| `/admin/reset-2fa` | ✅ Real | Administrator | [cp/admin-reset-2fa.md](cp/admin-reset-2fa.md) | [e2e/cp-admin-reset-2fa.md](../tests/e2e/cp-admin-reset-2fa.md) |
| `/admin/roles` | ✅ Real | Administrator | [cp/admin-roles.md](cp/admin-roles.md) | [e2e/cp-admin-roles.md](../tests/e2e/cp-admin-roles.md) |
| `/admin/roles/{id}/permissions` | ✅ Real | Administrator | — | [e2e/cp-admin-roles-permissions.md](../tests/e2e/cp-admin-roles-permissions.md) |
| **Programme & sessions** | | | | |
| `/admin/themes` | ✅ Real | Administrator | [cp/admin-themes.md](cp/admin-themes.md) | [e2e/cp-admin-themes.md](../tests/e2e/cp-admin-themes.md) |
| `/admin/halls` | ✅ Real | Administrator | [cp/admin-halls.md](cp/admin-halls.md) | [e2e/cp-admin-halls.md](../tests/e2e/cp-admin-halls.md) |
| `/admin/halls/seat-layouts` | ✅ Real | Administrator | — | [e2e/cp-admin-halls-seat-layouts.md](../tests/e2e/cp-admin-halls-seat-layouts.md) |
| `/admin/speakers` | ✅ Real (D-199) | Administrator | [cp/admin-speakers.md](cp/admin-speakers.md) | [e2e/cp-admin-speakers.md](../tests/e2e/cp-admin-speakers.md) |
| `/admin/speaker-presentations` | ✅ Real (D-228) | Administrator  | [cp/admin-speaker-presentations.md](cp/admin-speaker-presentations.md) | [e2e/cp-admin-speaker-presentations.md](../tests/e2e/cp-admin-speaker-presentations.md) |
| `/admin/sessions` | ✅ Real (D-199) | Administrator | [cp/admin-sessions.md](cp/admin-sessions.md) | [e2e/cp-admin-sessions.md](../tests/e2e/cp-admin-sessions.md) |
| `/admin/sessions/seat-plans` | ✅ Real | Administrator | — | [e2e/cp-admin-sessions-seat-plans.md](../tests/e2e/cp-admin-sessions-seat-plans.md) |
| `/admin/session-categories` | ✅ Real (D-226) | Administrator  | [cp/admin-session-categories.md](cp/admin-session-categories.md) | [e2e/cp-admin-session-categories.md](../tests/e2e/cp-admin-session-categories.md) |
| `/admin/programme-days` | ✅ Real (D-452) | Administrator  | [cp/admin-programme-days.md](cp/admin-programme-days.md) | [e2e/cp-admin-programme-days.md](../tests/e2e/cp-admin-programme-days.md) |
| `/admin/session-moderators` | ✅ Real | Administrator  | [cp/admin-session-moderators.md](cp/admin-session-moderators.md) | [e2e/cp-admin-session-moderators.md](../tests/e2e/cp-admin-session-moderators.md) |
| `/admin/programme/timeline` | ✅ Real | Administrator | — | [e2e/cp-admin-programme-timeline.md](../tests/e2e/cp-admin-programme-timeline.md) |
| `/admin/bookings` | ✅ Real (D-227) | Administrator  | [cp/admin-bookings.md](cp/admin-bookings.md) | [e2e/cp-admin-bookings.md](../tests/e2e/cp-admin-bookings.md) |
| `/admin/speaker-meeting-requests` | ✅ Real (D-269) | Administrator  | [cp/admin-speaker-meeting-requests.md](cp/admin-speaker-meeting-requests.md) | [e2e/cp-admin-speaker-meeting-requests.md](../tests/e2e/cp-admin-speaker-meeting-requests.md) |
| `/admin/speaker-availability` | ✅ Real (D-474/D-476 #11: team-defined availability windows → VIP free slots) | Administrator | _(see e2e)_ | [e2e/cp-admin-speaker-availability.md](../tests/e2e/cp-admin-speaker-availability.md) |
| `/admin/delegation-meetings` | ✅ Real (D-478 #11: delegation↔delegation meeting review desk — team accept/reject + notify/email) | Administrator | _(see e2e)_ | [e2e/cp-admin-delegation-meetings.md](../tests/e2e/cp-admin-delegation-meetings.md) |
| `/admin/document-requests` | ✅ Real (D-500 Wave 5: participation document requests review desk — Accept/Reject + note; mirrors speaker-meeting-requests) | Administrator | [cp/document-requests.md](cp/document-requests.md) | [e2e/cp-document-requests.md](../tests/e2e/cp-document-requests.md) |
| `/admin/badge-requests` | ✅ Real (D-500 Wave 5: badge update requests review desk — Accept applies the title to the user's profile JobTitle) | Administrator | [cp/badge-requests.md](cp/badge-requests.md) | [e2e/cp-badge-requests.md](../tests/e2e/cp-badge-requests.md) |
| `/admin/meeting-tables` | ✅ Real (D-248) | Administrator | [cp/meeting-tables.md](cp/meeting-tables.md) | [e2e/cp-meeting-tables.md](../tests/e2e/cp-meeting-tables.md) |
| `/admin/business-meetings` | ✅ Real (D-248) | Administrator | [cp/business-meetings.md](cp/business-meetings.md) | [e2e/cp-business-meetings.md](../tests/e2e/cp-business-meetings.md) |
| **Engagement, Q&A & attendance** | | | | |
| `/admin/question-queue` | ✅ Real (D-234) | Administrator  | [cp/admin-question-queue.md](cp/admin-question-queue.md) | [e2e/cp-admin-question-queue.md](../tests/e2e/cp-admin-question-queue.md) |
| `/sessions/{id}/moderate` | ✅ Real | Session moderator | — | [e2e/cp-session-moderate.md](../tests/e2e/cp-session-moderate.md) |
| `/admin/comments-moderation` | ✅ Real (D-199) | Administrator  | [cp/admin-comments-moderation.md](cp/admin-comments-moderation.md) | [e2e/cp-admin-comments-moderation.md](../tests/e2e/cp-admin-comments-moderation.md) |
| `/admin/ratings` | ✅ Real (D-496 — responses + KPI) | Administrator  | [cp/admin-ratings.md](cp/admin-ratings.md) | [e2e/cp-admin-ratings.md](../tests/e2e/cp-admin-ratings.md) |
| `/admin/rating-config` | ✅ Real (D-496 — dynamic rating config) | Administrator  | [cp/admin-rating-config.md](cp/admin-rating-config.md) | [e2e/cp-admin-rating-config.md](../tests/e2e/cp-admin-rating-config.md) |
| `/admin/session-summaries` | ✅ Real (D-238) | Administrator  | [cp/admin-session-summaries.md](cp/admin-session-summaries.md) | [e2e/cp-admin-session-summaries.md](../tests/e2e/cp-admin-session-summaries.md) |
| `/admin/hall-arrivals` | ✅ Real (D-244) | Administrator/operator | — | [e2e/cp-admin-hall-arrivals.md](../tests/e2e/cp-admin-hall-arrivals.md) |
| **Exhibition** | | | | |
| `/admin/companies` | ✅ Real (D-199) | Administrator | — | [e2e/cp-admin-companies.md](../tests/e2e/cp-admin-companies.md) |
| `/admin/exhibitors` | ✅ Real (D-202/D-274) | Administrator | [cp/admin-exhibitors.md](cp/admin-exhibitors.md) | [e2e/cp-admin-exhibitors.md](../tests/e2e/cp-admin-exhibitors.md) |
| `/admin/booths` | ✅ Real (D-199/D-222) | Administrator | [cp/admin-booths.md](cp/admin-booths.md) | [e2e/cp-admin-booths.md](../tests/e2e/cp-admin-booths.md) |
| `/admin/sponsors` | ✅ Real (D-199) | Administrator | [cp/admin-sponsors.md](cp/admin-sponsors.md) | [e2e/cp-admin-sponsors.md](../tests/e2e/cp-admin-sponsors.md) |
| `/admin/media-partners` | ✅ Real (D-199) | Administrator  | [cp/admin-media-partners.md](cp/admin-media-partners.md) | [e2e/cp-admin-media-partners.md](../tests/e2e/cp-admin-media-partners.md) |
| `/admin/venue-map` | ✅ Real (D-230) | Administrator | [cp/admin-venue-map.md](cp/admin-venue-map.md) | [e2e/cp-admin-venue-map.md](../tests/e2e/cp-admin-venue-map.md) |
| **Content & media** | | | | |
| `/admin/news` | ✅ Real (D-199) | Administrator  | [cp/admin-news.md](cp/admin-news.md) | [e2e/cp-admin-news.md](../tests/e2e/cp-admin-news.md) |
| `/admin/media` | ✅ Real (D-199) | Administrator  | [cp/admin-media.md](cp/admin-media.md) | [e2e/cp-admin-media.md](../tests/e2e/cp-admin-media.md) |
| `/admin/archive` | ✅ Real (D-199) | Administrator  | [cp/admin-archive.md](cp/admin-archive.md) | [e2e/cp-admin-archive.md](../tests/e2e/cp-admin-archive.md) |
| `/admin/media-library` | ✅ Real (D-357) | Administrator | [cp/media-library.md](cp/media-library.md) | [e2e/cp-admin-media-library.md](../tests/e2e/cp-admin-media-library.md) |
| `/admin/banners` | ✅ Real | Administrator  | [cp/admin-banners.md](cp/admin-banners.md) | [e2e/cp-admin-banners.md](../tests/e2e/cp-admin-banners.md) |
| `/admin/content-blocks` | ✅ Real | Administrator  | [cp/admin-content-blocks.md](cp/admin-content-blocks.md) | [e2e/cp-admin-content-blocks.md](../tests/e2e/cp-admin-content-blocks.md) |
| **Knowledge & AI** | | | | |
| `/admin/faq` | ✅ Real (D-218) | Administrator | — | [e2e/cp-admin-faq.md](../tests/e2e/cp-admin-faq.md) |
| `/admin/ai` | ✅ Real (CP Phase-1) | Administrator | [cp/admin-ai-dashboard.md](cp/admin-ai-dashboard.md) | [e2e/cp-admin-ai-dashboard.md](../tests/e2e/cp-admin-ai-dashboard.md) |
| `/admin/ai/services` | ✅ Real (CP Phase-1) | Administrator | [cp/admin-ai-services.md](cp/admin-ai-services.md) | [e2e/cp-admin-ai-services.md](../tests/e2e/cp-admin-ai-services.md) |
| `/admin/ai/services/{feature}` | ✅ Real (CP Phase-2) | Administrator | [cp/admin-ai-service-detail.md](cp/admin-ai-service-detail.md) | [e2e/cp-admin-ai-service-detail.md](../tests/e2e/cp-admin-ai-service-detail.md) |
| `/admin/ai/prompts` | ✅ Real (D-176) | Administrator  | [cp/admin-ai-prompts.md](cp/admin-ai-prompts.md) | [e2e/cp-admin-ai-prompts.md](../tests/e2e/cp-admin-ai-prompts.md) |
| `/admin/ai/invocations` | ✅ Real (D-176/D-179) | Administrator | — | [e2e/cp-admin-ai-invocations.md](../tests/e2e/cp-admin-ai-invocations.md) |
| **Access control & system** | | | | |
| `/admin/gates` | ✅ Real (D-148) | Administrator  | [cp/admin-gates.md](cp/admin-gates.md) | [e2e/cp-admin-gates.md](../tests/e2e/cp-admin-gates.md) |
| `/admin/gates/operator` | ✅ Real (D-148) | Gate operator | — | [e2e/cp-admin-gates-operator.md](../tests/e2e/cp-admin-gates-operator.md) |
| `/admin/gates/dashboard` | ✅ Real | Administrator | — | [e2e/cp-admin-gates-dashboard.md](../tests/e2e/cp-admin-gates-dashboard.md) |
| `/admin/configuration` | ✅ Real (D-229) | Administrator  | [cp/admin-configuration.md](cp/admin-configuration.md) | [e2e/cp-admin-configuration.md](../tests/e2e/cp-admin-configuration.md) |
| `/admin/organization-profile` | ✅ Real (D-495) | Administrator | [cp/organization-profile.md](cp/organization-profile.md) | [e2e/cp-organization-profile.md](../tests/e2e/cp-organization-profile.md) |
| `/admin/operations` | ✅ Real (D-166) | Administrator | — | [e2e/cp-admin-operations.md](../tests/e2e/cp-admin-operations.md) |
| `/admin/operation-log` | ✅ Real | Administrator | [cp/admin-operation-log.md](cp/admin-operation-log.md) | [e2e/cp-admin-operation-log.md](../tests/e2e/cp-admin-operation-log.md) |
| `/admin/logs` | ✅ Real | Administrator | [cp/admin-logs.md](cp/admin-logs.md) | [e2e/cp-admin-logs.md](../tests/e2e/cp-admin-logs.md) |
| `/admin/statistics` | ✅ Real | Administrator | — | [e2e/cp-admin-statistics.md](../tests/e2e/cp-admin-statistics.md) |
| `/admin/attendance` | ✅ Real (FR-506) | Administrator | — | [e2e/cp-admin-attendance.md](../tests/e2e/cp-admin-attendance.md) |
| `/m/{module}` | 🚧 Stub | Administrator | — | — |

### CP auth + account pages (not in main nav)

| Route | Status | Audience | Doc | Test |
|-------|--------|----------|-----|------|
| `/login` + `/login/totp` + `/login/recovery` + `/forgot-password` + `/auth/pending` + `/auth/rejected` | 🔒 Auth-only | Anyone / mid-sign-in | [cp/login.md](cp/login.md) (+ login-totp, login-recovery, forgot-password, auth-pending, auth-rejected) | [e2e/cp-auth-flow.md](../tests/e2e/cp-auth-flow.md) |
| `/account/profile` | 🔒 Bell / user menu | Any signed-in | [cp/account-profile.md](cp/account-profile.md) | [e2e/cp-account-profile.md](../tests/e2e/cp-account-profile.md) |
| `/account/notifications` | 🔒 Bell | Any signed-in | [cp/account-notifications.md](cp/account-notifications.md) | [e2e/cp-account-notifications.md](../tests/e2e/cp-account-notifications.md) |
| `/account/totp-pairing` | 🔒 First-time login | Any signed-in | [cp/account-totp-pairing.md](cp/account-totp-pairing.md) | [e2e/cp-account-totp-pairing.md](../tests/e2e/cp-account-totp-pairing.md) |
| `/admin/admins/new` · `/admin/others/new` · `/admin/visitors/new` | 🔒 Deep-link fallback | Administrator | (covered by the parent list docs) | (covered by the parent list e2e files) |

### CP framework / error pages

| Route | Status | Notes |
|-------|--------|-------|
| `/Error` | ✅ Real | Framework error |
| `/not-found` | ✅ Real | 404 page |
| `/not-permitted` | ✅ Real | 403 page (the auth-gate target for every per-page permission) |

---

## Website (Web) — http://localhost:5115

No public nav per D-064 — every page is reached via direct URL or auth redirect.

| Route | Status | Audience | Doc | Test |
|-------|--------|----------|-----|------|
| `/` (marketing landing) | ✅ Real (D-294 dynamic) | Public | [web/landing.md](web/landing.md) | [e2e/web-landing.md](../tests/e2e/web-landing.md) |
| `/account` | ✅ Real | Any signed-in | [web/home.md](web/home.md) | [e2e/web-home.md](../tests/e2e/web-home.md) |
| `/programme` | ✅ Real (D-199) | Public | — | [e2e/web-programme.md](../tests/e2e/web-programme.md) |
| `/visit` | ✅ Real | Public | — | [e2e/web-visit.md](../tests/e2e/web-visit.md) |
| `/login` | 🔒 Auth-only | Anyone | [web/login.md](web/login.md) | [e2e/web-login.md](../tests/e2e/web-login.md) |
| `/login/verify` | 🔒 Auth-only | Mid-sign-in | [web/otp-verify.md](web/otp-verify.md) | [e2e/web-otp-verify.md](../tests/e2e/web-otp-verify.md) |
| `/forgot-password` | 🔒 Auth-only | Anyone | [web/forgot-password.md](web/forgot-password.md) | [e2e/web-forgot-password.md](../tests/e2e/web-forgot-password.md) |
| `/reset-password` | 🔒 Auth-only | After ForgotPassword | [web/reset-password.md](web/reset-password.md) | [e2e/web-reset-password.md](../tests/e2e/web-reset-password.md) |
| `/account/profile` | ✅ Real (interactive) | Any signed-in | [web/account-profile.md](web/account-profile.md) | [e2e/web-account-profile.md](../tests/e2e/web-account-profile.md) |
| `/account/notifications` | ✅ Real | Any signed-in | [web/account-notifications.md](web/account-notifications.md) | [e2e/web-account-notifications.md](../tests/e2e/web-account-notifications.md) |
| `/account/pending` | 🔒 State-banner | Pending account | [web/account-pending.md](web/account-pending.md) | [e2e/web-account-pending.md](../tests/e2e/web-account-pending.md) |
| `/account/rejected` | 🔒 State-banner | Rejected account | [web/account-rejected.md](web/account-rejected.md) | [e2e/web-account-rejected.md](../tests/e2e/web-account-rejected.md) |

---

## Mobile App (Flutter) — App API + per-page docs

These rows track the **App API** (`/api/v1/app/*`) and the per-page documentation
(`docs/App/Page_NNN/` — Function/Logic/API/Design) for the owner's screen batch
(D-249). The Flutter screens themselves are still a **mockup** for API + UX
testing (see [`SIMF-APP-Build-Plan.md`](../App/SIMF-APP-Build-Plan.md)).

- 🟢 **Screen built** — the real Flutter screen + state + live `/app/*` calls are
  implemented (UI is an interim placeholder per SIMF-VID-001); E2E catalogue authored.
- ✅ **API built** — the backing endpoint is built + tested this wave (E2E linked).
- 📄 **Doc** — per-page doc authored; the backing API pre-exists or the screen has
  none; the Flutter screen is a mockup (E2E lands as the screen is built).

| Screen (route) | Status | Audience | Doc | Test |
|----------------|--------|----------|-----|------|
| #1 `splash` | 🟢 Screen built | Guest | [App/Page_001](../App/Page_001/README.md) | [e2e/mobile-splash.md](../tests/e2e/mobile-splash.md) |
| #2 `onboarding` | 🟢 Screen built | Guest | [App/Page_002](../App/Page_002/README.md) | [e2e/mobile-onboarding.md](../tests/e2e/mobile-onboarding.md) |
| #3 `signIn` (+ verify-otp / forgot / reset) | 🟢 Screen built · clean-code frozen (D-549) | Guest | [mobile/sign-in/README.md](mobile/sign-in/README.md) (legacy [App/Page_003](../App/Page_003/README.md)) | [e2e/mobile-sign-in.md](../tests/e2e/mobile-sign-in.md) |
| #3a `verifyOtp` — sign-in **2FA** email second factor | ✅ Real — Figma 758:2616; **clean-code frozen (D-552)** | Mid-sign-in | [mobile/email-otp-verify/](mobile/email-otp-verify/README.md) (legacy [App/Page_003](../App/Page_003/README.md)) | [e2e/mobile-sign-in.md](../tests/e2e/mobile-sign-in.md) (2FA scenarios) |
| #3b `forgotPassword` — request a reset OTP | ✅ Real · **clean-code frozen (D-556)** (KSA auth chrome; unbound) | Guest | [mobile/forgot-password/](mobile/forgot-password/README.md) | [e2e/mobile-sign-in.md](../tests/e2e/mobile-sign-in.md) (forgot/reset flow) |
| #3c `resetPassword` — OTP + new password | ✅ Real · **clean-code frozen (D-557)** (KSA auth chrome; unbound) | After forgot | [mobile/reset-password/](mobile/reset-password/README.md) | [e2e/mobile-sign-in.md](../tests/e2e/mobile-sign-in.md) (forgot/reset flow) |
| ~~#4 `signUpType`~~ **REMOVED (D-332)** — invented; not in the mockup. Sign-up goes Page 003 → Page 005 directly | ⛔ Removed | — | [App/Page_004](../App/Page_004/README.md) | [e2e/mobile-sign-up-type.md](../tests/e2e/mobile-sign-up-type.md) |
| #5 `signUpForm` | ✅ Real — Figma 168:3454; **clean-code frozen (D-551)** | Guest | [mobile/sign-up-form/](mobile/sign-up-form/README.md) (legacy [App/Page_005](../App/Page_005/README.md)) | [e2e/mobile-sign-up-form.md](../tests/e2e/mobile-sign-up-form.md) |
| #6 `emailOtp` (sign-up email verification) | ✅ Real — Figma 505:837; **clean-code frozen (D-553)** | Guest | [mobile/sign-up-email-verify/](mobile/sign-up-email-verify/README.md) (legacy [App/Page_006](../App/Page_006/README.md)) | [e2e/mobile-email-otp.md](../tests/e2e/mobile-email-otp.md) |
| #7 `signUpVisitor` — **profile data** (ProfileType + lookups; **Next → interests**) | ✅ Real — Figma 168:2972; **clean-code frozen (D-546)** | Visitor | [mobile/sign-up-visitor/](mobile/sign-up-visitor/README.md) _(Figma 168:2972; clean-code frozen D-546)_ | [e2e/mobile-sign-up-visitor.md](../tests/e2e/mobile-sign-up-visitor.md) |
| #7‑01 `signUpInterests` — **interests (1–10) + the single `POST /app/account/user-profile` save** | ✅ Real — Figma 505:1083; **clean-code frozen (D-550)** | Visitor | [mobile/sign-up-interests/](mobile/sign-up-interests/README.md) (legacy [App/Page_007-01](../App/Page_007-01/README.md)) | [e2e/mobile-sign-up-interests.md](../tests/e2e/mobile-sign-up-interests.md) |
| #9 `terms` (`GET /app/content/terms`) | 🟢 Screen built | Guest | [App/Page_009](../App/Page_009/README.md) | [e2e/mobile-terms.md](../tests/e2e/mobile-terms.md) |
| #10 `registrationSuccess` | ✅ Real — Figma `505:1451`; **clean-code frozen (D-625)**. Terminal sign-up confirmation (offline-safe, no write API); reference card (real ref / mask), status + home actions, visual-only contact tiles | Visitor (pending) | [mobile/registration-success/](mobile/registration-success/README.md) | [e2e/mobile-registration-success.md](../tests/e2e/mobile-registration-success.md) |
| #11 `registrationStatus` (`GET /app/users/me`) | ✅ Real — Figma `1701:3789`; **clean-code frozen (D-623)** — 517→235 + 5 widgets, golden held | Visitor (pending) | [mobile/registration-status/](mobile/registration-status/README.md) _(legacy: [App/Page_011](../App/Page_011/README.md))_ | [e2e/mobile-registration-status.md](../tests/e2e/mobile-registration-status.md) |
| #13 `home` (`GET …/notifications/unread-count`, signed-in only; `/app/bootstrap` is built but unused by the app) | ✅ Real — Figma 758:1134/2910; **clean-code frozen (D-602)** — 1271→111-line screen + 9 widgets, goldens both states | Guest+ | [mobile/home/](mobile/home/README.md) _(legacy: [App/Page_013](../App/Page_013/README.md))_ | [e2e/mobile-home.md](../tests/e2e/mobile-home.md) |
| #14 `myArea` (`GET /app/account/dashboard` + `.ics`/`.vcf`) | ✅ Real — Figma 213:963; **clean-code frozen (D-607)** — 790→269 + 3 widgets, golden | Visitor | [mobile/my-area/](mobile/my-area/README.md) _(legacy: [App/Page_014](../App/Page_014/README.md))_ | [e2e/mobile-my-area.md](../tests/e2e/mobile-my-area.md) |
| #15 `venueMap` (`GET /app/venue-map` + `/app/booths` + `/{id}`) | ✅ Real — Figma 215:562; **clean-code frozen (D-615)** — 775→333 + 5 widgets; L4 chrome matches (2 owner-decided deviations: 2D plane vs map tiles per D-199, close-X vs logo badge) | Guest | [mobile/venue-map/](mobile/venue-map/README.md) _(legacy: [App/Page_015](../App/Page_015/README.md))_ | [e2e/mobile-venue-map.md](../tests/e2e/mobile-venue-map.md) |
| #16 `sessions` (`GET /app/programme/days`) | ✅ Real — Figma 883:2308; **clean-code frozen (D-598)** — LTR day strip, 3 type tabs | Visitor+ (D-576 gate) | [mobile/sessions/](mobile/sessions/README.md) _(legacy: [App/Page_016](../App/Page_016/README.md) — predates D-597/598)_ | [e2e/mobile-agenda.md](../tests/e2e/mobile-agenda.md) |
| #17 `sessionDetail` (`GET /app/programme/sessions/{id}` + `…/sessions/{id}/seats`) | ✅ Real — Figma 889:2450; **clean-code frozen (D-597)** | Guest+ (seat card: Visitor) | [mobile/session-detail/](mobile/session-detail/README.md) _(legacy: [App/Page_017](../App/Page_017/README.md))_ | [e2e/mobile-session-detail.md](../tests/e2e/mobile-session-detail.md) |
| #18 `mySeat` (`GET /app/sessions/{id}/seats` + reserve/release) | ✅ Real — Figma 898:2873 full parity; **clean-code frozen (D-600)** — hall card = shared `HallSeatMapCard` | Visitor (login-only) | [mobile/my-seat/](mobile/my-seat/README.md) _(legacy: [App/Page_018](../App/Page_018/README.md))_ | [e2e/mobile-my-seat.md](../tests/e2e/mobile-my-seat.md) |
| `seatPicker` (`POST …/seats/reserve` / `reserve-random`) — D-485 | ✅ Real — **clean-code frozen (D-600)** — reuses the shared `HallSeatMapCard` (selectable config, owner-directed); render-lock golden | Visitor (login-only) | [mobile/seat-picker/](mobile/seat-picker/README.md) | [e2e/mobile-seat-picker.md](../tests/e2e/mobile-seat-picker.md) |
| `joinSessionHub` (`GET /app/programme/sessions`) — D-485 | ✅ Real — **clean-code frozen (D-601)** — pull-to-refresh added (was missing); RTL chevron double-mirror fixed; render-lock golden | Visitor (login-only) | [mobile/join-session-hub/](mobile/join-session-hub/README.md) | [e2e/mobile-join-hub.md](../tests/e2e/mobile-join-hub.md) |
| `savedSessions` (`GET /app/sessions/favourites` ∩ programme) — #8, D-584 | 🗑️ **Removed from app — D-609 (2026-07-04)**, owner directive: screen backed up as `.bk`, entry tile deleted from My Area, route removed _(was: ✅ built to Figma `1701:8928`, clean-code frozen D-599)_ | Visitor / Exhibitor (approved) | [mobile/saved-sessions/](mobile/saved-sessions/README.md) | [e2e/mobile-saved-sessions.md](../tests/e2e/mobile-saved-sessions.md) |
| #19 `speakers` (`GET /app/speakers`; avatar `GET /app/assets/SpeakerPhoto/{id}/image`) | ✅ Real — Figma 908:1744; **clean-code frozen (D-608)** — 447→229 + 2 widgets, golden held | Guest+ | [mobile/speakers/](mobile/speakers/README.md) _(legacy: [App/Page_019](../App/Page_019/README.md))_ | [e2e/mobile-speakers.md](../tests/e2e/mobile-speakers.md) |
| #20 `speakerProfile` (`GET /app/speakers/{id}` + `…/meeting-requests`; photo `GET /app/assets/SpeakerPhoto/{id}/image`) | ✅ Real — Figma 908:2110; **clean-code frozen (D-606)** — 1098→272 + 6 widgets, golden held | Guest+ (meeting: Visitor) | [mobile/speaker-profile/](mobile/speaker-profile/README.md) _(legacy: [App/Page_020](../App/Page_020/README.md))_ | [e2e/mobile-speaker-profile.md](../tests/e2e/mobile-speaker-profile.md) |
| #22 `booths` (`GET /app/booths` + `/{id}`) | ✅ Real — Figma 922:2458; **clean-code frozen (D-618)** — 833→211 + 6 widgets + SimfPullableHost DRY, golden held; contact-box mailto/tel flagged | Guest+ | [mobile/booths/](mobile/booths/README.md) _(legacy: [App/Page_022](../App/Page_022/README.md))_ | [e2e/mobile-booths.md](../tests/e2e/mobile-booths.md) |
| `boothMap` (`/booths/:id/map`) — #9 | 🟢 Screen built; the booth's "أرشدني" opens a pushed venue map that selects + centres the booth's node; on-device render pending | Guest+ | _(reuses Page_015 map)_ | [e2e/mobile-booths.md](../tests/e2e/mobile-booths.md) |
| #23 `sponsors` (`GET /app/sponsors`) | ✅ Real — Figma 922:2824; **clean-code frozen (D-620)** — 489→145 + 3 widgets (logo/card/grid) + SimfSectionHeader DRY, golden held | Guest+ | [mobile/sponsors/](mobile/sponsors/README.md) _(legacy: [App/Page_023](../App/Page_023/README.md))_ | [e2e/mobile-sponsors.md](../tests/e2e/mobile-sponsors.md) |
| #220 `exhibitorDetail` (`GET /app/booths/{id}`) | ✅ Real — Figma `1439:11881`; **clean-code frozen (D-619)** — thin EntityDetailScaffold wrapper, shared entity-detail helpers deduped | Guest+ | _(Figma 1439:11881)_ | [e2e/mobile-exhibitor-detail.md](../tests/e2e/mobile-exhibitor-detail.md) |
| #221 `sponsorDetail` (`GET /app/sponsors/{id}`) | ✅ Real — Figma `1439:11826`; **clean-code frozen (D-619)** — thin EntityDetailScaffold wrapper, shared entity-detail helpers deduped | Guest+ | _(Figma 1439:11826)_ | [e2e/mobile-sponsor-detail.md](../tests/e2e/mobile-sponsor-detail.md) |
| #21 `delegations` (`GET /app/delegations` — public, anonymous) | ✅ Real — Figma `1426:10771`; **clean-code frozen (D-624)**. Invited-country delegations — stats strip + search + per-country card (flag/name/head + dates/member count); head from `Country.HeadOfDelegationUserProfileId`, members derived from active delegate profiles; CP sets dates + head on `/admin/countries` (additive migration D499, D-499; screen #21 restored from D-277) | Guest+ (public) | [mobile/delegations/](mobile/delegations/README.md) | [e2e/mobile-delegations.md](../tests/e2e/mobile-delegations.md) |
| #24 `archive` (`GET /app/archive` + `/{id}`) | ✅ Real — Figma 925:3079; **clean-code frozen (D-617)** — 893→252 + 8 widgets + shared isHttpUrl, golden held; 3 Level-F gaps flagged (video-tile tap, speaker-card tap, countryId flag) | Guest+ | [mobile/archive/](mobile/archive/README.md) _(legacy: [App/Page_024](../App/Page_024/README.md))_ | [e2e/mobile-archive.md](../tests/e2e/mobile-archive.md) |
| #29 `news` (`GET /app/news` + `/{id}`; thumbnail `GET /app/assets/NewsImage/{id}/image`) | 🟢 Screen built (D-308); Figma 957:2197 card — thumbnail + date (P2) | Guest+ | [App/Page_029](../App/Page_029/README.md) | [e2e/mobile-news.md](../tests/e2e/mobile-news.md) |
| #30 `gallery` (`GET /app/media` + `/{id}/image`) | ✅ Real — Figma `947:3764`; **clean-code frozen (D-626)**. Media-coverage hub gallery tab — 3-tab selector + الصور/الفيديوهات two-up grids; tile bitmaps (D-342); video playback deferred | Guest+ | [mobile/gallery/](mobile/gallery/README.md) | [e2e/mobile-gallery.md](../tests/e2e/mobile-gallery.md) |
| #37 `aboutForum` (`GET /app/content/about`) | 🟢 Screen built (D-311); restructured Figma `1116:16448` — mission/vision/details/themes (D-465) | Guest+ | [App/Page_037](../App/Page_037/README.md) | [e2e/mobile-about.md](../tests/e2e/mobile-about.md) |
| #40 `rate` (`GET/POST /app/feedback/form\|submit`) | 🟢 Screen built (D-310); dynamic config-driven form + session deep-link (D-496) | Visitor (login-only) | [App/Page_040](../App/Page_040/README.md) | [e2e/mobile-rate.md](../tests/e2e/mobile-rate.md) |
| #31 `mediaPartners` (`GET /app/media-partners` + logo `GET /app/assets/MediaPartnerLogo/{id}/image`) | 🟢 Screen built (D-306); Figma 958:2246 hub + real logos (P1) | Guest+ | [App/Page_031](../App/Page_031/README.md) | [e2e/mobile-media-partners.md](../tests/e2e/mobile-media-partners.md) |
| #12 `guestMode` (no API) | 🟢 Screen built (D-316) | Guest | [App/Page_012](../App/Page_012/README.md) | [e2e/mobile-guest-mode.md](../tests/e2e/mobile-guest-mode.md) |
| #33 `notifications` (`POST /app/account/notifications/list` + `/{id}/read` + `/read-all`) | ✅ Real — Figma 223:4264; **clean-code frozen (D-621)** — 687→318 + 4 widgets + SimfPullableHost DRY, golden held; VIP-star deviation + 758:2491 stale-node inline comments flagged | Signed-in | [mobile/notifications/](mobile/notifications/README.md) _(legacy: [App/Page_033](../App/Page_033/README.md))_ | [e2e/mobile-notifications.md](../tests/e2e/mobile-notifications.md) |
| #35 `meetPeople` (`GET /app/account/recommendations/meet-like-you`) | 🟢 Screen built (D-313); Figma `1072:13409` parity (D-448); backend match reason (D-451) | Visitor (login-only) | [App/Page_035](../App/Page_035/README.md) | [e2e/mobile-meet-people.md](../tests/e2e/mobile-meet-people.md) |
| #38 `accessibility` (no API) | 🟢 Screen built (D-314); persisted + applied app-wide (D-327); Figma `1116:16630` + screen-reader/captions wired (D-465) | Guest+ | [App/Page_038](../App/Page_038/README.md) | [e2e/mobile-accessibility.md](../tests/e2e/mobile-accessibility.md) |
| #41 `more` (no API) | 🟢 Screen built (D-315); grouped re-skin Figma `1129:17224` — profile card + sections + sign-out (D-465) | Guest+ | [App/Page_041](../App/Page_041/README.md) | [e2e/mobile-more.md](../tests/e2e/mobile-more.md) |
| #34 `aiSummary` (`GET /app/programme/sessions/{id}/summary`) | 🟢 Screen built (D-317); Figma `1072:13518`; #1/#6 details-only when opened with a sessionId · **clean-code frozen (D-612)** — 715→291 + 3 widgets + 3 DRY wins (shared `gregorianWeekdayName` / `SimfEmptyState` / `SimfErrorState`); golden held | Guest+ | [App/Page_034](../App/Page_034/README.md) | [e2e/mobile-ai-summary.md](../tests/e2e/mobile-ai-summary.md) |
| #111 `sessionSummaryList` (cached programme + favourites/booked overlay) | 🟢 Figma `1388:8392`; searchable, day-grouped list with الجميع/جلساتي/المفضلة tabs + the المفضلة heart (`GET/POST/DELETE /app/sessions/favourites`); tap → #34 · **clean-code frozen (D-613)** — 596→249 + card widget + shared `SimfFilterSearchField` (DRY with الوفود); golden held | Guest+ (favourites/booked Approved) | _(Figma 1388:8392)_ | [e2e/mobile-session-summaries.md](../tests/e2e/mobile-session-summaries.md) |
| #26 `sendQuestion` (`POST /app/sessions/{id}/questions`) | ✅ Real — Figma 934:3636; **clean-code frozen (D-604)** — golden un-tofu'd the submit label | Visitor (login-only) | [mobile/send-question/](mobile/send-question/README.md) _(legacy: [App/Page_026](../App/Page_026/README.md))_ | [e2e/mobile-send-question.md](../tests/e2e/mobile-send-question.md) |
| ~~#28 `audienceComments`~~ | ⛔ **REMOVED from the app (D-605, 2026-07-04)** — rejected by the customer; the screen, route, data layer, tests + E2E were deleted. The backend `SessionComment` endpoints/tables + CP moderation are a **separate teardown** (frozen-schema migration) handed to the backend session. | — | _(removed)_ | _(removed)_ |
| #32 `badge` (`GET /app/account/dashboard`) | 🟢 Screen built (D-320) | Signed-in | [App/Page_032](../App/Page_032/README.md) | [e2e/mobile-badge.md](../tests/e2e/mobile-badge.md) |
| #25 `liveBroadcast` (`GET /app/programme/sessions/{id}`) | ✅ Real — Figma 934:3450; **clean-code frozen (D-603)** — 1286→348 + 5 widgets, golden | Login-only (D-577) | [mobile/live-broadcast/](mobile/live-broadcast/README.md) _(legacy: [App/Page_025](../App/Page_025/README.md))_ | [e2e/mobile-live.md](../tests/e2e/mobile-live.md) |
| #36 `chatbot` (interim shell — no API) | 🟢 Screen built (D-322); Figma `1064:13066` parity (D-448) | Guest+ | [App/Page_036](../App/Page_036/README.md) | [e2e/mobile-chatbot.md](../tests/e2e/mobile-chatbot.md) |
| #24-01 `archiveDetail` (`GET /app/archive/{id}`) | ✅ API built (NEW, D-273) | Public (anonymous) | [App/Page_024-01](../App/Page_024-01/README.md) | [e2e/mobile-archive-detail.md](../tests/e2e/mobile-archive-detail.md) |
| `shareMyContact` (`GET/POST /app/account/share-token` + `.vcf`) — FDS-014, additive | 🟢 Screen built (D-324) | Visitor (approved) | [App/FDS-014-Contact-UI](../App/FDS-014-Contact-UI/README.md) | [e2e/mobile-my-contacts.md](../tests/e2e/mobile-my-contacts.md) |
| `scanContact` (`POST /app/contacts/resolve` + `/save`) — FDS-014, additive | 🟢 Screen built (D-324) | Visitor (approved) | [App/FDS-014-Contact-UI](../App/FDS-014-Contact-UI/README.md) | [e2e/mobile-my-contacts.md](../tests/e2e/mobile-my-contacts.md) |
| `myContacts` (`GET /app/contacts` + `/{id}` delete + `/{id}/vcard`) — FDS-014, additive | 🟢 Screen built (D-324) | Visitor (approved) | [App/FDS-014-Contact-UI](../App/FDS-014-Contact-UI/README.md) | [e2e/mobile-my-contacts.md](../tests/e2e/mobile-my-contacts.md) |
| `identityVerification` (`POST /app/account/avatar`) — avatar liveness, additive | 🟢 Screen built (D-404) · **clean-code decompose (D-610, 489→304 + 3 files)** · **full-bleed exact-Figma redesign (D-611)** — owner chose full-bleed camera, removed the framed box + prompt + progress bar; liveness gating unchanged; 2 goldens | Visitor (approved) | Figma 758:4180/4248/4316 (full-bleed) | [e2e/mobile-identity-verification.md](../tests/e2e/mobile-identity-verification.md) |
| `requests` (`GET /app/my-requests` + `POST …/document-requests` · `…/badge-requests` · `…/my-requests/cancel`) — Wave 5 (D-500, Figma `1408:9726`); the unified requests feed — **supersedes** the D-479 read-only My-meetings screen. **D-595:** header renamed to **اللقاءات الثنائية**, top row cut to 2 buttons (طلب جديد + السجل) with exact Figma clipboard/history glyphs, gold iconamoon chevron, today's cards date as "time · اليوم" · **clean-code frozen (D-614)** — 623→171 + 4 widget files | 🟢 Screen built (D-500) | Visitor (approved) | [mobile/requests.md](mobile/requests.md) | [e2e/mobile-requests.md](../tests/e2e/mobile-requests.md) |
| #115 `myMeetings` (`GET /app/my-requests`, filtered to meetings) — **المقابلات** (D-587, Figma `1701:9406`); the caller's speaker + delegation meetings as person cards with الكل/مكتملة/قيد الانتظار/مرفوضة chips; reached from the My-Area "مقابلات" counter. A read-only view over the الطلبات feed (no new endpoint) | 🗑️ **Removed from app — D-609 (2026-07-04)**, owner directive: screen backed up as `.bk`, entry tile deleted from My Area, route removed _(was: 🟢 built D-587)_ | Visitor / Exhibitor (approved) | [mobile/my-meetings.md](mobile/my-meetings.md) | [e2e/mobile-my-meetings.md](../tests/e2e/mobile-my-meetings.md) |
| `sessionModerate` (`GET/PUT /app/sessions/{id}/questions/moderate·push·hide`) — moderator Q&A, additive (D-509: 5 chips + reject/answered/on-stage re-skin) | ✅ Real — Figma 1461:12227; **clean-code frozen (D-622)** — 699→264 + 3 widgets + SimfPullToRefresh DRY; L4 matches, country-vs-to-host subtitle + 4-frame-id inconsistency flagged | Moderator+ (server: per-session grant) | [mobile/session-moderate/](mobile/session-moderate/README.md) | [e2e/mobile-session-moderate.md](../tests/e2e/mobile-session-moderate.md) |
| `gateScanner` (`GET /app/gates/my-assignments` · `POST /app/gates/{id}/scans`) — staff gate console (D-509: setup + operator دخول/خروج honoured for Both-mode gates), additive | ✅ Real — Figma 758:4651; **clean-code frozen (D-616)** — 923→361 + 5 widgets, setup golden, dropdown-font bug fixed | Staff (server: GateOperator grant) | [mobile/gate-scan/](mobile/gate-scan/README.md) _(Figma 758:4651/4819/4886)_ | [e2e/mobile-gate-scan.md](../tests/e2e/mobile-gate-scan.md) |
| `staffRegisterVisitor` (`POST /app/staff/visitors/register-onsite` · `…/{id}/id-document` · `…/{id}/avatar`) — staff walk-in visitor registration, additive (D-509) | 🟢 Screen built (D-509) · **clean-code frozen (D-559)** | Staff (server: Visitors.RegisterOnsite) | [mobile/staff-register-visitor/](mobile/staff-register-visitor/README.md) (Figma 1467:12357) | [e2e/mobile-staff-register-visitor.md](../tests/e2e/mobile-staff-register-visitor.md) |
| `biometricStepUp` (`POST /app/auth/device-keys/step-up` + gated register) — #7a biometric-enable emailed-OTP step-up | ✅ Real (#7a) · **clean-code frozen (D-554)** — confirm → email a code → enrol; reached from the Face-ID toggle / post-sign-in nudge; server-enforced | Visitor+ (approved) | [mobile/biometric-step-up/](mobile/biometric-step-up/README.md) _(reuses the KSA OTP frame; unbound)_ | [e2e/mobile-biometric-step-up.md](../tests/e2e/mobile-biometric-step-up.md) |
| `badgeActivation` (`POST /app/auth/badge/activation/start` · `…/complete`) — Part B (D-430) passwordless badge activation | ✅ Real · **clean-code frozen (D-555)** — verify emailed code → set first password; reached from badge-scan | Guest (badge holder) | [mobile/badge-activation/](mobile/badge-activation/README.md) _(KSA auth chrome; unbound)_ | [e2e/mobile-badge-activation.md](../tests/e2e/mobile-badge-activation.md) |
| `badgeSignIn` (`POST /app/auth/badge/resolve`) — Part B (D-430) badge-QR sign-in entry | ✅ Real · **clean-code frozen (D-558)** — scan/type the badge QR → branch to sign-in or activation | Guest (badge holder) | [mobile/badge-sign-in/](mobile/badge-sign-in/README.md) _(shared QrScanView; unbound)_ | [e2e/mobile-badge-activation.md](../tests/e2e/mobile-badge-activation.md) |
| #200 `forumGuide` (no API — static guide) | 🟢 Screen built from ComingSoon (D-464 stub → Figma `1388:7493`); gold intro banner + five numbered step cards; reached from المزيد → دليل الملتقى | Guest+ | _(Figma 1388:7493)_ | [e2e/mobile-forum-guide.md](../tests/e2e/mobile-forum-guide.md) |
| #201 `faq` (`GET /app/faq` — public, anonymous) | 🟢 Screen built from ComingSoon (D-464 stub → Figma `1388:7567`); accordion over the D-211 FAQ tables; new public read endpoint (no schema change); reached from المزيد → الأسئلة الشائعة | Guest+ | [mobile/faq/](mobile/faq/README.md) _(Figma 1388:7567)_ | [e2e/mobile-faq.md](../tests/e2e/mobile-faq.md) |
| #203 `contactUs` (`POST /app/contact-inquiry` + `GET /app/organization-profile`) | ✅ Real — Figma `1388:7711`; **clean-code frozen (D-627)**. Form + org-profile info panel + social; _(backend, other session: new `ContactInquiries` table + CP inbox `/admin/contact-inquiries` perms `ContactInquiries.View`/`.Manage` — pending)_ | Guest+ (submit anonymous) | [mobile/contact-us/](mobile/contact-us/README.md) | [e2e/mobile-contact-us.md](../tests/e2e/mobile-contact-us.md) |
| #202 `sessionPresentations` (`GET /app/presentations`) | 🟢 Screen built from ComingSoon (D-464 stub → Figma `1388:7621`, title **"الجلسات"** — matches the Home tile); day-tabbed session list over the D-228 `SpeakerPresentation` files; reached from the Home "الجلسات" about-tile (D-583). **Owner 2026-07-03:** card tap → session **detail** (17); the gold **تحميل** button → session **summary** (34) — no longer downloads the deck (the `/{id}/file` endpoint is retained on the backend but unused by this screen) | Approved | _(Figma 1388:7621)_ | [e2e/mobile-session-presentations.md](../tests/e2e/mobile-session-presentations.md) |
| #113 `myAreaSessions` (`GET /app/account/sessions`) | 🗑️ **Removed from app — D-609 (2026-07-04)**, owner directive: screen backed up as `.bk`, More-menu "عروض الجلسات" row deleted, route removed _(was: 🟢 Wave 2 my-sessions "عروض الجلسات", Figma `1388:9067`, D-588)_ | Approved | _(Figma 1388:9067)_ | [e2e/mobile-my-sessions.md](../tests/e2e/mobile-my-sessions.md) |

---

## How to use this index

- **Reading the system:** start at a route → **Doc** (what the page does, who
  uses it, what API it calls) → **Test** (the executable E2E scenarios that
  prove it works).
- **Adding a new page (the six artefacts, one changeset):**
  1. Add a row here (route, status, audience, doc + test paths).
  2. Author the per-page doc from `docs/pages/_TEMPLATE.md`.
  3. Author the E2E catalogue file from `docs/tests/e2e/_TEMPLATE.md` + index it
     in `docs/tests/e2e/README.md` (HARD RULE — see project `CLAUDE.md`).
  4. Add the page to the relevant manual chapter.
  5. Add/update the use-case in `SIMF-UCS-001`.
  6. Add the per-page/per-action permission (HARD RULE — see `CLAUDE.md`).

The reverse rule: **a page that exists in code but not on this index has not
shipped.** At PR review, search this file for the route; a missing row = an
incomplete PR.

> **Doc-column debt (cleared for D-356, updated 2026-06-11):** every CP page
> touched by the D-356 Uniform CRUD wave now has a per-page reference doc — the
> 7 full-CrudShell conversions + 8 refreshed (Phase 5), plus the 22
> export/lookup pages backfilled on 2026-06-11 (AI prompts, Archive, Banners,
> Bookings, Comments moderation, Configuration, Contacts, Content blocks,
> Countries, Gates, Media, Media partners, News, Organisations, Question queue,
> Ratings, Session categories, Session moderators, Session summaries, Speaker
> meeting requests, Speaker presentations, VIPs). Any remaining `Doc = "—"` rows
> are CP pages **outside** the D-356 scope (e.g. seat-layout editors, gates
> operator/dashboard, programme timeline, statistics, attendance, AI invocations);
> each is still covered by its E2E catalogue file, and their reference docs stay
> tracked as a separate follow-up.
