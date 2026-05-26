# SIMF Canonical CRUD-Grid Pattern

| | |
|--|--|
| **Authority** | Decision D-117 (2026-05-26) |
| **Gold-standard reference** | [`UsersList.razor`](../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/UsersList.razor) |
| **Component used** | [`SimfDataGrid`](../../src/Shared/SIMF.Components/Forms/SimfDataGrid.razor) |
| **Status** | Active |

This is the single canonical pattern for every Control Panel CRUD list page.
**Copy `UsersList.razor` and adapt — do not invent a new shape.** If the canonical
shape is missing something your page needs, raise it as a new decision so the
shape evolves once for everyone.

---

## 1. Why one shape

A Control Panel where every list page looks and behaves the same is faster to
learn, faster to test, and faster to ship. SIMF spent enough sessions
re-deriving toolbar layouts, pager controls, and modal patterns across
admins / visitors / others / interests that the cost of fragmentation was
visible. D-117 ends that.

## 2. The seven pieces every list page must have

A CRUD list page that wires `SimfDataGrid` correctly gets all of these from
one place, in this order:

1. **Page-top banner** — `<SimfBanner Title="@L[...]" />`.
   No hardcoded titles. Optional `Subtitle` / `Actions` slots are available
   but rarely needed since the toolbar owns the actions.
2. **Server-paged grid** — `<SimfDataGrid TItem="...">` with `Query`, `Page`,
   `Loading`, `OnQueryChanged`, `Multiselect="true"`, `RowKey`, `RowLabel`,
   `Caption`.
3. **Sortable + filterable columns** — `<SimfDataGridColumn>` per column with
   `Sortable="true"` / `Filterable="true"` set deliberately, not by reflex.
4. **Toolbar callbacks wired to what exists** — every `On*` parameter
   (`OnAdd`, `OnEditOne`, `OnDeleteSelected`, `OnDeleteOne`, `OnCopySelected`,
   `OnCopyOne`, `OnPaste`, `OnDuplicateOne`, `OnImport`, `OnExport`,
   `OnDetailsOne`) is rendered **only when the callback is wired**. Wire
   what your domain supports; leave the rest unset. The grid hides anything
   you don't pass.
5. **Modal-based Add / Edit / Details** — never navigate away from the list
   for these. The Create form is a separate child component (e.g.
   `CreateAdminForm.razor`) the modal hosts. A dedicated `/admin/.../new`
   page is acceptable as a deep-link fallback only.
6. **Full pager** — `FirstLabel`, `LastLabel`, `PageSizeLabel`,
   `PageFormatter` parameters wired with localized strings. The grid renders
   First / Prev / numbered (5-wide window) / Next / Last + page-size selector
   + "Page X of Y" caption + the existing "from-to of total" summary.
7. **Stateful modals own their own state in `@code`** — bulk-delete reason
   modal, duplicate confirmation modal, import-result modal, etc. Each modal
   has an `_xOpen` bool (or an `_xTarget` row) on the page and renders inside
   a `<SimfModal>` at the bottom of the markup.

## 3. Iconography is centralized

Every toolbar / row-action / pager button picks its icon from `SimfIcon`'s
named set. The grid already does the picking for you — Add uses `plus`,
Edit uses `edit`, Delete uses `trash`, Copy/Paste/Duplicate use `copy`,
Import uses `upload`, Export uses `download`, pager uses
`chevron-first/-left/-right/-last`. **Do not pass an `Icon=` override** unless
you have a domain-specific reason and have added the icon to `SimfIcon`
additively (no renames of existing names).

## 4. CSS is centralized

All styling is in [`simf-components.css`](../../src/Shared/SIMF.Components/wwwroot/css/simf-components.css)
using tokens from [`theme.tokens.css`](../../src/Shared/SIMF.Components/wwwroot/css/theme.tokens.css).
Do not add a scoped `*.razor.css` to your page; do not write inline `style="..."`;
do not write raw hex; do not redeclare tokens. If a token is missing, add it
to `theme.tokens.css` first and then use it.

## 5. Resx is the source of truth for copy

Every visible string on the page is a `@L["Some.Key"]` lookup against the
project's `Strings.resx` + `Strings.ar.resx`. New keys land in **both**
files in the same change; English and Arabic never drift. Format strings
use positional placeholders (`{0}`, `{1}`, …) for `string.Format` so the
two locales can reorder if needed.

## 6. Backend conventions the grid assumes

- List endpoint: `POST /account/api/admin/{kind}/list`, body = `GridQuery`,
  response = `ApiResult<GridPage<TRow>>`.
- Create / duplicate: `POST /account/api/admin/{kind}` / `.../duplicate`.
- Bulk-delete: `POST /account/api/admin/{kind}/bulk-delete` with a reason.
- Export / Import: `POST /account/api/admin/{kind}/{export,import}`.
- All called via `simfAccount.postJson` / `downloadXlsx` / `uploadFile` JS
  interop — see `js/simf-account.js`.

## 7. What's intentionally NOT in the pattern

- **Per-page navbars / breadcrumbs** — the shell handles navigation.
- **Per-page theming** — `theme.tokens.css` owns colours; pages don't.
- **Server-rendered tables** — every CRUD list is `SimfDataGrid`; we don't
  hand-roll a `<table>` for CRUD.
- **Toast frameworks** — the page-local `_toast` record pattern from
  `UsersList.razor` is sufficient; do not pull in a global toast bus.

## 8. When the pattern changes

Open a new decision-log entry that supersedes the relevant point above, and
update this file in the same change. Do not let the canonical reference
silently diverge from the doc.

---

## Quick checklist for a new CRUD page

- [ ] Page uses `<SimfBanner Title="@L[...]" />` at the top — no hardcoded label.
- [ ] Grid uses `<SimfDataGrid>` with `Multiselect="true"` + `RowKey` + `RowLabel`.
- [ ] Sortable / Filterable on every column that genuinely supports it.
- [ ] Every wired `On*` callback points at a real backend endpoint
      (or an explicit "awaiting generic CRUD" stub).
- [ ] Add opens a modal hosting an extracted `Create...Form.razor` child;
      `/admin/.../new` exists as a fallback page that hosts the same form.
- [ ] Edit, Details, bulk-delete reason, duplicate confirmation, import result
      modals all use `<SimfModal>`.
- [ ] All labels pass through the resx, both `Strings.resx` and `Strings.ar.resx`.
- [ ] Pager-label parameters (`FirstLabel`, `LastLabel`, `PageSizeLabel`,
      `PageFormatter`) are wired.
- [ ] No inline `<style>`, no raw hex, no scoped `*.razor.css` on the page.
- [ ] No `Nav.NavigateTo` for create / edit / details (modals only).
