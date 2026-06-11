# Page 015 — API (الخريطة · Venue map)

Authoritative backend contract for this page. Inherits the `ApiResult<T>` envelope,
headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001 §3–§4. Page
rules are in [Page_015_Logic.md](Page_015_Logic.md).

> **Status:** **built.** `GET /app/venue-map` is D-230 (FR-605);
> `GET /app/booths` + `/app/booths/{id}` are D-199. All three are **public,
> read-only** reads — no schema change, no enum change, no migration on this page.
> The **Flutter screen is built** (D-298) and binds to the camelCase field names
> below (the booth field names were corrected from an earlier `nameEn/nameAr` draft).
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** — so the routes
> below resolve to `GET /api/v1/app/venue-map`, `/api/v1/app/booths`,
> `/api/v1/app/booths/{id}`. (Source: `Get("/app/venue-map")` /
> `Get("/app/booths")` in `src/Backend/SIMF.Api/Endpoints/Public/`.)

## E1 — `GET /app/venue-map`  (the 2D node list)
| | |
|---|---|
| Full route | `GET /api/v1/app/venue-map` |
| Source | `PublicVenueMapEndpoint` (`Endpoints/Public/PublicVenueMapEndpoints.cs`) |
| Access | **Public** — `AllowAnonymous()`, `Tags("Public")`. No token, no permission code. |
| App privilege | Guest and above |
| Returns | `ApiResult<IReadOnlyList<PublicVenueMapNode>>` |

```jsonc
// PublicVenueMapNode  (src/Shared/SIMF.Contracts/Programme/PublicVenueMap.cs)
{
  "id":         "guid",     // node id
  "label":      "string",   // EN label
  "labelArabic":"string",   // AR label
  "kind":       0,          // VenueMapNodeKind: 0 Hall · 1 Zone · 2 Booth · 3 PointOfInterest
  "x":          0.0,        // double — map design-space X
  "y":          0.0,        // double — map design-space Y
  "hallId":     "guid?",    // set on Hall (and possibly Booth) nodes — bare Guid, NO hall name (D11)
  "boothId":    "guid?"     // set on Booth nodes → matches PublicBoothSummary.Id
}
```
Envelope: `{ "success": true, "data": [ ...nodes ], "error": null }`.
Returns the **full** active node set in one call — no paging, no viewport query
(Logic L-2).

## E2 — `GET /app/booths`  (booth lookup for popups)
| | |
|---|---|
| Full route | `GET /api/v1/app/booths` |
| Source | `ListPublicBoothsEndpoint` (`Endpoints/Public/PublicBoothEndpoints.cs`) |
| Access | **Public** — `AllowAnonymous()`, `Tags("Public")` |
| App privilege | Guest and above |
| Returns | `ApiResult<IReadOnlyList<PublicBoothSummary>>` |

```jsonc
// PublicBoothSummary  (src/Shared/SIMF.Contracts/Exhibition/BoothContracts.cs)
// FIELD NAMES corrected (D-298): the shipped DTO is Name/NameArabic/ExhibitorName/
// Sector (NOT nameEn/nameAr) → camelCase JSON below. An earlier draft was wrong.
{
  "id":                  "guid",
  "code":                "string",
  "name":                "string",
  "nameArabic":          "string",
  "exhibitorName":       "string?",
  "exhibitorNameArabic": "string?",
  "sector":              "string?",
  "sectorArabic":        "string?",
  "hallId":              "guid?",   // bare Guid — NO hall name ships (D11)
  "mapX":                0.0,        // double? — booth's own map coordinates
  "mapY":                0.0         // double?
}
```
> **No `logoUrl` field exists** on this DTO — a booth logo in the popup is
> **decoration** (D11 / Logic L-6).

## E3 — `GET /app/booths/{id}`  (booth detail — description paragraph)
| | |
|---|---|
| Full route | `GET /api/v1/app/booths/{id:guid}` |
| Source | `GetPublicBoothEndpoint` (`Endpoints/Public/PublicBoothEndpoints.cs`) |
| Access | **Public** — `AllowAnonymous()`, `Tags("Public")` |
| App privilege | Guest and above |
| Returns | `ApiResult<PublicBoothDetail>` |

```jsonc
// PublicBoothDetail  (src/Shared/SIMF.Contracts/Exhibition/BoothContracts.cs)
{
  "id":                  "guid",
  "code":                "string",
  "name":                "string",
  "nameArabic":          "string",
  "exhibitorName":       "string?",
  "exhibitorNameArabic": "string?",
  "sector":              "string?",
  "sectorArabic":        "string?",
  "description":         "string?",  // ← the extra field over the summary
  "descriptionArabic":   "string?",
  "hallId":              "guid?",     // bare Guid — NO hall name (D11)
  "mapX":                0.0,
  "mapY":                0.0
}
```

### Errors (E3)
| HTTP | Code | When |
|------|------|------|
| 404 | `BOOTH_NOT_FOUND` | id is unknown or the booth is inactive — `ApiException(ErrorCodes.BoothNotFound, 404, "The booth was not found.", "لم يتم العثور على الجناح.")` |

Standard transport / envelope errors (network, 500) per SIMF-API-001 apply to all
three reads. None of the three requires a request body or query parameters
(beyond the `{id}` route value on E3).

## Reused / related
- The same `GET /app/booths` list feeds the **Exhibition** page (Mockup page 22)
  — this map page reuses it, no contract change.
- No write endpoints on this page. CP-side node placement (admin venue-map CRUD,
  D-230) is out of scope here.

## TO BUILD on this page
- **None for rendering.** The map + booth popup are fully served by E1–E3 above.
- **Hall-node → agenda deep link (Logic L-7) — TO BUILD:** depends on the agenda
  screen accepting a hall filter; **no new endpoint** is needed for the map.
- **Booth logo / hall-name fields (D11 / Logic L-6):** **not in the contract.**
  Adding either would be a new DTO field requiring **owner approval** (freeze).
