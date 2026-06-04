# Pending others — `/admin/others/pending`

| | |
|--|--|
| **Route** | `/admin/others/pending` |
| **Audience** | Administrator |
| **Pattern** | Per-kind sibling of [`admin-visitors-pending.md`](admin-visitors-pending.md); identical canonical shape. |
| **Status** | ✅ Real |
| **Backend** | `POST /account/api/admin/others/pending/list`, `POST /admin/others/{id}/approve`, `POST /admin/others/{id}/reject`, `GET /admin/others/{id}/profile-for-approval` |
| **Source** | [`PendingOthers.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/PendingOthers.razor) |
| **Last reviewed** | 2026-05-28 |

## 1. Purpose

Approval queue for Other-typed accounts (exhibitor reps, sponsor staff,
press, contractors) in `PendingApproval`. Same View / Approve-with-review
(D-128) / Reject-with-reason flow as the Visitors pending queue.

## Differences from `/admin/visitors/pending`

- The View modal's full-profile section is conditional on the user having
  filled their profile (admin-created Others may sit in PendingApproval
  before filling).
- The ID-document image URL routes to `/admin/others/{id}/id-document`.
- Cross-kind security: a Visitor id on `/admin/others/{id}/profile-for-approval`
  returns 404.

## 11. E2E

| Scenario | ID |
|----------|----|
| Approve with View first → row vanishes + Approved + QR minted | E2E-OPN-001 |
| Reject with reason → audited | E2E-OPN-002 |
| Cross-kind id on `.../profile-for-approval` → 404 | E2E-OPN-003 |

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 3).
