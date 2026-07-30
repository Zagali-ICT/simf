# SIMF — comprehensive fix-all plan (all open defects, parallel tracks)

_Authored 2026-07-30 against `docs/tests/SIMF-Defect-Register-2026-07-30.md`._

## Scope

Every open item from the 194-defect consolidation. Nothing is dropped: each row
below is either **scheduled into a track**, **blocked on a named owner answer**,
or **listed as owner-action** with the reason no code change discharges it.

| Bucket | Count | Where it goes |
|---|---:|---|
| Open defects, distinct | **34** | Tracks A–F below |
| Closed since the register was written | 5 | verified in code, listed for completeness |
| Duplicate rows in the register | 3 | `OA-D1`, `OA-D5`, `OA-D6` each appeared twice |
| Owner-action | 37 | §Owner-action — I prepare runbooks only |
| Not-a-defect | 12 | closed, no work |
| Cannot-verify | 7 | needs a device / provider / external artefact |

**Register arithmetic corrected:** the register lists 42 STILL-OPEN. Five have since
been fixed (`DEF-SEC-001-txt`, `SEC-SMOKE`, `no-literal-secrets-txt`, `#2b`, `#2c` —
re-verified in code on 2026-07-30) and three are duplicate rows. 42 − 5 − 3 = **34 distinct**.

## Owner decisions taken 2026-07-30 (all questions answered before start)

| Q | Item | Decision |
|---|---|---|
| Q1 | `#2` 2FA rollout | **Enrolment-first.** CP admin with 2FA off gets an enrolment challenge, not a token. Nobody is locked out. Unblocks A1→A5. |
| Q2 | `#8` time storage | **Full conversion + back-fill.** Owner chose this over my recommendation to keep UTC. I flagged that a partial conversion renders every instant 3h early with no way to distinguish converted rows; owner accepted. Mitigations below are mandatory, not optional. |
| Q3 | `OA-D1` Arabic names | *Decided by me:* greet the full trimmed name (the `Text` already has `maxLines:1` + ellipsis). No string surgery on compound given names. |
| Q4 | `cp-stub-modules` | *Decided by me:* the security half is unconditional — an `IsStub` nav item has `RequiredPermission = null` and is visible to every signed-in admin. Gate or remove the 8 stubs. Building a Live Sessions console is out of scope this round. |
| Q5 | `#33` CP manual | **Deferred** until after the code work. Stays open. |
| Q6 | Geofence / `FR-1103` | **Build CP-configurable.** App screen + arrival path now, plus a CP page for per-hall lat/lon/radius. Feature stays inert until a hall is given a boundary. Unblocks D4 + C8 without G-OI-2. |
| Q7 | `OA-D6` programme filter | *Decided by me:* build it. Additive nullable query param, `CacheOutput` already varies by query key. |
| Q8 | `FR-803` / `FR-903` | *Decided by me:* build to the stated 80% threshold. Additive `NotificationKind` values are permitted under D-110. |
| Q9 | `FR-702` georestriction | **Dropped.** Not achievable against a public YouTube feed (D-349). Closed with that rationale rather than shipping a control that looks like enforcement but is not. |
| Q10 | `#29` workshop CP | **Reuse the session admin.** No new CP surface. App half still built. |
| Q11 | `#16` sweep scope | *Decided by me:* shared `app/widgets` layer is **in** scope. |
| Q12 | `FR-1203` brand colours | **Closed.** `theme.tokens.css` stays the single source of truth per the project's own CSS rules. No code. |
| Q13 | `sev-1-6` placement | *Decided by me:* re-confirm the past-event deferral in writing rather than let it be quietly forgotten. No move. |
| Q14 | `SEC-NOTES` `myComment.txt` | *Decided by me:* `git rm --cached` + `.gitignore` — untracked, local copy preserved. Non-destructive and reversible; the file is owner-authored and other work references it. |
| Q15 | `sev-1-1` domain purity | **Correct the plan + add `DomainPurityTests`.** No POCO split, no freeze-lift. |

### Q2 mandatory mitigations (owner accepted the risk; these reduce it)

1. Verified database backup of both `SIMF_App` and `SIMF_Identity` **before** the back-fill runs.
2. The back-fill is a single idempotent migration carrying a marker row, so re-running it cannot
   double-shift. Without a marker there is no way to tell a converted row from an unconverted one.
3. Dry-run on the QA LocalDB first; row-count and spot-check a known instant before touching anything else.
4. Runs **last, alone**, after every other track has merged.

## Per-item definition of done (CLAUDE.md §17, D-246)

