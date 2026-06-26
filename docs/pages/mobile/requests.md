# Requests (الطلبات) — `/requests`

| | |
|--|--|
| **Route** | `/requests` (route name `requests`, `RouteNames.requests` → `RequestsScreen`) — Wave 5, Figma node `1408:9726` |
| **Layout** | KSA app shell (`KsaPage`), back chevron + centred title |
| **Surface** | Mobile App (Flutter) |
| **Audience** | Visitor (approved) — approved-account only |
| **Auth** | **Approved-only** — every backing endpoint requires an approved token; the user only ever sees and acts on their own requests |
| **Pattern** | Wave 5 (D-500) unified requests feed; a single screen over multiple request kinds with submit + self-cancel actions. **Supersedes** the D-479 read-only "My meetings" screen |
| **Status** | ✅ Real (D-500, Figma `1408:9726`) |
| **Implements use case(s)** | Track all of my requests in one place; request a participation document; request a badge job-title update; cancel my own pending requests |
| **Backend endpoints** | `GET /api/v1/app/my-requests` (feed) · `POST /api/v1/app/document-requests` (submit document) · `POST /api/v1/app/badge-requests` (submit badge) · `POST /api/v1/app/my-requests/cancel` (self-cancel). All **approved-only**. CP-side review: `/admin/document-requests` + `/admin/badge-requests`. |
| **Source file** | Flutter `features/requests/` screen + repository/model; API `app` my-requests / document-requests / badge-requests endpoints + the `ParticipationDocumentRequest` / `BadgeUpdateRequest` entities (`SimfAppDbContext`). |
| **Tests** | [`docs/tests/e2e/mobile-requests.md`](../../tests/e2e/mobile-requests.md) (`E2E-REQ-001..011`) |
| **Last reviewed** | 2026-06-26 |

---

## 1. Purpose

