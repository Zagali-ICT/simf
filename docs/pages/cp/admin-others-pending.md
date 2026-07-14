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
- **D-727 (owner item 5):** when the applicant has a profile photo
  (`HasAvatar`), the View modal renders a "Profile photo" block (shared
  `AdminProfilePhotoBlock`) streaming `/admin/others/{id}/avatar`, so the
  reviewer sees the staff member's face before approving. Same for the approved
  Others / Visitors detail views.
- Cross-kind security: a Visitor id on `/admin/others/{id}/profile-for-approval`
  returns 404.
- **D-353 parity:** the toolbar carries the popup/full-page presentation toggle
  (persisted per-user under `pending-others`); the View / Approve review is framed
  by `CrudShell` — a `SimfModal` popup by default, or a full-page `CrudPageFrame`
  that hides the grid — matching `/admin/visitors`. Shared plumbing lives in
  `PendingApprovalPageBase` (`PresentationPageKey`).
- **D-568 photo-in-list:** the queue's name column renders the applicant's
  profile-photo thumbnail via the shared `SimfIdentityCell`
  (`AdminPendingUserSummary.HasAvatar` streams `/account/api/admin/others/{id}/avatar`),
  or an initials tile when there is no photo.

## 11. E2E

| Scenario | ID |
|----------|----|
| Approve with View first → row vanishes + Approved + QR minted | E2E-OPN-001 |
| Reject with reason → audited | E2E-OPN-002 |
| Cross-kind id on `.../profile-for-approval` → 404 | E2E-OPN-003 |
| View / Approve opens as popup or full page per the toolbar toggle; full page hides the grid (D-353) | E2E-OPN-017 |

_Last reviewed:_ 2026-07-14 by Claude (D-353 parity — popup/full-page toggle on the review modal).
_Earlier:_ 2026-05-28 by Claude (D-133 slice 3).
