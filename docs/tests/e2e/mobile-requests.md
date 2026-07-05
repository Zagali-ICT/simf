# E2E test catalogue — `Requests` (`requests`)

> **Authority:** SIMF E2E test catalogue (D-133 / D-245). Mobile catalogue —
> data-driven from `GET /app/my-requests` (**approved-only**), the **Wave 5**
> unified requests feed (الطلبات, D-500, Figma node **`1408:9726`**). This screen
> **supersedes** the old read-only "My meetings" screen (`/my-meetings`,
> `GET /app/my-meetings`, D-479) which is removed. The feed unifies **five** request
> kinds in one place — `SpeakerMeeting`, `DelegationMeeting` (read-only),
> `SessionAttendance` (surfaced from the user's seat bookings), `ParticipationDocument`
> (new), and `BadgeUpdate` (new) — lets the user **submit** a new document/badge request
> and **cancel** their own pending speaker/document/badge requests. API implementation
> lives in `tests/SIMF.Api.Tests/MyRequestsTests.cs`,
> `ParticipationDocumentRequestsTests.cs`, and `BadgeUpdateRequestsTests.cs`.

| | |
|--|--|
| **Page** | [`mobile/requests.md`](../../pages/mobile/requests.md) (app screen `requests`) |
| **Route** | `/requests` (`RouteNames.requests` → `RequestsScreen`) — reached from My-Area (the "meetings" stat tile + an "الطلبات" More row) |
| **APIs** | `GET /api/v1/app/my-requests` (approved-only) → `AppRequestItem { kind, id, title, titleArabic, status, eventDateUtc?, createdAt, canCancel }[]`; `POST /api/v1/app/document-requests`; `POST /api/v1/app/badge-requests`; `POST /api/v1/app/my-requests/cancel`. All **approved-only**. |
| **Surface** | Mobile (Flutter) + App API |
| **Figma** | `1408:9726` |
| **Auth setup** | An **approved Visitor** token (the user only ever sees and acts on their own requests). No literal secrets. |
| **Last reviewed** | 2026-06-26 |

## Layout

- **Header**: back chevron + centred title **اللقاءات الثنائية** ("Bilateral
  meetings") — Figma 1408:9726 frame header (D-595, was "الطلبات").
- **Top button row (2, D-595)**: **"طلب جديد"** (New request, beige-outlined,
  clipboard glyph) opens a sheet to submit a **document request**
  (`ParticipationDocument`) or a **badge-update request** (`BadgeUpdate`); **"السجل"**
  (Log, gold-filled, history glyph) clears any status filter → all requests. Accepted
  is filtered via the "مقبول (N)" status chip (the earlier "المقبولة" button was dropped).
- **Status filter chips with counts**: الكل / قيد المراجعة / مقبول / مرفوض / ملغى
  (All / Pending / Accepted / Rejected / Cancelled), each showing the count of
  requests in that status; tapping a chip filters the feed.
- **Request cards** (expandable, across the five kinds): each card shows the
  bilingual title, a status pill (`MeetingRequestStatus`), the date
  (`eventDateUtc` when present, else `createdAt`), and — for the user's **own
  pending** speaker / document / badge requests — a **Cancel** affordance. The
  `DelegationMeeting` and `SessionAttendance` kinds are **read-only** on this
  screen (no Cancel; `canCancel = false`).
- **Cancel** confirms in a dialog, then `POST /app/my-requests/cancel` flips the
  request to **ملغى** (Cancelled) and shows a success toast.
- **States**: spinner while loading; an inline retry surface on a wire error; and
  the empty state when the user has no requests.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-REQ-001 | Golden path — open feed → طلب جديد → submit a document request → submit a badge request → both show Pending → filter by chip → cancel a pending one → toast + status becomes ملغى | happy | P0 | authored ✓ (`MyRequestsTests` + `ParticipationDocumentRequestsTests` + `BadgeUpdateRequestsTests`, API) |
| E2E-REQ-002 | The feed unifies all five kinds and is scoped to the caller (never another user's requests) | happy | P0 | authored ✓ (`MyRequestsTests`, API) |
| E2E-REQ-003 | Status filter chips show per-status counts and narrow the feed; الكل clears the filter | happy | P1 | _to author_ |
| E2E-REQ-004 | `SessionAttendance` (from seat bookings) + `DelegationMeeting` are read-only — no Cancel; cancel attempt → 409 `APP_REQUEST_NOT_CANCELLABLE` | data | P0 | authored ✓ (`MyRequestsTests`, API) |
| E2E-REQ-005 | Empty state — the user has no requests → the empty state, no error | empty | P1 | _to author_ |
| E2E-REQ-006 | Auth gate — an unauthenticated client → 401 on every requests endpoint | auth | P0 | authored ✓ (`MyRequestsTests`, API) |
| E2E-REQ-007 | Validation — submit a badge request with an empty job title → 400 `BADGE_UPDATE_REQUEST_INVALID` | error | P1 | authored ✓ (`BadgeUpdateRequestsTests`, API) |
| E2E-REQ-008 | Conflict — cancel an already-cancelled request → 409 `APP_REQUEST_NOT_CANCELLABLE` | error | P1 | authored ✓ (`MyRequestsTests`, API) |
| E2E-REQ-009 | Cancel a non-owned request → 404 `APP_REQUEST_NOT_FOUND` | auth | P1 | authored ✓ (`MyRequestsTests`, API) |
| E2E-REQ-010 | Server 500 on `GET /app/my-requests` → inline retry surface, no rows | resilience | P2 | _to author_ |
| E2E-REQ-011 | RTL render (Arabic) — header, طلب جديد button, status chips, cards + Cancel mirror right-to-left | i18n | P1 | _to author_ |

## Scenarios

### E2E-REQ-001 — Golden path: submit a document + a badge request, then cancel one

```gherkin
Feature: Requests unified feed golden path (Figma 1408:9726, GET /app/my-requests)
  As an approved attendee
  I want to submit and track my requests in one place
  So that I can ask for a participation document or a badge update and follow their status

Background:
  Given an approved Visitor is signed in to the app
  And the user opens /requests from My-Area (the "meetings" stat tile)

Scenario: Submit a document request, a badge request, then cancel a pending one
  When the screen loads
  Then it calls GET /api/v1/app/my-requests with the user's token
  And the feed renders with the status filter chips (الكل / قيد المراجعة / مقبول / مرفوض / ملغى) and their counts

  When the user taps "طلب جديد" (New request)
  And chooses a document request, picks documentType = ParticipationLetter (1), adds the note "For my employer."
  And submits
  Then POST /api/v1/app/document-requests fires with { documentType: 1, note: "For my employer." }
  And the API returns 200
  And a green toast reads "تم إرسال طلبك" / "Your request was submitted"
  And the feed reloads and a ParticipationDocument card appears with the قيد المراجعة (Pending) pill

  When the user taps "طلب جديد" again
  And chooses a badge-update request, enters requestedJobTitle "Lead Naval Architect", adds a note
  And submits
  Then POST /api/v1/app/badge-requests fires with { requestedJobTitle: "Lead Naval Architect", note: ... }
  And the API returns 200
  And a green toast reads "تم إرسال طلبك" / "Your request was submitted"
  And a BadgeUpdate card appears with the قيد المراجعة (Pending) pill

  When the user taps the "قيد المراجعة" (Pending) status chip
  Then only Pending cards render

  When the user taps Cancel on the BadgeUpdate card and confirms in the dialog
  Then POST /api/v1/app/my-requests/cancel fires with { kind: 4, id: <badgeRequestId> }
  And the API returns 200
  And a green toast reads "تم إلغاء الطلب" / "Request cancelled"
  And the BadgeUpdate card's status becomes ملغى (Cancelled) and it loses the Cancel affordance
```

**Evidence:** `ParticipationDocumentRequestsTests` (submit document request → Pending), `BadgeUpdateRequestsTests` (submit badge request → Pending), and `MyRequestsTests` (feed surfaces both + self-cancel flips to Cancelled) — all green at the API layer.

### E2E-REQ-002 — The feed unifies five kinds and is scoped to the caller

```gherkin
Scenario: The feed returns the user's own requests across all five kinds, never another user's
  Given an approved Visitor who has:
    a SpeakerMeeting request, a DelegationMeeting request, a SessionAttendance (from a seat booking),
    a ParticipationDocument request and a BadgeUpdate request
  And another user has their own ParticipationDocument request
  When the app calls GET /api/v1/app/my-requests with the first user's token
  Then the response is 200
  And it returns AppRequestItem rows for all five of the caller's kinds (kind, id, title, titleArabic, status, eventDateUtc?, createdAt, canCancel)
  And it does NOT return the other user's request
  And only the caller's pending SpeakerMeeting / ParticipationDocument / BadgeUpdate rows carry canCancel = true
  And the DelegationMeeting and SessionAttendance rows carry canCancel = false
```

**Evidence:** `MyRequestsTests` (green) — the feed is caller-scoped and projects the five kinds with the correct `canCancel` per kind.

### E2E-REQ-003 — Status filter chips with counts

```gherkin
Scenario: The status chips show counts and filter the feed
  Given the feed has 2 Pending, 1 Accepted, 1 Rejected and 1 Cancelled request
  Then the chip "قيد المراجعة" / "Pending" shows the count 2
  And "مقبول" / "Accepted" shows 1, "مرفوض" / "Rejected" shows 1, "ملغى" / "Cancelled" shows 1
  And "الكل" / "All" shows 5
  When the user taps "مقبول" / "Accepted"
  Then only the single Accepted card renders
  When the user taps "الكل" / "All"
  Then every card renders again
```

### E2E-REQ-004 — Read-only kinds are not cancellable

```gherkin
Scenario: SessionAttendance and DelegationMeeting are read-only on this screen
  Given the user has a SessionAttendance request (surfaced from a seat booking) and a DelegationMeeting request
  Then neither card shows a Cancel affordance (canCancel = false)
  When a scripted client POSTs /api/v1/app/my-requests/cancel with { kind: 2, id: <sessionAttendanceId> } (SessionAttendance)
  Then the API returns 409 with error code APP_REQUEST_NOT_CANCELLABLE
  When a scripted client POSTs /api/v1/app/my-requests/cancel with { kind: 1, id: <delegationId> } (DelegationMeeting)
  Then the API returns 409 with error code APP_REQUEST_NOT_CANCELLABLE
  # SessionAttendance is managed from the seat screens, not cancelled here.
```

**Evidence:** `MyRequestsTests` (green) — cancel of a non-cancellable kind returns 409 `APP_REQUEST_NOT_CANCELLABLE`.

### E2E-REQ-005 — Empty state

```gherkin
Scenario: A user with no requests sees the empty state
  Given the user has no requests of any kind
  When the user opens /requests
  Then GET /api/v1/app/my-requests returns 200 with an empty list
  And the screen shows the empty state (no cards, no error surface)
  And the "طلب جديد" (New request) button is still available
```

### E2E-REQ-006 — Auth gate (unauthenticated → 401)

```gherkin
Scenario: The requests endpoints require an approved token
  Given no session (no bearer token)
  When a client calls GET /api/v1/app/my-requests
  Then the API returns 401 (no payload)
  And the same 401 applies to POST /app/document-requests, POST /app/badge-requests, and POST /app/my-requests/cancel
  # All four endpoints are approved-only.
```

**Evidence:** `MyRequestsTests` (green) — the requests endpoints reject an unauthenticated caller.

### E2E-REQ-007 — Validation (empty job title → 400)

```gherkin
Scenario: A badge request needs a job title
  Given an approved Visitor is signed in
  When they submit a badge-update request with an empty requestedJobTitle
  Then POST /api/v1/app/badge-requests returns 400
  And ApiResult.Error.Code = "BADGE_UPDATE_REQUEST_INVALID"
  And no BadgeUpdate row is created
  And in the app the submit fails with the toast "تعذّر إرسال الطلب" / "Could not submit the request"
  # requestedJobTitle is required (1–128 chars); on admin Accept it is applied to the user's profile JobTitle.
```

**Evidence:** `BadgeUpdateRequestsTests` (green) — an invalid (empty job title) badge request returns 400 `BADGE_UPDATE_REQUEST_INVALID`.

### E2E-REQ-008 — Conflict (cancel an already-cancelled request → 409)

```gherkin
Scenario: Cancelling a non-pending request is rejected
  Given the user has a ParticipationDocument request that is already Cancelled (ملغى)
  When the user (or a scripted client) POSTs /api/v1/app/my-requests/cancel with { kind: 3, id: <documentId> }
  Then the API returns 409 with error code APP_REQUEST_NOT_CANCELLABLE
  And the request stays Cancelled
  And in the app the cancel fails with the toast "تعذّر إلغاء الطلب" / "Could not cancel the request"
  # Only OWN PENDING speaker(0) / document(3) / badge(4) requests are cancellable.
```

**Evidence:** `MyRequestsTests` (green) — cancelling a request that is not pending returns 409 `APP_REQUEST_NOT_CANCELLABLE`.

### E2E-REQ-009 — Cancel a non-owned request → 404

```gherkin
Scenario: A user cannot cancel a request they do not own
  Given a ParticipationDocument request owned by another user (id known)
  When a different approved Visitor POSTs /api/v1/app/my-requests/cancel with { kind: 3, id: <otherUsersDocumentId> }
  Then the API returns 404 with error code APP_REQUEST_NOT_FOUND
  And the other user's request is unchanged
```

**Evidence:** `MyRequestsTests` (green) — cancelling a request not owned by the caller returns 404 `APP_REQUEST_NOT_FOUND`.

### E2E-REQ-010 — Server 500 → inline retry

```gherkin
Scenario: A failed feed load shows the retry surface
  Given GET /app/my-requests fails (network / 5xx)
  When the user opens /requests
  Then the screen shows an inline retry surface (no cards)
  When the user taps Retry and the call succeeds
  Then the cards and the status chips render
```

### E2E-REQ-011 — RTL render (Arabic)

```gherkin
Scenario: The screen mirrors under Arabic
  Given the app language is Arabic
  Then the header title reads "اللقاءات الثنائية"
  And the "طلب جديد" button, the status chips (الكل / قيد المراجعة / مقبول / مرفوض / ملغى) and the cards mirror right-to-left
  And each card's title renders from titleArabic and the status pill reads قيد المراجعة / مقبول / مرفوض / ملغى
  When the user switches to English
  Then the labels flip to "Requests" / "New request" / All / Pending / Accepted / Rejected / Cancelled
```

---

## Implementation notes

- **API integration tests** at
  [`tests/SIMF.Api.Tests/MyRequestsTests.cs`](../../../tests/SIMF.Api.Tests/MyRequestsTests.cs),
  [`tests/SIMF.Api.Tests/ParticipationDocumentRequestsTests.cs`](../../../tests/SIMF.Api.Tests/ParticipationDocumentRequestsTests.cs),
  and [`tests/SIMF.Api.Tests/BadgeUpdateRequestsTests.cs`](../../../tests/SIMF.Api.Tests/BadgeUpdateRequestsTests.cs)
  cover the same surface at the API layer: the unified feed (caller-scoped, five
  kinds, per-kind `canCancel`), the document + badge submits + their validation
  (`PARTICIPATION_DOCUMENT_REQUEST_INVALID`, `BADGE_UPDATE_REQUEST_INVALID`), and
  the self-cancel guards (`APP_REQUEST_NOT_FOUND` 404, `APP_REQUEST_NOT_CANCELLABLE`
  409). The Flutter screen adds the chip-filter, expandable cards, confirm dialog,
  toast text and RTL coverage that the API tests cannot assert.
- **Backing surface:**
  - Feed — `GET /api/v1/app/my-requests` (approved-only) → list of
    `AppRequestItem { kind, id, title, titleArabic, status, eventDateUtc?,
    createdAt, canCancel }`. Kinds: `SpeakerMeeting`, `DelegationMeeting`
    (read-only), `SessionAttendance` (surfaced from the user's seat bookings, not
    cancellable here), `ParticipationDocument`, `BadgeUpdate`.
  - Submit document — `POST /api/v1/app/document-requests`
    (`{ documentType: int (0 = AttendanceCertificate, 1 = ParticipationLetter,
    2 = InvitationLetter), note?: string ≤ 1000 }`).
  - Submit badge — `POST /api/v1/app/badge-requests`
    (`{ requestedJobTitle: string 1–128 (required), note?: string ≤ 1000 }`); on
    admin **Accept** the title is applied to the user's profile `JobTitle`.
  - Self-cancel — `POST /api/v1/app/my-requests/cancel` (`{ kind: int, id: guid }`)
    — only **own pending** speaker(0) / document(3) / badge(4); delegation(1) /
    session-attendance(2) → 409.
  - Status — `MeetingRequestStatus` = Pending (قيد المراجعة) / Accepted (مقبول) /
    Rejected (مرفوض) / Cancelled (ملغى — added in D-500, additive value `3`).
  - Error codes — `PARTICIPATION_DOCUMENT_REQUEST_INVALID` / `_NOT_FOUND` /
    `_STATUS_INVALID`, `BADGE_UPDATE_REQUEST_INVALID` / `_NOT_FOUND` /
    `_STATUS_INVALID`, `APP_REQUEST_NOT_FOUND` (404), `APP_REQUEST_NOT_CANCELLABLE`
    (409).
  - Bilingual toasts — submit success "تم إرسال طلبك" / "Your request was
    submitted"; submit fail "تعذّر إرسال الطلب" / "Could not submit the request";
    cancel success "تم إلغاء الطلب" / "Request cancelled"; cancel fail
    "تعذّر إلغاء الطلب" / "Could not cancel the request".
- **Supersedes My meetings.** The Wave-5 الطلبات feed replaces the D-479 read-only
  "My meetings" screen (`/my-meetings`, `GET /app/my-meetings`) — that screen,
  endpoint, contract and its catalogue file (`mobile-my-meetings.md`,
  `E2E-MMM-*`) are removed. The CP-side desks for the two new kinds are
  [`cp-document-requests.md`](cp-document-requests.md) and
  [`cp-badge-requests.md`](cp-badge-requests.md).

---

_Last reviewed:_ `2026-06-26` by `SIMF Team` — D-500 Wave 5 unified requests feed
(الطلبات, Figma `1408:9726`): five-kind feed + document/badge submit + self-cancel;
supersedes the D-479 My-meetings screen.
