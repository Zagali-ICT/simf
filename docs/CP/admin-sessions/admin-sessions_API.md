# Programme sessions — API (`/admin/sessions`)

Authoritative backend contract for this CP config page. Inherits the
`ApiResult<T>` envelope, standard headers, error model and auth from SIMF-API-001.
Verified against code this session.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> Companion docs: [Design](admin-sessions_Design.md) ·
> [Function](admin-sessions_Function.md) · [Logic](admin-sessions_Logic.md).

## Call path

```
SessionsList / SessionsAddEdit / SessionsViewDelete (Blazor)
  → JS interop (simfAccount.postJson / getJson / putJson / deleteJson / uploadFile)
  → CP BFF  /account/api/admin/sessions/...
  → API     /api/v1/admin/sessions/...  →  IAdminSessionService  →  SimfAppDbContext
  → ApiResult<T> envelope  →  UI update + bilingual toast
```

Every API endpoint is gated
`Policies(PermissionCatalog.PolicyFor(<code>), nameof(AuthorizationPolicies.RequireApprovedAccount))`.
Mutations also `Options(rb => rb.RequireRateLimiting("auth"))`. App routes are
under `/api/v1/app/*`; admin routes under `/api/v1/admin/*` (App↔CP split, D-247).

## Admin endpoints (the CP page calls these)

| # | Verb + route (API) | Permission | Request | Response | Source |
|---|--------------------|------------|---------|----------|--------|
| A1 | `POST /admin/sessions/list` | `Sessions.View` | `GridQuery` | `ApiResult<GridPage<AdminSessionSummary>>` | `ListSessionsEndpoint` |
| A2 | `GET /admin/sessions/{id:guid}` | `Sessions.View` | route id | `ApiResult<AdminSessionDetail>` (404 `SessionNotFound` when missing) | `GetSessionEndpoint` |
| A3 | `POST /admin/sessions` | `Sessions.Create` | `AdminCreateSessionRequest` | `ApiResult<AdminSessionDetail>` | `CreateSessionEndpoint` |
| A4 | `PUT /admin/sessions/{id:guid}` | `Sessions.Edit` | `UpdateSessionRequest` → `AdminUpdateSessionRequest` | `ApiResult<AdminSessionDetail>` | `UpdateSessionEndpoint` |
| A5 | `DELETE /admin/sessions/{id:guid}` | `Sessions.Delete` | route id | `ApiResult<bool>` (soft-delete) | `DeactivateSessionEndpoint` |
| A6 | `PUT /admin/sessions/{id:guid}/status` | `Sessions.Publish` | `SetSessionStatusRequest` | `ApiResult<AdminSessionDetail>` | `SetSessionStatusEndpoint` |
| A7 | `POST /admin/sessions/{id:guid}/recording` | `Sessions.Publish` | multipart `file` (video) | `ApiResult<AdminSessionDetail>` | `UploadSessionRecordingEndpoint` |
| A8 | `DELETE /admin/sessions/{id:guid}/recording` | `Sessions.Publish` | route id | `ApiResult<AdminSessionDetail>` | `DeleteSessionRecordingEndpoint` |
| A9 | `POST /admin/sessions/export` | `Sessions.Export` | `AdminGridExportRequest { Ids, Query }` | `.xlsx` download | `ExportSessionsEndpoint` |
| A10 | `POST /admin/sessions/import` | `Sessions.Import` | multipart `file` (`.xlsx`) | `ApiResult<AdminGridImportResult>` | `ImportSessionsEndpoint` |

The BFF passthrough mirrors each as `/account/api/admin/sessions/...`. A3/A4/A5/A6
read the actor id from the `sub` JWT claim (401 when missing).

### Relational pickers (the Add/Edit form also calls)
Each `POST` with `GridQuery { Top = 500, Filters: { isActive = "true" } }`:
`POST /admin/halls/list` (`Halls.View`), `POST /admin/speakers/list`
(`Speakers.View`), `POST /admin/themes/list` (`Themes.View`),
`POST /admin/session-categories/list` (`SessionCategories.View`).

## Request / response DTOs (`SIMF.Contracts.Admin/Sessions.cs`)

### `AdminSessionSummary` (grid row, A1)
```jsonc
{
  "id": "guid", "code": "string", "title": "string", "titleArabic": "string",
  "hallId": "guid", "hallName": "string", "hallNameArabic": "string",
  "startUtc": "DateTimeOffset", "endUtc": "DateTimeOffset",
  "capacity": 0,                 // effective capacity (override ?? hall)
  "isActive": true,
  "createdAt": "DateTimeOffset",
  "categoryId": null,            // Guid? (D-226)
  "status": 0                    // SessionStatus int — 0 Scheduled / 1 Held / 2 Recorded / 3 Published (D-231)
}
```

