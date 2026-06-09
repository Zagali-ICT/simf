# SIMF — CRUD Frame Dev Guide (dialog vs full page)

| | |
|--|--|
| **Decision** | D-353 |
| **Status** | Framework shipped; Interests is the reference pilot. The other 38 CP list pages roll out framework-first, in per-area batches. |
| **Audience** | Developers converting a CP list page to the centralized framing. |
| **Reference page** | [`InterestsList.razor`](../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/InterestsList.razor) + [`InterestAddEdit.razor`](../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/InterestAddEdit.razor) + [`InterestViewDelete.razor`](../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/InterestViewDelete.razor) |

---

## 1. What the framework gives you

Every CP CRUD page lets the admin choose how Add / Edit / View / Delete forms
open — a **popup** or a **full page** — from one toolbar toggle. The choice is
saved per browser and applied next time. The form is written **once** and the
shell decides the framing; the form never knows whether it is in a dialog or a
page.

Each entity collapses to **two** reusable forms, not four:

- **Add + Edit** → one component inheriting `CrudAddEditFormBase<T>`.
- **View + Delete** → one component inheriting `CrudViewDeleteFormBase<T>`.

### Shared building blocks (`SIMF.Components/Forms`)

| Type | Role |
|------|------|
| `CrudPresentation` | enum `Dialog \| Page` (UI-only; not a persisted domain enum). |
| `CrudFormBase<T>` | shared `Initial` + `OnCancel`. |
| `CrudAddEditFormBase<T>` | adds `IsEdit` + `OnSuccess`. Inherit this for the Add/Edit form. |
| `CrudViewDeleteFormBase<T>` | adds `IsDelete` + `OnDeleted`. Inherit this for the View/Delete form. |
| `CrudShell` | the one switch: frames `ChildContent` as a dialog or a page by `Presentation`. |
| `CrudDialogFrame` / `CrudPageFrame` | the two presentations (wrap `SimfModal` / an in-flow panel). You rarely use these directly — use `CrudShell`. |
| `SimfConfirm` | a must-decide confirmation dialog (for the Delete step). |

### CP building blocks (`SIMF.ControlPanel`)

| Type | Role |
|------|------|
| `CpPreferences` | typed, versioned per-page localStorage store. `GetPresentationAsync` / `SetPresentationAsync` / `ClearAllAsync`. Injected `@inject CpPreferences Prefs`. |
| `CrudPresentationToggle` | the toolbar button. Self-persists; `@bind-Value` it to the page's `_presentation`. |
| `simf-prefs.js` | the localStorage helper behind `CpPreferences` (already wired in `App.razor`). |

The per-user wipe lives on **Profile → Display preferences → Clear saved layout**.

---

## 2. Per-page conversion recipe

For a page `XList.razor` editing summary type `AdminXSummary`:

1. **Add/Edit form `XAddEdit.razor`** — `@inherits CrudAddEditFormBase<AdminXSummary>`.
   - Do **not** redeclare `Initial` / `IsEdit` / `OnSuccess` / `OnCancel` — they
     come from the base.
   - Branch Create vs Update on `IsEdit` (POST when `!IsEdit`, PUT against
     `Initial!.Id` when `IsEdit`). Prefill from `Initial` in `OnInitialized`.
   - Render the form's own action row (`simf-form__actions`) with the submit
     button + a Cancel that calls `OnCancel`. The frame supplies no buttons.
2. **View/Delete form `XViewDelete.razor`** — `@inherits CrudViewDeleteFormBase<AdminXSummary>`.
   - Render the read-only details (`<dl class="simf-dl">`).
   - When `IsDelete`, render a danger Delete button that opens a `SimfConfirm`
     (`Danger="true"`); on confirm call the delete endpoint and, on success,
     `await OnDeleted.InvokeAsync(Initial)`.
   - Always render a Close button calling `OnCancel`.
3. **List page `XList.razor`**:
   - `@inject CpPreferences Prefs` and a `private const string PageKey = "x";`
     (stable, unique per page — used as the localStorage key suffix).
   - State: `_presentation`, a `FormKind { None, AddEdit, ViewDelete }`,
     `_isEdit`, `_isDelete`, `_target`; helpers `FormOpen` and
     `GridHidden => FormOpen && _presentation == CrudPresentation.Page`.
   - `OnInitializedAsync`: `_presentation = await Prefs.GetPresentationAsync(PageKey);`
     then load the grid.
   - Wrap the banner + grid in `@if (!GridHidden) { … }` so full-page mode hides
     the grid.
   - Add the toggle to the grid's `<CustomToolbar>`:
     ```razor
     <CrudPresentationToggle PageKey="@PageKey" @bind-Value="_presentation"
                             DialogLabel="@L["Grid.View.Dialog"]"
                             PageLabel="@L["Grid.View.Page"]" />
     ```
   - Wire `OnAdd` / `OnEditOne` / `OnDetailsOne` / `OnDeleteOne` to set the
     `FormKind` + flags + `_target` (no direct delete call any more).
   - Render one `CrudShell` hosting the right form:
     ```razor
     @if (FormOpen)
     {
         <CrudShell Open="true" Presentation="_presentation" Title="@FormTitle"
                    CloseLabel="@L["…Close"]" OnClose="CloseForm">
             @if (_form == FormKind.AddEdit)
             {
                 <XAddEdit IsEdit="_isEdit" Initial="_target"
                           OnSuccess="OnSavedAsync" OnCancel="CloseForm" />
             }
             else if (_form == FormKind.ViewDelete)
             {
                 <XViewDelete IsDelete="_isDelete" Initial="_target"
                              OnDeleted="OnDeletedAsync" OnCancel="CloseForm" />
             }
         </CrudShell>
     }
     ```

