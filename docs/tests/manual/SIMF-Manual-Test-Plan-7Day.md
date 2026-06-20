# SIMF — 7-Day Manual Production-Rehearsal Test Plan (Control Panel + Mobile App)

| Field | Value |
|-------|-------|
| Title | Manual (human) production-rehearsal test plan |
| Status | Living plan — subordinate to `SIMF-TST-001-Test-Plan.md` |
| Surfaces | Control Panel (`https://simf.zagali-ict.com` admin) + Mobile App (Flutter, prod API) |
| Team | 3–4 testers + 1 QA Lead (the Lead may also be one of the 4) |
| Window | 7 working days |
| Environment | **LIVE production** (`simf_api` / `simf_app` / `simf.zagali-ict.com`, self-signed TLS) |
| Gate simulation | **Both** in-app scanner **and** a physical-device dry-run |
| Devices | Android phone · Huawei / no-GMS Android · Android tablet · iOS |
| Companion cases | [`SIMF-Manual-Test-Cases.md`](SIMF-Manual-Test-Cases.md) |
| Catalogue | [`../e2e/README.md`](../e2e/README.md) (74 pages / ~1,044 scenarios) |
| Last reviewed | 2026-06-20 |

---

## 1. Purpose & what "done" means

Prove, by **human hands on the real product**, that an attendee can be registered,
approved, badged, **let through the gate (in and out)**, seated, engaged, moderated
and reported on — and that every **constraint** (validation rule) and every
**policy** (permission / role / account-state gate) holds, in **both Arabic (RTL)
and English**, on **all four device classes**. The pass is "done" when:

- every **P0** case in [`SIMF-Manual-Test-Cases.md`](SIMF-Manual-Test-Cases.md)
  is **PASS**, and
- every **P1** is PASS **or** has a logged defect with an owner, and
- the **gate check-in/out** journey (`TC-J-03 … TC-J-05`) passes in-app **and**
  on the physical-device dry-run, and
- the **role × permission policy matrix** (`TC-P-*`) shows no over-grant
  (no signed-in user reaching a page/action they lack), and
- the **bilingual / RTL** sweep and the **device matrix** sweep are clean, and
- the **QA Lead and Project Owner sign §11**.

This plan does **not** replace the security pen-test, the performance/load test, or
formal UAT sign-off owned by `SIMF-TST-001` §§9–12 — it **feeds** them.

---

## 2. ⚠️ Live-production Rules of Engagement (READ FIRST — binding)

You chose to test against **live production**. That is realistic, but it means
**real data, real users, real audit trails, and no automatic reset**. These rules
are mandatory; a breach is a stop-the-line event.

1. **Never use real attendee PII.** Every account, name, email, phone, ID and
   company you create is **synthetic** and **tagged** so it is findable and
   removable. Use the naming convention in §6.2 (`QA-` prefix / `qa+...@` emails).
2. **No destructive action on data you did not create.** Do **not** delete,
   deactivate, reject, or bulk-edit any real row. Catalogue scenarios whose
   "golden path" ends in *Deactivate / Delete / Reject* are run **only on a row
   you created this session** — never on seeded or real content. Cases that
   cannot be made safe on prod are marked **`PROD-SKIP`** in doc 2 and run later
   on staging/local instead.
3. **Cleanup register is mandatory.** Every created row is written to the
   **Cleanup Register** (§10) at creation time, and removed (soft-delete) at the
   end of its test day. Test data left in production after Day 7 is a **go-live
   defect** (`SIMF-TST-001` §7; SES-001 §12).
4. **Low-traffic windows for writes.** Schedule create/approve/scan bursts and
   any load-ish repetition outside real user peak hours; coordinate with the
   DevOps owner before any bulk-generate (e.g. delegate badge bulk-generate).
5. **No literal secrets anywhere.** TOTP for the super-admin is generated at run
   time via the `Get-Totp` helper; OTPs are read from the system, never written
   into a log, screenshot, or chat (`SIMF-TST-001` §7; E2E plan §3.5). Redact
   tokens/Q*R* payloads in screenshots.
6. **Self-signed TLS is expected** on `simf.zagali-ict.com`. Browsers/devices will
   warn; that is the current production posture, not a test defect — but **record
   it** (it ties to the open security finding on TLS trust).