### `AdminSessionDetail` (A2/A3/A4/A6/A7/A8)
```jsonc
{
  "id": "guid", "code": "string", "title": "string", "titleArabic": "string",
  "description": null, "descriptionArabic": null,         // string?
  "hallId": "guid", "hallName": "string", "hallNameArabic": "string",
  "hallCapacity": 0,
  "startUtc": "DateTimeOffset", "endUtc": "DateTimeOffset",
  "capacityOverride": null,      // int? — null = inherit hall
  "effectiveCapacity": 0,        // CapacityOverride ?? HallCapacity
  "isActive": true,
  "speakers": [                  // ordered roster
    { "speakerId": "guid", "name": "string", "nameArabic": "string",
      "displayOrder": 0, "role": 0 }   // SessionSpeakerRole int — 0 Speaker / 1 Host (D-225)
  ],
  "themeIds": ["guid"],
  "createdAt": "DateTimeOffset", "updatedAt": null,
  "categoryId": null,            // Guid? (D-226)
  "status": 0,                   // SessionStatus int (D-231)
  "publishedAt": null,           // DateTimeOffset? — set on Publish
  "hasRecording": false, "recordingFileName": null,
  "recordingSizeBytes": null, "recordingUploadedAt": null,   // (D-232)
  "liveStreamUrl": null, "liveSignLanguageUrl": null         // string? (§8 / D-349)
}
```

### `AdminCreateSessionRequest` (A3) / `AdminUpdateSessionRequest` (A4)
Create body: `Code`, `Title`, `TitleArabic`, `Description?`, `DescriptionArabic?`,
`HallId`, `StartUtc`, `EndUtc`, `CapacityOverride?`, `CategoryId?`,
`Speakers` (`AdminSessionSpeakerEntry[]`), `ThemeIds` (`Guid[]`),
`LiveStreamUrl?`, `LiveSignLanguageUrl?`. Update adds `IsActive`. (The API's
`UpdateSessionEndpoint` binds a wire `UpdateSessionRequest` carrying `Id` from the
route, then maps to `AdminUpdateSessionRequest`.)

`AdminSessionSpeakerEntry` = `{ speakerId, name, nameArabic, displayOrder, role }`.

> **#3 / #4 rules.** `Type` (`SessionType?`, Workshop/Session/Event) is **required**
> on create and update, and `Speakers` must hold **≥1 entry unless `Type == Event`**.
> Both are enforced in `AdminSessionService.Create/UpdateAsync` with a no-regression
> grandfather on update (a pre-existing violating row stays saveable; a compliant one
> cannot regress). Violations return `SESSION_TYPE_REQUIRED` / `SESSION_SPEAKER_REQUIRED`.

### `SetSessionStatusRequest` (A6)
`{ "status": 1 }` — a `SessionStatus` int. The service enforces adjacent moves
(`Scheduled ↔ Held ↔ Recorded ↔ Published`); an illegal jump is a 400
`SESSION_STATUS_TRANSITION_INVALID`; setting the same status is an idempotent no-op.

> **Enum wire format = int.** `status` (`SessionStatus`) and the speaker `role`
> (`SessionSpeakerRole`) serialise as **integers** (there is no
> `JsonStringEnumConverter` in `SIMF.Api`). The CP deserialises into the strong
> enum types directly; the app decodes tolerantly (int or name).

## Error codes (`ErrorCodes`, surfaced bilingually via `MessageForCurrentCulture()`)