No item is "done" until all five land **in the same commit**:

1. The fix, at root cause.
2. Unit + integration test that fails without the fix.
3. E2E catalogue file created or updated under `docs/tests/e2e/` + indexed in its README.
4. Docs updated (`PAGE-INDEX.md` + the per-page reference doc) where a page/API changed.
5. New CP page or admin action → permission added to `PermissionCatalog`, seeded, gating
   **both** API and CP (`CpNavigationPermissionTests` / `PermissionEnforcementTests` fail otherwise).

Then: review agents + `simplify` before commit. Push per track. Full suite at the end.

## Parallelisation

Tracks are cut so their file sets are disjoint. Within a track, order is fixed.

| Track | Items | Runs | Why it can't merge with another |
|---|---:|---|---|
| **A — Auth & security** | 5 | serial, in my own context | All five touch `SignInService` / `JwtTokenService` / `AdminAccountService`. Security-critical; a lockout bug here bricks the CP. `#2 → #2d → exit-gate` is a hard chain. |
| **B — Time storage** | 1 | serial, alone, LAST | `#8` rewrites 318 call sites in 106 files plus a data back-fill. It conflicts with every other track by construction. |
| **C — API features** | 8 | parallel subagents | Disjoint endpoint/service files. |
| **D — Flutter app** | 11 | parallel subagents | Disjoint widget files. Goldens re-lock serially at track end. |
| **E — CP & Website** | 4 | parallel subagents | Disjoint Razor pages. |
| **F — Docs, arch, CI** | 5 | parallel subagents | Docs + two architecture refactors. |

C, D, E, F run concurrently. A runs alongside them in my context. B runs after everything
else has merged, so its sweep is applied once to settled code.

---

## Track A — Auth & security (serial, mine)

Blocked to start on **Q1**.

### A1 · `#2` — CP sign-in mints a full token on the password alone
- **Sev** high · **Eff** L
- **Root cause** `SignInService.cs:175` — `if (!user.TwoFactorEnabled)` issues tokens with no
  branch on `request.Audience == SignInAudience.Cp`.
- **Fix** Before the fast path, branch on the Cp audience: mint an *enrolment* ticket instead of
  a verification one, and add a CP enrolment page consuming the existing `TotpSetupEndpoint` /
  `TotpConfirmEndpoint`. `TotpPairing.razor` only re-renders an existing secret; it cannot create one.
- **Tests** Cp-audience password-only sign-in returns an enrolment challenge, not tokens; App
  audience unaffected; enrol-then-complete issues a token stamped `amr=mfa`.
- **E2E** `docs/tests/e2e/cp-2fa-enrolment.md` (new).
- **Permission** none (auth surface, pre-token — belongs to the anonymous surface; must be added to
  the reviewed allow-list in `BusinessFlow13PermissionMatrixTests`).

### A2 · `#2d` — admin creation does not force `TwoFactorEnabled`
- **Sev** medium · **Eff** M · **Hard-blocked on A1.**
- **Root cause** `AdminAccountService.CreateAccountAsync` never sets the flag.
- **Why it cannot ship first** `SignInService.cs:202` selects the factor as
  `authenticatorKey != "" || roles.Count > 0 ? Totp : EmailOtp`. A new admin has a role and no
  key, so it challenges TOTP against a secret that does not exist. Permanent lockout at creation.
- **Tests** New admin is created with the flag set **and** can complete first sign-in via enrolment.

### A3 · `exit-gate-no-open-high` — charter exit criterion unmet
- Closes when A1 + A2 land. Verification-only: re-run the held-items check and update
  `SIMF-Round1-Held-Items-Plan.md`.

### A4 · `OP-SUPERADMIN-SEED` — seed fails silently on a policy-violating password
- **Sev** medium · **Eff** S
- **Root cause** `IdentitySeeder.cs:595-602` logs and returns `null`; caller at `:140-143` returns.
  App boots with **no super-admin**. `Program.cs:454-460` only guards the exact committed default
  string, so a custom bad password sails past.
- **Fix** Throw in Production when `!result.Succeeded`, including `result.Errors`.
- **Tests** Seeder throws on a policy-violating password in Production; still logs-and-skips in Development.

### A5 · `itokenissuer-extraction` — token minting duplicated across auth paths
- **Sev** low · **Eff** M · Runs last in the track (A1 changes the same code).
- **Fix** Extract `ITokenIssuer` in Application; route device-key, password and badge-QR sign-in
  through it so the claim set and D-443 lifetime caps cannot drift.
