# SIMF — Per-Screen Test-Case Plan (human execution)

| | |
|--|--|
| **What this is** | One fillable test-case sheet **per screen**. A human tester opens one file, runs every case on it, and records the result, the defect, the developer's fix and the re-test — on that same page. |
| **Status** | Living pack (not a controlled `SIMF-XXX-NNN` deliverable) |
| **Authority** | Subordinate to [`SIMF-TST-001-Test-Plan.md`](../SIMF-TST-001-Test-Plan.md). Where the two disagree, `SIMF-TST-001` wins. |
| **Companions** | [`docs/tests/e2e/`](../tests/e2e/README.md) (agent-executable Gherkin catalogue) · [`docs/tests/manual/`](../tests/manual/README.md) (7-day team rehearsal, cross-page journeys, defect process) |
| **Phases** | **1 — Mobile app** · **2 — Control Panel** · **3 — Website** |
| **Created** | 2026-08-01 |

---

## Why this pack exists

The existing test estate is strong but leaves one gap: nothing lets a **person**
pick up a **single screen**, see every scenario for it, run them, and record on
the same page what failed, who fixed it, when, and whether the re-test passed.

| Existing artefact | What it gives | What it does not give |
|---|---|---|
| [`SIMF-TST-001`](../SIMF-TST-001-Test-Plan.md) | The strategy — layers, coverage floor, gates | No individual test cases |
| [`docs/tests/e2e/`](../tests/e2e/README.md) | 184 per-page Gherkin catalogues | Written for a runner, not a person: no tester, no date, no actual-result column, no fix / re-test record |
| [`docs/tests/manual/`](../tests/manual/README.md) | 7-day rehearsal: journeys, permission matrix, run-log, defect log | Per-page rows are one-line checklists; results live in a **separate** run-log |
| The FDS series | Feature-level scenarios `T-01…T-20` | Feature-level, not screen-level; no execution record |

This pack fills exactly that gap. It **does not replace** any of the above and
duplicates none of them — each sheet cites the `E2E-*` ids it derives from.

## What is on a sheet

1. **Identification and run context** — screen name, route, build, environment, device, language, **tester name**, **test date**, reviewer.
2. **Pre-conditions, fixtures and test data.**
3. **Inherited common cases** — the ~50 checks identical on every screen, authored once in [`_COMMON-CASES.md`](_COMMON-CASES.md).
4. **Test cases** in eight sections: **A** render · **B** field validation · **C** functional / business rules · **D** server-side and NCA security · **E** error handling · **F** accessibility and localisation · **G** data integrity and audit · **H** customer acceptance and DoD. Each row carries its expected result and the ids it proves, plus tester-filled **Actual result / Status / Evidence / Notes**.
5. **Execution summary** — totals, pass rate, exit criteria.
6. **Defect, fix and re-test ledger** — defect id, severity, root cause, **developer**, **fix reference**, **fix date and time**, **re-test result**, **re-test date and time**, **re-tested by**, state.
7. **Sign-off** — tester, QA Lead, developer, owner.
8. **Revision history.**

## How to run a sheet

1. Copy the sheet if a previous run is already recorded — **a new build is a new run**; never overwrite an old result.
2. Fill §1: build, environment, device, language, your name, the date.
3. Set up §2. Never paste a real secret anywhere: admin TOTP comes from the `Get-Totp` helper, visitor OTP is read from `SIMF_Identity.AccountCodes` at run time.
4. Run §3's inherited blocks, then §4 A→H. **P0 first.**
5. Record every result with evidence. A `PASS` with no evidence is an opinion.
6. Every `FAIL` gets a §6 ledger row **the same hour**, not at end of day.
7. Run the sheet **twice — Arabic (RTL) and English (LTR)**. SIMF is Arabic-first; a mirrored-layout fault is a functional defect.
8. After a fix, re-test the failed case **and re-run this screen's whole P0 set** — fixes break neighbours. Record both.
9. Complete §5 and §7.