7. **Two known OPEN security findings touch this run** (treat as context, verify,
   do **not** "fix" mid-test): committed live secrets in
   `appsettings.Development.json`, the Flutter release TLS-trust-all, and the
   **default super-admin credentials**. The test **confirms** these as findings
   (a row in the defect log), it does not remediate them here.
8. **Pipeline ≠ this branch.** Production is built from `main`. The latest CP/app
   work sits on `feature/app-cp-api-split` and is **pushed but not deployed**.
   Before Day 1, the QA Lead confirms with the owner **which build is live** so
   the team tests what is actually deployed — not a newer un-deployed feature.
   Any case that targets an un-deployed feature is marked **`NOT-DEPLOYED`** and
   parked.

---

## 3. The team model (3–4 testers + Lead)

With a small team, run **three lanes** and rotate. The Lead coordinates, owns the
defect log and the cleanup register, and floats.

| Lane | Owner | Scope | Primary devices |
|------|-------|-------|-----------------|
| **Lane A — Control Panel** | Tester 1 (admin-minded) | All `/admin/*` CP pages, the permission/policy matrix, config, statistics, logs | Laptop (Chrome + Edge) |
| **Lane B — Mobile App** | Tester 2 (+ Tester 4 if present) | All app screens, the device matrix, badge/QR, on-device gate scanning | All 4 device classes |
| **Lane C — Cross-journey + RTL** | Tester 3 | The end-to-end **journeys** (`TC-J-*`), the **gate check-in/out** simulation (needs CP + app together), the Arabic/RTL sweep | Laptop + 1 phone + 1 tablet |
| **QA Lead** | Lead | Schedule, fixtures, defect triage, cleanup register, daily report, sign-off | Laptop |

- **If only 3 testers:** Lane B owner also covers Lane C's app-side steps; the
  Lead takes the RTL sweep.
- **Pairing for the gate journey:** the gate check-in/out simulation (`TC-J-03/04`)
  needs one person **scanning on the app/operator console** and one watching the
  **gate dashboard / hall-arrivals / attendance** update live — run it as a pair
  (Lane B + Lane C) on Day 2.
- **Arabic-native tester:** SIMF is Arabic-first. Assign the RTL sweep to whoever
  reads Arabic; the bilingual toast/label text must be judged by a native reader.

---

## 4. The 7-day staged schedule (risk-ordered)

Each day = **morning run → afternoon run → 30-min end-of-day defect triage +
cleanup**. Cases live in [`SIMF-Manual-Test-Cases.md`](SIMF-Manual-Test-Cases.md);
the "Catalogue" column points at the per-page files under [`../e2e/`](../e2e/README.md).

### Day 1 — Prep + Auth & Registration lifecycle
*Goal: the stack is testable, the fixtures exist, and every door into the system works.*

| Block | Work | Cases | Catalogue |
|-------|------|-------|-----------|
| AM | **Stand-up (§6):** confirm live build, create all role fixtures, brief the team, open the run-logs + cleanup register | — | — |
| AM | Visitor sign-up → email-OTP → Terms → registration success → **pending** | `TC-J-01`, `TC-V-01…06` | `mobile-sign-in/-sign-up-form/-email-otp/-sign-up-visitor/-sign-up-interests/-terms/-registration-success/-registration-status` |
| PM | Admin **approve / reject** the pending visitor; account-state routing (pending / rejected / disabled) | `TC-J-02`, `TC-P-07` | `cp-admin-visitors-pending`, `cp-admin-visitors`, `mobile-registration-status`, `web-account-pending/-rejected` |
| PM | Sign-in 2FA: **visitor email-OTP** vs **admin TOTP**; forgot/reset password; **token caps** (5-min access / 24-h session warning modal) | `TC-J-01b`, `TC-V-30…33` | `cp-auth-flow`, `web-login/-otp-verify/-forgot-password/-reset-password`, `mobile-sign-in` |

### Day 2 — Badge & Access Control = **the gate check-in / check-out simulation**
*Goal: prove the physical-event flow end to end, in-app and on a real device.*

