# SIMF App — Build Plan (for the independent Flutter developer)

| Field | Value |
|-------|-------|
| Date | 2026-06-03 |
| Status | Active plan — orchestrates the controlled docs + the per-page folders into build phases |
| Authority | Owner directive ("make plan for app design plan", "do") · decisions in `DECISIONS_LOG` D-249 |
| Audience | The independent Flutter developer who builds the real app against the **App API** (`/api/v1/app/*`) |

This plan is the **entry point**. It does not restate the controlled docs — it
points to them and sequences the work. Read the source-of-truth docs first:

| Concern | Source of truth |
|---------|-----------------|
| App architecture (state, navigation, packages, offline) | [`SIMF-MAA-001`](../SIMF-MAA-001-Mobile-Application-Architecture.md) |
| App API contract (`/api/v1/app/*`, `ApiResult<T>`, errors) | [`SIMF-MOB-API-001`](../SIMF-MOB-API-001-Mobile-API-Requirements.md) |
| Per-screen design template + Screen 14 | [`SIMF-MOB-SDS-001`](../SIMF-MOB-SDS-001-Mobile-App-Screen-Design.md) |
| **Per-page detail (Function/Logic/API/Design)** | [`docs/App/Page_NNN/`](.) — one folder per screen |
| Resolved page-spec decisions D1–D12 | `DECISIONS_LOG` **D-249** |
| Screen number ↔ route map (authoritative, 41 screens) | `src/Mobile/simf_app/lib/app/router.dart` |

> **The current Flutter project is a mockup** for API + UX testing only (owner,
> 2026-06-02). It already wires the router, the typed API clients, and the auth
> state machine against `/api/v1/app/*`; the screens render placeholders. This
> plan is for turning those placeholders into the real app.

---

## 1. Ground rules (do not violate)

1. **Four privileges only** — `Guest(0)`, `Visitor(1)`, `Moderator(2)`, `Staff(3)`
   (Flutter `AppRole`; `Guest` = no JWT). Gate every screen on the **cached**
   privilege. See [`SIMF-MOB-SDS-001`](../SIMF-MOB-SDS-001-Mobile-App-Screen-Design.md) §2.
2. **App API only** — call `/api/v1/app/*`. The `/api/v1/admin/*` surface is the
   Control Panel's and is off-limits (App↔CP split, D-247).
3. **`ApiResult<T>` envelope** — every response is `{success, data, error}`; never
   assume a bare body. Bilingual error text (`message` / `messageArabic`).
4. **RTL-first, Arabic default** — the primary language is Arabic
   ([`SIMF-MAA-001`](../SIMF-MAA-001-Mobile-Application-Architecture.md) §10).
5. **Fetch-all-then-cache on login** — on sign-in the app fetches its data +
   privileges and caches them (owner rule); features read the cache.
6. **Wire contracts are append-only** — the JSON field names the app decodes are
   a contract (D-219). Decode tolerantly (unknown enum → safe default).

---

## 2. Screen inventory & API readiness

Status legend: ✅ **ready** (endpoint shipped under `/app/*`) · 🟡 **to build**
(named in D-249, additive) · ⛔ **blocked** · — **no API** · ⏳ **later wave**.

### Section 1 — Start & entry (screens 1–12)

| # | Route | Privilege | API status | Page doc |
|---|-------|-----------|------------|----------|
| 1 | `splash` | Guest | ✅ `POST /app/auth/refresh` + `GET /app/account/profile` (version = store-native, —) | [Page_001](Page_001/README.md) |
| 2 | `onboarding` | Guest | — (no API; optional `GET /app/content/{key}`) | [Page_002](Page_002/README.md) |
| 3 | `signIn` | Guest | ✅ sign-in, refresh, forgot/reset-password, verify-otp, device-key (biometric) | [Page_003](Page_003/README.md) |
| 4 | `signUpType` | Guest | — (visitor-only app; UI gate) | [Page_004](Page_004/README.md) |
| 5 | `signUpForm` | Guest | ✅ `POST /app/auth/sign-up` (D-198 enumeration-resistant; generic 201) | [Page_005](Page_005/README.md) |
| 6 | `emailOtp` | Guest | ✅ `POST /app/auth/verify-email` + `resend-code` | [Page_006](Page_006/README.md) |
| 7 | `signUpVisitor` | Visitor | ✅ user-profile upsert + countries + profile-types + interests + `GET /app/organisations` (interests picker is a sub-step here) | [Page_007](Page_007/README.md) |
| 8 | ~~`signUpExhibitor`~~ | — | **removed from the app (D-276)** — exhibitor/sponsor are CP concepts (D-199) | _n/a_ |
| 9 | `terms` | Guest | ✅ `GET /app/content/{key}` (`terms`; accept is client-side, D8 consent record deferred) | [Page_009](Page_009/README.md) |
| 10 | `registrationSuccess` | Visitor (pending) | ✅ `GET /app/users/me` (status poll) | [Page_010](Page_010/README.md) |
| 11 | `registrationStatus` | Visitor (pending) | ✅ **`GET /app/users/me`** (built this wave, D-249) | [Page_011](Page_011/README.md) |
| 12 | `guestMode` | Guest | — | _n/a (not in this batch)_ |

