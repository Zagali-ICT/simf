# Page 016 — API (الجلسات · Sessions)

Authoritative backend contract for this page. Inherits the `ApiResult<T>` envelope,
headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001 §3–§4. The
counter/filter rules are in [Page_016_Logic.md](Page_016_Logic.md).

> **Status:** **BUILT** (API) · **Flutter screen built (D-299).** The list
> (`GET /app/programme/sessions`) shipped in D-199, was **enriched in D-252** so the
> cached payload also carries the body + the ordered speaker cards, and was
> **further enriched in D-271** so each speaker also carries its **country (id +
> EN/AR name) + photo**. The detail (`GET /app/programme/sessions/{id}`) is
> unchanged on this page. Both `AllowAnonymous`. Covered by
> `tests/SIMF.Api.Tests/ProgrammeSessionsTests.cs` (incl.
> `Session_speaker_carries_country_flag_and_photo`). The Flutter screen + the
> tolerant int-enum decode are covered by
> `src/Mobile/simf_app/test/features/sessions/` (D-299).
>
> **Rename (D-271):** the screen is renamed **الأجندة · Agenda → الجلسات ·
> Sessions** (title + nav label + pills). The **API route is unchanged**
> (`/app/programme/sessions`) — the rename is UI-only.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split,
> D-247) — so the routes below are `GET /api/v1/app/programme/sessions` etc.

## E1 — `GET /app/programme/sessions`  (the full programme)  **(BUILT — enriched D-252)**
| | |
|---|---|
| Full route | `GET /api/v1/app/programme/sessions` (optional `?day=yyyy-MM-dd`) |
| Access | **`AllowAnonymous`** — guest / not-logged-in (Screen Guide Journey C) |
| Returns | `ApiResult<PublicSessions>` — **the whole active programme**, time-ordered |
| Caching | The app fetches this **once with no `day`** and caches it; the UI filters inline (Page_016_Logic L-1). `?day=` exists for thin clients but the app does not need it. |

```jsonc
// PublicSessions = { "items": PublicSessionListItem[] }
// PublicSessionListItem  (fields appended in D-226/D-231/D-252 are append-only, D-219)
{
  "id":            "guid",
  "code":          "string",   // Session.Code
  "title":         "string",
  "titleArabic":   "string",
  "hallId":        "guid",
  "hallName":      "string",
  "hallNameArabic":"string",
  "startUtc":      "2026-11-03T06:00:00Z",
  "endUtc":        "2026-11-03T07:00:00Z",
  "primaryThemeName":      "string?",   // theme chip
  "primaryThemeNameArabic":"string?",
  "primaryThemeColor":     "string?",
  "categoryId":        "guid?",   // ← the "is main session / type" tag (SessionCategory, D-226)
  "categoryName":      "string?", //   e.g. "Main Session"
  "categoryNameArabic":"string?", //   e.g. "جلسة رئيسية"
  "status":            0,           // int! SessionStatus 0=Scheduled 1=Held 2=Recorded 3=Published (wire is int — D-299)
  // --- added D-252 so the cached payload also drives the detail/preview ---
  "description":        "string?",  // body
  "descriptionArabic":  "string?",
  "speakers": [                      // ordered speaker cards (active only)
    {
      "id": "guid",
      "name": "string",
      "nameArabic": "string",
      "title": "string?",   // the speaker's rank/role (e.g. "Chief Scientist")
      "displayOrder": 0,
      "role": 0,            // int! SessionSpeakerRole 0=Speaker 1=Host (wire is int — D-225/D-299; the mockup's "host" marker)
      // --- added D-271 (append-only, D-219): country flag + photo on the speaker ---
      "countryId":     null,   // int? → the client renders the FLAG from this id
      "countryNameEn": null,   // string? → country label / no-flag text fallback
      "countryNameAr": null,   // string?
      "photoRelativePath": null // string? → the speaker AVATAR image (null → placeholder)
    }
  ]
}
```

### Notes
- **Enum wire format = int (D-299).** `status` (`SessionStatus`) and the speaker
  `role` (`SessionSpeakerRole`) serialise as **integers**, not their names — there
  is no `JsonStringEnumConverter` anywhere in `SIMF.Api` (same as the venue-map
  `kind`, D-298). An earlier draft of this sample showed `"status":"Scheduled"` /
  `"role":"Speaker"`; corrected with D-299. The Flutter client decodes **tolerantly**
  (accepts int **or** name; unknown → a safe default) so a future converter flip is
  non-breaking.
- **Full programme, cacheable, guest-allowed** — exactly the owner directive; the
  Upcoming/Forum pills, day strip and search are **client-side** over the cache.
- **"is main session / type" = `category*`** (SessionCategory, D-226) — "Main
  Session" is a seeded category value, not a boolean (Page_016_Logic L-4). Null
  until the team seeds the category list.
- `speakers` is always an array (empty when none) — never null on the wire.
- **Speaker country + photo (D-271)** — each `PublicSessionSpeaker` carries
  `countryId` (int?), `countryNameEn` / `countryNameAr` (string?) and
  `photoRelativePath` (string?). The client renders the **flag from `countryId`**
  (names are the label/fallback) and the **avatar from `photoRelativePath`**. All
  four are nullable and **append-only** (D-219). They appear on **both** this list
  and the session detail (Page_017) — `Session_speaker_carries_country_flag_and_photo`.

## E2 — `GET /app/programme/sessions/{id}`  (session detail, screen 17)  **(BUILT, D-199)**
| | |
|---|---|
| Full route | `GET /api/v1/app/programme/sessions/{id:guid}` |
| Access | `AllowAnonymous` |
| Returns | `ApiResult<PublicSessionDetail>` — title + abstract, hall, time, themes, **ordered speakers**, category, a seat-availability summary, and a `hasRecording` flag. 404 when missing / soft-deleted. |

The detail carries everything the list does plus the seat summary, themes and the
recording flag. With D-252 the **app can preview a session from the cached list**;
it still calls the detail when it needs the live seat count / recording state. The
detail's `speakers[]` carry the **same** D-271 country + photo fields as the list
(append-only) — see Page_017_API E2.

## Error responses
| HTTP | When |
|------|------|
| 400 | `?day=` is not `yyyy-MM-dd` (`SessionInvalid`) |
| 404 | (detail) session missing / soft-deleted (`SessionNotFound`) |

## Build dependencies
None outstanding. The D-252 + D-271 enrichments are **additive over existing
tables** — no schema change, no migration, append-only wire (D-219). The speaker
country + photo is covered by
`tests/SIMF.Api.Tests/ProgrammeSessionsTests.cs.Session_speaker_carries_country_flag_and_photo`.
