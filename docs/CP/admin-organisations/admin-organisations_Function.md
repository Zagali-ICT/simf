# Organisations — Function (`/admin/organisations`)

What the operator does on the page, the golden path, validation, permission
gating, and the bilingual toast/confirm text. Verified against
`OrganisationsList.razor`, `OrganisationAddEdit.razor`,
`OrganisationViewDelete.razor`, `AdminOrganisationService.cs` this session.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> Companion docs: [Design](admin-organisations_Design.md) ·
> [API](admin-organisations_API.md) · [Logic](admin-organisations_Logic.md).

## Who can use it

The page is gated by `@attribute [RequirePermission(PermissionCatalog.
Organisations.View)]`. A signed-in admin without `Organisations.View` is routed
to `/not-permitted` and no `/list` call fires. Each action is then individually
gated (baseline `AdminOnly`; `Administrator = "*"` sees everything):

| Action | Permission |
|--------|------------|
| See the grid / search / paging | `Organisations.View` |
| Add / Create | `Organisations.Create` |
| Edit | `Organisations.Edit` |
| Details (view) | `Organisations.View` |
| Deactivate (soft-delete) | `Organisations.Delete` |
| Import Excel | `Organisations.Import` |
| Export Excel | `Organisations.Export` |

Per the project HARD RULE, action buttons are gated so an admin who lacks a
permission does not see the affordance (`AuthorizedAction` / the grid's
permission-aware action set). An admin with View but not Create sees the grid but
no Add affordance; missing Import/Export hides those toolbar actions; missing
Edit/Delete hides the per-row pencil/trash icons.

## Operator actions

### 1. Browse / search / filter / sort
- The grid loads on init (`POST …/organisations/list`, `Top = 20`).
- **Search** (toolbar): type a term + click Search → reloads server-side with
  `GridQuery.Search`, `Skip` reset to 0. LIKE matches across Arabic name,
  English name, commercial registration and city. Clearing + Search returns the
  full grid (`Search = null`).
- **Per-column filter** (D-255 grid): the Name (Arabic), Name (English), CR,
  Sector and City columns each carry a filter input that drives
  `GridQuery.Filters["{key}"]`; filters accumulate; `Skip` resets to 0. (Active
  is not filterable in the UI, though the service can parse an `isactive`
  filter.)
- **Sort**: the Name (Arabic), City and Active headers are sortable
  (`GridQuery.Sort` + `SortDescending`). Default order is Arabic name ascending.

### 2. Create (`Add`)
- Click Add → `OrganisationAddEdit` opens in Create mode (dialog or full page per
  the toggle).
- Fill at least **Name (Arabic)** (the only required field); optionally the other
  seven fields + (Edit mode only) the Active checkbox.
- Click **Save** → `POST …/organisations` with `CreateOrganisationRequest`
  (blank optional fields sent as `null`, Arabic name trimmed).
- Success → form closes, green toast `Admin.Organisations.Saved` = "Organisation
  saved.", grid reloads.

### 3. Edit
- Click the row's pencil → the page first fetches the full detail
  (`GET …/organisations/{id}`, because the grid omits Phone/Email/Website), then
  opens `OrganisationAddEdit` in Edit mode with every field pre-filled and the
  Active checkbox shown.
- Change fields → **Save** → `PUT …/organisations/{id}` with
  `UpdateOrganisationRequest` (includes `IsActive`). Same green "Organisation
  saved." toast.

### 4. View (Details)
- Click the row's eye → `OrganisationViewDelete` opens read-only, showing every
  column including Phone / Email / Website. Only a **Close** button — no
  Deactivate.

### 5. Deactivate (soft-delete)
- Click the row's trash → the detail loads and `OrganisationViewDelete` opens
  with a red **Deactivate** button.