---

## 3. Rules of the road

- **Type-safety, not dictionaries.** Bind the form's typed parameters directly
  in the list page. Never use `DynamicComponent` + a parameter dictionary.
- **The toggle goes in `CustomToolbar`** — it renders after the built-in grid
  buttons ("at the end"), matching the owner's request.
- **`PageKey` is permanent.** Renaming it orphans a user's saved choice.
- **No new route, no new permission.** Full page is in-place on the same route,
  so it reuses the list page's existing `[RequirePermission]` and per-row
  `<AuthorizedAction>` gates. Do not add `/x/{id}/edit` routes.
- **No schema, no server preference.** The choice is localStorage only — the
  Identity schema stays frozen (D-110).
- **Delete always confirms.** The View/Delete form must gate the destructive
  call behind `SimfConfirm`.

---

## 4. Definition of Done (per converted page)

Per the project rules (D-246), the conversion is not done until, in the **same
changeset**:

- bilingual resx for any new strings (reuse `Grid.View.Dialog` / `Grid.View.Page`
  for the toggle);
- the per-page reference doc (`docs/pages/cp/{slug}.md`) + `PAGE-INDEX.md`
  updated;
- the E2E catalogue (`docs/tests/e2e/cp-{slug}.md`) updated with the toggle,
  full-page round-trip, and delete-confirmation scenarios;
- unit/bUnit coverage for the new View/Delete confirm + the `IsEdit` branch;
- `dotnet build -c Release` 0/0 and a live browser check of the page in **both**
  dialog and full-page mode.

---

---

## 5. Excel Export + Import (D-356) — the dynamic grid Excel engine

The uniform standard now also includes Excel **Export** (every CRUD page) and
**Import** (every page that has a create/upsert path; read-only / queue pages
get Export only). One hardened engine renders/parses for all resources — the
per-resource code is minimal. **Interests is the reference** (`InterestExcelEndpoints.cs`,
`CrudGridExcel.razor`, `InterestsList.razor`).

### Backend (one file per resource)
`src/Backend/SIMF.Api/Endpoints/Admin/{Resource}ExcelEndpoints.cs`:

- `Export{Resource}Endpoint : AdminGridExportEndpoint<TSummary>` — declare
  `RoutePath` (`/admin/{slug}/export`), `Permission`
  (`PermissionCatalog.{Resource}.Export`), `SheetName`, `FilePrefix`, the
  `Columns` (header + per-row value selector), `ListAsync` (reuse the existing
  list service) and `IdOf`.
- `Import{Resource}Endpoint : AdminGridImportEndpoint` — declare `RoutePath`
  (`/admin/{slug}/import`), `Permission` (`...Import`), `SheetName`,
  `RequiredHeaders`, `RowKey`, and `ApplyRowAsync` (bind a parsed row to the
  create request + call the service; **throw `DataValidationException` for a bad
  row** → it becomes a per-row error, never a batch abort). Omit this class for
  read-only / queue resources.

### Permissions
Add `{Resource}.Export` (+ `.Import`) to the nested class **and** to `All` in
`PermissionCatalog` (`AdminOnly` baseline). Idempotent seed — **no migration**.

### CP BFF
One line in `AccountEndpoints.MapAccountEndpoints`: `MapGridExcel(group, "{slug}");`
— registers the `/admin/{slug}/export` + `/import` proxies.

### CP page
Wire the grid: `OnExport="OnExportAsync"` + `OnImport="OnImportAsync"` +
`ExportLabel="@L["Grid.Export"]"` + `ImportLabel="@L["Grid.Import"]"`; add
`<CrudGridExcel @ref="_excel" Resource="{slug}" OnImported="OnImportedAsync" OnError="OnExcelError" />`
after the grid; add the four handlers (copy from `InterestsList.razor`).
Read-only pages omit `OnImport` / `ImportLabel`.

### Security (inherited — never reimplement per page)
Formula-injection sanitisation (CWE-1236), strict sheet-name match, required-header
check, 5 MB + ZIP-magic upload gate and the 5 000-row cap all live in the engine
(`ClosedXmlGridExcelExporter` / `ClosedXmlGridExcelImporter` /
`AdminGridImportEndpoint`).

### DoD (in addition to §4)
An integration test mirroring `InterestExcelTests.cs` (export round-trip, a
positive import, the not-a-workbook + wrong-sheet rejections, the permission
gate) and the page's E2E catalogue gains export + import scenarios.

---

_Authored 2026-06-09 (D-353); §5 added 2026-06-09 (D-356). Update this guide if the framework API changes._
