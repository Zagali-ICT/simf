# CP page — Logic (العارضون · Exhibitors)

Business rules behind `/admin/exhibitors`. Grounded in `AdminExhibitorService`,
`Exhibitor` (domain), `ExhibitorEndpoints` and the two forms. The wire contract
is in [admin-exhibitors_API.md](admin-exhibitors_API.md); the layout is in
[admin-exhibitors_Design.md](admin-exhibitors_Design.md).

Last updated: 2026-06-13 — CP config-page documentation (D-380).

## L-1 — what an exhibitor is
An **exhibitor** is a CP-created **company** record (D-199 #3 / D-202 Track-2):
a bilingual name (`Name` EN + `NameArabic` AR, Arabic the primary surface),
optional inline `ContactEmail` / `ContactPhone` / `Website`, and an optional
`ContactId` link to the shared **Contact** directory (D-281). It is a
`BaseAuditEntity` (Id, IsActive, CreatedAt/UpdatedAt, CreatedBy/UpdatedBy). The
owner model is "create the exhibitor **name** first, then create login
**accounts** under it" — accounts are `ExhibitorMembership` rows. Exhibitors are
**CP-created only**; in-app exhibitor self-signup is permanently descoped.

## L-2 — validation (`AdminExhibitorService.Validate`)
The server is authoritative. On create/update:
- **NameEn** and **NameAr** must each be **1–256** chars (non-blank). Both are
  `.Trim()`ed before persist. → 400 `EXHIBITOR_INVALID`.
- **ContactEmail** ≤ 320, **ContactPhone** ≤ 32, **Website** ≤ 512 (each only
  checked when present). → 400 `EXHIBITOR_INVALID`. Blank optional strings are
  normalised to `null` (`NormaliseOptional`).
- **ContactId** (when set) must reference an **existing active** Contact
  (`EnsureContactIsValidAsync`) — else 400 `EXHIBITOR_INVALID`
  ("Contact id '…' does not exist or is inactive."). This turns a bad FK into a
  clean 400 instead of a DB FK-violation 500.

The form's **client guard** only checks blank NameEn/NameAr (→
`Admin.Exhibitors.NameRequired`, no POST). The field `MaxLength` attributes
(256/256/320/32/512) mirror the server limits per the alignment rule.

## L-3 — create vs update
- **Create** (`CreateAsync`) sets `Id = NewGuid()`, `IsActive = true`,
  `CreatedAt = now`. There is **no Active flag on the create request** — the
  AddEdit form hides the Active checkbox when `!IsEdit`, so a created exhibitor
  is always active. Writes `ExhibitorCreated` audit. Returns the re-read detail.
- **Update** (`UpdateAsync`) re-validates, loads the tracked entity (404
  `EXHIBITOR_NOT_FOUND` if missing), overwrites all fields **including
  `IsActive`** (so Edit can toggle active/inactive), sets `UpdatedAt = now`.
  Writes `ExhibitorUpdated` audit (`active={IsActive}`).

## L-4 — soft-delete is idempotent
`DeactivateAsync` loads the entity (404 if missing); **if already inactive it
returns early** with no write and **no second audit row**. Otherwise it sets
`IsActive = false` + `UpdatedAt`, then writes `ExhibitorDeactivated`. There is no
hard delete. Soft-deactivated rows still appear in the grid (with the "off" pill)
unless an `isActive=false`/`true` column filter excludes them.

## L-5 — AccountCount is derived
The grid's **Accounts** column is computed per-row as
`Set<ExhibitorMembership>().Count(m => m.ExhibitorId == c.Id && m.IsActive)` — a
**sub-query over active memberships**. Consequences:
- It is **read-only**, **not sortable** and **not server-filterable**.
- It is **never set by import** (import is name-only).
- It increments after a successful provision (the page reloads the grid).

