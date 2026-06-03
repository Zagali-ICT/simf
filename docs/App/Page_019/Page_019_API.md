# Page 019 — API (المتحدثون · Speakers list)

Authoritative backend contract for this page. Inherits the `ApiResult<T>`
envelope, headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001
§3–§4. The ordering / status rules are in [Page_019_Logic.md](Page_019_Logic.md).

> **Status:** **BUILT — no new API (reuse, D-199).** The whole list comes from the
> existing anonymous `GET /app/speakers` (`PublicSpeakers`, D-199). There is **no**
> new endpoint, schema change, migration or permission for this page.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split,
> D-247) — so the route below is `GET /api/v1/app/speakers`.

## E1 — `GET /app/speakers`  (the speakers list)  **(BUILT — D-199)**
| | |
|---|---|
| Full route | `GET /api/v1/app/speakers` |
| Access | **`AllowAnonymous`** — guest+, no sign-in (D-199) |
| Returns | `ApiResult<PublicSpeakers>` — the ordered list of speaker summaries |
| Ordering | by `displayOrder` ascending, then `name` |

```jsonc
// PublicSpeakers
{
  "items": [                                    // ← one sp-card per entry, ordered
    {
      "id": "guid",
      "name": "...",            "nameArabic": "...",        // ← card name
      "rank": "القبطان البحري",                              // ← card rank line
      "countryId": 682,                                       // ← ISO numeric (int?), optional
      "countryNameEn": "...",   "countryNameAr": "...",      // ← optional country label
      "photoRelativePath": "speakers/....jpg",               // ← card avatar (placeholder if empty)
      "displayOrder": 0                                       // ← list order
    }
  ]
}
```

`PublicSpeakerSummary` is a **summary** projection: it carries only what the card
needs (id, names, rank, country, photo, order). The bio / CV tabs, social URLs,
`allowsMeetingRequests` and the sessions list are **not** here — they load on the
**profile** (20) via `GET /app/speakers/{id}` (see [Page_020](../Page_020/README.md)).

## E2 — tap-through is a client navigation (no endpoint)
| Action | Behaviour | Server call |
|---|---|---|
| Tap a card / `المزيد` | screen-navigate to **Speaker profile (20)** for `summary.id` (`/speakers/:speakerId`) | **none** here — the profile does its own `GET /app/speakers/{id}` |

## Error responses
| HTTP | When |
|------|------|
| 200 | success — `items` may be empty (no speakers) |
| 5xx | server error — the list shows its error / retry state |

(No 401/403/404 path on this read: it is anonymous and not id-scoped — an empty
list is a `200` with `items: []`, not a 404.)

## Build dependencies
**None.** The endpoint exists and is tested
(`tests/SIMF.Api.Tests/PublicSpeakersTests.cs` — the list read + ordering, and the
`{id}` detail read used by the profile). No schema change, no migration, no new
permission — this page is a **reuse** of the shipped public-speakers surface
(D-199). E2E catalogue: [`docs/tests/e2e/mobile-speakers.md`](../../tests/e2e/mobile-speakers.md).
