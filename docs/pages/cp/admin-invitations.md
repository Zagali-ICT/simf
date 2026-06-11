# Invitations desk — `/admin/invitations`

| | |
|--|--|
| **Route** | `/admin/invitations` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel |
| **Audience** | Public Relations administrators (and Administrator via the `"*"` wildcard) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Invitations.View)]` on the page; mutations gated by `Invitations.Manage` + `RequireApprovedAccount` + `RequireRateLimiting("auth")`; export gated by `Invitations.Export` |
| **Pattern** | D-168 PR invitation desk · D-256 `SimfDataGrid` migration · **D-353** CrudShell popup/full-page framing + SimfConfirm delete gate · **D-356** Excel export (export only) |
| **Status** | Real (D-356 Phase 5, 2026-06-10) |
| **Implements use case(s)** | Invitation create / state-settle / cancel (SIMF gap doc G5, PDF §2.7.3) |
| **Backend endpoints** | `POST /account/api/admin/invitations/list`, `GET /account/api/admin/invitations/{id}`, `POST /account/api/admin/invitations`, `PUT /account/api/admin/invitations/{id}`, `DELETE /account/api/admin/invitations/{id}`, `POST /account/api/admin/invitations/export` |
| **Source file** | [`InvitationsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/InvitationsList.razor), [`InvitationsAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/InvitationsAddEdit.razor), [`InvitationsViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/InvitationsViewDelete.razor) |
| **Backend** | [`InvitationEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/InvitationEndpoints.cs), [`InvitationsExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/InvitationsExcelEndpoints.cs), [`AdminInvitationService.cs`](../../../src/Backend/SIMF.Infrastructure/PublicRelations/AdminInvitationService.cs) |
| **Backed by** | `dbo.Invitations` table (migration `AddInvitations`, 2026-05-29; FK to `UserProfile` on the App DB) |
| **Tests** | [`docs/tests/e2e/cp-admin-invitations.md`](../../tests/e2e/cp-admin-invitations.md); API: [`AdminInvitationsTests.cs`](../../../tests/SIMF.Api.Tests/AdminInvitationsTests.cs), [`InvitationsExcelTests.cs`](../../../tests/SIMF.Api.Tests/InvitationsExcelTests.cs) |
| **Last reviewed** | 2026-06-10 |

---

## 1. Purpose

The invitations desk is the Public Relations team's outreach register. A PR rep
sends an invitation to a person already on the system as a `UserProfile`
(typically a VIP), records the recipient's settled response (Pending →
Confirmed / Declined), and cancels (soft-deletes) an invitation that is no longer
relevant. Each row projects the recipient's bilingual name, profile type, the
state, who sent it, and when. Creating an invitation also fires a best-effort
in-app notification (`NotificationKind.InvitationReceived`) to the recipient.
Since D-356, the whole filtered grid (or a hand-picked selection) can be exported
to an Excel workbook for offline reporting.

## 2. Audience + permissions

- **Who can reach it:** any admin whose role grants `Invitations.View`. The
  **PublicRelations** role holds it as a seeded baseline grant; **Administrator**
  reaches it through the `"*"` wildcard.
- **Who can edit/write on it:** holders of `Invitations.Manage` (PublicRelations
  baseline + Administrator wildcard). Create / update / delete are additionally
  gated by `RequireApprovedAccount` and the `"auth"` rate-limit policy.
- **Who can export:** holders of `Invitations.Export` (PublicRelations baseline +
  Administrator wildcard).
- **Authorisation gates:** page — `[RequirePermission(PermissionCatalog.Invitations.View)]`;
  API — `Policies(PermissionCatalog.PolicyFor(Invitations.View|Manage|Export), RequireApprovedAccount)`.
  The BFF forwards the `access_token` JWT to the API, which enforces the permission.
- **What an unauthenticated / under-privileged user sees:** an admin who lacks
  `Invitations.View` is redirected to `/not-permitted` (HTTP 200) by the
  `RequirePermission` attribute; no `/list` request fires.

## 3. Screenshots

| State | File | Captured |
|-------|------|----------|
| Default (with rows) | `docs/screenshots/cp-admin-invitations-default.png` | _pending_ |
| Empty state | `docs/screenshots/cp-admin-invitations-empty.png` | _pending_ |
| Add (Send) form | `docs/screenshots/cp-admin-invitations-add-modal.png` | _pending_ |
| Edit form | `docs/screenshots/cp-admin-invitations-edit-modal.png` | _pending_ |
| Details (View) form | `docs/screenshots/cp-admin-invitations-details-modal.png` | _pending_ |
| Delete confirm (SimfConfirm) | `docs/screenshots/cp-admin-invitations-delete-confirm.png` | _pending_ |
| Full-page presentation | `docs/screenshots/cp-admin-invitations-fullpage.png` | _pending_ |
| RTL (Arabic) | `docs/screenshots/cp-admin-invitations-rtl.png` | _pending_ |