| HTTP | `ApiResult.Error.Code` | When |
|------|------------------------|------|
| 404 | `SESSION_NOT_FOUND` | unknown / soft-deleted id (A2 throws `ApiException` "The session was not found." / "لم يتم العثور على الجلسة.") |
| 409 | `SESSION_CODE_DUPLICATE` | duplicate `Code` on create/update |
| 400 | `SESSION_INVALID_TIME_WINDOW` | `End ≤ Start` |
| 400 | `SESSION_HALL_NOT_FOUND` | inactive/unknown hall |
| 400 | `SESSION_SPEAKER_NOT_FOUND` / `SESSION_THEME_NOT_FOUND` | bad M-to-M link |
| 400 | `SESSION_TYPE_REQUIRED` | no `Type` on create; or clearing a set type on update (#3, grandfathered) |
| 400 | `SESSION_SPEAKER_REQUIRED` | a non-Event session with no speaker on create; or dropping the last speaker of a compliant non-Event on update (#4, grandfathered) |
| 400 | `SESSION_STATUS_TRANSITION_INVALID` | illegal lifecycle move (A6) |
| 400 | `SESSION_RECORDING_INVALID` | non-video / empty / oversize upload (A7) |
| 400 | `SESSION_INVALID` | a live URL fails the shared `LiveStreamUrlPolicy` |

### Recording upload defence (A7, `UploadSessionRecordingEndpoint`)
Reads the multipart form manually so the per-request body + multipart ceilings are
raised to `SessionRecordingStorageOptions.MaxUploadBytes` **before** the body is
read (scoped to this request only). The content-type is resolved from the file
**extension** against an allow-list and stored canonically (never browser-supplied):
`.mp4`/`.m4v` → `video/mp4`, `.webm` → `video/webm`, `.ogg`/`.ogv` → `video/ogg`,
`.mov` → `video/quicktime`. Empty / oversize / non-allowlisted extension →
400 `SESSION_RECORDING_INVALID`.

### Excel (A9 export / A10 import, `SessionsExcelEndpoints`)
- **Export** sheet `"Sessions"`, file prefix `simf-sessions`, columns:
  `Code | Title | TitleArabic | Hall | Category | StartUtc | EndUtc | Capacity | Status | IsActive`.
  Hall → its **code**, Category → its **English name**, Start/End → ISO-8601 UTC
  (`yyyy-MM-ddTHH:mm:ss'Z'`), Status → enum **name**. Speaker roster + theme set
  are **omitted** (M-to-M).
- **Import** is **insert-only**; row key = `Code`; required headers
  `Code, Title, TitleArabic, Hall, StartUtc, EndUtc`. An optional **`Speakers`**
  column holds comma-separated speaker **codes** (resolved case-insensitive,
  active-only; position sets the display order, role defaults to Speaker) so an
  imported non-Event row can meet the #4 min-1-speaker rule. Hall resolves from its
  code (case-insensitive, active-only); Category from its English name (blank =
  unset). Per-row `DataValidationException` — a blank `Type` (#3), a non-Event row
  with no speakers or an unknown/duplicate speaker code (#4), plus bad
  code/title/time-window/capacity, unresolved Hall or unknown Category — is
  collected; one bad row never aborts the batch. The export still omits the roster.
  Upload defence (in `AdminGridImportEndpoint`): ZIP-magic gate → 400 "not a valid
  Excel workbook"; > 5 MB → 413; cap 5000 rows.

## App reads (the same `Session` data, consumed by App Page 016 / 013)

| Verb + route (API) | Access | Response | Source |
|--------------------|--------|----------|--------|
| `GET /api/v1/app/programme/sessions` (`?day=yyyy-MM-dd`) | `AllowAnonymous` | `ApiResult<PublicSessions>` — the whole active programme, time-ordered | App Page 016 E1 |
| `GET /api/v1/app/programme/sessions/{id:guid}` | `AllowAnonymous` | `ApiResult<PublicSessionDetail>` — title + abstract, hall, time, themes, ordered speakers, category, seat summary, `hasRecording` | App Page 016 E2 |

`PublicSessions = { items: PublicSessionListItem[] }`. Each
`PublicSessionListItem` carries: `id`, `code`, `title`, `titleArabic`, `hallId`,
`hallName`, `hallNameArabic`, `startUtc`, `endUtc`, `primaryThemeName(+Arabic)`,
`primaryThemeColor`, `categoryId/categoryName/categoryNameArabic` (the "is main
session / type" tag, D-226), `status` (int), `description(+Arabic)` and the ordered
`speakers[]` (`PublicSessionSpeaker` — incl. D-271 `countryId`, `countryNameEn/Ar`,
`photoRelativePath`). The app fetches the list **once** and caches it. These app
reads are **append-only** (D-219) — the CP writes the rows, the app reads them.

> **Field mapping CP → app.** `Session.StartUtc/EndUtc` → agenda time chip;
> `Code`/`Title`/`TitleArabic`/`Description*` → row + detail; `Hall*` → hall line;
> `Category*` → the "type" tag; `Status` → optional Recorded/Published badge;
> `Speakers` (order + role) → the speaker cards. Live URLs feed the app's live
> screen; the recording (`HasRecording`) is only surfaced to the app once
> `Status == Published`.

## Cross-references
- App contract: [App Page 016 API](../../App/Page_016/Page_016_API.md) (E1/E2) ·
  [App Page 013 API](../../App/Page_013/Page_013_API.md).
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) (admin Sessions group + `ApiResult<T>`).
- Existing reference: [`docs/pages/cp/admin-sessions.md`](../../pages/cp/admin-sessions.md) §5.
- Tests: `tests/SIMF.Api.Tests/AdminSessionsTests.cs`, `SessionLifecycleTests.cs`,
  `SessionRecordingTests.cs`, `SessionsExcelTests.cs`, `PermissionEnforcementTests.cs`.
