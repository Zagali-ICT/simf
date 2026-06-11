# Speaker presentations — `/admin/speaker-presentations`

| | |
|--|--|
| **Route** | `/admin/speaker-presentations` |
| **Audience** | Administrator (any admin holding the Speakers permissions) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Speakers.View)]` on the page; API endpoints gated `Policies(PolicyFor(Speakers.View|Edit|Export), nameof(RequireApprovedAccount))`; mutating + export endpoints add `RequireRateLimiting("auth")` |
| **Pattern** | P2.3 / D-228 (FR-407, SIMF-FDS-004 §5.3) — **master-detail** file manager (pick a speaker → manage that speaker's presentation files). Not the canonical single-grid CRUD. |
| **Status** | ✅ Real (D-228; speaker-card picker D-357; Excel export D-356) |
| **Backend endpoints** (BFF `/account/api/admin/...` → API `/admin/...`) | `GET /account/api/admin/speakers/{speakerId}/presentations` (list for one speaker), `POST /account/api/admin/speakers/{speakerId}/presentations?sessionId={id}` (multipart upload), `GET /account/api/admin/speaker-presentations/{id}/file` (download), `DELETE /account/api/admin/speaker-presentations/{id}` (soft-delete), `POST /account/api/admin/speaker-presentations/export` (D-356 **export-only**) |
| **Source** | [`SpeakerPresentationsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SpeakerPresentationsList.razor), [`SpeakerPresentationEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SpeakerPresentationEndpoints.cs), [`SpeakerPresentationsExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SpeakerPresentationsExcelEndpoints.cs), [`AdminSpeakerPresentationService.cs`](../../../src/Backend/SIMF.Infrastructure/Programme/AdminSpeakerPresentationService.cs), [`IAdminSpeakerPresentationService.cs`](../../../src/Backend/SIMF.Application/Programme/Abstractions/IAdminSpeakerPresentationService.cs), [`SpeakerPresentations.cs`](../../../src/Shared/SIMF.Contracts/Admin/SpeakerPresentations.cs) |
| **Backed by** | `dbo.SpeakerPresentations` table (migration `D228_AddSpeakerPresentations`, 2026-06-02). Bytes stored **out-of-row** via `ISpeakerPresentationStorage`; the row holds only metadata. |
| **Tests** | [`docs/tests/e2e/cp-admin-speaker-presentations.md`](../../tests/e2e/cp-admin-speaker-presentations.md) |
| **Last reviewed** | 2026-06-11 |

## 1. Purpose

Admin management of the presentation files uploaded per speaker
(SIMF-FDS-004 §5.3, FR-407, D-228). Each file links **one speaker** to
**one session** and carries the original file name, MIME content type and
byte size; the bytes live out-of-row in the speaker-presentation storage
provider, while the table row is metadata only.

The page is **master-detail**, not a flat CRUD grid. The admin first
picks a speaker, then sees that speaker's files and can upload a new file
(against a chosen session), download an existing one, or remove one. There
is no "all presentations across all speakers" list — the list endpoint is
keyed by speaker id.

## 4. UI

- Banner (`Admin.SpeakerPresentations.Title`) + a single page surface.
- **Speaker picker (D-357):** when no speaker is selected the page renders
  a wall of profile cards — one `simf-speaker-card` per active speaker
  showing the speaker photo (`/account/api/admin/assets/SpeakerPhoto/{id}/image`),
  the name (Arabic name preferred under an `ar` UI culture) and the
  country (EN/AR per culture, "—" when unknown). This gallery replaced an
  earlier `<select>` picker. If there are no speakers an empty state
  (`Admin.SpeakerPresentations.NoSpeakers`) is shown.
- **Selected-speaker view:** a Back button (`Admin.SpeakerPresentations.BackToSpeakers`)
  plus the selected speaker's name, then the upload form and the file grid.
- **Upload form** (wrapped in `<AuthorizedAction Permission="Speakers.Edit">`):
  a session `<select>` (active sessions only, by title) and a native file
  `<input type="file">`, with an Upload button that is disabled until a
  session is chosen.
- **File grid** (`SimfDataGrid`, multiselect): columns **File** (`FileName`),
  **Session** (`SessionTitle`) and **Size** (humanised B / KB / MB).
  Sortable on File, Session and Size; File and Session are text-filterable.
  Default order is newest-first (`CreatedAt` descending). Because the list
  endpoint returns the speaker's whole set non-paged, the grid pages,
  filters and sorts the in-memory set client-side (`_allRows` →
  `BuildPage`) — there is no server `GridQuery` round-trip for this list.
- **Per-row quiet actions:** a **download** icon (gated `Speakers.View`)
  that opens the file endpoint (served `Content-Disposition: attachment`),
  and a **trash** icon (gated `Speakers.Edit`) that deletes after a native
  `confirm()` (`Admin.SpeakerPresentations.Delete.Confirm`).
