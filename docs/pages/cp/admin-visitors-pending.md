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
| **Backend** | `POST /account/api/admin/visitors/pending/list`, `POST /admin/visitors/{id}/approve` (optional `ProfileTypeId`, D-386), `POST /admin/visitors/{id}/reject`, `GET /admin/visitors/{id}/profile-for-approval` (D-124, D-125; full profile via `PendingProfileResponse` + `AdminApprovalReadService`, D-385), `GET /admin/visitors/{id}/id-document` (face photo, D-387) |
| **Source** | [`PendingVisitors.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/PendingVisitors.razor) |
| **Last reviewed** | 2026-06-13 |

## 1. Purpose

Approval queue for visitors whose accounts are in `PendingApproval` state.
Each row has three actions: **View** (read-only profile preview, D-124/D-125),
**Approve** (D-128 review-before-approve — opens the same View modal with a
Confirm footer), **Reject** (opens a reason modal, 10–500 chars). Approval
mints the QR badge and unlocks event entry; rejection records a reason for
audit and the visitor sees it on `/account/rejected`.

The View / Approve modal now shows **all captured profile data** (D-385): in
addition to the base identity and form fields it renders **Job title**,
**Gender**, **Organisation** (bilingual name), **Plate number**,
**Reference number**, and the **interest names** (the selected interests are
listed by name, replacing the earlier bare count). The data is served by
`PendingProfileResponse` populated by `AdminApprovalReadService`. `QrId` and
`RejectionReason` are deliberately **excluded** from this pending preview (the
badge has not been minted yet and there is no rejection on a pending account).

When **approving**, the admin can **optionally pick the visitor's profile-type
(tier)** in the approve modal (D-386). The picker lists the active
audience-side profile types and defaults to **"Keep current"**; the chosen id
is sent as the optional `ProfileTypeId` on `POST /admin/visitors/{id}/approve`.
A null / "Keep current" selection leaves the visitor's tier unchanged.

The captured **face photo** (D-387) is shown as a thumbnail in the modal and is
**clickable → a full / original-size lightbox** (a stacked `SimfModal`). A
**Download** link (`<a download>`, same-origin cookie-auth to
`/account/api/admin/visitors/{id}/id-document`) sits under the thumbnail and in
the lightbox footer.

## 4.2 Per-row actions

| Button | Behaviour |
|--------|-----------|
| View | Opens View modal (read-only `<dl>` of **every** captured profile field — base identity + form fields + Job title / Gender / Organisation / Plate number / Reference number / interest **names**, D-385 — plus the clickable face-photo thumbnail, D-387) |
| Approve | Opens the same modal in `_approveMode = true` → footer shows the **profile-type (tier) picker** ("Keep current" default, D-386) + Confirm / Cancel; Confirm calls `POST /approve` with the optional `ProfileTypeId` |
| Reject | Opens a separate reason modal with `SimfTextarea` (10–500 chars required) |

**Approve-time tier picker (D-386).** The picker is populated only when the
admin holds `ProfileTypes.View` (it reads the active audience-side profile
types). It lists active, audience-side types and defaults to **"Keep current"**.
The selected id is sent as the optional `ProfileTypeId`; a partner-side /
inactive / unknown id is rejected by the API with **400
`ADMIN_PROFILE_TYPE_INVALID`** and a bilingual error toast. `null` ("Keep
current") leaves the visitor's existing tier unchanged.

**Photo lightbox + download (D-387).** The face-photo thumbnail in the modal is
clickable and opens the original-size image in a stacked `SimfModal` lightbox.
A **Download** link (`<a download>`) appears under the thumbnail and in the
lightbox footer; both point at `/account/api/admin/visitors/{id}/id-document`
and rely on the same-origin admin cookie for auth.

The Select-all toolbar checkbox + per-row checkboxes (D-132) drive the
**bulk-approve (D-164)** and **bulk-reject (D-209)** endpoints — they are no
longer presentational.

## 7. Edge cases

- **Stale row** — between list-load and Approve click, the row could already
  have been approved/rejected by another admin → server returns 404 +
  `ErrorCodes.NotFound`; toast surfaces the bilingual fallback.
- **Reject reason too short / long** → Submit button disabled.
- **View modal closed before Approve** → ConfirmApproveFromReviewAsync also
  closes the view so the success toast isn't obscured.
- **Invalid tier on approve** — a partner-side / inactive / unknown
  `ProfileTypeId` → server returns 400 `ADMIN_PROFILE_TYPE_INVALID`; the
  bilingual error toast surfaces and the visitor stays pending (D-386).
- **Admin lacks `ProfileTypes.View`** → the approve-time tier picker is empty /
  unavailable; approval still works (the tier is simply left unchanged) (D-386).

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
| Modal shows all captured profile fields (Job title / Gender / Organisation / Plate / Reference) (D-385) | E2E-VPN-017 |
| Selected interests render by name, not a count (D-385) | E2E-VPN-018 |
| Approve **with** a tier → visitor's profile-type is set (D-386) | E2E-VPN-019 |
| Approve with a partner/inactive tier → 400 `ADMIN_PROFILE_TYPE_INVALID` toast (D-386) | E2E-VPN-020 |
| Approve with "Keep current" → tier unchanged (D-386) | E2E-VPN-021 |
| Photo thumbnail opens full-size in the lightbox (D-387) | E2E-VPN-022 |
| Photo downloads via the Download link (D-387) | E2E-VPN-023 |
| RTL render of the View / Approve modal (D-385/386/387) | E2E-VPN-024 |

## 12. Related

- Sibling: [`admin-visitors.md`](admin-visitors.md)
- Decisions: D-128 (review-before-approve), D-124 / D-125 / D-126 (pending-profile read), D-132 (canonical sweep), D-385 (all-data display via `PendingProfileResponse` + `AdminApprovalReadService`), D-386 (approve-time profile-type picker + `ADMIN_PROFILE_TYPE_INVALID`), D-387 (face-photo lightbox + download).

## Changelog

- **2026-06-13 (D-385 / D-386 / D-387):** modal now shows all captured profile
  data (Job title, Gender, Organisation [bilingual], Plate number, Reference
  number, interest names instead of a count); approve modal gained an optional
  profile-type (tier) picker ("Keep current" default; needs `ProfileTypes.View`
  to populate; invalid/partner/inactive → 400 `ADMIN_PROFILE_TYPE_INVALID`); the
  face photo is clickable → full-size `SimfModal` lightbox with a Download link.

_Last reviewed:_ 2026-06-13 by Claude (D-385/386/387 doc DoD).
_Earlier:_ 2026-05-28 by Claude (D-133 slice 3).
