# Programme sessions — Logic (`/admin/sessions`)

The state + data model behind the page: the `Session` entity, the lifecycle state
machine, soft-delete, capacity resolution, the M-to-M sets, audit, and how the
catalogue reaches the app (resolve-on-read across the two databases). Verified
against code this session.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> Companion docs: [Design](admin-sessions_Design.md) ·
> [API](admin-sessions_API.md) · [Function](admin-sessions_Function.md).

## L-1 — Data model

A **`Session`** row lives in `dbo.Sessions` on `SimfAppDbContext` with the
many-to-many join sets `SessionSpeakers` (ordered, with a per-join
`SessionSpeakerRole`) and `SessionThemes`. The CP works through two contracts:

- **`AdminSessionSummary`** — the grid projection. Carries the hall name EN+AR
  inline (no second fetch) and the **effective** `Capacity`, `IsActive` and
  lifecycle `Status`. Built by `AdminSessionSummaryService` / `ListAllAsync`.
- **`AdminSessionDetail`** — the full record for View/Edit, including the ordered
  `Speakers` (`AdminSessionSpeakerEntry` with `Role`), `ThemeIds`, the recording
  metadata, `PublishedAt`, the live URLs, and both `CapacityOverride` and the
  resolved `EffectiveCapacity`. Built by `IAdminSessionService.GetAsync`.

The grid summary deliberately **omits** speakers/themes/recording/live URLs, so
Edit/View/Delete each `GET .../sessions/{id}` for the full detail first
(`LoadDetailAsync`) — editing from a summary-only form would wipe those sets.

## L-2 — Lifecycle state machine (`SessionStatus`, D-231)

`SessionStatus` is an int-backed, additive-only enum:
`Scheduled = 0`, `Held = 1`, `Recorded = 2`, `Published = 3`. Every new session
starts `Scheduled`. The Committee drives **adjacent** moves only — the CP form
offers exactly the legal next move(s) via `NextTransitions(current)`:

```
Scheduled ⇄ Held ⇄ Recorded ⇄ Published
```

`Publish` stamps `PublishedAt`; `Un-publish` returns to `Recorded`. The
**service** enforces the same adjacency server-side — an illegal jump is a 400
`SESSION_STATUS_TRANSITION_INVALID`; setting the same status is an idempotent
no-op. Status is **distinct from `IsActive`**: `Status` is the broadcast lifecycle;
`IsActive` is the soft-delete flag. A session can be Scheduled-and-active or
Published-and-active; a soft-deleted session keeps whatever status it had.

## L-3 — Soft-delete

`DELETE .../sessions/{id}` is a **soft-delete** (`service.DeactivateAsync` →
`IsActive = false`), gated by `Sessions.Delete` and a `SimfConfirm`. The row stays
in the grid with the grey "Inactive" pill. The app's public reads filter on the
active set, so a deactivated session disappears from the agenda without losing its
history.

## L-4 — Capacity resolution

`EffectiveCapacity = CapacityOverride ?? HallCapacity`. A **blank** override in the
form is sent as `null` and the View form shows "Inherits from hall"
(`Admin.Sessions.Field.CapacityInherits`); a non-negative integer overrides it.
The grid's `Capacity` column shows the resolved effective value.

## L-5 — The relational sets

- **Hall** (mandatory) — a `HallId` Guid resolved against the active Halls lookup
  (`/admin/halls`). An inactive/unknown hall is rejected 400
  `SESSION_HALL_NOT_FOUND`.
- **Category** (optional, D-226) — the "is main session / type" tag. It is a
  **dynamic lookup** (`SessionCategory`), **not** a fixed enum — the table ships
  empty pending the client's category list (OI-2). Null until a category is picked.
- **Speakers** (D-225) — an **ordered** roster. Each entry carries a
  `SessionSpeakerRole` (`Speaker = 0` / `Host = 1`) modelled on the JOIN (a person
  can host one session and speak in another). The CP form renumbers on
  add/move/remove (`DisplayOrder` 0-based); order 0 is the primary speaker.
- **Themes** — a multi-pick set; the first by order is the primary pillar the
  agenda groups under and the source of the agenda colour chip.

A bad M-to-M link is rejected 400 `SESSION_SPEAKER_NOT_FOUND` /
`SESSION_THEME_NOT_FOUND`.

