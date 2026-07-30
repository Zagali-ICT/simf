# Pending admins — `/admin/admins/pending`

| | |
|--|--|
| **Route** | `/admin/admins/pending` |
| **Audience** | Administrator |
| **Pattern** | Per-kind sibling of [`admin-visitors-pending.md`](admin-visitors-pending.md). |
| **Status** | ✅ Real |
| **Backend** | `POST /account/api/admin/admins/pending/list`, `POST /admin/admins/{id}/approve`, `POST /admin/admins/{id}/reject` |
| **Source** | [`PendingStaff.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/PendingStaff.razor) |
| **Last reviewed** | 2026-07-30 |

## 1. Purpose

Approval queue for self-registered admin candidates in `PendingApproval`.

## Notable difference from Pending Visitors / Others

This queue confirms rather than reviews. `PendingVisitors` / `PendingOthers`
open the D-128 profile-review modal before approving, because those accounts
carry a profile worth reading (tier, ID images). An administrator candidate
has no such profile, so **D-799** gives this queue a `SimfConfirm` naming the
candidate instead — closing the D-132 parity gap without inventing a review
surface with nothing to show.

Approving grants Control Panel access and mints the QR badge, so it cannot
commit on a single click. Bulk approve confirms the count on all three
queues (the guard lives in `PendingApprovalPageBase`).

## Reject flow

Identical to the visitors pending queue: reason modal, 10–500 chars, audited.

## 11. E2E

| Scenario | ID |
|----------|----|
| Approve → confirm → row vanishes, Administrator role granted | E2E-APN-001 |
| Reject with reason → audited | E2E-APN-002 |
| Reason < 10 chars → Submit disabled | E2E-APN-003 |
| Approve → Cancel on the confirm → nothing is posted | E2E-APN-016 |
| Bulk approve → confirm names the count → Cancel → nothing posted | E2E-APN-017 |

## Cross-references

The D-132 parity gap with `PendingVisitors` / `PendingOthers` is **closed** by
D-799 — as a confirmation rather than a review modal, for the reason above.

_Last reviewed:_ 2026-07-30 by Claude (D-799 destructive-action safety).