**Status vocabulary:** `PASS` · `FAIL` · `BLOCKED` · `N-A` · `NOT-RUN` · `PROD-SKIP`
**Priority:** `P0` security / data loss / golden path · `P1` important function · `P2` cosmetic or rare
**Severity:** `Critical` · `High` · `Medium` · `Low`

Severity, the defect process and the cleanup register follow
[`SIMF-Manual-Test-Plan-7Day.md`](../tests/manual/SIMF-Manual-Test-Plan-7Day.md)
§8–§10, so both packs speak the same language.

## Reference ids used in the `Refs` column

| Ref | Source | Format |
|---|---|---|
| NCA control | [`docs/security/SIMF-NCA-AppSec-Standard-GapAnalysis-2026-06-20.md`](../security/SIMF-NCA-AppSec-Standard-GapAnalysis-2026-06-20.md) — 298 controls, aligned to ECC-1:2018 / CSCC-1:2019 | `A7-8`, `A1-12`, `A9-7`, `§3-4` |
| Requirement | [`SIMF-SRS-001`](../SIMF-SRS-001-Software-Requirements-Specification.md) v1.1 — 87 FR, 11 NFR, 5 EIR | `FR-107`, `NFR-01` |
| Use case | [`SIMF-UCS-001`](../SIMF-UCS-001-Use-Case-Specifications.md) | `UC-35` |
| Definition of Done | [`SES-001 §14`](../SIMF-SES-001-Software-Engineering-Standards.md) (8 items) + the 7-step gate in [`SIMF-Remediation-QA-Plan.md`](../SIMF-Remediation-QA-Plan.md) | `DoD-SES-3`, `DoD-Gate-4` |
| E2E scenario | [`docs/tests/e2e/`](../tests/e2e/README.md) | `E2E-MOB003-004` |
| Permission | `src/Shared/SIMF.Common/PermissionCatalog.cs` · [`SIMF-Permission-Catalogue.md`](../SIMF-Permission-Catalogue.md) | `Visitors.View` |

> The NCA control scheme in this repo is `A1`–`A11` plus `§1`–`§3`. There is **no**
> `ECC-1-2-3`-style id anywhere in SIMF — do not invent one.

## Adding a sheet

1. Copy [`_TEMPLATE.md`](_TEMPLATE.md) to `{cp|web|mobile}/{screen-slug}.md`.
2. Fill §1 identification and §2 fixtures from the screen's page doc and E2E catalogue file.
3. Author §4 A–H. **Every literal — message text, error code, max length, rate limit, expiry, permission code — is read from the source and cited.** Anything you cannot find is marked `TBC — confirm with dev`, never guessed.
4. Tick the applicable blocks in §3.
5. Add the row to the coverage table below.

**Case ids:** `TC-{SURFACE}-{SCREEN}-{SECTION}{NN}`, where `{SCREEN}` is a short
three-letter screen code declared as the sheet's **Doc id** in §1 — e.g.
`TC-MOB-FPW-D03` (mobile · forgot-password · section D · case 03).

Ids are stable and are never reused or renumbered. A retired case is marked
*Retired*, not deleted, so an old run-log still resolves. These ids deliberately
do not collide with the `TC-P` / `TC-J` / `TC-V` schemes in `docs/tests/manual/`.

Screen codes issued so far — `SPL` splash · `ONB` onboarding · `SGI` sign-in ·
`OTP` verify-otp · `FPW` forgot-password · `RPW` reset-password · `GST` guest-mode ·
`SUF` sign-up-form · `EOT` email-otp · `SUV` sign-up-visitor · `SUI` sign-up-interests ·
`TRM` terms · `RGS` registration-success · `RGT` registration-status.

---

## Coverage — Phase 1: Mobile app

`_to author_` = the sheet is not written yet. Screens removed from the app
(`signUpType`, `savedSessions`, `audienceComments`, `myMeetings`) are out of scope.

### Authentication and onboarding

