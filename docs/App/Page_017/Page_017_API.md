# Page 017 — API (تفاصيل الجلسة · Session detail)

Authoritative backend contract for this page. Inherits the `ApiResult<T>`
envelope, headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001
§3–§4. The render/seat-card rules are in [Page_017_Logic.md](Page_017_Logic.md).

> **Status:** **BUILT — no new endpoint for this page (D-265).** Every element of
> this page is served by endpoints that already shipped: the session content from
> the cached sessions-list payload + `GET /app/programme/sessions/{id}`
> (D-199/D-252), and the my-seat card from the seat-map's `MyCell` (D-175). The two
> CTAs (add-to-calendar, reminder) are **client-local OS actions** — no endpoint
> (Page_017_Logic L-5). **D-271** appends (append-only, D-219) the speaker
> **country + photo** and the session **live-stream URLs** to the existing detail /
> list payloads — no new route, no schema break.
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
| speaker country **flag** | `Speakers[].CountryId` (int?) — client renders the flag from the id (D-271) |
| speaker country **label** | `Speakers[].CountryNameEn` / `CountryNameAr` (string?) — label / fallback |
| speaker **avatar** | `Speakers[].PhotoRelativePath` (string?) — placeholder when null |

## E2 — `GET /app/programme/sessions/{id}`  (live refresh)  **(BUILT — D-199)**
| | |
|---|---|
| Full route | `GET /api/v1/app/programme/sessions/{id:guid}` |
| Access | **`AllowAnonymous`** — guest can read the detail |
| Returns | `ApiResult<PublicSessionDetail>` — everything the list carries (incl. the speaker **country + photo**, D-271) **plus** the full `themes[]`, the live `seats` summary, `status`/`publishedAt`, `hasRecording`, and the **live-stream URLs** (`liveStreamUrl` + `liveSignLanguageUrl`, D-271). **404** (`SessionNotFound`) when missing / soft-deleted. |
| When | Optional — the app calls it to refresh the **live seat-availability count** / themes / recording flag / live-stream URLs; the first paint is from the cache (Page_017_Logic L-1). |

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
                  "title": "string?", "displayOrder": 0, "role": "Speaker",
                  // --- D-271 append-only (D-219): country flag + photo on the speaker ---
                  "countryId": null, "countryNameEn": null, "countryNameAr": null,
                  "photoRelativePath": null } ],
  "seats":    { "capacity": 0, "reserved": 0, "available": 0 },
  "categoryId": "guid?", "categoryName": "string?", "categoryNameArabic": "string?",
  "status": "Scheduled", "publishedAt": "string?", "hasRecording": false,
  // --- D-271 append-only (D-219): live-stream stub (drives screen 25) ---
  "liveStreamUrl": null,         // string? → non-null = the session has a LIVE broadcast (LIVE player + badge); null = recorded/scheduled
  "liveSignLanguageUrl": null    // string? → optional sign-language feed (the live screen's لغة الإشارة toggle)
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

## E5 — related session surfaces (documented here, served elsewhere) — D-271
Screen 17 **links to** these adjacent session surfaces; their full contracts live
on their own screens. Listed so the contract is discoverable from the detail
(Page_017_Logic L-9):

| Surface | Endpoint / field | Rules | Owning screen |
|---|---|---|---|
| **Ask a question** | `POST /api/v1/app/sessions/{id}/questions` (`RequireApprovedAccount`) | open only **5 min before `StartUtc` → `EndUtc`** (`PreStartWindow = 5`, `PostEndWindow = 0`) **and** the attendee is arrived at the hall (geofence → `HallAttendance`, else `IsAtVenue` — D-242); outside → **400 `SESSION_NOT_LIVE_FOR_QUESTIONS`**. Tested in `SessionQuestionsTests`. | **screen 26** (Q&A) |
| **Live broadcast** | `PublicSessionDetail.liveStreamUrl` (+ `liveSignLanguageUrl`) — E2 | non-null `liveStreamUrl` = LIVE (player + badge); null = recorded/scheduled. `liveSignLanguageUrl` drives the live screen's لغة الإشارة toggle. Interim **manual-URL stub provider**, a managed provider replaces it later (deferred, D-211 D7). Tested in `ProgrammeSessionsTests.Session_detail_carries_live_stream_urls_when_set`. | **screen 25** (Live / player) |
| **Recording + AI summary** | `PublicSessionDetail.hasRecording` (token-gated recording, D-232) · `GET /api/v1/app/programme/sessions/{id}/summary` (محضر — D-237/238, anonymous, gated by the summary's `publishedAt`) | recorded sessions stream via the token-gated endpoint; the AI summary surfaces when published. | **screen 25** (Live / player) |
| **Audience comments** | two-stage: (1) AI filter on submit (`ICommentAiFilter` stub → **Approved** / **Pending**) → (2) admin **approve / hide** in CP `CommentsModerationList` (`/admin/comments-moderation`) | the standalone app comments screen (28) is **removed** (updated mockup) — comments surface **inside** the session / live screen (25). | **screen 25** (Live / player) + CP moderation |

## Error responses
| HTTP | When |
|------|------|
| 401 | (seats) no/expired token — the app simply hides the my-seat card |
| 403 | (seats) account not approved (pending/rejected) — card hidden |
| 404 | (detail) session missing / soft-deleted (`SessionNotFound`) |

## Build dependencies
**None for this page.** All endpoints this page reads exist and are tested
(`tests/SIMF.Api.Tests/ProgrammeSessionsTests.cs` for the detail — incl.
`Session_speaker_carries_country_flag_and_photo` and
`Session_detail_carries_live_stream_urls_when_set`;
`tests/SIMF.Api.Tests/SeatReservationsTests.cs` for `MyCell`). The D-271 speaker
**country + photo** fields are append-only over existing tables. The D-271
**live-stream URLs** added two additive nullable columns
(`Session.LiveStreamUrl` + `Session.LiveSignLanguageUrl`, migration **D271**),
owned by the live screen (25); this page only **reads** them. No new endpoint, no
new permission for screen 17 — it is a **reuse** of the shipped surface (D-265).
