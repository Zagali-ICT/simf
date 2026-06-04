# Pending visitors — `/admin/visitors/pending`

| | |
|--|--|
| **Route** | `/admin/visitors/pending` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel |
| **Audience** | Administrator |
| **Auth** | `[Authorize(Roles = "Administrator")]` + Approved |
| **Pattern** | D-117 canonical CRUD + D-132 mandatory Multiselect/SimfBanner. Approval queue (no CRUD writes from the toolbar — per-row Approve / Reject / View only). |
| **Status** | ✅ Real |
| **Backend** | `POST /account/api/admin/visitors/pending/list`, `POST /admin/visitors/{id}/approve`, `POST /admin/visitors/{id}/reject`, `GET /admin/visitors/{id}/profile-for-approval` (D-124, D-125) |
| **Source** | [`PendingVisitors.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/PendingVisitors.razor) |
| **Last reviewed** | 2026-05-28 |

## 1. Purpose

Approval queue for visitors whose accounts are in `PendingApproval` state.
Each row has three actions: **View** (read-only profile preview, D-124/D-125),
**Approve** (D-128 review-before-approve — opens the same View modal with a
Confirm footer), **Reject** (opens a reason modal, 10–500 chars). Approval
mints the QR badge and unlocks event entry; rejection records a reason for
audit and the visitor sees it on `/account/rejected`.

## 4.2 Per-row actions

| Button | Behaviour |
|--------|-----------|
| View | Opens View modal (read-only `<dl>` of every profile field + ID image inline if present) |
| Approve | Opens the same modal in `_approveMode = true` → footer shows Confirm / Cancel; Confirm calls `POST /approve` |
| Reject | Opens a separate reason modal with `SimfTextarea` (10–500 chars required) |

D-132 added the Select-all toolbar checkbox + per-row checkboxes for canonical
consistency, but **no bulk-approve / bulk-reject endpoint exists yet** — the
checkboxes are presentational. Bulk endpoints are tracked as a separate
backlog item.

## 7. Edge cases

- **Stale row** — between list-load and Approve click, the row could already
  have been approved/rejected by another admin → server returns 404 +
  `ErrorCodes.NotFound`; toast surfaces the bilingual fallback.
- **Reject reason too short / long** → Submit button disabled.
- **View modal closed before Approve** → ConfirmApproveFromReviewAsync also
  closes the view so the success toast isn't obscured.

## 10. Use cases

UC-VIS-PENDING-LIST, UC-VIS-PENDING-VIEW, UC-VIS-APPROVE-WITH-REVIEW
(D-128), UC-VIS-REJECT-WITH-REASON.

## 11. E2E

| Scenario | ID |
|----------|----|
| Approve flow with View first → row vanishes + AccountState=Approved + QR minted | E2E-VPN-001 |
| Reject with 50-char reason → AccountState=Rejected + reason audited | E2E-VPN-002 |
| Reject with 5-char reason → Submit disabled | E2E-VPN-003 |
| View shows ID image inline when HasIdImage | E2E-VPN-004 |
| Stale row (already approved by sibling admin) → 404 toast | E2E-VPN-005 |

## 12. Related

- Sibling: [`admin-visitors.md`](admin-visitors.md)
- Decisions: D-128 (review-before-approve), D-124 / D-125 / D-126 (pending-profile read), D-132 (canonical sweep).

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 3).
