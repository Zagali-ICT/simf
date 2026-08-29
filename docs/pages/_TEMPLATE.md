# {Page Title} — `{route}`

> **Authority:** SIMF page reference doc template (D-133).
> Copy this file to `docs/pages/{cp|web|mobile}/{slug}.md` and fill every section.
> Empty sections are NOT acceptable — write "N/A — {reason}" if a section genuinely doesn't apply.

| | |
|--|--|
| **Route** | `{e.g. /admin/interests}` |
| **Layout** | `{e.g. CpShellLayout}` |
| **Surface** | `{Control Panel / Website / Mobile App}` |
| **Audience** | `{e.g. Administrator}` |
| **Auth** | `{e.g. Administrator role + Approved account + JWT bearer}` |
| **Pattern** | `{e.g. D-117 canonical CRUD + D-132 mandatory Multiselect/Banner}` |
| **Status** | `{Real / Stub / Auth-only}` |
| **Implements use case(s)** | `{UC-IDs from SIMF-UCS-001, comma-separated}` |
| **Backend endpoints** | `{list of /account/api/* or /api/v1/* calls}` |
| **Source file** | `{path}` |
| **Tests** | `{E2E entry path + unit/integration test classes if any}` |
| **Last reviewed** | `{YYYY-MM-DD}` |

---

## 1. Purpose

One paragraph (3–6 sentences) that answers: **why does this page exist?**
What problem does it solve for the audience listed above?
What does the audience walk in expecting to do?
Avoid restating the title — explain the job.

## 2. Audience + permissions

- **Who can reach it:** {role list}
- **Who can edit/write on it:** {role list} (if different from reach)
- **Authorisation gates:** the page attribute + the API policy behind it
  (e.g. `@attribute [RequirePermission(PermissionCatalog.Visitors.View)]` +
  `RequireApprovedAccount`). Pages are gated by a NAMED PERMISSION, not by a
  role — a role check would admit any Administrator to every page, which is
  what the permission system exists to prevent. Copy the real code from the
  page's `@attribute`; do not write a role here.
- **What an unauthenticated user sees:** {redirect target / 401 / 403}

## 3. Screenshots

Each screenshot lives under `docs/screenshots/`. Annotate the file name with the
page slug + state (e.g. `interests-canonical.png`, `interests-add-modal.png`,
`interests-empty-state.png`).

| State | File | Captured |
|-------|------|----------|
| Default (with rows) | `docs/screenshots/{slug}-default.png` | {YYYY-MM-DD} |
| Empty state | `docs/screenshots/{slug}-empty.png` | {YYYY-MM-DD} |
| Add modal | `docs/screenshots/{slug}-add-modal.png` | {YYYY-MM-DD} |
| Edit modal | `docs/screenshots/{slug}-edit-modal.png` | {YYYY-MM-DD} |
| Details modal | `docs/screenshots/{slug}-details-modal.png` | {YYYY-MM-DD} |
| RTL (Arabic) | `docs/screenshots/{slug}-rtl.png` | {YYYY-MM-DD} |
| Error state | `docs/screenshots/{slug}-error.png` | {YYYY-MM-DD} |

## 4. UI affordances

### 4.1 Banner / page header
{What does `SimfBanner` show? Subtitle? Actions slot? Or — for non-list pages — describe the page header / hero.}

### 4.2 Toolbar (CRUD pages only)
| Button | Wired callback | Calls | Notes |
|--------|----------------|-------|-------|
| Select all | `ToggleSelectAllAsync` | — | Multiselect=true mandatory per D-132 |
| Add | `OnAdd` | opens modal | … |
| Edit | `OnEditOne` | opens modal | … |
| Details | `OnDetailsOne` | opens modal | … |
| Delete | `OnDeleteOne` | `DELETE /…/{id}` | … |
| (others — Copy, Paste, Duplicate, Import, Export — fill if wired) | | | |

### 4.3 Grid columns (CRUD pages only)
| Column | Source field | Sortable | Filterable | Notes |
|--------|--------------|----------|------------|-------|
| Name | `r.Name` | yes | yes | … |
| … | | | | |