| # | Screen | Sheet | E2E catalogue |
|---|---|---|---|
| #1 | `splash` | [mobile/splash.md](mobile/splash.md) | [mobile-splash.md](../tests/e2e/mobile-splash.md) |
| #2 | `onboarding` | [mobile/onboarding.md](mobile/onboarding.md) | [mobile-onboarding.md](../tests/e2e/mobile-onboarding.md) |
| #3 | `signIn` | [mobile/sign-in.md](mobile/sign-in.md) | [mobile-sign-in.md](../tests/e2e/mobile-sign-in.md) |
| #3a | `verifyOtp` — sign-in 2FA | [mobile/verify-otp.md](mobile/verify-otp.md) | [mobile-sign-in.md](../tests/e2e/mobile-sign-in.md) |
| #3b | `forgotPassword` | [mobile/forgot-password.md](mobile/forgot-password.md) | [mobile-sign-in.md](../tests/e2e/mobile-sign-in.md) |
| #3c | `resetPassword` | [mobile/reset-password.md](mobile/reset-password.md) | [mobile-sign-in.md](../tests/e2e/mobile-sign-in.md) |
| #12 | `guestMode` | [mobile/guest-mode.md](mobile/guest-mode.md) | [mobile-guest-mode.md](../tests/e2e/mobile-guest-mode.md) |

### Sign-up and registration

| # | Screen | Sheet | E2E catalogue |
|---|---|---|---|
| #5 | `signUpForm` | [mobile/sign-up-form.md](mobile/sign-up-form.md) | [mobile-sign-up-form.md](../tests/e2e/mobile-sign-up-form.md) |
| #6 | `emailOtp` — sign-up verification | [mobile/email-otp.md](mobile/email-otp.md) | [mobile-email-otp.md](../tests/e2e/mobile-email-otp.md) |
| #7 | `signUpVisitor` — profile data | [mobile/sign-up-visitor.md](mobile/sign-up-visitor.md) | [mobile-sign-up-visitor.md](../tests/e2e/mobile-sign-up-visitor.md) |
| #7-01 | `signUpInterests` | [mobile/sign-up-interests.md](mobile/sign-up-interests.md) | [mobile-sign-up-interests.md](../tests/e2e/mobile-sign-up-interests.md) |
| #9 | `terms` | [mobile/terms.md](mobile/terms.md) | [mobile-terms.md](../tests/e2e/mobile-terms.md) |
| #10 | `registrationSuccess` | [mobile/registration-success.md](mobile/registration-success.md) | [mobile-registration-success.md](../tests/e2e/mobile-registration-success.md) |
| #11 | `registrationStatus` | [mobile/registration-status.md](mobile/registration-status.md) | [mobile-registration-status.md](../tests/e2e/mobile-registration-status.md) |

### Badge auth and account

| # | Screen | Sheet | E2E catalogue |
|---|---|---|---|
| — | `badgeSignIn` | _to author_ | [mobile-badge-activation.md](../tests/e2e/mobile-badge-activation.md) |
| — | `badgePassword` | _to author_ | [mobile-badge-activation.md](../tests/e2e/mobile-badge-activation.md) |
| — | `badgeActivation` | _to author_ | [mobile-badge-activation.md](../tests/e2e/mobile-badge-activation.md) |
| — | `biometricStepUp` | _to author_ | [mobile-biometric-step-up.md](../tests/e2e/mobile-biometric-step-up.md) |
| — | `changeEmail` | _to author_ | [mobile-change-email.md](../tests/e2e/mobile-change-email.md) |
| #702 | `myInterests` | _to author_ | [mobile-my-interests.md](../tests/e2e/mobile-my-interests.md) |
| #703 | `myMobile` | _to author_ | [mobile-my-mobile.md](../tests/e2e/mobile-my-mobile.md) |
| — | `identityVerification` | _to author_ | [mobile-identity-verification.md](../tests/e2e/mobile-identity-verification.md) |
| #32 | `badge` | _to author_ | [mobile-badge.md](../tests/e2e/mobile-badge.md) |

