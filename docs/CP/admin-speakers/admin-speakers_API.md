# Speakers — API (`/admin/speakers` · المتحدّثون)

Authoritative backend contract for this page. Every route inherits the
`ApiResult<T>` envelope, standard headers, error model and auth from
SIMF-API-001. Admin routes are under **`/api/v1/admin/*`**; the Control Panel
calls them through its BFF proxy at **`/account/api/admin/*`**
(`simfAccount.postJson/getJson/putJson/deleteJson`). The public app/website
reads are under **`/api/v1/app/*`** (App↔CP split, D-247).

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> **Status:** **BUILT** — admin CRUD (D-199 / D-153), Excel export/import
> (D-356), public reads (D-199), photo via the unified media-asset pipeline
> (D-357). Covered by `tests/SIMF.Api.Tests/AdminSpeakersTests.cs`,
> `SpeakersExcelTests.cs`, `PublicSpeakersTests.cs`.

## Admin CRUD (gated, `RequireApprovedAccount`)

Source: [`SpeakerEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SpeakerEndpoints.cs).
All admin routes carry `Policies(PermissionCatalog.PolicyFor(<code>), nameof(AuthorizationPolicies.RequireApprovedAccount))`
and `Tags("Admin")`.

| # | CP (BFF) route | API route + verb | Policy | Rate limit | Request → Response |
|---|----------------|------------------|--------|-----------|--------------------|
| A1 | `POST /account/api/admin/speakers/list` | `POST /admin/speakers/list` | `Speakers.View` | — | `GridQuery` → `ApiResult<GridPage<AdminSpeakerSummary>>` |
| A2 | `GET /account/api/admin/speakers/{id}` | `GET /admin/speakers/{id:guid}` | `Speakers.View` | — | route id → `ApiResult<AdminSpeakerDetail>` (404 `SPEAKER_NOT_FOUND`) |
| A3 | `POST /account/api/admin/speakers` | `POST /admin/speakers` | `Speakers.Create` | `auth` | `AdminCreateSpeakerRequest` → `ApiResult<AdminSpeakerDetail>` |
| A4 | `PUT /account/api/admin/speakers/{id}` | `PUT /admin/speakers/{id:guid}` | `Speakers.Edit` | `auth` | `UpdateSpeakerRequest` → `ApiResult<AdminSpeakerDetail>` |
| A5 | `DELETE /account/api/admin/speakers/{id}` | `DELETE /admin/speakers/{id:guid}` | `Speakers.Delete` | `auth` | route id → `ApiResult<bool>` (soft-delete, idempotent) |
| A6 | `POST /account/api/admin/speakers/export` | `POST /admin/speakers/export` | `Speakers.Export` | — | `AdminGridExportRequest { Ids, Query }` → `.xlsx` |
| A7 | `POST /account/api/admin/speakers/import` | `POST /admin/speakers/import` | `Speakers.Import` | — | multipart `.xlsx` → import-result |

- **A3 / A4 / A5** resolve the actor from the `sub` claim; an unparseable `sub`
  → `401`.
- **A4** is the only route whose request type differs from the contract: the
  endpoint's `UpdateSpeakerRequest` (with the route `Id`) is mapped field-for-field
  into `AdminUpdateSpeakerRequest` before the service call.

## Public reads (anonymous — app + website)

Source: [`PublicSpeakerEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Public/PublicSpeakerEndpoints.cs).
Both `AllowAnonymous()`, `Tags("Public")`.

| # | API route | Returns |
|---|-----------|---------|
| P1 | `GET /api/v1/app/speakers` | `ApiResult<PublicSpeakers>` — active speakers ordered by `DisplayOrder` (Mockup page 19 "Speakers") |
| P2 | `GET /api/v1/app/speakers/{id:guid}` | `ApiResult<PublicSpeakerDetail>` — one active speaker + their sessions (Mockup page 20 "Speaker profile"); 404 `SPEAKER_NOT_FOUND` |

The app also consumes the speaker **cards embedded in the programme** —
`GET /api/v1/app/programme/sessions` + `…/sessions/{id}` carry the ordered
`PublicSessionSpeaker[]` per session (name, rank/title, country flag id + names,
photo, and the **`role`** = `SessionSpeakerRole` 0=Speaker / 1=Host, D-225). See
[`docs/App/Page_016/Page_016_API.md`](../../App/Page_016/Page_016_API.md).

## DTOs

### `AdminSpeakerSummary` (grid row — `Speakers.cs`)

```jsonc
{
  "id":            "guid",
  "code":          "string",
  "name":          "string",
  "nameArabic":    "string",
  "rank":          "string?",
  "countryId":     null,        // int? (ISO 3166-1 numeric, FK Country.Id)
  "countryNameEn": null,        // string? projected for display
  "countryNameAr": null,        // string?
  "displayOrder":  0,
  "isActive":      true,
  "createdAt":     "datetimeoffset"
}
```

### `AdminSpeakerDetail` (Get / Edit / Details / Deactivate — `Speakers.cs`)

Adds to the summary: `userProfileId` (`Guid?`, cross-context logical FK to
`SimfIdentityDbContext` — never surfaced publicly), the four bilingual rich-text
pairs (`bio`/`bioArabic`, `qualifications`/`qualificationsArabic`,
`trainingExperience`/`trainingExperienceArabic`, `awards`/`awardsArabic`), the
consent flags (`allowsMeetingRequests`, `allowsDataSharing`), the social URLs
(`facebookUrl`, `linkedInUrl`, `xUrl`), `photoRelativePath` (legacy path),
`displayOrder`, `isActive`, `createdAt`, `updatedAt` (`DateTimeOffset?`), and
`contactId` (`Guid?` shared-Contact link).

