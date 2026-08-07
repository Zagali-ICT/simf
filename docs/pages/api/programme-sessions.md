# Public programme list — `GET /api/v1/app/programme/sessions`

| | |
|--|--|
| **Route** | `GET /api/v1/app/programme/sessions` |
| **Surface** | Public API (anonymous) |
| **Consumers** | Website `/programme`, mobile agenda screen, the AI assistant's context builder |
| **Auth** | `AllowAnonymous` |
| **Caching** | `CacheOutput("PublicRead")` — 45 s, varies by **all** query keys |
| **Source** | `src/Backend/SIMF.Api/Endpoints/Programme/PublicSessionEndpoints.cs` · `src/Backend/SIMF.Infrastructure/Programme/ProgrammeSessionService.cs` |
| **Tests** | `tests/SIMF.Api.Tests/ProgrammeSessionsTests.cs` · E2E [`api-programme-category-filter.md`](../../tests/e2e/api-programme-category-filter.md) |
| **Last reviewed** | 2026-07-31 |

## Purpose

The one read the agenda is built from: every active session, ordered by start time,
with its hall, its primary theme, its speakers and a cheap seat-availability summary.
Anonymous, because the programme is public marketing content — a visitor decides
whether to register by reading it.

## Query parameters

| Name | Type | Meaning |
|---|---|---|
| `day` | `yyyy-MM-dd`, optional | One **event-local (+03:00)** calendar day. Drives the agenda's Day 1/2/3 control. A6c: the window is half-open `[dayStart, nextDayStart)` at +03:00, matching `ProgrammeDay.Date` and the day-grouped agenda, so the flat list and the day strip agree at the UTC-midnight edge. |
| `categoryId` | `Guid`, optional | **OA-D6.** One `SessionCategory` (the dynamic D-226 lookup). Server-side track filter. |

The two combine with AND. Both omitted returns the whole programme.

## Behaviour worth knowing

- **A malformed `day` is a 400** (`SESSION_INVALID`); a `categoryId` that matches
  nothing is a **200 with an empty list**, never a 404. The asymmetry is deliberate: a
  404 would let an anonymous caller enumerate which category ids exist, whereas a
  malformed date is a client bug with nothing to leak.
- **The cache needed no change** for `categoryId`. `CacheOutput("PublicRead")` varies
  by every query key, so each filter combination keys its own entry. E2E-PCF-005 proves
  it rather than assuming it.
- **`SessionCategory` ships empty** pending the client's category list (D-226, open
  item OI-2). Until it is seeded, requests without `?categoryId=` behave exactly as
  before and requests with one return nothing. That is correct, not a defect.
- Only `IsActive` sessions are returned; times are stored UTC and rendered through the
  display seam by the client.

## Related endpoints

| Route | Purpose |
|---|---|
| `GET /app/programme/days` | The day-grouped agenda (day banner + day strip) |
| `GET /app/programme/sessions/{id}` | Full public detail for one session |
| `GET /app/programme/sessions/{id}/summary` | The published محضر, when there is one |
