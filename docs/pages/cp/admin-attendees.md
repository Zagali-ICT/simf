# Attendees roster — `/admin/attendees`

| | |
|--|--|
| **Route** | `/admin/attendees` |
| **Audience** | Administrator |
| **Auth** | `[Authorize(Roles = "Administrator")]` + `RequireApprovedAccount` |
| **Pattern** | D-117 + D-132 read-only canonical grid + filter row. |
| **Status** | ✅ Real (D-134 Sprint A) |
| **Backend endpoints** | `POST /account/api/admin/attendees/list` |
| **Source** | [`AttendeesList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/AttendeesList.razor), [`AdminAttendeeService`](../../../src/Backend/SIMF.Infrastructure/Identity/AdminAttendeeService.cs) |
| **Backed by** | Join over the existing `SimfUser` + `UserProfile` + `ProfileType` — **no schema change**. |
| **Tests** | [`docs/tests/e2e/cp-admin-attendees.md`](../../tests/e2e/cp-admin-attendees.md) |
| **Last reviewed** | 2026-05-29 |

## 1. Purpose

Combined **read-only roster** of every event attendee — Visitors and
Others in one place. Admins are excluded (they're operators, not
attendees). The page is the single fastest answer to "is X registered?"
and "how many Approved Visitors do we have?" — the per-kind pages
(`/admin/visitors`, `/admin/others`) are still the surface for creating
or editing rows; this page is the unified read.

## 4. UI

- Banner: `<SimfBanner Title="@L[\"Admin.Attendees.Title\"]" />`.
- Filter row: Kind dropdown (All / Visitors only / Others only) +
  State dropdown (Any / Approved / Pending / Rejected) + Email-or-
  display-name search + Apply / Clear buttons.
- Grid columns: Email, Display name, Kind (localised), Profile type
  (bilingual, "—" when not filled), State (coloured pill), QR id
  (12-char id or "—" when not minted), Registered (yyyy-MM-dd).
- Sortable on Email, DisplayName, Kind, Registered.
- Newest-first default.
- Multiselect checkboxes render per the D-132 mandate, but no bulk
  callbacks are wired — the page is read-only.

## 5. Data flow

```
Page init → POST /account/api/admin/attendees/list (GridQuery)
  → AdminAttendeeService.ListAsync
      - excludes admins
      - left-joins UserProfile and ProfileType
      - applies userType / accountState / search filters
      - sorts and paginates
  → ApiResult<GridPage<AdminAttendeeSummary>>
```

## 7. Edge cases + known limitations

- **Profile not filled yet** — left-join yields null `ProfileTypeName` /
  `QrId`; the page renders "—" so an operator can spot incomplete
  registrations.
- **Cross-kind double-counting impossible** — `UserType` is single-valued
  per user.
- **No write surface** — Edit happens on the per-kind page; Approve /
  Reject happen on `/admin/visitors/pending` or `/admin/others/pending`.
- **Export to XLSX deferred** — D-134-MBP §3.2.2 listed it; ships in a
  follow-up.

## 11. E2E

See [`docs/tests/e2e/cp-admin-attendees.md`](../../tests/e2e/cp-admin-attendees.md):
E2E-ATT-001 default render, 002 filter by kind, 003 filter by state,
004 search by email substring, 005 auth gate, 006 RTL.

## 12. Related docs

- Admin Manual: `Admin-Manual.md § 4.2 Attendees`.
- Sibling write surfaces: [`admin-visitors.md`](admin-visitors.md), [`admin-others.md`](admin-others.md).
- D-134 plan §3.2.2.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-29 | D-134 Sprint A | Original — read-only roster join across existing tables. |

_Last reviewed:_ 2026-05-29 by Claude (D-134 Sprint A).