## 4. UI affordances

### 4.1 Banner / page header

`SimfBanner` titled `Admin.Invitations.Title` ("Invitations" / "الدعوات"). The
banner + grid are hidden whenever a form is open **in full-page presentation**
(`GridHidden = FormOpen && _presentation == Page`); in dialog presentation they
stay behind the popup.

### 4.2 Toolbar

| Control | Wired callback | Calls | Notes |
|---------|----------------|-------|-------|
| Select all / row checkboxes | `Multiselect="true"` | — | drives the selected-ids export |
| New invitation (Add) | `OnAdd` → `OnAddAsync` | opens `InvitationsAddEdit` (IsEdit=false) | label `Admin.Invitations.New` |
| Edit (per-row pencil) | `OnEditOne` → `OnEditAsync` | `GET .../{id}` then opens `InvitationsAddEdit` (IsEdit=true) | re-fetches the full detail first |
| Details (per-row) | `OnDetailsOne` → `OnDetailsAsync` | `GET .../{id}` then opens `InvitationsViewDelete` (IsDelete=false) | read-only |
| Cancel invitation (per-row trash) | `OnDeleteOne` → `OnDeleteAsync` | `GET .../{id}` then opens `InvitationsViewDelete` (IsDelete=true) | SimfConfirm gates the `DELETE` |
| Export | `OnExport` → `OnExportAsync` | `POST .../export` (download) | **export only** — no Import is wired |
| **Presentation toggle** (`CustomToolbar`) | `CrudPresentationToggle PageKey="invitations"` `@bind-Value="_presentation"` | localStorage | **D-353** — Page ↔ Dialog, persisted via `CpPreferences` |

There is **no Import** control — invitations are created/edited from the forms,
so `OnImport` is not wired and no `<input>` is rendered.

### 4.3 Grid columns

| Column | Key | Source field | Sortable | Filterable | Notes |
|--------|-----|--------------|----------|------------|-------|
| Recipient | `recipient` | `RecipientLabel(r)` | no | no | shows Arabic name when culture is `ar`, else English |
| Profile type | `profileType` | `RecipientProfileTypeName` | no | no | "—" when null |
| State | `state` | `State` | yes | yes | `SimfPill` (Confirmed=on, Declined=off, Pending=admin) |
| Sent | `createdat` | `CreatedAt` | yes | no | `yyyy-MM-dd HH:mm 'UTC'` |
| Sent by | `sentBy` | `SentByDisplayName` | no | no | sender display name resolved from Identity DB |
| Active | `active` | `IsActive` | no | no | Active / Inactive pill |

`Filterable="true"` is set on **State** only; `Sortable="true"` on **State** +
**Sent**. The service also honours an `isActive` filter, but **no UI control
drives it**. Default order = `CreatedAt` descending.

### 4.4 Pager

- Prev / Next / First / Last + a page-size selector and a summary line.
- Page size: `_query = new() { Top = 20 }`. The service clamps `Top` to 1–200
  (default 25 when ≤ 0).

### 4.5 Form fields

The reusable forms are hosted by `<CrudShell Presentation="_presentation">` —
a popup by default, or a full page when the toggle is set.

**`InvitationsAddEdit`** — Add (Send) branch (`IsEdit=false`):

| Field | Type | Required | MaxLength | Validation | Locale |
|-------|------|----------|-----------|------------|--------|
| Recipient (UserProfile id) | text (GUID) | yes | n/a | `Guid.TryParse` client-side; profile must exist server-side | `Admin.Invitations.Field.Recipient` |
| Notes | text | no | 1000 | length-gated server-side | `Admin.Invitations.Field.Notes` |

**`InvitationsAddEdit`** — Edit branch (`IsEdit=true`): the Recipient field is
replaced by a **State** `SimfSelect` (Pending / Confirmed / Declined, from the
`InvitationState` enum) plus Notes. State is system-defaulted to Pending on
create, so it never appears on the Add branch; Recipient + SentBy are fixed once
sent, so they never appear on the Edit branch.

**`InvitationsViewDelete`** — read-only `dl` (recipient EN/AR name, profile type,
job title, email, state, notes, sent-by, created / responded / updated
timestamps, active flag). When `IsDelete=true`, a danger "Cancel invitation"
button opens the `SimfConfirm` gate.