### Section 2 — Core screens (13–20)

| # | Route | Privilege | API status | Page doc |
|---|-------|-----------|------------|----------|
| 13 | `home` | Guest+ | 🟢 **screen built (D-296)** · `…/notifications/unread-count` (count) · `GET /app/bootstrap` (D-251) · live banner deferred (D10) | [Page_013](Page_013/README.md) |
| 14 | `myArea` | Visitor | 🟢 **screen built (D-297)** · `GET /app/account/dashboard` + `calendar.ics` + `contact-card.vcf` (D-249) — unions held bookings + speaker meetings + confirmed business meetings | [Page_014](Page_014/README.md) |
| 15 | `venueMap` | Guest | 🟢 **screen built (D-298)** · `GET /app/venue-map` (D-230) + `GET /app/booths` | [Page_015](Page_015/README.md) |
| 16 | `sessions` (renamed from `agenda`, D-276) | Guest+ | 🟢 **screen built (D-299)** · `GET /app/programme/sessions` (D-199/D-252/D-271) — fetch-once + client-side pills/day-strip/search | [Page_016](Page_016/README.md) |
| 17–20 | `sessionDetail`, `mySeat`, `speakers`, `speakerProfile` | Guest/Visitor | ⏳ existing `Endpoints/Programme/*`, `Endpoints/Sessions/SeatReservation*`, `Endpoints/Public/PublicSpeaker*` (later wave) | — |

### Sections 3–8 — content, live, media, badge, smart, settings (21–41)

⏳ **Later waves.** The backend families already exist under `/app/*` for most of
these (booths, sponsors, archive, live Q&A, audience comments, news,
media gallery, media partners, badge/QR, notifications, AI summary, meet-people,
chatbot, about, accessibility, cybersecurity, rate/feedback). They are catalogued
at the index level in [`SIMF-MOB-API-001`](../SIMF-MOB-API-001-Mobile-API-Requirements.md)
§7–§14 and will get their own `Page_NNN/` folders + per-page detail in the next
documentation waves, screen by screen, as they are built.

---

## 3. Build phases (recommended order for the Flutter dev)

Each phase ships a vertical slice: real screens + state + the live `/app/*` calls,
following the matching `Page_NNN/` Function/Logic/Design docs.

| Phase | Screens | Why first | Gate |
|-------|---------|-----------|------|
| **P0 — Foundation** | app shell, router, theme/RTL, the typed `/app` API client, the auth state machine, secure token store | everything else depends on it | matches `SIMF-MAA-001` |
| **P1 — Auth** | 1 splash, 3 signIn (+ biometric), 5/6 sign-up + OTP, forgot/reset | the only "must be ready" surface (owner) — and it **is** ready server-side | sign-in → token cached |
| **P2 — Onboarding & registration** | 2 onboarding, 4 type, 7 profile + interests, 9 terms, 10 success, 11 status | completes the new-visitor journey end-to-end; all APIs ready (11 just built) | profile complete → pending → approved |
| **P3 — Home & identity** | 13 home (privilege-gated tiles), 14 My-Area | the signed-in landing + the personal dashboard | 13 + 14 **screen built (D-296 / D-297)** |
| **P4 — Core event** | 15 map, 16–20 sessions/detail/seat/speakers | the core attendee value | existing endpoints — 15 **built (D-298)**, 16 sessions **built (D-299)** |
| **P5+ — Content / Live / Media / Smart / Settings** | 21–41 | breadth, per the later doc waves | per-screen as documented |