- **Tests** All three entry points produce an identical claim set and identical lifetime caps.

---

## Track B — Time storage (serial, last, alone)

### B1 · `#8` — Saudi wall-clock storage
- **Sev** medium · **Eff** L · **Blocked on Q2.**
- **State** Storage is UTC: `BaseEntity.cs:15-17`, `AuditStampingSaveChangesInterceptor.cs:51`;
  `GetUtcNow()` appears **318 times in 106 files**. Only the display seam shipped (`SaudiTime.cs`).
  Migration `20260725083434_RenameAppUtcColumnsToLocal` dropped the `*Utc` suffixes **without**
  changing the stored offset — so the column names now understate that the values are UTC.
- **My recommendation** is in Q2. This is the single riskiest item in the programme: a partial
  conversion renders every stored instant 3 hours early, and there is no way to tell a converted
  row from an unconverted one after the fact.

---

## Track C — API features (parallel)

| # | Ref | Sev | Eff | Fix | Blocked on |
|---|---|---|---|---|---|
| C1 | `#10-phase4` | med | M | Extend `BadgeActivationCompleteRequest` with name/nationality/interests; fill the placeholder `UserProfile` in `CompleteActivationAsync`; add the capture step to `badge_activation_screen.dart` before it routes to `signIn`. No Identity schema change. | — |
| C2 | `OA-D5` | low | M | Append `CheckedInAt` + `CheckedInByName` to `AdminSpeakerMeetingRequestRow` (append-only, D-219), project in the list query, add two `GridExcelColumn` entries; mirror for delegation meetings + new `PermissionCatalog.*.Export` code. | — |
| C3 | `OA-D6` | low | M | Add `Guid? CategoryId` to `ListProgrammeSessionsRequest`, thread into `IProgrammeSessionService.ListAsync`. `CacheOutput("PublicRead")` already varies by query key. | Q7 |
| C4 | `FR-903` | low | M | Additive `NotificationKind.SessionNotAttended`; dispatch from a worker at Start+N to reservation holders with no `HallAttendance` row. | Q8 |
| C5 | `FR-803` | low | M | Normalised score threshold in `RecommendationService`; additive `NotificationKind.MatchRecommended`; poll worker with a per-pair dedup stamp. | Q8 |
| C6 | `FR-702` | low | M | Gate the live URL server-side on resolved `Region`. | Q9 |
| C7 | `sms-whatsapp-channels` | low | L | Introduce `INotificationChannel`; in-app + email as the first two implementations. Gateways deferred to procurement. | — |
| C8 | `FR-1103` | low | L | Device-position ping table + dwell aggregation + route projection. | Q6 + D6 |

---

## Track D — Flutter app (parallel; goldens re-lock serially at track end)

| # | Ref | Sev | Eff | Fix | Blocked on |
|---|---|---|---|---|---|
| D1 | `#29` | med | M | Branch on `SessionType.workshop` in `session_detail_body.dart`; render title + time only. Re-lock goldens. | Q10 (CP half only) |
| D2 | `#40-residual` | med | S | `splashEventLine` → `OrgProfile.eventDateRange(isArabic)` with the literal as fallback; **delete** `wwwroot/speakers.html` (627-line legacy page superseded by Blazor `/speakers`). | — |
| D3 | `OA-D1` | med | S | Arabic compound given names. | **Q3** |
| D4 | `geofence-self-checkin` | med | L | Arrival/departure repo method + "I'm here" action rendering `HallAttendanceStatus`. | **Q6** |
| D5 | `#16` | low | M | Sweep 4 remaining feature files onto `SimfTokens.surface`. | Q11 |
| D6 | `PAR-B4` | low | S | Skip the `fullName` Text when `fullName.trim() == name.trim()` in `booth_company_header.dart`. | — |
| D7 | `PAR-D3` | low | S | Category pill under the header-card title bound to `detail.localizedCategory(isArabic)`; fix the stale doc comment at `session_detail_screen.dart:38`. | — |
| D8 | `PAR-P1a` | low | S | Pill label `maxLines: 2` with tighter line-height (pill is 48h, two 12px lines fit). Re-confirm vs frame `1049:12629` first. | — |
| D9 | `PAR-P4a` | low | S | Star glyph beside/instead of the `المضيف` marker when `speaker.role == SessionSpeakerRole.host`; else correct the stale claim at `speaker_list_card.dart:14-17`. | — |
| D10 | `STALE-GOLDEN-ARTIFACTS` | low | S | `git rm -r --cached src/Mobile/simf_app/test/golden/failures/` + `.gitignore`. Generated output, never an input. | — |
| D11 | `accessibility-server-sync` | low | M | Five flags into the profile-preferences DTO; write-through on change, hydrate at sign-in, local prefs stay as offline cache. | — |