| Block | Work | Cases | Catalogue |
|-------|------|-------|-----------|
| AM | CP gate **setup**: create a test gate (direction **Both / In / Out**), set the ProfileType **allow-list**, **assign** the staff operator | `TC-J-03a`, `TC-P-05` | `cp-admin-gates` |
| AM | **Badge QR**: approved visitor opens the app badge (#32); confirm the QR + the holder data | `TC-J-03b` | `mobile-badge` |
| PM | **Gate check-IN** (Allowed) and **check-OUT** (Both-mode direction) — **in-app scanner** (staff `gateScanner` #105) **and** **CP operator console** `/admin/gates/operator`; watch the **gate dashboard** + **GateScan** rows | `TC-J-03`, `TC-J-04` | `mobile-gate-scan`, `cp-admin-gates-operator`, `cp-admin-gates-dashboard` |
| PM | **Denials**: unknown QR, profile-type-not-allowed, not-approved holder, not-assigned operator (403), duplicate-within-5-s absorption, 429 rate | `TC-J-04b`, `TC-V-34…37` | `cp-admin-gates-operator` (GOP-004/005/010/011), `mobile-gate-scan` (MOBGATE-002/004) |
| PM | **Hall-door arrival** (session attendance): pick active session → scan badge QR → `HallAttendance`; idempotent re-scan; **attendance** report | `TC-J-05` | `cp-admin-hall-arrivals`, `cp-admin-attendance` |
| PM | **Physical-device dry-run** (§5): scan a printed/displayed badge with a real device at a mock gate; offline/poor-network behaviour | `TC-J-03p`, `TC-D-05…07` | `mobile-gate-scan` |

### Day 3 — Programme, sessions, seats & bookings
| Block | Work | Cases | Catalogue |
|-------|------|-------|-----------|
| AM | CP: themes, halls + seat-layouts, programme-days, session-categories, speakers, sessions, seat-plans | `TC-CP-PRG-*` | `cp-admin-themes/-halls/-halls-seat-layouts/-programme-days/-session-categories/-speakers/-sessions/-sessions-seat-plans` |
| PM | App: agenda/sessions (#16), session detail (#17), **my-seat reserve / release** (#18), speakers (#19), speaker profile + **meeting request** (#20) | `TC-J-06`, `TC-APP-PRG-*` | `mobile-agenda/-session-detail/-my-seat/-speakers/-speaker-profile` |
| PM | CP: bookings approval workflow, speaker-meeting-requests, meeting-tables, business-meetings, programme timeline | `TC-CP-PRG-*` | `cp-admin-bookings/-speaker-meeting-requests/-meeting-tables/-business-meetings/-programme-timeline` |

### Day 4 — Engagement, Q&A, moderation & feedback
| Block | Work | Cases | Catalogue |
|-------|------|-------|-----------|
| AM | App visitor: **send a question** (#26), **audience comments** + like (#28), **rate** per-element (#40), AI summary (#34), live broadcast (#25) | `TC-J-07`, `TC-APP-ENG-*` | `mobile-send-question/-audience-comments/-rate/-ai-summary/-live` |
| PM | **Moderation**: CP question-queue, `/sessions/{id}/moderate`, comments-moderation, ratings; **app moderator** session-moderate (#104 push/hide) | `TC-J-08`, `TC-P-03` | `cp-admin-question-queue/-session-moderate/-comments-moderation/-ratings`, `mobile-session-moderate` |
| PM | **Session-summary review/approval** workflow Draft → InReview → Approved ("ready for المحاور"); moderator/host read | `TC-J-09` | `cp-admin-session-summaries` |

### Day 5 — Exhibition, content/media & networking
| Block | Work | Cases | Catalogue |
|-------|------|-------|-----------|
| AM | CP: companies, exhibitors, booths, sponsors, media-partners, venue-map nodes | `TC-CP-EXH-*` | `cp-admin-companies/-exhibitors/-booths/-sponsors/-media-partners/-venue-map` |
| AM | App: booths (#22), sponsors (#23), venue map (#15), media-partners (#31) | `TC-APP-EXH-*` | `mobile-booths/-sponsors/-venue-map/-media-partners` |
| PM | CP: news, media gallery, archive (+ detail), banners, content-blocks, media-library | `TC-CP-CNT-*` | `cp-admin-news/-media/-archive/-banners/-content-blocks/-media-library` |
| PM | App: news (#29), gallery (#30), archive (#24/#24-01), about (#37); **networking** meet-people (#35) + **share-contact / scan-contact / my-contacts** (vCard) | `TC-J-10`, `TC-APP-CNT-*` | `mobile-news/-gallery/-archive/-archive-detail/-about/-meet-people/-my-contacts` |

### Day 6 — CP admin, system, config & the policy/constraint sweep
| Block | Work | Cases | Catalogue |
|-------|------|-------|-----------|
| AM | **Role × permission policy matrix** end to end: create limited roles, assign, and prove each gated page/action denies + the nav item hides | **`TC-P-01…12`** | `cp-admin-roles/-roles-permissions`, every page's auth-gate scenario |
| AM | People/accounts breadth: admins (+pending), others (+pending), visitors (+pending/VIP/export), delegates, attendees, print-bag, interests, profile-types, organisations, contacts, countries, VIPs, invitations, reset-2FA | `TC-CP-PPL-*` | the `People & accounts` block in `../e2e/README.md` |
| PM | System: configuration, site-settings, operations, operation-log, logs, statistics, AI prompts/invocations, FAQ | `TC-CP-SYS-*` | `cp-admin-configuration/-site-settings/-operations/-operation-log/-logs/-statistics/-ai-prompts/-ai-invocations/-faq` |
| PM | **Constraint / validation full sweep** (every rule in one pass) | **`TC-V-01…40`** | per-field, cross-referenced in doc 2 |

### Day 7 — Regression, RTL, device matrix & sign-off
| Block | Work | Cases | Catalogue |
|-------|------|-------|-----------|
| AM | **P0 regression sweep** of every journey + every defect's retest (fix-and-retest loop §9) | all `P0` | — |
| AM | **Bilingual / RTL sweep** — Arabic toggle on the highest-traffic 15 pages/screens: `dir="rtl"`, Arabic labels, mirrored nav, reversed action order, LTR-only fields stay LTR | `TC-I18N-*` | every page's `i18n` scenario |
| PM | **Device-matrix sweep** — the four classes against the app-critical screens (QR scan on Huawei/no-GMS, badge-QR cap on tablet, iOS build, standard Android) | `TC-D-01…08` | `mobile-*` |
| PM | **Final cleanup** (drain the register), consolidate the run report, **§11 sign-off** | — | — |

> **If a day overruns** (likely on a 3–4-person team running live): carry P2/
> resilience cases to a backlog, never P0/P1. Protect Day 2 (gate) and Day 7
> (regression + sign-off) — those are the spine.

---

## 5. Gate check-in / check-out — the simulation method (in-app **and** physical)

This is the headline of the rehearsal, so it gets its own procedure. Full step
cases are `TC-J-03 … TC-J-05` in doc 2; this is the operating context.

**Two distinct "gate" mechanisms exist — test both:**

1. **Venue gate (access control)** — entry/exit to the event.
   - Config: `/admin/gates` (define gate, **direction** `Both`/`In`/`Out`,
     **ProfileType allow-list**, **operator assignment**).
   - Scan: **app** `gateScanner` (#105, staff, `Gates.Operate` + assignment) **or**
     **CP** `/admin/gates/operator`.
   - Result: a scan returns **HTTP 200** with `Outcome = Allowed | Denied`,
     `Direction = CheckIn | CheckOut` (a `Both` gate **infers** direction from the
     last scan), and on denial a `DenialReasonCode` (`QrUnknown`,
     `ProfileTypeNotAllowed`, `HolderNotApproved`, …). A denial is **not** an HTTP
     error. A `GateScan` audit row is written (`Source = Simulator` for the
     console). Watch it live on `/admin/gates/dashboard`.
   - Edge engine to exercise: **5-second duplicate absorption**, **idempotency-key
     replay → 409**, **not-assigned → 403**, **rate-limit → 429**.

2. **Hall door (session attendance)** — arrival into a specific session's hall.
   - Console: `/admin/hall-arrivals` — pick an **active** session, scan the
     attendee **badge QR** (`MaxLength 64`) → opens/merges one **`HallAttendance`**
     row (`Method = QrScan`), idempotent re-scan. Errors: `ATTENDEE_QR_UNKNOWN`
     (400), `ATTENDEE_NOT_APPROVED` (403). Report on `/admin/attendance`.

**The physical-device dry-run (you chose "both"):**

- Print or display a **test** visitor's **badge QR** (from the app badge screen
  #32). Use a **synthetic** visitor only — never a real attendee's badge.
- On each **staff device class** (Android phone, Huawei/no-GMS, tablet), open the
  app `gateScanner`, scan the printed/displayed QR at a mock "gate", and confirm
  the same Allowed/Denied result and the live dashboard update.
- **Huawei / no-GMS:** the camera QR path uses the **disabled-GMS stub /
  fallback** — verify the **manual-entry** path (type the code → *Check*) works
  there, and that the scanner viewfinder renders (bounded-box composit­ing).
- **Poor / no network:** confirm the app's behaviour when the scan POST cannot
  reach the prod API (error surface + retry; no silent "allowed").
- Record gate-device readiness (camera permission, lighting, scan distance) — this
  doubles as venue-ops readiness.

---

## 6. Stage 0 — setup (Day 1 AM)

### 6.1 Confirm the target
- QA Lead confirms **which build is live** on `simf.zagali-ict.com` (see §2.8) and
  notes the deployed commit in the Day-1 run-log.
- Confirm both DBs reachable and `/health` green (the DevOps owner runs the remote
  health/curl recipe; testers do not need DB access except the OTP read, which the
  Lead/DevOps performs).

### 6.2 Create the role fixtures (all synthetic, all tagged)
Create these **once**, on Day 1, and reuse all week. Naming: display names start
`QA `, emails `qa+<role><nn>@<test-domain>`. Record every one in the Cleanup
Register.

| Fixture | How created | Used for |
|---------|-------------|----------|
| **Super-admin** | the seeded Administrator (`*` wildcard), TOTP via `Get-Totp` | every CP admin case; the over-grant baseline |
| **Limited admin** (role with **only** a few permissions) | `/admin/roles` → grant e.g. only `Visitors.View` → assign to a `QA-LimitedAdmin` account | the **auth-gate** half of every `TC-P` policy case |
| **Gate operator (staff)** | role with `Gates.Operate` (+`Gates.ViewOwnReports`), **assigned to the test gate**; app `AppRole.staff` | gate scan journeys, app `gateScanner` |
| **Session moderator** | per-session moderator grant (`/admin/session-moderators`); app moderator | Q&A moderation journeys |
| **Visitor — approved** | sign-up → approve | the golden visitor, badge, gate-in, seat, engage |
| **Visitor — pending** | sign-up, leave un-approved | account-state routing, gate **deny** (not-approved) |
| **Visitor — rejected / disabled** | approve-screen reject / disable | rejected/disabled routing |
| **VIP / VVIP** | `/admin/visitors/vip` (creates pending) | VIP journey + export roster |
| **Delegate** | `/admin/delegates` (visitor + IsDelegate + invited country; bulk-generate badges) | delegate journey + bulk-badge |
| **Guest** | no login (app guest mode #12) | public/anonymous reads |

### 6.3 Brief + open artefacts
- Walk the team through doc 2's case-id scheme and the PASS/FAIL/BLOCKED rule.
- Open one run-log per tester from the **template (§10)** under `run-log/`.
- Open the **Cleanup Register** and the **Defect Log** (templates §10).

---

## 7. Entry / exit gates

**Entry (before Day 1 testing):**
- The live target build is confirmed and known (§2.8, §6.1).
- `/health` is green; CP login + app login both reachable on prod.
- The role fixtures (§6.2) exist and the cleanup register is open.
- The lower automated layers are green on the deployed commit (the team takes the
  last recorded `dotnet test` / Flutter snapshot from the build that was deployed;
  a red lower layer is itself a Day-1 defect).

**Exit (pass) — per §1:**
- All P0 PASS; all P1 PASS-or-tracked; gate journey PASS in-app **and** physical;
  policy matrix shows no over-grant; RTL + device sweeps clean; **no open
  high-severity defect**; cleanup register drained; §11 signed.

A gate that does not pass **stops the work behind it** (`SIMF-TST-001` §14).

---

## 8. Priorities

| Priority | Meaning | Effect on sign-off |
|----------|---------|--------------------|
| **P0** | Core path / security gate / data-safety. Golden journeys, auth gates, gate scan allow/deny, approval, payments-of-record (attendance/badge) | **Must PASS.** A P0 FAIL blocks sign-off. |
| **P1** | Important function, validation, conflict, RTL | PASS, or a tracked defect with an owner + ETA. |
| **P2** | Resilience (server-500, throttle, double-submit), nice-to-have edges | Sampled at least once; logged + risk-assessed, does not alone block. |

Inherited from `SIMF-TST-001` §14 + E2E plan §6.2.

---

## 9. Defect management & the fix-and-retest loop

1. **Log** every FAIL immediately as a defect (template §10) with: case id, page/
   screen, device + language, **expected vs actual**, evidence (screenshot /
   network status / console), severity, and the catalogue id it breaches.
2. **Severity:** Critical (blocks a P0 / data-loss / security) → High → Medium →
   Low. Critical/High are triaged the **same day** by the Lead.
3. **Root cause, not symptom** (SES-001 §13). The fix carries a **regression test**
   at the lowest layer that can hold it (usually an `SIMF.Api.Tests` case or a
   widget test).
4. **Retest:** re-run the failing case, then re-run that page's **P0 set** to
   confirm no regression; flip the run-log cell to PASS with the new date + the
   fix reference.
5. A failing case is **never** weakened or skipped to go green (`SIMF-TST-001`
   §13). If a case cannot run on prod safely, it is `PROD-SKIP`, not "passed".

---

## 10. Templates (copy these into `run-log/`)

### 10.1 Daily run-log (one per tester per day)
```
# Run-log — <Tester> — Day <n> — <date> — Lane <A|B|C>
Build under test: <commit / version>   Env: PROD   Language(s): <ar|en|both>
Device(s): <phone | huawei | tablet | ios | laptop-chrome | laptop-edge>

| Case id | Title | Pri | Result | Evidence (path/note) | Defect id | Notes |
|---------|-------|-----|--------|----------------------|-----------|-------|
| TC-J-01 | Visitor sign-up→pending | P0 | PASS | shots/d1-tc-j-01-*.png | — | |
| TC-J-03 | Gate check-IN allowed | P0 | FAIL | shots/d2-tc-j-03.png | DEF-014 | denial card showed wrong name |
| ...     |       |     | BLOCKED| (why blocked)        | —        | waiting on fixture |

Summary: <n PASS / n FAIL / n BLOCKED / n SKIP>   Carried to backlog: <ids>
```

### 10.2 Defect (one row per defect, in the shared Defect Log)
```
| DEF-id | Date | Case id | Page/Screen | Device+Lang | Severity | Expected | Actual | Evidence | Status | Owner | Fix ref | Retest date |
```

### 10.3 Cleanup Register (every created prod row)
```
| Reg-id | Date | Created by | Entity | Identifier (QA-name / email / code) | Page created from | Removed? (date) |
```

> Drain the Cleanup Register at the end of **every** test day, and a final full
> drain on Day 7. Nothing `QA-`-tagged may remain in production after sign-off.

---

## 11. Sign-off

| Role | Name | Statement | Date | Signature |
|------|------|-----------|------|-----------|
| QA Lead | | "All P0 PASS; P1 PASS-or-tracked; gate journey PASS (in-app + physical); policy matrix clean; RTL + device sweeps clean; cleanup drained." | | |
| Lane A (CP) | | "CP scope complete per doc 2." | | |
| Lane B (App) | | "App scope complete across the device matrix." | | |
| Lane C (Journeys/RTL) | | "Journeys + RTL complete." | | |
| Project Owner (MoD / RSNF) | | "Accepted for go-live / next gate." | | |

Open high-severity defects at sign-off are listed here with the owner's
risk-acceptance or a remediation date.

---

## 12. Risks specific to this run

| Risk | Mitigation |
|------|------------|
| **Live-prod data pollution** | §2 rules of engagement + the Cleanup Register; `PROD-SKIP` for unsafe destructive cases. |
| **Testing an un-deployed build** | §2.8 — confirm the live commit Day 1; park `NOT-DEPLOYED` cases. |
| **Small team / 7 days** | Risk-ordered schedule; protect Day 2 (gate) + Day 7 (regression); carry only P2 to backlog. |
| **Default super-admin creds / TLS-trust / committed secrets** | Confirmed as defects (§2.7), not fixed mid-run; flagged to the owner at §11. |
| **Huawei/no-GMS QR camera** | Manual-entry fallback path is the primary gate-scan route on that class; verified in §5. |
| **Self-signed TLS warnings** | Expected; recorded, not raised as functional defects. |
| **No literal secrets** | `Get-Totp` for TOTP; OTP read by Lead/DevOps; redact QR/token in evidence. |

---

_Subordinate to `SIMF-TST-001-Test-Plan.md`. Living document — last reviewed
2026-06-20 by SIMF Team._