## L-6 — Live URLs (§8 / D-349)

`LiveStreamUrl` and `LiveSignLanguageUrl` are optional (≤ 1024 chars each), each
validated by the **shared** `LiveStreamUrlPolicy.IsAllowed` on **both** the client
guard and the API (the API failure is 400 `SESSION_INVALID`). Per D-349 the live +
sign-language feeds use **YouTube** (POC) with a direct HLS/MP4 URL as a fallback;
no schema change — the URL lives on `Session.LiveStreamUrl`.

## L-7 — Recording (D-232)

The recording **bytes live out-of-row on disk**; only metadata
(`HasRecording`, `RecordingFileName`, `RecordingSizeBytes`, `RecordingUploadedAt`)
rides the detail. The upload allow-lists by file **extension** and stores a
canonical content-type so a recording can never be MIME-confused in the browser.
The app is only allowed to stream a recording once `Status == Published` **and** a
recording exists (the `PublicSessionDetail.HasRecording` flag; no stored file name
is exposed to the app).

## L-8 — Audit + persistence discipline

Writes go through `IAdminSessionService` on `SimfAppDbContext`. Create/Update/
Deactivate/SetStatus all take the actor id (from the `sub` JWT claim) so the
central `AuditStampingSaveChangesInterceptor` stamps `CreatedBy`/`UpdatedBy` and
`RowAudit` captures Insert/Update/soft-delete rows. Bilingual `Name`/`*Arabic`
columns; UTC timestamps; soft-delete via `IsActive`.

## L-9 — How the catalogue reaches the app (resolve-on-read)

The CP writes `Session` rows in `SIMF_App`; the app reads them anonymously via
`GET /api/v1/app/programme/sessions` (`PublicSessions`) — fetched **once** and
cached, then filtered client-side (App Page 016 L-1). The public projection
(`PublicSessionListItem` / `PublicSessionDetail`) is built from the **same**
`Session` rows:

| `Session` field (CP) | Public field (app) | App use |
|----------------------|--------------------|---------|
| `Start` / `End` | `start` / `end` | agenda time chip (rendered device-local) |
| `Code` | `code` | row code |
| `Title` / `TitleArabic` | `title` / `titleArabic` | row + detail title |
| `Description*` | `description*` | detail abstract |
| `HallId` + name EN/AR | `hallId` / `hallName*` | hall line |
| `CategoryId` + name EN/AR | `categoryId` / `categoryName*` | the "type" tag |
| `Status` | `status` (int) | optional Recorded/Published badge |
| `Speakers` (order + role) | `speakers[]` | speaker cards (+ D-271 country/photo on the public DTO) |
| live URLs / `HasRecording` | live screen / `hasRecording` | live player / recording (Published only) |

These app DTOs are **append-only** (D-219): the CP may add fields, but the shipped
mobile wire contract (public JSON field names the app decodes) is preserved.

## L-10 — Cross-database rule (D-157)

`Session` and all its relations are entirely within `SimfAppDbContext`
(`SIMF_App`). The actor ids stamped on audit are **bare Guids** (logical FKs into
`SIMF_Identity`) — there is no EF navigation, no cross-DB FK and no cross-DB JOIN
to the Identity database. The only Identity-owned copies are the immutable audit
snapshots (`RowAudit`), never live data.

## L-11 — Permission seeding

The seven `Sessions.*` codes are defined in `PermissionCatalog.Sessions`
(lines 206–218) and seeded via `PermissionCatalog.All` (baseline `AdminOnly`,
idempotent — no migration; the `Permission`/`RolePermission` tables pre-exist).
`Administrator = "*"` (wildcard) satisfies all. The page + every endpoint + every
action button are gated against these codes; `CpNavigationPermissionTests` and
`PermissionEnforcementTests` fail the build if a gate is missing.

## Related
- App contract + caching model: [App Page 016 Logic](../../App/Page_016/Page_016_Logic.md)
  / [API](../../App/Page_016/Page_016_API.md); home surface
  [App Page 013](../../App/Page_013/README.md).
- Existing reference: [`docs/pages/cp/admin-sessions.md`](../../pages/cp/admin-sessions.md) §5–§8.
- Decisions: D-165, D-219, D-225, D-226, D-231, D-232, D-247, D-271, D-349, D-353, D-356.
