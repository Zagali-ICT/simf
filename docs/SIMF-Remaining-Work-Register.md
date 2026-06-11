# SIMF — Remaining-Work Register

| Field | Value |
|-------|-------|
| Date | 2026-06-08 |
| Verified against | Branch `feature/app-cp-api-split` at **DECISIONS_LOG D-350** |
| Status | **Authoritative.** Supersedes `SIMF-Implementation-Gap-Report.md` (snapshot 2026-05-31, now stale) |
| Purpose | The single, accurate answer to "what is remaining?" — every open item tagged by who can do it |

## How to read this

Each item is tagged:

| Tag | Meaning |
|-----|---------|
| **DOABLE-NOW** | An in-repo agent/developer can finish it with no external input. |
| **BLOCKED-OWNER** | Needs an owner decision or data before it can be built. |
| **BLOCKED-EXTERNAL** | Needs a third-party deliverable (designer assets, procurement, NCA clearance). |
| **OPS** | Done by a human operator at deploy time, not in the repo. |
| **DONE-RECENTLY** | Shipped D-336→D-350 — listed only to stop it being re-raised. |

> **Accuracy note.** A 2026-06-08 cross-cutting scan found the old
> `SIMF-Implementation-Gap-Report.md` (2026-05-31) lists **shipped** features as
> "not implemented" (it predates ~20 decisions). Items below were re-checked
> against the **code at D-350**, not that report. Three contested "missing" claims
> were verified **shipped**: the permission system (`PermissionCatalog.cs` = **178**
> codes, enforced), notification triggers (`NotificationKind` 40/41/42 present),
> booking approval (`SeatReservationService` + `BookingStatus`/review columns), and
> speaker files (`SpeakerPresentation` + `ISpeakerPresentationStorage`). Anything I
> could not confirm firsthand is marked **(unverified)**.

---

## Section 1 — Done recently (D-336 → D-350) — do NOT re-raise

These landed in the days before this register and account for most of what older
notes still call "remaining".

| Dec | What shipped |
|-----|--------------|
| D-336 | Website landing editorial sections (About / stats / pillars / goals) made CMS-driven |
| D-337 | Website landing skeleton-loading + render-resilience fix |
| D-338 | CP FAQ + MeetingTables raw tables → `SimfDataGrid` — **all CP list pages now on the standard** |
| D-339 | CP dashboard live event stat cards (UI/UX Tier 4) |
| D-340 | CP shell responsive on phones (UI/UX Tier 3) |
| D-341 | "Halls visual gallery" — analysed and **deliberately not built** (no spec, no image field) |
| D-342 | Flutter media gallery renders tile bitmaps; **scope finding**: other image surfaces need a backend pipeline (see §2.1) |
| D-343 | Website landing 404 fixes + a standing **delivery-verification-gate** rule (live DOM check before "done") |
| D-344 | Website partners marquee RTL direction; WEB-005/006/007 diagnosed (not guessed) |
| D-345 | Seeded a demo speaker roster (8 speakers) |
| D-346 | Speaker portraits render from seeded test photos (no migration) |
| D-347 | Website archive per-year page (mockup 24-01) + seeded 2022–2025 editions |
| D-348 | Website partners logo strip + sessions card polish |
| D-349 | **Live-video provider = YouTube (PoC)** + HLS/MP4 fallback + sign-language toggle + URL validation — resolves the D7 live-feed deferral (interim) |
| D-350 | Website landing stored-DOM-XSS hardening (output-encode all admin-entered values) |

Also confirmed shipped earlier (the stale report wrongly flags these as missing):
per-page/per-action **permission system** (D-207/208, 178 codes, enforced API+CP),
**booking approval** workflow (D-227), **speaker presentation files** (D-228),
**System Configuration** (D-229), **Venue-Map 2D nodes** (D-230), **notification
triggers** BookingConfirmed/SessionReminder/BookingRejected (D-217/D-227),
**FAQ** (D-218), **Networking** (D-224).

---

## Section 2 — DOABLE-NOW (genuinely remaining, no external blocker)

| # | Item | Evidence | How |
|---|------|----------|-----|
| 2.1 | **Per-entity image pipeline** (the one substantial remaining build). Speaker photos, sponsor/booth/media-partner logos, news/archive covers each carry only a `*RelativePath` **string** — no upload endpoint, no stored bytes, no anonymous serving route. Only `media` has the full pipeline. | `PublicMediaEndpoints.cs` (`/media/{id}/image`+`/thumbnail`) is the only serve route; `Sponsor.cs`/`News.cs`/`MediaPartner.cs`/`ArchiveEdition.cs`/`Speaker` carry `*RelativePath` only (D-342 scope finding). | Per entity: upload endpoint + out-of-row byte store + anonymous serve endpoint + CP upload UI + app/Website render. **Note:** adds App tables/columns — within the D-199/D-219 additive lift, but the D-110 freeze must be re-instated before publish, so do this *before* §3 freeze-seal or argue it as the last additive wave. |
| 2.2 | **~23 missing CP per-page reference docs** (DoD D-246). | `docs/pages/PAGE-INDEX.md` rows with "—" in the Doc column (e.g. organisations, contacts, countries, vips, speaker-presentations, session-categories, bookings, ratings, session-summaries). | Copy `docs/pages/_TEMPLATE.md`, fill each from the existing E2E catalogue (cross-refs already there). |
| 2.3 | **End-to-end lifecycle integration test** (Registered → EmailVerified → profile → Pending → admin Approve → Approved → App sign-in). | Deferred at Sprint-1 §3.5; each hop tested in isolation, the cross-hop seam is not pinned. | One xUnit integration test driving the full chain against the test host. |
| 2.4 | **Full bUnit harness** (runtime interaction tests for CP/Website components). | Deferred at Sprint-1 §3.4 (markup-source assertions only today). | Add bUnit; convert the key interaction assertions (skip-link focus, dropdown ESC, etc.). |
| 2.5 | **`AllowAnonymous` audit test.** 44 instances exist, all currently legitimate (public reads + auth flows). | No automated guard exists. | A test that fails the build if `AllowAnonymous` ever appears on `/admin/*` or a policy-gated endpoint. |
| 2.6 | **D-110 freeze re-instatement (doc/ceremony half).** | CLAUDE.md: "freeze must be re-instated before the production publish / handover"; lifts D-186/199/211/217/219 + the D-336→D-350 additive work. | Verify no migrations/enum renames beyond the authorised lifts; write the seal entry; confirm the shipped mobile wire contract is preserved. (Owner sign-off is the §3 part.) |
| 2.7 | **Doc-sync.** This register supersedes the gap report; verify no other stale snapshot or "deferred D-7" comment lingers (D-349 already re-pointed the code comments). | gap report (superseded here); PAGE-INDEX vs reality. | Banner + spot-fixes. |
| 2.8 | **Mobile E2E catalogue completeness** (a few screens) (unverified — needs a count vs the 41-screen list). | `docs/tests/e2e/README.md` mobile section. | Author any missing `mobile-*.md` from the template. |