### `AdminCreateSpeakerRequest` / `AdminUpdateSpeakerRequest` (`Speakers.cs`)

Create carries every editable field (Code, Name, NameArabic, Rank, CountryId,
UserProfileId, the four bilingual rich-text pairs, AllowsMeetingRequests,
AllowsDataSharing, Facebook/LinkedIn/X URL, DisplayOrder, ContactId). Update is
the same plus **`IsActive`** (default `true`). Consent flags default `false` —
the admin opts in per speaker.

### `PublicSpeakerSummary` (P1 — `PublicSpeakers.cs`)

```jsonc
{
  "id":               "guid",
  "name":             "string",
  "nameArabic":       "string",
  "rank":             "string?",
  "countryId":        null,     // int? → client renders the flag
  "countryNameEn":    null,     // string?
  "countryNameAr":    null,     // string?
  "photoRelativePath":null,     // string? legacy avatar path
  "displayOrder":     0,
  "hasPhotoAsset":    false      // D-357 append-only; true → prefer /content|/app/assets/SpeakerPhoto/{id}/image
}
```

### `PublicSpeakerDetail` (P2 — `PublicSpeakers.cs`)

The full public projection: bilingual name + rank, country (id + EN/AR name),
the four bilingual rich-text sections, **`allowsMeetingRequests`** (drives the
client's "Request meeting" affordance), **`allowsDataSharing`** (the social URLs
are returned **only** when true), `facebookUrl` / `linkedInUrl` / `xUrl`,
`photoRelativePath`, `displayOrder`, and `sessions[]` (`PublicSpeakerSession` —
id, code, bilingual title, hall id + bilingual name, `startUtc` / `endUtc`,
ordered by start). **`userProfileId` is deliberately NOT on the public
projection.**

## Excel (D-356)

Source: [`SpeakersExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SpeakersExcelEndpoints.cs).

- **Export (A6)** — `ExportSpeakersEndpoint : AdminGridExportEndpoint<AdminSpeakerSummary>`;
  sheet **"Speakers"**, file prefix `simf-speakers`. Columns:
  `Code | Name | NameArabic | Rank | Country | DisplayOrder | IsActive` (Country
  = the EN display name). Lists via the same `IAdminSpeakerService.ListAllAsync`,
  so it honours the current filter; whole-grid set capped at 5000 rows.
- **Import (A7)** — `ImportSpeakersEndpoint : AdminGridImportEndpoint`,
  **insert-only**; sheet **"Speakers"**, required headers `Code | Name | NameArabic`
  (Rank + DisplayOrder optional). Country, bilingual rich-text, social URLs and
  consent flags are **deliberately not imported**. Each row binds to
  `AdminCreateSpeakerRequest` (Code upper-cased); a per-row blank/out-of-range
  Code or blank EN/AR name throws a bilingual `DataValidationException`, and a
  duplicate Code raises `SPEAKER_CODE_DUPLICATE` as a **per-row** error — one bad
  row never aborts the batch. A non-`.xlsx` (ZIP-magic) → 400; an over-5 MB
  upload → 413.

## Error responses

| HTTP | `ApiResult.Error.Code` | When |
|------|------------------------|------|
| 400 | `SPEAKER_INVALID` | Code not 2–16; Name/NameArabic not 1–128; DisplayOrder < 0; a social URL > 256; CountryId missing/inactive; ContactId missing/inactive |
| 401 | — | `sub` claim missing/unparseable on create/update/deactivate |
| 404 | `SPEAKER_NOT_FOUND` | get / update / deactivate of a missing speaker; public detail of a missing/soft-deleted speaker |
| 409 | `SPEAKER_CODE_DUPLICATE` | create (always) / update (only when the code changes) collides on the upper-cased Code |
| 413 | — | import `.xlsx` over 5 MB |

> **Reserved:** `SPEAKER_IN_USE` exists in `ErrorCodes` for a future in-use guard;
> the current Deactivate is unconditional (and idempotent).

## Permission gating (CLAUDE.md hard rule)

- Page: `@attribute [RequirePermission(PermissionCatalog.Speakers.View)]`.
- Nav: `CpNavigation` `Module.Speakers` → `/admin/speakers`,
  `RequiredPermission = Speakers.View`, icon `mic`.
- Codes (`PermissionCatalog.Speakers`, all `AdminOnly`): `Speakers.View`,
  `Speakers.Create`, `Speakers.Edit`, `Speakers.Delete`, `Speakers.Export`,
  `Speakers.Import`. `PermissionEnforcementTests` + `CpNavigationPermissionTests`
  fail the build if a gate is missing.

## Audit events (`AuditEvents`, written by `AdminSpeakerService`)

`Speaker.Created`, `Speaker.Updated`, `Speaker.Deactivated` — each
`AuditOutcome.Success`, with the actor user id in `ActorUserId` and a `Detail`
string (`id=…; code=…; name=…` etc.).

## Cross-links

- Logic: [admin-speakers_Logic.md](admin-speakers_Logic.md)
- Page index: [`docs/pages/cp/admin-speakers.md`](../../pages/cp/admin-speakers.md)
- E2E: [`docs/tests/e2e/cp-admin-speakers.md`](../../tests/e2e/cp-admin-speakers.md)
- App consumer: [`docs/App/Page_016/Page_016_API.md`](../../App/Page_016/Page_016_API.md)
