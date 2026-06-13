# Programme sessions — Function (`/admin/sessions`)

What the operator does on the page, the golden path, validation, permission
gating and the bilingual toast copy. Verified against code this session.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> Companion docs: [Design](admin-sessions_Design.md) ·
> [API](admin-sessions_API.md) · [Logic](admin-sessions_Logic.md).

## Audience + permission gates

- **Reach the page:** any signed-in admin whose role grants `Sessions.View`
  (`@attribute [RequirePermission(PermissionCatalog.Sessions.View)]`). The
  `Module.Sessions` nav item carries `RequiredPermission = Sessions.View`, so an
  admin without it never sees the link and lands on `/not-permitted` if they
  navigate directly. `Administrator = "*"` satisfies every code.
- **Write:** CRUD splits across `Sessions.Create` / `.Edit` / `.Delete` (each
  endpoint policy-gated). The **broadcast lifecycle** transitions **and** the
  **recording** upload/remove block both sit behind `Sessions.Publish` (the
  Scientific Committee role) — both API-gated **and** UI-gated via
  `<AuthorizedAction Permission="@PermissionCatalog.Sessions.Publish">`. Excel
  export = `Sessions.Export`, import = `Sessions.Import`.
- An admin with View/Edit but **not** `Sessions.Publish` sees the read-only
  details **without** the lifecycle footer or the recording uploader, and a direct
  `PUT .../status` returns **403**.

## Golden path — create → edit → view → deactivate

1. **List.** `OnInitializedAsync` reads the presentation preference
   (`Prefs.GetPresentationAsync("sessions")`) then `POST .../sessions/list`
   (`GridQuery { Top = 20 }`). Rows render with Code, Title, Hall, Start/End,
   Capacity, an Active pill and a lifecycle Status pill.