---

## Section 3 — BLOCKED-OWNER (needs your decision/data before build)

| Item | What it blocks | Input needed from you |
|------|----------------|------------------------|
| **D6 — statistics metric list** (OI-1) | The final statistics dashboard spec (basic counts ship today; exact metric set is open). | The exact list of metrics each dashboard should show. |
| **G-OI-2 — venue boundary / GPS geofence chain** (FR-305/506/704/1103) | Arrival recording → session attendance → movement/dwell → question-gating-on-arrival. None built (only `HallAttendance` entity + a geofence migration exist). | The venue-boundary definition + the attendance/geofence rules (and whether this is in V1 at all). |
| **Permission-gated CP nav scope** (D1 / D-018 / D-167) **(unverified)** | Whether the side menu hides items a role can't access. Code comment now reads "the shell hides items…" but a recent scan claimed "not applied yet" — **conflicting signals; confirm in `SimfAppShell` before relying.** | Confirm the intended behaviour; if genuinely off, it's a small wiring change (the *policy/scope* is the owner part). |
| **2FA-every-login policy** (FR-104) | Whether email-OTP is mandatory at every sign-in vs the current behaviour. | Confirm the policy. |
| **Real managed providers** | Whether YouTube (D-349 PoC) + Echo/OpenAI (AI) are the V1 answer or a real managed provider is required. | Confirm provider(s); procurement is then §4. |
| **Which deferred mockup data-gap elements to build** | booth contacts (currently CP-only by owner), sponsor mini-grid, archive cover/stat-cells, meet score, about pillars, rate sub-ratings (all "noted, not built", D-330/D-331). | Pick any you want built (most also need backend data and/or §2.1 image pipeline). |

---

## Section 4 — BLOCKED-EXTERNAL (third-party deliverable)

| Item | Blocks | Owner of the dependency |
|------|--------|--------------------------|
| **SIMF-VID-001 designer assets** | Final visuals across ~14 app screens + Website (currently initials/placeholders: speaker photos, logos, flags, cover art, brand video). | External designer. |
| **NCA security clearance + pen-test** | Production go-live (hard Stage-6 gate). | NCA / MoD + accredited pen-test firm. |
| **App-store publication** | Public app availability. | Apple/Google review + RSNF accounts. |
| **SMS / WhatsApp gateways** (EIR-02) | Any SMS/WhatsApp notification channel. | External gateway provider. |
| **AI / live provider procurement + keys** | A real managed AI provider (beyond Echo/OpenAI) and/or a managed live-video provider beyond the YouTube PoC. | Procurement + the chosen vendor. |
| **YouTube/Google reachability on the event network** (D-349 caveat) | The live player loading on attendee devices at the venue. | Network/NCA posture decision (operational). |

---

## Section 5 — OPS (human operator at deploy time)

| Item | Note |
|------|------|
| **Committed-secrets rotation decision + history scrub** | Sprint-1 §3.1. Super-admin password / JWT key / TOTP seed / ID-doc key sit in committed `appsettings`. **Blocks the origin push.** Choose: rotate+scrub history, or accept-and-`.gitignore`-forward, then deploy with env-var keys. |
| **CI/CD pipeline + load-test scripts** | Not set up. |
| **Production config/env** | Connection strings + all secrets via env vars, not committed files. |
| **`/health` monitoring + alerting** | Endpoint exists; wiring to monitoring is ops. |
| **Stage-6 deployment runbook** | Build → config → migration → health → smoke → monitoring; rollback to last-good folder. |
| **SignalR backplane (Redis vs SQL)** (AR-OI-3) | Deferred to scale-out, "closer to the event". |

---

## Section 6 — What genuinely blocks the production handover

Short critical path (everything else is polish, docs, or in-scope-optional):

1. **NCA clearance + pen-test** — external hard gate; longest lead time (~2 months pre-event). *(§4)*
2. **Committed-secrets rotation** — blocks even pushing the branch to origin. *(§5)*
3. **D6 statistics list** and **G-OI-2 geofence chain** — *only if* the owner wants those features in V1; otherwise they are out-of-scope, not blockers. *(§3)*
4. **SIMF-VID-001 assets** — for final visuals (the product works without them; it just shows placeholders). *(§4)*
5. **D-110 freeze re-instated** — governance gate before publish; the doc half is DOABLE-NOW (§2.6), the sign-off is owner.

Everything in §2 is finishable in-repo now and none of it blocks handover on its own.
