# Page 024-01 — API (تفاصيل النسخة · Past-edition detail)

Authoritative backend contract for this page. Inherits the `ApiResult<T>` envelope,
headers, error model from SIMF-API-001 + SIMF-MOB-API-001 §3–§4. The visibility +
deferred-list rules are in [Page_024-01_Logic.md](Page_024-01_Logic.md).

> **Status:** **BUILT — NEW endpoint (D-273).** One anonymous read returns the whole
> detail. The four new scalars (`locationEn/Ar`, `dateLabelEn/Ar`) ship as additive
> nullable columns on `ArchiveEditions` (migration `App/D273_AddArchiveEditionLocationDate`).
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split,
> D-247) — so the route below is `GET /api/v1/app/archive/{id}`.

## E1 — `GET /app/archive/{id}`  (the edition detail)  **(NEW — D-273)**
| | |
|---|---|
| Full route | `GET /api/v1/app/archive/{id:guid}` |
| Access | **`AllowAnonymous`** — public (no token), like the Archive list |
| Gate | archive-visibility operations toggle (D-166) — see L-2 |
| Returns | `ApiResult<PublicArchiveEditionDetail>` |

```jsonc
// PublicArchiveEditionDetail
{
  "id": "guid",
  "year": 2024,
  "titleEn": "Saudi International Maritime Forum 2024",
  "titleAr": "الملتقى البحري السعودي الدولي 2024",
  "summaryEn": "An international platform that gathered naval leaders…",   // nullable
  "summaryAr": "نبذة: منصة دولية جمعت قادة القوات البحرية…",                // nullable
  "locationEn": "Riyadh · Riyadh Front",                                   // nullable (NEW D-273)
  "locationAr": "الرياض · واجهة الرياض",                                   // nullable (NEW D-273)
  "dateLabelEn": "November 2024 · 3 days",                                 // nullable (NEW D-273)
  "dateLabelAr": "نوفمبر 2024 · 3 أيام",                                   // nullable (NEW D-273)
  "attendees": 375,
  "sessions": 30,
  "speakers": 250,
  "coverImageRelativePath": "archive/simf2024.png"                         // nullable
}
```

The gallery / session-titles / past-speakers lists the mockup sketches are **not**
in the payload — they are deferred (Page_024-01_Logic L-3, §9 / D-273). When modelled
they append to this DTO (append-only, D-219).

## Error responses
| HTTP | Code | When |
|------|------|------|
| 404 | `archive_edition_not_found` | the archive is hidden (toggle off), **or** the edition id is unknown / soft-deleted (`IsActive == false`) — a single 404 surface (L-2) |
| 5xx | — | server fault (generic envelope) |

There is **no** 401 / 403 — the read is anonymous.

## Admin authoring (context, not this page)
The edition's data is authored from the CP **`/admin/archive`** page (D-199); the
new place + date-label fields were added there in the same change (D-273). Admin CRUD
routes (all `Administrator`-gated, `Archive.*` permissions):
`POST /admin/archive/list` · `GET/POST /admin/archive` · `PUT/DELETE /admin/archive/{id}`
· `PUT /admin/archive/visibility`.

## Build dependencies
**None outstanding.** The endpoint, the service (`PublicArchiveService.GetAsync`), the
DTO, the four additive columns and the migration are built and tested
(`tests/SIMF.Api.Tests/ArchiveTests.cs` — public detail returns the edition when
visible, 404 for unknown id, 404 when visibility is off;
`tests/SIMF.Api.Tests/AdminArchiveTests.cs` — place + date-label round-trip through
CRUD). No new permission (the read is anonymous; admin reuses the existing
`Archive.*` permissions).
