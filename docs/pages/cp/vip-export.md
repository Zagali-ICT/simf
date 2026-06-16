# VIP welcome export — `/admin/visitors/vip/export`

| | |
|--|--|
| **Route** | `/admin/visitors/vip/export` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel |
| **Audience** | Administrator |
| **Auth** | `[RequirePermission(Visitors.ExportVip)]` + Approved |
| **Pattern** | D-429 (V-3). Read-only roster on the canonical `SimfDataGrid`; the CSV / Excel downloads + the JSON API are the export surface (toolbar links to the dedicated endpoints). |
| **Status** | ✅ Real |
| **Backend** | `POST /account/api/admin/visitors/vip/roster/list` (`GridPage` for the grid), `GET /account/api/admin/visitors/vip/roster` (JSON API for Mawj), `GET /account/api/admin/visitors/vip/roster/export?format=csv\|xlsx` (download), `GET /account/api/admin/visitors/{id}/vip-photo` (per-row photo) |
| **Source** | [`VipExport.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/VipExport.razor) · service [`VipRosterService.cs`](../../../src/Backend/SIMF.Infrastructure/Identity/VipRosterService.cs) · endpoints [`AdminVipRosterEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/AdminVipRosterEndpoints.cs) |
| **E2E** | [`cp-vip-export.md`](../../tests/e2e/cp-vip-export.md) (E2E-VIPX-001..008) |
| **Last reviewed** | 2026-06-15 |

## 1. Purpose

The **VVIP/VIP welcome roster** shared with the technical teams for the موج
(Mawj) welcome messages. The grid lists every visitor whose tier is **VVIP** or
**VIP** with their welcome data (Mawj ID, honorific, job title, preferred
language, tier, names, email, mobile, reference) and a **photo thumbnail** with
a per-row download. Non-VIP tiers (Normal / Staff / Media / Sponsor) never
appear.

Three export surfaces, all gated by `Visitors.ExportVip`:

1. **CSV** — `…/roster/export?format=csv` → `vip-welcome-roster.csv`
   (UTF-8 BOM so Excel opens the Arabic columns correctly).
2. **Excel** — `…/roster/export?format=xlsx` → `vip-welcome-roster.xlsx`
   (ClosedXML).
3. **JSON API** — `…/roster` → `ApiResult<VipRosterRow[]>`, the feed the Mawj
   integration consumes.

Export-only (us → teams); there is no Mawj import.

## 2. Security

Every exported free-text cell is run through
`ClosedXmlUserExcelService.SanitiseForExcel` (CWE-1236 CSV/formula-injection
guard) before it reaches the CSV or XLSX, since the file is shared outside the
system. The roster is a cross-DB read resolved on the server (D-157): VVIP/VIP
profiles on `SIMF_App`, the owners' email/state/name batched from
`SIMF_Identity`.

## 3. Data

Backed by the V-1 (D-429) additive `UserProfile` columns + the seeded VVIP/VIP
tiers. The roster is intentionally small (dozens of rows), so the grid's
`GridQuery` (search / sort / filter / page) is applied in memory over the full
roster.

## 4. Tests

Integration: `VipRosterTests.Roster_includes_vvip_with_mawj_data_and_excludes_non_vip`,
`…Roster_csv_export_contains_header_and_mawj_id`,
`…Roster_requires_export_permission`. E2E catalogue: `cp-vip-export.md`.