### Home and navigation

| # | Screen | Sheet | E2E catalogue |
|---|---|---|---|
| #13 | `home` | _to author_ | [mobile-home.md](../tests/e2e/mobile-home.md) |
| #14 | `myArea` | _to author_ | [mobile-my-area.md](../tests/e2e/mobile-my-area.md) |
| #41 | `more` | _to author_ | [mobile-more.md](../tests/e2e/mobile-more.md) |
| #33 | `notifications` | _to author_ | [mobile-notifications.md](../tests/e2e/mobile-notifications.md) |
| #38 | `accessibility` | _to author_ | [mobile-accessibility.md](../tests/e2e/mobile-accessibility.md) |

### Programme and sessions

| # | Screen | Sheet | E2E catalogue |
|---|---|---|---|
| #16 | `sessions` | _to author_ | [mobile-agenda.md](../tests/e2e/mobile-agenda.md) |
| #17 | `sessionDetail` | _to author_ | [mobile-session-detail.md](../tests/e2e/mobile-session-detail.md) |
| #18 | `mySeat` | _to author_ | [mobile-my-seat.md](../tests/e2e/mobile-my-seat.md) |
| — | `seatPicker` | _to author_ | [mobile-seat-picker.md](../tests/e2e/mobile-seat-picker.md) |
| — | `joinSessionHub` | _to author_ | [mobile-join-hub.md](../tests/e2e/mobile-join-hub.md) |
| #111 | `sessionSummaryList` | _to author_ | [mobile-session-summaries.md](../tests/e2e/mobile-session-summaries.md) |
| #34 | `aiSummary` | _to author_ | [mobile-ai-summary.md](../tests/e2e/mobile-ai-summary.md) |
| #202 | `sessionPresentations` | _to author_ | [mobile-session-presentations.md](../tests/e2e/mobile-session-presentations.md) |
| #113 | `myAreaSessions` | _to author_ | [mobile-my-sessions.md](../tests/e2e/mobile-my-sessions.md) |

### Engagement

| # | Screen | Sheet | E2E catalogue |
|---|---|---|---|
| #26 | `sendQuestion` | _to author_ | [mobile-send-question.md](../tests/e2e/mobile-send-question.md) |
| #40 | `rate` | _to author_ | [mobile-rate.md](../tests/e2e/mobile-rate.md) |
| #25 | `liveBroadcast` | _to author_ | [mobile-live.md](../tests/e2e/mobile-live.md) |
| #36 | `chatbot` | _to author_ | [mobile-chatbot.md](../tests/e2e/mobile-chatbot.md) |
| — | `sessionModerate` | _to author_ | [mobile-session-moderate.md](../tests/e2e/mobile-session-moderate.md) |

### People, exhibition and venue

| # | Screen | Sheet | E2E catalogue |
|---|---|---|---|
| #19 | `speakers` | _to author_ | [mobile-speakers.md](../tests/e2e/mobile-speakers.md) |
| #20 | `speakerProfile` | _to author_ | [mobile-speaker-profile.md](../tests/e2e/mobile-speaker-profile.md) |
| #22 | `booths` | _to author_ | [mobile-booths.md](../tests/e2e/mobile-booths.md) |
| — | `boothMap` | _to author_ | [mobile-booths.md](../tests/e2e/mobile-booths.md) |
| #23 | `sponsors` | _to author_ | [mobile-sponsors.md](../tests/e2e/mobile-sponsors.md) |
| #220 | `exhibitorDetail` | _to author_ | [mobile-exhibitor-detail.md](../tests/e2e/mobile-exhibitor-detail.md) |
| #221 | `sponsorDetail` | _to author_ | [mobile-sponsor-detail.md](../tests/e2e/mobile-sponsor-detail.md) |
| #21 | `delegations` | _to author_ | [mobile-delegations.md](../tests/e2e/mobile-delegations.md) |
| #35 | `meetPeople` | _to author_ | [mobile-meet-people.md](../tests/e2e/mobile-meet-people.md) |
| #15 | `venueMap` | _to author_ | [mobile-venue-map.md](../tests/e2e/mobile-venue-map.md) |