- Empty grid renders `Admin.SpeakerPresentations.None`.

### D-356 — Excel **export only**

The grid toolbar carries an **Export** action (`Grid.Export`). No import is
offered: presentations are binary files uploaded one at a time, not flat
spreadsheet rows, so this resource is **export-only** (unlike the canonical
CRUD pages that ship both Export and Import).

- `OnExportAsync` posts an `AdminGridExportRequest { Ids, Query }` to
  `/account/api/admin/speaker-presentations/export`. Because the list is
  master-detail (keyed by speaker, with no `GridQuery` list endpoint), the
  page rides the selected speaker id in `Query.Filters["speakerId"]`; the
  export endpoint lists **that speaker's** files and then narrows to the
  selected row ids if any are ticked, else exports the whole (filtered)
  speaker set.
- The downloaded workbook uses sheet name **`SpeakerPresentations`** and
  file prefix **`simf-speaker-presentations-{yyyyMMddHHmmss}.xlsx`**.
- Header / columns (in order): `FileName | Session | SessionArabic |
  ContentType | SizeBytes | CreatedAt`.
- **Row cap:** the shared `AdminGridExportEndpoint<TRow>` base caps the
  whole-set export at **5,000 rows** (`MaxExportRows`) and resets `Skip`.
- **No speaker id ⇒ empty export** — the same as the grid before a speaker
  is picked (`ListAsync` returns `[]` when `Filters["speakerId"]` is
  missing or not a GUID).

### D-353 page ↔ popup presentation toggle — NOT present

This page does **not** carry the D-353 `CrudPresentationToggle`. Its
editing surface is an inline upload form plus per-row download/delete
icons — not a `CrudShell`-hosted Add/Edit/View/Delete dialog — so there is
no page-vs-popup preference and no `localStorage` preferences key for it.
(Verified: `SpeakerPresentationsList.razor` references no
`CrudPresentationToggle`.)

## 4.5 Upload fields

| Field | Required | Notes |
|-------|----------|-------|
| Speaker | yes | Chosen from the speaker-card picker; rides the route id on upload. |
| Session | yes | `<select>` of active sessions by title; Upload disabled until set; rides `?sessionId=` on upload. |
| File | yes | Native file input (id `presentation-file-input`); the bytes are the only payload posted by `simfAccount.uploadFile`. |

Server-side limits (`AdminSpeakerPresentationService.UploadAsync`): the
file must be non-empty and **≤ 50 MB** (`MaxFileBytes = 50 * 1024 * 1024`);
the file name is sanitised to its leaf name (default `presentation`,
truncated to 256 chars).

## 5. Data flow + endpoints

- **List** — `GET /admin/speakers/{speakerId}/presentations` →
  `ListForSpeakerAsync`: active rows for the speaker, newest-first,
  inner-joined to `Sessions` on `SimfAppDbContext` to resolve the bilingual
  session title (both Speaker and Session are real FKs in the **same** App
  context — no cross-DB hop).
- **Upload** — `POST /admin/speakers/{speakerId}/presentations?sessionId={id}`,
  multipart; the actor is read from the `sub` claim; validates the file,
  the speaker (active) and the session (active), stores the bytes via the
  storage provider, inserts the metadata row, and writes a
  `SpeakerPresentationUploaded` audit entry.
- **Download** — `GET /admin/speaker-presentations/{id}/file`: streams the
  stored bytes with `Content-Disposition: attachment` (original file name);
  404 if the row/file is missing.
- **Delete** — `DELETE /admin/speaker-presentations/{id}`: soft-deletes the
  row (`Deactivate()` → `IsActive = false`), removes the stored file, and
  writes a `SpeakerPresentationDeleted` audit entry; idempotent on an
  already-inactive row.
- **Export** — `POST /admin/speaker-presentations/export` (see §4 D-356).
- The Control Panel reaches every API route through the BFF proxy under
  `/account/api/admin/...` (`AccountEndpoints` forwards each call with the
  signed-in admin's access token; the export is wired by
  `MapGridExport(group, "speaker-presentations")`).

### Permission — which code gates what (verified)

There is **no dedicated `SpeakerPresentations` permission class** in
`PermissionCatalog`. The page reuses the existing **`Speakers.*`** surface:

| Action | Permission code | Where enforced |
|--------|-----------------|----------------|
| Page load + nav item | `Speakers.View` (`"Speakers.View"`) | `[RequirePermission]` on the page; `CpNavigation` `RequiredPermission` |
| List a speaker's files | `Speakers.View` | `ListSpeakerPresentationsEndpoint.Configure` |
| Download a file | `Speakers.View` | `DownloadSpeakerPresentationEndpoint.Configure` + the `<AuthorizedAction>` around the download icon |
| Upload a file | `Speakers.Edit` (`"Speakers.Edit"`) | `UploadSpeakerPresentationEndpoint.Configure` + the `<AuthorizedAction>` around the upload form |
| Delete a file | `Speakers.Edit` | `DeleteSpeakerPresentationEndpoint.Configure` + the `<AuthorizedAction>` around the trash icon |
| **Excel export** | **`Speakers.Export`** (`"Speakers.Export"`) | `ExportSpeakerPresentationsEndpoint.Permission` (the export reuses the Speakers Export permission — there is no `SpeakerPresentations.Export`) |

## 6. Validation + error handling

- **Empty / oversized file** — 400 `SPEAKER_PRESENTATION_INVALID`
  (`ErrorCodes.SpeakerPresentationInvalid`), bilingual message.
- **Speaker not found / inactive** — 404 `SPEAKER_NOT_FOUND`.
- **Session not found / inactive** — 404 `SESSION_NOT_FOUND`.
- **Presentation not found on delete** — 404
  `SPEAKER_PRESENTATION_NOT_FOUND` (`ErrorCodes.SpeakerPresentationNotFound`).
- **Unauthenticated actor on upload/delete** — 401 (no `sub` claim).
- The CP surfaces the API error via `MessageForCurrentCulture()` in a
  `SimfAlert` toast; a failed load falls back to
  `Admin.SpeakerPresentations.LoadFailed`.

## 7. Edge cases + known limitations

- **Master-detail only** — there is no cross-speaker list; everything is
  scoped to the picked speaker. Export with no speaker selected yields an
  empty workbook (by design).
- **Client-side paging/sort/filter** — the list endpoint returns the full
  non-paged set for one speaker; the grid windows it in memory. For a
  speaker with a very large number of files this loads them all at once.
- **Soft delete** — files are deactivated (and the stored bytes removed),
  not hard-deleted; delete is idempotent.
- **File-name sanitisation** — only the leaf name is kept, truncated to
  256 chars; an empty name becomes `presentation`.
- **Native `confirm()`** — delete uses the browser confirm dialog, not a
  `SimfConfirm` component.

## 8. i18n + RTL

`Admin.SpeakerPresentations.*` resx keys (Title, NoSpeakers,
BackToSpeakers, Session, SelectSession, File, Upload, Uploaded, Download,
Delete, Delete.Confirm, Deleted, None, Loading, LoadFailed, Col.File,
Col.Session, Col.Size) plus shared `Grid.*` keys. EN ↔ AR parity is
expected (descriptive — resx contents not re-verified line-by-line in this
review). The speaker name and country prefer the Arabic value under an
`ar` UI culture; the grid and cards render right-to-left under RTL.

## 10. Use cases

- Upload a presentation file for a speaker's session.
- Download a previously uploaded presentation.
- Remove an obsolete presentation.
- Export the selected speaker's presentation list to Excel (D-356).

## 11. E2E

See [`docs/tests/e2e/cp-admin-speaker-presentations.md`](../../tests/e2e/cp-admin-speaker-presentations.md):
speaker-picker render, select speaker → file list, upload golden path,
session-required upload guard, oversized/empty-file rejection, download,
delete + confirm, **Excel export (selected rows + whole speaker set)**,
empty state, auth-gate (Speakers.View / Edit / Export), and RTL.

## 12. Related docs

- Authority spec: SIMF-FDS-004 §5.3 (FR-407).
- Decisions: D-228 (entity + endpoints + migration), D-357 (speaker-card
  picker), D-356 (grid Excel export-only).
- Permissions: `PermissionCatalog.Speakers` (`docs/SIMF-Permission-Catalogue.md`);
  the page deliberately reuses `Speakers.*` rather than a dedicated class.
- Sibling Programme modules: Speakers, Sessions, Themes.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-02 | D-228 | Original — `SpeakerPresentation` entity + migration `D228_AddSpeakerPresentations`, list/upload/download/delete endpoints, out-of-row storage, master-detail CP page gated by `Speakers.View`/`Speakers.Edit`. |
| 2026-06-08 | D-357 | Speaker picker turned into a wall of profile cards (photo + name + country) replacing the `<select>`. |
| 2026-06-10 | D-356 | Excel **export only** added (toolbar Export → `.xlsx`, sheet `SpeakerPresentations`, columns `FileName | Session | SessionArabic | ContentType | SizeBytes | CreatedAt`, 5,000-row cap, speaker id carried in `Query.Filters["speakerId"]`); export gated by the reused `Speakers.Export` permission. No import; no D-353 page↔popup toggle on this page. |

_Last reviewed:_ 2026-06-11 by Claude (D-356 reference-doc authoring — Excel export-only; permission = reused Speakers.Export; no D-353 toggle).
