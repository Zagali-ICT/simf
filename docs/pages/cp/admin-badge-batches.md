# CP — Badge batches (`/admin/visitors/badge-batches`)

| | |
|---|---|
| **Route** | `/admin/visitors/badge-batches` |
| **Component** | `Components/Pages/Admin/BadgeBatchesPage.razor` (+ `.razor.cs`) |
| **Permission** | `Visitors.ViewBatches` (Administrator baseline) gates the page + the list API; `Visitors.ManageBatches` (Administrator baseline) gates the two destructive row actions (re-email / revoke) and their APIs — split out by the 2026-07-24 security review so read-only batch visibility can be granted without the power to re-email or revoke; `Visitors.BulkGenerate` gates **top-up**, which mints |
| **Nav** | People → **Badge batches** (`Module.AdminBadgeBatches`) |
| **Decision** | D-758 (#10 Phase 2) |
| **E2E** | [`cp-admin-badge-batches.md`](../../tests/e2e/cp-admin-badge-batches.md) (E2E-BBT-001..018) |

## Purpose

Bulk-badge generation (D-473 / D-751, the Delegates + Visitors desks) mints a set of
placeholder QR badges in one action. Before D-758 that "batch" existed only in the
request DTO and vanished once the loop finished — the minted accounts were ordinary
rows with no shared handle. This page makes each bulk-generate run a **persisted
`BadgeBatch`** (App DB), so an admin can manage the generated set as a unit after the
fact: see what was generated, **re-email** the QR pack to an organiser, or **revoke**
the whole batch.

## Data flow

- **List** — `POST /account/api/admin/visitors/badge-batches/list` (GridQuery →
  `GridPage<AdminBadgeBatchSummary>`), newest-first. Server-paged `SimfDataGrid`.
- **Re-email** — `POST /account/api/admin/visitors/badge-batches/re-email`
  (`{ BatchId, RecipientEmail }`). Re-materialises the pack from the batch's minted
  badges (tier name + QR id, stable order), rebuilds the QR pack — a **ZIP of PNGs +
  a printable PDF contact sheet** (D-759) — and enqueues it to the organiser. The
  badges themselves are unchanged; the batch remembers the last recipient.
- **Top-up** — `POST /account/api/admin/visitors/badge-batches/top-up`
  (`{ BatchId, Batches }`). Adds badges to an order that already exists, minting
  them immediately so `TotalCount` always equals the badges that exist, and
  folding the added tier into the order's existing `BadgeBatchItem` row rather
  than appending a second line for the same tier. The dialog adds **one tier per
  run** — the contract
  takes a list, but an order is topped up to add "3 more VIP", and repeating the
  action reads better than a second row-builder inside a modal.
- **Revoke** — `POST /account/api/admin/visitors/badge-batches/revoke` (`{ BatchId }`).
  Disables every account the batch minted (reusing the audience-scoped bulk-delete
  path: `AccountState = Disabled` + token revoke + per-account audit) and marks the
  batch `IsActive = false`. Not reversible.

All four forward through `SimfAdminClient` (CP proxy in `AccountEndpoints`) to the
FastEndpoints in `BadgeBatchEndpoints.cs` → `IAdminUserBulkService`
(`AdminAccountService.Bulk.cs`).

## Columns & actions

| Column | Source |
|---|---|
| Contents | `Tiers` in the reading language, falling back to `CountsSummary`. Both are composed **at read time** from the order's `BadgeBatchItem` rows joined to the LIVE profile-type names — `CountsSummary` (e.g. `VIP × 3 + Normal × 2`) is the invariant-culture English rendering of the same rows, so renaming a tier corrects every historical order instead of freezing the name it carried at mint time |
| Total | `TotalCount` — the sum of the `BadgeBatchItem` counts, derived on read rather than cached on the batch |
| Delegation | `IsDelegate` pill |
| Emailed to | `RecipientEmail` (last organiser, or —) |
| Generated | `CreatedAt` (Saudi time) |
| Status | `IsActive` → Active / Revoked pill |

Row actions, active batches only: **Add more badges** (modal — pick a tier and a
count), **Re-email QR pack** (modal — edit the organiser address, Send) and
**Revoke batch** (confirm modal).

The three do **not** share one permission. Re-email and revoke carry
`Visitors.ManageBatches`; **top-up carries `Visitors.BulkGenerate`**, because it
MINTS badges and that is the permission its endpoint checks. Creating more of an
order is a different authority from re-emailing or revoking one, and each button
must carry the permission of the endpoint it calls rather than the page's.

## Edge cases

- Empty list → `SimfEmptyState` (`Admin.BadgeBatches.None`).
- Re-email with an invalid organiser email → `400 VALIDATION_FAILED`, nothing sent.
- Re-email / revoke an unknown (or already-revoked) batch → `404 ADMIN_USER_NOT_FOUND`.
- Revoke is idempotent-safe: a revoked batch drops its row actions and cannot be
  re-revoked (the endpoint 404s on `IsActive = false`).

## Design notes (D-758)

- **Owner-approved D-110/D-199 freeze-lift** for one additive App-DB table
  `BadgeBatches` + one additive nullable column `UserProfile.BadgeBatchId`
  (migration `App/D758_AddBadgeBatch`). Identity schema untouched.
- The `UserProfile → BadgeBatch` FK is an **intra-App-DB** relation (nullable +
  `OnDelete.Restrict`, mirroring the Organisation / Region FKs) — the D-157 bare-Guid
  rule does not apply (no Identity-owned data in the batch).
- Revoke crosses the App/Identity boundary as **two separate units of work** (disable
  the Identity accounts, then deactivate the App batch) — never a distributed
  transaction (D-157).
- The emailed pack is built by one shared helper (`EnqueueBadgePackEmailAsync`) used by
  both bulk-generate and re-email, so both send the identical **ZIP + PDF** pack.
- **D-759 (#10 Phase 3) — PDF contact sheet.** The email now also carries a printable
  PDF (3-up grid of QR + tier + `#N` + QR id) rendered with **QuestPDF**, beside the
  ZIP of individual PNGs. **Licence caveat (owner-accepted follow-up):** QuestPDF's
  free Community licence only covers organisations under ~$1M revenue; the production
  customer (RSNF) needs a **paid QuestPDF licence** before go-live. Owner chose
  QuestPDF over the MIT alternative (PDFsharp) on 2026-07-22, accepting this.

## Self-claim (#10 Phase 4, Option A)

When the person who receives a badge scans + activates it (`badge_activation_screen`
→ `CompleteActivationAsync` sets their first password + attaches their email) and then
signs in, the placeholder profile is completed by the person, not left as
`{Type} #N` / `NationalityId = 0`. This needs **no new code**: a bulk-badge placeholder
has no interests and no ID image, so `IsProfileCompleteAsync` is `false`, and the
existing D-374 `routeAfterAuth` rule force-routes any signed-in user with
`profileComplete = false` into the add-profile stage (`signUpVisitor`). Saving the real
profile replaces the placeholder display name (D-609). Pinned by
`DelegatesAndBulkBadgesTests.Bulk_generated_badge_profile_is_incomplete_so_self_claim_prompts_the_profile_stage`.

## Changelog

- **2026-07-22 (#10 Phase 4, Option A)** — documented + test-pinned that badge
  self-claim rides the existing D-374 profile-completion flow (no new code).
- **2026-07-22 (D-759, #10 Phase 3)** — the emailed pack (bulk-generate + re-email)
  now attaches a QuestPDF contact-sheet PDF beside the ZIP. Licence follow-up flagged.
- **2026-07-22 (D-758, #10 Phase 2)** — page created. Persisted `BadgeBatch` +
  `BadgeBatchId`, list (`Visitors.ViewBatches`), re-email / revoke (`Visitors.ManageBatches`).