## 5. Data flow

```
Admin action → InvitationsList / InvitationsAddEdit / InvitationsViewDelete
            → simfAccount.{post,get,put,delete,downloadXlsx} (BFF /account/api/...)
            → API /admin/invitations/* (FastEndpoints) → AdminInvitationService
            → SimfAppDbContext (Invitations + UserProfiles) + SimfIdentityDbContext (Users, read-only merge)
            → ApiResult<T> envelope → grid reload + toast
```

| When | Method + path (BFF → API) | Request body | Response shape |
|------|---------------------------|--------------|----------------|
| OnInit / query change | `POST /account/api/admin/invitations/list` | `GridQuery` | `ApiResult<GridPage<AdminInvitationSummary>>` |
| Edit / Details / Delete open | `GET /account/api/admin/invitations/{id}` | — | `ApiResult<AdminInvitationDetail>` |
| Add submit | `POST /account/api/admin/invitations` | `AdminCreateInvitationRequest` (`SentToUserProfileId`, `Notes`) | `ApiResult<AdminInvitationDetail>` |
| Edit submit | `PUT /account/api/admin/invitations/{id}` | `AdminUpdateInvitationRequest` (`State`, `Notes`) | `ApiResult<AdminInvitationDetail>` |
| Delete confirm | `DELETE /account/api/admin/invitations/{id}` | — | `ApiResult<bool>` (`Ok(true)`) |
| Export | `POST /account/api/admin/invitations/export` | `AdminGridExportRequest` (`Ids`, `Query`) | XLSX bytes (`attachment; filename="simf-invitations-{ts}.xlsx"`) |

**Export semantics:** `OnExportAsync` sends the selected row ids; when no rows
are selected it sends `Query = _query` (whole filtered grid) and an empty `Ids`
list. The endpoint (`ExportInvitationsEndpoint`) resets `Skip` to 0 and caps
`Top` at **5000** rows, lists via the same `AdminInvitationService.ListAllAsync`,
filters to the selected ids when supplied, and renders the "Invitations" sheet.

**Export column order (workbook header row):**
`RecipientEnglishName | RecipientArabicName | ProfileType | Email | State |
Notes | SentBy | CreatedAt | RespondedAt | IsActive`.

## 6. Validation + error handling

- **Client-side guards:** the Add branch rejects a non-GUID Recipient
  (`Guid.TryParse`) and shows `Admin.Invitations.RecipientRequired` in a
  `SimfAlert` without firing a POST. `NullIfBlank` trims Notes to `null`.
- **Server-side validation** (`AdminInvitationService`):
  - `ValidateNotes` — Notes > 1000 chars → **400 `INVITATION_INVALID`**
    ("Invitation notes cannot exceed 1000 characters." / "لا يمكن أن تتجاوز ملاحظات الدعوة 1000 حرف.").
  - Recipient profile must exist → else **400 `INVITATION_TARGET_NOT_FOUND`**.
  - Moving a settled invitation back to Pending → **400 `INVITATION_STATE_INVALID`**.
  - Unknown invitation id on GET / PUT / DELETE → **404 `INVITATION_NOT_FOUND`**.
- **Error envelope:** standard `ApiResult<T>.Error` with `Code` (from
  `ErrorCodes`) + bilingual `Message` / `MessageArabic`; the UI surfaces
  `MessageForCurrentCulture()`.
- **Toast strategy:** success on save → `Admin.Invitations.Saved`; success on
  delete → `Admin.Invitations.Deactivated` ("Invitation cancelled." / "تم إلغاء الدعوة.");
  load failure → `Admin.Invitations.LoadFailed`; generic form failure →
  `Admin.Invitations.Fallback`. The shared import-done key
  (`Grid.Import.Done`) is **not used here** (export only).

## 7. Edge cases + known limitations

- **Delete is idempotent.** `DeactivateAsync` returns without writing when the
  invitation is already inactive (`if (!invitation.IsActive) return;`).
- **Settled-state correction.** A Confirmed → Declined change is allowed and
  re-stamps `RespondedAt`; only a settled → Pending move is rejected.
- **Notification is best-effort.** `notificationDispatcher.TryDispatchAsync`
  swallows failures so the API still returns 200 if the notification subsystem
  is down.
- **Cross-DB read merge.** Recipient email + sender display name come from the
  Identity DB via an in-memory merge (no cross-DB FK, per D-157); the invitation
  → `UserProfile` FK is intra-App.
- **Export cap.** Whole-grid export is capped at 5000 rows; a larger filtered set
  is truncated to the first 5000 in the service order (CreatedAt desc).
