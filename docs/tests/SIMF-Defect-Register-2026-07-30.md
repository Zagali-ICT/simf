# SIMF — consolidated defect register and fix plan

_Built 2026-07-30. Seven QA reports were read end to end by one agent each, and **every defect they contain was re-checked against the current code** rather than trusted from its status column._

## Why this document exists

The defects were spread across seven documents with three different id schemes, and their status columns had drifted badly. Working from any one of them would have meant re-fixing what is already fixed while missing what is not.

> **STATUS CORRECTED 2026-07-31 — read [§ Status correction (2026-07-31)](#status-correction-2026-07-31) before working from this document.** The "42 STILL-OPEN" figure below was accurate when the register was built on 2026-07-30 and is **wrong now**. Commit `5418ed34` cleared the great majority, `bc30bafd` + `abf87841` cleared the time-storage item, the 2026-07-31 round closed the three the fix-all run had explicitly not delivered, and later the same day the owner answered **Q9** and **Q12**, closing `FR-702-riyadh-georestriction` (built as a notice) and `FR-1203-brand-colour-tokens` (closed by decision, no code). **1 of the 42 remains open** — `#33`, blocked on a delivery date. The counts in the table below are left at their as-built values so the delta is visible; the corrected column is the authority.

| Verified status | Count (2026-07-30) | Count (2026-07-31) | Meaning |
|---|---:|---:|---|
| **STILL-OPEN** | **42** | **2** | The defective code is still there. Both now need an owner decision, not code. |
| OWNER-ACTION | 37 | 37 | Real, but only you can do it (rotate a credential, procure a provider, flip CI). |
| ALREADY-FIXED | 96 | 96 (+39 closed since) | Fixed since the report; the fixing code was read. **Do not re-do.** |
| NOT-A-DEFECT | 12 | 12 (+1: `PAR-P1a`) | On inspection, intended behaviour or a mistaken report. |
| CANNOT-VERIFY | 7 | 7 | Needs a running stack, a device, or provider access. |
| **Total reported** | **194** | **194** | across 7 documents |

**96 of 194 were already fixed when this was written.** That was the single most important number here: the reports are a historical record, not a worklist. Every ALREADY-FIXED row below names the file or commit that closed it, read first-hand. **A further 39 have closed since** — see the status correction below, where each one names the code that was re-read to confirm it.

## Status correction (2026-07-31)

The line "STILL-OPEN | 42 | The defective code is still there. This is the work."
described the tree on **2026-07-30**. It is no longer true, and a register that
overstates its own open count is as harmful as one that understates it: the next
round re-investigates 40 closed items and buries the 2 that actually need a
decision.

**How this correction was made.** Every row below was re-checked **against the
code on this branch**, not against the commit messages that claim to have closed
it and not against the per-item notes under `docs/decisions/fix-all-2026-07-31/`.
Where a claim and the code disagreed, the code won — that is why `PAR-P1a` is
recorded as not-a-defect rather than fixed, why `sev-1-1-domain-purity` and
`sev-1-6-service-placement` are recorded as *documentation* corrections with the
code state unchanged, and why the three security items are split into a closed
code half and an open owner half.

**Row arithmetic.** 42 STILL-OPEN rows → **2 open**, 39 fixed, 1 not-a-defect.
Three refs appear twice in the register (`OA-D1`, `OA-D5`, `OA-D6` were each
reported by two source documents), so the 42 rows are 39 distinct refs and the
1 that remains open is 1 distinct ref. _(Was "3 open, 38 fixed" earlier the same
day; `FR-702-riyadh-georestriction` moved across when the owner answered Q9 —
see the FR-702 subsection below.)_

### Closed by `5418ed34` (the fix-all run), re-verified 2026-07-31

| Ref | Verified against |
|---|---|
| `DEF-SEC-001-txt` · `no-literal-secrets-txt` · `SEC-NOTES` | `git ls-files txt.txt myComment.txt` returns nothing (both untracked); `.gitignore:77,85` lists both with the rationale. **Code half only** — rotating the credential the file carried is still `DEF-SEC-001-rotate` / `committed-secrets-rotation` under OWNER-ACTION, and history is not purged. |
| `SEC-SMOKE` | `tools/smoke/smoke.sh:17-20,34` reads `SIMF_SMOKE_EMAIL` / `_PASSWORD` / `_TOTP_SECRET` and fails fast if unset; no literal remains. Rotation + history purge stay OWNER-ACTION. |
| `#2` | `SignInService.cs:200-223` — a `SignInAudience.Cp` password step under `RequireControlPanelTwoFactorEnrolment` mints a `SecondFactorKind.TotpEnrolment` ticket (15-minute lifetime, `EnrolmentTicketLifetime:52`) and returns **no tokens**; `StartTwoFactorEnrolmentAsync:418` completes it. `ControlPanelTwoFactorEnrolmentTests` covers it. See the residual closed this round, below. |
| `#2b` | `Program.cs:476-481` — production boot fails when `SuperAdmin:TotpSecret` is blank, alongside the five existing guards. |
| `#2c` | `JwtTokenService.cs:51-58` — RFC 8176 `amr`, `"mfa"` when the second factor completed, `"pwd"` otherwise. |
| `#2d` | `AdminAccountService.cs:670-674` — `SetTwoFactorEnabledAsync(user, true)` for `UserType.Admin`, **conditional on `RequireControlPanelTwoFactorEnrolment`**. The condition is the `#2` dependency expressed in code: with the enrolment path off, forcing the flag would lock every new admin out at creation. |
| `exit-gate-no-open-high` | The sum of `#2` / `#2b` / `#2c` / `#2d` above; the suite now runs the shipping posture (see below). |
| `#10-phase4` | `BadgeAuth.cs:112-127` — `EnglishName`, `ArabicName`, `NationalityCode`, `InterestIds` on `BadgeActivationCompleteRequest`; `BadgeSelfClaimProfileTests` + E2E `api-badge-self-claim-profile.md`. |
| `OP-SUPERADMIN-SEED` | `IdentitySeeder.cs:598-621` — throws `InvalidOperationException` naming the policy reasons instead of returning null and booting with no super-admin. |
| `#29` | `session_detail_body.dart:83` returns `_workshopBody()` on `SessionType.workshop`; E2E-MOB017-032. |
| `#40-residual` | `splash_screen.dart:84-86` renders `profile.eventDateRange(isArabic)` with `splashEventLine` only as the fallback; `src/Website/SIMF.Web/wwwroot/speakers.html` is deleted. |
| `OA-D1` (×2 rows) | `greeting_header.dart` — the `split(' ').first` is gone; the full trimmed name is greeted, with the existing `maxLines: 1` + ellipsis handling length. |
| `FR-1103-movement-dwell` | `DevicePositionPing` entity + `MovementTrackingEndpoints.cs` (`POST /app/movement/pings`, `GET /admin/movement/dwell`, `GET /admin/movement/route/{userId}`) + `MovementTrackingService`; E2E `api-movement-tracking.md`. The D6 metric list stays OWNER-ACTION. |
| `FR-803-80pct-push` | `NotificationKind.MatchRecommended = 60` (additive) + the push worker; E2E `api-match-recommendation-push.md`. |
| `FR-903-not-attended-reminder` | `NotificationKind.SessionNotAttended = 59` (additive) + the worker; E2E `api-session-not-attended-reminder.md`. |
| `OA-D5` (×2 rows) | `SpeakerMeetingRequestsExcelEndpoints.cs:45-48` — `CheckedInAt` + `CheckedInBy` columns; E2E `api-meeting-checkin-export.md`. |
| `OA-D6` (×2 rows) | `PublicSessionEndpoints.cs:33,69` — `Guid? CategoryId` threaded into `service.ListAsync(day, req.CategoryId, ct)`; E2E `api-programme-category-filter.md`. |
| `sms-whatsapp-channels` | `INotificationChannel` + `InAppNotificationChannel` + `EmailNotificationChannel` behind `NotificationDispatcher`; E2E `api-notification-channels.md`. **The seam is the deliverable** — the SMS / WhatsApp gateways themselves stay OWNER-ACTION (procurement). |
| `#16` | Exactly **one** `Colors.white` remains under `src/Mobile/simf_app/lib`, at `app/theme/tokens.dart:267` where the token is defined. That is the sweep's target state, not a residue. |
| `PAR-B4` | `booth_company_header.dart:34-36` — the exhibitor line is dropped when `exhibitor.trim() == name.trim()`. |
| `PAR-D3` | `session_header_card.dart:62` builds the pill from `detail.localizedCategory(isArabic)`; E2E-MOB017-033. |
| `PAR-P4a` | `session_speaker_card.dart:104` renders `Icons.star_rounded` in gold beside المضيف for a `Host` role; E2E-MOB017-034. |
| `STALE-GOLDEN-ARTIFACTS` | `git ls-files src/Mobile/simf_app/test/golden/failures/` returns **0**; `.gitignore:44` covers the directory. |
| `itokenissuer-extraction` | `SIMF.Application/IdentityAccess/ITokenIssuer.cs` + `TokenIssuer.cs` exist and `SignInService` consumes them. |
| `QA-LIVE-001` | `wwwroot/favicon.png` exists and `App.razor:31` declares `<link rel="icon" type="image/png" href="@Assets["favicon.png"]" />`. |
| `cp-stub-modules` | `CpNavigation.cs:191` — the sole `IsStub` entry now carries `RequiredPermission: PermissionCatalog.Sessions.View`, and `ModulePlaceholder.razor` carries the matching `[RequirePermission]`. The register's headline count (8 of 22) was already stale; the live defect was the **null gate**, and that is what closed. Per owner Q4 no Live Sessions console was built, so the entry is still a stub. |
| `catalogue-count-drift` | The Round-1 charter no longer states a catalogue size; the counts live in `docs/tests/e2e/README.md` and are machine-checked by `E2eCatalogueIntegrityTests.The_index_roll_up_matches_the_catalogue_it_describes`. |
| `FR-1203-markdown-render` | Option (b): the "markdown allowed" claim is gone from `ContentBlock`'s XML doc and `ContentBlockPlainTextContractTests` pins the plain-text behaviour. **No renderer was built** — that was the decision, not an omission. |

### Closed by `bc30bafd` + `abf87841`

| Ref | Verified against |
|---|---|
| `#8` | `src/Shared/SIMF.Common/SimfClock.cs` is the single Saudi wall-clock seam (`SimfClock.Now`, `timeProvider.SimfNow()`); `BaseEntity.CreatedAt` is a `DateTime` defaulted to `SimfClock.Now`; and a `GetUtcNow()` search across `src/Backend/**/*.cs` (build output excluded) returns **0** — the 318 call sites the register recorded are all converted. `abf87841` carried the existing rows across. |

### Closed by this round (2026-07-31)

These are the three the fix-all run explicitly did **not** deliver.

| Ref | What closed it | Verified against |
|---|---|---|
| `geofence-self-checkin` | **Owner decision: the GATE SCAN establishes a session arrival, not GPS.** The whole server chain already existed (`GateOperatorService.RecordGateDoorScanAsync` opens and closes the `HallAttendance` row for the session live in that hall; the CP reports it on `/admin/hall-arrivals` and `/admin/gates/operator`), so the app only has to read it back. `SessionArrivalAction` is now a **read-only** status card fed by `GET /app/sessions/{id}/attendance`, and is composed into the screen — the previous round built the widget and never rendered it, which it flagged as a follow-up. | `session_arrival_action.dart` (three states: recorded arrival + departure line, "show your badge at the hall door", failed-read + retry; no write path); `session_detail_screen.dart` `_showArrivalStatus` + the `hallAttendanceStatusProvider` invalidate in `_load()`; `session_arrival_action_test.dart` (6 widget + 3 decode cases, its fake repository **throwing** from `recordArrival` / `recordDeparture`). E2E-MOB017-035..040. |
| `accessibility-server-sync` | **The server half shipped.** The app half landed on 2026-07-30 with nothing to call — both sync directions swallow their failures by contract, so its tests passed while the feature did nothing. `GET`/`PUT /api/v1/app/account/preferences` now stores the five choices on the account. | `AccountPreferencesEndpoints.cs` (`RequireApprovedAccount`, own `sub`, no admin permission); `AccountPreferences` + `UpdateAccountPreferencesRequest`; `AccountPreferencesService` (five-column `AsNoTracking` projection, full-replace write, bilingual 400 on an unknown `textSize`); five additive `UserProfile` columns with two load-bearing `HasSentinel` calls; `AccountPreferencesTests.cs` (8 facts). E2E-ACP-001..013 + E2E-MOB038-012. |
| `#2` **residual** — the `SimfApiFactory` 2FA test posture | `#2` shipped the enrolment-first branch, but `SimfApiFactory` pinned `IdentityLifecycle__RequireControlPanelTwoFactorEnrolment` to **`false`** for the whole assembly, because ~150 admin fixtures read `Tokens.AccessToken` off a Cp-audience password sign-in. So the general suite exercised the **pre-fix** path and only one dedicated test class ever ran the shipping posture. That is a green suite proving the wrong thing. | `SimfApiFactory.cs` now pins the value to **`true`** (the production default), and the fixtures go through `AuthFlow.SignInControlPanelAsync`, which enrols the fixture admin and completes a real TOTP step. `ControlPanelTwoFactorApiFactory` is kept as the explicit statement that `ControlPanelTwoFactorEnrolmentTests` depends on the gate being on. |

### Closed by an owner answer, later on 2026-07-31

| Ref | What closed it | Verified against |
|---|---|---|
| `FR-702-riyadh-georestriction` | **Q9 answered: there is no restriction.** The register's own note said the requirement was worth re-confirming because the feed is public YouTube, which makes a server-side region gate largely unenforceable. Put to the owner, the answer was verbatim *"No restriction, this is only notification and be added to session."* **What shipped (2026-07-31):** an optional per-session bilingual free-text **live notice** — `Session.LiveNotice` / `LiveNoticeArabic` (`nvarchar(512)`, nullable; wire `liveNotice` / `liveNoticeArabic`, appended per D-219) — authored on the `/admin/sessions` broadcast block and displayed **with** the stream: above the player on app screen #25 and under the at-a-glance card on the Website `/sessions/{id}`. Blank in both languages shows nothing; clearing the CP boxes stores `null` and removes the banner. **No geo-detection, no IP lookup, no `Region` check and no player gate was built, and none will be** — the item is closed as re-scoped, not as deferred. | `Session.cs` + `SessionConfiguration.cs` (the two 512-capped columns); `AdminSessionService` (create/update/`ValidateTextLengths`, bilingual 400 `SESSION_INVALID`) + `ProgrammeSessionService` (read-only projection, no filter); `SessionsAddEdit.razor` + `Strings(.ar).resx` `Admin.Sessions.Field.LiveNotice*`; `LiveNoticeBanner` (`live_content.dart`) + `LiveSession.localizedNotice`; `SessionDetail.razor` `.ln-glance__notice`. `tests/SIMF.Api.Tests/SessionLiveNoticeTests.cs` — 9 facts, including `A_live_notice_does_not_withhold_the_live_stream` (an anonymous caller with a notice set still receives `liveStreamUrl` in full). Spec corrected at SIMF-FDS-007 §5.1 with the superseded wording kept and dated; decision **D-815**. E2E: SES-054..056, MOB025-026..028, WSDT-014..016. |

| `FR-1203-brand-colour-tokens` | **Q12 answered: `theme.tokens.css` is the single source of truth.** Owner decision, verbatim *"Accept theme.tokens.css as the source of truth"*. Closed by decision, with **no code** — and deliberately so. The project's own CSS rules already mandate that file as the sole home of every colour, with zero hardcoded hex anywhere; a Control Panel brand-colour editor would have written colours into `SystemSettings` that then compete with the token file, giving the same value two owners. That is the precise failure the rule exists to prevent, so building the surface would have introduced the defect rather than closed it. Re-verified 2026-07-31: no `brandColour` / `brandColor` / `brand-colour` surface exists in the Control Panel, and `SiteSettingsPage.razor` remains scoped to the bilingual registration welcome message. |

### Reclassified, not fixed

| Ref | Verified status |
|---|---|
| `PAR-P1a` | **NOT-A-DEFECT.** `media_coverage_tabs.dart:90` still sets `maxLines: 1`, and that is correct: frame `1049:12629` was re-read on 2026-07-30 and shows a **one-line** tab label. The item allowed re-confirming against the frame first; the frame said no change. |
| `sev-1-1-domain-purity` | **Documentation defect closed; the code state is unchanged by decision (owner Q15).** `SimfUser : IdentityUser<Guid>` and `SimfRole : IdentityRole<Guid>` are still in `SIMF.Domain`, and `SIMF.Domain.csproj` still references `Microsoft.Extensions.Identity.Stores`. What was actually wrong was the refactor plan asserting "Arch SEV-1.1 fully closed"; that is corrected, and `tests/SIMF.Api.Tests/DomainPurityTests.cs` pins the real state with **inverted** facts (`Domain_SimfUser_still_derives_from_AspNet_Identity`) so the day someone does the POCO split, the test fails and tells them to flip it. The architectural work itself is still outstanding. |
| `sev-1-6-service-placement` | **Deferral re-confirmed in writing (owner Q13).** `AdminAccountService` is still under `SIMF.Infrastructure/Identity/` (3,452 lines across six partials). The defect was a deferral that lived only in a table cell and was being inherited by silence; the reasoning is now recorded where the next round will read it. The move itself is still outstanding, past the event. |

### Genuinely still open (1)

One item remains, and it is blocked on a date from the owner rather than on
engineering time.

_(This section read "still open (3)" earlier on 2026-07-31. The owner answered
both blocking questions the same day: **Q9** closed
`FR-702-riyadh-georestriction` (built as a notice) and **Q12** closed
`FR-1203-brand-colour-tokens` (closed by decision, no code). Both moved to
[§ Closed by an owner answer](#closed-by-an-owner-answer-later-on-2026-07-31).)_

| Ref | Sev | Where it still lives (re-verified 2026-07-31) | Blocked on |
|---|---|---|---|
| `#33` — Control Panel user manual not delivered | high | `docs/manuals/Admin-Manual.md` is still **1,118 lines with 14 `_(planned)_` markers**, unchanged: the contents list marks Registration requests (line 32), Attendees (33), Roles & permissions (35), Halls & seating (39), Speakers (40) and Bookings (42) as unwritten, all of Exhibition / Engagement / Knowledge & AI / Content (43–46) likewise, and chapter stubs remain at 204, 446 and 585. | **Q5** — the stated due date (19-07-2026) is long past and the real one is unconfirmed. |

### What did NOT change

The **37 OWNER-ACTION** rows, the **7 CANNOT-VERIFY** rows and the **12
NOT-A-DEFECT** rows are untouched by this correction — no code closes any of
them. In particular the three security items above closed only their *code*
half: the credentials they exposed are still in git history and still unrotated
(`DEF-SEC-001-rotate`, `committed-secrets-rotation`, `SEC-APPSET`, `SEC-AIKEY`),
and `PROC-CI` / `entry-gate-ci-tests` still leave the .NET test gate switched off
in the pipeline, so none of the green suites cited above is enforced by CI.

## Sequencing correction found while executing Wave 2 (2026-07-30)

**`#2d` must not ship before `#2`. On its own it is worse than the defect it fixes.**

`#2d` reads "force `TwoFactorEnabled` for Admin-typed users". Doing only that locks
every newly created admin out of the system permanently, at creation:

- `SignInService.cs:175` issues tokens on the password alone **only** when
  `!user.TwoFactorEnabled`. Setting the flag closes that path — correct, and the
  point of `#2`.
- But `SignInService.cs:202` then picks the factor kind as
  `!string.IsNullOrEmpty(authenticatorKey) || roles.Count > 0 ? Totp : EmailOtp`.
  A new admin **has a role** and **has no enrolled authenticator key**, so it
  selects TOTP.
- The account is therefore challenged for a TOTP code against a secret that does
  not exist. There is no enrolment path in the sign-in flow to create one. The
  admin cannot sign in, and no amount of retrying helps.

So the order is fixed, not a preference:

1. **`#2` first** — a CP enrolment challenge: when an admin with `TwoFactorEnabled`
   and no authenticator key reaches the second-factor step, return an *enrolment*
   ticket instead of a verification one, and have the CP consume the existing
   `TotpSetupEndpoint` / `TotpConfirmEndpoint` to enrol before completing sign-in.
2. **`#2d` after** — only once an unenrolled admin has a way through.

`#2b` and `#2c` were deliberately shipped ahead of both because neither can lock
anyone out: `#2b` is a boot-time configuration guard, and `#2c` only adds a claim.

## THE PLAN — what I fix, in this order

Ordered by user impact, not by effort. Waves are sequenced so nothing in a later wave depends on an earlier one being incomplete.

> **Executed. Kept as the record of what was planned and why.** Every wave below
> has since run: 41 of the 42 rows are closed and 1 remains open (`#33`, the
> Control Panel user manual), waiting on a delivery date from the owner. The Wave-4
> `FR-702` row below is one of the closed ones, and it did **not** close the way
> it was planned: the plan proposed gating the live URL on the caller's resolved
> `Region`, and the owner instead removed the restriction from the requirement
> altogether (2026-07-31, D-815). Read that row as history, not as work. Per-row
> status and the evidence for it are in
> [§ Status correction (2026-07-31)](#status-correction-2026-07-31).

### Wave 1 — security and crashes — 2 item(s) (2xS)

_Anything that leaks a credential or takes a surface down. Nothing else ships before these._

| Ref | Surface | Effort | Defect | The fix |
|---|---|---|---|---|
| `DEF-SEC-001-txt` | security | S | Plaintext PRODUCTION super-admin credential still committed and tracked in repo scratch file txt.txt | Delete txt.txt from the working tree AND from git history (filter-repo/BFG), add the path to .gitignore, and add a fingerprint entry for these values to CommittedSecretsTests.ForbiddenCredentials so the guard actually covers them. Credential rotation itself is owner action (tracked separately). |
| `SEC-SMOKE` | security | S | tools/smoke/smoke.sh embeds a super-admin email, password and TOTP secret in plaintext | Delete the three literals and read them from environment variables (SIMF_SMOKE_EMAIL / _PASSWORD / _TOTP_SECRET), or delete the script. Then the owner must rotate the super-admin password AND re-seed the TOTP secret, and purge both from git history — a code-side deletion alone leaves the working credential in every clone. |

### Wave 2 — high severity — 5 item(s) (3xL, 2xS)

_Broken behaviour a user or admin will hit on a normal path._

| Ref | Surface | Effort | Defect | The fix |
|---|---|---|---|---|
| `#2` | api | L | Control-Panel sign-in mints a full token on the password alone when 2FA is not enrolled | In SignInService, before the `!user.TwoFactorEnabled` fast path, branch on `request.Audience == SignInAudience.Cp`: instead of issuing tokens, mint a SecondFactorKind enrolment ticket and return a mandatory-2FA-enrolment challenge, and add a CP enrolment page that consumes TotpSetupEndpoint/TotpConfirmEndpoint. Roll out enrolment-first so … |
| `#33` | docs | L | Control Panel user manual not delivered (owner due date 19-07-2026, already past) | Write the 14 _(planned)_ chapters against docs/pages/PAGE-INDEX.md (each CP module gets purpose, common tasks, control-by-control notes, troubleshooting), and get the owner to confirm the real deadline — the stated one is long past. |
| `#2b` | security | S | No production boot guard requiring SuperAdmin:TotpSecret — a default prod deploy leaves the bootstrap super-admin single-factor | Add a guard next to the existing TempPassword one in Program.cs: `if (app.Environment.IsProduction() && string.IsNullOrWhiteSpace(superAdminOptions.TotpSecret)) throw new InvalidOperationException(...)` — superAdminOptions is already bound at Program.cs:423-425. |
| `exit-gate-no-open-high` | security | L | The charter's exit criterion "no open high-severity defect" is not met | Held-item #2 option C: boot-fail-fast in Production when SuperAdmin:TotpSecret is empty, force TwoFactorEnabled on admin creation, add a CP forced-enrolment challenge for the Cp audience, and emit an mfa claim from JwtTokenService so CP policies can tell a TOTP-completed token from a password-only one. Ship enrolment-first so no admin is  … |
| `no-literal-secrets-txt` | security | S | The same §4.2 "no literal secrets, ever" hard rule is still broken by a git-tracked repo-root scratch file | git rm txt.txt (or move it to the git-ignored deploy/ overlay), purge it from history if the credential was ever real, and rotate the account it names. |

### Wave 3 — medium — 10 item(s) (2xL, 3xM, 5xS)

_Wrong or missing behaviour with a workaround._

| Ref | Surface | Effort | Defect | The fix |
|---|---|---|---|---|
| `#10-phase4` | api | M | Badge self-claim does not capture the claimer's profile data (name / nationality / interests) | Extend BadgeActivationCompleteRequest with the profile fields, fill the placeholder UserProfile inside CompleteActivationAsync (still no Identity schema change), and add the capture step to badge_activation_screen.dart before it routes to signIn. |
| `#8` | api | L | All times must be stored as Saudi wall-clock, no UTC — storage is still UTC | Owner must first answer the doc's 3 scope questions (scope, rename, +3h back-fill). Then: replace timeProvider.GetUtcNow() with one Saudi clock seam (+03:00) in the audit interceptor, BaseEntity defaults and all seven workers, and run a one-time +3h shift of existing rows — otherwise every stored instant renders 3 hours early after the sw … |
| `OP-SUPERADMIN-SEED` | api | S | Super-admin seed fails silently when the configured temp password violates the password policy, leaving a Control Panel nobody can sign into | In CreateSuperAdminAsync, throw instead of returning null when result.Succeeded is false and the environment is Production (mirroring the existing Program.cs guards at 440-460 for Swagger creds and the AI prompt-hash secret). Include result.Errors in the message so the operator sees which policy rule the password broke. |
| `#29` | app | M | Workshop management in CP + app must show workshop title and time only | In session_detail_body.dart, branch on SessionType.workshop and render only the title + time block (suppressing speakers, seat/join and live/summary actions), then re-lock the affected goldens. Separately get the owner's reuse-vs-dedicated-surface ruling for the CP. |
| `#40-residual` | app | S | Two hardcoded "23–25 November 2026" strings survive the dynamic-dates conversion | Splash: the org profile is warmed at splash already, so render OrgProfile.eventDateRange(isArabic) with the literal only as a fallback. speakers.html: delete the file (the Blazor /speakers page replaced it) rather than editing the date. |
| `OA-D1` | app | S | App home greeting truncates Arabic compound given names ("عبد الله" renders as "عبد") | Either drop the split and greet the full localized name, or special-case the compound-given-name constructions (عبد …, أبو …, عبد الـ…) before taking the first token. A durable third option is capturing an explicit GivenName at sign-up. The owner must pick one -- the code change itself is a few lines in greeting_header.dart plus a golden  … |
| `OA-D1` | app | S | App home greeting truncates Arabic compound given names (عبد الله renders as عبد) | Cheapest correct fix is to drop the split and greet the full trimmed name (the Text already has maxLines:1 + TextOverflow.ellipsis, so long names degrade gracefully). If the owner insists on first-name-only, the robust version is a captured GivenName field at sign-up rather than string surgery. Owner picks; the code change is a one-liner  … |
| `geofence-self-checkin` | app | L | Attendee-facing geofence self-check-in: backend built, no app screen | Add an arrival/departure method to a sessions repository and an "I'm here" action on features/sessions/widgets/session_detail_body.dart that posts the device position and renders the returned HallAttendanceStatus — gated on the owner's G-OI-2 venue-boundary answer, since the hall geofence triple must be seeded first. |
| `#2c` | security | S | JWT carries no amr/mfa claim, so CP authorization policies cannot distinguish a TOTP-completed token from a password-only one | Thread a `secondFactorCompleted` flag into IssueTokensAsync and emit an `amr` claim (`pwd` vs `mfa`/`otp`); then add a `RequireMfa` authorization policy the sensitive CP endpoints can opt into. |
| `#2d` | security | M | AdminAccountService account creation does not force TwoFactorEnabled for Admin-typed users | In CreateAccountAsync, when `userType == UserType.Admin`, mark the account as requiring 2FA enrolment (a forced-enrolment flag or `SetTwoFactorEnabledAsync(user, true)` paired with the #2 enrolment challenge) so a CP-provisioned admin cannot end up permanently single-factor. |

### Wave 4 — low — 25 item(s) (4xL, 12xM, 9xS)

_Polish, reporting gaps, and cosmetic parity._

| Ref | Surface | Effort | Defect | The fix |
|---|---|---|---|---|
| `FR-1103-movement-dwell` | api | L | Movement / dwell / route tracking from GPS has no capture path or data source | Needs a periodic device-position ping table (userId, hallId/sessionId, capturedAt, lat/lon) plus an aggregation for dwell-per-hall and a route projection. In practice still blocked on the G-OI-2 venue-boundary decision and the D6 statistics metric list. |
| `FR-702-riyadh-georestriction` | api | M | Live-stream Riyadh-region restriction has no geo-restriction logic | Gate the returned live URL server-side on the caller's resolved Region (the lookup already exists at src/Backend/SIMF.Domain/Regions/Region.cs), or enforce it at the provider/CDN. Worth re-confirming the requirement first — the feed is now public YouTube, which makes the restriction largely unenforceable. |
| `FR-803-80pct-push` | api | M | No >=80% match-score threshold and no auto-recommendation push | Add a normalised score threshold to RecommendationService, an additive NotificationKind (e.g. MatchRecommended), and a poll worker alongside SessionReminderWorker that dispatches once per (caller, candidate) pair with a dedup stamp. |
| `FR-903-not-attended-reminder` | api | M | FR-903 'session started but you have not attended' reminder still has no kind or sender | Add an additive NotificationKind (e.g. SessionNotAttended) and have ReservationNoShowReleaseWorker, or a sibling worker firing at Start+N minutes, dispatch it to holders of an active reservation with no HallAttendance row. |
| `OA-D5` | api | M | No meeting hall-check-in report or export; the CheckedInAt/CheckedInByUserId stamps are written but never exported | Add `CheckedInAt` and the check-in operator to the SpeakerMeetingRequests export column list (and add a matching AdminGridExportEndpoint for delegation meeting requests, which has none), reusing the existing AdminGridExportEndpoint/GridExcelColumn pattern plus a new PermissionCatalog.*.Export code for the delegation grid. |
| `OA-D5` | api | M | Meeting hall check-in stamps are recorded but never surfaced in any report or export | Append CheckedInAt and CheckedInByName to AdminSpeakerMeetingRequestRow (append-only, D-219 wire rule), project them in the service's list query, add the two GridExcelColumn entries to SpeakerMeetingRequestsExcelEndpoints._columns, and mirror for the delegation row. |
| `OA-D6` | api | M | Public programme endpoint supports only ?day=; no server-side theme/category filter | Add an optional `ThemeId`/`CategoryId` (Guid?) to ListProgrammeSessionsRequest and thread it into IProgrammeSessionService.ListAsync as an extra Where clause; the 45s output cache already varies by query key so each filter combination keys separately. Only worth doing if the owner actually wants a server-side track filter -- the app curre … |
| `OA-D6` | api | M | Public programme endpoint filters by ?day= only; no server-side theme/category filter | Add an optional Guid? CategoryId to ListProgrammeSessionsRequest and thread it into IProgrammeSessionService.ListAsync as a second nullable filter. Note the endpoint uses CacheOutput("PublicRead") which already varies by all query keys, so a new query parameter keeps its own cache entry automatically. |
| `sms-whatsapp-channels` | api | L | Notification multi-channel dispatch: no SMS or WhatsApp channel behind the abstraction | Introduce an INotificationChannel abstraction with in-app + email as the first two implementations, then add gateway-backed SMS/WhatsApp channels once a provider is procured. |
| `#16` | app | M | Flutter clean-code / tokenisation sweep across ~39 features | Sweep the four remaining feature files onto SimfTokens.surface, then decide whether the shared app/widgets layer (simf_page_shell, more_drawer, dialogs) is in or out of the sweep's scope and record that in the checklist. |
| `PAR-B4` | app | S | Booth card subtitle duplicates the company name | Either give the seeded booths a distinct trading name vs legal exhibitor name, or add a one-line guard in booth_company_header.dart so the fullName Text is skipped when fullName.trim() == name.trim(). |
| `PAR-D3` | app | S | Session detail is missing the session-type / category pill | Render a small pill under the header-card title bound to detail.localizedCategory(isArabic) (and optionally the hall name), shown only when non-null; update the stale doc comment at session_detail_screen.dart:38 in the same change. |
| `PAR-P1a` | app | S | Media-partners active tab label renders 1 line vs Figma's 2 | Raise the pill label to maxLines: 2 with a slightly smaller line-height (the pill is already 48 high, so two 12px lines fit), or re-confirm against frame 1049:12629 before changing anything. |
| `PAR-P4a` | app | S | Host STAR glyph not rendered on the speakers list | On the session-detail speaker card, swap the 'المضيف' text marker for the Figma star glyph (or add the star beside it) when speaker.role == SessionSpeakerRole.host; alternatively correct the stale claim in speaker_list_card.dart:14-17. |
| `STALE-GOLDEN-ARTIFACTS` | app | S | 48 stale golden-failure PNGs committed under test/golden/failures/ | `git rm -r --cached src/Mobile/simf_app/test/golden/failures/` and add the path to .gitignore — the directory is generated output from a failing golden run, never an input. |
| `accessibility-server-sync` | app | M | Accessibility settings are local prefs only, not server-synced | Add the five flags (font scale, high contrast, reduce motion, screen reader, captions) to the user-profile preferences DTO; have AccessibilityController write through on change and hydrate at sign-in, keeping the local prefs as the offline cache. |
| `itokenissuer-extraction` | build-ci | M | Architecture: ITokenIssuer never extracted — token minting duplicated across auth paths | Extract an ITokenIssuer in Application with a single implementation, and route the device-key ceremony, password sign-in and badge-QR sign-in through it so the claim set and the D-443 lifetime caps cannot drift between entry points. |
| `sev-1-1-domain-purity` | build-ci | L | Architecture SEV-1.1: Domain still depends on ASP.NET Core Identity (SimfUser : IdentityUser<Guid>) while the refactor plan claims it closed | Either re-do the D-093 POCO split on this branch or correct the refactor plan; one of the two must change. The minimum honest step is a DomainPurityTests fixture asserting the SIMF.Domain assembly references no Identity type, so the claim cannot silently regress again. |
| `sev-1-6-service-placement` | build-ci | L | Architecture SEV-1.6: AdminAccountService still in Infrastructure rather than Application | Extract IRoleManager / IUserRoleStore-shaped abstractions in Application, then move AdminAccountService behind them. The plan defers this past the event, which is defensible — but the deferral should be re-confirmed rather than quietly forgotten. |
| `FR-1203-brand-colour-tokens` | cp | M | CMS: brand-colour token editing from the Control Panel deferred | Either close the item by accepting theme.tokens.css as the single source of truth (which the project's own CSS rules mandate), or add a SystemSettings-backed brand-colour section that emits CSS custom-property overrides at layout render. |
| `QA-LIVE-001` | cp | S | GET /favicon.ico returns 404 on every Control Panel page | Drop a favicon.ico (or the existing SIMF logo as .png/.svg) into src/ControlPanel/SIMF.ControlPanel/wwwroot/ and add <link rel="icon" href="@Assets["favicon.ico"]" /> to the App.razor head alongside the stylesheet links. |
| `cp-stub-modules` | cp | M | 8 of the 22 original D-134 CP stub modules remain ModulePlaceholder stubs | Either build a Live Sessions console over the existing Session.LiveStreamUrl / LiveSignLanguageUrl columns and the SessionLiveHall data, or remove the nav entry — an IsStub item has RequiredPermission null, so it is visible to every signed-in admin. |
| `SEC-NOTES` | docs | S | txt.txt and myComment.txt tracked plaintext working notes | `git rm --cached txt.txt myComment.txt` and add both to .gitignore, or move their surviving content into docs/. Confirm with the owner first — myComment.txt is an owner-authored fix-list that other work still references. |
| `catalogue-count-drift` | docs | S | The charter's "164-file / 2,142-scenario" catalogue size is stale | Re-derive both counts from the catalogue (or have build_testbook.py emit them) the next time the workbook is regenerated. |
| `FR-1203-markdown-render` | website | S | Public content ships raw markdown — no renderer on Website or app | Either render server-side through a sanitizing markdown pipeline before emitting the block, or drop the 'markdown allowed' claim from ContentBlock's XML doc so the contract matches behaviour. Do not render unsanitised HTML from an admin-editable field. |

## Yours, not mine — OWNER-ACTION

These are real and several are critical, but no code change discharges them.

| Ref | Sev | Surface | What it needs from you |
|---|---|---|---|
| `SEC-TLS` | critical | app | Mobile app installs a global TLS trust-all, disabling MITM protection app-wide |
| `DEF-SEC-001-rotate` | critical | security | Already-deployed demo / super-admin credentials must be rotated |
| `committed-secrets-rotation` | critical | security | Committed secrets in appsettings (super-admin temp pw, TOTP seed, Jwt:SigningKey, ID-doc key) need rotate + scrub |
| `nca-mod-clearance` | critical | security | NCA / MoD security clearance — hard go-live gate |
| `app-store-publication` | high | app | App-store publication pending |
| `PROC-CI` | high | build-ci | CI "Run all tests" task is still enabled:false — nothing gates anything in the pipeline |
| `entry-gate-ci-tests` | high | build-ci | The charter's entry criterion "unit + integration + Flutter suites green" is unenforceable — the .NET test gate is switched off in CI |
| `#31` | high | cp | Forum programme content not fed |
| `#5` | high | cp | Halls with no HallSeatLayout have no seat picker — layouts not configured for all seat-halls |
| `uat` | high | docs | UAT not performed |
| `OA-D4` | high | infra | AI answers are the Echo stub under the committed configuration ("does the AI work" is false out of the box) |
| `prod-secrets-provisioning` | high | infra | Production secrets not provisioned (machine-scope env vars on the server) |
| `SEC-AIKEY` | high | security | Committed AI API key (D-756) — no longer present in the tree; rotation unverifiable |
| `SEC-APPSET` | high | security | Committed SMTP and demo-seed passwords in appsettings.Development.json — blanked in the tree, still in history and unrotated |
| `OA-D4` | medium | api | AI assistance returns the Echo stub rather than a real provider answer |
| `SEC-AIANON` | medium | api | Anonymous AI endpoints (/app/ai/faq, /app/ai/translate) |
| `ai-provider-keys` | medium | api | Real cognitive-AI provider + keys must be procured (UI + seam run over Echo) |
| `PAR-B1` | medium | app | Booth card omits the officer block and the email/phone contact row |
| `PAR-X2` | medium | app | Zero media assets in prod - every logo/photo/thumbnail falls back |
| `#10-questpdf-licence` | medium | build-ci | QuestPDF ships under the free Community licence; a paid licence is required above the revenue threshold |
| `#1` | medium | cp | Per-day programme images never uploaded (schema + upload path exist, no data) |
| `live-render-not-captured` | medium | infra | The P6 fix batch was never verified against a live render (own admitted gap) |
| `#15` | low | api | Speakers / Companies / Booths need a profile for extra data — design decision |
| `#2` | low | api | Session is linked to ProgrammeDay by date matching, not a foreign key |
| `#7` | low | api | Event / Workshop / Session share one entity and all optionally carry hall, seat, speakers |
| `ai-question-filter-stub` | low | api | The audience-question AI filter is a stub by default and never hides a question |
| `signalr-hub` | low | api | No real SignalR hub — live translation / sign-language ship as chunk-per-request HTTP POST |
| `sms-whatsapp-gateways` | low | api | SMS / WhatsApp notification gateways (EIR-02) must be procured |
| `#11` | low | app | Four session surfaces must scope + phase-gate their buttons per the owner matrix |
| `#22` | low | app | Sign-up category section UI update on sign_up_visitor_screen |
| `#23` | low | app | Session-summary logic update; conflicts with #11's scoping matrix |
| `PAR-A2` | low | app | Archive gallery / session-titles / past-speakers sections empty, unverifiable vs Figma |
| `D6-statistics-metrics` | low | cp | The exact statistics metric list is still undecided (D6) |
| `FR-1101-statistics-metric-list` | low | cp | Statistics dashboards shipped but the exact metric list is pending owner decision D6 |
| `grey-theme-toggle` | low | cp | 3-way grey-theme toggle UI deferred until the owner confirms grey for general use |
| `session-category-empty` | low | cp | Dynamic SessionCategory lookup ships empty pending the client's list |
| `siem-rule-deployment` | low | security | SIEM rules authored but not deployed / false-positive tuned |

## Cannot verify from source

| Ref | Surface | Why |
|---|---|---|
| `mobile-manual-only` | app | The Flutter App UI cannot be driven from the agent session, so all 70 mobile catalogue files are manual-only — §6 of the charter states the emulator renders SurfaceView black and USB is manual, which is why sheet "05 - Mobile App" exists. The repo does have an automated browser sweep for the other two surfaces |
| `48-pre-existing-failures` | build-ci | Document's verification baseline concedes 48 pre-existing full-suite test failures — Stated at D:/swt-fix/docs/tests/SIMF-Round1-Held-Items-Plan.md:160-161 ("full-suite regression cert vs the 48 pre-existing failures = 48/48 shared, 0 new regressions"). Confirming the current count ne |
| `#35-#39/#41` | docs | Items #36–#39 and #41 (and #35) are referenced by number but have no text in this document — The reconciliation banner at D:/swt-fix/docs/SIMF-Bugs-And-Updates-TODO.md lines 24, 35 and 50 counts #35 as already-tracked-done and lists '#36–#39, #41' as 'not re-traced this pass', deferring their |
| `GAP-UNTESTED-SCOPE` | infra | Lighthouse/performance certification, physical-device mobile testing, load/concurrency and penetration testing never performed — Reported in §8 as explicitly out of scope. I cannot determine from the repository whether any of these were performed since — they produce external artifacts (Lighthouse reports, pentest findings, k6  |
| `OA-09-emulator-youtube` | app | YouTube live playback fails on the Android emulator (cert trust); live-video acceptance needs a real device — This is a device/runtime claim -- it needs an emulator and a real handset side by side to reproduce, neither of which I have in a read-only survey. I did not run the app. The backend half of the journ |
| `PAR-P3a` | app | Speaker CV shows 1 full-width pill vs Figma's 4; tuned 4-pill row unverified — D:/swt-fix/src/Mobile/simf_app/lib/features/speakers/widgets/speaker_cv.dart:30-47 builds one Expanded pill per title passed in, so the count is purely a function of how many of bio/qualifications/tra |
| `PAR-P5a` | app | Live screen populated player / captions / geofence / upcoming unverified — Partly covered, partly impossible headlessly, partly deliberately removed. D:/swt-fix/src/Mobile/simf_app/test/golden/live_broadcast_golden_test.dart:20-35 renders frame 934:3450's chrome and the الجل |

## Reported but NOT defects

Closing these is as valuable as fixing the real ones: each is a row that would otherwise be re-investigated every round.

| Ref | Why it is not a defect |
|---|---|
| `OA-04-dates-independent` | **Shifting the event date range does not move existing sessions** — Confirmed as intended, separate-column design: D:/swt-fix/src/Backend/SIMF.Domain/Programme/Session.cs:68,72 declares `public DateTimeOffset Start/End` on the session itself, while D:/swt-fix/src/Backend/SIMF.Domain/Organization/OrganizationProfile.cs:79,82 holds the nullable EventStartDate/EventEnd |
| `OA-07-poll-race` | **Seat map is poll-based (no push), so two users can briefly see the same seat as free** — The observation's own mitigation holds in code: the reserve call is authoritative. D:/swt-fix/src/Backend/SIMF.Infrastructure/SeatReservations/SeatReservationService.cs:188 returns 409 SEAT_ALREADY_RESERVED on the read check, and lines 1962-1974 catch the DbUpdateException from the filtered unique i |
| `OA-D3` | **No-show seat release fires 3 minutes before start, not the "2 minutes" the owner stated** — D:/swt-fix/src/Backend/SIMF.Infrastructure/SeatReservations/SeatReservationService.cs:32-38 -- `NoShowReleaseGrace = TimeSpan.FromMinutes(3)`, whose XML comment quotes the owner directive #6/#17 of 2026-07-20 verbatim: "cancelled if you don't check in 3 minutes before start". The same rule is writte |
| `OA-D3` | **Seat no-show release grace is 3 minutes but the requirement says 2 minutes** — The report has the spec wrong, not the code. Every owner-facing source says 3 minutes: D:/swt-fix/docs/SIMF-Bugs-And-Updates-TODO.md line 508 records the owner summary rule "auto-cancel the seat 3 minutes before start if not checked in" and line 64 marks item #17 built to it; D:/swt-fix/docs/decisio |
| `PAR-X3` | **Sign-in shows an extra alt-login button not in frame 758:2555** — It is a real shipped feature, as the log itself suspected. D:/swt-fix/src/Mobile/simf_app/lib/app/localization/app_l10n.dart:673 defines 'الدخول بمسح الشارة' / 'Sign in by scanning your badge', and D:/swt-fix/src/Mobile/simf_app/lib/features/account/badge_password_screen.dart:76 drives it through au |
| `inapp-exhibitor-signup` | **In-app Exhibitor/Sponsor sign-up (Mockup #08, FR-601) not built** — Permanently descoped (D-199/D-202) and the code reflects the replacement: exhibitors are CP-only companies with provisioned accounts via src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ExhibitorsList.razor and ExhibitorsAddEdit.razor, wired into the Nav.Exhibition group in CpNavigation.cs  |
| `photo-id-verification-signup` | **Photo / ID verification at mobile sign-up (Mockup #06, FR-205/207) descoped** — src/Mobile/simf_app/lib/features/registration/ contains only registration_status_screen.dart, registration_success_screen.dart and their widgets — no capture or verification step, matching the owner descope (D-192/D-200/D-199). The equivalent control moved to the staffed CP path, where ID-document u |
| `exhibitor-self-registration` | **Exhibitor self-registration -> PR approve + assign booth (FR-602) not built** — Same descope. The CP path shipped instead — Exhibitors, Booths and Sponsors pages under Components/Pages/Admin/ (BoothsList.razor / BoothsAddEdit.razor, SponsorsList.razor, ExhibitorsList.razor), all reachable from the Nav.Exhibition group in src/ControlPanel/SIMF.ControlPanel/CpNavigation.cs. |
| `no-admin-delegations-page` | **§7 asserts there is no /admin/delegations page and that bulk delegate badges live on /admin/delegates** — Verified and still accurate — src/ControlPanel/SIMF.ControlPanel/CpNavigation.cs registers /admin/delegates (line 86, the bulk-badge desk), /admin/delegation-meetings (line 130) and /admin/delegation-availability (line 132), and there is no /admin/delegations route. This §7 row is a tester-facing mo |
| `device-attestation` | **Device attestation (App Attest / Play Integrity) + CP device-key surface not built** — Confirmed absent — a case-insensitive grep for AppAttest\|PlayIntegrity\|attestation across all *.cs and *.dart in src/ returns zero hits. That matches the owner's explicit descope (D-172/G10: 'Phase 1 stops at the crypto ceremony'), so it is intended state rather than a defect. |
| `email-otp-every-login` | **Email-OTP at every login (FR-104) out of scope; shipped code keeps 2FA opt-in** — Owner decision D-198. The opt-in model is visible at src/ControlPanel/SIMF.ControlPanel/Components/Pages/Account/Profile.razor:35-44, where two-factor is a per-user Enable/Disable choice rather than a forced step, and src/Shared/SIMF.Common/Enums/NotificationKind.cs documents CredentialSignInOtpSent |
| `website-post-login` | **Public Website post-login experience is a 'You're signed in' placeholder at /account** — The Website no longer has an authenticated surface at all: src/Website/SIMF.Web/Components/Pages/ holds 29 public marketing/programme pages (About, Programme, Speakers, Themes, Venue, Visit, Exhibition, Archive, SessionDetail and so on) with no Home.razor, no /account route and no sign-in page — a g |

## Full detail — every STILL-OPEN item with its evidence

> **This section is a snapshot of 2026-07-30 and is kept verbatim as the
> historical evidence trail — it is NOT the current status.** 40 of the 42
> entries below have since closed and 1 remains open; each one's present state,
> and the file re-read to establish it, is in
> [§ Status correction (2026-07-31)](#status-correction-2026-07-31). Do not plan
> work from this section without reading that one first.

### `DEF-SEC-001-txt` — Plaintext PRODUCTION super-admin credential still committed and tracked in repo scratch file txt.txt

- **Severity** critical · **Surface** security · **Effort** S (<1h)
- **Reported in** `SIMF-Round1-Held-Items-Plan.md` (as: open)
- **Where it still lives** D:/swt-fix/txt.txt is still present and `git ls-files --error-unmatch txt.txt` succeeds (tracked), `git check-ignore` reports not ignored. Line 78 holds a production Control-Panel super-admin email + plaintext password in a "Purpose / Email / Password / Verified" table; lines 49 and 76-77 hold two further plaintext passwords, and lines 52-61 enumerate the nine demo account emails. Notably the DEF-SEC-001 scanner at D:/swt-fix/tests/SIMF.Api.Tests/CommittedSecretsTests.cs:464-513 does scan `.txt` files (ScannedTextExtensions line 456) but does NOT flag this file, because these specific values are not among the three fingerprints in ForbiddenCredentials (CommittedSecretsTests.cs:389-451) — so the guard test gives false assurance here.
- **Fix** Delete txt.txt from the working tree AND from git history (filter-repo/BFG), add the path to .gitignore, and add a fingerprint entry for these values to CommittedSecretsTests.ForbiddenCredentials so the guard actually covers them. Credential rotation itself is owner action (tracked separately).

### `SEC-SMOKE` — tools/smoke/smoke.sh embeds a super-admin email, password and TOTP secret in plaintext

- **Severity** critical · **Surface** security · **Effort** S (<1h)
- **Reported in** `SIMF-QA-Report-2026-07-24.md` (as: open)
- **Where it still lives** D:/swt-fix/tools/smoke/smoke.sh is still tracked and unchanged. Line 8 sets SUPER_TOTP_SECRET to a literal base32 TOTP seed; line 22 posts {"email":"superadmin@zagali-ict.com","password":"<SIMF_SuperAdmin__TempPassword>","audience":1}. That is a complete second factor plus first factor for a super-admin identity, committed. (The script targets http://localhost:5175, not a public host, but the credential triple is real and the email matches the production super-admin.)
- **Fix** Delete the three literals and read them from environment variables (SIMF_SMOKE_EMAIL / _PASSWORD / _TOTP_SECRET), or delete the script. Then the owner must rotate the super-admin password AND re-seed the TOTP secret, and purge both from git history — a code-side deletion alone leaves the working credential in every clone.

### `#2` — Control-Panel sign-in mints a full token on the password alone when 2FA is not enrolled

- **Severity** high · **Surface** api · **Effort** L (multi-day)
- **Reported in** `SIMF-Round1-Held-Items-Plan.md` (as: deferred)
- **Where it still lives** D:/swt-fix/src/Backend/SIMF.Application/IdentityAccess/SignInService.cs:175 is unchanged: `if (!user.TwoFactorEnabled) { var tokens = await IssueTokensAsync(user, cancellationToken); ... return new SignInResponse(false, null, null, tokens, stateInfo); }` — there is no branch on `request.Audience == SignInAudience.Cp`, so a CP admin with 2FA off gets a full access token from the password alone. Corroborated live by D:/swt-fix/txt.txt:78, which records the production super-admin as "2FA off". The voluntary enrolment API exists (D:/swt-fix/src/Backend/SIMF.Api/Endpoints/Auth/TotpSetupEndpoint.cs + TotpConfirmEndpoint.cs) and the CP has a re-pairing page (D:/swt-fix/src/ControlPanel/SIMF.ControlPanel/Components/Pages/Account/TotpPairing.razor:1-11, which explicitly only re-renders an EXISTING secret and does not create one), but there is no mandatory-enrolment challenge.
- **Fix** In SignInService, before the `!user.TwoFactorEnabled` fast path, branch on `request.Audience == SignInAudience.Cp`: instead of issuing tokens, mint a SecondFactorKind enrolment ticket and return a mandatory-2FA-enrolment challenge, and add a CP enrolment page that consumes TotpSetupEndpoint/TotpConfirmEndpoint. Roll out enrolment-first so no admin is locked out.

### `#33` — Control Panel user manual not delivered (owner due date 19-07-2026, already past)

- **Severity** high · **Surface** docs · **Effort** L (multi-day)
- **Reported in** `SIMF-Bugs-And-Updates-TODO.md` (as: open)
- **Where it still lives** D:/swt-fix/docs/manuals/Admin-Manual.md exists (1118 lines) but is explicitly a partial: the string '_(planned)_' occurs 14 times, and the contents list marks whole module families as unwritten — Registration requests (line 32), Attendees (33), Roles & permissions (35), Halls & seating (39), Speakers (40), Bookings (42), and all of Exhibition (43), Engagement (44), Knowledge & AI (45) and Content (46). Chapter stubs remain at lines 204, 446 and 585.
- **Fix** Write the 14 _(planned)_ chapters against docs/pages/PAGE-INDEX.md (each CP module gets purpose, common tasks, control-by-control notes, troubleshooting), and get the owner to confirm the real deadline — the stated one is long past.

### `#2b` — No production boot guard requiring SuperAdmin:TotpSecret — a default prod deploy leaves the bootstrap super-admin single-factor

- **Severity** high · **Surface** security · **Effort** S (<1h)
- **Reported in** `SIMF-Round1-Held-Items-Plan.md` (as: deferred)
- **Where it still lives** D:/swt-fix/src/Shared/SIMF.Common/Options/SuperAdminOptions.cs:17 still defaults `TotpSecret` to string.Empty and D:/swt-fix/src/Backend/SIMF.Api/appsettings.json:27 ships `"TotpSecret": ""`. D:/swt-fix/src/Backend/SIMF.Api/Program.cs:429-476 contains five production fail-fast guards (KnownProxies, Swagger basic-auth, SuperAdmin:TempPassword default, AI prompt-hash secret, PII encryption key, file-store KEK) but NONE for TotpSecret. Consequently D:/swt-fix/src/Backend/SIMF.Infrastructure/Identity/IdentitySeeder.cs:604-608 only calls `SetTwoFactorEnabledAsync(admin, true)` inside `if (!string.IsNullOrWhiteSpace(settings.TotpSecret))`, so an unconfigured prod seeds a 2FA-off Administrator.
- **Fix** Add a guard next to the existing TempPassword one in Program.cs: `if (app.Environment.IsProduction() && string.IsNullOrWhiteSpace(superAdminOptions.TotpSecret)) throw new InvalidOperationException(...)` — superAdminOptions is already bound at Program.cs:423-425.

### `exit-gate-no-open-high` — The charter's exit criterion "no open high-severity defect" is not met

- **Severity** high · **Surface** security · **Effort** L (multi-day)
- **Reported in** `programme-ws0, HEAD fcaa830b)` (as: open)
- **Where it still lives** docs/tests/SIMF-Round1-Held-Items-Plan.md records 5 findings held for owner decision, including one BLOCKER and one HIGH. I code-checked the top two. #1 (demo accounts incl. Administrator seeded in every environment) IS fixed — src/Backend/SIMF.Infrastructure/Identity/IdentitySeeder.cs:296 now gates EnsureDemoAccountsAsync behind hostEnvironment.IsDevelopment() || demoOptions.Value.EnableDemoAccounts and logs a skip at :303. #2 is STILL-OPEN — src/Backend/SIMF.Application/IdentityAccess/SignInService.cs:175 still does `if (!user.TwoFactorEnabled)` and issues full tokens on the password alone at :177-190, with the comment at :172-174 confirming it applies "to both Control Panel users and visitors" — there is no CP-audience carve-out and no mfa/amr claim. The run report deliverable itself does exist (docs/tests/SIMF-Round1-Run-Log.md).
- **Fix** Held-item #2 option C: boot-fail-fast in Production when SuperAdmin:TotpSecret is empty, force TwoFactorEnabled on admin creation, add a CP forced-enrolment challenge for the Cp audience, and emit an mfa claim from JwtTokenService so CP policies can tell a TOTP-completed token from a password-only one. Ship enrolment-first so no admin is locked out.

### `no-literal-secrets-txt` — The same §4.2 "no literal secrets, ever" hard rule is still broken by a git-tracked repo-root scratch file

- **Severity** high · **Surface** security · **Effort** S (<1h)
- **Reported in** `programme-ws0, HEAD fcaa830b)` (as: unknown)
- **Where it still lives** The charter's §4.2 HARD RULE is "No file in this round may contain a literal TOTP secret, password, API key, token, or connection string." D:/swt-fix/txt.txt is git-tracked (git ls-files --error-unmatch txt.txt succeeds; git check-ignore returns nothing) and line 49 is a literal `pwd : <value>` immediately followed by a demo Email/Role account table describing the super-admin and demo visitor logins. I deliberately did not print the value. The charter does not name this file; docs/tests/SIMF-Round1-Held-Items-Plan.md does flag it in its security note.
- **Fix** git rm txt.txt (or move it to the git-ignored deploy/ overlay), purge it from history if the credential was ever real, and rotate the account it names.

### `#10-phase4` — Badge self-claim does not capture the claimer's profile data (name / nationality / interests)

- **Severity** medium · **Surface** api · **Effort** M (half-day)
- **Reported in** `SIMF-Bugs-And-Updates-TODO.md` (as: held)
- **Where it still lives** D:/swt-fix/src/Shared/SIMF.Contracts/Authentication/BadgeAuth.cs:86-99 — BadgeActivationCompleteRequest carries only QrId, Code, NewPassword, ConfirmPassword. D:/swt-fix/src/Backend/SIMF.Application/IdentityAccess/BadgeAuthService.cs:187+ (CompleteActivationAsync) verifies the code and attaches the stashed email but never fills the placeholder UserProfile, so a claimed badge keeps its generated name (e.g. "VIP #3") and NationalityId = 0. The app screen src/Mobile/simf_app/lib/features/account/badge_activation_screen.dart:149,169 goes straight to signIn after activation with no profile step.
- **Fix** Extend BadgeActivationCompleteRequest with the profile fields, fill the placeholder UserProfile inside CompleteActivationAsync (still no Identity schema change), and add the capture step to badge_activation_screen.dart before it routes to signIn.

### `#8` — All times must be stored as Saudi wall-clock, no UTC — storage is still UTC

- **Severity** medium · **Surface** api · **Effort** L (multi-day)
- **Reported in** `SIMF-Bugs-And-Updates-TODO.md` (as: open)
- **Where it still lives** Storage remains UTC: D:/swt-fix/src/Backend/SIMF.Domain/Common/BaseEntity.cs:15-17 defaults CreatedAt to DateTimeOffset.UtcNow and lines 24-26 document CreatedAt as UTC; D:/swt-fix/src/Backend/SIMF.Infrastructure/Auditing/AuditStampingSaveChangesInterceptor.cs:51 stamps 'var now = timeProvider.GetUtcNow()'. GetUtcNow() appears 318 times across 106 files under src/Backend. Only the display seam shipped: D:/swt-fix/src/Shared/SIMF.Common/SaudiTime.cs:5-16 states outright 'All instants are persisted as UTC DateTimeOffset; every user-facing render converts through here.' A migration D:/swt-fix/src/Backend/SIMF.Infrastructure/Persistence/Migrations/App/20260725083434_RenameAppUtcColumnsToLocal.cs dropped the *Utc suffixes (Session.Start/End etc. are now unsuffixed) without changing the stored offset — so the column names now understate that the values are UTC.
- **Fix** Owner must first answer the doc's 3 scope questions (scope, rename, +3h back-fill). Then: replace timeProvider.GetUtcNow() with one Saudi clock seam (+03:00) in the audit interceptor, BaseEntity defaults and all seven workers, and run a one-time +3h shift of existing rows — otherwise every stored instant renders 3 hours early after the switch.

### `OP-SUPERADMIN-SEED` — Super-admin seed fails silently when the configured temp password violates the password policy, leaving a Control Panel nobody can sign into

- **Severity** medium · **Surface** api · **Effort** S (<1h)
- **Reported in** `SIMF-QA-Report-2026-07-24.md` (as: open)
- **Where it still lives** Unchanged. D:/swt-fix/src/Backend/SIMF.Infrastructure/Identity/IdentitySeeder.cs lines 595-602: `var result = await accounts.CreateAsync(admin, settings.TempPassword); if (!result.Succeeded) { logger.LogError(...); return null; }` — and the caller at lines 140-143 does `if (admin is null) { return; }`, so seeding is abandoned and the app boots normally with no super-admin. D:/swt-fix/src/Backend/SIMF.Api/Program.cs lines 454-460 DO fail fast in Production, but only for the exact committed default string "<SIMF_SuperAdmin__TempPassword>" — a policy-violating custom password sails past that guard and hits the silent path.
- **Fix** In CreateSuperAdminAsync, throw instead of returning null when result.Succeeded is false and the environment is Production (mirroring the existing Program.cs guards at 440-460 for Swagger creds and the AI prompt-hash secret). Include result.Errors in the message so the operator sees which policy rule the password broke.

### `#29` — Workshop management in CP + app must show workshop title and time only

- **Severity** medium · **Surface** app · **Effort** M (half-day)
- **Reported in** `SIMF-Bugs-And-Updates-TODO.md` (as: held)
- **Where it still lives** The app-side half is absent. Grepping the whole sessions feature for 'workshop' (D:/swt-fix/src/Mobile/simf_app/lib/features/sessions) finds only the enum value (data/session_models.dart:81), the empty-state string (sessions_screen.dart:108) and the filter tab (widgets/session_type_tabs.dart:37) — there is no branch anywhere that reduces a Workshop's detail to title + time, so a workshop renders the full session detail (speakers, seat block, live/summary actions) via widgets/session_detail_body.dart. The CP half genuinely is covered by the existing session admin + HallAttendance; whether that suffices is the owner ruling the doc asks for.
- **Fix** In session_detail_body.dart, branch on SessionType.workshop and render only the title + time block (suppressing speakers, seat/join and live/summary actions), then re-lock the affected goldens. Separately get the owner's reuse-vs-dedicated-surface ruling for the CP.

### `#40-residual` — Two hardcoded "23–25 November 2026" strings survive the dynamic-dates conversion

- **Severity** medium · **Surface** app · **Effort** S (<1h)
- **Reported in** `SIMF-Bugs-And-Updates-TODO.md` (as: fixed)
- **Where it still lives** D:/swt-fix/src/Mobile/simf_app/lib/app/localization/app_l10n.dart:748-751 — splashEventLine is a literal 'النسخة الرابعة\n23–25 نوفمبر 2026 · الرياض' / '4th Edition\n23–25 Nov 2026 · Riyadh', and it is live: rendered at src/Mobile/simf_app/lib/features/splash/splash_screen.dart:65. Second, the legacy static page the doc said to 'delete/reconcile, not patch' is still in the served folder — D:/swt-fix/src/Website/SIMF.Web/wwwroot/speakers.html:356 contains '23–25 نوفمبر 2026' inside a 627-line page under wwwroot, so it is still reachable as a static file even though the Blazor /speakers page supersedes it.
- **Fix** Splash: the org profile is warmed at splash already, so render OrgProfile.eventDateRange(isArabic) with the literal only as a fallback. speakers.html: delete the file (the Blazor /speakers page replaced it) rather than editing the date.

### `OA-D1` — App home greeting truncates Arabic compound given names ("عبد الله" renders as "عبد")

- **Severity** medium · **Surface** app · **Effort** S (<1h)
- **Reported in** `SIMF-Owner-Acceptance-Round-2026-07.md` (as: open (report-only, owner to choose a fix))
- **Where it still lives** D:/swt-fix/src/Mobile/simf_app/lib/features/home/widgets/greeting_header.dart:31 still reads `final firstName = name.trim().split(' ').first;` and line 32 builds the greeting from it. A grep for `split(' ').first` / `givenName` / `firstName` across src/Mobile/simf_app/lib returns only these two lines -- no FirstName/GivenName field was ever added. The header comment (lines 28-30) records the owner's 2026-07-21 "first name only" instruction, so the split itself is intended; only the Arabic compound-name handling is unaddressed.
- **Fix** Either drop the split and greet the full localized name, or special-case the compound-given-name constructions (عبد …, أبو …, عبد الـ…) before taking the first token. A durable third option is capturing an explicit GivenName at sign-up. The owner must pick one -- the code change itself is a few lines in greeting_header.dart plus a golden refresh.

### `OA-D1` — App home greeting truncates Arabic compound given names (عبد الله renders as عبد)

- **Severity** medium · **Surface** app · **Effort** S (<1h)
- **Reported in** `SIMF-QA-Report-2026-07-24.md` (as: open)
- **Where it still lives** Unchanged. D:/swt-fix/src/Mobile/simf_app/lib/features/home/widgets/greeting_header.dart still computes `final firstName = name.trim().split(' ').first;` and renders `'$firstName 👋'`. The comment above it ("First name only (owner 2026-07-21)") shows the split is deliberate, but the split is on a plain space, so every Arabic compound given name (عبد الله، عبد الرحمن، أبو بكر) loses its second token, and family-name-first data greets the wrong name.
- **Fix** Cheapest correct fix is to drop the split and greet the full trimmed name (the Text already has maxLines:1 + TextOverflow.ellipsis, so long names degrade gracefully). If the owner insists on first-name-only, the robust version is a captured GivenName field at sign-up rather than string surgery. Owner picks; the code change is a one-liner either way.

### `geofence-self-checkin` — Attendee-facing geofence self-check-in: backend built, no app screen

- **Severity** medium · **Surface** app · **Effort** L (multi-day)
- **Reported in** `programme-ws0, HEAD fcaa830b)` (as: deferred)
- **Where it still lives** Backend is live: src/Backend/SIMF.Api/Endpoints/Sessions/HallAttendanceEndpoints.cs exposes POST /app/sessions/{sessionId:guid}/arrival (RecordGeofenceArrivalAsync with lat/lon) and POST /app/sessions/{sessionId:guid}/departure, both under RequireApprovedAccount. No Flutter caller exists — grepping arrival|departure|geofence across src/Mobile/simf_app/lib/ returns only doc comments (src/Mobile/simf_app/lib/features/questions/data/questions_repository.dart:8-9,32-33) and the unrelated delegation arrivalDate/departureDate fields in features/delegations/data/delegation_models.dart. Still blocked on the D-211 G-OI-2 venue-boundary decision per docs/decisions/DECISIONS_LOG.md D-211.
- **Fix** Add an arrival/departure method to a sessions repository and an "I'm here" action on features/sessions/widgets/session_detail_body.dart that posts the device position and renders the returned HallAttendanceStatus — gated on the owner's G-OI-2 venue-boundary answer, since the hall geofence triple must be seeded first.

### `#2c` — JWT carries no amr/mfa claim, so CP authorization policies cannot distinguish a TOTP-completed token from a password-only one

- **Severity** medium · **Surface** security · **Effort** S (<1h)
- **Reported in** `SIMF-Round1-Held-Items-Plan.md` (as: deferred)
- **Where it still lives** Grepped D:/swt-fix/src/Backend/SIMF.Infrastructure/Identity/JwtTokenService.cs for `amr`, `"mfa"` and `MfaClaim` — no matches. The token issued by the password-only path (SignInService.cs:177 `IssueTokensAsync`) is therefore byte-for-byte indistinguishable from one issued after a completed second factor, so no downstream policy can require MFA.
- **Fix** Thread a `secondFactorCompleted` flag into IssueTokensAsync and emit an `amr` claim (`pwd` vs `mfa`/`otp`); then add a `RequireMfa` authorization policy the sensitive CP endpoints can opt into.

### `#2d` — AdminAccountService account creation does not force TwoFactorEnabled for Admin-typed users

- **Severity** medium · **Surface** security · **Effort** M (half-day)
- **Reported in** `SIMF-Round1-Held-Items-Plan.md` (as: deferred)
- **Where it still lives** D:/swt-fix/src/Backend/SIMF.Infrastructure/Identity/AdminAccountService.cs:619-660 — the `CreateAccountAsync` SimfUser initializer (lines 620-635) sets UserName/Email/EmailConfirmed/DisplayName/AccountState/UserType/PasswordChangeRequired/CreatedAt and never touches TwoFactorEnabled, and the Admin-only branch that follows at line 651 only adds RBAC roles. The single `SetTwoFactorEnabledAsync` call in that file is at line 126 and sets it to FALSE (the admin 2FA-reset path). Same omission in the walk-in path at AdminAccountService.cs:400-419.
- **Fix** In CreateAccountAsync, when `userType == UserType.Admin`, mark the account as requiring 2FA enrolment (a forced-enrolment flag or `SetTwoFactorEnabledAsync(user, true)` paired with the #2 enrolment challenge) so a CP-provisioned admin cannot end up permanently single-factor.

### `FR-1103-movement-dwell` — Movement / dwell / route tracking from GPS has no capture path or data source

- **Severity** low · **Surface** api · **Effort** L (multi-day)
- **Reported in** `SIMF-Implementation-Gap-Report.md` (as: open)
- **Where it still lives** src/Backend/SIMF.Domain/Programme/HallAttendance.cs:18 states in-code that the row is a per-session arrival/departure pair and 'not a continuous track (that is the deferred movement/dwell feature, FR-1103)'; src/Backend/SIMF.Infrastructure/Programme/HallAttendanceService.cs:25 repeats it. A repo-wide grep for dwell|MovementTrack across src/ returns only those two comments.
- **Fix** Needs a periodic device-position ping table (userId, hallId/sessionId, capturedAt, lat/lon) plus an aggregation for dwell-per-hall and a route projection. In practice still blocked on the G-OI-2 venue-boundary decision and the D6 statistics metric list.

### `FR-702-riyadh-georestriction` — Live-stream Riyadh-region restriction has no geo-restriction logic

- **Severity** low · **Surface** api · **Effort** M (half-day)
- **Reported in** `SIMF-Implementation-Gap-Report.md` (as: open)
- **Where it still lives** A case-insensitive grep for 'riyadh' across src/ *.cs and *.dart returns only timezone comments (src/Backend/SIMF.Infrastructure/MyArea/MyAreaService.cs:27, src/Mobile/simf_app/lib/core/utils/saudi_time.dart:13), seeded organisation cities and marketing copy — no region gate. src/Mobile/simf_app/lib/features/live/live_broadcast_screen.dart:317-337 selects the stream URL on session timing and role only, and no server-side region check exists.
- **Fix** Gate the returned live URL server-side on the caller's resolved Region (the lookup already exists at src/Backend/SIMF.Domain/Regions/Region.cs), or enforce it at the provider/CDN. Worth re-confirming the requirement first — the feed is now public YouTube, which makes the restriction largely unenforceable.

### `FR-803-80pct-push` — No >=80% match-score threshold and no auto-recommendation push

- **Severity** low · **Surface** api · **Effort** M (half-day)
- **Reported in** `SIMF-Implementation-Gap-Report.md` (as: open)
- **Where it still lives** src/Backend/SIMF.Infrastructure/Recommendations/RecommendationService.cs:170-176 sorts by score and simply Takes the top N — there is no threshold constant and no 0.8 comparison in the file. src/Shared/SIMF.Common/Enums/NotificationKind.cs has no recommendation/match kind among values 0-58, and no worker under src/Backend/SIMF.Infrastructure/Operations/ pushes recommendations.
- **Fix** Add a normalised score threshold to RecommendationService, an additive NotificationKind (e.g. MatchRecommended), and a poll worker alongside SessionReminderWorker that dispatches once per (caller, candidate) pair with a dedup stamp.

### `FR-903-not-attended-reminder` — FR-903 'session started but you have not attended' reminder still has no kind or sender

- **Severity** low · **Surface** api · **Effort** M (half-day)
- **Reported in** `SIMF-Implementation-Gap-Report.md` (as: open)
- **Where it still lives** The booking half of FR-903 shipped (NotificationKind.BookingConfirmed=40, BookingReleased=51) but src/Shared/SIMF.Common/Enums/NotificationKind.cs has no not-attended/no-show kind across values 0-58, and src/Backend/SIMF.Infrastructure/Operations/ReservationNoShowReleaseWorker.cs — the only worker reasoning about no-shows — merely calls ISeatReservationService.ReleaseNoShowsAsync to free the seat; it notifies nobody.
- **Fix** Add an additive NotificationKind (e.g. SessionNotAttended) and have ReservationNoShowReleaseWorker, or a sibling worker firing at Start+N minutes, dispatch it to holders of an active reservation with no HallAttendance row.

### `OA-D5` — No meeting hall-check-in report or export; the CheckedInAt/CheckedInByUserId stamps are written but never exported

- **Severity** low · **Surface** api · **Effort** M (half-day)
- **Reported in** `SIMF-Owner-Acceptance-Round-2026-07.md` (as: open (reporting gap))
- **Where it still lives** The stamps are written -- SpeakerMeetingRequestService.cs:565-568 sets Status=Done, CheckedInAt, CheckedInByUserId -- but a repo-wide grep for `CheckedInAt` across D:/swt-fix/src hits only the domain entities, the two meeting services, their interfaces and EF migrations: no export endpoint and no CP grid column. D:/swt-fix/src/Backend/SIMF.Api/Endpoints/Admin/SpeakerMeetingRequestsExcelEndpoints.cs:32-42 exports only Speaker / Requester / Subject / Status / CreatedAt / RespondedAt. BusinessMeetingsExcelEndpoints.cs exports Hall / Table / Type / Start / End / Parties / Status. Delegation meeting requests have a check-in route (Endpoints/Programme/DelegationMeetingRequestEndpoints.cs:189) but no /export route at all. AttendanceDashboard.razor.cs is session (hall) attendance, not meetings.
- **Fix** Add `CheckedInAt` and the check-in operator to the SpeakerMeetingRequests export column list (and add a matching AdminGridExportEndpoint for delegation meeting requests, which has none), reusing the existing AdminGridExportEndpoint/GridExcelColumn pattern plus a new PermissionCatalog.*.Export code for the delegation grid.

### `OA-D5` — Meeting hall check-in stamps are recorded but never surfaced in any report or export

- **Severity** low · **Surface** api · **Effort** M (half-day)
- **Reported in** `SIMF-QA-Report-2026-07-24.md` (as: open)
- **Where it still lives** Half-closed. The check-in ACTIONS now exist — D:/swt-fix/src/Backend/SIMF.Api/Endpoints/Programme/SpeakerMeetingRequestEndpoints.cs:182 POST /admin/speaker-meeting-requests/{id}/check-in and DelegationMeetingRequestEndpoints.cs:189 for delegations — and the entity carries the stamps (SpeakerMeetingRequestService.cs:636-637 resets req.CheckedInAt and req.CheckedInByUserId on reopen). But the reporting surface does not expose them: D:/swt-fix/src/Shared/SIMF.Contracts/Programme/SpeakerMeetingRequests.cs AdminSpeakerMeetingRequestRow (lines 35-46) and AdminSpeakerMeetingRequestDetail (lines 55-74) have no CheckedIn* member at all, and the export at src/Backend/SIMF.Api/Endpoints/Admin/SpeakerMeetingRequestsExcelEndpoints.cs ships exactly six columns — Speaker, Requester, Subject, Status, CreatedAt, RespondedAt. A grep for CheckedIn across src/Shared/SIMF.Contracts hits only AppRequests.cs:86 and Sessions/SeatReservations.cs:55, both unrelated to meetings.
- **Fix** Append CheckedInAt and CheckedInByName to AdminSpeakerMeetingRequestRow (append-only, D-219 wire rule), project them in the service's list query, add the two GridExcelColumn entries to SpeakerMeetingRequestsExcelEndpoints._columns, and mirror for the delegation row.

### `OA-D6` — Public programme endpoint supports only ?day=; no server-side theme/category filter

- **Severity** low · **Surface** api · **Effort** M (half-day)
- **Reported in** `SIMF-Owner-Acceptance-Round-2026-07.md` (as: open (feature gap))
- **Where it still lives** D:/swt-fix/src/Backend/SIMF.Api/Endpoints/Programme/PublicSessionEndpoints.cs:20-25 -- ListProgrammeSessionsRequest exposes a single `Day` property, and HandleAsync (lines 40-61) parses it and calls service.ListAsync(day, ct) with no other predicate. A grep for categoryId/CategoryId/themeId/ThemeId across src/Backend/SIMF.Api/Endpoints/Programme returns no matches, so theme and category grouping remains client-side.
- **Fix** Add an optional `ThemeId`/`CategoryId` (Guid?) to ListProgrammeSessionsRequest and thread it into IProgrammeSessionService.ListAsync as an extra Where clause; the 45s output cache already varies by query key so each filter combination keys separately. Only worth doing if the owner actually wants a server-side track filter -- the app currently groups client-side.

### `OA-D6` — Public programme endpoint filters by ?day= only; no server-side theme/category filter

- **Severity** low · **Surface** api · **Effort** M (half-day)
- **Reported in** `SIMF-QA-Report-2026-07-24.md` (as: open)
- **Where it still lives** Unchanged. D:/swt-fix/src/Backend/SIMF.Api/Endpoints/Programme/PublicSessionEndpoints.cs: ListProgrammeSessionsRequest (lines 20-25) declares exactly one property, `public string? Day { get; set; }`, and HandleAsync (lines 40-61) parses only that and calls `service.ListAsync(day, ct)`. There is no CategoryId / theme / axis parameter, even though Session.CategoryId and the dynamic SessionCategory lookup exist (D-226). Any category filtering the app or website does is client-side over the full list. This is a feature gap, not a fault — the endpoint is correct for what it declares.
- **Fix** Add an optional Guid? CategoryId to ListProgrammeSessionsRequest and thread it into IProgrammeSessionService.ListAsync as a second nullable filter. Note the endpoint uses CacheOutput("PublicRead") which already varies by all query keys, so a new query parameter keeps its own cache entry automatically.

### `sms-whatsapp-channels` — Notification multi-channel dispatch: no SMS or WhatsApp channel behind the abstraction

- **Severity** low · **Surface** api · **Effort** L (multi-day)
- **Reported in** `SIMF-Implementation-Gap-Report.md` (as: held)
- **Where it still lives** A case-insensitive grep for sms|whatsapp across src/Backend/ returns exactly one unrelated hit (src/Backend/SIMF.Infrastructure/AccessControl/GateOperatorService.cs:651, the word 'mechanisms'). src/Backend/SIMF.Application/Notifications/INotificationDispatcher.cs:8 describes an in-app row plus an IEmailSender queue — there is no channel interface, so the 'one abstraction' the report credits does not yet generalise past email.
- **Fix** Introduce an INotificationChannel abstraction with in-app + email as the first two implementations, then add gateway-backed SMS/WhatsApp channels once a provider is procured.

### `#16` — Flutter clean-code / tokenisation sweep across ~39 features

- **Severity** low · **Surface** app · **Effort** M (half-day)
- **Reported in** `SIMF-Bugs-And-Updates-TODO.md` (as: open)
- **Where it still lives** Largely done but not complete: 40 raw Colors.white remain under D:/swt-fix/src/Mobile/simf_app/lib, including in features the checklist marks '✅ merged' — features/requests/new_request_sheet.dart lines 135, 266, 309, 337; features/accessibility/widgets/accessibility_toggle_row.dart (2), accessibility_section_heading.dart, accessibility_font_size_card.dart; and the two deferred ones the doc names, features/myarea/identity_verification_screen.dart:363 and features/myarea/widgets/identity_capture_view.dart:70 (Colors.white70). The rest sit in shared app/widgets (simf_page_shell.dart has 11), which the per-feature checklist never covered.
- **Fix** Sweep the four remaining feature files onto SimfTokens.surface, then decide whether the shared app/widgets layer (simf_page_shell, more_drawer, dialogs) is in or out of the sweep's scope and record that in the checklist.

### `PAR-B4` — Booth card subtitle duplicates the company name

- **Severity** low · **Surface** app · **Effort** S (<1h)
- **Reported in** `FIGMA-PARITY-DEFECTS.md` (as: deferred)
- **Where it still lives** D:/swt-fix/src/Mobile/simf_app/lib/features/booths/widgets/booth_company_header.dart:59-63 renders the exhibitor line whenever fullName != null, with no equality guard against the short name above it. The shipped seed still makes them identical: D:/swt-fix/docs/migrations/2026/SIMF_App_SeedGaps.sql:47-49 inserts Name = N'Advanced Naval Technologies' and ExhibitorName = N'Advanced Naval Technologies' (same for A-02, B-01, B-02), so every seeded booth card shows the same string twice.
- **Fix** Either give the seeded booths a distinct trading name vs legal exhibitor name, or add a one-line guard in booth_company_header.dart so the fullName Text is skipped when fullName.trim() == name.trim().

### `PAR-D3` — Session detail is missing the session-type / category pill

- **Severity** low · **Surface** app · **Effort** S (<1h)
- **Reported in** `FIGMA-PARITY-DEFECTS.md` (as: deferred)
- **Where it still lives** The document blamed null data, but the code no longer renders a category pill at all. SessionDetail.localizedCategory exists (D:/swt-fix/src/Mobile/simf_app/lib/features/sessions/data/session_models.dart:442-443) and is never called: grepping category/Category across D:/swt-fix/src/Mobile/simf_app/lib/features/sessions/widgets/*.dart and session_detail_screen.dart returns only a doc comment. The header card (D:/swt-fix/src/Mobile/simf_app/lib/features/sessions/widgets/session_header_card.dart:56-110) is badge+title, meta row, and two action chips - no pill row; the body (widgets/session_detail_body.dart:87-96) adds none. The doc comment at session_detail_screen.dart:38 still promises 'hall + category tag pills', so the comment is stale too.
- **Fix** Render a small pill under the header-card title bound to detail.localizedCategory(isArabic) (and optionally the hall name), shown only when non-null; update the stale doc comment at session_detail_screen.dart:38 in the same change.

### `PAR-P1a` — Media-partners active tab label renders 1 line vs Figma's 2

- **Severity** low · **Surface** app · **Effort** S (<1h)
- **Reported in** `FIGMA-PARITY-DEFECTS.md` (as: open)
- **Where it still lives** D:/swt-fix/src/Mobile/simf_app/lib/app/widgets/media_coverage_tabs.dart:87-98 still sets maxLines: 1 with TextOverflow.ellipsis on the tab label inside a fixed 48-high pill, so a long label such as 'الشركاء الإعلاميون' truncates rather than wrapping. Note the strip was rebuilt against a newer frame since the log was written (1049:12629, two tabs, the معرض الصور tab dropped - see the class doc at lines 11-16), so the exact 2-line expectation may itself be stale.
- **Fix** Raise the pill label to maxLines: 2 with a slightly smaller line-height (the pill is already 48 high, so two 12px lines fit), or re-confirm against frame 1049:12629 before changing anything.

### `PAR-P4a` — Host STAR glyph not rendered on the speakers list

- **Severity** low · **Surface** app · **Effort** S (<1h)
- **Reported in** `FIGMA-PARITY-DEFECTS.md` (as: open)
- **Where it still lives** The list omission is now an explicit, documented decision, not a bug: D:/swt-fix/src/Mobile/simf_app/lib/features/speakers/widgets/speaker_list_card.dart:14-17 states the host/speaker distinction is per-session (D-432) so the list shows the anchor for everyone, 'the host star appears on the session detail'. But that promise is unmet - on session detail the host is a plain text label, D:/swt-fix/src/Mobile/simf_app/lib/features/sessions/widgets/session_speaker_card.dart:32-40 appends l10n.hostLabel ('المضيف', app_l10n.dart:1193) to the rank line, and a repo-wide grep for Icons.star / ic_star finds star glyphs only in feedback, more-menu, my-area and notifications - none on a speaker.
- **Fix** On the session-detail speaker card, swap the 'المضيف' text marker for the Figma star glyph (or add the star beside it) when speaker.role == SessionSpeakerRole.host; alternatively correct the stale claim in speaker_list_card.dart:14-17.

### `STALE-GOLDEN-ARTIFACTS` — 48 stale golden-failure PNGs committed under test/golden/failures/

- **Severity** low · **Surface** app · **Effort** S (<1h)
- **Reported in** `SIMF-QA-Report-2026-07-24.md` (as: open)
- **Where it still lives** The report notes at §4 that these artifacts are "stale from an earlier main revision". They are still tracked: `git ls-files src/Mobile/simf_app/test/golden/failures/` returns 48 files (12 screens x isolatedDiff/maskedDiff/masterImage/testImage), covering badge_sign_in, home_guest, home_signed_in, live_broadcast, more, presentations, share_my_contact, sign_up_visitor, speaker_profile, speakers, splash and staff_register_visitor. My full `flutter test` run is 1247/1247 green, so every one of these diffs is obsolete debris that will mislead the next person who greps the directory.
- **Fix** `git rm -r --cached src/Mobile/simf_app/test/golden/failures/` and add the path to .gitignore — the directory is generated output from a failing golden run, never an input.

### `accessibility-server-sync` — Accessibility settings are local prefs only, not server-synced

- **Severity** low · **Surface** app · **Effort** M (half-day)
- **Reported in** `SIMF-Implementation-Gap-Report.md` (as: open)
- **Where it still lives** src/Mobile/simf_app/lib/features/accessibility/accessibility_screen.dart:19-24 states the choices are 'persisted ([AccessibilityController], prefs-backed) and applied app-wide'; the only data file is src/Mobile/simf_app/lib/features/accessibility/data/accessibility_controller.dart — no repository, no API call. Preferences therefore do not follow the user across devices or survive a reinstall.
- **Fix** Add the five flags (font scale, high contrast, reduce motion, screen reader, captions) to the user-profile preferences DTO; have AccessibilityController write through on change and hydrate at sign-in, keeping the local prefs as the offline cache.

### `itokenissuer-extraction` — Architecture: ITokenIssuer never extracted — token minting duplicated across auth paths

- **Severity** low · **Surface** build-ci · **Effort** M (half-day)
- **Reported in** `SIMF-Implementation-Gap-Report.md` (as: open)
- **Where it still lives** src/Backend/SIMF.Infrastructure/IdentityAccess/DeviceKeyService.cs:27 still says an 'ITokenIssuer abstraction would be the right follow-up' and line 410 repeats that 'the fix is to extract a shared ITokenIssuer'. A repo-wide grep for ITokenIssuer finds only those two comments — no interface exists.
- **Fix** Extract an ITokenIssuer in Application with a single implementation, and route the device-key ceremony, password sign-in and badge-QR sign-in through it so the claim set and the D-443 lifetime caps cannot drift between entry points.

### `sev-1-1-domain-purity` — Architecture SEV-1.1: Domain still depends on ASP.NET Core Identity (SimfUser : IdentityUser<Guid>) while the refactor plan claims it closed

- **Severity** low · **Surface** build-ci · **Effort** L (multi-day)
- **Reported in** `SIMF-Implementation-Gap-Report.md` (as: open)
- **Where it still lives** src/Backend/SIMF.Domain/SIMF.Domain.csproj still carries <PackageReference Include="Microsoft.Extensions.Identity.Stores" Version="10.0.8" />; src/Backend/SIMF.Domain/IdentityAccess/SimfUser.cs:1,20 is `using Microsoft.AspNetCore.Identity;` / `public class SimfUser : IdentityUser<Guid>` and SimfRole.cs:1,11 likewise derives from IdentityRole<Guid>. No DomainPurityTests.cs exists under tests/. docs/SIMF-Architecture-Refactor-Plan.md:26 and :172 nonetheless assert 'DONE — D-090→093' with 'Arch SEV-1.1 fully closed' and the package reference removed — that document is wrong for this line of the code.
- **Fix** Either re-do the D-093 POCO split on this branch or correct the refactor plan; one of the two must change. The minimum honest step is a DomainPurityTests fixture asserting the SIMF.Domain assembly references no Identity type, so the claim cannot silently regress again.

### `sev-1-6-service-placement` — Architecture SEV-1.6: AdminAccountService still in Infrastructure rather than Application

- **Severity** low · **Surface** build-ci · **Effort** L (multi-day)
- **Reported in** `SIMF-Implementation-Gap-Report.md` (as: open)
- **Where it still lives** src/Backend/SIMF.Infrastructure/Identity/AdminAccountService.cs is still under Infrastructure — the one service docs/SIMF-Architecture-Refactor-Plan.md:25 records as 'AdminAccountService move DEFERRED post-event (security-critical, would need new RoleManager/UserRoles abstractions)'. The other named moves (notifications, interests, user profile) did land under src/Backend/SIMF.Application/.
- **Fix** Extract IRoleManager / IUserRoleStore-shaped abstractions in Application, then move AdminAccountService behind them. The plan defers this past the event, which is defensible — but the deferral should be re-confirmed rather than quietly forgotten.

### `FR-1203-brand-colour-tokens` — CMS: brand-colour token editing from the Control Panel deferred

- **Severity** low · **Surface** cp · **Effort** M (half-day)
- **Reported in** `SIMF-Implementation-Gap-Report.md` (as: open)
- **Where it still lives** src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SiteSettingsPage.razor:1-5 scopes that page to the bilingual registration welcome message only (social links moved to Organization Profile). Grepping SiteSettingsPage.razor, OrganizationProfilePage.razor and ContentBlockAddEdit.razor for brand/Colour/Color returns nothing; ThemesList/ThemesAddEdit are forum content themes, not colour tokens.
- **Fix** Either close the item by accepting theme.tokens.css as the single source of truth (which the project's own CSS rules mandate), or add a SystemSettings-backed brand-colour section that emits CSS custom-property overrides at layout render.

### `QA-LIVE-001` — GET /favicon.ico returns 404 on every Control Panel page

- **Severity** low · **Surface** cp · **Effort** S (<1h)
- **Reported in** `SIMF-QA-Report-2026-07-24.md` (as: open)
- **Where it still lives** Unchanged. D:/swt-fix/src/ControlPanel/SIMF.ControlPanel/wwwroot/ contains only app.css and a js/ directory — no favicon.ico, no icon of any kind. A grep for "favicon" across the whole SIMF.ControlPanel project (.razor, .html, .cs) returns nothing, and the <head> of App.razor (lines 6-46) has five <link rel="stylesheet"> tags and no <link rel="icon">, so the browser falls back to requesting /favicon.ico and gets a 404 on every page load.
- **Fix** Drop a favicon.ico (or the existing SIMF logo as .png/.svg) into src/ControlPanel/SIMF.ControlPanel/wwwroot/ and add <link rel="icon" href="@Assets["favicon.ico"]" /> to the App.razor head alongside the stylesheet links.

### `cp-stub-modules` — 8 of the 22 original D-134 CP stub modules remain ModulePlaceholder stubs

- **Severity** low · **Surface** cp · **Effort** M (half-day)
- **Reported in** `SIMF-Implementation-Gap-Report.md` (as: open)
- **Where it still lives** Down from 8 to exactly 1. src/ControlPanel/SIMF.ControlPanel/CpNavigation.cs:179 is the only remaining IsStub entry — new("Module.LiveSessions", "/m/live-sessions", IsStub: true, Icon: "video") — resolving to src/ControlPanel/SIMF.ControlPanel/Components/Pages/ModulePlaceholder.razor (@page "/m/{Module}"). The other seven graduated: /admin/bookings (CpNavigation.cs:141), /admin/faq, /admin/configuration (line 261), /admin/venue-map, /admin/exhibitors, /admin/site-settings, and registration-requests (BadgeRequestsList / DocumentRequestsList).
- **Fix** Either build a Live Sessions console over the existing Session.LiveStreamUrl / LiveSignLanguageUrl columns and the SessionLiveHall data, or remove the nav entry — an IsStub item has RequiredPermission null, so it is visible to every signed-in admin.

### `SEC-NOTES` — txt.txt and myComment.txt tracked plaintext working notes

- **Severity** low · **Surface** docs · **Effort** S (<1h)
- **Reported in** `SIMF-QA-Report-2026-07-24.md` (as: open)
- **Where it still lives** `git ls-files` still lists both myComment.txt and txt.txt at the repo root, and neither appears in .gitignore. I read both heads: they contain owner fix-lists and developer scratch notes, no credentials — so the exposure is low. Worth noting txt.txt is actively misleading now: it documents FLAG_SECURE being commented out for screenshot capture, but D:/swt-fix/src/Mobile/simf_app/android/app/src/main/kotlin/com/example/simf_app/MainActivity.kt has the window.setFlags(FLAG_SECURE, FLAG_SECURE) call live and uncommented, so the NCA control A11-6 is in place and the note is stale.
- **Fix** `git rm --cached txt.txt myComment.txt` and add both to .gitignore, or move their surviving content into docs/. Confirm with the owner first — myComment.txt is an owner-authored fix-list that other work still references.

### `catalogue-count-drift` — The charter's "164-file / 2,142-scenario" catalogue size is stale

- **Severity** low · **Surface** docs · **Effort** S (<1h)
- **Reported in** `programme-ws0, HEAD fcaa830b)` (as: unknown)
- **Where it still lives** The preamble ("a 164-file / 2,142-scenario per-page Gherkin E2E catalogue") and §2.2/§5 ("every one of the 2,142 per-page scenarios") no longer match the repo: docs/tests/e2e/ holds 183 authored .md files excluding README.md, _TEMPLATE.md and E2E-TEST-PLAN.md, carrying roughly 2,868 E2E-{NS}-{NNN} scenario ids. tools/testbook/build_testbook.py and docs/tests/SIMF-Production-Readiness-TestBook.xlsx were both last written 2026-07-26, so the generated workbook is probably current and only the charter prose is stale — but a tester reading the charter will under-scope the round by about 700 scenarios.
- **Fix** Re-derive both counts from the catalogue (or have build_testbook.py emit them) the next time the workbook is regenerated.

### `FR-1203-markdown-render` — Public content ships raw markdown — no renderer on Website or app

- **Severity** low · **Surface** website · **Effort** S (<1h)
- **Reported in** `SIMF-Implementation-Gap-Report.md` (as: open)
- **Where it still lives** src/Backend/SIMF.Domain/Cms/ContentBlock.cs documents both Content and ContentArabic as 'English/Arabic content (markdown allowed)', but a grep for Markdown/markdown across src/Website/ and src/ControlPanel/ returns no hits — no renderer and no sanitizer. Consumers read the text via src/Backend/SIMF.Api/Endpoints/Public/PublicCmsEndpoints.cs and print it verbatim.
- **Fix** Either render server-side through a sanitizing markdown pipeline before emitting the block, or drop the 'markdown allowed' claim from ContentBlock's XML doc so the contract matches behaviour. Do not render unsanitised HTML from an admin-editable field.

## Already fixed — do not re-do

Listed so a future round does not re-open them. Each was confirmed by reading the code that closed it.

| Ref | Reported in | Closed by |
|---|---|---|
| `PAR-A1` | FIGMA-PARITY-DEFECTS.md | D:/swt-fix/src/Mobile/simf_app/lib/features/archive/widgets/archive_stat_row.dart:22-38 - the row is now the two tiles the current frame (926:3285) shows, الفعاليات then المتحدثون, with the attendees tile deliberately dropped (doc … |
| `PAR-B2` | FIGMA-PARITY-DEFECTS.md | D:/swt-fix/src/Mobile/simf_app/lib/features/booths/widgets/booth_hall_row.dart:26-45 renders BoothHallBox(code: hallEn.toUpperCase(), name: hallAr) - the Figma 'HALL A · القاعة الرئيسية' shape - whenever both hall names are on the … |
| `PAR-B3` | FIGMA-PARITY-DEFECTS.md | D:/swt-fix/src/Mobile/simf_app/lib/features/booths/widgets/booth_company_header.dart:44-53 puts _LogoTile first in the Row (inline-start / physical right), name column next, country flag last. The search glyph is a prefixIcon on t … |
| `PAR-D-extra` | FIGMA-PARITY-DEFECTS.md | D:/swt-fix/src/Mobile/simf_app/lib/features/sessions/widgets/session_speaker_card.dart:46-53 puts _SpeakerAvatar first, text column second. D:/swt-fix/src/Mobile/simf_app/lib/features/sessions/widgets/session_reservation_card.dart … |
| `PAR-D1` | FIGMA-PARITY-DEFECTS.md | D:/swt-fix/src/Mobile/simf_app/lib/features/sessions/widgets/session_header_card.dart:197-213 wraps the badge text in FittedBox(fit: BoxFit.scaleDown) with maxLines:1/softWrap:false. Better than the log records: lines 44-49 now co … |
| `PAR-D2` | FIGMA-PARITY-DEFECTS.md | D:/swt-fix/src/Mobile/simf_app/lib/features/sessions/widgets/session_booking_actions.dart:101-168 - the gold Expanded 'أضف إلى تقويمي' FilledButton is the first Row child (inline-start / physical right under RTL) and the outlined  … |
| `PAR-D4` | FIGMA-PARITY-DEFECTS.md | The verification gap is closed by a populated golden: D:/swt-fix/src/Mobile/simf_app/test/golden/session_detail_golden_test.dart:27 describes the fixture as the 'Richest representative state - signed-in + a held assigned-seat rese … |
| `PAR-P3b` | FIGMA-PARITY-DEFECTS.md | The verification gap is closed: D:/swt-fix/src/Mobile/simf_app/test/golden/my_seat_golden_test.dart:19-31 is an explicit golden render of the My-Seat screen against frame 898:2873 in the approved-Visitor state, with a fully popula … |
| `PAR-P4b` | FIGMA-PARITY-DEFECTS.md | rankArabic now exists at every layer: D:/swt-fix/src/Backend/SIMF.Domain/Programme/Speaker.cs:23 (RankArabic), D:/swt-fix/src/Shared/SIMF.Contracts/Programme/PublicSpeakers.cs:15 and :50 (both list and detail), and the app picks i … |
| `PAR-S1` | FIGMA-PARITY-DEFECTS.md | D:/swt-fix/src/Mobile/simf_app/lib/app/localization/app_l10n.dart:1676-1697 defines sponsorTierStrategic/Premium/Gold/Bronze and maps the tier weight to them (10 -> 'الرعاية الاستراتيجية', 20 -> 'رعاة بريميوم', 30 -> 'رعاة ذهبيون' … |
| `PAR-S2` | FIGMA-PARITY-DEFECTS.md | D:/swt-fix/src/Mobile/simf_app/lib/features/sponsors/widgets/sponsor_card.dart:55-124 - the Row children are [_BadgeBox(logo), name column, chevron], so the logo leads at the inline-start. Regression-guarded by D:/swt-fix/src/Mobi … |
| `PAR-S3` | FIGMA-PARITY-DEFECTS.md | Code path: D:/swt-fix/src/Mobile/simf_app/lib/features/sponsors/sponsors_screen.dart:129-130 passes sponsor.localizedTagline(isArabic) ?? sponsor.url as the card secondary line. Content is now shipped too: D:/swt-fix/docs/migratio … |
| `PAR-X1` | FIGMA-PARITY-DEFECTS.md | The owner decision was taken and implemented: D:/swt-fix/src/Mobile/simf_app/lib/app/widgets/simf_page_shell.dart:121-127 documents 'Defaults to false (owner 2026-06-28): the Figma standard sub-page nav ... is back + centred title … |
| `PAR-X4` | FIGMA-PARITY-DEFECTS.md | D:/swt-fix/src/Mobile/simf_app/test/golden/home_golden_test.dart:19-25 is a golden render of BOTH the signed-in Home (VisitorHome, frame 758:1134) and the guest Home (GuestHome, 758:2910), importing features/home/widgets/visitor_h … |
| `#10` | SIMF-Bugs-And-Updates-TODO.md | The doc calls Phases 2 and 3 'owner-gated — NOT yet built'; both have since shipped. Phase 2: migration D:/swt-fix/src/Backend/SIMF.Infrastructure/Persistence/Migrations/App/20260722175304_D758_AddBadgeBatch.cs, entity src/Backend … |
| `#12 / #26` | SIMF-Bugs-And-Updates-TODO.md | D:/swt-fix/src/Mobile/simf_app/lib/features/myarea/identity_verification_screen.dart:124-155 reads the front camera's sensorOrientation and normalises the yaw sign per platform+sensor before feeding livenessStepSatisfied. Pinned b … |
| `#13` | SIMF-Bugs-And-Updates-TODO.md | Endpoint D:/swt-fix/src/Backend/SIMF.Api/Endpoints/Networking/PartnerDirectoryEndpoint.cs:20 serves GET /app/networking/partner-directory (interface at src/Backend/SIMF.Application/Networking/Abstractions/IPartnerDirectoryService. … |
| `#14` | SIMF-Bugs-And-Updates-TODO.md | D:/swt-fix/src/Mobile/simf_app/lib/app/router.dart:390 returns 'const SignUpInterestsScreen(editMode: true)' for the edit route (route_names.dart:29 documents it as the same screen in edit mode), and the entry point is on My-Area: … |
| `#17` | SIMF-Bugs-And-Updates-TODO.md | Bookings auto-approve (SeatReservationService.cs:207/350/512/876/937/1938). The 3-minute rule is real, not just a comment: D:/swt-fix/src/Backend/SIMF.Infrastructure/SeatReservations/SeatReservationService.cs:34-38 defines NoShowR … |
| `#18` | SIMF-Bugs-And-Updates-TODO.md | D:/swt-fix/src/Mobile/simf_app/lib/features/sessions/session_detail_screen.dart:163-214 — _join() branches on seat mode: assigned-seat pushes RouteNames.seatPicker and reloads on return (lines 168-177); open-seating confirms, call … |
| `#19` | SIMF-Bugs-And-Updates-TODO.md | D:/swt-fix/src/Mobile/simf_app/lib/app/localization/app_l10n.dart:550 — guestSignInLink now reads 'الدخول كضيف' / 'Enter as guest'. |
| `#20` | SIMF-Bugs-And-Updates-TODO.md | D:/swt-fix/src/Mobile/simf_app/lib/app/router.dart:259-262 documents and applies the public route: the sessions list and session detail (17) are PUBLIC so a guest can browse the programme and open a session without signing in; lin … |
| `#21` | SIMF-Bugs-And-Updates-TODO.md | The button at D:/swt-fix/src/Mobile/simf_app/lib/features/myarea/widgets/my_area_dashboard_body.dart:75 (l10n.shareContact) now has a destination: features/contacts/share_my_contact_screen.dart:203, backed by the vCard payload bui … |
| `#24` | SIMF-Bugs-And-Updates-TODO.md | D:/swt-fix/src/Backend/SIMF.Api/Endpoints/Auth/ChangeEmailEndpoints.cs:12-53 exposes POST /app/auth/change-email/send-otp and /confirm for a signed-in user; both paths are rate-limited by key at src/Backend/SIMF.Api/RateLimiting/E … |
| `#27` | SIMF-Bugs-And-Updates-TODO.md | D:/swt-fix/src/Mobile/simf_app/lib/features/live/widgets/live_video_player.dart:57-69 — the player marks the session active on start and then re-marks it on a periodic Timer (ref.read(sessionActivityProvider).markActive()) with th … |
| `#28` | SIMF-Bugs-And-Updates-TODO.md | Both halves verified, including the residual the doc still flags: D:/swt-fix/src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/GateOperatorConsole.razor:103 now renders @row.ScannedAt.FormatSaudi("hh:mm:ss tt") rather than … |
| `#3` | SIMF-Bugs-And-Updates-TODO.md | D:/swt-fix/src/Backend/SIMF.Infrastructure/Programme/AdminSessionService.cs:191-198 throws ApiException(ErrorCodes.SessionTypeRequired, 400) when request.Type is null on create; lines 341-348 do the same on update while grandfathe … |
| `#30` | SIMF-Bugs-And-Updates-TODO.md | CP surface present at D:/swt-fix/src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/BusinessMeetingsList.razor (+ .razor.cs). The supporting data model shipped in this window too — migrations 20260722141011_AddUserProfileMe … |
| `#32` | SIMF-Bugs-And-Updates-TODO.md | D:/swt-fix/src/Shared/SIMF.Common/AppRoles.cs declares PublicRelations (line 42), SecurityTeam (line 50) and ScientificCommittee (lines 52-60, D-752) alongside Administrator, and exposes them as AppRoles.CpRoles (line 64). D:/swt- … |
| `#34` | SIMF-Bugs-And-Updates-TODO.md | Every speaker surface goes through the locale-aware accessor: D:/swt-fix/src/Mobile/simf_app/lib/features/speakers/data/speaker_models.dart:37,162 define localizedRank(isArabic), and it is the only reader at speaker_profile_screen … |
| `#4` | SIMF-Bugs-And-Updates-TODO.md | D:/swt-fix/src/Backend/SIMF.Infrastructure/Programme/AdminSessionService.cs:199-206 rejects a create that fails SatisfiesSpeakerRule with ErrorCodes.SessionSpeakerRequired; lines 352-353 apply the same rule on update only when the … |
| `#40` | SIMF-Bugs-And-Updates-TODO.md | The dynamic chain is real end to end: D:/swt-fix/src/Website/SIMF.Web/Content/ForumDates.cs:16-28 fetches and caches the range from the public profile API; Components/Layout/LandingPageHero.razor:36 and Components/Pages/Landing.ra … |
| `#42` | SIMF-Bugs-And-Updates-TODO.md | D:/swt-fix/src/Mobile/simf_app/lib/features/home/widgets/greeting_header.dart:31-32 takes the first token (name.trim().split(' ').first) and appends 👋, and line 65 renders l10n.greetingWelcome instead of the old time-of-day greeti … |
| `#43` | SIMF-Bugs-And-Updates-TODO.md | D:/swt-fix/src/Mobile/simf_app/lib/features/home/widgets/visitor_home.dart:78-86 now renders HomeHeroBanner (widgets/home_hero_banner.dart) fed by the CP-managed banners list and the OrgProfile (fields declared at lines 49-55), fa … |
| `#6` | SIMF-Bugs-And-Updates-TODO.md | D:/swt-fix/src/Backend/SIMF.Infrastructure/SeatReservations/SeatReservationService.cs writes Status = BookingStatus.Approved on every creation path (lines 207, 350, 512, 876, 937, 1938); D:/swt-fix/src/Backend/SIMF.Domain/SeatRese … |
| `#9 / #25` | SIMF-Bugs-And-Updates-TODO.md | The implementation is present and wired: D:/swt-fix/src/Mobile/simf_app/lib/features/account/biometric_auth.dart and .../features/account/biometric_step_up_screen.dart, referenced from sign_in_screen.dart, badge_password_screen.da … |
| `avatar-tap-red-test` | SIMF-Bugs-And-Updates-TODO.md | I ran it: 'flutter test test/features/home/home_screen_test.dart' in D:/swt-fix/src/Mobile/simf_app finishes '00:03 +36: All tests passed!', and the specific case the doc calls out — 'tapping the greeting avatar switches to the Pr … |
| `D-014-mobile-role-routing` | SIMF-Implementation-Gap-Report.md | The TODO is gone — a grep for TODO(D-014)/_homeForRole across src/Mobile/simf_app/lib returns nothing. Routing keys on CurrentUser.effectiveAppRole (src/Mobile/simf_app/lib/app/router.dart:694-699, D-666) and the role surfaces exi … |
| `FR-105-superadmin-totp-self-reset` | SIMF-Implementation-Gap-Report.md | src/ControlPanel/SIMF.ControlPanel/Components/Pages/Account/Profile.razor:37-44 offers Disable and Re-enrol (StartSetupAsync) to the signed-in admin while 2FA is enabled, with the confirm-code flow at lines 50-83 — a self-service  … |
| `FR-1201-nav-permission-filter` | SIMF-Implementation-Gap-Report.md | src/ControlPanel/SIMF.ControlPanel/CpNavigation.cs:27-42 gives every NavItem a RequiredPermission plus an IsPermittedFor(permissions, hasAllPermissions) predicate described as 'the single source of truth for nav visibility — share … |
| `FR-1202-permission-editor` | SIMF-Implementation-Gap-Report.md | src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/RolePermissionsEditor.razor is the per-permission grant editor (backed by tests/SIMF.Api.Tests/RolePermissionsEndpointsTests.cs); src/ControlPanel/SIMF.ControlPanel/Compone … |
| `FR-1205-oplog-export` | SIMF-Implementation-Gap-Report.md | src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/OperationLogViewer.razor:73-76 has an Export button gated on PermissionCatalog.OperationLog.Export, and lines 49-61 provide the From/To date-range filter inputs. |
| `FR-212-bulk-pending-staff` | SIMF-Implementation-Gap-Report.md | src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/PendingStaff.razor:31-38 wires OnApproveSelected/OnBulkApproveAsync and OnRejectSelected/OnBulkRejectAsync with a SelectAll label; lines 116-140 implement the shared-reason … |
| `FR-217-walkin-extras` | SIMF-Implementation-Gap-Report.md | src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/WalkInRegistrationForm.razor now has numbered sections for ProfileType (line 50), VIP (80), Identity (116), Organisation (215) and IdDocument (370) — the last a SimfFileUpl … |
| `FR-305-geofence-arrival` | SIMF-Implementation-Gap-Report.md | src/Backend/SIMF.Domain/Programme/Hall.cs:54-61 now carries the GeofenceCenterLat/GeofenceCenterLon/GeofenceRadiusMeters triple; src/Backend/SIMF.Application/Programme/Abstractions/IHallAttendanceService.cs:17-20 declares RecordGe … |
| `FR-405-cp-seat-grid` | SIMF-Implementation-Gap-Report.md | src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionSeatPlan.razor:86-102 renders a real seat grid ('P1.4 (D-215) — visual seat grid: render the hall layout when it is available, overlaying each reservation by (row, s … |
| `FR-407-speaker-presentations` | SIMF-Implementation-Gap-Report.md | src/Backend/SIMF.Domain/Programme/SpeakerPresentation.cs is the metadata entity backed by ISpeakerPresentationStorage; src/Backend/SIMF.Api/Endpoints/Admin/SpeakerPresentationEndpoints.cs exposes the admin routes (its test header  … |
| `FR-409-device-calendar` | SIMF-Implementation-Gap-Report.md | src/Mobile/simf_app/pubspec.yaml:45-50 adds add_2_calendar ^3.0.1 with the comment '"Add to calendar" for the session detail (Page_017 E4)'; src/Mobile/simf_app/lib/features/sessions/session_detail_screen.dart:265 implements _addT … |
| `FR-502-booking-overlap` | SIMF-Implementation-Gap-Report.md | src/Backend/SIMF.Infrastructure/SeatReservations/SeatReservationService.cs:1408-1428 defines EnsureNoOverlapAsync, which looks for an existing booking on another session whose time window overlaps and throws ErrorCodes.BookingOver … |
| `FR-504-cancel-before-start` | SIMF-Implementation-Gap-Report.md | src/Backend/SIMF.Api/Endpoints/Sessions/SeatReservationEndpoints.cs:162-181 defines ReleaseMySeatEndpoint, calling service.ReleaseMineAsync(sessionId, actorId). |
| `FR-506-attendance-tracking` | SIMF-Implementation-Gap-Report.md | src/Backend/SIMF.Domain/Programme/HallAttendance.cs is a real entity; src/Backend/SIMF.Infrastructure/Programme/HallAttendanceService.cs implements it; the CP surfaces it at src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admi … |
| `FR-605-venue-map` | SIMF-Implementation-Gap-Report.md | src/Mobile/simf_app/lib/features/venuemap/ now holds venue_map_screen.dart plus widgets/venue_map_geometry.dart, venue_map_marker.dart, venue_map_controls.dart, venue_map_booth_sheet.dart and venue_map_info_card.dart — a real 2D i … |
| `FR-704-question-arrival-gate` | SIMF-Implementation-Gap-Report.md | src/Backend/SIMF.Infrastructure/SessionQuestions/SessionQuestionService.cs:114-138 computes atVenue from a HallAttendance row once the session is live and the hall has a geofence, writes AuditEvents.SessionQuestionRejectedNotAtVen … |
| `FR-802-match-score` | SIMF-Implementation-Gap-Report.md | src/Backend/SIMF.Infrastructure/Recommendations/RecommendationService.cs:118-176 computes a real Jaccard match score over shared interests plus a same-ProfileType bonus and a shared-session count, then ranks by Score. The Connect  … |
| `FR-804-app-meeting-request` | SIMF-Implementation-Gap-Report.md | src/Mobile/simf_app/lib/features/speakers/data/speakers_repository.dart:49-61 posts to /app/speakers/{id}/meeting-requests (D-269); the form is src/Mobile/simf_app/lib/features/speakers/widgets/meeting_request_sheet.dart with vali … |
| `FR-805-faq-cp` | SIMF-Implementation-Gap-Report.md | src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/FaqManager.razor:1-14 is the real two-level manager ('a groups grid (top) and, when a group is selected via "Manage entries", its entries grid (below)'), on SimfDataGrid an … |
| `FR-807-live-sign-language` | SIMF-Implementation-Gap-Report.md | The delivered design changed: src/Backend/SIMF.Domain/Programme/Session.cs:149 carries LiveSignLanguageUrl (editable in CP via src/Backend/SIMF.Api/Endpoints/Admin/SessionEndpoints.cs:101,159 and the sessions XLSX importer), and s … |
| `FR-808-provider-abstraction` | SIMF-Implementation-Gap-Report.md | src/Backend/SIMF.Infrastructure/Ai/AiProviderRouting.cs plus the four provider classes in that folder; the admin routing UI at src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/AiRoutingEditor.razor edits per-prompt Provid … |
| `FR-902-session-reminders` | SIMF-Implementation-Gap-Report.md | src/Shared/SIMF.Common/Enums/NotificationKind.cs now carries SessionReminder=41, BookingConfirmed=40, MeetingScheduled=43, MeetingRequestConfirmed=50 and MeetingReminder=55. Senders exist: src/Backend/SIMF.Infrastructure/Operation … |
| `ai-provider-stub` | SIMF-Implementation-Gap-Report.md | src/Backend/SIMF.Infrastructure/Ai/ now contains EchoAiProvider.cs, OpenAiProvider.cs, AnthropicAiProvider.cs and GeminiAiProvider.cs; src/Shared/SIMF.Common/Enums/AiProvider.cs documents OpenAi/Anthropic/Gemini as 'outbound HTTP  … |
| `ai-settings-cp-page` | SIMF-Implementation-Gap-Report.md | The AI module is now five real pages under Components/Pages/Admin/ — AiDashboard.razor, AiServicesConsole.razor, AiServiceDetail.razor, AiPromptsList.razor, AiInvocationsLog.razor — all linked from CpNavigation.cs under Nav.Knowle … |
| `attendees-export` | SIMF-Implementation-Gap-Report.md | src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/AttendeesList.razor:77-80 has an Export button gated on PermissionCatalog.Attendees.Export. |
| `badge-qr-render` | SIMF-Implementation-Gap-Report.md | src/Mobile/simf_app/pubspec.yaml:53 pins qr_flutter ^4.1.0; src/Mobile/simf_app/lib/features/badge/widgets/badge_qr_card.dart:2 imports it and line 78 renders QrImageView. The same package backs src/Mobile/simf_app/lib/features/co … |
| `bookings-dormant-queue` | SIMF-Implementation-Gap-Report.md | src/ControlPanel/SIMF.ControlPanel/CpNavigation.cs:141 now points Module.Bookings at the real route /admin/bookings gated on PermissionCatalog.Bookings.View, not /m/bookings. src/ControlPanel/SIMF.ControlPanel/Components/Pages/Adm … |
| `cancellationtoken-propagation` | SIMF-Implementation-Gap-Report.md | Async service interfaces now carry the token: src/Backend/SIMF.Application/Programme/Abstractions/IHallAttendanceService.cs declares `CancellationToken cancellationToken = default` on every method (lines 22, 27, 32, 43, 54), and t … |
| `cicd-yaml-loadtest` | SIMF-Implementation-Gap-Report.md | azure-pipelines.yml exists at the repo root and builds, tests and publishes all three .NET apps plus a non-deploying Flutter analyze+test stage (header lines 1-25). The load-test script is tests/perf/k6-baseline.js with tests/perf … |
| `eir-03-04-external-services` | SIMF-Implementation-Gap-Report.md | Both were satisfied without a paid external dependency. Broadcast embed: src/Mobile/simf_app/lib/features/live/widgets/live_video_player.dart (YouTube iframe). Map: a self-hosted 2D venue map — src/Mobile/simf_app/lib/features/ven … |
| `live-video-provider-procurement` | SIMF-Implementation-Gap-Report.md | Resolved for the proof of concept by choosing YouTube, which needs no procurement or keys: src/Mobile/simf_app/lib/features/live/widgets/live_video_player.dart:7,78 uses youtube_player_iframe (pinned at pubspec.yaml:63) against th … |
| `live-video-stub` | SIMF-Implementation-Gap-Report.md | src/Mobile/simf_app/lib/features/live/widgets/live_video_player.dart:7 imports youtube_player_iframe and lines 78-193 build a real YoutubePlayerController from the session URL; src/Mobile/simf_app/pubspec.yaml:63 pins youtube_play … |
| `mobileapprole-picker` | SIMF-Implementation-Gap-Report.md | src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ProfileTypes/ProfileTypeForm.razor:55-64 renders the D-161 MobileAppRole picker (shown for UserType=Other; Visitor types resolve to MobileAppRole.Visitor at JWT time); the  … |
| `per-user-edit-page` | SIMF-Implementation-Gap-Report.md | src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/UsersAddEdit.razor:1-12 (D-353) is a real reusable Add/Edit form hosted by CrudShell from UsersList, inheriting CrudAddEditFormBase<AdminUserSummary>: Add creates a CP admi … |
| `permission-system-headline` | SIMF-Implementation-Gap-Report.md | src/Shared/SIMF.Common/PermissionCatalog.cs now declares 272 `new(...)` catalogue entries (was 6). Enforcement is pinned by tests/SIMF.Api.Tests/PermissionEnforcementTests.cs and tests/SIMF.Api.Tests/BusinessFlow13PermissionMatrix … |
| `push-login-api-branch` | SIMF-Implementation-Gap-Report.md | `git remote -v` in D:/swt-fix resolves origin to https://dev.azure.com/Zagali-KSA/SIMF/_git/SIMF, and origin/main is at db164ac4 'Merged PR 252: PR' — the work was pushed and merged through the PR pipeline long ago. |
| `sev-1-3-dbcontext-split` | SIMF-Implementation-Gap-Report.md | The split that matters shipped as D-157: src/Backend/SIMF.Infrastructure/Persistence/SimfAppDbContext.cs and SimfIdentityDbContext.cs are two separate contexts over two physically separate databases (recorded as permanent in CLAUD … |
| `test-coverage-backfill` | SIMF-Implementation-Gap-Report.md | tests/SIMF.ControlPanel.Tests/ now holds 81 files; tests/SIMF.Web.Tests/ holds a per-page suite of 24 files (AboutPageTests, ProgrammePageTests, SpeakersPageTests, VenuePageTests, VisitPageTests, PublicSiteRoutesTests and so on);  … |
| `OA-05-requester-email` | SIMF-Owner-Acceptance-Round-2026-07.md | D:/swt-fix/src/Backend/SIMF.Infrastructure/MeetingRequests/MeetingActionTokenService.cs:233-253 -- the speaker's own Approve/Reject decision now dispatches MeetingRequestConfirmed / MeetingCancelled to the requester with `SendEmai … |
| `OA-D2` | SIMF-Owner-Acceptance-Round-2026-07.md | D:/swt-fix/src/Backend/SIMF.Infrastructure/MeetingRequests/SpeakerMeetingRequestService.cs:749-779 adds EnsureSpeakerConfirmationIsDeliverableAsync, which throws a bilingual 409 MEETING_LINKS_NOT_CONFIGURED (line 758) or SPEAKER_M … |
| `BASE-PERMCAT` | SIMF-QA-Report-2026-07-24.md | I RAN IT. `dotnet test tests/SIMF.Application.Tests` on D:/swt-fix returns "Failed: 0, Passed: 63, Skipped: 0, Total: 63". The fix is visible in D:/swt-fix/tests/SIMF.Application.Tests/IdentityAccess/PermissionCatalogBaselineTests … |
| `BASE-RED-API` | SIMF-QA-Report-2026-07-24.md | I RAN THEM. `dotnet test tests/SIMF.Api.Tests --filter "FullyQualifiedName~BusinessMeetingsTests\|~SpeakerAvailabilityTests\|~IdentitySeederTests\|~SqlContentSeederTests"` on D:/swt-fix returns "Failed: 0, Passed: 61, Skipped: 0,  … |
| `BASE-RED-GOLDENS` | SIMF-QA-Report-2026-07-24.md | I RAN IT. `flutter test` in D:/swt-fix/src/Mobile/simf_app finished "02:04 +1247: All tests passed!" — 1247 tests, zero failures, goldens included (the run visibly executes sign_up_visitor, speakers, speaker_profile, sponsors, sta … |
| `DEF-001` | SIMF-QA-Report-2026-07-24.md | Fixed by commit 6d3819d8. D:/swt-fix/tests/SIMF.ControlPanel.Tests/SIMF.ControlPanel.Tests.csproj lines 22-45 carry a long rationale comment and `<NuGetAuditSuppress Include="https://github.com/advisories/GHSA-pgww-w46g-26qg" />`; … |
| `DEF-003` | SIMF-QA-Report-2026-07-24.md | Both guards are present. SponsorsFeed.cs lines 28-34 carry the comment "Groups is non-nullable on the contract, but System.Text.Json can still..." and the spread `.. (sponsors.Groups ?? []).SelectMany(...)`. PublicEditions.cs line … |
| `DEF-CP-NUM` | SIMF-QA-Report-2026-07-24.md | Confirmed in both files. D:/swt-fix/src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/OrganizationProfilePage.razor.cs formats with CultureInfo.InvariantCulture at lines 76, 84, 85 and parses with it at line 147 (int, Numb … |
| `DEF-PRIV` | SIMF-QA-Report-2026-07-24.md | Confirmed present in D:/swt-fix/src/Backend/SIMF.Api/Endpoints/Admin/CreateUserEndpoint.cs. Line 44 guards `if (req.Roles is { Count: > 0 } && !CanAssignRoles())`, and CanAssignRoles() at lines 54-56 accepts either the `*` wildcar … |
| `GAP-BROWSER-CHECKS` | SIMF-QA-Report-2026-07-24.md | Both checks are now automated in a Playwright-for-.NET project that did not exist when the report was written: D:/swt-fix/tests/SIMF.E2E.Tests/ (CpElementSweepTests.cs, WebElementSweepTests.cs, ElementSweep.cs, QaStack.cs, CpPermi … |
| `GAP-CP-LIVE-QA` | SIMF-QA-Report-2026-07-24.md | Superseded by the WS2/WS4 element-sweep work landed after this report — commits 668c7ca0 ("fix(cp): WS2 — the browser sweep ran for real and found two defects on 92 of 97 pages"), 7916017f ("feat(qa): WS4 — the element sweep runs  … |
| `GAP-ENDPOINT-COVERAGE` | SIMF-QA-Report-2026-07-24.md | The residual scope the report deferred has since been worked as its own programme: D:/swt-fix/docs/tests/SIMF-BF-Coverage-Map-2026-07-29.md exists (143 KB, dated two days ago) from commit 7722b8c9 "docs(qa): map all 128 remaining  … |
| `OA-D2` | SIMF-QA-Report-2026-07-24.md | Fixed and strengthened beyond what the report asked for. D:/swt-fix/src/Backend/SIMF.Infrastructure/MeetingRequests/SpeakerMeetingRequestService.cs lines 378-389 now pre-flight the approve path via EnsureSpeakerConfirmationIsDeliv … |
| `#1` | SIMF-Round1-Held-Items-Plan.md | D:/swt-fix/src/Backend/SIMF.Infrastructure/Identity/IdentitySeeder.cs:296 now wraps the call: `if (hostEnvironment.IsDevelopment() \|\| demoOptions.Value.EnableDemoAccounts)`, with an else-branch logging "Demo-account seed skipped … |
| `#1b` | SIMF-Round1-Held-Items-Plan.md | D:/swt-fix/src/Shared/SIMF.Common/Options/DemoSeedOptions.cs:29 is now `public string DemoPassword { get; set; } = string.Empty;` with an explicit XML comment that there is no committed default. D:/swt-fix/tests/SIMF.Api.Tests/Com … |
| `#20` | SIMF-Round1-Held-Items-Plan.md | Option C landed exactly as recommended. D:/swt-fix/src/Backend/SIMF.Infrastructure/SeatReservations/SeatReservationService.cs:1753-1767 adds `EnsureSessionNotEnded(DateTimeOffset end)` throwing `ErrorCodes.BookingSessionEnded` 409 … |
| `#21` | SIMF-Round1-Held-Items-Plan.md | Option A landed. D:/swt-fix/src/Backend/SIMF.Infrastructure/SeatReservations/SeatReservationService.cs:1835-1882 introduces `InsertHoldWithinCapacityAsync`, which runs the capacity COUNT, the free-seat pick (`build`) and the INSER … |
| `#22` | SIMF-Round1-Held-Items-Plan.md | Option A landed. D:/swt-fix/src/Backend/SIMF.Infrastructure/MeetingRequests/SpeakerMeetingRequestService.cs:394-466 — comment header "FIX #22 (R-1 held item)" — wraps the hall bind, `SpeakerHasOverlappingMeetingAsync` (line 427),  … |
| `D-406` | programme-ws0, HEAD fcaa830b) | Closed by D-563. src/Shared/SIMF.Common/PermissionCatalog.cs:1185-1203 puts Gates.Operate (and Gates.ViewOwnReports) in both StaffAppPermissions and ModeratorAppPermissions, and src/Backend/SIMF.Infrastructure/Identity/JwtTokenSer … |
| `R1-4.2-SECRET-SCRUB` | programme-ws0, HEAD fcaa830b) | docs/tests/e2e/cp-auth-flow.md:41 now reads Password="[REDACTED - supply via SIMF_SuperAdmin__TempPassword]" and :44 reads Get-Totp '[REDACTED - supply via SIMF_SuperAdmin__TotpSecret]'. A base32-shaped scan (\b[A-Z2-7]{26,}\b) ov … |
| `live-video-provider-stub` | programme-ws0, HEAD fcaa830b) | Resolved for the PoC by D-349 with YouTube. src/Mobile/simf_app/pubspec.yaml:63 pins youtube_player_iframe: ^6.0.2; src/Mobile/simf_app/lib/features/live/widgets/live_video_player.dart:7 imports it; src/Mobile/simf_app/lib/feature … |
