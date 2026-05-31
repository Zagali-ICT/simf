# SIMF — Implementation Gap Report

**Date:** 2026-05-31  ·  **Branch:** `feature/login-api`  ·  **Status:** point-in-time snapshot

This report cross-references the **intended functional scope** (Mockup 41 screens, SRS `FR-1xx…FR-12xx`,
Programme-Plan stages/gates) against the **implemented surface** (API endpoints, Control Panel pages,
Website pages, Flutter screens) and the decisions log. Items are categorised by the *nature* of the gap.

## Methodology & provenance (read this first)

- **Sources of intended scope:** `Mockup.html` (the app scope), `docs/SIMF-SRS-001` functional
  requirements, `docs/SIMF-Program-Plan.md`, and the `docs/decisions/DECISIONS_LOG.md` + `CLAUDE.md`
  freeze/scope record.
- **Provenance of the implemented surface — IMPORTANT:** the **backend, Control Panel and Website** are
  assessed on this checked-out repo (`d:/SIMF/System/V1.0.0`, branch `feature/login-api`). The **mobile
  app (`mkp_*` screens / `*_feature.dart`)** lives on a **separate branch/worktree**
  (`feature/mobile-app-skeleton`, copy at `D:/SIMF/System/simf-run`) — every App-layer claim here is
  accurate against *that* tree, not this branch. The two are merged only at release.
- **Inventories are derived, not committed artifacts.** The implemented/intended inventories were
  generated this session by parallel investigation agents and were adversarially re-verified against the
  source; they are not files in `docs/`. Treat any single inventory line as "verified this session," not
  as an authoritative manifest.
- **Scope of THIS report = functional.** Non-functional requirements (`NFR-01..NFR-11`:
  performance, availability, audit depth, data retention, etc.) and integration item `EIR-01` are **not**
  assessed here — they need their own review. Accessibility and per-endpoint permission are touched only
  in passing.

---

## 1) Not implemented

Features in the intended scope (SRS / Mockup) with no corresponding code in any layer.

