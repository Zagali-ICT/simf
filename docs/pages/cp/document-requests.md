# Participation document requests — `/admin/document-requests`

| | |
|--|--|
| **Route** | `/admin/document-requests` (`DocumentRequestsList.razor`) |
| **Audience** | Administrator |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.ParticipationDocumentRequests.View)]` (page); API endpoints gated `ParticipationDocumentRequests.View` (list + GetById) / `.Manage` (respond), all `+ RequireApprovedAccount` |
| **Pattern** | D-500 (Wave 5) review/approval queue (NOT full CRUD) — SimfDataGrid (server-paged, sortable + filterable) + a per-row Respond modal. **Mirrors** `SpeakerMeetingRequestsList.razor`. |
| **Status** | ✅ Real (D-500) |
| **Backend endpoints** | BFF passthroughs `/account/api/admin/document-requests/*` → API: `POST /admin/document-requests/list`, `GET /admin/document-requests/{id}`, `PUT /admin/document-requests/{id}/respond`. Public submit (app, not this page): `POST /api/v1/app/document-requests`. |
| **Backed by** | `dbo.ParticipationDocumentRequests` table (additive, migration `D500`). |
| **Tests** | [`docs/tests/e2e/cp-document-requests.md`](../../tests/e2e/cp-document-requests.md) (`E2E-CPDR-001..008`) |
| **Last reviewed** | 2026-06-26 |

## 1. Purpose

The admin review queue for **participation document requests** submitted by the
audience (D-500, Wave 5, app الطلبات screen `/requests`, Figma `1408:9726`). An
approved attendee asks for a participation document — an attendance certificate, a
participation letter, or an invitation letter — with an optional note. Those
requests land here as `Pending` rows; the administrator reviews each one and
**responds** (Accept or Reject) with an optional note (≤ 2000).

This is a **response-only queue, not a CRUD page** — there is no Add / Edit /
Delete from the Control Panel. Requests are *created* from the app (the public
submit endpoint) and *responded to* from the CP modal. The golden path is
therefore **list → Respond (Accept) → row flips to Accepted**, not the classic
"Add → Edit → Delete" round-trip.

It **mirrors** the speaker meeting requests desk
([`admin-speaker-meeting-requests.md`](admin-speaker-meeting-requests.md)) — same
SimfDataGrid + Respond-modal pattern and the same list-omits-email PII rule — and
sits alongside the sibling new desk
[`badge-requests.md`](badge-requests.md). The app side is
[`mobile/requests.md`](../mobile/requests.md).

## 4. UI

- Banner (`SimfBanner`) + a single owner-mandated **SimfDataGrid** (server-paged,
  per-column filter + sort, full pager). No Add toolbar button — this page does not
  create rows.
- Grid columns (see §4.5): Requester, Document type, Status (pill), Submitted
  (CreatedAt, UTC), Responded (RespondedAt, UTC or "—").
- Status pill (`SimfPill`): Pending = amber (`warn`), Accepted = green (`on`),
  Rejected = grey (`off`).
- Per-row **Respond** action — a quiet reply (↩) icon shown **only on `Pending`
  rows**, wrapped in `<AuthorizedAction Permission="ParticipationDocumentRequests.Manage">`.
  Resolved (Accepted / Rejected) rows show no action icon.
- **Respond modal** (`SimfModal`): a description list (Requester, Document type,
  Note) + a **Decision** select (Accept / Reject) + an optional **Response note**
  textarea (≤ 2000) + Send / Cancel footer buttons. The requester email is resolved
  on read and loaded into the modal on open (see §5 / §7).
- Empty state: `SimfEmptyState` ("No document requests yet.") via the grid's
  `EmptyTemplate`.

### 4.5 Queue columns + actions

| Column | Sortable | Filterable | Notes |
|--------|----------|-----------|-------|
| Requester | yes | yes (Contains) | Requester display name; the email is **not** in the list row (PII detail-only, resolved on read) |
| Document type | yes | yes (enum) | AttendanceCertificate / ParticipationLetter / InvitationLetter |
| Status | yes | yes (enum) | Pill: Pending (amber) / Accepted (green) / Rejected (grey) |
| Submitted | yes | no | `CreatedAt`, rendered `yyyy-MM-dd HH:mm 'UTC'` |
| Responded | yes | no | `RespondedAt`, same UTC format, or "—" when unresolved |
| Actions | — | — | Quiet **Respond** (↩) icon on `Pending` rows only (gated `ParticipationDocumentRequests.Manage`) |

**Actions on this page (no create / edit / delete):**

- **Respond → Accept** — `PUT /admin/document-requests/{id}/respond` with
  `Status = Accepted` + optional note. Row flips to Accepted; success toast
  "Response sent." / "تم إرسال الردّ."
- **Respond → Reject** — same endpoint with `Status = Rejected` + optional note.
  Row flips to Rejected.

## 5. Data flow + endpoints

1. **List** — the page posts `GridQuery` to
   `/account/api/admin/document-requests/list` (BFF) →
   `POST /admin/document-requests/list` (API). Rows carry requester name + document
   type + status + timestamps, **no email**.
2. **Detail (on Respond)** — opening the modal fires
   `GET /admin/document-requests/{id}` which **adds the requester email** (resolved
   on read, PII on detail only).
3. **Respond** — Send fires `PUT /admin/document-requests/{id}/respond` (body
   `{ Status, ResponseNote }`). The list reloads on success.

The actor id is resolved from the access-token `sub` claim on every endpoint for
the service's audit-log entry.

## 6. Validation + error handling

- **Pending → Pending is rejected.** A respond that does not move the request out
  of `Pending` returns **400 `PARTICIPATION_DOCUMENT_REQUEST_STATUS_INVALID`**. The
  CP modal only offers Accept / Reject, so this is reachable only via a scripted /
  malformed client; the API re-loads the row and re-validates status before
  writing.
- **Not found.** A respond / detail for an unknown id returns **404
  `PARTICIPATION_DOCUMENT_REQUEST_NOT_FOUND`**.
- **Public submit guard** (app side, surfaced for completeness):
  `PARTICIPATION_DOCUMENT_REQUEST_INVALID` (400, bad/missing `documentType` or an
  over-length note).
- **Load failure / 500 on `/list`** — a red fallback toast; no rows render and no
  empty-state shows (the load failed rather than returning an empty page).

## 7. Edge cases + known limitations

- **Requester email is detail-only PII.** The list deliberately omits the requester
  email; it is resolved on read and fetched only when the Respond modal opens, via
  the audited GetById endpoint (the D-185 pattern), consistent with the speaker
  meeting requests desk.
- **Only Pending rows are actionable.** Resolved (Accepted / Rejected) rows show no
  Respond icon; the modal opens only for a `Pending` request.
- **A `View`-only admin cannot respond.** The Respond icon is wrapped in
  `<AuthorizedAction Permission="ParticipationDocumentRequests.Manage">`, so a
  View-only admin never sees it; the PUT `/respond` independently enforces `Manage`
  as defence-in-depth.
- **Response-only.** Requests are created from the app, not the CP.

## 8. i18n + RTL

`Admin.DocumentRequests.*` keys (title, columns, status filters, document-type
labels, the Respond modal labels/buttons, loading + empty + load-failed copy) with
full EN ↔ AR parity. RTL mirrors the page, grid headers, per-column filter inputs,
status pills, and the Respond modal (title, Decision label/options, note label,
footer buttons). The status pills read قيد المراجعة / مقبول / مرفوض.

## 10. Use cases

- Review the pending queue and respond Accept/Reject to a document request. (No
  create/edit/delete use case — submission is an app use case against the public
  endpoint.)

## 11. E2E

See [`docs/tests/e2e/cp-document-requests.md`](../../tests/e2e/cp-document-requests.md):
E2E-CPDR-001 golden (list → Respond Accept), 002 Respond Reject + note, 003
Pending→Pending 400 status-invalid, 004 list-omits-email / modal reveals detail,
005 status filter + only-Pending-actionable, 006 permission gate (non-Admin →
`/not-permitted`; View-only sees no Respond icon; PUT 403), 007 empty state, 008
RTL.

## 12. Related docs

- Sibling new desk: [`badge-requests.md`](badge-requests.md).
- Mirror: [`admin-speaker-meeting-requests.md`](admin-speaker-meeting-requests.md).
- App screen: [`docs/pages/mobile/requests.md`](../mobile/requests.md).
- Permissions: `PermissionCatalog.ParticipationDocumentRequests.{View, Manage}`
  (baselined `AdminOnly`); guide `docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`.
- Decisions: D-500 (Wave 5 الطلبات unified requests + the two new desks).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-26 | D-500 | Original — `ParticipationDocumentRequest` entity + additive table (migration `D500`) + CP review queue (SimfDataGrid + Respond modal) + public submit endpoint. Response-only (no CRUD). Permissions `ParticipationDocumentRequests.{View, Manage}`. |

_Last reviewed:_ 2026-06-26 by SIMF Team (D-500 — reference doc authored).
