# Page 017 — API (تفاصيل الجلسة · Session detail)

Authoritative backend contract for this page. Inherits the `ApiResult<T>`
envelope, headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001
§3–§4. The render/seat-card rules are in [Page_017_Logic.md](Page_017_Logic.md).

> **Status:** **BUILT — no new API (D-265).** Every element of this page is served
> by endpoints that already shipped: the session content from the cached agenda
> payload + `GET /app/programme/sessions/{id}` (D-199/D-252), and the my-seat card
> from the seat-map's `MyCell` (D-175). The two CTAs (add-to-calendar, reminder)
> are **client-local OS actions** — no endpoint (Page_017_Logic L-5).
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split,
> D-247) — so the routes below are `GET /api/v1/app/programme/sessions/{id}` etc.

## E1 — the session content (from the p16 cache)  **(BUILT — D-252)**
The header / tags / description / speaker cards render from the **cached
`PublicSessionListItem`** the agenda already holds (Page_016 E1). No call is made
on open. Mapping:

| Screen 17 element | Cached field |
|---|---|
| index + time band | `Code`, `StartUtc`, `EndUtc` |
| title | `Title` / `TitleArabic` |
| hall tag | `HallName` / `HallNameArabic` |
| category tag (`جلسة رئيسية`) | `CategoryName` / `CategoryNameArabic` (D-226) |
| description (`وصف الجلسة`) | `Description` / `DescriptionArabic` |
| speaker cards | `Speakers[]` (`PublicSessionSpeaker`) |

## E2 — `GET /app/programme/sessions/{id}`  (live refresh)  **(BUILT — D-199)**
| | |
|---|---|
| Full route | `GET /api/v1/app/programme/sessions/{id:guid}` |
| Access | **`AllowAnonymous`** — guest can read the detail |
| Returns | `ApiResult<PublicSessionDetail>` — everything the list carries **plus** the full `themes[]`, the live `seats` summary, `status`/`publishedAt`, and `hasRecording`. **404** (`SessionNotFound`) when missing / soft-deleted. |
| When | Optional — the app calls it to refresh the **live seat-availability count** / themes / recording flag; the first paint is from the cache (Page_017_Logic L-1). |

```jsonc
// PublicSessionDetail (abridged — see Page_016_API E2 for the shared fields)
{
  "id": "guid", "code": "string",
  "title": "string", "titleArabic": "string",
  "description": "string?", "descriptionArabic": "string?",
  "hallId": "guid", "hallName": "string", "hallNameArabic": "string",
  "startUtc": "2026-11-03T06:00:00Z", "endUtc": "2026-11-03T07:30:00Z",
  "themes":   [ { "id": "guid", "name": "string", "nameArabic": "string", "color": "#RRGGBB" } ],
  "speakers": [ { "id": "guid", "name": "string", "nameArabic": "string",
                  "title": "string?", "displayOrder": 0, "role": "Speaker" } ],
  "seats":    { "capacity": 0, "reserved": 0, "available": 0 },
  "categoryId": "guid?", "categoryName": "string?", "categoryNameArabic": "string?",
  "status": "Scheduled", "publishedAt": "string?", "hasRecording": false
}
```

## E3 — `GET /app/sessions/{sessionId}/seats`  (my-seat card)  **(BUILT — D-175)**
| | |
|---|---|
| Full route | `GET /api/v1/app/sessions/{sessionId:guid}/seats` |
| Access | **`RequireApprovedAccount`** — an approved, signed-in account |
| Returns | `ApiResult<SessionSeatMap>` — the full seat grid **and** `myCell` (the caller's own active seat, or `null`) |
| Used for | screen 17's `مقعدي` card reads **`myCell`**; the **same** payload draws screen 18's grid (Page_017_Logic L-4) |

```jsonc
// SessionSeatMap
{
  "sessionId": "guid", "hallId": "guid",
  "hallCapacity": 0, "sessionCapacity": null,
  "rowLabels": ["A","B","C"], "seatsPerRow": 12,
  "reservedCells": [ { "reservationId": "guid", "rowLabel": "C", "seatNumber": 5, "kind": "UserBooking" } ],
  "myCell": {                       // ← the مقعدي card; null when the caller has no seat
    "reservationId": "guid",
    "rowLabel": "B",                //   "الصف B"  — the row label (string)
    "seatNumber": 12,               //   "مقعد 12" — the 1-based seat number within the row
    "kind": "UserBooking"
  },
  "activeReservedCount": 0
}
```

### Notes
- **The card shows only when `myCell != null`.** Guest / pending callers do not
  call this endpoint (guest = no token; pending = 403), so they see no card
  (Page_017_Logic L-3).
- **`rowLabel` + `seatNumber` are the whole "location"** — there is no column
  field (Page_017_Logic L-3.1). The app must not synthesise one.
- `myCell.kind` is `UserBooking` (self-pick) or `RandomAssignment` (random
  allocate); both are the caller's own seat.

## E4 — the two functions are client-local (no endpoint)
| CTA | Action | Server call |
|---|---|---|
| `أضف إلى تقويمي` (add to calendar) | build one calendar event from the cached session (title / start / end / hall = location) → device add-event intent | **none** |
| `تذكير` (reminder) | schedule a **local** notification at `startUtc − lead-time` | **none** |

Both work offline because every field is in the cached session (Page_017_Logic
L-5). This is **not** the Page_014 `.ics` (which aggregates many server-side
sources) and **not** the server reminder worker (D-217).

## Error responses
| HTTP | When |
|------|------|
| 401 | (seats) no/expired token — the app simply hides the my-seat card |
| 403 | (seats) account not approved (pending/rejected) — card hidden |
| 404 | (detail) session missing / soft-deleted (`SessionNotFound`) |

## Build dependencies
**None.** All endpoints exist and are tested
(`tests/SIMF.Api.Tests/ProgrammeSessionsTests.cs` for the detail;
`tests/SIMF.Api.Tests/SeatReservationsTests.cs` for `MyCell`). No schema change,
no migration, no new permission — this page is a **reuse** of the shipped surface
(D-265).
