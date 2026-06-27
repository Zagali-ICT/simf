# Badge update requests — `/admin/badge-requests`

| | |
|--|--|
| **Route** | `/admin/badge-requests` (`BadgeRequestsList.razor`) |
| **Audience** | Administrator |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.BadgeUpdateRequests.View)]` (page); API endpoints gated `BadgeUpdateRequests.View` (list + GetById) / `.Manage` (respond), all `+ RequireApprovedAccount` |
| **Pattern** | D-500 (Wave 5) review/approval queue (NOT full CRUD) — SimfDataGrid (server-paged, sortable + filterable) + a per-row Respond modal. **Mirrors** `SpeakerMeetingRequestsList.razor`. |
| **Status** | ✅ Real (D-500) |
| **Backend endpoints** | BFF passthroughs `/account/api/admin/badge-requests/*` → API: `POST /admin/badge-requests/list`, `GET /admin/badge-requests/{id}`, `PUT /admin/badge-requests/{id}/respond`. Public submit (app, not this page): `POST /api/v1/app/badge-requests`. |
| **Backed by** | `dbo.BadgeUpdateRequests` table (additive, migration `D500`). |
| **Tests** | [`docs/tests/e2e/cp-badge-requests.md`](../../tests/e2e/cp-badge-requests.md) (`E2E-CPBR-001..008`) |
| **Last reviewed** | 2026-06-26 |

## 1. Purpose

The admin review queue for **badge update requests** submitted by the audience
(D-500, Wave 5, app الطلبات screen `/requests`, Figma `1408:9726`). An approved
attendee asks for a corrected job title on their badge — a `requestedJobTitle`
(1–128 chars) plus an optional note. Those requests land here as `Pending` rows;
the administrator reviews each one and **responds** (Accept or Reject) with an
optional note (≤ 2000). **On Accept the requested title is applied to the user's
profile `JobTitle`** — that is the action's whole point.

This is a **response-only queue, not a CRUD page** — there is no Add / Edit /
Delete from the Control Panel. Requests are *created* from the app (the public
submit endpoint) and *responded to* from the CP modal. The golden path is
therefore **list → Respond (Accept) → row flips to Accepted + the user's JobTitle
updates**, not the classic "Add → Edit → Delete" round-trip.

It **mirrors** the speaker meeting requests desk
([`admin-speaker-meeting-requests.md`](admin-speaker-meeting-requests.md)) — same
SimfDataGrid + Respond-modal pattern and the same list-omits-email PII rule — and
sits alongside the sibling new desk
[`document-requests.md`](document-requests.md). The app side is
[`mobile/requests.md`](../mobile/requests.md).

## 4. UI

- Banner (`SimfBanner`) + a single owner-mandated **SimfDataGrid** (server-paged,
  per-column filter + sort, full pager). No Add toolbar button — this page does not
  create rows.
- Grid columns (see §4.5): Requester, Requested job title, Status (pill), Submitted
  (CreatedAt, UTC), Responded (RespondedAt, UTC or "—").
- Status pill (`SimfPill`): Pending = amber (`warn`), Accepted = green (`on`),
  Rejected = grey (`off`).
- Per-row **Respond** action — a quiet reply (↩) icon shown **only on `Pending`
  rows**, wrapped in `<AuthorizedAction Permission="BadgeUpdateRequests.Manage">`.
  Resolved (Accepted / Rejected) rows show no action icon.
- **Respond modal** (`SimfModal`): a description list (Requester, Requested job
  title, Note) + a **Decision** select (Accept / Reject) + an optional **Response
  note** textarea (≤ 2000) + Send / Cancel footer buttons. The requester email is
  resolved on read and loaded into the modal on open (see §5 / §7).
- Empty state: `SimfEmptyState` ("No badge requests yet.") via the grid's
  `EmptyTemplate`.

### 4.5 Queue columns + actions

| Column | Sortable | Filterable | Notes |
|--------|----------|-----------|-------|
| Requester | yes | yes (Contains) | Requester display name; the email is **not** in the list row (PII detail-only, resolved on read) |
| Requested job title | yes | yes (Contains) | The title the user wants on their badge (1–128) |
| Status | yes | yes (enum) | Pill: Pending (amber) / Accepted (green) / Rejected (grey) |
| Submitted | yes | no | `CreatedAt`, rendered `yyyy-MM-dd HH:mm 'UTC'` |
| Responded | yes | no | `RespondedAt`, same UTC format, or "—" when unresolved |
| Actions | — | — | Quiet **Respond** (↩) icon on `Pending` rows only (gated `BadgeUpdateRequests.Manage`) |

**Actions on this page (no create / edit / delete):**

- **Respond → Accept** — `PUT /admin/badge-requests/{id}/respond` with
  `Status = Accepted` + optional note. Row flips to Accepted, **the requested title
  is written to the user's profile `JobTitle`**; success toast "Response sent." /
  "تم إرسال الردّ."
- **Respond → Reject** — same endpoint with `Status = Rejected` + optional note.
  Row flips to Rejected; the profile `JobTitle` is **not** changed.

## 5. Data flow + endpoints

1. **List** — the page posts `GridQuery` to
   `/account/api/admin/badge-requests/list` (BFF) →
   `POST /admin/badge-requests/list` (API). Rows carry requester name + requested
   job title + status + timestamps, **no email**.
2. **Detail (on Respond)** — opening the modal fires
   `GET /admin/badge-requests/{id}` which **adds the requester email** (resolved on
   read, PII on detail only).
3. **Respond** — Send fires `PUT /admin/badge-requests/{id}/respond` (body
   `{ Status, ResponseNote }`). On Accept the service applies `requestedJobTitle`
   to the user's profile `JobTitle`. The list reloads on success.

The actor id is resolved from the access-token `sub` claim on every endpoint for
the service's audit-log entry.

## 6. Validation + error handling

- **Pending → Pending is rejected.** A respond that does not move the request out
  of `Pending` returns **400 `BADGE_UPDATE_REQUEST_STATUS_INVALID`**. The CP modal
  only offers Accept / Reject, so this is reachable only via a scripted / malformed
  client; the API re-loads the row and re-validates status before writing.
- **Not found.** A respond / detail for an unknown id returns **404
  `BADGE_UPDATE_REQUEST_NOT_FOUND`**.
- **Public submit guard** (app side, surfaced for completeness):
  `BADGE_UPDATE_REQUEST_INVALID` (400, missing/over-length `requestedJobTitle`,
  which is required at 1–128 chars).
- **Load failure / 500 on `/list`** — a red fallback toast; no rows render and no
  empty-state shows (the load failed rather than returning an empty page).

## 7. Edge cases + known limitations

- **Accept mutates the profile.** This is the only one of the new request desks
  whose Accept has a side effect beyond the request row — it writes the
  `requestedJobTitle` to the user's profile `JobTitle`. Reject leaves the profile
  untouched.
- **Requester email is detail-only PII.** The list deliberately omits the requester
  email; it is resolved on read and fetched only when the Respond modal opens, via
  the audited GetById endpoint (the D-185 pattern).
- **Only Pending rows are actionable.** Resolved (Accepted / Rejected) rows show no
  Respond icon; the modal opens only for a `Pending` request.
- **A `View`-only admin cannot respond.** The Respond icon is wrapped in
  `<AuthorizedAction Permission="BadgeUpdateRequests.Manage">`, so a View-only admin
  never sees it; the PUT `/respond` independently enforces `Manage` as
  defence-in-depth.
- **Response-only.** Requests are created from the app, not the CP.

## 8. i18n + RTL

`Admin.BadgeRequests.*` keys (title, columns, status filters, the Respond modal
labels/buttons, loading + empty + load-failed copy) with full EN ↔ AR parity. RTL
mirrors the page, grid headers, per-column filter inputs, status pills, and the
Respond modal (title, Decision label/options, note label, footer buttons). The
status pills read قيد المراجعة / مقبول / مرفوض.

## 10. Use cases

- Review the pending queue and respond Accept/Reject to a badge update request; on
  Accept the requester's badge job title is corrected. (No create/edit/delete use
  case — submission is an app use case against the public endpoint.)

## 11. E2E

See [`docs/tests/e2e/cp-badge-requests.md`](../../tests/e2e/cp-badge-requests.md):
E2E-CPBR-001 golden (list → Respond Accept → profile JobTitle applied), 002 Respond
Reject + note (profile unchanged), 003 Pending→Pending 400 status-invalid, 004
list-omits-email / modal reveals detail, 005 status filter + only-Pending-actionable,
006 permission gate (non-Admin → `/not-permitted`; View-only sees no Respond icon;
PUT 403), 007 empty state, 008 RTL.

## 12. Related docs

- Sibling new desk: [`document-requests.md`](document-requests.md).
- Mirror: [`admin-speaker-meeting-requests.md`](admin-speaker-meeting-requests.md).
- App screen: [`docs/pages/mobile/requests.md`](../mobile/requests.md).
- Permissions: `PermissionCatalog.BadgeUpdateRequests.{View, Manage}` (baselined
  `AdminOnly`); guide `docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`.
- Decisions: D-500 (Wave 5 الطلبات unified requests + the two new desks).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-26 | D-500 | Original — `BadgeUpdateRequest` entity + additive table (migration `D500`) + CP review queue (SimfDataGrid + Respond modal) + public submit endpoint; Accept applies the requested title to the user's profile `JobTitle`. Response-only (no CRUD). Permissions `BadgeUpdateRequests.{View, Manage}`. |

_Last reviewed:_ 2026-06-26 by SIMF Team (D-500 — reference doc authored).
