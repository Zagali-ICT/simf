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

_Authored 2026-06-09 (D-353). Update this guide if the framework API changes._
