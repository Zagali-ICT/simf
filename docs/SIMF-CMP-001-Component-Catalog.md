# SIMF-CMP-001 — `Simf*` Component Catalog

**Doc owner:** SIMF Frontend
**Status:** Living document — maintained per increment alongside `docs/decisions/DECISIONS_LOG.md`.
**Last updated:** 2026-05-23 (Commit A of decision D-044 series).

The `Simf*` library is the dependency-light Blazor component set the Control
Panel and the Website both build against. No MudBlazor, no Radzen, no
DevExpress — every component lives in `src/Shared/SIMF.Components` and every
visual value comes from a token in
`src/Shared/SIMF.Components/wwwroot/css/theme.tokens.css`.

This catalog is the single answer to *"is there already a component for this?"*
Before adding any new UI, check it here first.

## Conventions

- **Namespace**: `SIMF.Components`, `SIMF.Components.Forms`, `SIMF.Components.Layout`, `SIMF.Components.Controls`.
- **Naming**: `SimfFoo` for the component, `simf-foo` for the BEM root class.
- **Tokens only**: no raw colours, no raw font-families, no off-scale spacing.
- **Direction-aware**: layout uses CSS logical properties (`inline-size`, `inset-inline-start`, etc.) so AR (RTL) and EN (LTR) share one stylesheet.
- **One primary action per surface**: the button library encodes the rule.
- **`CancellationToken`** on async interfaces; `@bind-Value` on form components.

## Status legend

- ✅ **Shipped** — in the library and usable today.
- 🚧 **In flight** — being built in the current commit series (D-044 a/b/c).
- ⏭ **Queued** — known need, scheduled as its own increment.

---

## Layout

| Component | Status | Purpose |
|-----------|--------|---------|
| `SimfAuthLayout` | ✅ | Two-column sign-in layout — brand panel + form area, collapses on narrow viewports. |
| `SimfAuthCard` | ✅ | The form card on the auth pages (400 px / 440 px wide variants). |
| `SimfBrandPanel` | ✅ | Brand-panel content with logo, subtitle and caption. |
| `SimfWordmark` | ✅ | The "SIMF" wordmark lockup (placeholder while final SVG is pending). |
| `SimfSignedInLanding` | ✅ | The post-sign-in landing card before the user picks a section. |
| `SimfAppShell` | ✅ | The CP shell — top bar + side nav + content; toggleable nav; direction-aware. |
| `SimfNavGroup` / `SimfNavItem` | ✅ | Side-nav grouping + entries with active-route styling. |
| `SimfPageHeader` | ✅ | Page-level title + `Actions` slot inside the shell content area. |
| `SimfStatCard` | ⏭ | Dashboard summary tile (title + value + delta). Queued for Commit C. |

## Forms — fields

| Component | Status | Purpose |
|-----------|--------|---------|
| `SimfTextField` | ✅ | Labelled single-line input, `EditContext`-bound. |
| `SimfPasswordField` | ✅ | Password input with show/hide toggle. |
| `SimfCodeField` | ✅ | 6-digit code input (centered, large, monospace-spaced). |
| `SimfCheckbox` | ✅ | Labelled checkbox primitive (D-043). |
| `SimfSelect<TValue>` | ✅ | Typed labelled select; shares the field shell with `SimfTextField`. |
| `SimfTextarea` | ✅ | Multi-line text field; shares the field shell. |
| `SimfRadioGroup` | ⏭ | Mutually-exclusive choice — for Saudi / non-Saudi visitor profile (Commit C). |
| `SimfDatePicker` | ⏭ | Native `<input type="date">` with the SIMF field shell (Commit C). |
| `SimfPhoneInput` | ⏭ | Saudi / international phone, country-code prefix toggle (Commit C). |
| `SimfFileUpload` | ⏭ | Magic-byte-validated file upload (ID image attachment, Commit C). |
| `SimfNumberField` | ⏭ | Numeric input with stepper. Queued — needed by event capacity / pricing fields. |
| `SimfChip` | ⏭ | Multi-select tag picker — interests / topics. Queued. |

## Forms — buttons and actions

