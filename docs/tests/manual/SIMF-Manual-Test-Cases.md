# SIMF — Manual Test Cases (Human Execution) — Control Panel + Mobile App

> Companion to [`SIMF-Manual-Test-Plan-7Day.md`](SIMF-Manual-Test-Plan-7Day.md).
> These are the **human-runnable** cases: the cross-cutting **journeys** the
> per-page catalogue can't hold, the role × permission **policy matrix**, the
> **constraint / validation** matrix, the **device** + **RTL** sweeps, and the
> per-page **execution checklists** that route you into the
> [`../e2e/`](../e2e/README.md) catalogue for exact field/text/error detail.
>
> **Read the catalogue file named in each row** for the exact bilingual toast
> text, field names, max-lengths and error codes — this document gives the *human
> procedure and the assertions that matter*; the catalogue gives the *literals*.

## How to read a case

```
TC-x-nn  — <title>                                   [Pri] [Surface] [Catalogue]
Pre:    what must be true / which fixture
Steps:  do this …
Expect: assert this (the outcome that proves it works)
Safety: PROD note if any (PROD-SKIP / create-your-own-row / low-traffic)
```

Result values: **PASS / FAIL / BLOCKED / PROD-SKIP / NOT-DEPLOYED**. Record in
your day's run-log with evidence.

---

# Section A — Role × Permission **Policy** matrix (`TC-P`)

SIMF enforces a **per-page / per-action** permission system (D-207/D-208):
roles-only assignment, codes baked into the JWT, **`Administrator = "*"`
wildcard**. The single source of truth is `PermissionCatalog`. Every CP page is
gated `@attribute [RequirePermission(...)]`; every admin endpoint
`Policies(PolicyFor(...), RequireApprovedAccount)`. **The proof is the negative
case**: a signed-in user *without* a permission must (a) not see the nav item and
(b) land on `/not-permitted` (HTTP 200) — and the API must 403 a forged call.

Each `TC-P` is run **twice**: once as the privileged fixture (positive — the
action works) and once as a fixture that **lacks** the permission (negative — it
is blocked).

| ID | Policy under test | Positive fixture | Negative fixture | Expect (negative) | Pri |
|----|-------------------|------------------|------------------|-------------------|-----|
| TC-P-01 | Page gate (any `/admin/*`) | Super-admin (`*`) | `QA-LimitedAdmin` lacking that page's permission | nav item hidden; direct URL → `/not-permitted` (200); no list API call fires | P0 |
| TC-P-02 | Action gate (Add/Edit/Delete buttons) | role with the action perm | role with **View** but not the action | action buttons not rendered (`<AuthorizedAction>` hides them); forged API call → **403** | P0 |
| TC-P-03 | Q&A moderation | session **moderator** grant | visitor / non-moderator | app `sessionModerate` hidden + route redirects home; CP moderate page denied | P0 |
| TC-P-04 | Hall arrivals | `HallArrivals.View` + `.Record` | View-only admin | QR field + Record button hidden; `POST …/arrivals` → 403 | P0 |
| TC-P-05 | Gate operate | `Gates.Operate` + gate **assignment** | staff without grant **or** without assignment | app: "not authorised"/"not assigned" state; CP `/admin/gates/operator` → `/not-permitted`; scan at unassigned gate → **403 `GATE_OPERATOR_NOT_ASSIGNED`** | P0 |
| TC-P-06 | Admin vs Visitor surface | admin on CP | **visitor** account attempting CP login | visitor cannot reach `/admin/*`; wrong-surface routing | P0 |
| TC-P-07 | Account-state gate | Approved | Pending / Rejected / Disabled | pending → pending screen/banner; rejected → rejected; disabled → blocked sign-in; **none** reach app/CP content | P0 |
| TC-P-08 | App privilege ladder | Guest=0 / Visitor=1 / Moderator=2 / Staff=3 | lower privilege | guest-only reads work signed-out; visitor-only actions (seat, question, comment, rate, contacts) blocked for guest | P0 |
| TC-P-09 | Role CRUD + permission editor | `Roles.*` | limited admin | `/admin/roles` + `/admin/roles/{id}/permissions` gated; changing a role's perms updates **its** members' JWT on next token | P1 |
| TC-P-10 | 2FA reset (admin action) | `/admin/reset-2fa` perm | limited admin | page gated; reset writes an audit row | P1 |
| TC-P-11 | Wildcard sanity | Super-admin | — | `*` reaches **every** page; this is the over-grant **baseline** the negatives are measured against | P1 |
| TC-P-12 | Permission-change propagation | super-admin grants a new perm to `QA-LimitedAdmin` | — | after re-login (new JWT), the previously-denied page now loads; **before** re-login it stays denied (codes are baked into the token) | P1 |

