# AI prompts — `/admin/ai/prompts`

| | |
|--|--|
| **Route** | `/admin/ai/prompts` |
| **Audience** | Administrator |
| **Auth** | `[RequirePermission(PermissionCatalog.AiPrompts.View)]` (page) + per-action permission at the API (`AiPrompts.Create` / `.Edit` / `.Delete` / `.Test` / `.Export` / `.Import`) + `RequireApprovedAccount`. Create/Update/Delete sit behind the per-IP `auth` rate-limit; Test sits behind the per-admin `ai-test` limiter (D-179). |
| **Pattern** | D-176 (gap doc G12) AI-prompt catalogue + D-353 centralized CRUD framing (`CrudShell` + Add/Edit/View-Delete forms) + D-356 grid Excel export/import. `SimfDataGrid`-based list. |
| **Status** | ✅ Real (D-176; D-353 framing; D-356 Excel) |
| **Backend endpoints** | BFF `/account/api/admin/ai/prompts/*` → API `/api/v1/admin/ai/prompts/*`: `POST .../list`, `GET .../{id}`, `POST .../{id}/history/list`, `POST ...` (create), `PUT .../{id}`, `DELETE .../{id}`, `POST .../{id}/test`, `POST .../export`, `POST .../import` |
| **Source** | [`AiPromptsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/AiPromptsList.razor), [`AiPromptsAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/AiPromptsAddEdit.razor), [`AiPromptsViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/AiPromptsViewDelete.razor), [`AiPromptAdminEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/AiPromptAdminEndpoints.cs), [`AiPromptsExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/AiPromptsExcelEndpoints.cs), [`AdminAiPromptService.cs`](../../../src/Backend/SIMF.Infrastructure/Ai/AdminAiPromptService.cs) |
| **Tests** | [`docs/tests/e2e/cp-admin-ai-prompts.md`](../../tests/e2e/cp-admin-ai-prompts.md) |
| **Last reviewed** | 2026-06-11 |

## 1. Purpose

The single place to manage every AI prompt the platform uses, dynamically and
without a redeploy (D-176, gap doc G12). Each prompt carries a stable kebab-case
**Key**, an `AiFeature` it belongs to (`QuestionFilter`, `Faq`, `Assistance`,
`Translate`, `LiveTranslation`, `LiveSignLanguage`, `SessionSummary`), a bilingual
display name, an `AiProvider` (`Echo`, `OpenAi`, `AzureOpenAi`, `Anthropic`) +
model id, a System prompt, a User prompt template, generation parameters
(Temperature, Max output tokens), an active flag, and a monotonic `Version` that
the service bumps on every edit. `Echo` is the deterministic offline provider used
by dev + tests; in this build only `OpenAi` has a working outbound implementation.

The page also exposes a per-row **Test** (dry-run) action that renders the prompt
against ad-hoc inputs and shows the output, latency and token counts, and a
**history** read (`POST .../{id}/history/list`, D-188) that returns one
`GridPage` of the append-only pre-mutation snapshots used for drift detection / SOC reconstruction.

## 4. UI

- `SimfBanner` titled `Admin.AiPrompts.Title`, then a `SimfDataGrid` of
  `AdminAiPromptSummary` inside `.simf-page-wide` / `.simf-surface`.
- Grid columns: **Key** (`<code>`, sortable, filterable), **Feature** (sortable,
  filterable), **Display name** (sortable, filterable), **Provider** (sortable),
  **Model**, **Version** (`v{n}`, sortable), **Active** (`SimfPill` on/off).
- Multiselect grid (`RowKey` = id, `RowLabel` = Key). Canonical toolbar actions
  wired: **Add**, per-row **Edit**, **Details**, **Delete**, **Export**, **Import**.
- Per-row quiet **Test** action (`flask` icon `SimfToolbarButton`) in `RowActions`,
  wrapped in `<AuthorizedAction Permission="AiPrompts.Test">`. Clicking it opens a
  standalone `SimfModal` (not a CRUD step) with an inputs textarea (`key=value`,
  one per line) and, after Run test, a description list of Output / Latency / Tokens.
- `EmptyTemplate` renders `SimfEmptyState` titled `Admin.AiPrompts.None`.
- Toasts surface via a top-of-surface `SimfAlert` (`_toast`), success on save /
  delete / import, error on load / API failure.
- **Page ↔ Popup presentation toggle (D-353):** `AiPromptsList` renders a
  `CrudPresentationToggle` in the grid's `CustomToolbar` slot, bound to
  `_presentation` with `PageKey = "ai-prompts"`. The choice is read on init via
  `Prefs.GetPresentationAsync("ai-prompts")` and persisted by `CpPreferences`
  (localStorage). When the presentation is `Page`, `GridHidden` becomes true so the
  banner + grid leave the DOM while the form takes over the content area; when
  `Dialog`, Add/Edit/View/Delete open inside a `CrudShell` popup.
- **Excel export + import (D-356):** Export (`OnExportAsync`) calls
  `CrudGridExcel.ExportAsync(selectedIds, _query)` → `POST .../export`; with no rows
  selected it sends an empty `Ids` list + the current `GridQuery` (whole filtered
  grid), else only the ticked ids. The download file prefix is `simf-ai-prompts` and
  the sheet is named `AiPrompts`. Import (`OnImportAsync`) calls
  `CrudGridExcel.TriggerImportAsync()` (hidden `.xlsx` file input, Resource
  `ai/prompts`) → `POST .../import`; success raises `OnImported` →
  `Grid.Import.Done` toast + grid reload, failure raises `OnError` → red toast.

## 4.5 Form fields

CRUD is hosted by `CrudShell`, framing `AiPromptsAddEdit` (Add/Edit) or
`AiPromptsViewDelete` (Details/Deactivate). Add/Edit fields and their **server-side**
limits (from `AdminAiPromptService` validation):

| Field | Required | Limit / rule | Notes |
|-------|----------|--------------|-------|
| Key | yes (create only) | 2–64 chars, kebab-case (`a-z`, `0-9`, `-`) | Immutable once written — the field is `Disabled` in edit mode and `UpdateAiPromptRequest` has no `Key` |
| Feature | yes | `AiFeature` enum (`<select>`) | — |
| Display name (English) | yes | 1–128 chars | — |
| Display name (Arabic) | yes | 1–128 chars | — |
| Description (English) | no | 1–512 chars when present | nullable |
| Description (Arabic) | no | 1–512 chars when present | nullable |
| Provider | yes | `AiProvider` enum (`<select>`) | defaults to `Echo` |
| Model | yes | 1–64 chars | defaults to `echo` |
| System prompt | yes | 1–8000 chars | textarea (rows 6) |
| User prompt template | yes | 1–8000 chars | textarea (rows 4) |
| Temperature | yes | 0–2 (clamped/validated; `NaN`/out-of-range → 400) | number, step 0.1; default 0.2 |
| Max output tokens | yes | 1–8000 | number; default 512 |
| Active | (edit only) | bool | checkbox; create always sets `IsActive = true`, `Version = 1` |

The form view-model is named `FormModel` (not `Model`) to avoid clashing with its
own `Model` property (CS0542). Client-side it guards a blank Key (create) and blank
display names before posting; the authoritative checks are server-side.

## 5. Data flow + endpoints

All calls go through the BFF JS proxy (`simfAccount.postJson` / `getJson` /
`putJson` / `deleteJson`) at `/account/api/admin/ai/prompts/*`, which forwards to
the API at `/api/v1/admin/ai/prompts/*`. Each returns the `ApiResult<T>` envelope.

| Action | BFF route | API endpoint | Permission |
|--------|-----------|--------------|------------|
| List grid | `POST /account/api/admin/ai/prompts/list` | `POST /api/v1/admin/ai/prompts/list` | `AiPrompts.View` |
| Get detail | `GET .../{id}` | `GET .../{id}` | `AiPrompts.View` |
| Edit history | `POST .../{id}/history/list` | `POST .../{id}/history/list` | `AiPrompts.View` (+ `auth` limit, D-188) |
| Create | `POST .../` | `POST .../` | `AiPrompts.Create` (+ `auth` limit) |
| Update | `PUT .../{id}` | `PUT .../{id}` | `AiPrompts.Edit` (+ `auth` limit) |
| Deactivate | `DELETE .../{id}` | `DELETE .../{id}` | `AiPrompts.Delete` (+ `auth` limit) |
| Test (dry-run) | `POST .../{id}/test` | `POST .../{id}/test` | `AiPrompts.Test` (+ `ai-test` limit, D-179) |
| Excel export | `POST .../export` | `POST .../export` | `AiPrompts.Export` |
| Excel import | `POST .../import` | `POST .../import` | `AiPrompts.Import` |

- **List** returns `GridPage<AdminAiPromptSummary>`; the grid carries the light
  summary (no SystemPrompt / UserPromptTemplate / Description). Edit / Details /
  Delete first call `GET .../{id}` to load the full `AdminAiPromptDetail`.
- **Create** bumps nothing (Version starts at 1); **Update** snapshots the
  pre-mutation row into `AiPromptHistory` (D-188), bumps `Version`, and audits with
  `contentHashOld` / `contentHashNew` / `contentChanged`. **Deactivate** is a soft
  `IsActive = false` and is idempotent (early-returns when already inactive).
- **Export** sheet `AiPrompts` columns: `Key, Feature, DisplayName,
  DisplayNameArabic, Provider, Model, Temperature, MaxOutputTokens, Version,
  IsActive`. **Import** (insert-only) required headers: `Key, Feature, DisplayName,
  DisplayNameArabic, Provider, Model, SystemPrompt, UserPromptTemplate` (RowKey =
  `Key`); optional `Description`, `DescriptionArabic`, `Temperature`,
  `MaxOutputTokens` are read when present. `SystemPrompt` + `UserPromptTemplate` are
  not on the light grid, so they are import-only columns the admin fills before
  re-uploading.

## 6. Validation + error handling

Server validation lives in `AdminAiPromptService` (`ValidateCreate` /
`ValidateUpdate` / `ValidateText` / `ClampTemperature` / `ClampMaxTokens`). Error
codes (from `ErrorCodes`):

- **`AI_PROMPT_INVALID` (400)** — bad Key (not 2–64 kebab-case), a text field out of
  its length band (the message names the field and band, e.g. "DisplayName must be
  between 1 and 128 characters."), Temperature outside 0–2, or MaxOutputTokens
  outside 1–8000. All messages are bilingual (EN + AR).
- **`AI_PROMPT_KEY_DUPLICATE` (409)** — Key already in use; the message surfaces the
  offending key (EN + AR).
- **`AI_PROMPT_NOT_FOUND` (404)** — `GET .../{id}` for an unknown id.
- **`AI_PROVIDER_NOT_CONFIGURED` (503)** — Test against a provider with no key
  configured (e.g. `OpenAi` in default dev posture).
- Related module codes also defined: `AI_PROVIDER_FAILED`, `AI_INPUT_INVALID`.

**Import** runs each row through `CreateAiPromptRequest` + the same service
validation; a duplicate Key or an invalid/unrecognised `Feature`/`Provider`/blank
required cell throws a `DataValidationException` (bilingual) recorded as a **per-row
error**, not a batch abort. A non-`.xlsx` / wrong-sheet / oversized upload is
rejected up-front by the shared import base (HTTP 400, bilingual). On the CP, all
API failures surface `ApiResult.Error.MessageForCurrentCulture()`, falling back to
`Admin.AiPrompts.LoadFailed` when no message is present.

## 7. Edge cases

- **Key is immutable.** Editable only on create; disabled in edit mode and absent
  from the update contract — there is no rename path.
- **Deactivate is idempotent** and soft — the row stays in the grid with the "off"
  pill; a second delete writes no further audit row (service early-return).
- **Test never persists a prompt** — it dry-runs the live row's content against the
  supplied inputs; the inputs textarea is parsed line-by-line on `=` and blank /
  malformed lines are skipped.
- **Echo determinism** — `Echo` makes no outbound call, so Test output is
  deterministic; `OpenAi`/`AzureOpenAi`/`Anthropic` route outbound and only `OpenAi`
  is wired in this build.
- **Light vs full payload** — the grid summary omits the long prompt text, so every
  Edit/Details/Delete pays one extra `GET .../{id}`; a failed load surfaces a toast
  and aborts opening the form.
- **History** is append-only and **server-paged** on the shared grid seam. Its one
  declared column key is `version`; default order is `version` descending (newest
  snapshot first), page size falls back to 20 and is capped at 50, so the modal
  pages a long-lived prompt's snapshots rather than fetching all of them.

## 8. i18n + RTL

All strings come from `Admin.AiPrompts.*` resx keys (e.g. `Title`, `None`,
`Loading`, `LoadFailed`, `Saved`, `Deleted`, `Required`, `Save`, `Cancel`, the
`Col.*` headers, the `Field.*` form labels, `Active.Yes` / `Active.No`, and the
`Test.*` / `Delete.*` / `Add.*` / `Edit.*` / `Details.*` titles) plus the shared
`Grid.*` keys for toolbar/pager labels. The exact resx literals are not reproduced
here; descriptively, headers read Key / Feature / Display name / Provider / Model /
Version / Active, the toggle reads "Open as full page" ↔ "Open as dialog", and the
Arabic locale mirrors the whole page (banner, toolbar, grid, modals) to RTL with the
Arabic equivalents. Server error messages are themselves bilingual (EN + AR).

## 10. Use cases

- Create a prompt for a feature, dry-run it against sample inputs, then activate it.
- Edit an existing prompt (Version bumps; the prior version is snapshotted to
  history) and toggle it active/inactive.
- Bulk-seed prompts from a spreadsheet (Import) and export the catalogue (Export).
- Investigate AI behaviour: read a prompt's edit history (D-188) and correlate with
  the sibling AI invocations log (`/admin/ai/invocations`).

## 11. E2E

See [`docs/tests/e2e/cp-admin-ai-prompts.md`](../../tests/e2e/cp-admin-ai-prompts.md):
E2E-AIP-001 golden round-trip, 002 empty, 003 auth gate, 004–006 validation
(`AI_PROMPT_INVALID`), 007 duplicate key (`AI_PROMPT_KEY_DUPLICATE`), 008 edit
(Key immutable), 009 Echo dry-run, 010 unconfigured provider
(`AI_PROVIDER_NOT_CONFIGURED`), 011 soft-delete, 012 server-500, 013 RTL, 014 pager,
015 column filter, 016 column sort, 017 presentation toggle (D-353), 018 full-page
round-trip (D-353), 019 SimfConfirm delete gate (D-353), 020 Excel export (D-356),
021 Excel import (D-356), 022 Excel import rejection (D-356). Lower-layer API
coverage lives in `tests/SIMF.Api.Tests/AiModuleTests.cs`,
`AiHardeningTests.cs`, and (Excel) `AiPromptsExcelTests.cs`.

## 12. Related docs

- Sibling page: AI invocations log `/admin/ai/invocations` (`AiInvocations.View`) —
  its own reference doc + catalogue.
- Permissions: `src/Shared/SIMF.Common/PermissionCatalog.cs` (`AiPrompts` nested
  class) + `docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`.
- Decisions: D-176 (AI prompt catalogue / gap G12), D-179 (hardening: `ai-test`
  limiter + audit redaction), D-188 (edit history), D-353 (CrudShell framing +
  presentation toggle), D-356 (grid Excel export/import).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-11 | D-356 / D-353 / D-176 | Reference doc backfill — authored from live source (`AiPromptsList` + `AiPromptsAddEdit`/`AiPromptsViewDelete` + `AiPromptAdminEndpoints` + `AiPromptsExcelEndpoints` + `AdminAiPromptService` + `PermissionCatalog.AiPrompts`). Documents the D-356 Excel export/import (sheet `AiPrompts`; import RowKey `Key`; per-row errors; 400 on bad upload) and the D-353 Page⇄Popup `CrudPresentationToggle` + `CrudShell`-hosted CRUD with the `SimfConfirm`-gated Deactivate. |

_Last reviewed:_ 2026-06-11 by Claude (D-356 ref-doc backfill).