## L-6 — account provisioning (`ProvisionAccountAsync`)
- Loads the exhibitor (404 `EXHIBITOR_NOT_FOUND` if missing). If the exhibitor is
  **inactive** → 409 `EXHIBITOR_INACTIVE` ("reactivate it before adding
  accounts").
- Validates **ContactName** 1–256 and **Email** 1–320 (else 400
  `EXHIBITOR_ACCOUNT_INVALID`) and **RoleLabel** ≤ 128 (else 400
  `EXHIBITOR_ACCOUNT_INVALID`).
- **Reuses the existing admin provisioning pipeline**
  `IAdminUserProvisioningService.CreateVisitorAsync` — a **least-privilege
  Visitor** account (`ProfileTypeId = null`, no RBAC role) created in the
  pending-approval state. That pipeline validates the email-already-registered
  case and throws its own `ApiException` on conflict (never a hand-rolled
  `UserManager`).
- Inserts an `ExhibitorMembership` (Id, ExhibitorId, `UserId = created.UserId`,
  ContactName, RoleLabel, IsActive=true, CreatedAt) and writes
  `ExhibitorAccountProvisioned` audit (ActorUserId + SubjectUserId +
  SubjectEmail).

## L-7 — cross-database separation (D-157)
`ExhibitorMembership.UserId` is a **logical FK** to the Identity DB's `SimfUser`
— **not** a DB constraint and **never** a cross-DB JOIN. `ListAccountsAsync`:
1. confirms the exhibitor exists (else 404 `EXHIBITOR_NOT_FOUND` — a stranger id
   does not silently return an empty list),
2. reads the active memberships from `SimfAppDbContext`,
3. resolves the small id-set of emails from `SimfIdentityDbContext.Users`
   `AsNoTracking` and stitches them on read (missing email → `""`).
No App entity holds a copy of Identity-owned data; the email is resolved on read.

## L-8 — booth → exhibitor link (D-222) — the app linkage
The mobile **Venue map (Page 015)** / **Booth detail** attribute a booth to its
exhibitor through this directory:
- `Booth.ExhibitorId` (nullable `Guid?`) is a **real FK to `Exhibitor.Id`** in
  the **same App DB** and is the **source of truth** for a booth's exhibitor
  (D-222). Nullable because a booth may exist before its exhibitor is
  provisioned.
- The **public booth projection** fills `ExhibitorName` / `ExhibitorNameArabic`
  from the **linked exhibitor** when `ExhibitorId` is set. The free-text
  `Booth.ExhibitorName*` columns are a **legacy fallback** retained for the wire
  contract (D-219) and pre-D-222 rows; they are **no longer settable** from the
  admin write surface.
- On the app, the venue-map booth **info card** surfaces the exhibitor **name** +
  **sector** (the sector is `Booth.Sector*`, distinct from the exhibitor record).
  So the **name** the app shows behind a booth comes from the exhibitor created
  on this CP page; editing an exhibitor's name here changes what the app shows
  for every booth linked to it.
- The booth **officer** (`Booth.ContactId`) is a *person* in the shared Contact
  directory and is **distinct** from the exhibitor — do not conflate them.

## L-9 — audit events
`AuditEvents.ExhibitorCreated`, `ExhibitorUpdated`, `ExhibitorDeactivated`,
`ExhibitorAccountProvisioned` — each `Outcome=Success` with `ActorUserId`;
provisioning adds `SubjectUserId` + `SubjectEmail`. Idempotent deactivate writes
no audit (L-4).

## L-10 — permissions (per-action)
| Action | Policy |
|--------|--------|
| Page + list + get + list-accounts | `Exhibitors.View` |
| Create + provision-account | `Exhibitors.Create` |
| Update + (UI) Accounts icon | `Exhibitors.Edit` |
| Deactivate | `Exhibitors.Delete` |
| Excel export | `Exhibitors.Export` |
| Excel import | `Exhibitors.Import` |

All six codes are `PermissionCatalog.Exhibitors.*`; `Administrator = "*"`
satisfies all. The page is gated by `Exhibitors.View`; the CRUD action buttons
are **not** individually gated in the CP (per-action enforcement is API-side),
while the **Accounts** icon **is** wrapped in
`<AuthorizedAction Permission="Exhibitors.Edit">`.

## L-11 — edge cases / known limitations
- **No Active checkbox on Add** — a created exhibitor is always active (L-3).
- **AccountCount derived + read-only** — not sortable, not filterable, not
  importable (L-5).
- **Deactivate idempotent** — re-deactivating writes nothing (L-4).
- **Provisioning under an inactive exhibitor** → 409 (L-6); reactivate via Edit
  first.
- **Account modal is independent of `CrudShell`** — the Page↔Popup toggle does
  not affect it.
- **Import is insert-only and name-only** — `AccountCount`, `IsActive` and the
  `ContactId` directory FK are intentionally not importable.
- **Cross-DB rule is permanent** (L-7); never add an EF navigation between
  `ExhibitorMembership.UserId` and `SimfUser`.

## Dependencies
- `IAdminExhibitorService` / `AdminExhibitorService` (Infrastructure) + the
  `SimfAppDbContext` `Exhibitors` / `ExhibitorMembership` sets + `Contacts`.
- `IAdminUserProvisioningService.CreateVisitorAsync` (account provisioning).
- `SimfIdentityDbContext.Users` (cross-context email resolution).
- `IAuditLog` (`AuditEvents.Exhibitor*`).
- BFF: `AccountEndpoints.cs` passthroughs `/account/api/admin/exhibitors/*` +
  `MapGridExcel(group, "exhibitors")` (D-356).
- App: `Booth.ExhibitorId` link consumed by `docs/App/Page_015/` (venue map).
</content>
