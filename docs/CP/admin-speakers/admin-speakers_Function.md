# Speakers — Function (`/admin/speakers` · المتحدّثون)

What the administrator does on this page. Behaviour traced to
`SpeakersList.razor`, `SpeakersAddEdit.razor`, `SpeakersViewDelete.razor` and
`AdminSpeakerService`.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

## Who can use it

A signed-in administrator whose role holds `Speakers.View` (or the
`Administrator = "*"` wildcard). Without it the page hits `/not-permitted` and
the **Speakers** item is absent from the nav rail. Each mutating action needs its
own code (`Speakers.Create` / `Edit` / `Delete` / `Export` / `Import`).

## Functions

### F1 — Browse the speaker list

On load, `OnInitializedAsync` reads the saved presentation preference, then
`LoadAsync` posts the current `GridQuery` (default `Top = 20`) to
`/account/api/admin/speakers/list` and renders the grid. Columns: Code, Name,
Name (Arabic), Rank, Country, Display order, Active (Active/Inactive pill).
Empty → `SimfEmptyState`.

### F2 — Filter by Name

Typing in the Name column filter re-issues `/list` with the search term. The
server matches the term (case-insensitive `LIKE`) against **Code, Name OR
NameArabic**. Clearing reloads the full list.

### F3 — Sort

Clicking a sortable header (**Code**, **Name**, **Display order**) re-issues
`/list` with `Sort` + `SortDescending`. The default order is **DisplayOrder then
Name**. Name (Arabic), Rank, Country and Active are **not** sortable.

### F4 — Page

First / Prev / Next / Last walk pages (`Skip` steps by the page size); the
page-size selector changes `Top`. `FormatSummary` / `FormatPage` render the
"showing X–Y of Z" + "page A of B" lines.

### F5 — Add a speaker (`Speakers.Create`)

**Add speaker** opens the `CrudShell`-framed `SpeakersAddEdit` (Add mode — no
Active checkbox, no photo control). The admin fills Code, Name, Name (Arabic),
optionally Rank, Country (picker), the bilingual Bio/Qualifications/Training/Awards,
the consent checkboxes, social URLs, an optional Contact link, and Display order,
then **Create speaker**. The client guards the bounds (see
[admin-speakers_Logic.md](admin-speakers_Logic.md)) before
`POST /account/api/admin/speakers` (Code upper-cased + trimmed). On success the
form closes, a green `Admin.Speakers.Created` toast shows the name, and the grid
reloads.

### F6 — Edit a speaker (`Speakers.Edit`)

The row **Edit** icon first `GET`s the full `AdminSpeakerDetail` (the grid
summary omits the rich-text + social URLs), then opens `SpeakersAddEdit` (Edit
mode) pre-filled. Edit mode also shows the **Active** checkbox and the **Image**
upload (`SimfImageUpload Category="SpeakerPhoto"`, D-357 — only once the row
exists). **Save changes** → `PUT /account/api/admin/speakers/{id}`; green
`Admin.Speakers.Updated` toast; grid reloads. `UserProfileId` is preserved
(not editable in this form).

### F7 — View details (`Speakers.View`)

The **Details** icon `GET`s the detail and opens `SpeakersViewDelete` (read-only
`dl`) showing every field (blanks → **—**) plus the photo thumbnail. **Close**
dismisses it; no network call.

### F8 — Deactivate (soft-delete) (`Speakers.Delete`)

The **Deactivate** icon `GET`s the detail and opens `SpeakersViewDelete` in
delete mode. The red **Deactivate** button raises a **`SimfConfirm`** Danger
dialog naming the speaker; confirming fires
`DELETE /account/api/admin/speakers/{id}` (sets `IsActive = false`). Green
`Admin.Speakers.Deactivated` toast; the row stays visible with the grey
**Inactive** pill. Re-deactivating an already-inactive speaker still returns 200
(idempotent — no second audit row). Cancelling fires nothing. Reactivate later
via F6 (re-tick Active).

### F9 — Toggle the form presentation (D-353)

The toolbar `CrudPresentationToggle` switches Add/Edit/Details/Deactivate
between **dialog** and **full page**; the choice persists in `localStorage`
under `simf.cp.prefs.speakers`. In full-page mode the grid + banner hide while
the form takes over.

### F10 — Export to Excel (`Speakers.Export`)

**Export** calls `_excel.ExportAsync(selectedIds, query)`. With no rows selected
it exports the **whole filtered grid** (`Ids: []`, current `Query`); with rows
selected it exports **just those** (their `Ids`, null `Query`). The browser
saves `simf-speakers-{yyyyMMddHHmmss}.xlsx`; the "Speakers" sheet header is
`Code | Name | NameArabic | Rank | Country | DisplayOrder | IsActive`. Capped at
5000 rows.

### F11 — Import from Excel (`Speakers.Import`)

**Import** triggers the hidden `speakers-import-input` (`accept=".xlsx"`).
Choosing a workbook whose "Speakers" sheet has the required headers
(`Code | Name | NameArabic`; Rank + DisplayOrder optional) posts it, then shows
an import-result modal ("N created, N updated, N skipped" + per-row errors)
followed by the green `Grid.Import.Done` toast and a grid reload. Import is
**insert-only**; Country, rich-text, social URLs and consent flags are not
imported (set later via Edit). A duplicate Code is a per-row error; a non-`.xlsx`
or over-5 MB file is rejected before any row is applied (red error toast).

### F12 — Upload / link a photo (D-357, `Speakers.Edit`)

In Edit mode the **Image** control (`SimfImageUpload Category="SpeakerPhoto"`)
uploads a file or sets an external link through the unified media-asset pipeline.
The Details/Deactivate form previews it via
`/account/api/admin/assets/SpeakerPhoto/{id}/image`. The same `SpeakerPhoto`
asset drives the public website speaker card (`HasPhotoAsset` →
`/content/assets/SpeakerPhoto/{id}/image`) and is preferred over the legacy
`PhotoRelativePath`.

## What the app / website do with this data (downstream)

- **App — Sessions / session detail ([Page_016](../../App/Page_016/README.md)):**
  each session embeds the ordered speaker cards — name, rank/title, country flag
  + photo — and the **session role** (`SessionSpeakerRole` 0=Speaker / 1=Host,
  D-225). The app shows the host marker from `role`.
- **App / website — Speakers list + Speaker profile:** `GET /app/speakers`
  renders the avatar + rank line + bilingual name ordered by `DisplayOrder`;
  `GET /app/speakers/{id}` renders the four bilingual rich-text tabs, the
  nationality, the **opted-in** social URLs (only when `AllowsDataSharing`), the
  **Request meeting** affordance (only when `AllowsMeetingRequests`), and the
  speaker's sessions.

## Cross-links

- Design: [admin-speakers_Design.md](admin-speakers_Design.md)
- API: [admin-speakers_API.md](admin-speakers_API.md)
- Logic: [admin-speakers_Logic.md](admin-speakers_Logic.md)
- E2E: [`docs/tests/e2e/cp-admin-speakers.md`](../../tests/e2e/cp-admin-speakers.md) (E2E-SPK-001…023)
- Page index: [`docs/pages/cp/admin-speakers.md`](../../pages/cp/admin-speakers.md)