---

## 4. What is ready **today** vs pending

**Ready now (build against these immediately):**
- The entire **Auth** surface (`/app/auth/*`) — sign-in, refresh, sign-up,
  verify-email, resend, forgot/reset-password, verify-otp, TOTP, device-key.
- **Account**: profile, avatar, user-profile upsert, the lookups (countries,
  profile-types, interests, organisations), notifications, **`users/me`** (new).
- **Public**: content blocks (`terms` etc.), venue-map, booths, speakers, news,
  media, sponsors, sessions/agenda.

**Recently shipped (D-249 / D-250 / D-251):**
- ✅ `GET /app/users/me` (Screen 11 registration status) — D-249.
- ✅ `GET /app/account/dashboard` + `calendar.ics` + `contact-card.vcf` (Screen 14)
  — the My-Area dashboard, unioning held bookings + accepted speaker meetings +
  confirmed business meetings (D-248/D-250). See [Page_014](Page_014/README.md).
- ✅ `GET /app/bootstrap` (Screen 13 on-login bundle) — current user + unread
  count + server time, composed from existing reads (D-251).
- ✅ **FDS-014 Contact sharing (D-281 → D-287)** — the org `Contact` directory
  (CP `/admin/contacts` + picker in the five admin forms) and the visitor
  contact-sharing **app API** are live; public Sponsor/MediaPartner cards carry
  the additive contact cluster (D-287). The **Flutter screens are not built yet**
  (see the to-do below).

**Flutter to-do — FDS-014 Contact UI (API ready; screens pending):** the
server side is shipped and tested; these screens bind to the live `/app/*`
endpoints and are ready to build in the app **page-by-page workflow**. Add the
routes only when `router.dart` is clean — do not collide with in-flight mobile
edits.

1. **Shared read-only contact-card widget** (FDS-014 Slice D) — reused by the
   org screens and by the scan-preview.
2. **Share my contact** — mint/show the QR (`GET /app/account/share-token`,
   `POST /app/account/share-token/rotate`) + OS share-intent vCard (client
   builds from the card DTO, or `GET /app/contacts/{id}/vcard`).
3. **Scan QR** → `POST /app/contacts/resolve` → preview the card →
   `POST /app/contacts/save`.
4. **My Contacts** list — `GET /app/contacts`, remove via
   `DELETE /app/contacts/{id}`.

- Privilege: **Visitor**; app audience; **no permission code** (matches
  `Connection` — `RequireApprovedAccount` + app token).
- Wire contract is **append-only** (D-219); decode tolerantly.
- E2E catalogue already authored:
  [`mobile-my-contacts.md`](../tests/e2e/mobile-my-contacts.md) (E2E-MMC-001..011).
- The **Website** read-only contact-card stays deferred until the Website has
  public org pages to host it (FDS-014 §13; not Flutter work).

**No App-API builds remain open for the owner's page batch.** The remaining
items are the explicitly **deferred** ones below.

**Deferred (not in this version) — do not build app paths that depend on them:**
- D4 Nafath sign-in, D8 server-side T&C consent record, D10 live/YouTube banner,
  D11 mockup decorations (approval ref#+date, booth logo+hall-name).
- **D1 — configurable 5-day session** → moved to **V2** (owner, 2026-06-03);
  see [`SIMF-V2-Plan.md`](../SIMF-V2-Plan.md) **V2-02**. The V1 refresh-token
  lifetime stays at its current 30-day value.

---

## 5. Definition of Done (per screen)

A screen is "done" only when, in the **same changeset** (project rule D-246):
1. The screen + state + the live `/app/*` calls are implemented per its
   `Page_NNN/` Function/Logic/Design docs.
2. The `Page_NNN/` docs are updated to "built" (no stale `(TO BUILD)`).
3. Unit/widget + integration coverage exists; the backing API has its xUnit tests.
4. The E2E catalogue file (`docs/tests/e2e/mobile-{slug}.md`) is authored + indexed
   (see [`mobile-registration-status.md`](../tests/e2e/mobile-registration-status.md)
   as the pattern).
