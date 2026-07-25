# SIMF App — Offline Cache & Version-Based Sync (Design Proposal)

**Status:** Draft for owner sign-off. **Not approved, not implemented.** This is the
design doc requested as owner batch item 16 ("offline plan: cache + version-based
sync"). Nothing in the app or backend changes until the owner signs off the open
decisions in §9. On sign-off the accepted decisions get a decisions-log entry
(next free id ≈ D-728 at time of writing — verify against the log tail then).

**Last updated:** 2026-07-09

**Scope of this proposal.** How the Flutter app should (1) keep working — read-only —
when the device is offline or on a flaky connection, and (2) avoid re-downloading
unchanged catalog data on every screen open, by syncing only what changed since the
last fetch ("version-based sync"). It is deliberately phased so the cheap, zero-risk
part (offline reads with **no** backend/schema/wire change) can ship first, and the
expensive parts (delta endpoints, a local database, offline writes) are separate,
independently-approvable increments.

---

## 1. Why — the problem today

Every data screen is **network-only**. A screen mounts → a Riverpod
`FutureProvider.autoDispose` → a feature repository → `SimfApiClient.get(...)` →
model `fromJson`. There is no read-from-cache tier. Concretely
(`src/Mobile/simf_app`):

- `SessionsRepository.getSessions()` fetches the **whole** programme every time the
  Sessions tab opens — `client.get('/app/programme/sessions', ...)`
  (`features/sessions/data/sessions_repository.dart:18`); the comment even notes it
  "fetches the whole programme once … the UI filters it inline".
- `NewsRepository`, `SpeakersRepository`, sponsors, booths, media, archive, faq — all
  the same network-only shape; on connection loss they throw `ApiFailure(clientNetwork)`
  and the screen shows `SimfErrorState` (`lib/app/widgets/simf_page_shell.dart:1076`).
- List providers are `autoDispose` with **no `keepAlive`** — data is dropped on
  unmount, so even re-opening a tab within one session re-downloads everything.

Consequences: (a) the app is unusable in a poor-signal hall except for the two things
already cached (the auth session and the About/organisation profile); (b) repeated
full downloads of catalog data that rarely changes waste bandwidth and battery and make
every screen feel slow after the first paint.

**The one thing that already works offline** is the pattern we should generalise: the
public organisation profile. `OrgProfileRepository` (`lib/core/organization_profile/organization_profile.dart:183`)
reads a cached JSON from `shared_preferences` **synchronously on the first frame**
(`loadCached()`), then revalidates over the network with
`SimfApiClient.getConditional('/app/organization-profile', ifModifiedSince: <stored token>)`
(D-495), keeping the cache on a `304` or on any network error. This is exactly the
cache-then-network + conditional-GET shape a general offline layer needs — it is just
wired to a single endpoint today.

---

## 2. What already exists (the reusable foundation)

Grounded inventory — the design builds on these, it does not reinvent them.

### App side

| Capability | Where | Reuse |
|---|---|---|
| Single Dio client, all traffic funnelled through it | `SimfApiClient` (`packages/simf_data_pkg/.../simf_api_client.dart`) | The one place to add a cache tier / read interception. |
| Conditional GET (`If-Modified-Since` → `304`, returns `Last-Modified`) | `getConditional<T>()` + `ConditionalResponse<T>` (`simf_api_client.dart:290`) | Generalise from 1 caller to N. |
| Cache-then-network repository pattern | `OrgProfileRepository` (`organization_profile.dart:183`) | Template for a shared `CachedCatalogRepository`. |
| Key-value store for non-secret cache | `shared_preferences` → `SimfPrefsStorage`; already holds `orgProfileJson` + `orgProfileLastModified` (`storage/storage_keys.dart:45`) | Phase-0 cache backing store (public data only). |
| Encrypted store for secrets/PII | `flutter_secure_storage` → `SimfSecureStorage`; holds tokens + `currentUserJson` (offline-resume of the session) | Where any user-scoped cache must live (NCA). |
| Shared error / empty / pull-to-refresh widgets | `SimfErrorState`, `SimfEmptyState`, `SimfPullToRefresh` (`simf_page_shell.dart:1076,1111,340`) | Add an "offline / showing saved data" variant. |
| Session offline-resume | `AuthController` restores `currentUserJson` and tolerates `NetworkUnavailable` at cold start (`auth_controller.dart:465`) | Proof the offline-degraded path already exists for auth. |

### Backend side

| Capability | Where | Reuse |
|---|---|---|
| `UpdatedAt` on essentially every app entity | `BaseAuditEntity.UpdatedAt` (`SIMF.Domain/Common/BaseEntity.cs:34`), stamped centrally by `AuditStampingSaveChangesInterceptor` (`...:69`) | The per-row "last modified" a delta sync needs — **already in the DB**, just not on the DTOs. |
| Soft-delete columns | `BaseAuditEntity.IsActive` + `DeletedAt` (`BaseEntity.cs:39-40`) | Detectable deletions → "tombstones" for sync. |
| Full server-side change ledger | `RowAudit` (D-109, `RowAuditingSaveChangesInterceptor.cs:154`) logs every INSERT/UPDATE/DELETE with `OccurredAt`/`TableName`/`PrimaryKey`/`Operation` | Natural backing store for a `/app/changes` delta endpoint. |
| Proven conditional-GET | `Last-Modified`/`304` on org-profile + CMS (`OrganizationProfileEndpoint.cs:33`, `PublicCmsEndpoints.cs:41`); strong ETag/`304` on assets (`AssetEndpoints.cs:64`) | Copy to the JSON list reads. |
| Free metadata slot in the envelope | `ApiResult<T>.Meta` (`SIMF.Common/ApiResult.cs:21`) — present, currently always null | Carry a sync token / version without touching any `Data` contract. |
| Append-only wire discipline | D-219 — new fields = trailing defaulted constructor params (e.g. `PublicSessions.cs:37`) | Additive freshness fields are safe. |
| Offset pagination + totals | `PublicNewsPage`, `PublicMediaPage`, `GridPage<T>` | Catch-up paging for large deltas. |

### What is missing (the actual gaps)

- **No local database.** Only `shared_preferences` + `flutter_secure_storage`; no
  `sqflite`/`drift`/`hive`/`isar`. Per-row upsert/delete merge is not possible today.
- **No per-row change token on the wire.** DTOs expose `publishedAt` / `start` /
  `createdAt` (schedule/editorial times), not the audit `UpdatedAt`. Only
  `PublicContentBlock.LastUpdatedAt` (`Contracts/Cms/ContentBlocks.cs:9`) is a true
  row-modified stamp on the wire.
- **No delta / `since` query on any app list.** Every content list is a full fetch.
- **No global data-version or "what changed" endpoint.** `/app/bootstrap` returns only
  a server clock + user + unread count (`AppBootstrapEndpoint.cs:47`); SystemSettings
  (D-229) is admin-only.
- **No connectivity layer** (`connectivity_plus` absent) and **no mutation queue** —
  writes go straight to the network and throw on failure.

---

## 3. Constraints this design must respect

1. **D-110 schema freeze / no `RowVersion`.** We do **not** add a `[Timestamp]`
   concurrency column. The design uses the **existing** `UpdatedAt` / `DeletedAt` —
   no schema change is required for sync. (Any new *table*, if a phase needs one, is a
   separate freeze-lift request.)
2. **D-219 shipped-wire-contract freeze (append-only).** Every new DTO field is a
   trailing, defaulted constructor param so older installed apps keep decoding. No
   field is renamed, reordered, or removed. Prefer the `ApiResult<T>.Meta` slot for
   sync metadata so `Data` contracts are untouched.
3. **D-157 Data ↔ Identity separation.** The sync layer is App-DB only. User-identity
   data is never joined or duplicated into a catalog cache; user-scoped reads resolve
   as they do today.
4. **NCA / privacy — what may be cached where.** Public `AllowAnonymous` catalog data
   (sessions, news, speakers, sponsors, booths, media, archive, faq, org profile, site
   settings) may be cached in **plaintext** (`shared_preferences` / an unencrypted DB) —
   it is already world-readable. **User-scoped or PII data** (my-area, my-sessions,
   notifications, requests, badge/QR, ID documents, profile) must **not** be cached in
   plaintext: cache it only in `flutter_secure_storage` or an **encrypted** DB, and
   **purge it on sign-out**. Tokens stay in secure storage (already the case). ID-image
   / avatar bytes are never persisted to an offline store.
5. **D-443 session caps.** Offline mode never extends or forges a session. If the
   access token is expired and the device is offline, cached **public** data still
   renders (read-only); anything requiring auth shows the existing signed-out / retry
   state. The 24h absolute cap stays server-enforced.
6. **Owner rules (memory).** Reuse/extend shared `Simf*` widgets (no page-local
   copies); every data page keeps pull-to-refresh; responsive width; ASK-don't-guess on
   any ambiguity. A new cache layer is a **shared Flutter foundation** → D-694
   blast-radius rule: land it behind the existing `simf_data_pkg` seam with its own
   tests before any feature adopts it.

---

## 4. Proposed model

### 4.1 Two data classes, two policies

| Class | Examples | Cache store | Sync policy |
|---|---|---|---|
| **Public catalog** (read-only, `AllowAnonymous`) | sessions, days, news, speakers, sponsors, booths, media, archive, faq, org profile, site-settings, content blocks, banners | plaintext (prefs blob → later local DB) | cache-then-network + conditional revalidate; render cache offline |
| **User-scoped** (auth-gated, PII) | my-area, my-sessions, notifications, requests, badge/QR, profile | encrypted (`flutter_secure_storage` / encrypted DB) or **not cached** | opt-in per screen; purge on sign-out; never plaintext |

The bulk of the offline win is the **public catalog** — and it carries no privacy
risk. The recommendation is to cache the public catalog aggressively and treat
user-scoped caching as a small, explicit, encrypted add-on (or skip it for v1).

### 4.2 Version-token model — per-collection high-watermark + tombstones

Recommended over a single global data-version, because collections change
independently and a per-collection watermark minimises over-fetch:

- The app stores, per collection, the **max `UpdatedAt` it has seen** (the
  "watermark") plus the cached rows.
- A delta request sends that watermark: `GET /app/programme/sessions?since=<utc>`.
  The server returns rows with `UpdatedAt > since` **plus tombstones** (ids of rows
  soft-deleted since then, from `IsActive=false`/`DeletedAt` or `RowAudit`), and the
  new max `UpdatedAt` in `ApiResult.Meta`.
- The app upserts changed rows, removes tombstoned ids, advances the watermark.
- **First run / no watermark** = today's full fetch (server ignores `since`).

A cheap **global gate** sits on top (Phase 2+): `GET /app/changes` returns a small
map `{ "sessions": <maxUpdatedAt>, "news": <maxUpdatedAt>, ... }` computed as
`MAX(UpdatedAt)` per table, so the app can skip syncing collections whose max hasn't
moved — one tiny request instead of N conditional GETs.

### 4.3 Read flow (generalised from the org-profile pattern)

```
provider.build():
  1. emit cached rows immediately (synchronous, if present)  → instant first paint, works offline
  2. if online: revalidate
       - Phase 0/1: conditional full GET (304 = keep cache)
       - Phase 2:   delta GET ?since=watermark → merge
  3. on success: update cache + watermark, emit fresh rows
  4. on network error: keep showing cache, surface a non-blocking "offline / saved data" hint
```

Pull-to-refresh (already everywhere) becomes "force revalidate now".

### 4.4 Connectivity layer

Add `connectivity_plus` behind a single `ConnectivityService` provider exposing an
`online`/`offline` stream. Uses: (a) drive the offline banner / stale-data hint;
(b) let the read flow skip a doomed network call and go straight to cache; (c) in a
later phase, trigger draining of a mutation queue on reconnect. It is advisory only —
the transport error boundary (`_mapDioException`) stays the source of truth for an
actual failed request.

---

## 5. Options & recommendation

### Local store

| Option | Pros | Cons | Verdict |
|---|---|---|---|
| **A. Extend `shared_preferences` JSON blobs** (one blob per collection) | Zero new dependency; matches the shipped org-profile cache; trivial | Whole-blob rewrite on any change; no per-row upsert/tombstone; weak for large lists / true deltas | **Phase 0/1** — enough for cache-then-network + conditional full-fetch |
| **B. Embedded DB — `drift`** (SQL, reactive, codegen) | Per-row upsert/delete, indexing, tombstones, partial delta merge, reactive queries; SQLCipher option for the encrypted (user-scoped) tier | New dependency + codegen; migration management; ~1–2 days plumbing | **Phase 2** — required for true delta sync |
| C. Embedded DB — `hive`/`isar` (NoSQL) | Fast, simple, less boilerplate than SQL | Weaker relational/query story; another ecosystem to learn; isar maintenance flux | Not recommended — `drift` fits the relational catalog better |

**Recommendation:** blobs now (Phase 0/1), `drift` when we commit to true deltas
(Phase 2). Don't add a DB before the delta endpoints exist — it buys nothing until then.

### Sync signal

| Option | Pros | Cons | Verdict |
|---|---|---|---|
| **1. Conditional full-fetch** (`Last-Modified`/ETag → 304 on the list) | Tiny backend change (reuse the proven pattern); no new query semantics; `304` = zero-byte revalidate | Still transfers the whole list when *anything* changed | **Phase 1** — big win for cheap |
| **2. Per-collection `?since=` delta + tombstones** | Minimal transfer; scales to large catalogs | Needs `UpdatedAt` on DTOs, tombstone semantics, watermark plumbing, a local DB | **Phase 2** — the real version-based sync |
| 3. Global monolithic `dataVersion` | One flag to check | Any change busts the whole cache; over-fetches | Rejected as the primary model; kept only as the cheap `/app/changes` gate in §4.2 |

---

## 6. Phased rollout (each phase independently approvable)

**Phase 0 — Offline reads, no backend change (app-only).**
Generalise the org-profile pattern into a shared `CachedCatalogRepository<T>` in
`simf_data_pkg`: cache the last successful full fetch per public collection in prefs,
render it on cold start / offline, revalidate on network. Add `ConnectivityService`
and an "offline — showing saved data" hint on the shared page shell. **No wire, schema,
or DTO change.** Delivers a usable offline app for all public catalog screens. *Risk:
low; app-only; behind the existing data-package seam.*

**Phase 1 — Cheap revalidation (additive wire).**
Add `Last-Modified`/`If-Modified-Since` → `304` (and/or an ETag) to the public JSON
**list** reads, reusing `OrganizationProfileEndpoint`/`PublicCmsEndpoints` as the
template; optionally project the existing `UpdatedAt` onto the list DTOs as a trailing
appended field. The app switches its Phase-0 revalidation to `getConditional`, so an
unchanged collection costs a single `304`. *Risk: low; additive-only per D-219; no
schema change (`UpdatedAt` already exists).*

**Phase 2 — True delta sync (additive endpoints + local DB).**
Add `?since=<utc>` to the list endpoints (server returns changed rows + tombstones +
new watermark in `Meta`), backed by `UpdatedAt`/`DeletedAt` (or `RowAudit`); add the
`GET /app/changes` global gate; introduce `drift` for per-row merge and a per-collection
watermark. *Risk: medium; new endpoints + a DB dependency + merge logic; still no
schema change if built on existing columns.*

**Phase 3 — Offline writes (mutation outbox). OUT OF SCOPE unless requested.**
Queue user actions (e.g. session booking, rating, moderator questions) when offline and
replay on reconnect with idempotency keys + conflict handling. *Risk: high — conflict
resolution, idempotency, interaction with D-443 auth caps while offline. Recommend
deferring past the event; most user writes are time-sensitive and better failed-fast
than silently queued.*

---

## 7. Security & compliance checklist (applies to every phase)

- Public catalog cache = plaintext OK (already world-readable, `AllowAnonymous`).
- User-scoped / PII cache = encrypted store only, **purged on sign-out**; ID-image and
  avatar bytes never persisted offline.
- Tokens remain in `flutter_secure_storage` (unchanged); offline mode never mints,
  extends, or forges a session (D-443 intact).
- No Identity data enters the App catalog cache (D-157).
- A "clear cached data" affordance (Settings) and automatic purge on sign-out / account
  switch.
- Cache staleness is always visible to the user (the "showing saved data" hint) — never
  present stale data as live.

---

## 8. Effort & impact (rough)

| Phase | Backend | App | New deps | User-visible win |
|---|---|---|---|---|
| 0 | none | ~2–3 days | `connectivity_plus` | App works offline for all public screens; instant re-open |
| 1 | ~1–2 days (headers on list reads) | ~1 day | none | Unchanged data revalidates as a `304` — near-zero traffic |
| 2 | ~3–5 days (`?since=` + `/app/changes`) | ~3–4 days | `drift` (+ SQLCipher if user-scoped) | Only changed rows transfer; scales to large catalogs |
| 3 | ~1 week+ | ~1 week+ | — | Offline writes (deferred; high risk) |

---

## 9. Open decisions — owner sign-off needed

1. **How far do we go?** (a) Phase 0 only (offline reads, no backend change); (b)
   Phases 0–1 (adds cheap `304` revalidation); (c) Phases 0–2 (full version-based
   delta sync). *Recommendation: approve Phase 0 now (cheap, zero-risk, biggest UX
   jump); scope Phase 1 next; treat Phase 2 as a follow-on once Phase 0 is live.*
2. **Cache user-scoped data offline at all?** Yes (encrypted, purge-on-sign-out) or
   No (public catalog only for v1). *Recommendation: No for v1 — public catalog is the
   whole win with none of the privacy risk.*
3. **Local store when we reach Phase 2:** `drift` (recommended) vs. `hive`/`isar`.
4. **Phase 3 offline writes:** confirm deferred past the event (recommended) or name
   the specific actions that must work offline.

Until these are answered, **no code lands**. On sign-off, the accepted set becomes a
decisions-log entry and each phase follows the normal per-change DoD (implement →
tests → analyze/build → live device check → E2E/docs same changeset → review +
simplify → commit).