> **Coverage technique:** you do not need 62 separate negative logins. Create
> **one** `QA-LimitedAdmin` with a deliberately small permission set, then walk
> the CP nav: every item that is **hidden** for it is a passing page-gate, every
> item visible is in-scope and gets a direct-URL negative check. Spot-check ~10
> pages spanning every nav group at the API layer (forged call → 403). Record the
> walk as a single `TC-P-01` evidence sheet listing each page + hidden/denied.

---

# Section B — Cross-cutting **Journeys** (`TC-J`)

Authored in full — these span many pages and are the heart of "real test as in
production". Run them in order; later journeys reuse earlier fixtures.

### TC-J-01 — Visitor registration → pending  `[P0] [App] [mobile-sign-* / -terms / -registration-*]`
**Pre:** clean synthetic identity (`qa+vis01@…`); app on a standard Android phone.
**Steps:**
1. Launch app → splash (#1) → onboarding (#2) → **Sign in** (#3) → **Create account**.
2. Sign-up form (#5): enter name **(2–4 chars rule — see `TC-V-01`)**, email, password.
3. Email-OTP (#6): read the code (Lead/DevOps reads it from the system — never inline it), enter it; resend once to prove resend works.
4. Profile (#7): pick Visitor + ProfileType + the 3 lookups (incl. **birth-location 13-region picker**, `TC-V-08`); **Next → interests** (#7-01): pick 1–10 → the single `POST /app/account/user-profile` save.
5. Terms (#9) → accept → registration success (#10) → registration status (#11).
**Expect:** account lands **PendingApproval**; status screen shows pending; no app content (sessions/seat/etc.) is reachable yet (`TC-P-07`).
**Safety:** synthetic only; add to Cleanup Register.

### TC-J-01b — Sign-in 2FA + token caps  `[P0] [App+CP+Web] [cp-auth-flow / mobile-sign-in / web-login]`
**Steps:**
1. **Visitor** sign-in → second factor is **email-OTP** (D-033); enter the emailed code.
2. **Admin** sign-in (CP) → second factor is **TOTP** (`Get-Totp`); first-time admin sees TOTP pairing.
3. Leave a session idle to cross the **5-minute access-token** lifetime → the app's single-flight refresh renews silently (no reuse-storm); CP/Web show the **session-warning modal** approaching the **24-hour absolute** cap.
4. **Forgot password** → reset → sign in with the new password.
**Expect:** correct factor per audience; silent refresh works; warning modal fires before the 24-h cap; reset path works. **Wrong OTP/TOTP** is rejected with the bilingual error (`TC-V-31`).

### TC-J-02 — Admin approves / rejects the pending visitor  `[P0] [CP] [cp-admin-visitors-pending / cp-admin-visitors]`
**Steps:**
1. Super-admin → `/admin/visitors/pending` → open the `TC-J-01` applicant → review all submitted data + photo.
2. **Approve** (assign tier where prompted). Then, on a **second** synthetic applicant, **Reject** with a reason.
**Expect:** approved visitor flips to **Approved**, appears in `/admin/visitors`, and its app `registrationStatus` now routes to **home**; the rejected one routes to the rejected screen/banner. Audit rows written.
**Safety:** approve/reject only your own synthetic applicants.

### TC-J-03 — **Gate check-IN** (Allowed)  `[P0] [App+CP] [mobile-gate-scan / cp-admin-gates-operator / cp-admin-gates-dashboard]`
**Pre (TC-J-03a — gate setup):** super-admin `/admin/gates` → create `QA-GATE-1`, direction **Both**, allow-list **includes** the approved visitor's ProfileType, **assign** the `QA` gate operator.
**Pre (TC-J-03b — badge):** approved visitor opens app **badge** (#32) — confirm the QR + holder name/type render (tablet: QR capped to `min(width, 50% height)`).
**Steps (TC-J-03 core):**
1. Gate operator opens **app `gateScanner`** (#105) → confirms it is **assigned** to `QA-GATE-1` (`GET /app/gates/my-assignments`).
2. Scan the visitor's badge QR (or manual-entry the code → *Check*).
**Expect:** `POST /app/gates/{id}/scans` → **HTTP 200**, `Outcome = Allowed`, **`Direction = CheckIn`**; green **"مسموح / Allowed"** card shows holder name, type, gate, direction; "scan again" resets. A **`GateScan`** row appears live on `/admin/gates/dashboard`. Repeat the same scan on the **CP `/admin/gates/operator`** console → "My day so far" increments **1 allowed**.

### TC-J-03p — Physical-device gate dry-run  `[P0] [App] [mobile-gate-scan]`
**Steps:** print/display the synthetic visitor's badge QR; on **each staff device class** (Android phone, **Huawei/no-GMS**, tablet) scan it at a mock gate.
**Expect:** same Allowed result on every class. On **Huawei/no-GMS** the camera path uses the GMS-disabled **fallback** → verify **manual-entry** works and the viewfinder renders (bounded box). Under **poor/no network**: error + retry surfaces, **never** a silent "Allowed".

### TC-J-04 — **Gate check-OUT** + denials  `[P0] [App+CP] [cp-admin-gates-operator / mobile-gate-scan]`
**Steps:**
1. Scan the **same** visitor again at the **Both** gate → direction is **inferred** as **`CheckOut`**.
2. **TC-J-04b denials** (each returns **200 + `Outcome=Denied`**, not an HTTP error — except the operational faults):
   - Unknown QR → `DenialReasonCode = QrUnknown`.
   - Visitor whose ProfileType is **excluded** by the gate allow-list → `ProfileTypeNotAllowed`.
   - **Pending/not-approved** holder → `HolderNotApproved`.
   - Operator scanning a gate they're **not assigned** to → **403 `GATE_OPERATOR_NOT_ASSIGNED`**.
   - **Duplicate within 5 s** → absorbed (no double count).
   - Rapid repeats → **429** rate, "too many attempts", no result card.
**Expect:** red **"ممنوع / Denied"** card with the server's denial message + code for each; dashboard "denied" count increments; operational faults surface their own bilingual fallback alert.

### TC-J-05 — Hall-door arrival (session attendance)  `[P0] [CP] [cp-admin-hall-arrivals / cp-admin-attendance]`
**Steps:**
1. `/admin/hall-arrivals` → select an **active** session → scan the approved visitor's **badge QR** (≤64 chars) → **Record arrival**.
2. Re-scan the **same** attendee (idempotent).
3. Unknown QR; not-approved attendee.
**Expect:** (1) green "Arrival recorded: \<name\>", QR field clears, one **`HallAttendance`** row `Method=QrScan`; (2) merges into the **one open row** (no duplicate, no second audit row); (3) `ATTENDEE_QR_UNKNOWN` (400) bilingual toast; not-approved → `ATTENDEE_NOT_APPROVED` (403). Confirm the arrival on `/admin/attendance`.

### TC-J-06 — Visitor seat reserve / release  `[P0] [App+CP] [mobile-session-detail / -my-seat / cp-admin-sessions-seat-plans]`
**Steps:** approved visitor → agenda (#16) → session detail (#17) → **my-seat** (#18): reserve an available seat, view it as "mine", **release** it; try to reserve a **taken** seat.
**Expect:** reserve marks the seat **محجوز/yours** and is **confirmed immediately** (no Control Panel approval step); the seat is held **provisionally** until the attendee **checks in at the hall gate** (`TC-J-05`), which confirms it, or is released by the pre-start sweep if not checked in; release frees it before start; taken seat is blocked with the bilingual conflict message; CP seat-plan reflects the reservation. (The CP Bookings approval queue in `TC-CP-PRG` is **retained but dormant** — nothing creates a Pending booking, so it is always empty.)

### TC-J-07 — Visitor engagement  `[P1] [App] [mobile-send-question / -audience-comments / -rate / -ai-summary / -live]`
**Steps:** in a session: **send a question** (#26); **post a comment** + **like** another (#28); **rate** the session per-element (#40); read the **AI summary** (#34); open the **live broadcast** (#25, YouTube provider).
**Expect:** each write succeeds with its bilingual confirmation; **guest** is blocked from all five (login-only) — that's the `TC-P-08` negative; rating per-element scores persist (4 nullable score columns, D-463).

### TC-J-08 — Moderation  `[P0] [CP+App] [cp-admin-question-queue / cp-session-moderate / mobile-session-moderate]`
**Steps:** moderator (CP `/sessions/{id}/moderate` and/or app `sessionModerate` #104) → review the queued `TC-J-07` question → **push** it live, **hide** another; in CP `/admin/comments-moderation` approve/hide a comment; in `/admin/ratings` view the rating.
**Expect:** pushed question appears live; hidden one disappears; moderation actions write audit rows; a **non-moderator** cannot reach either surface (`TC-P-03`).

### TC-J-09 — Session-summary review/approval  `[P1] [CP] [cp-admin-session-summaries]`
**Steps:** `/admin/session-summaries` → move a summary **Draft → InReview → Approved** ("ready for المحاور"); confirm moderator/host **read** access; try an illegal transition.
**Expect:** the state machine advances only on valid transitions; "Approved" is the published-to-moderator state; illegal transition is rejected with the bilingual error.

### TC-J-10 — Networking + contacts (vCard)  `[P1] [App] [mobile-meet-people / -my-contacts]`
**Steps:** approved visitor → **meet-people** (#35, sees match reason); **share my contact** (QR → vCard); on a second device **scan** that contact → **save**; open **my-contacts**, delete one, export a `.vcf`.
**Expect:** recommendations show a reason; share-QR resolves to a vCard; scan→save adds the contact; delete + vCard export work; all **login-only** (guest blocked).

---

# Section C — **Constraint / Validation** matrix (`TC-V`)

Every field rule, account rule, and protocol rule in one sweep (Day 6 PM). For
each, try the **invalid** input and assert the **inline bilingual error + no
write**, then the **valid** input succeeds. Confirm the **exact** error text
against the named catalogue file (don't trust memory for the literal string).

| ID | Constraint | Where | Invalid → expect | Pri |
|----|-----------|-------|------------------|-----|
| TC-V-01 | **Name length 2–4** (Group A, D-459) | app sign-up form #5; CP people forms | 1 char / 5+ chars → inline error, no submit | P0 |
| TC-V-02 | Required fields | every create form | blank required → inline error, no write | P1 |
| TC-V-03 | Email format | sign-up / login / profile | `notanemail` → format error | P1 |
| TC-V-04 | Password policy | sign-up / reset | weak/short → policy error | P1 |
| TC-V-05 | Email uniqueness | sign-up; `/admin/*` new | duplicate → 409 `…NotUnique` + bilingual msg | P1 |
| TC-V-06 | Interests count 1–10 | app #7-01 | 0 selected / >10 → guard | P1 |
| TC-V-07 | **Saudi National ID — Luhn** (Group A, D-459) | where ID is captured | wrong checksum → invalid; valid Luhn passes | P0 |
| TC-V-08 | **Birth-location 13-region picker** (D-469) | app #7 | only the 13 regions selectable; searchable | P1 |
| TC-V-09 | **Saudi plate — 17-letter set** (Group A, D-459) | plate fields | letter outside the 17 allowed → rejected | P1 |
| TC-V-10 | **Region / plate searchable pickers** (D-471) | plate/region | same searchable picker UX as country | P2 |
| TC-V-11 | MaxLength alignment (UI = FluentValidation = EF) | every text field | paste over the cap → truncated at `MaxLength`; server agrees | P1 |
| TC-V-12 | Bilingual Name / NameArabic round-trip | every lookup (interests, themes, …) | Arabic saved + re-rendered RTL | P1 |
| TC-V-13 | Numeric / range fields (seats, counts, order) | sessions, seat-plans, banners order | out-of-range / negative → error | P1 |
| TC-V-14 | Date / time + session overlap | sessions, programme-days | end < start / overlap → error | P1 |
| TC-V-15 | File upload type + size (photos, presentations, media) | speaker photo, presentations, media, avatar | wrong type / oversize → rejected | P1 |
| TC-V-16 | Avatar liveness (3-step, randomized order) | app identityVerification #103 | order shuffles; failed step blocks save | P1 |
| TC-V-17 | Soft-delete semantics | every list page | Deactivate → `IsActive=false`, drops from default list, not hard-deleted | P1 |
| TC-V-18 | Duplicate-name conflict | lookups, gates, halls | duplicate → 409 conflict code | P1 |
| TC-V-19 | Illegal state transition | bookings (dormant admin path), session-summaries, gate direction | invalid transition → domain error, no write | P1 |
| TC-V-20 | Delegate bulk-generate by type/count (D-473) | `/admin/delegates` | count ≤ 0 / huge → guard; low-traffic window | P2 |
| TC-V-21 | VIP/VVIP fields (Mawj) + photo (D-429) | `/admin/visitors/vip` | required VIP fields enforced; creates **pending** | P1 |
| TC-V-22 | Invitation rules | `/admin/invitations` | expiry / single-use enforced | P1 |
| TC-V-23 | Country / org lookups | `/admin/countries`, `/admin/organisations` | referential rules hold | P2 |
| TC-V-24 | Site-settings message + social links (D-461..D-466) | `/admin/site-settings` | malformed URL → error; message length cap | P2 |
| TC-V-25 | Content blocks / banners ordering + active window | `/admin/content-blocks`, `/admin/banners` | overlap/order enforced; inactive hidden on Web | P2 |
| TC-V-26 | AI prompt fields | `/admin/ai/prompts` | required template fields; invocation log written | P2 |
| TC-V-27 | Configuration key/value | `/admin/configuration` | typed value validation per key | P2 |
| TC-V-28 | Rating per-element scores (D-463) | app #40 | each of the 4 scores nullable, in range | P2 |
| TC-V-29 | Comment length + like idempotency (D-223) | app #28 | over-cap blocked; double-like is idempotent | P2 |
| TC-V-30 | OTP expiry / single-use | login verify | reused/expired OTP rejected | P0 |
| TC-V-31 | Wrong 2FA factor / wrong code | login | wrong code → bilingual error, attempt counted | P0 |
| TC-V-32 | Lockout / rate-limit on auth | login | repeated failures → throttle/lock | P1 |
| TC-V-33 | Token caps (5-min access / 24-h session, D-443) | all surfaces | warning modal pre-cap; silent refresh; hard expiry at 24 h | P0 |
| TC-V-34 | Gate QR unknown | gate scan | `QrUnknown` denial (200) | P0 |
| TC-V-35 | Gate profile-type not allowed | gate scan | `ProfileTypeNotAllowed` denial (200) | P0 |
| TC-V-36 | Gate holder not approved | gate scan | `HolderNotApproved` denial (200) | P0 |
| TC-V-37 | Gate idempotency replay / not-assigned | gate scan | reused key+diff payload → 409; unassigned → 403 | P1 |
| TC-V-38 | Hall-arrival QR cap 64 + unknown/not-approved | hall-arrivals | cap enforced; `ATTENDEE_QR_UNKNOWN` 400 / `ATTENDEE_NOT_APPROVED` 403 | P0 |
| TC-V-39 | Seat double-book conflict | my-seat | taken seat → conflict, no double reservation | P0 |
| TC-V-40 | Server-500 resilience (sample) | any list/create | forced/observed 500 → bilingual fallback toast, no partial write | P2 |

---

# Section D — **Device matrix** (`TC-D`)

Run against the app-critical screens. ✓ = must verify on that class.

| ID | Check | Android phone | Huawei / no-GMS | Tablet | iOS | Catalogue |
|----|-------|:---:|:---:|:---:|:---:|-----------|
| TC-D-01 | App installs + signs in (prod API, self-signed TLS) | ✓ | ✓ | ✓ | ✓ | mobile-sign-in |
| TC-D-02 | Badge QR renders + caps on landscape/tablet | ✓ | ✓ | ✓ | ✓ | mobile-badge |
| TC-D-03 | **QR camera scan** (gate + contact) | ✓ | ✓ (fallback) | ✓ | ✓ | mobile-gate-scan, mobile-my-contacts |
| TC-D-04 | **Manual-entry** gate code (no-GMS primary path) | ✓ | ✓ | ✓ | ✓ | mobile-gate-scan |
| TC-D-05 | Avatar via authenticated bytes (not `Image.network`) | ✓ | ✓ | ✓ | ✓ | mobile-identity-verification, mobile-speakers |
| TC-D-06 | Tablet layout (flex-fill stretch; square caps centre) | — | — | ✓ | — | mobile-my-seat, mobile-badge |
| TC-D-07 | Poor/no-network gate scan → error+retry, never silent allow | ✓ | ✓ | ✓ | ✓ | mobile-gate-scan |
| TC-D-08 | Bottom-nav persistence (StatefulShellRoute, fixed bar) | ✓ | ✓ | ✓ | ✓ | mobile-home |

---

# Section E — **Bilingual / RTL** sweep (`TC-I18N`)

SIMF is **Arabic-first**. For each surface below: toggle to العربية and assert
`<html dir="rtl" lang="ar">` (web/CP) or RTL layout (app), Arabic
labels/titles/toasts, **mirrored** nav + **reversed** action order, and that
LTR-only fields (email, QR, codes) stay LTR.

| ID | Surface (run the 15 highest-traffic) | Pri |
|----|--------------------------------------|-----|
| TC-I18N-01 | CP login + dashboard | P1 |
| TC-I18N-02 | CP a list page (e.g. `/admin/visitors`) + its Add/Edit modal | P1 |
| TC-I18N-03 | CP gate operator console | P1 |
| TC-I18N-04 | CP hall-arrivals | P1 |
| TC-I18N-05 | CP session-summaries review | P1 |
| TC-I18N-06 | App sign-in + sign-up | P1 |
| TC-I18N-07 | App home + global app-bar (bell/lang/inert dark-mode/hamburger) | P1 |
| TC-I18N-08 | App badge + gate scanner | P1 |
| TC-I18N-09 | App agenda + session detail + my-seat | P1 |
| TC-I18N-10 | App send-question + comments + rate | P1 |
| TC-I18N-11 | App speakers + speaker profile | P1 |
| TC-I18N-12 | App news + gallery + archive | P1 |
| TC-I18N-13 | App about + accessibility settings | P1 |
| TC-I18N-14 | Web landing + programme | P1 |
| TC-I18N-15 | Web login + account profile | P1 |

> Also confirm the app **accessibility** settings (font size, contrast, reduced
> motion, screen reader, captions) apply app-wide (D-327/D-465), and that colour
> is never the **only** signal of a state (`SIMF-TST-001` §11).

---

# Section F — Control Panel per-page **execution checklist**

Run each page's catalogue file (open the link in [`../e2e/README.md`](../e2e/README.md))
golden path + that page's distinct actions + its **auth-gate** (feeds `TC-P-01`).
All `/admin/*` pages are **Administrator / AdminOnly** unless noted. ✗ = do **not**
run the destructive end-state on real rows (create your own row first; see plan §2).

### `TC-CP-PPL` — People & accounts (Day 6 AM)
| Route | Key actions to run | Catalogue ids | Destructive on prod? |
|-------|--------------------|---------------|:---:|
| `/admin/admins` (+`/pending`) | list, approve pending admin, edit, deactivate | USR-001..024, APN-001..015 | ✗ own only |
| `/admin/others` (+`/pending`) | list, approve, edit | OTH-001..024, OPN-001..016 | ✗ own only |
| `/admin/visitors` (+`/pending`) | list, **approve+tier**, photo viewer, reject | VIS-001..025, VPN-001..025 | ✗ own only |
| `/admin/visitors/vip` (+`/export`) | VVIP/VIP register (creates pending), Mawj fields, export roster CSV/Excel | VIPR-001..007, VIPX-001..008 | ✗ own only |
| `/admin/delegates` | single add, **bulk-generate badges by type/count** (low-traffic) | DLG-001..009 | ✗ own only |
| `/admin/attendees` | list, filter, export | ATT-001..016 | read-mostly |
| `/admin/print-bag` | queue, print states | PRT-001..011 | read-mostly |
| `/admin/interests` | full CRUD (gold standard) | INT-001..013 | ✗ own only |
| `/admin/profile-types/visitor` · `/other` | CRUD | VPT-001..014, OPT-001..015 | ✗ own only |
| `/admin/organisations` | CRUD | ORG-001..019 | ✗ own only |
| `/admin/contacts` | CRUD | CON-001..020 | ✗ own only |
| `/admin/countries` | CRUD, region/plate pickers | CTY-001..020 | ✗ own only |
| `/admin/vips` | CRUD | VIP-001..013 | ✗ own only |
| `/admin/invitations` | create, expiry, single-use | INV-001..018 | ✗ own only |
| `/admin/reset-2fa` | reset a **synthetic** account's 2FA | R2F-001..012 | ✗ own only |
| `/admin/roles` (+`/{id}/permissions`) | role CRUD + permission editor (`TC-P-09`) | ROL-001..024, RPM-001..013 | ✗ own only |

### `TC-CP-PRG` — Programme & sessions (Day 3)
| Route | Key actions | Catalogue ids |
|-------|-------------|---------------|
| `/admin/themes` | CRUD | THM-001..024 |
| `/admin/halls` (+`/seat-layouts`) | CRUD + layout editor | HAL-001..022, HSL-001..015 |
| `/admin/speakers` (+`/speaker-presentations`) | CRUD + photo + presentation files | SPK-001..022, SPP-001..017 |
| `/admin/sessions` (+`/seat-plans`) | CRUD, date/overlap, seat plan | SES-001..024, SSP-001..014 |
| `/admin/session-categories` | CRUD (table may ship empty) | SCT-001..021 |
| `/admin/programme-days` | CRUD | PGD-001..018 |
| `/admin/session-moderators` | assign moderator | SMD-001..018 |
| `/admin/programme/timeline` | view/build timeline | PTL-001..011 |
| `/admin/bookings` | approval queue (**retained but dormant** — always empty; nothing creates a Pending booking) | BKG-001..013 |
| `/admin/speaker-meeting-requests` | review/approve | SMR-001..015 |
| `/admin/meeting-tables` · `/business-meetings` | CRUD + scheduling | MHT-001..013, BMT-001..016 |

### `TC-CP-ENG` — Engagement, Q&A & attendance (Day 4)
| Route | Key actions | Catalogue ids |
|-------|-------------|---------------|
| `/admin/question-queue` | review, push, hide | QQU-001..015 |
| `/sessions/{id}/moderate` | live moderate | MOD-001..012 |
| `/admin/comments-moderation` | approve/hide | CMT-001..018 |
| `/admin/ratings` | view/aggregate | RAT-001..013 |
| `/admin/session-summaries` | Draft→InReview→Approved (`TC-J-09`) | SUM-001..022 |
| `/admin/hall-arrivals` | door-scan arrival (`TC-J-05`) | HAR-001..014 |

### `TC-CP-EXH` — Exhibition (Day 5)
| Route | Key actions | Catalogue ids |
|-------|-------------|---------------|
| `/admin/companies` | CRUD | CMP-001..016 |
| `/admin/exhibitors` | CRUD + import/export | EXH-001..023 |
| `/admin/booths` | CRUD + company/officer | BTH-001..023 |
| `/admin/sponsors` | CRUD + tier | SPN-001..023 |
| `/admin/media-partners` | CRUD + logo | MPR-001..019 |
| `/admin/venue-map` | 2D nodes | VMP-001..024 |

### `TC-CP-CNT` — Content & media (Day 5)
| Route | Key actions | Catalogue ids |
|-------|-------------|---------------|
| `/admin/news` | CRUD + image | NWS-001..021 |
| `/admin/media` | gallery CRUD | MED-001..022 |
| `/admin/archive` | editions + child tables | ARC-001..023 |
| `/admin/media-library` | shared assets (D-357) | MLIB-001..010 |
| `/admin/banners` | CRUD + order/active window | BNR-001..022 |
| `/admin/content-blocks` | CRUD | CNT-001..020 |
| `/admin/faq` | groups + entries | (cp-admin-faq) |

### `TC-CP-SYS` — Access control, AI & system (Day 6 PM)
| Route | Key actions | Catalogue ids |
|-------|-------------|---------------|
| `/admin/gates` | gate CRUD, direction, allow-list, assignment (`TC-J-03a`) | GAT-001..021 |
| `/admin/gates/operator` | scan console (`TC-J-03/04`) | GOP-001..013 |
| `/admin/gates/dashboard` | live scan dashboard | GDS-001..011 |
| `/admin/attendance` | attendance report (`TC-J-05`) | (cp-admin-attendance) |
| `/admin/configuration` | system settings key/value | CFG-001..023 |
| `/admin/site-settings` | message + social (D-461..466) | CPSET-001..006 |
| `/admin/operations` · `/operation-log` | ops + audit trail | OPS-001..011, OPL-001..018 |
| `/admin/logs` | system logs | LOG-001..013 |
| `/admin/statistics` | dashboards (metric list pending D6) | STA-001..012 |
| `/admin/ai/prompts` · `/ai/invocations` | prompt CRUD + invocation log | AIP-001..022, AIV-001..012 |
| `/` (dashboard) | tiles, any-signed-in | DSH-001..013 |

### `TC-CP-AUTH` — Auth + account (Day 1)
| Route | Catalogue ids |
|-------|---------------|
| `/login` + `/login/totp` + `/login/recovery` + `/forgot-password` + `/auth/pending` + `/auth/rejected` | AUTH-001..010 |
| `/account/profile` · `/account/notifications` · `/account/totp-pairing` | PRF-001..016, NTF-001..012, TPP-001..010 |

---

# Section G — Mobile App per-screen **execution checklist**

Run each screen's catalogue file golden path + its distinct actions + auth/role
gate. **Dev** column flags screens that must also clear the **device matrix**
(Section D). Audience: Guest (signed-out) / Visitor (approved, login-only) /
Moderator / Staff.

| Screen (route) | Audience | Key actions | Catalogue ids | Dev |
|----------------|----------|-------------|---------------|:---:|
| #1 splash · #2 onboarding · #3 sign-in | Guest | boot, refresh, sign-in (+verify/forgot/reset) | MOB001/002/003 | ✓ |
| #5 sign-up form · #6 email-OTP | Guest | register, OTP, resend | MOB005/006 | |
| #7 profile · #7-01 interests | Visitor | profile + 13-region + interests save | MOB007 / MOB7A | |
| #9 terms · #10 success · #11 status | Visitor | accept, pending status | MOB009/010/011 | |
| #12 guest-mode | Guest | anonymous reads | MOB012 | |
| #13 home | Guest+ | bootstrap, unread count, app-bar | MOB013 | ✓ |
| #14 my-area | Visitor | dashboard, `.ics`, `.vcf` | MOB014 | |
| #15 venue-map | Guest+ | map + booths | MOB015 | |
| #16 sessions · #17 detail · #18 **my-seat** | Guest+ / Visitor | agenda, detail, **reserve/release** (`TC-J-06`) | MOB016/017/018 | ✓ (tablet) |
| #19 speakers · #20 speaker profile | Guest+ / Visitor | list, profile, **meeting request** | MOB019/020 | ✓ (avatar) |
| #22 booths · #23 sponsors | Guest+ | list/detail, logos | MOB022/023 | |
| #24 archive · #24-01 detail · #29 news · #30 gallery | Guest+ | lists + detail + images | MOB024/024D/029/030 | |
| #31 media-partners · #37 about | Guest+ | hub, about | MOB031/037 | |
| #25 live · #34 AI summary · #36 chatbot | Guest+ | YouTube live, AI summary, chatbot shell | MOB025/034/036 | |
| #26 send-question · #28 comments | Visitor | ask, comment, like (`TC-J-07`) | MOB026/028 | |
| #40 rate | Visitor | per-element scores (`TC-V-28`) | MOB040 | |
| #32 **badge** | Signed-in | badge QR (`TC-J-03b`) | MOB032 | ✓ |
| #33 notifications | Signed-in | list, read, read-all | MOB033 | |
| #35 meet-people | Visitor | recommendations + reason (`TC-J-10`) | MOB035 | |
| share/scan/my-contacts | Visitor | QR→vCard, scan, save, delete (`TC-J-10`) | MMC-001..011 | ✓ (scan) |
| #103 identity-verification | Visitor | avatar **liveness** (randomized, `TC-V-16`) | MOBIDV | ✓ |
| #104 session-moderate | Moderator | push/hide questions (`TC-J-08`) | MOBMOD | |
| #105 **gate-scanner** | Staff | scan/deny, manual entry, hold (`TC-J-03/04`) | MOBGATE | ✓ |
| #38 accessibility · #41 more | Guest+ | settings app-wide, profile/sign-out | MOB038/041 | |

> **Note on the app being a "mockup".** Per the project notes the Flutter UI is an
> interim mockup over the **real** App API + security. Test the **behaviour and
> the API contract** (auth, role gates, validation, the bilingual data) as
> production; treat purely-visual Figma-parity deltas as **low**-severity unless
> they break a function.

---

_Companion to `SIMF-Manual-Test-Plan-7Day.md`. Subordinate to `SIMF-TST-001`.
Last reviewed 2026-06-20 by SIMF Team._