Requests (الطلبات) is the approved attendee's single place to **see and act on
every request they have made** to the forum. It unifies five request kinds in one
feed — a speaker meeting, a delegation meeting, a session attendance (surfaced from
the user's own seat bookings), a participation-document request, and a badge-update
request — and it adds two things the old "My meetings" screen did not: the user can
**submit** a new document or badge request, and can **cancel** their own pending
speaker / document / badge requests. It replaces the D-479 read-only My-meetings
screen entirely (that screen, its endpoint and its contract are removed). The
delegation-meeting and session-attendance rows remain read-only here — they are
managed elsewhere (the CP desk and the seat screens respectively).

## 2. Audience + permissions

- **Who can reach it:** an **approved** attendee, from My-Area (the "meetings" stat
  tile and an "الطلبات" More row).
- **Who can act on it:** the signed-in user, on their **own** requests only — they
  can submit a document/badge request and cancel their own **pending** speaker /
  document / badge requests.
- **Authorisation gates:** every endpoint (`GET /app/my-requests`,
  `POST /app/document-requests`, `POST /app/badge-requests`,
  `POST /app/my-requests/cancel`) is **approved-only**; an unauthenticated client
  gets **401**. The feed is caller-scoped — it never returns another user's
  requests, and a cancel of a non-owned request returns **404
  `APP_REQUEST_NOT_FOUND`**.
- **What an unauthenticated user sees:** nothing — the screen is for approved
  accounts only.

## 3. Screenshots

| State | File | Captured |
|-------|------|----------|
| Default (with cards) | `docs/screenshots/requests-default.png` | _pending on-device capture_ |
| New-request sheet | `docs/screenshots/requests-new.png` | _pending_ |
| Empty state | `docs/screenshots/requests-empty.png` | _pending_ |
| RTL (Arabic) | `docs/screenshots/requests-rtl.png` | _pending_ |

> Figma reference frame: `1408:9726`.

## 4. UI affordances

### 4.1 Header

Back chevron + centred title **الطلبات** ("Requests").

### 4.2 "طلب جديد" (New request) button

Opens a sheet to submit one of the two new request kinds:

| Submit | Endpoint | Body |
|--------|----------|------|
| Participation document | `POST /app/document-requests` | `{ documentType: int (0 = AttendanceCertificate, 1 = ParticipationLetter, 2 = InvitationLetter), note?: string ≤ 1000 }` |
| Badge update | `POST /app/badge-requests` | `{ requestedJobTitle: string 1–128 (required), note?: string ≤ 1000 }` |

On success a green toast reads "تم إرسال طلبك" / "Your request was submitted"; on
failure "تعذّر إرسال الطلب" / "Could not submit the request".

### 4.3 Status filter chips (with counts)

A row of chips, each carrying its count, that filter the feed:

| Chip (AR) | Chip (EN) | `MeetingRequestStatus` |
|-----------|-----------|------------------------|
| الكل | All | (no filter) |
| قيد المراجعة | Pending | Pending |
| مقبول | Accepted | Accepted |
| مرفوض | Rejected | Rejected |
| ملغى | Cancelled | Cancelled |

### 4.4 Request card (one per `AppRequestItem`)

| Element | Source field(s) | Notes |
|---------|-----------------|-------|
| Title | `title` / `titleArabic` | localized; expandable card |
| Status pill | `status` | `MeetingRequestStatus` — Pending / Accepted / Rejected / Cancelled |
| Date | `eventDateUtc` (else `createdAt`) | the event/slot date when present, else the submitted time |
| Cancel | `canCancel` | shown **only** for the user's own **pending** speaker / document / badge requests; confirm dialog → `POST /app/my-requests/cancel` |

The five kinds (`AppRequestItem.kind`): `SpeakerMeeting`, `DelegationMeeting`
(read-only), `SessionAttendance` (read-only — surfaced from the user's seat
bookings, **not** cancellable here), `ParticipationDocument` (new), `BadgeUpdate`
(new).

## 5. Cancel (self-cancel of own pending requests)

The Cancel affordance is shown only when `canCancel = true` — i.e. the user's own
**pending** speaker(0) / document(3) / badge(4) request. Tapping it opens a confirm
dialog; on confirm the screen posts `POST /app/my-requests/cancel`
(`{ kind: int, id: guid }`). On success a green toast reads "تم إلغاء الطلب" /
"Request cancelled" and the card flips to **ملغى** (Cancelled). A cancel of a
non-cancellable kind (delegation(1) / session-attendance(2)) or a non-pending
request returns **409 `APP_REQUEST_NOT_CANCELLABLE`**; a cancel of a non-owned
request returns **404 `APP_REQUEST_NOT_FOUND`**; either way the failure toast reads
"تعذّر إلغاء الطلب" / "Could not cancel the request".

## 6. Data flow

```
User opens /requests → screen calls GET /api/v1/app/my-requests (approved-only)
  → app my-requests service: gather the caller's
       SpeakerMeeting + DelegationMeeting + SessionAttendance (from seat bookings)
       + ParticipationDocument + BadgeUpdate requests, newest first
     → set canCancel = (own && pending && kind ∈ {speaker, document, badge})
  → ApiResult<List<AppRequestItem>> → cards + status chips render

طلب جديد → POST /app/document-requests | /app/badge-requests → 200 → feed reloads
Cancel (confirm) → POST /app/my-requests/cancel { kind, id } → 200 → card → ملغى
```

| When | Method + path | Request | Response shape |
|------|---------------|---------|----------------|
| Screen open | `GET /api/v1/app/my-requests` | — | `ApiResult<List<AppRequestItem>>` |
| New document request | `POST /api/v1/app/document-requests` | `{ documentType, note? }` | `ApiResult<…>` |
| New badge request | `POST /api/v1/app/badge-requests` | `{ requestedJobTitle, note? }` | `ApiResult<…>` |
| Cancel | `POST /api/v1/app/my-requests/cancel` | `{ kind, id }` | `ApiResult<…>` |

`AppRequestItem { kind, id, title, titleArabic, status, eventDateUtc?, createdAt,
canCancel }`.

## 7. States (loading / error / empty)

- **Loading:** a spinner while the GET is in flight.
- **Error:** an inline retry surface on a network / 5xx failure; Retry re-runs the
  call.
- **Empty:** the empty state when the user has no requests (the feed returns an
  empty list); the "طلب جديد" button stays available.
- **Cancelled:** a self-cancelled request keeps its card with the ملغى pill and
  loses the Cancel affordance.

## 8. i18n + RTL

All visible strings are localized (AR / EN): title "الطلبات" / "Requests", the
"طلب جديد" / "New request" button, the status chips (الكل / قيد المراجعة / مقبول /
مرفوض / ملغى → All / Pending / Accepted / Rejected / Cancelled), the submit toasts
("تم إرسال طلبك" / "Your request was submitted"; "تعذّر إرسال الطلب" / "Could not
submit the request") and the cancel toasts ("تم إلغاء الطلب" / "Request cancelled";
"تعذّر إلغاء الطلب" / "Could not cancel the request"). Card titles come from
`title` / `titleArabic` and switch with the locale. Under Arabic the header, the
button, the chips and the cards mirror right-to-left.

## 9. Edge cases + known limitations

- **Read-only kinds.** `DelegationMeeting` and `SessionAttendance` rows carry
  `canCancel = false` and show no Cancel — delegation meetings are managed on the
  CP desk (`/admin/delegation-meetings`) and session attendance is managed from the
  seat screens. A scripted cancel of either returns **409
  `APP_REQUEST_NOT_CANCELLABLE`**.
- **Cancel is own + pending only.** Only the user's own **pending** speaker /
  document / badge requests can be cancelled; a non-owned target → **404**, a
  non-pending target → **409**.
- **Badge job title is required.** A badge request with an empty `requestedJobTitle`
  → **400 `BADGE_UPDATE_REQUEST_INVALID`**; on admin **Accept** the requested title
  is written to the user's profile `JobTitle`.
- **Session attendance is a projection, not a new entity.** It is surfaced from the
  user's existing **seat bookings** (owner decision) — there is no new
  attendance-request table.

## 10. Related E2E test scenarios

See [`docs/tests/e2e/mobile-requests.md`](../../tests/e2e/mobile-requests.md)
(`E2E-REQ-001..011`): golden path (submit a document + a badge request, see them
pending, filter by chip, cancel a pending one), the five-kind caller-scoped feed,
status-chip filtering, read-only kinds (409 on cancel), empty state, the 401
auth-gate, validation (empty job title → 400), conflict (cancel already-cancelled
→ 409), cancel a non-owned request → 404, server-500 retry, and RTL.

## 11. Related docs

- CP review desks: [`docs/pages/cp/document-requests.md`](../cp/document-requests.md)
  and [`docs/pages/cp/badge-requests.md`](../cp/badge-requests.md) (where the
  document / badge requests are reviewed and responded to).
- Decisions log: **D-500** (this feed + the two new endpoints/tables + the
  `Cancelled` status value + self-cancel + the My-meetings removal). Related:
  D-479 (the superseded read-only My-meetings screen), D-269 (speaker meeting
  requests), D-478 (delegation meeting requests), D-157 (resolve-on-read).
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) — `app`
  endpoint group, `ApiResult<T>` envelope.

## 12. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-26 | D-500 | Wave 5 — new unified الطلبات requests screen (Figma `1408:9726`) + `GET /app/my-requests` (five kinds) + `POST /app/document-requests` + `POST /app/badge-requests` + `POST /app/my-requests/cancel`; `Cancelled` added to `MeetingRequestStatus` (additive); two new additive App tables (`ParticipationDocumentRequests`, `BadgeUpdateRequests`, migration `D500`). Supersedes and removes the D-479 read-only My-meetings screen / endpoint / contract. |

---

_Last reviewed:_ 2026-06-26 by SIMF Team (D-500 — requests screen reference doc).
