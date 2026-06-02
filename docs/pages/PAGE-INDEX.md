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
| `/admin/visitors/pending` | ✅ Real | Administrator | [cp/admin-visitors-pending.md](cp/admin-visitors-pending.md) | [e2e/cp-admin-visitors-pending.md](../tests/e2e/cp-admin-visitors-pending.md) |
| `/admin/attendees` | ✅ Real | Administrator | [cp/admin-attendees.md](cp/admin-attendees.md) | [e2e/cp-admin-attendees.md](../tests/e2e/cp-admin-attendees.md) |
| `/admin/print-bag` | ✅ Real | Administrator | [cp/admin-print-bag.md](cp/admin-print-bag.md) | [e2e/cp-admin-print-bag.md](../tests/e2e/cp-admin-print-bag.md) |
| `/admin/interests` | ✅ Real | Administrator | [cp/admin-interests.md](cp/admin-interests.md) | [e2e/cp-admin-interests.md](../tests/e2e/cp-admin-interests.md) |
| `/admin/profile-types/visitor` | ✅ Real | Administrator | [cp/admin-profile-types-visitor.md](cp/admin-profile-types-visitor.md) | [e2e/cp-admin-profile-types-visitor.md](../tests/e2e/cp-admin-profile-types-visitor.md) |
| `/admin/profile-types/other` | ✅ Real | Administrator | [cp/admin-profile-types-other.md](cp/admin-profile-types-other.md) | [e2e/cp-admin-profile-types-other.md](../tests/e2e/cp-admin-profile-types-other.md) |
| `/admin/organisations` | ✅ Real (D-220) | Administrator | — | [e2e/cp-admin-organisations.md](../tests/e2e/cp-admin-organisations.md) |
| `/admin/countries` | ✅ Real | Administrator | — | [e2e/cp-admin-countries.md](../tests/e2e/cp-admin-countries.md) |
| `/admin/delegations` | ✅ Real | Administrator | — | [e2e/cp-admin-delegations.md](../tests/e2e/cp-admin-delegations.md) |
| `/admin/vips` | ✅ Real | Administrator | — | [e2e/cp-admin-vips.md](../tests/e2e/cp-admin-vips.md) |
| `/admin/invitations` | ✅ Real | Administrator | — | [e2e/cp-admin-invitations.md](../tests/e2e/cp-admin-invitations.md) |
| `/admin/reset-2fa` | ✅ Real | Administrator | [cp/admin-reset-2fa.md](cp/admin-reset-2fa.md) | [e2e/cp-admin-reset-2fa.md](../tests/e2e/cp-admin-reset-2fa.md) |
| `/admin/roles` | ✅ Real | Administrator | [cp/admin-roles.md](cp/admin-roles.md) | [e2e/cp-admin-roles.md](../tests/e2e/cp-admin-roles.md) |
| `/admin/roles/{id}/permissions` | ✅ Real | Administrator | — | [e2e/cp-admin-roles-permissions.md](../tests/e2e/cp-admin-roles-permissions.md) |
| **Programme & sessions** | | | | |
| `/admin/themes` | ✅ Real | Administrator | [cp/admin-themes.md](cp/admin-themes.md) | [e2e/cp-admin-themes.md](../tests/e2e/cp-admin-themes.md) |
| `/admin/halls` | ✅ Real | Administrator | [cp/admin-halls.md](cp/admin-halls.md) | [e2e/cp-admin-halls.md](../tests/e2e/cp-admin-halls.md) |
| `/admin/halls/seat-layouts` | ✅ Real | Administrator | — | [e2e/cp-admin-halls-seat-layouts.md](../tests/e2e/cp-admin-halls-seat-layouts.md) |
| `/admin/speakers` | ✅ Real (D-199) | Administrator | — | [e2e/cp-admin-speakers.md](../tests/e2e/cp-admin-speakers.md) |
| `/admin/speaker-presentations` | ✅ Real (D-228) | Administrator | — | [e2e/cp-admin-speaker-presentations.md](../tests/e2e/cp-admin-speaker-presentations.md) |
| `/admin/sessions` | ✅ Real (D-199) | Administrator | — | [e2e/cp-admin-sessions.md](../tests/e2e/cp-admin-sessions.md) |
| `/admin/sessions/seat-plans` | ✅ Real | Administrator | — | [e2e/cp-admin-sessions-seat-plans.md](../tests/e2e/cp-admin-sessions-seat-plans.md) |
| `/admin/session-categories` | ✅ Real (D-226) | Administrator | — | [e2e/cp-admin-session-categories.md](../tests/e2e/cp-admin-session-categories.md) |
| `/admin/session-moderators` | ✅ Real | Administrator | — | [e2e/cp-admin-session-moderators.md](../tests/e2e/cp-admin-session-moderators.md) |
| `/admin/programme/timeline` | ✅ Real | Administrator | — | [e2e/cp-admin-programme-timeline.md](../tests/e2e/cp-admin-programme-timeline.md) |
| `/admin/bookings` | ✅ Real (D-227) | Administrator | — | [e2e/cp-admin-bookings.md](../tests/e2e/cp-admin-bookings.md) |
| `/admin/meeting-requests` | ✅ Real | Administrator | — | [e2e/cp-admin-meeting-requests.md](../tests/e2e/cp-admin-meeting-requests.md) |
| `/admin/meeting-tables` | ✅ Real (D-248) | Administrator | [cp/meeting-tables.md](cp/meeting-tables.md) | [e2e/cp-meeting-tables.md](../tests/e2e/cp-meeting-tables.md) |
| `/admin/business-meetings` | ✅ Real (D-248) | Administrator | [cp/business-meetings.md](cp/business-meetings.md) | [e2e/cp-business-meetings.md](../tests/e2e/cp-business-meetings.md) |
| **Engagement, Q&A & attendance** | | | | |
| `/admin/question-queue` | ✅ Real (D-234) | Administrator | — | [e2e/cp-admin-question-queue.md](../tests/e2e/cp-admin-question-queue.md) |
| `/sessions/{id}/moderate` | ✅ Real | Session moderator | — | [e2e/cp-session-moderate.md](../tests/e2e/cp-session-moderate.md) |
| `/admin/comments-moderation` | ✅ Real (D-199) | Administrator | — | [e2e/cp-admin-comments-moderation.md](../tests/e2e/cp-admin-comments-moderation.md) |
| `/admin/ratings` | ✅ Real (D-199) | Administrator | — | [e2e/cp-admin-ratings.md](../tests/e2e/cp-admin-ratings.md) |
| `/admin/session-summaries` | ✅ Real (D-238) | Administrator | — | [e2e/cp-admin-session-summaries.md](../tests/e2e/cp-admin-session-summaries.md) |
| `/admin/hall-arrivals` | ✅ Real (D-244) | Administrator/operator | — | [e2e/cp-admin-hall-arrivals.md](../tests/e2e/cp-admin-hall-arrivals.md) |
| **Exhibition** | | | | |
| `/admin/companies` | ✅ Real (D-199) | Administrator | — | [e2e/cp-admin-companies.md](../tests/e2e/cp-admin-companies.md) |
| `/admin/booths` | ✅ Real (D-199/D-222) | Administrator | — | [e2e/cp-admin-booths.md](../tests/e2e/cp-admin-booths.md) |
| `/admin/sponsors` | ✅ Real (D-199) | Administrator | — | [e2e/cp-admin-sponsors.md](../tests/e2e/cp-admin-sponsors.md) |
| `/admin/media-partners` | ✅ Real (D-199) | Administrator | — | [e2e/cp-admin-media-partners.md](../tests/e2e/cp-admin-media-partners.md) |
| `/admin/venue-map` | ✅ Real (D-230) | Administrator | — | [e2e/cp-admin-venue-map.md](../tests/e2e/cp-admin-venue-map.md) |
| **Content & media** | | | | |
| `/admin/news` | ✅ Real (D-199) | Administrator | — | [e2e/cp-admin-news.md](../tests/e2e/cp-admin-news.md) |
| `/admin/media` | ✅ Real (D-199) | Administrator | — | [e2e/cp-admin-media.md](../tests/e2e/cp-admin-media.md) |
| `/admin/archive` | ✅ Real (D-199) | Administrator | — | [e2e/cp-admin-archive.md](../tests/e2e/cp-admin-archive.md) |
| `/admin/banners` | ✅ Real | Administrator | — | [e2e/cp-admin-banners.md](../tests/e2e/cp-admin-banners.md) |
| `/admin/content-blocks` | ✅ Real | Administrator | — | [e2e/cp-admin-content-blocks.md](../tests/e2e/cp-admin-content-blocks.md) |
| **Knowledge & AI** | | | | |
| `/admin/faq` | ✅ Real (D-218) | Administrator | — | [e2e/cp-admin-faq.md](../tests/e2e/cp-admin-faq.md) |
| `/admin/ai/prompts` | ✅ Real (D-176) | Administrator | — | [e2e/cp-admin-ai-prompts.md](../tests/e2e/cp-admin-ai-prompts.md) |
| `/admin/ai/invocations` | ✅ Real (D-176/D-179) | Administrator | — | [e2e/cp-admin-ai-invocations.md](../tests/e2e/cp-admin-ai-invocations.md) |
| **Access control & system** | | | | |
| `/admin/gates` | ✅ Real (D-148) | Administrator | — | [e2e/cp-admin-gates.md](../tests/e2e/cp-admin-gates.md) |
| `/admin/gates/operator` | ✅ Real (D-148) | Gate operator | — | [e2e/cp-admin-gates-operator.md](../tests/e2e/cp-admin-gates-operator.md) |
| `/admin/gates/dashboard` | ✅ Real | Administrator | — | [e2e/cp-admin-gates-dashboard.md](../tests/e2e/cp-admin-gates-dashboard.md) |
| `/admin/configuration` | ✅ Real (D-229) | Administrator | — | [e2e/cp-admin-configuration.md](../tests/e2e/cp-admin-configuration.md) |
| `/admin/operations` | ✅ Real (D-166) | Administrator | — | [e2e/cp-admin-operations.md](../tests/e2e/cp-admin-operations.md) |
| `/admin/operation-log` | ✅ Real | Administrator | [cp/admin-operation-log.md](cp/admin-operation-log.md) | [e2e/cp-admin-operation-log.md](../tests/e2e/cp-admin-operation-log.md) |
| `/admin/logs` | ✅ Real | Administrator | [cp/admin-logs.md](cp/admin-logs.md) | [e2e/cp-admin-logs.md](../tests/e2e/cp-admin-logs.md) |
| `/admin/statistics` | ✅ Real | Administrator | — | [e2e/cp-admin-statistics.md](../tests/e2e/cp-admin-statistics.md) |
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

## Mobile App (Flutter) — DEFERRED

The Flutter app build is on a separate branch; per-page docs + `mobile-*.md` E2E
catalogue files land under `docs/pages/mobile/` and `docs/tests/e2e/` when it
merges to the main line.

| Route | Status | Audience | Doc | Test |
|-------|--------|----------|-----|------|
| _to be filled when the Flutter App build merges_ | | | | |

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

> **Doc-column debt (2026-06-02):** the ~33 event-module + P2–P5 pages now have
> full E2E catalogue files (Test column) but their `docs/pages/cp/{slug}.md`
> reference docs are not yet authored (Doc = "—"). Authoring those reference
> docs is tracked as a follow-up; the E2E files are the executable source of
> truth in the meantime.
