# CP — FAQ manager (`/admin/faq`)

_Component:_ `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/FaqManager.razor`
_API:_ `src/Backend/SIMF.Api/Endpoints/Admin/FaqEndpoints.cs`
_E2E:_ [e2e/cp-admin-faq.md](../../tests/e2e/cp-admin-faq.md)

## 1. Purpose

Authors the الأسئلة الشائعة content the mobile app renders as an accordion. Two
levels: **groups** (a heading) and **entries** (a question and its answer, both
bilingual) inside a group. The team edits here; the app reads the published
result anonymously.

## 2. Shape

Master-detail over two `SimfDataGrid`s — `RatingConfig` mirrors this page, so
the two read alike on purpose.

1. **Groups** grid — Name (EN), Name (AR), Order, Entry count, Active. The row
   action **Manage entries** selects a group and loads its entries below.
2. **Entries** grid (per selected group) — Question (EN), Question (AR), Order,
   Active, plus a read-only **Details** view.

CRUD runs through two `SimfModal`s. Delete is a soft-delete (`IsActive=false`),
confirmed through `SimfConfirm`.

**Why the entries grid has Details and the groups grid does not (D-835).**
Gating Edit and Delete removed the only two ways to open an entry, so a holder of
`Faq.View` alone could no longer read one — and the **answer text** has no
column, making Edit the only path to the content the team is responsible for.
Details is ungated: reading a row is what `Faq.View` already bought. The groups
grid needs no equivalent because it already columns every field on its summary
but `CreatedAt`, and its **Manage entries** action is itself ungated — it is the
single reviewed entry in `ActionPermissionGuardRatchetTests.ReviewedReadPaths`.

## 3. Permission

Page gate `Faq.View` (`@attribute [RequirePermission(...)]`). The four codes map
to the endpoints exactly:

| Action | Code | Endpoints |
|---|---|---|
| Read | `Faq.View` | `POST …/groups/list`, `GET …/groups/{id}`, `POST …/groups/{groupId}/entries/list`, `GET …/entries/{id}` |
| Create | `Faq.Create` | `POST …/groups`, `POST …/entries` |
| Edit | `Faq.Edit` | `PUT …/groups/{id}`, `PUT …/entries/{id}` |
| Delete | `Faq.Delete` | `DELETE …/groups/{id}`, `DELETE …/entries/{id}` |

All ten admin endpoints are under `/api/v1/admin/faq/` and carry both their
permission policy and `RequireApprovedAccount`. Since D-837 the grid resolves
these once per parameter set, so a holder denied every action gets no toolbar
bar or actions column rather than an empty one.

## 4. Public read

`GET /api/v1/app/faq` is **`AllowAnonymous`** and projects only *active* groups
and entries, ordered server-side — the same posture as the other public content
reads (organization profile, sessions, archive). Deactivating a group or entry
here is therefore what removes it from the app; there is no separate publish
step.

## 5. Data

`FaqGroup` + `FaqEntry` on `SimfAppDbContext`, added additively under the D-211
freeze lift (as-built D-218). No schema change since.

## 6. Tests

- E2E catalogue: [cp-admin-faq.md](../../tests/e2e/cp-admin-faq.md) —
  E2E-FAQ-001..017, including E2E-FAQ-017 for the D-835 Details view and the
  groups grid's ungated drill-in.
- Ratchet: `ActionPermissionGuardRatchetTests` holds the groups grid as its one
  reviewed read-path exception, with the reason.

## 7. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-01 | D-218 | Page and API shipped (FAQ groups + entries, bilingual, soft-delete). |
| 2026-08-02 | D-830 | Grid-rendered Add / Edit / Delete gained their permission parameters. |
| 2026-08-03 | D-835 | Entries grid gained the read-only **Details** view; the groups grid was reviewed and kept its **Manage entries** drill-in instead. |
| 2026-08-03 | D-837 | Container visibility now follows the permissions, not just the wired callbacks. |

_Last reviewed:_ 2026-08-03 — first authored; the route had carried "—" in the
doc column of PAGE-INDEX since D-218.
