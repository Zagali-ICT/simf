# Page 002 — API (التهيئة · Onboarding)

Backend contract for this screen.

> **This screen has NO SIMF API.** Owner decision: the onboarding sequence (loading image
> + three intro videos) is **first-run-only**, **client-side**, and serves its media from
> **bundled assets / external YouTube** — it makes **no call to any `/api/v1/app/*`
> endpoint**, sends no auth header, and reads/writes no SIMF table. The first-run flag is a
> **local-storage** boolean (see [Page_002_Logic.md](Page_002_Logic.md) L-1).

## Endpoints used by this screen
**None.** There is intentionally no `GET`, `POST`, `PUT`, or `DELETE` under
`/api/v1/app/*` for this page.

| Method + route | Access / policy | Request | Response | Status |
|----------------|-----------------|---------|----------|--------|
| — (no endpoint) | — | — | — | **N/A — screen has no SIMF API** |

## Media sources (not SIMF endpoints)
The three intro videos and the loading image are content, not API resources:

| Logical name | Source | Notes |
|--------------|--------|-------|
| `introd_loading` | bundled asset | shown first while video 1 buffers |
| `introd_001` | **YouTube** (preferred) → bundled fallback | intro video 1 |
| `introd_002` | **YouTube** (preferred) → bundled fallback | intro video 2 |
| `introd_003` | **YouTube** (preferred) → bundled fallback | intro video 3 |

YouTube is an **external** player, not a SIMF backend. There is no SIMF `ApiResult<T>`
envelope, no SIMF error code, and no SIMF auth involved on this screen.

## Optional future remote swap (NOT built)
If the client ever wants the media list / URLs to be swappable without an app release, the
**existing generic** content endpoint could supply the ordered list of intro media by key —
without adding any new endpoint:

| Method + route | Access / policy | Request | Response | Status |
|----------------|-----------------|---------|----------|--------|
| `GET /api/v1/app/content/{key}` | Anonymous (Guest; public content) | path `key` (e.g. `onboarding-intro`) | `ApiResult<T>` per SIMF-API-001 — a content payload listing the ordered media (`introd_001..` names + URLs) | **(TO BUILD — optional, NOT wired)** |

> `GET /app/content/{key}` is referenced as the **only** seam that would avoid a new
> endpoint. It is **not consumed by this screen today** and is **out of scope** until the
> owner asks for a remote-swappable onboarding. Verify the exact `ApiResult<T>` shape of
> that endpoint before wiring it; do not assume.

## Summary
- **This screen has no SIMF API.** ✅
- Media via **bundled assets + external YouTube**, addressed by **stable names** `introd_001..`.
- First-run state is **local**, not server-side.
- The **only** possible future API is reusing the existing `GET /app/content/{key}` — marked **(TO BUILD)**, optional, not wired.
