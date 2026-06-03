# Page 017 — Logic (تفاصيل الجلسة · Session detail)

Business rules behind the session detail. Verified against the public programme
reads (D-199 + D-252) and the per-session seat reservations (D-175). **No new
backend behaviour** — this page composes endpoints that already exist (D-265).

## L-1 Render from the cache; the session body comes from p16
The owner rule: **"details about the session can be got from p16."**
- The agenda (Page_016) fetches the **whole programme once** and caches it. Each
  cached `PublicSessionListItem` already carries **Title, Code, time, Hall (EN/AR),
  category, Description (body) and the ordered Speakers** (enriched in D-252).
- So tapping a session opens the detail **immediately from the cached item** — no
  fetch is needed to draw the header, tags, description and speaker cards.
- The app **may** call `GET /app/programme/sessions/{id}` (`PublicSessionDetail`)
  when it wants the **live seat-availability summary**, the full theme list, or the
  `hasRecording` flag — fields the list row does not carry. This is a refresh, not
  a prerequisite for the first paint.

## L-2 The "type" tag = SessionCategory (D-226)
The `جلسة رئيسية` / "Main session" tag is the **session category** — a dynamic
lookup (D-226), confirmed by the mockup tag and the Screen Guide ("Tags: hall
location + session category"). "Main Session" is **one seeded category value**,
not a boolean — there is no `IsMain` field. Null until the team seeds the list →
the tag is hidden. (Same rule as Page_016 L-4.)

## L-3 The `مقعدي` (my-seat) card — login + reservation only
The card shows the caller's **own** reserved seat for this session.
- Source: `GET /app/sessions/{sessionId}/seats` → `SessionSeatMap.MyCell`
  (`SessionSeatCell?`). The service sets `MyCell` to the reservation whose
  `ReservedForUserId == the caller` and `ReleasedAt == null`; it is **null** when
  the caller has none.
- This endpoint requires an **approved account** (`RequireApprovedAccount`), so:
  - **Guest / not-logged-in** → the app does not call it → **no card**.
  - **Pending / rejected** → 403 → **no card**.
  - **Approved, no booking** → `MyCell == null` → **no card**.
  - **Approved, with a booking** → `MyCell` → the card renders.
- The card text is **`الصف {RowLabel} · مقعد {SeatNumber}`** (Row B · Seat 12).

### L-3.1 "Location (row / column)" = RowLabel + SeatNumber — there is no column axis
The owner said "seat number, location (row / column)". The seat model
(`SeatReservation`) stores **`RowLabel`** (a string, e.g. `"B"` / `"VIP"`) and
**`SeatNumber`** (a 1-based position *within* the row). There is **no separate
column field** — the "location" is the row label plus the seat-number-within-row,
exactly as the mockup renders it (`الصف B · مقعد 12`). The app must **not** invent
a column coordinate.

## L-4 `عرض ←` → My Seat map (18) reuses the same payload
The `عرض ←` link opens **My Seat map (screen 18)** at
`/agenda/:sessionId/my-seat`. Screen 18 renders the **full hall grid** from the
**same** `SessionSeatMap` (`RowLabels`, `SeatsPerRow`, `ReservedCells`, `MyCell`).
So one `GET …/seats` call serves both: screen 17's one-line card (`MyCell`) and
screen 18's grid. The app caches the seat map across the 17 → 18 hop.

## L-5 The two functions are client-local (no API)
Per the Screen Guide both CTAs are **system / on-device** actions:
- **`أضف إلى تقويمي` (add to calendar)** — "→ device calendar (system action)".
  The app builds a single calendar event from the cached session
  (title = `Title|TitleArabic`, start = `StartUtc`, end = `EndUtc`,
  location = the hall name) and hands it to the OS add-event intent. **No server
  call** — all fields are already cached, so it works offline. (Contrast Page_014,
  whose `.ics` aggregates **many** server-side sources and therefore needs a
  server build.)
- **`تذكير` (reminder)** — "→ schedules a local push notification before the
  session starts". A **local notification** scheduled on-device at
  `StartUtc − lead-time`. **No server call.** (This is independent of the
  server-side automated session-reminder worker, D-217, which is a separate
  back-office concern.)

## L-6 Speakers (incl. country flag + photo — D-271)
The speaker cards mirror the sessions-list/detail: **active** speakers only, ordered
by `DisplayOrder` (0 = primary), each with name (AR/EN), rank (`Title`) and role
(`Speaker` / `Host` — D-225, so the mockup's `المضيف` host marker renders). Each
card opens **Speaker profile (20)** at `/speakers/:speakerId`.

Each `PublicSessionSpeaker` now **also carries (append-only, D-219 / D-271)** the
speaker's country + photo, surfaced on **both** the sessions list
(Page_016_Logic L-6) and this detail:

| Field | Type | Drives the card |
|-------|------|-----------------|
| `CountryId` | `int?` | the speaker's **country flag** — the client renders the flag **from `CountryId`** |
| `CountryNameEn` / `CountryNameAr` | `string?` | the country **label / fallback** (tooltip + a no-flag text fallback) |
| `PhotoRelativePath` | `string?` | the speaker **avatar** (the card's round photo; placeholder when null) |

The **flag is rendered from `CountryId`** (names are label/fallback only) and the
**avatar from `PhotoRelativePath`**; all four are nullable. Because the detail draws
from the same cached payload as the list, these need no extra fetch. Covered by
`tests/SIMF.Api.Tests/ProgrammeSessionsTests.cs.Session_speaker_carries_country_flag_and_photo`.

## L-7 Edge cases
- **Session soft-deleted / missing** → the detail endpoint returns **404**
  (`SessionNotFound`); the screen shows a "not found / removed" state. (The agenda
  list also drops it, so a stale cached tap is the main way to reach 404.)
- **No description** → the description section is omitted (body is optional).
- **No speakers** → the speakers section is empty (the wire array is empty, never
  null).
- **Speaker with no country / no photo** → `CountryId` / `PhotoRelativePath` null
  → no flag (name fallback) / placeholder avatar (D-271).
- **No category** → the type tag is hidden.
- **No seat layout for the hall** → the my-seat card still renders from `MyCell`
  if the caller has a reservation (the card needs only row+seat, not the layout).

## L-8 Localization
Arabic primary (RTL), English secondary; bilingual data is paired
(`Title`/`TitleArabic`, `HallName`/`HallNameArabic`,
`CategoryName`/`CategoryNameArabic`, speaker `Name`/`NameArabic`, country
`CountryNameEn`/`CountryNameAr`). Times are UTC on the wire, rendered in the device
tz. The seat card row/seat are locale-neutral (`dir="ltr"` on the row letter +
number inside the Arabic phrase, per the mockup); the speaker **flag** (from
`CountryId`) and **avatar** (`PhotoRelativePath`) are locale-neutral graphics.

## L-9 Related session surfaces — questions, live + recording, comments (D-271 cross-refs)
The session detail is the hub for several adjacent session surfaces. None of them
are built **on** screen 17 — it links/relates to them — but they are documented
here so the contract is discoverable from one place:

- **Live questions → screen 26 (Q&A).** Submitting a question for this session
  (`POST /api/v1/app/sessions/{id}/questions`, `RequireApprovedAccount`) is gated
  by **three** rules: (1) **arrival** at the hall (when the hall has a geofence the
  attendee needs a `HallAttendance` arrival record, else the `IsAtVenue`
  self-assert — D-242), (2) the window **opens 5 minutes before** `StartUtc`
  (`PreStartWindow = 5 min`), and (3) it **closes at** `EndUtc`
  (`PostEndWindow = 0`). Outside the window the API returns **400
  `SESSION_NOT_LIVE_FOR_QUESTIONS`**. Full contract: **screen 26** (Q&A), tests in
  `SessionQuestionsTests`. Screen 17 only deep-links into it.
- **Live broadcast + recording → screen 25 (Live / session player).** When this
  session has a **live broadcast**, the detail's `LiveStreamUrl` is non-null (the
  app shows the LIVE player + badge); a null `LiveStreamUrl` means
  recorded/scheduled. `LiveSignLanguageUrl` is the optional sign-language feed (the
  live screen's لغة الإشارة toggle). Recorded sessions stream via the **token-gated
  recording** endpoint (`HasRecording` on `PublicSessionDetail`, D-232); the **AI
  session summary (محضر)** is `GET /api/v1/app/programme/sessions/{id}/summary`
  (D-237/238, anonymous, gated by the summary's `PublishedAt`). Full contract:
  **screen 25** (Live / player). See Page_017_API E2.
- **Audience comments — two-stage pipeline.** Comments pass **(1)** an AI filter on
  submit (`ICommentAiFilter` stub → the comment lands **Approved** or **Pending**)
  then **(2)** admin **approve / hide** in the CP `CommentsModerationList`
  (`/admin/comments-moderation`). **The standalone app comments screen (28) is
  removed** per the updated mockup — comments now surface **inside** the
  session / live screen (screen 25), not on a separate screen. No build change on
  screen 17.