- **Export-only by design.** There is intentionally no import path — invitations
  are authored from the CP forms, so the page wires no `OnImport`.
- **i18n parity gap (noted, not fixed here).** Several `Admin.Invitations.*` keys
  the D-353 forms reference (`Saved`, `Fallback`, `RecipientRequired`,
  `Delete.Title`, `Delete.Message`, `Details.Title`, `Details.Close`, the
  `Col.RecipientEn/Ar/JobTitle/Email/RespondedAt/UpdatedAt`,
  `New.Submitting`, `Edit.Submitting`) exist in `Strings.ar.resx` but are
  **missing from the English `Strings.resx`** — English would fall back to the
  key name. This is a resx parity defect to be fixed separately (out of scope
  for this doc).

## 8. i18n + RTL

- All visible strings come from `Strings.resx` (EN) + `Strings.ar.resx` (AR) via
  `IStringLocalizer<Strings> L`, under the `Admin.Invitations.*` keyspace (plus
  shared `Grid.*` keys for the toolbar/pager and `Admin.Invitations.Export` via
  the `Grid.Export` label).
- `RecipientLabel` picks the recipient's Arabic name when
  `CurrentUICulture.TwoLetterISOLanguageName == "ar"`, else the English name.
- RTL: `<html dir="rtl" lang="ar">`; the banner title, grid headers, toolbar
  toggle, and form fields mirror.
- See the i18n parity gap noted in §7.

## 9. Accessibility

- Keyboard: the `CrudShell` form (popup or full page) takes focus on open and
  returns it to the grid on close; `SimfConfirm` requires an explicit
  confirm/cancel (no backdrop dismissal).
- Screen reader: `SimfDataGrid` carries `Caption="@L[\"Admin.Invitations.Title\"]"`
  plus per-row select / action labels.
- Colour contrast + focus: WCAG AA via `theme.tokens.css`; `--focus-ring` token.

## 10. Related use cases (UCS-001)

| UC ID | Title | Notes |
|-------|-------|-------|
| (G5 / PDF §2.7.3) | PR invitation desk | UCS entries pending the UCS expansion; this page implements send / settle-state / cancel + export |

## 11. Related E2E test scenarios

| Scenario | File | Coverage |
|----------|------|----------|
| Golden round-trip — Send → confirm → cancel | [`cp-admin-invitations.md` E2E-INV-001](../../tests/e2e/cp-admin-invitations.md) | create / state change / soft delete |
| Send / Edit / Cancel | E2E-INV-002..004 | per-action happy paths |
| Empty + auth + validation + 500 + RTL | E2E-INV-005..012 | empty state, `/not-permitted`, error codes, fallback toast, RTL |
| Grid filter + sort | E2E-INV-013..014 | State filter, State/Sent sort |
| **Presentation toggle persists (D-353)** | E2E-INV-015 | localStorage `simf.cp.prefs.invitations` |
| **Full-page round-trip (D-353)** | E2E-INV-016 | CrudShell Page presentation |
| **Delete confirmation gate (D-353)** | E2E-INV-017 | ViewDelete + SimfConfirm |
| **Excel export (D-356)** | E2E-INV-018 | whole grid vs selected rows; sheet header |

## 12. Related docs

- E2E catalogue: [`docs/tests/e2e/cp-admin-invitations.md`](../../tests/e2e/cp-admin-invitations.md).
- Pattern doc: [`SIMF_TABLE_PATTERN.md`](../../dev/SIMF_TABLE_PATTERN.md).
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) — `ApiResult<T>` envelope + error model.
- Decisions: D-168 (invitation desk), D-256 (`SimfDataGrid`), D-353 (CrudShell popup/full-page + SimfConfirm delete), D-356 (uniform CRUD Excel — export only here).
- Source: [`InvitationsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/InvitationsList.razor).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-29 | D-168 | Original invitation desk (inline Send + Edit modals; trash soft-delete). |
| 2026-06-03 | D-256 | Migrated to `SimfDataGrid` (per-column filter/sort + pager). |
| 2026-06-10 | D-353 | Add/Edit/View/Delete moved into `CrudShell` + reusable `InvitationsAddEdit` / `InvitationsViewDelete` forms; popup ↔ full-page toggle (`CrudPresentationToggle`, persisted); delete gated by `SimfConfirm`. |
| 2026-06-10 | D-356 | Excel **export** added (`ExportInvitationsEndpoint`, `Invitations.Export` permission, "Invitations" sheet, 5000-row cap). No import — export only. Reference doc created. |

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5).