---

## Track E — Control Panel & Website (parallel)

| # | Ref | Sev | Eff | Fix | Blocked on |
|---|---|---|---|---|---|
| E1 | `QA-LIVE-001` | low | S | Add `favicon.ico` to CP `wwwroot/` + `<link rel="icon">` in `App.razor`. 404s on every CP page today. | — |
| E2 | `cp-stub-modules` | low | M | 8 of 22 modules still `ModulePlaceholder`. **Security angle:** an `IsStub` item has `RequiredPermission = null`, so every signed-in admin sees it regardless of role. | **Q4** |
| E3 | `FR-1203-brand-colour-tokens` | low | M | Brand-colour editing from CP. | Q12 |
| E4 | `FR-1203-markdown-render` | low | S | Render server-side through a **sanitizing** pipeline, or drop the "markdown allowed" claim from `ContentBlock`'s XML doc. Never render unsanitised HTML from an admin-editable field. | — |

---

## Track F — Docs, architecture, CI (parallel)

| # | Ref | Sev | Eff | Fix | Blocked on |
|---|---|---|---|---|---|
| F1 | `#33` | high | L | 14 `_(planned)_` chapters in `docs/manuals/Admin-Manual.md` (1118 lines, partial). Unwritten: Registration requests, Attendees, Roles & permissions, Halls & seating, Speakers, Bookings, all of Exhibition, Engagement, Knowledge & AI, Content. | **Q5** |
| F2 | `sev-1-1-domain-purity` | low | L | Domain depends on ASP.NET Identity (`SimfUser : IdentityUser<Guid>`) while the refactor plan claims it closed. | **Q15** |
| F3 | `sev-1-6-service-placement` | low | L | `AdminAccountService` in Infrastructure, not Application. Plan defers past the event. | Q13 |
| F4 | `SEC-NOTES` | low | S | `myComment.txt` still tracked (verified 2026-07-30). | Q14 |
| F5 | `catalogue-count-drift` | low | S | Re-derive the "164-file / 2,142-scenario" counts; have `build_testbook.py` emit them. | — |

---

## Owner-action — 37 items, no code change discharges them

I will prepare a **runbook** for each where one helps (rotation steps, provisioning checklist,
CI diff), but I cannot close any of these.

**Critical (4)** — `SEC-TLS` mobile global TLS trust-all disabling MITM protection app-wide ·
`DEF-SEC-001-rotate` + `committed-secrets-rotation` (the credentials I removed remain in git
history) · `nca-mod-clearance` hard go-live gate.

**High (11)** — `PROC-CI` / `entry-gate-ci-tests` (test task `enabled: false` at
`azure-pipelines.yml:221,246`) · `app-store-publication` · `#31` forum content · `#5` hall seat
layouts · `uat` · `OA-D4` AI is the Echo stub out of the box · `prod-secrets-provisioning` ·
`SEC-AIKEY` · `SEC-APPSET`.

**Medium (9)** — AI provider keys · `SEC-AIANON` anonymous AI endpoints · QuestPDF licence above
the revenue threshold · `PAR-B1` booth officer block · `PAR-X2` zero media assets in prod · `#1`
per-day programme images · `live-render-not-captured`.

**Low (13)** — D6 statistics metric list · session-category list · grey-theme ruling ·
SIEM deployment · SMS/WhatsApp gateways · SignalR hub · AI question-filter stub · and six design
rulings (`#15`, `#7`, `#11`, `#22`, `#23`, session↔ProgrammeDay FK).

## Cannot verify (7)

`mobile-manual-only` (emulator renders SurfaceView black) · `OA-09-emulator-youtube` (needs a real
handset) · `PAR-P3a`, `PAR-P5a` (device render) · `GAP-UNTESTED-SCOPE` (Lighthouse / load / pentest
produce external artefacts) · `#35-#39/#41` (referenced by number, no text in the source document) ·
`48-pre-existing-failures` — **worth reconciling:** a prior baseline concedes 48 pre-existing
full-suite failures, but my run on 2026-07-30 was 2076 passed / 0 failed. One of the two is wrong.

## Execution loop

Per item, per track: fix → unit+integration test → E2E catalogue → docs → review agents +
`simplify` → commit (targeted `git add` by path, never `-A`, verify `git diff --cached`).
Push per track as it completes. Full suite + Flutter analyze/test at the end.