- Click Deactivate → a **`SimfConfirm`** appears: title "Deactivate
  organisation", message `Deactivate "{name}"? It will be removed from the public
  lookup.` Cancel → no DELETE, the row stays active. Confirm → exactly one
  `DELETE …/organisations/{id}`.
- Success → form closes, green toast `Admin.Organisations.Deleted` =
  "Organisation deactivated.", grid reloads (the row drops out of the
  active-default grid and out of the app الجهة picker).
- Deactivate is **idempotent** server-side: an already-inactive row returns
  without a second audit write.

### 6. Import a government Excel sheet
- Click **Import Excel** → the import modal opens.
- Pick a `.xlsx`; Upload enables; click **Upload** →
  `POST …/organisations/import` (multipart, field `file`).
- The modal shows `Rows read · Inserted · Updated · Skipped` plus a per-row error
  list; a green toast `Admin.Organisations.Import.Done` = "Import complete — {0}
  inserted, {1} updated, {2} skipped." The grid reloads.
- Re-uploading the same sheet **upserts** (matched by commercial registration, or
  by exact active Arabic name when no CR) — so a re-import updates rather than
  duplicates. Bad rows (e.g. blank Arabic name) are skipped and listed, not
  fatal.
- Guard failures (no file / > 5 MB / not a real `.xlsx`) → error toast
  `Admin.Organisations.Import.Failed` = "Excel import failed." or the server's
  bilingual message.

### 7. Export the grid to Excel
- Toolbar **Export** → direct `.xlsx` download
  (`simf-organisations-{timestamp}.xlsx`, sheet "Organisations"). With rows
  selected it exports those rows; with none selected it exports the current
  filtered/searched grid (capped at 5,000 rows).

## Golden path (create → edit → soft-delete)

1. **Add** "شركة البحرية للأنظمة" (+ optional NameEn / CR / Sector / City /
   Phone / Email / Website) → Save → 200 → "Organisation saved."
2. **Search** the CR → grid shows the one row.
3. **Edit** (pencil) → detail prefilled (incl. contact fields) → change City →
   Save → 200 → "Organisation saved.", City updated.
4. **Delete** (trash) → View/Delete opens → Deactivate → confirm → 200 →
   "Organisation deactivated." → on reload the row is gone from the active grid.

(Mirrors E2E-ORG-001 in `docs/tests/e2e/cp-admin-organisations.md`.)

## Validation rules (aligned across the three layers)

The server-side `AdminOrganisationService.ValidateAndNormalise` is the **source
of truth**; the UI `MaxLength` caps match it; there is no EF `HasMaxLength`
attribute on the entity (the entity is plain; the field caps are enforced in the
service, and the import path additionally `Clamp`s to the same lengths).

| Field | UI `MaxLength` | Server limit (`ValidateAndNormalise`) | Required | Over-limit error |
|-------|----------------|----------------------------------------|----------|------------------|
| Name (Arabic) | 256 | 1–256 (`< 1 or > 256` → throw) | yes | 400 `ORGANISATION_INVALID` |
| Name (English) | 256 | ≤ 256 | no | 400 `ORGANISATION_INVALID` |
| Commercial registration | 32 | ≤ 32 (+ unique → 409) | no | 400 / 409 `ORGANISATION_INVALID` |
| Sector | 128 | ≤ 128 | no | 400 |
| City | 128 | ≤ 128 | no | 400 |
| Phone | 32 | ≤ 32 | no | 400 |
| Email | 320 | ≤ 320 | no | 400 |
| Website | 512 | ≤ 512 | no | 400 |

- **Client guard:** a blank Arabic name shows the inline bilingual alert
  `Admin.Organisations.Required` = "Arabic name is required." and **does not
  POST**.
- **Server guard:** every other rule (length, uniqueness) is enforced by the
  service and surfaced via `MessageForCurrentCulture()` in the form's error
  alert; the form stays open on failure.
- Because the UI caps the inputs at the same lengths, hitting the server length
  error normally requires bypassing `MaxLength` (e.g. scripted input) — see
  E2E-ORG-008.

## Bilingual toast / confirm text (verbatim EN; AR pair in `Strings.ar.resx`)

| Key | EN value |
|-----|----------|
| `Admin.Organisations.Saved` | Organisation saved. |
| `Admin.Organisations.Deleted` | Organisation deactivated. |
| `Admin.Organisations.LoadFailed` | Could not load organisations. |
| `Admin.Organisations.Required` | Arabic name is required. |
| `Admin.Organisations.Import.Done` | Import complete — {0} inserted, {1} updated, {2} skipped. |
| `Admin.Organisations.Import.Failed` | Excel import failed. |
| `Admin.Organisations.Delete.Message` | Deactivate "{0}"? It will be removed from the public lookup. |
| `Admin.Organisations.None` | No organisations found |
| `Admin.Organisations.Loading` | Loading organisations… |

All toast/confirm text is resx-driven and renders in the active culture; server
errors carry their own EN/AR pair via the `ApiResult.Error` envelope.

## Why it matters to the app

Curating this list keeps the app's **الجهة (organisation) picker** on App Page
007 accurate: only **active** organisations appear in
`GET /app/organisations`, so adding a row makes it pickable and deactivating one
removes it from the visitor's sign-up form. See
[App Page 007](../../App/Page_007/README.md).