| Component | Status | Variants |
|-----------|--------|----------|
| `SimfButton` | ✅ | `primary` (navy) / `secondary` (outlined) / `neutral` (Grey, myComment #17) / `danger`. |
| `SimfLink` | ✅ | Anchor styled with the link tokens; focus-ring respected. |

## Indicators and feedback

| Component | Status | Purpose |
|-----------|--------|---------|
| `SimfAlert` | ✅ | Inline form-level message (`error` / `info` / `success`). |
| `SimfPill` | ✅ | Inline status chip (`neutral` / `admin` / `on` / `off`, D-043). |
| `SimfIcon` | ✅ | The inline-SVG icon set — `mail`, `lock`, `eye`, `eye-off`, `alert`, `info`, `check`, `check-circle`, `globe`, `sun`, `moon`, `arrow-back`, `menu`, `bell`, `user`, `close`, `search`. |
| `SimfSpinner` | ✅ | Standalone loading indicator (`sm` / `lg`). |
| `SimfBadge` | ✅ | Small count or dot indicator (`neutral` / `info` / `success` / `warning` / `danger`). |
| `SimfEmptyState` | ✅ | Centred empty-state panel with title, description and action slot. |
| `SimfTooltip` | ⏭ | CSS-only hover hint (Commit C). |

## Data display

| Component | Status | Purpose |
|-----------|--------|---------|
| `SimfTable<TItem>` | ✅ | Typed list table with `HeaderTemplate` / `RowTemplate` / `EmptyTemplate` (D-043). Kept for simple ad-hoc lists; new pages prefer `SimfDataGrid`. |
| `SimfDataGrid<TItem>` v1 | ✅ | Server-paged grid — header sort, per-column filter inputs, row-actions slot, loading overlay, paged footer (D-044 a). |
| `SimfDataGridColumn<TItem>` | ✅ | Column declaration nested inside `SimfDataGrid <Columns>` slot. |
| `SimfDataGrid<TItem>` v2 | ✅ | Adds row right-click context menu (Edit / Copy / Duplicate / Delete), per-row fixed action buttons (Edit / Copy / Delete), top action toolbar (Select All / Add / Edit / Delete / Copy / Paste / Duplicate / Import / Export), per-row selection via a checkbox column, bulk-action endpoints, Excel I/O via ClosedXML (D-044 b). |
| `SimfContextMenu` + `SimfContextMenuItem` | ✅ | Floating context menu positioned at a mouse coordinate — used by the data-grid (D-044 b). |
| `SimfToolbarButton` | ✅ | Compact icon-first button used by the data-grid top toolbar (D-044 b). |
| `SimfChart` | ⏭ | Charting primitives. Library choice deferred. |

## Overlays

| Component | Status | Purpose |
|-----------|--------|---------|
| `SimfModal` | ✅ | Overlay dialog — Esc / backdrop / explicit close; body scroll-locked. |

## Controls

| Component | Status | Purpose |
|-----------|--------|---------|
| `SimfThemeToggle` | ✅ | Light / dark theme switch — persists to `localStorage`. |
| `SimfLanguageSwitch` | ✅ | English / Arabic switch — full page reload on change. |

## Cross-cutting

| Topic | Status | Notes |
|-------|--------|-------|
| Grid query / page contract | ✅ | `GridQuery` + `GridPage<T>` in `SIMF.Common` (D-044 a). Every server-paged list endpoint uses this shape. |
| Excel I/O | ✅ | `IUserExcelService` + ClosedXML on the server (D-044 b). Used by the grid Import / Export buttons. |
| Grey theme tokens | ⏭ | Full third theme (Grey) in `theme.tokens.css` — separate increment. The `SimfButton.neutral` variant lands now (D-044 a). |
| `SimfTabs` | ⏭ | Needs design pass for active/disabled/RTL states. |
| `SimfBreadcrumbs` | ⏭ | Route-aware crumb-trail; needs the crumb infra. |
| `SimfAvatar` | ⏭ | Dedicated avatar component; the profile page currently inlines `<img>`. |
| `SimfNotificationCenter` | ⏭ | Needs the backend notification table + transport (myComment #33). |

---

## Where do they live?

```
src/Shared/SIMF.Components/
  Layout/          SimfAppShell, SimfAuthLayout, SimfAuthCard, SimfPageHeader, SimfNavGroup, SimfNavItem, SimfBrandPanel, SimfWordmark, SimfSignedInLanding
  Forms/           SimfButton, SimfTextField, SimfPasswordField, SimfCodeField, SimfCheckbox, SimfSelect, SimfTextarea, SimfDataGrid, SimfDataGridColumn, SimfModal, SimfTable, SimfPill, SimfSpinner, SimfBadge, SimfEmptyState, SimfAlert
  Controls/        SimfLink, SimfThemeToggle, SimfLanguageSwitch
  SimfIcon.razor   the inline-SVG icon set
  wwwroot/
    css/
      theme.tokens.css     the only source of design tokens
      simf-components.css  the only component stylesheet
    js/
      simf-theme.js
```

## How to add a new component

1. Add it under the right folder (Forms / Layout / Controls).
2. Use the `simf-foo` BEM root and put all styles in `simf-components.css` under a labelled section.
3. Tokens only. If a value isn't in `theme.tokens.css` yet, add it there first.
4. Direction-aware: use `inline-size` / `inset-inline-*` instead of `width` / `left`.
5. Add a line to this catalog with the right status.
6. If the component changes a convention, add a row to `docs/decisions/DECISIONS_LOG.md`.