### Meetings and contacts

| # | Screen | Sheet | E2E catalogue |
|---|---|---|---|
| #116 | `meetings` | _to author_ | [mobile-meetings.md](../tests/e2e/mobile-meetings.md) |
| #117 | `meetingConfirm` | _to author_ | [mobile-meeting-confirm.md](../tests/e2e/mobile-meeting-confirm.md) |
| — | `delegationMeetingRequest` sheet | _to author_ | [mobile-delegation-request.md](../tests/e2e/mobile-delegation-request.md) |
| — | `requests` | _to author_ | [mobile-requests.md](../tests/e2e/mobile-requests.md) |
| — | `shareMyContact` | _to author_ | [mobile-my-contacts.md](../tests/e2e/mobile-my-contacts.md) |
| — | `scanContact` | _to author_ | [mobile-my-contacts.md](../tests/e2e/mobile-my-contacts.md) |
| — | `myContacts` | _to author_ | [mobile-my-contacts.md](../tests/e2e/mobile-my-contacts.md) |

### Media and content

| # | Screen | Sheet | E2E catalogue |
|---|---|---|---|
| #29 | `news` | _to author_ | [mobile-news.md](../tests/e2e/mobile-news.md) |
| #29a | `newsArticle` | _to author_ | [mobile-news.md](../tests/e2e/mobile-news.md) |
| #30 | `gallery` | _to author_ | [mobile-gallery.md](../tests/e2e/mobile-gallery.md) |
| #24 | `archive` | _to author_ | [mobile-archive.md](../tests/e2e/mobile-archive.md) |
| #24-01 | `archiveDetail` | _to author_ | [mobile-archive-detail.md](../tests/e2e/mobile-archive-detail.md) |
| #31 | `mediaPartners` | _to author_ | [mobile-media-partners.md](../tests/e2e/mobile-media-partners.md) |
| #37 | `aboutForum` | _to author_ | [mobile-about.md](../tests/e2e/mobile-about.md) |
| #207 | `aboutApp` | _to author_ | [mobile-about-app.md](../tests/e2e/mobile-about-app.md) |
| #200 | `forumGuide` | _to author_ | [mobile-forum-guide.md](../tests/e2e/mobile-forum-guide.md) |
| #201 | `faq` | _to author_ | [mobile-faq.md](../tests/e2e/mobile-faq.md) |
| #203 | `contactUs` | _to author_ | [mobile-contact-us.md](../tests/e2e/mobile-contact-us.md) |

### Staff and exhibitor tools

| # | Screen | Sheet | E2E catalogue |
|---|---|---|---|
| — | `gateScanner` | _to author_ | [mobile-gate-scan.md](../tests/e2e/mobile-gate-scan.md) |
| — | `staffRegisterVisitor` | _to author_ | [mobile-staff-register-visitor.md](../tests/e2e/mobile-staff-register-visitor.md) |
| — | `staffSeating` | _to author_ | [mobile-staff-seating.md](../tests/e2e/mobile-staff-seating.md) |
| — | `myVisitors` | _to author_ | [mobile-my-visitors.md](../tests/e2e/mobile-my-visitors.md) |
| — | `scanVisitor` | _to author_ | [mobile-scan-visitor.md](../tests/e2e/mobile-scan-visitor.md) |

---

## Coverage — Phase 2: Control Panel

_Not started. ~93 screens; see [`docs/pages/PAGE-INDEX.md`](../pages/PAGE-INDEX.md)._

## Coverage — Phase 3: Website

_Not started. 19 pages; see [`docs/pages/PAGE-INDEX.md`](../pages/PAGE-INDEX.md)._

---

_Last reviewed: 2026-08-01 by SIMF Team._
