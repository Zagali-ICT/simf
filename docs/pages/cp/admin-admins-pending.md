# Pending admins — `/admin/admins/pending`

| | |
|--|--|
| **Route** | `/admin/admins/pending` |
| **Audience** | Administrator |
| **Pattern** | Per-kind sibling of [`admin-visitors-pending.md`](admin-visitors-pending.md). |
| **Status** | ✅ Real |
| **Backend** | `POST /account/api/admin/admins/pending/list`, `POST /admin/admins/{id}/approve`, `POST /admin/admins/{id}/reject` |
| **Source** | [`PendingStaff.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/PendingStaff.razor) |
| **Last reviewed** | 2026-05-28 |

## 1. Purpose

Approval queue for self-registered admin candidates in `PendingApproval`.

## Notable difference from Pending Visitors / Others

PendingStaff does **not** yet have the D-128 review-before-approve modal.
The Approve button is a one-click action — no profile preview, no Confirm
modal. This is a parity gap flagged in the D-132 audit and listed on the
backlog as a separate item (would require lifting the same `OpenViewAsync`
+ `ConfirmApproveFromReviewAsync` pattern from PendingVisitors/Others).

For now, **always click View on a peer admin's pending row before approving
manually** (no View button exists; either reach out to the candidate offline
or check `/admin/admins` after approving).

## Reject flow

Identical to the visitors pending queue: reason modal, 10–500 chars, audited.

## 11. E2E

| Scenario | ID |
|----------|----|
| Approve → row vanishes, Administrator role granted | E2E-APN-001 |
| Reject with reason → audited | E2E-APN-002 |
| Reason < 10 chars → Submit disabled | E2E-APN-003 |

## Cross-references

Parity gap with `PendingVisitors` / `PendingOthers` review-before-approve
flow is logged. Lifting D-128 here is straightforward; tracked separately.

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 3).
