# CP page — Logic (أنواع ملفات الزوار · Visitor profile types)

Business rules behind the visitor profile-types page. Verified against
`VisitorProfileTypesList.razor`, `ProfileTypeForm.razor`,
`AdminProfileTypeCommandService.cs`, `ProfileTypeEndpoints.cs` and
`AdminAccount.cs` (the DTOs).

Last updated: 2026-06-13 — CP config-page documentation (D-380)

## L-1 The Visitor scope is pinned twice (page + every grid change)
The page initialises `_query.Filters` with **`userType=Visitor`** and
**`isVisitor=true`** (`Top=20`). On **every** `OnQueryChangedAsync` (sort /
page / filter) the page **re-applies both pins** before reloading — the grid
drops filter keys on sort/page change, and both are "structural to this page".
So the scope can never silently widen to the Other pool.

Server-side, the **`isVisitor` filter is the one that scopes the list**
(`IsForVisitor == true`); after the D-186 collapse every non-admin row is
`UserType.Visitor`, so the `userType` pin is belt-and-braces here — it matters
for **Create** (L-3), where the value must parse to `Visitor`.

## L-2 Audience vs partner = `IsVisitor` (D-186)
Post-D-186 there is no `UserType.Other` enum value — all non-admin profile
types are `UserType.Visitor`, and the **audience-vs-partner split rides on the
`IsForVisitor` column** (`AdminProfileTypeSummary.IsVisitor`). This page is the
**audience** queue (`IsVisitor=true`); the sibling Other page is the
**partner** queue (`IsVisitor=false`). Both write the same `ProfileTypes`
table through the same endpoints. The summary's `UserType` is hard-set to
`"Visitor"` for every row.

## L-3 Create is Visitor-only, audience-flagged
The form (`IsPartnerForm="false"`) sends, on Create:
`UserType="Visitor"`, `IsVisitor = !IsPartnerForm = true`, `MobileAppRole=null`.
The server (`CreateAsync`):
- rejects any UserType that doesn't parse to `Visitor` with **400
  `PROFILE_TYPE_INVALID_USER_TYPE`**;
- writes `IsForVisitor = request.IsVisitor` (→ true here);
- defaults `MobileAppRole` to `None` when null.

## L-4 Per-name uniqueness + in-use delete guard
- **Uniqueness** is enforced **across the whole table** (case-insensitive via
  SQL Server's default collation), on Create and on a rename in Update →
  **409 `PROFILE_TYPE_NAME_TAKEN`**. (Note: the check is `row.Name == name`
  table-wide, not per-`IsVisitor`; the bilingual create message phrases it
  "… for Visitor".)
- **Deactivate** refuses while any `UserProfile.ProfileTypeId == id` →
  **409 `PROFILE_TYPE_IN_USE`**; the CP surfaces the server message in a red
  toast and the row stays Active. An already-inactive row deactivates
  idempotently. Soft-delete sets `IsActive=false` (the active-filtered list
  drops it).

## L-5 The MobileAppRole picker is hidden on this page
`ProfileTypeForm.ShowMobileAppRolePicker` is **false** here:
- Create: `IsPartnerForm == false` → false.
- Edit: `Initial.IsVisitor == true` → false.

Rationale (in-code, D-161/D-186): a visitor/audience profile type resolves to
`MobileAppRole.Visitor` **at JWT issue time from `UserType`**, regardless of
the row's `MobileAppRole` column — so the picker is irrelevant on the audience
side. The form therefore sends `MobileAppRole = null` (Create/Update), the
server stores `None`, and the picker only renders on the Other / partner page.
`MobileAppRole.Visitor` is **never** assignable per row (the service rejects it
with 400).

## L-6 Two-stage validation (client guard, then server)
`ProfileTypeForm.HandleSubmitAsync` guards **before** any POST/PUT:
- Name: required, ≤ 128 → `…Field.NameInvalid`.
- Name (Arabic): required, ≤ 128 → `…Field.NameArabicInvalid`.
- Page colour: required, ≤ 32 → `…Field.PageColorInvalid`.

The server re-validates with FluentValidation (same 1–128 / 1–32 limits;
UserType required ≤ 16 on Create). Both align with the EF column maxes
(CLAUDE.md §7). Inputs are `.Trim()`-ed before the request is built.

## L-7 PageColor — the D-120 paired control
The text input is the **source of truth** and accepts the full free-text
contract (`#rrggbb`, 3-digit hex, or a `var(--…)` CSS variable). The native
`<input type="color">` swatch only **displays** a canonical 6-hex value —
`PageColorAsHex` matches `^#[0-9A-Fa-f]{6}$` and otherwise falls back to navy
**`#244A77`** for display; picking from the swatch writes the chosen
`#rrggbb` back into the text input. No write happens until the user picks. The
grid + Details render the raw stored string.

## L-8 List-failure fallback (no crash)
`LoadAsync` reads the envelope and, on a non-success / null-data response,
falls back to `GridPage<AdminProfileTypeSummary>.Of(empty)` — so an API 500 or
error envelope renders the empty grid (`SimfEmptyState`) rather than throwing.
A successful deactivate / save shows a green toast and reloads the grid; a
failed deactivate shows the localised error toast.

## L-9 Permissions (per-page + per-action)
| Surface | Gate |
|---------|------|
| Page (`@attribute [RequirePermission]`) | `ProfileTypes.View` |
| Nav item (`CpNavigation`) | `RequiredPermission = ProfileTypes.View` |
| List / Get API | `ProfileTypes.View` |
| Create API | `ProfileTypes.Create` |
| Update API | `ProfileTypes.Edit` |
| Deactivate API | `ProfileTypes.Delete` |

All four codes are seeded `AdminOnly` in `PermissionCatalog.All`;
`Administrator = "*"` passes them all. (CLAUDE.md hard rule: an ungated admin
page/endpoint is a security defect.)

## L-10 Consumer linkage — the app picker + the C5 lock
The rows managed here feed the app sign-up profile form
([Page_007](../../App/Page_007/)) via
`GET /app/account/profile-types?isVisitor=true` (D-190 — active rows only,
ordered by Name). On that screen the **Visitor** tab does **not** show a
picker: per **C5 (D-371)** it **auto-locks** to the single seeded **"Normal"
/ "عادي"** audience row (server-enforced). The **Other** tab loads
`?isVisitor=false` and shows the partner picker (managed by the sibling page).
So in practice the visitor list is usually the one seeded "Normal" row, while
the multi-row picker lives on the Other side — but both are this same table,
edited through this same form.

## L-11 Audit trail
Every mutation writes one audit row: `ProfileTypeCreated`,
`ProfileTypeUpdated` (with the IsVisitor old/new flip + linked-account count
when it changes), `ProfileTypeDeactivated` — each carrying the actor's id and
a `Detail` string (id + name). Reads are not audited.