| Feature | Layer(s) | Evidence | Decision |
|---|---|---|---|
| **GPS geofence hall-arrival recording** (FR-305: arrival recorded by *both* QR scan *and* GPS geofence; enter/leave time per session) | API / App | Gate scans (`POST /gates/{gateId}/scans`) record QR entry only. No `VenueBoundary` table, no `Hall.PolygonGeoJson`, no geofence check executes — only the seam exists. | D-166 / D-169 / D-171 (G7) — BLOCKED on G-OI-2 |
| **Session attendance tracking from arrival records** (FR-506) | API / App | No `NotificationKind`/service backing; depends on the un-built arrival/geofence path above. | follows FR-305 |
| **Movement / dwell / route tracking from GPS** (FR-1103) | API / CP / App | No GPS presence capture; depends on the unbuilt geofence. Statistics dashboard has no movement/dwell/route source. | follows FR-305 |
| **Notification trigger events** (FR-902 session reminders + meeting confirmations; FR-903 booking-confirmed + "started-but-not-attended" reminder) | API | `NotificationKind` carries credential / account / admin / invitation / VIP kinds only — **no session-reminder, booking-confirmed, or not-attended kinds, and no senders** for them. (FR-902's *VIP-invitation* part **is** done — `/admin/vips/notify` + `VipBroadcast`/`InvitationReceived`.) | — |
| **Booking approval workflow** (FR-503 every booking needs CP approval before confirmed; FR-504 cancel before start; FR-502 no time-overlap) | API / CP / App | Seat *reservation* exists (`/sessions/{id}/seats/reserve`) but there's no booking-approval queue, no CP approval surface, no confirm/cancel-with-approval state, and no no-overlap constraint. The **Bookings** nav item is a stub (`/m/bookings` → `ModulePlaceholder`). | Bookings = D-134 stub, never graduated |
| **Device-calendar add + reminder for a session** (FR-409) | App | No calendar-integration feature; `mkp_session_detail` has no calendar action. | — |
| **Speaker presentation-file storage** (FR-407) | API / CP | Speaker CRUD covers bio/profile only; no presentation-file upload endpoint or CP surface. | — |
| **Match-score networking with ≥80% push** (FR-802/FR-803) | API / App | `meet-like-you` recommendations + `mkp_meet_people` exist, but the **Connect button is disabled ("coming soon")**; no match-score threshold, no ≥80% auto-recommendation push. | — |
| **Live-stream Riyadh-region restriction** (FR-702) | API / App | Live screen is a provider-stub; no geo-restriction logic (and depends on the unbuilt geofence). | D-199 |
| **Questions gated on "verified arrived"** (FR-704) | API / App | `POST /sessions/{id}/questions` is gated by `RequireApprovedAccount` (account-state approval) — **not** the arrival-verification precondition the FR wants; that precondition is the geofence seam, which does not execute. | D-166/D-171 (G7) |
| **AI accessibility — live sign-language + speech conversion** (FR-807) | API / App | `POST /ai/live-sign-language/chunk` exists but over the Echo/provider stub; no real provider, so not *functionally* delivered (seam is in §2). | D-199 |

---

## 2) Stubbed on an approved provider / offline seam

Integration seam shipped; the live provider is intentionally a stub per an owner decision. **Not missing code — deliberate offline seams.**

| Feature | Layer(s) | Evidence | Decision |
|---|---|---|---|
| **Live-video streaming** | API / App / CP | `mkp_live.dart` derives the current session from the real agenda; `viewerCount`/`streamUrl`/`isLive` are stubs. Live AI chunk endpoints run over Echo. CP `LiveSessions` is a stub. | D-199 |
| **Cognitive-AI provider** (chatbot, session summary, live captions/translation) | API / App | `mkp_chatbot` (`/ai/faq`) + `mkp_ai_summary` (`/ai/assistance`) labelled preview/demo; live chunk endpoints exist. `AiProvider` implements only `Echo` + `OpenAi`; `AzureOpenAi`/`Anthropic` declared, not implemented. | D-199, D-176 (G12) |
| **AI provider abstraction (swap with no code change)** (FR-808) | API | Abstraction + `AiProvider` enum + admin prompt catalogue (`/admin/ai/prompts`) + invocations log shipped; concrete providers = Echo + OpenAi. | D-176 (G12) |
| **SignalR real-time hubs** (live translation / sign-language) | API | Ship as chunk-per-request HTTP POST, not a SignalR hub; real hub blocked on owner approval to add `Microsoft.AspNetCore.SignalR` to the API csproj (§1.7). | D-176 (G12) |
| **Notification multi-channel dispatch** (FR-901/FR-904: in-app/email/SMS/WhatsApp via one abstraction) | API | In-app fully implemented; email wired (codes emailed). SMS / WhatsApp gateways behind the abstraction are the external seam (EIR-02). *Note: trigger-event coverage is incomplete — see §1 FR-902/903.* | EIR-02 / FR-904 |
| **Entry-badge QR rendering (mobile)** | App | `mkp_badge.dart` fetches the **real** QR payload (`/users/me/badge`) but renders the payload *string*, pending `qr_flutter`. Data path real; visual is a placeholder. | package add pending |

---

## 3) Partially implemented

Real, working code exists but the intended scope is only partly covered.

| Feature | Layer(s) | Evidence | Decision |
|---|---|---|---|
| **Interactive 3D / isometric venue map** (FR-605, Mockup #15) | App / CP | `mkp_map.dart` lists halls + booth counts as a **textual stand-in**; CP `VenueMap` is a stub. 2D-interactive is the agreed target. | D-199 |
| **Seat grid / seat map** (FR-405/FR-505, Mockup #18) | API / CP / App | Backend reserve/random/release + hall layout editor + per-session seat plan shipped; CP renders **plain inputs/tables**, not a visual grid; visitor-view preview + bulk row ops deferred. App `mkp_my_seat` works. | D-182 / D-175 |
| **Bulk approve / reject for pending users** (FR-212) | API / CP | `bulk-approve` (D-164) **and now `bulk-reject` (D-210)** ship for visitors/others (Select-All + shared-reason modal). PendingStaff (Admin-queue) bulk parity + per-user Edit still deferred. | D-118 / D-132 / D-210 |
| **On-site registration + badge reprint** (FR-217) | API / CP | `register-onsite` + `PrintBag.razor` + `WalkInRegistrationForm` shipped. Walk-in exhibitor extras (Company/Booth/Role) + walk-in ID-doc upload deferred. | D-127 |
| **Roles & permissions management** (FR-1201/FR-1202) | API / CP | Roles list/CRUD shipped, but **per-permission grant editor, assign-to-user surface, bulk delete deferred** — this is the subject of the planned permission build. | D-134-A1 |
| **Permission-gated CP navigation** (FR-1201) | CP | `CpNavigation.cs` (lines 9-11) states permission filtering is **not applied yet**; the full nav shows to every admin. | D-018 / D-167 (gate D1) |
| **CMS content editing without code** (FR-1203) | API / CP | Content blocks + banners CMS shipped; brand-colour token editing + markdown rendering on public content deferred (raw markdown shipped). | D-167 (G8/D-173) |
| **Operation-log + Attendees exports** (FR-1205 + roster) | CP | Viewer + roster shipped; XLSX export + date-range filter UI deferred. | D-134-A2 |
| **Meeting / interview request → PR approval** (FR-804) | API / CP / App | `meeting-requests` + admin list/respond + `mkp_request_interview` shipped. App `mkp_speaker_profile` "Request meeting" is **snackbar-only this wave**; country dropdown deferred. | D-184 |
| **Statistics dashboards** (FR-1101/1102/1104) | API / CP | `/admin/statistics` + `StatisticsDashboard.razor` (live counts) shipped. Exact stats list pending D6 (OI-1); movement/dwell (FR-1103) absent (§1). | OI-1 (D6) |
| **FAQ knowledge base** (FR-805/806) | API / CP / App | Chatbot `/ai/faq` + `mkp_chatbot` exist; CP `Faq` is a stub. Two-level group→entry management not built. | D-134 |
| **TOTP self-reset (SuperAdmin)** (FR-105) | API / CP | TOTP setup/confirm/disable/pairing + admin reset shipped; SuperAdmin "reset my own TOTP" is a follow-up. | D-096 |
| **Mobile post-login role routing** (Moderator/Staff shells) | App | `_homeForRole` carries `TODO(D-014)`: Staff→scan-QR home and Moderator→moderator shell not built; all land on the main shell. | D-014 |
| **Accessibility settings** (Mockup #38) | App | `mkp_accessibility.dart` is local-only (font scale + high-contrast in prefs); not server-synced. | — |
| **Public Website post-login experience** | Website | `Home.razor` (`/account`) is a "You're signed in" placeholder + sign-out; full forum experience deferred. | — |

---

## 4) Deferred by an owner decision

Explicit decisions to *not build now* (or to descope permanently).

| Feature | Layer(s) | Evidence | Decision |
|---|---|---|---|
| **In-app Exhibitor/Sponsor sign-up** (Mockup #08, FR-601) | App | **Permanently descoped.** Sign-up is always Visitor; exhibitors/sponsors are CP-only Companies with provisioned accounts. No in-app portal. | D-199/D-202/D-186/D-190 |
| **Photo / ID verification at mobile sign-up** (Mockup #06, FR-205/207) | App | Descoped — the mockup itself comments it out. | D-192/D-200/D-199 |
| **Email-OTP at every login** (FR-104) | API / App / Web | Out of scope / unresolved; shipped code keeps 2FA **opt-in**. | D-198 |
| **Exhibitor self-registration → PR approve + assign booth** (FR-602) | CP | Follows the descope; onboarding is CP-only Company + provisioning. | D-199 |
| **CP picker for editing `MobileAppRole`** | CP | Deferred — editable via API; no CP picker yet. | D-161 |
| **Visual seat-grid renderer + visitor preview + bulk row ops** | CP | Deferred (plain inputs/tables shipped). | D-182/D-175 |
| **3-way grey-theme toggle UI** | CP | Deferred until owner confirms grey for general use. | D-103/D-094 |
| **Per-user Edit page / generic CRUD write paths** | CP | Gated on the User Management module; Edit stubs shown. | D-117 |
| **Device attestation (App Attest / Play Integrity) + CP device-key surface** | API/App/CP | Out of scope — Phase 1 stops at the crypto ceremony. | D-172 (G10) |
| **"AI Settings" CP page + prompt import/export** | CP | `AiSettings` remains a `ModulePlaceholder`. | G12-CP |
| **Remaining CP stub modules** | CP | Of the **22** original D-134 stub modules, **~14 graduated** (Themes, Sessions, Halls, Speakers, Roles, Operation log, Attendees, Banners, Content blocks, …) and **8 remain stubs**: registration-requests, bookings, exhibitors, venue-map, live-sessions, faq, configuration, settings. | D-134/D-135 |

---

## 5) External / non-code blocked

Cannot be completed by code generation — procurement, external authorities, time-bound processes, or csproj/owner approvals.

| Item | Layer | Evidence | Decision |
|---|---|---|---|
| **NCA / MoD security clearance** | whole system | Hard go-live gate; external + time-bound; 10-item deploy-time runbook. | D-199/D-193 |
| **UAT** | whole system | External + time-bound. | D-199 |
| **App-store publication** | App | External + time-bound. | D-199 |
| **Real live-video streaming provider + keys** | API/App | Provider procurement external; seam ships now. | D-199/D7 |
| **Real cognitive-AI provider + keys** | API/App | UI + seam over Echo; provider procurement external. | D-199/D7 |
| **SMS / WhatsApp notification gateways** (FR-901, EIR-02) | API | External gateways behind the abstraction. | EIR-02/D7 |
| **Live-broadcast embed + map/location service** (EIR-03/04) | App | External services. | D7 |
| **CI/CD YAML + load-test scripts (k6/JMeter) + prod secrets** | DevOps | WS5 workstream; outside Stage-6 runbook doc. | D-193 |
| **Committed-secrets rotation** (super-admin temp pw, TOTP seed, `Jwt:SigningKey`, ID-doc key in `appsettings*.json`) | API/DevOps | Operational (rotate+scrub / gitignore / env override). Blocks the origin push. | Sprint1 §3.1 |
| **Push `feature/login-api` to origin** | repo | Not done — waiting on owner; behind the secret-rotation decision. | Sprint1 §4 |
| **Real SignalR hub** (csproj add) | API | Owner approval to add the SignalR package (§1.7). | D-176 (G12) |
| **SIEM rule deployment + false-positive tuning** | DevOps/Sec | Rules authored under `docs/soc/siem-rules/`; deployment is operator-side. | D-176/D-179 |
| **`qr_flutter` package add** (badge QR image) | App | Visual QR blocked on the package add. | — |

---

## 6) Implemented and complete (for context)

Areas where the implemented surface matches intended scope.

- **Accounts & authentication (FR-101–FR-108)** — full suite: sign-in/up/out/refresh, email verification + resend, password reset/change (+ **D-206** forced first-login change), TOTP pairing/setup/confirm/disable + recovery codes, device-key passkey ceremony. CP + Website login/reset/2FA flows; mobile auth wired. Lifecycle closed by D-085.
- **Registration & approval (FR-201–FR-214 core)** — Visitor/Other/Admin create + list + pending + approve/reject + profile-for-approval + ID-doc upload; Terms consent; pending/rejected landing pages on Web + App. UserType collapsed to (Visitor, Admin) per D-186.
- **Registration open/close gate (FR-216)** — `/admin/registration-gate` + `OperationsToggles.razor`.
- **Badge & access — QR entry path (FR-301/302/303/304)** — badge issue/payload, QR lookup, gate scans + currently-inside + scans XLSX, gate management + operator console + dashboard (full Gates policy set).
- **Forum programme (FR-401/402/404/406/408)** — Themes, sessions, halls, speakers CRUD; public programme + speakers endpoints; agenda/session-detail/speakers on App; timeline + delegations on CP.
- **Exhibition (FR-603/604)** — booth directory (public + admin), sponsors (3-tier), companies + account provisioning; booths/sponsors/archive on App.
- **Engagement (FR-703/705/706/707)** — send-question + moderator queue (moderate/hide/push/reorder), session-moderator grants + live moderation desk, audience comments with AI-filter → review desk.
- **Media, news & archive (FR-1001–FR-1005)** — gallery, news (public + PR admin), media partners, archive with CP-controlled visibility; all on App.
- **Notifications — in-app inbox (FR-901 in-app)** — list/unread/read/read-all/delete on API, CP, Web, App. *(Qualifier: the inbox is complete, but the FR-902/903 session-reminder / booking / not-attended trigger events are NOT yet generated — see §1.)*
- **Interests / networking base (FR-801)** — interests CRUD, selection in sign-up, `meet-like-you` + `mkp_meet_people` (Connect deferred, §1).
- **Feedback / ratings (FR-708)** — `/feedback/rate` + admin ratings + `mkp_rate`; AI session-summary preview.
- **Operation log + application logs (FR-1205)** — operation log + log viewer/tail/download.
- **Dynamic categories (FR-1204)** — profile types, interests, countries, themes, roles all CP-managed.
- **Health/ops (EIR-05)** — `/health` endpoint per the Stage-6 runbook.

---

## Cross-cutting notes

- **Permission system is the headline functional gap** (§3 rows FR-1201/1202 + nav filtering): the
  `Permission`/`RolePermission` storage exists and is seeded with 6 codes, but enforcement, the
  full catalogue, nav filtering, and the assignment UI are not built. This is the subject of the
  approved follow-up build (roles-only assignment, JWT-baked permissions, Administrator = `*`).
- **Freeze posture:** D-110 froze EF schema + enum names/values + migration history; D-186 (partial)
  and D-199 (broad, additive App tables) lifted portions. **Identity schema + enum rename/reorder remain
  frozen.** The approved permission build needs **no schema change** (roles-only).
- **Architecture-refactor backlog (does not block features):** Domain-on-Identity decoupling (SEV-1.1),
  shared-DbContext split (SEV-1.3, partly addressed by D-157), `*Service` placement (SEV-1.6),
  `ITokenIssuer` extraction. `CancellationToken` propagation is blocked by the `UserManager` API until
  SEV-1.1 lands.
- **Test-coverage gaps (deferred, not feature gaps):** full bUnit CP harness backfill across ~40 pages
  (D-191 shipped base + a few), Website coverage backlog (D-194), OpenAI-provider unit tests (D-176).
  This is the target of the planned E2E phase.
- **Mockup numbering quirk:** the mockup footnote labels use "06" twice (Photo verification + Email OTP);
  printed labels run to 41 with one repeat. Screen #06 photo-verify and #08 exhibitor sign-up are both
  descoped (§4).
