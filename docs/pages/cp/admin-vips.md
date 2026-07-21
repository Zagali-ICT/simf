# VIPs — `/admin/vips`

| | |
|--|--|
| **Route** | `/admin/vips` |
| **Audience** | Public Relations (baseline role `PublicRelations`; Administrator `"*"` always passes) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Vips.View)]` (CP page); API endpoints `Policies(PermissionCatalog.PolicyFor(Vips.View / Vips.Notify / Vips.Export), RequireApprovedAccount)`; the notify + export endpoints additionally `RequireRateLimiting("auth")`. **New VIP** + **row Edit** are UX-gated in the page by `Visitors.RegisterOnsite` / `Visitors.Edit` respectively (the reused admin endpoints enforce the same policies). |
| **Pattern** | D-168 (gap doc G5, PDF §2.7.3). Derived-list `SimfDataGrid` (D-256) + three mutating flows: bulk-notify, **New VIP** (nav to `/admin/visitors/vip`), and **row Edit** (shared `EditAccountForm` modal). |
| **Status** | ✅ Real (D-168; VIP edit added 2026-07-21) |
| **Backend endpoints** | BFF `/account/api/admin/vips/list` → API `POST /admin/vips/list`; BFF `/account/api/admin/vips/notify` → API `POST /admin/vips/notify`; BFF `/account/api/admin/vips/export` → API `POST /admin/vips/export` (D-356, export-only). **Row Edit reuses the existing visitor admin endpoints** (no new endpoint): `PUT /admin/visitors/{id}` (email/name/tier), `POST /admin/visitors/{id}/avatar`, `/id-document`, `/vip-photo` — all gated `Visitors.Edit`. |
| **Source** | [`VipsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/VipsList.razor), [`EditAccountForm.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/EditAccountForm.razor) (shared edit form), [`VipEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/VipEndpoints.cs), [`VipsExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/VipsExcelEndpoints.cs), [`AdminInvitationService`](../../../src/Backend/SIMF.Infrastructure/PublicRelations/AdminInvitationService.cs) (`ListVipsAsync` / `NotifyVipsAsync`), [`AdminVipSummary`](../../../src/Shared/SIMF.Contracts/Admin/Invitations.cs) |
| **Backed by** | **No new table.** The VIP list is a derived view of `dbo.UserProfiles` — the rows whose `ProfileType.Name` is in `{VVIP, VIP, Gold}` (`VipProfileTypes.All`). Recipient email is resolved on read from the Identity DB (`SimfUser`). `AdminVipSummary.UserId` carries the account id that drives the row Edit. |
| **Tests** | [`docs/tests/e2e/cp-admin-vips.md`](../../tests/e2e/cp-admin-vips.md) |
| **Last reviewed** | 2026-07-21 |

## 1. Purpose

The Public Relations VIP desk per SIMF gap doc G5 (PDF §2.7.3) — the
single place the PR team views the forum's VIP guests and sends them a
coordinated bilingual broadcast. "VIP" is not a stored flag: it is the
subset of `UserProfiles` whose `ProfileType.Name` is one of `VVIP`,
`VIP`, or `Gold` (the server-side `VipProfileTypes.All` discriminator).
The membership is derived (a visitor becomes a VIP by holding a VIP tier),
but the desk supports three mutations: **bulk-notify**, **New VIP**
(navigate to the dedicated `/admin/visitors/vip` registration page), and
**row Edit** — change a VIP's name, email, tier, profile photo, ID image,
and VVIP/VIP welcome photo via the shared `EditAccountForm` (keyed by the
account id). Editing the tier is also how a VIP is promoted (VIP → VVIP)
or demoted out of the VIP set.

The page shares its auth surface and its backing service
(`AdminInvitationService`) with the PR invitation desk; both are owned by
the `PublicRelations` baseline role rather than `AdminOnly`.

## 4. UI

- `SimfBanner` titled from `Admin.Vips.Title`, inside the wide page surface.
- **Read-only `SimfDataGrid`** (D-256) with `Multiselect="true"` (row +
  Select-all checkboxes), the standard pager (First / Prev / numbered /
  Next / Last + page-size selector + "Showing …" summary), and a default
  query of `Top = 20`.
- Four columns: **Name** (bilingual projection — `EnglishName` /
  `ArabicName` by current UI culture), **Job title** (`JobTitle ?? "—"`),
  **Profile type** (bilingual — `ProfileTypeName` / `ProfileTypeNameArabic`),
  **Email** (`Email ?? "—"`).
- **Toolbar Add ("New VIP")** — shown only when the admin holds
  `Visitors.RegisterOnsite`; navigates to `/admin/visitors/vip`. **Per-row
  Edit (pencil)** — shown only when the admin holds `Visitors.Edit`; opens
  the "Edit VIP" modal (below). Both affordances are wired conditionally
  (`SimfDataGrid` renders an action only when its callback `HasDelegate`),
  so a `Vips.View`-only admin sees neither. No Details / row-delete slot.
  The grid columns do not set `Filterable`/`Sortable`, so the page renders
  neither a per-column filter row nor sortable headers; `/list` is fetched
  once per page change.
- **Edit VIP modal (`SimfModal`):** hosts the shared `EditAccountForm`
  with `AccountId=row.UserId`, `Scope="visitors"`, `IsVisitorScope=true`,
  `ShowVipPhoto=true`. Edits email + display name + tier (the profile-type
  dropdown), plus a **Photo & ID** section — profile photo, ID document,
  and VVIP/VIP welcome photo file inputs (each optional; leave empty to
  keep the current image). On Save the core fields PUT first, then each
  picked image uploads to its account-id-keyed endpoint. On success the
  modal closes with the `Admin.Vips.Edit.Saved` toast and the grid
  reloads; an image failure (e.g. the ID face-gate) keeps the modal open
  with the bilingual reason.
- **`CustomToolbar` bulk action — Notify selected (N):** a single
  send-icon `SimfToolbarButton` whose label is
  `string.Format(L["Admin.Vips.NotifySelected"], _selected.Count)`. It is
  `Disabled` while `_selected.Count == 0` and enables as rows are ticked.
- **Notify modal (`SimfModal`):** opened by the toolbar button. Four
  `SimfTextField`s — Title (English), Title (Arabic), Body (English),
  Body (Arabic) — all reset to blank each time the modal opens. Footer:
  **Send** (`Admin.Vips.Notify.Submit`, shows a busy/loading state) and
  **Cancel** (`Admin.Vips.Notify.Cancel`, closes without sending). On
  success the modal closes, the selection clears, and a success alert
  reports `Dispatched` + `EmailsEnqueued`.
- **Empty state:** `SimfEmptyState` (`Admin.Vips.None`) when no profile
  matches the discriminator.
- **Toast / alert:** a single `SimfAlert` (`error` or `success`) rendered
  above the grid for load failures and notify outcomes.
- **Excel export only (D-356):** the grid's built-in **Export** toolbar
  action (`OnExport` → `ExportLabel="Grid.Export"`) posts an
  `AdminGridExportRequest` to `/account/api/admin/vips/export` and
  downloads the workbook via the `simfAccount.downloadXlsx` JS helper.
  With rows selected it sends `Ids = [selected UserProfileIds], Query =
  null`; with no rows selected it sends an empty `Ids` list plus the
  current `_query` (the whole filtered grid). **There is no Import** — the
  VIP list is a derived view, so the BFF wires only `MapGridExport(group,
  "vips")` (export), not the export+import pair.

> **No CrudShell / presentation toggle / delete here.** The row Edit is
> hosted in a plain `SimfModal` (like the notify modal), not the D-353
> `CrudShell` + `CrudPresentationToggle`, and there is no delete form —
> removing a VIP means changing the tier out of the VIP set on the Edit
> modal, or deactivating the account from the Visitors desk.

## 4.5 Notify-modal fields

| Field | Required | Length guard (server) | Notes |
|-------|----------|-----------------------|-------|
| Title (English) | yes | 1–200 chars | trimmed server-side |
| Title (Arabic) | yes | 1–200 chars | trimmed server-side |
| Body (English) | yes | 1–2000 chars | trimmed server-side |
| Body (Arabic) | yes | 1–2000 chars | trimmed server-side |

The grid itself has no editable fields (read-only derived list).

## 5. Data flow + endpoints

- **List** — `VipsList.razor` posts `_query` (`GridQuery`) to the BFF
  `/account/api/admin/vips/list`, which forwards to API `POST
  /admin/vips/list` (`ListVipsEndpoint`, gated `Vips.View`). The service
  `ListVipsAsync` filters `UserProfiles` to `ProfileType.Name ∈ {VVIP,
  VIP, Gold}`, applies an optional `Search` (EF `Like` over `Name` /
  `NameArabic`) and optional sort on `name` / `profiletype` (default
  `OrderBy(Name)`), pages it (`Top` clamped 1–200, default 25), then
  resolves each recipient's email from the Identity DB by `UserId`
  (resolve-on-read, no cross-DB FK — D-157). Returns
  `GridPage<AdminVipSummary>`.
- **Notify** — Send posts `AdminNotifyVipsRequest` (`UserProfileIds`,
  `Title`, `TitleArabic`, `Body`, `BodyArabic`) to BFF
  `/account/api/admin/vips/notify` → API `POST /admin/vips/notify`
  (`NotifyVipsEndpoint`, gated `Vips.Notify` + `"auth"` rate limiter).
  The endpoint reads the actor id from the `sub` claim
  (401 if absent), then `NotifyVipsAsync` re-validates that each id is
  still a VIP, dispatches a `NotificationKind.VipBroadcast` in-app row per
  valid recipient (with email enqueued only when an address is on file),
  writes one `AuditEvents.VipNotificationSent` audit row, and returns
  `AdminNotifyVipsResult(Dispatched, EmailsEnqueued, SkippedProfileIds)`.
- **Export (D-356)** — BFF `/account/api/admin/vips/export` → API `POST
  /admin/vips/export` (`ExportVipsEndpoint : AdminGridExportEndpoint<AdminVipSummary>`,
  gated `Vips.Export` + `"auth"` rate limiter). It lists rows through the
  **same** `ListVipsAsync` service method, honours a selected-ids subset
  (else the whole filtered set), and renders the workbook.

## 6. Validation + error handling

- **Empty selection** — `ListVipsAsync` / `NotifyVipsAsync`: an empty
  `UserProfileIds` → HTTP 400 `VipNotifyEmpty` ("Select at least one
  VIP." / "اختر مستلماً واحداً على الأقل."). In the UI this path is
  normally blocked by the disabled toolbar button.
- **Over-batch** — more than 500 ids → HTTP 400 `VipNotifyTooLarge`
  ("Cannot dispatch to more than 500 VIPs in one batch." / "لا يمكن
  الإرسال إلى أكثر من 500 ضيف في دفعة واحدة.").
- **Title/body length** — title outside 1–200 (EN or AR) or body outside
  1–2000 (EN or AR) → HTTP 400 `InvitationInvalid` with the matching
  bilingual length message.
- **Non-VIP id in the selection** — silently skipped, not failed: the id
  is returned in `AdminNotifyVipsResult.SkippedProfileIds` and the audit
  `Detail` records `skipped=N`.
- **Notification dispatch is best-effort** — `TryDispatchAsync` swallows a
  failed dispatch so the PR rep still gets a 200 even if the notification
  subsystem is down.
- **List load failure** — a non-success envelope surfaces
  `Error.MessageForCurrentCulture()` (fallback `Admin.Vips.LoadFailed`) in
  the error alert; no rows render.
- **Export permission** — an admin lacking `Vips.Export` is rejected with
  HTTP 403 (`AdminGridExportEndpoint` policy) and no file is produced.

## 7. Edge cases + known limitations

- **VIP membership is derived, not stored.** Moving a profile in/out of
  `{VVIP, VIP, Gold}` (on the profile page) is what adds/removes a VIP row
  — there is no VIP record to manage here.
- **Recipient with no email** counts toward `Dispatched` (the in-app row
  is still created) but not toward `EmailsEnqueued`.
- **Re-validation on notify** — a profile that left the VIP set between
  list-load and Send is treated as a non-VIP and skipped, so a stale
  selection cannot broadcast to a non-VIP.
- **Whole-grid export cap** — `AdminGridExportEndpoint.MaxExportRows`
  caps the export at **5000** rows (`Skip` reset to 0, `Top` set to 5000).
- **Notify cap (500) vs export cap (5000)** differ deliberately — the
  notify batch is a smaller, rate-limited fan-out; the export is a
  read-only download.

## 8. i18n + RTL

`Admin.Vips.*` resx keys back the title, column headers, the
`NotifySelected` counter label, the notify-modal title/field labels/Send
+ Cancel, the empty-state, the loading label, and the success/failure
toasts; the grid chrome uses the shared `Grid.*` keys. The Name and
Profile-type columns swap to the Arabic projection
(`ArabicName` / `ProfileTypeNameArabic`) when the UI culture is `ar`. The
Arabic strings above are described from source comments/exceptions; the
exact resx phrasing is whatever ships in the `ar` resource files.

## 10. Use cases

- UC-VIP-LIST-001 (view + filter the VIP guest list), UC-VIP-NOTIFY-001
  (bulk-notify selected VIPs), UC-VIP-EXPORT-001 (export the VIP list to
  XLSX) — _UCS detail entries authored under the PR-desk UCS follow-up._

## 11. E2E

See [`docs/tests/e2e/cp-admin-vips.md`](../../tests/e2e/cp-admin-vips.md):
E2E-VIP-001 golden notify, 002 button gated by selection, 003 cancel
modal, 004 empty list, 005 auth (`Vips.View`), 006 auth (`Vips.Notify`),
007 blank title/body 400, 008 empty-selection guard, 009 over-batch
guard, 010 non-VIP skipped, 011 server 500 on list, 012 RTL, 013 Excel
export (D-356), 014 New VIP nav, 015 row Edit name/email/tier, 016 Edit
photo + welcome-photo upload, 017 ID face-gate validation, 018 Add/Edit
permission-gated, 019 RTL of the Edit modal.

## 12. Related docs

- Permission catalogue: `docs/SIMF-Permission-Catalogue.md` — `Vips.View`,
  `Vips.Notify`, `Vips.Export` (all baseline role `PublicRelations`).
- Auth + permissions guide: `docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`.
- Sibling PR-desk page: the invitation desk (same `AdminInvitationService`).
- Authority spec: SIMF gap doc G5 / PDF §2.7.3.
- Decisions: D-168 (VIP list + bulk-notify), D-256 (grid conversion),
  D-356 (Excel export).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-11 | D-168 | Original — VIP derived list (`{VVIP, VIP, Gold}`) + bilingual bulk-notify modal, gated by `Vips.View` / `Vips.Notify` (baseline role `PublicRelations`). |
| 2026-06-11 | D-356 | Excel **export only** added (toolbar Export → `/admin/vips/export`, gated `Vips.Export`, sheet "VIPs", header `EnglishName \| ArabicName \| JobTitle \| ProfileType \| ProfileTypeArabic \| Email`, 5000-row cap; no Import — derived list). E2E catalogue extended with E2E-VIP-013. |
| 2026-07-21 | VIP edit | **New VIP** (toolbar Add → `/admin/visitors/vip`, gated `Visitors.RegisterOnsite`) and **row Edit** added (per-row pencil → `SimfModal` hosting the shared `EditAccountForm`, gated `Visitors.Edit`; edits name/email/tier + a Photo & ID section for profile photo / ID image / VVIP-VIP welcome photo). Reuses the existing account-id-keyed admin endpoints — no new permission, endpoint, or migration. E2E extended with E2E-VIP-014..019. |

_Last reviewed:_ 2026-07-21 by Claude (VIP edit — New VIP nav + row Edit via the shared EditAccountForm with photo/ID/welcome-photo upload).
_Prior:_ 2026-06-11 (D-356 — VIP desk reference doc; Excel export-only, baseline role PublicRelations verified against source).