### 4.4 Pager
- First / Prev / numbered (5-wide) / Next / Last
- Page-size selector: 10 / 20 / 50 / 100
- Caption: "Showing X–Y of Z"

### 4.5 Form fields (if the page hosts a form or modal)
| Field | Type | Required | MaxLength | Validation | Locale |
|-------|------|----------|-----------|------------|--------|
| Name | text | yes | 128 | `^.{1,128}$`, unique | EN + AR resx keys |
| … | | | | | |

## 5. Data flow

```
{User action} → {component event handler} → {JS interop call to BFF /account/api/...}
              → {API endpoint /api/v1/...} → {Application service} → {DB}
              → ApiResult<T> envelope → {UI update + toast}
```

List every backend call this page makes:

| When | Method + path | Request body | Response shape |
|------|---------------|--------------|----------------|
| OnInit | `POST /account/api/admin/interests/list` | `GridQuery` | `ApiResult<GridPage<AdminInterestSummary>>` |
| OnAdd success | `POST /account/api/admin/interests` | `AdminCreateInterestRequest` | `ApiResult<AdminInterestSummary>` |
| … | | | |

## 6. Validation + error handling

- **Client-side guards:** {what the .razor checks before the call fires}
- **Server-side validation:** {which FluentValidation validator + where it lives}
- **Error envelope:** standard `ApiResult<T>.Error` with `Code` (from `ErrorCodes`)
  + bilingual `Message`/`MessageArabic`
- **Toast strategy:** {success / error / info — which resx keys}

## 7. Edge cases + known limitations

- {List 3–10 edge cases the implementation handles, with the source line that
  proves the handling. E.g. "Concurrent edit collision → backend returns 409
  + `ErrorCodes.Conflict` → UI shows the bilingual error in the toast."}
- {Known limitations: things the page does NOT do today, and why.}

## 8. i18n + RTL

- All visible strings come from `Strings.resx` (EN) + `Strings.ar.resx` (AR)
  via `IStringLocalizer<Strings> L`.
- Toggle: `العربية` / `English` link in the top header.
- RTL: `<html dir="rtl" lang="ar">` set on the document; nav rail mirrors,
  table headers flip, action buttons stay inside the row.

## 9. Accessibility

- Keyboard: {tab order, focus management when modals open/close, ESC to close modals}
- Screen reader: {what the SimfDataGrid Caption / aria-label / role provides}
- Colour contrast: {WCAG AA via `theme.tokens.css`}
- Focus indicators: `--focus-ring` token, visible on every focusable element

## 10. Related use cases (UCS-001)

| UC ID | Title | Notes |
|-------|-------|-------|
| UC-… | … | … |

## 11. Related E2E test scenarios

| Scenario | File | Coverage |
|----------|------|----------|
| Golden path — Add / Edit / Delete | `docs/tests/e2e/{slug}.md#golden` | … |
| Empty state | `docs/tests/e2e/{slug}.md#empty` | … |
| Auth failure (non-admin user) | `docs/tests/e2e/{slug}.md#auth` | … |
| Validation failure on Add | `docs/tests/e2e/{slug}.md#validation` | … |
| Server error (500 on the list endpoint) | `docs/tests/e2e/{slug}.md#server-error` | … |
| RTL render | `docs/tests/e2e/{slug}.md#rtl` | … |

## 12. Related docs

- Manual chapter: {link to the Admin/User/Developer Manual section}
- Pattern doc: [`SIMF_TABLE_PATTERN.md`](../../dev/SIMF_TABLE_PATTERN.md) (for CRUD pages)
- Architecture: [`SIMF-SAD-001`](../../SIMF-SAD-001-Software-Architecture-Document.md) — relevant section
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) — relevant endpoint group
- Decisions log: link to the D-### that shipped (and any later D-### that changed) this page
- Source: link to the `.razor` file
- Component catalogue: [`SIMF-CMP-001`](../../SIMF-CMP-001-Component-Catalog.md) — components used

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-MM-DD | D-### | … |

---

_Last reviewed:_ `YYYY-MM-DD` by `{author}`. If the page has changed and this
doc has not been re-reviewed in 60 days, it is **out of date**. Re-walk the
page in a browser and update every section that drifted.