2. **Add.** Toolbar **Add** → `OnAddAsync` opens `SessionsAddEdit` (Create) in the
   `CrudShell`. Fill Code, Title (EN/AR), Hall, Start, End (+ optional Description,
   Category, Capacity override, speakers/roles, themes, live URLs) → **Create
   session** → `POST .../sessions`. On success the form closes, the grid reloads,
   and a green toast reads `Admin.Sessions.Created` ("Session \"{title}\" was
   created.").
3. **Edit.** Row **Edit** → `OnEditAsync` first `GET .../sessions/{id}` for the
   **full** detail (the grid summary omits speakers/themes/recording/live URLs, so
   editing from a summary would wipe them), then opens `SessionsAddEdit` (Edit)
   pre-filled with the `IsActive` checkbox visible → **Save changes** →
   `PUT .../sessions/{id}` → green toast `Admin.Sessions.Updated`.
4. **Details.** Row **Details** → `OnDetailsAsync` → `GET .../sessions/{id}` →
   `SessionsViewDelete` (`IsDelete=false`) renders the read-only `<dl>` plus
   Effective capacity, Speakers, Published-at (when present) and the recording row.
5. **Deactivate.** Row **Deactivate** → `OnDeleteAsync` → `GET .../sessions/{id}` →
   `SessionsViewDelete` (`IsDelete=true`) shows a red **Deactivate** button →
   a `SimfConfirm` naming the session (`Admin.Sessions.Delete.Message`) →
   confirm → `DELETE .../sessions/{id}` (soft-delete, `IsActive=false`) → green
   toast `Admin.Sessions.Deactivated`. The row stays visible with the grey
   "Inactive" pill.

## Broadcast lifecycle (`Sessions.Publish`)

In the View form, the footer offers only the **legal next move(s)** from
`NextTransitions(Status)`:

- `Scheduled` → **Mark held**
- `Held` → **Back to scheduled** · **Mark recorded**
- `Recorded` → **Back to held** · **Publish**
- `Published` → **Un-publish**

Each button `PUT .../sessions/{id}/status` (`SetSessionStatusRequest`) and
refreshes the in-form detail. **Publish** stamps `PublishedAt`; the grid's Status
pill turns the green `on` variant ("Published"). An illegal move (e.g. a stale
modal posting Scheduled→Published) is rejected 400
`SESSION_STATUS_TRANSITION_INVALID` with a bilingual error.

## Recording (`Sessions.Publish`)

The View form's recording block: choose a video in
`#session-recording-input` (`accept="video/*"`) → **Upload recording**
(`simfAccount.uploadFile` → `POST .../recording`) → the row reflects
`HasRecording` + file name + size. **Remove recording** → `DELETE .../recording`.
A non-video / empty / oversize file → 400 `SESSION_RECORDING_INVALID` with the
bilingual "The recording must be a video file (mp4, m4v, webm, ogg, mov)." text.

## Excel (D-356)

- **Export** — toolbar **Export** → `OnExportAsync` → `_excel.ExportAsync(ids, query)`
  → `POST .../sessions/export`. With no rows selected it exports the whole filtered
  grid; with rows selected, only those ids. Downloads `simf-sessions-{timestamp}.xlsx`
  (sheet `Sessions`). Speaker roster + theme set are **not** exported.
- **Import** — toolbar **Import** → `OnImportAsync` → `_excel.TriggerImportAsync()`
  opens the picker → `POST .../sessions/import`. Insert-only (row key = Code).
  The result modal shows per-row created/skipped + errors; on success a green
  `Grid.Import.Done` toast and the grid reloads. A bad upload (non-workbook,
  wrong sheet, missing header) is rejected with a bilingual error and nothing is
  created.

## Validation (client guards in `SessionsAddEdit.HandleSubmitAsync`)

| Rule | Failure key (bilingual) |
|------|-------------------------|
| Code present, length 2–16 | `Admin.Sessions.Field.CodeInvalid` |
| Title present, ≤ 256 | `Admin.Sessions.Field.TitleInvalid` |
| Title (Arabic) present, ≤ 256 | `Admin.Sessions.Field.TitleArabicInvalid` |
| Hall parses to a Guid | `Admin.Sessions.Field.HallRequired` |
| Start / End parse | `Admin.Sessions.Field.TimeInvalid` |
| End > Start | `Admin.Sessions.Field.TimeWindowInvalid` |
| Capacity blank, or int ≥ 0 | `Admin.Sessions.Field.CapacityInvalid` |
| Each non-blank live URL passes `LiveStreamUrlPolicy.IsAllowed` | `Admin.Sessions.Field.LiveUrlInvalid` |

A failed guard sets `_error` (in-form `SimfAlert`) and **no** request fires.
Code is trimmed + `ToUpperInvariant`; blank description / live URL becomes `null`.
The server re-validates and returns the matching `ErrorCodes` (see the API doc) —
surfaced via `Error.MessageForCurrentCulture()`.

## Toast / message strategy

- Success: `Admin.Sessions.Created` / `.Updated` / `.Deactivated` (each
  `string.Format` with the title); import → `Grid.Import.Done`.
- List/load failure: `Admin.Sessions.LoadFailed`; form fallback:
  `Admin.Sessions.Fallback`.
- All server errors surface `Error.MessageForCurrentCulture()` (EN/AR per the
  active culture).

## Acceptance (mirrors the E2E catalogue)
The E2E scenarios `E2E-SES-001 … 024` in
[`docs/tests/e2e/cp-admin-sessions.md`](../../tests/e2e/cp-admin-sessions.md)
cover: the CRUD round-trip, the pickers (Hall/Speakers+role+reorder/Themes/
Category), the lifecycle matrix, recording upload/remove, capacity semantics,
grid filter/sort/paginate, empty state, the page + action auth gates, every
validation/conflict, the RTL render, the live-URL validation, the presentation
toggle + full-page round-trip, the delete-confirmation gate, and the Excel
export/import + rejection.

## Related app behaviour
What the operator curates here is what the app shows: the agenda
(**[App Page 016](../../App/Page_016/README.md)**) fetches the whole programme
once and filters client-side; the home surface
(**[App Page 013](../../App/Page_013/README.md)**) shows a next/live-session entry
off the same data. A session is only visible to the app while `IsActive`; its
recording is only surfaced once `Status == Published`.
